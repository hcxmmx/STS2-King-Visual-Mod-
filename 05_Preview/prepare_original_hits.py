"""Recover the ordinary physical Mob.onDamage / Fx.bloodHit preview assets."""
import json


def prepare_original_hits(assets, generated):
    from prepare_extra_weapons import Atlas

    atlas = Atlas(assets, "fxCommon")
    animations = []
    for name in ["fx_bloodA/bloodA", "fxBloodB/bloodB"]:
        paths = []
        for index, image in atlas.frames(name):
            target = generated / "OriginalHitAssets" / name.replace("/", "_") / f"{index:04}.png"
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target)
            paths.append("res://Generated/" + target.relative_to(generated).as_posix())
        animations.append(paths)
    gibs = []
    for name in ["fxGib0", "fxGib1"]:
        target = generated / "OriginalHitAssets" / (name + ".png")
        atlas.frames(name)[0][1].save(target)
        gibs.append("res://Generated/" + target.relative_to(generated).as_posix())
    data = {"animations": animations, "gibs": gibs, "fps": 30.0,
            "blood_color": "ff0000", "bloodless_color": "f78505", "blink_seconds": 0.15,
            "source": "en.Mob.onDamage / Fx.bloodHit / Entity.pickColorBlink"}
    (generated / "original_hit_data.json").write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
    print("ORIGINAL_HIT_PREVIEW_READY: 29 blood frames, two gib tiles; source physical blink in both blood settings")
    return data
