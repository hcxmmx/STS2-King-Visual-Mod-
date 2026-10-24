"""Build preview-only original weapon timing/audio; never change the installed mod."""
from pathlib import Path
import json
import re
import shutil
import struct

from PIL import Image

from prepare_preview import prepare

# Retain the existing project's five alternative FX choices explicitly. They
# require a separate special-weapon/variant review, rather than pretending these
# are exact data.cdb pairs. All other native pairs must match the source table.
PROJECT_FX_VARIANTS = {
    ("AtkKatanaB4", "fxBeheadedKatanaB", "fxBeheadedKatanaB4"),
    ("atkPanA", "fxPanHitA", "fxAtkPanA"),
    ("atkPanB", "fxPanHitB", "fxAtkPanB"),
    ("atkPanC", "fxPanHitC", "fxAtkPanC"),
    ("atkPanD", "fxPanHitC", "fxAtkPanD"),
}


def main():
    root = Path(__file__).resolve().parents[1]
    assets = Path(r"D:\Dead Cells\ModTools\Unpacked_Assets")
    prepare(assets, root)
    generated = Path(__file__).parent / "Generated"
    data = json.loads((generated / "preview_data.json").read_text(encoding="utf-8"))
    database = json.loads((assets / "data.cdb").read_text(encoding="utf-8-sig"))
    rows = next(sheet["lines"] for sheet in database["sheets"] if sheet["name"] == "weapon")
    strikes = {}
    for row in rows:
        for index, strike in enumerate(row.get("strikeChain", [])):
            if strike.get("animId"):
                strikes.setdefault(strike["animId"].lower(), []).append((row["item"], index, strike))
    profiles = {}
    weapons = []
    skipped = []
    audit = []
    audio_dir = generated / "BattleAudio"
    audio_dir.mkdir(exist_ok=True)
    scene = (root / "03-godot-project/Hcxmmx_King/tscn/white_king.tscn").read_text(encoding="utf-8")
    fx_atlas = (assets / "atlas/fxWeapon.atlas").read_bytes()
    fx_pages = {}
    for weapon in data["weapons"]:
        if any(len(strikes.get(stage["body"].lower(), [])) != 1 for stage in weapon["stages"]):
            skipped.append(weapon["id"])
            continue
        for stage in weapon["stages"]:
            item, source_index, strike = strikes[stage["body"].lower()][0]
            source_fx = strike.get("fxId")
            variant = source_fx != stage["vfx"]
            if variant and (stage["body"], source_fx, stage["vfx"]) not in PROJECT_FX_VARIANTS:
                raise ValueError(f"Unreviewed FX mapping: {stage['body']}: source={source_fx}, project={stage['vfx']}")
            profile = {
                "source_item": item, "charge": strike["charge"], "hit_frame": strike["hitFrame"],
                "animation_speed": abs(strike.get("animSpd", 1)), "reverse": strike.get("animSpd", 1) < 0,
                "lock_after": strike["lockCtrlAfter"], "cooldown": strike["coolDown"],
                "vfx": stage["vfx"], "audio": {},
                "source_fx": source_fx, "source_strike_index": source_index,
            }
            # Original OldSkill uses 60-Hz counters and doubles the atlas hit-frame number.
            # Entity animation genSpeed=.5 => 30 atlas frames/s. Animation starts when
            # remaining charge <= doubled hitFrame; release/FX use their own charge threshold.
            profile["body_fps"] = 30.0 * profile["animation_speed"]
            profile["body_delay"] = max(0.0, strike["charge"] - strike["hitFrame"] / 30.0)
            fx_frame = strike.get("fxProps", {}).get("fxStartFrame", strike["hitFrame"])
            profile["fx_time"] = max(0.0, strike["charge"] - (strike["hitFrame"] - fx_frame) / 30.0)
            # This mod imported only the visible whip FX frames. Original FX starts
            # with transparent frames 0..5 / 0..9 / 0..16. Keep the original release
            # gate, but delay the trimmed visual sequence by the omitted frames.
            lead = 0
            if item == "VampireKiller":
                indices = re.findall(re.escape(stage["vfx"]) + r"_(\d+)-=-", scene)
                if not indices:
                    raise ValueError(f"Missing imported FX sequence: {stage['vfx']}")
                lead = min(map(int, indices))
                for index in range(lead):
                    name = f"fxAtkVampireKiller/{stage['vfx']}_{index:04}".encode("ascii")
                    offset = fx_atlas.index(name) + len(name)
                    page, x, y, width, height, *_ = struct.unpack_from("<9H", fx_atlas, offset)
                    if page not in fx_pages:
                        fx_pages[page] = Image.open(assets / f"atlas/fxWeapon-{page}.png").convert("RGBA")
                    assert fx_pages[page].crop((x, y, x + width, y + height)).getchannel("A").getextrema() == (0, 0), "Omitted FX frame is visible"
            profile["fx_lead_frames"] = lead
            profile["fx_visible_time"] = profile["fx_time"] + lead / profile["body_fps"]
            for key, field in (("release", "sfxRelease"), ("hit", "sfxHit"), ("charge", "sfxCharge")):
                if not strike.get(field):
                    continue
                source = assets / strike[field]
                if not source.exists():
                    raise FileNotFoundError(source)
                assert source.read_bytes()[:4] == b"OggS", source
                destination = audio_dir / (source.stem + ".ogg")
                shutil.copyfile(source, destination)
                profile["audio"][key] = {"path": "res://Generated/BattleAudio/" + destination.name, "volume_db": -6.0}
            profiles[stage["body"]] = profile
            audit.append({"weapon": weapon["id"], "source_item": item, "source_strike_index": source_index,
                          "body": stage["body"], "source_fx": source_fx, "project_fx": stage["vfx"],
                          "mapping": "project_variant_pending_review" if variant else "source_exact",
                          "source_anim_speed": strike.get("animSpd", 1), "trimmed_fx_lead": lead})
        weapons.append(weapon)
    data["weapons"] = weapons
    data["timing"] = profiles
    data["source_fps"] = 30.0
    data["spires_fast"] = {"attack_wait": 0.075, "damage_wait": 0.1, "interval": 0.175,
                           "card_id": "STARDUST", "card_name": "星尘", "damage": 5}
    data["skipped_custom_weapons"] = skipped
    (generated / "battle_data.json").write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    (generated / "weapon_source_audit.json").write_text(json.dumps({"source": str(assets / "data.cdb"), "stages": audit, "skipped_custom_weapons": skipped}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"SOURCE_MAPPING_AUDIT: {len(audit)} stages; {sum(row['mapping'] == 'source_exact' for row in audit)} exact FX pairs; explicit project variants require separate review")
    print(f"BATTLE_DATA_READY: {len(weapons)} native weapon chains, {len(profiles)} stages; custom composites excluded: {skipped}")


if __name__ == "__main__":
    main()
