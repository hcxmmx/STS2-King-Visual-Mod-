"""Check actual preview/production cue bytes against independently resolved source entries."""
from pathlib import Path
import hashlib
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "02_Scripts_Tools"))
from weapon_sources import load_sources, resolve_stage


def main():
    assets = Path(r"D:\Dead Cells\ModTools\Unpacked_Assets")
    rows, overrides = load_sources(ROOT, assets)
    generated = ROOT / "05_Preview/Generated"
    data = json.loads((generated / "battle_data.json").read_text(encoding="utf-8"))
    manifest = json.loads((ROOT / "03-godot-project/Hcxmmx_King/Presentation/weapons.json").read_text(encoding="utf-8"))
    report = []
    for mode, weapons in [("preview", data["weapons"]), ("production_definitions", manifest["weapons"])]:
        for weapon in weapons:
            for stage in weapon["stages"]:
                if weapon.get("kind") == "ranged":
                    item, index = "FastBow", 0
                    strike = dict(rows[item]["strikeChain"][index])
                    strike["sfxHit"] = "sfx/env/arrow_hit.wav"
                else:
                    item, index, strike, _ = resolve_stage(stage, rows, overrides)
                cues = data["timing"][stage["body"]]["audio"] if mode == "preview" else stage["audio"]
                for kind, field in [("release", "sfxRelease"), ("hit", "sfxHit"), ("charge", "sfxCharge")]:
                    if not strike.get(field):
                        continue
                    source = assets / strike[field]
                    base = ROOT / ("05_Preview" if mode == "preview" else "03-godot-project")
                    target = base / cues[kind]["path"].removeprefix("res://")
                    blob = source.read_bytes()
                    assert target.read_bytes() == blob, (mode, stage["body"], kind)
                    report.append({"mode": mode, "weapon": weapon["id"], "body": stage["body"],
                                   "item": item, "strike": index, "cue": kind,
                                   "source": str(source), "sha256": hashlib.sha256(blob).hexdigest()})
    destination = ROOT / "artifacts/weapon-expansion/source-audio-audit.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"WEAPON_AUDIO_SOURCE_PASS: {len(data['weapons'])} preview weapons / {len(manifest['weapons'])} production definitions, {len(report)} exact byte comparisons")


if __name__ == "__main__":
    main()
