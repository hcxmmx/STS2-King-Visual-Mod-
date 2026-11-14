"""Shared, explicit source resolution for original and project-variant weapon stages."""
from pathlib import Path
import json
import shutil


def load_sources(root: Path, assets: Path):
    database = json.loads((assets / "data.cdb").read_text(encoding="utf-8-sig"))
    rows = {row["item"]: row for sheet in database["sheets"] if sheet["name"] == "weapon" for row in sheet["lines"]}
    overrides = json.loads((root / "03-godot-project/Hcxmmx_King/Presentation/weapon_overrides.json").read_text(encoding="utf-8"))
    return rows, overrides


def resolve_stage(stage, rows, overrides):
    body = stage["body"]
    override = overrides["stages"].get(body, {})
    if stage.get("source_item"):
        override = {"item": stage["source_item"], "strike": stage["source_strike"]}
    if override:
        item, index = override["item"], override["strike"]
        strike = rows[item]["strikeChain"][index]
    else:
        matches = [(item, index, strike) for item, row in rows.items()
                   for index, strike in enumerate(row.get("strikeChain", []))
                   if strike.get("animId", "").lower() == body.lower()]
        if len(matches) != 1:
            raise ValueError(f"Explicit stage source required for {body}: {len(matches)} candidates")
        item, index, strike = matches[0]
    original_fx = strike.get("fxId")
    if stage.get("vfx") != original_fx and [body, original_fx, stage.get("vfx")] not in overrides["fx_aliases"]:
        raise ValueError(f"Unreviewed FX mapping: {body}: {original_fx} -> {stage.get('vfx')}")
    return item, index, strike, override


def timing_for(stage, item, index, strike, override):
    profile = {
        "source_item": item, "source_strike_index": index, "source_fx": strike.get("fxId"),
        "charge": strike["charge"], "hit_frame": strike["hitFrame"],
        "animation_speed": abs(strike.get("animSpd", 1)), "reverse": strike.get("animSpd", 1) < 0,
        "lock_after": strike["lockCtrlAfter"], "cooldown": strike["coolDown"],
        "vfx": stage.get("vfx"), "audio": {}, "adaptation": override.get("note", ""),
    }
    profile.update(override.get("timing", {}))
    profile["body_fps"] = 30.0 * profile["animation_speed"]
    profile["body_delay"] = max(0.0, profile["charge"] - profile["hit_frame"] / 30.0)
    fx_frame = strike.get("fxProps", {}).get("fxStartFrame", profile["hit_frame"])
    profile["fx_time"] = max(0.0, profile["charge"] - (profile["hit_frame"] - fx_frame) / 30.0)
    profile["release_time"] = profile["fx_time"]
    # NunchuckPan.dynOnAttackAnim creates FX with the body; dynOnFxFrame
    # only emits release audio. This is different from the generic weapon path.
    if item == "NunchuckPan":
        profile["fx_time"] = profile["body_delay"]
    return profile


def copy_audio(strike, assets, destination, resource_root, existing=None):
    destination.mkdir(parents=True, exist_ok=True)
    result = {}
    for kind, field in (("release", "sfxRelease"), ("hit", "sfxHit"), ("charge", "sfxCharge")):
        if not strike.get(field):
            continue
        source = assets / strike[field]
        assert source.read_bytes()[:4] == b"OggS", source
        target = destination / (source.stem + ".ogg")
        shutil.copyfile(source, target)
        cue = (existing or {}).get(kind, {})
        result[kind] = {"path": resource_root + target.name, "volume_db": cue.get("volume_db", 0.0),
                        "offset_seconds": cue.get("offset_seconds", 0.0)}
    return result
