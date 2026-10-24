"""Import the two audio samples using Dead Cells' own weapon/strike mappings (no re-encoding)."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


def prepare(assets: Path, root: Path):
    presentation = root / "03-godot-project/Hcxmmx_King/Presentation"
    manifest_path = presentation / "weapons.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    database = json.loads((assets / "data.cdb").read_text(encoding="utf-8-sig"))
    rows = next(sheet["lines"] for sheet in database["sheets"] if sheet["name"] == "weapon")
    selected = {row["item"]: row for row in rows if row.get("item") in {"Katana", "Pan"}}
    report = []
    audio_dir = presentation / "Audio"
    audio_dir.mkdir(exist_ok=True)
    for weapon in manifest["weapons"]:
        source_item = {"katana": "Katana", "pan": "Pan"}.get(weapon["id"])
        if not source_item:
            continue
        strikes = {strike.get("animId"): strike for strike in selected[source_item]["strikeChain"]}
        for stage in weapon["stages"]:
            strike = strikes[stage["body"]]
            audio = stage.setdefault("audio", {})
            audio["source_item"] = source_item
            audio["source_hit_frame"] = strike["hitFrame"]
            for kind, field in (("release", "sfxRelease"), ("hit", "sfxHit")):
                source = assets / strike[field]
                blob = source.read_bytes()
                assert blob[:4] == b"OggS", f"Unsupported source format: {source}"
                destination = audio_dir / (source.stem + ".ogg")
                shutil.copyfile(source, destination)
                cue = audio.setdefault(kind, {})
                cue.update(path="res://Hcxmmx_King/Presentation/Audio/" + destination.name)
                cue.setdefault("volume_db", -6.0)
                cue.setdefault("offset_seconds", 0.0)
                report.append({"animation": stage["body"], "kind": kind, "source": str(source),
                               "sha256": hashlib.sha256(blob).hexdigest()})
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    output = root / "artifacts/audio-integration"
    output.mkdir(parents=True, exist_ok=True)
    (output / "source-audio.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"AUDIO_IMPORT_PASS: 7 stages, {len({r['sha256'] for r in report})} original Ogg files; no re-encoding")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path, default=Path(r"D:\Dead Cells\ModTools\Unpacked_Assets"))
    options = parser.parse_args()
    prepare(options.assets, Path(__file__).resolve().parents[1])
