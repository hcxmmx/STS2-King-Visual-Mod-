"""Prepare the first ranged prototype independently of the runtime weapon pool."""
from pathlib import Path
import json

from prepare_extra_weapons import Atlas


def prepare_ranged(assets, generated, database):
    atlas = Atlas(assets, "king")
    frames = atlas.frames("fastBow")
    tracks = json.loads((assets / "atlas/king_tracks.json").read_text(encoding="utf-8-sig"))["fastBow"]
    windows = next((r["modes"] for s in database["sheets"] if s["name"] == "backFrames"
                    for r in s["lines"] if r["animId"].lower() == "fastbow"), [])
    paths, anchors, fronts = [], {}, {}
    for index, image in frames:
        target = generated / "RangedAssets/fastBow" / f"{index:04}.png"
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target)
        path = "res://Generated/" + target.relative_to(generated).as_posix()
        paths.append(path)
        points = tracks["headBone"]
        anchors[path] = [points[index * 3] - image.width / 2, points[index * 3 + 1] - image.height / 2]
        fronts[path] = any(w["mode"] == 0 and w["startFrame"] <= index <= (w["endFrame"] or 999) for w in windows)
    arrow = Atlas(assets, "gameElements").frames("tinyArrow")[0][1]
    arrow.save(generated / "RangedAssets/tinyArrow.png")
    strike = next(r for s in database["sheets"] if s["name"] == "weapon" for r in s["lines"] if r["item"] == "FastBow")["strikeChain"][0]
    audio = {}
    for kind, source in [("charge", strike["sfxCharge"]), ("release", strike["sfxRelease"]), ("hit", "sfx/env/arrow_hit.wav")]:
        source = assets / source
        raw = source.read_bytes()
        assert raw[:4] == b"OggS"
        target = generated / "RangedAssets" / (source.stem + ".ogg")
        target.write_bytes(raw)
        audio[kind] = {"path": "res://Generated/" + target.relative_to(generated).as_posix(), "volume_db": 0.0}
    data = {"animations": {"FastBowPreview": {"node": "Body", "fps": 30.0 * strike["animSpd"], "frames": paths}},
            "anchors": anchors, "front_frames": fronts, "audio": audio,
            "arrow": "res://Generated/RangedAssets/tinyArrow.png", "color": "fff700",
            "charge": strike["charge"], "fps": 30.0 * strike["animSpd"], "release_ratio": 0.5,
            "source": "BaseBow charge + FastBow.playShootAnim fastBow at ratio 0.5; tinyArrow projectile",
            "flight_original": 0.1, "flight_fast": 0.06, "fast_charge": 0.07}
    (generated / "ranged_data.json").write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"RANGED_PREVIEW_READY: FastBow {len(frames)} source frames, original tinyArrow and three original Ogg cues")
