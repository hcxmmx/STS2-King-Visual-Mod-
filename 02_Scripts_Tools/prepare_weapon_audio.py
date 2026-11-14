"""Import every existing weapon's audio using explicit source mappings, without re-encoding."""
import argparse
import hashlib
import json
from pathlib import Path
from weapon_sources import load_sources, resolve_stage, copy_audio


def prepare(assets: Path, root: Path):
    presentation = root / "03-godot-project/Hcxmmx_King/Presentation"
    manifest_path = presentation / "weapons.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    rows, overrides = load_sources(root, assets)
    report = []
    audio_dir = presentation / "Audio"
    audio_dir.mkdir(exist_ok=True)
    for weapon in manifest["weapons"]:
        if weapon.get("kind") == "ranged":
            continue  # Promoted from the separately verified bow source data.
        for stage in weapon["stages"]:
            source_item, source_index, strike, override = resolve_stage(stage, rows, overrides)
            audio = stage.setdefault("audio", {})
            audio["source_item"] = source_item
            audio["source_strike_index"] = source_index
            audio["source_hit_frame"] = strike["hitFrame"]
            cues = copy_audio(strike, assets, audio_dir, "res://Hcxmmx_King/Presentation/Audio/", audio)
            audio.update(cues)
            for kind, cue in cues.items():
                source = assets / strike[{"release": "sfxRelease", "hit": "sfxHit", "charge": "sfxCharge"}[kind]]
                report.append({"animation": stage["body"], "kind": kind, "source": str(source),
                               "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    output = root / "artifacts/audio-integration"
    output.mkdir(parents=True, exist_ok=True)
    (output / "source-audio.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"AUDIO_IMPORT_PASS: {len({r['animation'] for r in report})} melee stages, {len({r['sha256'] for r in report})} original Ogg files; no re-encoding")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path, default=Path(r"D:\Dead Cells\ModTools\Unpacked_Assets"))
    options = parser.parse_args()
    prepare(options.assets, Path(__file__).resolve().parents[1])
