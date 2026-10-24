"""Prepare an isolated Godot preview from existing local game/mod resources."""
from pathlib import Path
import argparse
import json
import re
import shutil
import struct

from PIL import Image


def prepare(game_assets: Path, workspace: Path) -> None:
    generated = Path(__file__).parent / "Generated"
    generated.mkdir(exist_ok=True)
    database = json.loads((game_assets / "data.cdb").read_text(encoding="utf-8-sig"))
    skins = next(sheet["lines"] for sheet in database["sheets"] if sheet["name"] == "skin")
    selected = {skin["item"]: skin for skin in skins if skin.get("item") in {"KingDefault", "KingWhite"}}
    assert set(selected) == {"KingDefault", "KingWhite"}

    atlas = game_assets / "atlas"
    binary = (atlas / "gameElements.atlas").read_bytes()
    image = Image.open(atlas / "gameElements.png")
    for name in ("scarfGlow0", "scarfGlow1"):
        offset = binary.index(name.encode("ascii")) + len(name)
        page, x, y, width, height, _, _, _, _ = struct.unpack_from("<9H", binary, offset)
        assert page == 0 and width == 25 and height == 13, "Unexpected scarf atlas layout"
        image.crop((x, y, x + width, y + height)).save(generated / f"{name}.png")

    tracks = json.loads((atlas / "king_tracks.json").read_text(encoding="utf-8-sig"))
    by_name = {name.lower(): value for name, value in tracks.items()}
    back_rows = next(sheet["lines"] for sheet in database["sheets"] if sheet["name"] == "backFrames")
    back_windows = {row["animId"].lower(): [window for window in row["modes"] if window["mode"] == 0] for row in back_rows}
    source = workspace / "03-godot-project"
    scene = (source / "Hcxmmx_King/tscn/white_king.tscn").read_text(encoding="utf-8")
    paths = re.findall(r'path="(res://Hcxmmx_King/Asset/Body/[^\"]+\.png)"', scene)
    anchors = {}
    front_frames = {}
    dimensions = {}
    missing = []
    for path in paths:
        frame_path = Path(path)
        match = re.match(r"(.+)_(\d+)-=-", frame_path.name)
        if not match:
            missing.append(path)
            continue
        name, index = match[1], int(match[2])
        track = by_name.get(name.lower(), {})
        # The game's default scarf root uses en.Hero.get_headX/Y -> headBone.
        points = track.get("headBone", [])
        if index * 3 + 1 >= len(points):
            missing.append(path)
            continue
        local = source / path.removeprefix("res://")
        if name not in dimensions:
            with Image.open(local) as frame_image:
                dimensions[name] = frame_image.size
        width, height = dimensions[name]
        anchors[path] = [points[index * 3] - width / 2, points[index * 3 + 1] - height / 2]
        # Assets.initBackFramesCache maps endFrame=0 to 999; comparisons are inclusive.
        front_frames[path] = any(window["startFrame"] <= index <= (window["endFrame"] or 999)
                                 for window in back_windows.get(name.lower(), []))

    presentation = source / "Hcxmmx_King/Presentation"
    weapons = json.loads((presentation / "weapons.json").read_text(encoding="utf-8"))["weapons"]
    stages = [stage for weapon in weapons for stage in weapon["stages"]]
    assert len({stage["body"] for stage in stages}) == len(stages), "Duplicate weapon animation"
    vfx = {stage["body"]: stage["vfx"] for stage in stages if stage["vfx"]}
    attacks = sorted(stage["body"] for stage in stages)
    package = workspace / "04_ModUper/ModUploader-win-x64/NewModWorkspace/content/Hcxmmx_King_Skin.pck"
    shutil.copy2(package, generated / "king_source.pck")
    payload = {"schema_version": 1, "weapons": weapons, "skins": selected, "anchors": anchors, "anchor_bone": "headBone", "front_frames": front_frames,
               "attack_animations": attacks, "vfx": vfx, "unmapped_frames": missing}
    if missing:
        raise ValueError(f"Cannot ship missing headBone tracks: {missing}")
    serialized = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    (presentation / "presentation.json").write_text(serialized, encoding="utf-8")
    (generated / "preview_data.json").write_text(serialized, encoding="utf-8")
    for name in ("scarfGlow0.png", "scarfGlow1.png"):
        shutil.copy2(generated / name, presentation / name)
    shutil.copy2(presentation / "ScarfRig.gd", generated / "ScarfRig.gd")
    if (presentation / "Audio").exists():
        shutil.copytree(presentation / "Audio", generated / "Audio", dirs_exist_ok=True)
    print(f"Prepared {len(selected)} skins / {len(anchors)} headBone anchors / {len(vfx)} weapon VFX mappings")
    print(f"Original front-layer windows cover {sum(front_frames.values())} frames; {len(attacks)} weapon-use animations")
    if missing:
        print(f"Head anchor fallback needed for {len(missing)} frames (see preview_data.json)")
    print("Shared presentation data and preview assets generated; no installed mod/game files modified.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--assets", type=Path, default=Path(r"D:\Dead Cells\ModTools\Unpacked_Assets"))
    parser.add_argument("--workspace", type=Path, default=Path(__file__).resolve().parents[1])
    options = parser.parse_args()
    prepare(options.assets, options.workspace)
