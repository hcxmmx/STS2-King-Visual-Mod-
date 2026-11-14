"""Recover the three existing shields and the original procedural parry tiles."""
from pathlib import Path
import json


def prepare_shields(assets, generated, database):
    # Atlas is shared with the weapon importer, including full-canvas offsets.
    from prepare_extra_weapons import Atlas

    body_atlas = Atlas(assets, "king")
    fx_atlas = Atlas(assets, "fxCommon")
    tracks = json.loads((assets / "atlas/king_tracks.json").read_text(encoding="utf-8-sig"))
    tracks = {name.lower(): value for name, value in tracks.items()}
    windows = {row["animId"].lower(): row["modes"] for sheet in database["sheets"]
               if sheet["name"] == "backFrames" for row in sheet["lines"]}
    animations, anchors, fronts, variants = {}, {}, {}, []
    for shield, item, sound in [
        ("DashShield", "DashShield", "weapon_shield_block"),
        ("IceShield", "IceShield", "weapon_iceshield_hit"),
        ("LightningShield", "ThunderShield", "weapon_shield_block"),
    ]:
        variant = {"id": item}
        for phase, source in [("hold", "block" + shield), ("end", "blockEnd" + shield)]:
            name = "KingShield" + shield + phase.title()
            variant[phase] = name
            paths = []
            for index, picture in body_atlas.frames(source):
                target = generated / "ShieldAssets" / source / f"{index:04}.png"
                target.parent.mkdir(parents=True, exist_ok=True)
                picture.save(target)
                resource = "res://Generated/" + target.relative_to(generated).as_posix()
                paths.append(resource)
                points = tracks[source.lower()]["headBone"]
                anchors[resource] = [points[index * 3] - picture.width / 2,
                                     points[index * 3 + 1] - picture.height / 2]
                fronts[resource] = any(w["mode"] == 0 and w["startFrame"] <= index <= (w["endFrame"] or 999)
                                       for w in windows.get(source.lower(), []))
                if phase == "hold":
                    # Procedural source FX uses entity-relative coordinates. The
                    # imported canvas is foot-centred, so anchor at its torso.
                    variant["fx_origin"] = [0.0, tracks[source.lower()]["Bip001 Spine2"][1] - picture.height / 2]
            animations[name] = {"node": "Body", "fps": 30.0, "frames": paths}
        target = generated / "ShieldAssets" / (sound + ".ogg")
        audio = (assets / "sfx/weapon" / (sound + ".wav")).read_bytes()
        assert audio[:4] == b"OggS"
        target.write_bytes(audio)
        variant["sound"] = "res://Generated/" + target.relative_to(generated).as_posix()
        variants.append(variant)
    tiles = {}
    for name in ["fxParryLine", "fxLineWhite"]:
        picture = fx_atlas.frames(name)[0][1]
        target = generated / "ShieldAssets" / (name + ".png")
        picture.save(target)
        tiles[name] = "res://Generated/" + target.relative_to(generated).as_posix()
    hero = next(row for sheet in database["sheets"] if sheet["name"] == "hero"
                for row in sheet["lines"] if row["id"] == "Beheaded")
    hurt_source = assets / hero["sounds"]["dmg"]
    hurt_audio = hurt_source.read_bytes()
    assert hurt_audio[:4] == b"OggS"
    hurt_target = generated / "ShieldAssets" / (hurt_source.stem + ".ogg")
    hurt_target.write_bytes(hurt_audio)
    # Source BaseShield uses a 0.2 s parry window. STS has discrete incoming hits,
    # so the hold is finite and followed by the original nine-frame release.
    data = {"variants": variants, "tiles": tiles, "hold_original": 0.2, "hold_fast": 0.12,
            "recovery_fps": 30.0, "impact_color": "699ce9", "start_color": "445e7d",
            "hurt_sound": "res://Generated/" + hurt_target.relative_to(generated).as_posix()}
    print(f"SHIELD_ASSETS_READY: {len(variants)} shields, 30 body frames, two original particle tiles")
    return data, animations, anchors, fronts


if __name__ == "__main__":
    raise SystemExit("Run prepare_battle_preview.py or prepare_game_presentation.py")
