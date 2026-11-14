"""Promote approved previews into append-only runtime presentation resources."""
from pathlib import Path
import json
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "05_Preview"))
from prepare_weapon_audio import prepare as prepare_audio
from prepare_battle_preview import main as prepare_preview


def main():
    presentation = ROOT / "03-godot-project/Hcxmmx_King/Presentation"
    preview = ROOT / "05_Preview"
    manifest_path = presentation / "weapons.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    drafts = json.loads((preview / "draft_weapons.json").read_text(encoding="utf-8"))["weapons"]
    known = {weapon["id"] for weapon in manifest["weapons"]}
    manifest["weapons"] += [weapon for weapon in drafts if weapon["id"] not in known]
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    prepare_audio(Path(r"D:\Dead Cells\ModTools\Unpacked_Assets"), ROOT)
    prepare_preview()
    data = json.loads((preview / "Generated/battle_data.json").read_text(encoding="utf-8"))
    old_root = "res://Generated/ExpansionAssets/"
    new_root = "res://Hcxmmx_King/Presentation/ExpansionAssets/"
    # Rewrite only owned resource roots, including dictionary keys for bone lookup.
    runtime = json.loads(json.dumps(data, ensure_ascii=False).replace(old_root, new_root))
    runtime = json.loads(json.dumps(runtime, ensure_ascii=False).replace(
        "res://Generated/ShieldAssets/", "res://Hcxmmx_King/Presentation/ShieldAssets/"))
    runtime = json.loads(json.dumps(runtime, ensure_ascii=False).replace(
        "res://Generated/ImpactAssets/", "res://Hcxmmx_King/Presentation/ImpactAssets/"))
    runtime = json.loads(json.dumps(runtime, ensure_ascii=False).replace(
        "res://Generated/OriginalHitAssets/", "res://Hcxmmx_King/Presentation/OriginalHitAssets/"))
    ranged = json.loads((preview / "Generated/ranged_data.json").read_text(encoding="utf-8"))
    ranged = json.loads(json.dumps(ranged).replace("res://Generated/RangedAssets/", "res://Hcxmmx_King/Presentation/RangedAssets/"))
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    bow = {"id": "fast_bow", "kind": "ranged", "weight": 1,
           "stages": [{"body": "FastBow", "vfx": None, "audio": ranged["audio"]}]}
    manifest["weapons"] = [w for w in manifest["weapons"] if w["id"] != "fast_bow"] + [bow]
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    runtime["ranged"] = ranged
    runtime["extra_animations"]["FastBow"] = ranged["animations"]["FastBowPreview"]
    runtime["anchors"].update(ranged["anchors"])
    runtime["front_frames"].update(ranged["front_frames"])
    runtime["impacts"]["stages"]["FastBow"] = {"style": "arrow", "strength": 1.0, "angle": 0.0}
    runtime["timing"]["FastBow"] = {
        "body": "FastBow", "vfx": None, "ranged": True, "body_fps": ranged["fps"],
        "body_delay": 0.0, "hit_frame": len(ranged["animations"]["FastBowPreview"]["frames"]) * ranged["release_ratio"],
        "charge": ranged["charge"], "fast_charge": ranged["fast_charge"], "animation_speed": ranged["fps"] / 30.0,
        "fx_time": ranged["charge"], "fx_visible_time": ranged["charge"], "reverse": False}
    for weapon in manifest["weapons"]:
        for index, stage in enumerate(weapon["stages"]):
            profile = runtime["timing"][stage["body"]]
            profile["audio"] = stage["audio"]
            profile["weapon_id"] = weapon["id"]
            profile["stage_index"] = index
    runtime["weapons"] = manifest["weapons"]
    runtime.pop("spires_fast", None)
    (presentation / "combat_presentation.json").write_text(json.dumps(runtime, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    base_data = json.loads((presentation / "presentation.json").read_text(encoding="utf-8"))
    base_data["anchors"].update(runtime["anchors"])
    base_data["front_frames"].update(runtime["front_frames"])
    (presentation / "presentation.json").write_text(json.dumps(base_data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    shutil.copytree(preview / "Generated/ExpansionAssets", presentation / "ExpansionAssets", dirs_exist_ok=True)
    shutil.copytree(preview / "Generated/ShieldAssets", presentation / "ShieldAssets", dirs_exist_ok=True)
    shutil.copytree(preview / "Generated/ImpactAssets", presentation / "ImpactAssets", dirs_exist_ok=True)
    shutil.copytree(preview / "Generated/OriginalHitAssets", presentation / "OriginalHitAssets", dirs_exist_ok=True)
    shutil.copytree(preview / "Generated/RangedAssets", presentation / "RangedAssets", dirs_exist_ok=True)
    for name in ["RhythmTiming.gd", "ExtraAnimations.gd", "OriginalEnemyHit.gd",
                 "SelectionSettings.gd", "SettingsIconButton.gd"]:
        shutil.copyfile(preview / name, presentation / name)
    print(f"GAME_PRESENTATION_READY: {len(runtime['weapons'])} weapons, {len(runtime['timing'])} stages; preview timing scripts copied byte-for-byte")


if __name__ == "__main__":
    main()
