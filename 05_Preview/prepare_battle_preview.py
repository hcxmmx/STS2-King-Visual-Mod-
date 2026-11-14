"""Build both-rhythm previews for all existing weapons and selected new drafts."""
from pathlib import Path
import json
import re
import struct
import sys

from PIL import Image

from prepare_preview import prepare
from prepare_extra_weapons import prepare_extra
from prepare_ranged_preview import prepare_ranged
from prepare_original_hits import prepare_original_hits

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "02_Scripts_Tools"))
from weapon_sources import load_sources, resolve_stage, timing_for, copy_audio
from prepare_shield_presentation import prepare_shields
from prepare_impact_presentation import prepare_impacts


def main():
    assets = Path(r"D:\Dead Cells\ModTools\Unpacked_Assets")
    prepare(assets, ROOT)
    generated = Path(__file__).parent / "Generated"
    presentation = ROOT / "03-godot-project/Hcxmmx_King/Presentation"
    data = json.loads((generated / "preview_data.json").read_text(encoding="utf-8"))
    original_weapons = list(data["weapons"])
    database = json.loads((assets / "data.cdb").read_text(encoding="utf-8-sig"))
    rows, overrides = load_sources(ROOT, assets)
    alignment = json.loads((presentation / "fx_alignment.json").read_text(encoding="utf-8"))["stages"]
    drafts, extra_animations, anchors, fronts = prepare_extra(assets, generated, database)
    existing_ids = {weapon["id"] for weapon in data["weapons"]}
    data["weapons"] += [weapon for weapon in drafts if weapon["id"] not in existing_ids]
    data["extra_animations"] = extra_animations
    data["anchors"].update(anchors)
    data["front_frames"].update(fronts)
    shield, animations, shield_anchors, shield_fronts = prepare_shields(assets, generated, database)
    data["shield"] = shield
    data["impacts"] = prepare_impacts(assets, generated, data["weapons"])
    data["extra_animations"].update(animations)
    data["anchors"].update(shield_anchors)
    data["front_frames"].update(shield_fronts)
    profiles, audit = {}, []
    scene = (ROOT / "03-godot-project/Hcxmmx_King/tscn/white_king.tscn").read_text(encoding="utf-8")
    fx_atlas = (assets / "atlas/fxWeapon.atlas").read_bytes()
    fx_pages = {}
    for weapon in data["weapons"]:
        for stage in weapon["stages"]:
            item, source_index, strike, override = resolve_stage(stage, rows, overrides)
            profile = timing_for(stage, item, source_index, strike, override)
            profile["body"] = stage["body"]
            lead = 0
            if stage.get("vfx") in extra_animations:
                lead = extra_animations[stage["vfx"]]["trimmed_lead"]
            elif item == "VampireKiller":
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
                    assert fx_pages[page].crop((x, y, x + width, y + height)).getchannel("A").getextrema() == (0, 0)
            profile["fx_lead_frames"] = lead
            profile["fx_visible_time"] = profile["fx_time"] + lead / profile["body_fps"]
            binding = alignment.get(stage["body"], {})
            profile["fx_clock"] = binding.get("clock", "independent")
            profile["fx_carry_over"] = binding.get("carry_over", True)
            profile["audio"] = copy_audio(strike, assets, generated / "BattleAudio", "res://Generated/BattleAudio/")
            profiles[stage["body"]] = profile
            audit.append({"weapon": weapon["id"], "source_item": item, "source_strike_index": source_index,
                          "body": stage["body"], "source_body": strike.get("animId"),
                          "source_fx": strike.get("fxId"), "project_fx": stage.get("vfx"),
                          "mapping": "explicit_project_adaptation" if override.get("note") else
                                     "project_fx_alias" if stage.get("vfx") != strike.get("fxId") else "source_exact",
                          "adaptation": override.get("note", ""),
                          "source_anim_speed": strike.get("animSpd", 1), "trimmed_fx_lead": lead,
                          "fx_clock": profile["fx_clock"], "alignment_basis": binding.get("basis", ""),
                          "fx_onset_body_frame": (profile["fx_visible_time"] - profile["body_delay"]) * profile["body_fps"],
                          "contact_anchor_phase_error_frames": profile["hit_frame"] - (profile["fx_visible_time"] - profile["body_delay"]) * profile["body_fps"]})
    data["timing"] = profiles
    data["source_fps"] = 30.0
    data["spires_fast"] = {"attack_wait": 0.075, "damage_wait": 0.1, "interval": 0.175,
                           "card_id": "STARDUST", "card_name": "星尘", "damage": 5}
    data["skipped_custom_weapons"] = []
    runtime = {}
    for weapon in original_weapons:
        for stage in weapon["stages"]:
            profile = profiles[stage["body"]].copy()
            profile["audio"] = stage["audio"]
            runtime[stage["body"]] = profile
    (presentation / "weapon_timing.json").write_text(json.dumps(runtime, ensure_ascii=False, indent=2), encoding="utf-8")
    data["impacts"]["ordinary"] = prepare_original_hits(assets, generated)
    (generated / "battle_data.json").write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    import shutil
    shutil.copyfile(presentation / "ShieldPresentation.gd", Path(__file__).parent / "ShieldPresentation.gd")
    shutil.copyfile(presentation / "ImpactPresentation.gd", Path(__file__).parent / "ImpactPresentation.gd")
    prepare_ranged(assets, generated, database)
    (generated / "weapon_source_audit.json").write_text(json.dumps({"source": str(assets / "data.cdb"), "stages": audit}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"SOURCE_MAPPING_AUDIT: {len(audit)} stages; all project variants have explicit source records")
    print(f"BATTLE_DATA_READY: {len(original_weapons)} existing + {len(drafts)} draft weapons, {len(profiles)} stages; no skipped groups")


if __name__ == "__main__":
    main()
