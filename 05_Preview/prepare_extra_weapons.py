"""Extract selected preview weapons directly from original atlases and bone tracks."""
from pathlib import Path
import json
import re
import struct

from PIL import Image


class Atlas:
    def __init__(self, assets, name):
        self.assets, self.name = assets, name
        self.binary = (assets / f"atlas/{name}.atlas").read_bytes()
        # BATL stores image-page headers between record groups. The first ushort
        # of a sprite record is a flag, NOT the image page number.
        self.page_headers = [(m.start(), m[0].decode("ascii")) for m in
                             re.finditer(re.escape(name.encode("ascii")) + rb"(?:-?\d+)?\.png", self.binary)]
        self.pages = {}

    def frames(self, name):
        # The record flags begin with zero. Require the complete sprite name so
        # bloodSplatter_1 cannot also match bloodSplatter_1_noblood. Static groups
        # can contain an unnumbered frame zero alongside numbered later frames.
        matches = list(re.finditer(re.escape(name.encode("ascii")) + rb"_(\d+)(?=\x00)", self.binary))
        matches += list(re.finditer(re.escape(name.encode("ascii")) + rb"(?=\x00)", self.binary))
        if not matches:
            raise ValueError(f"Missing atlas animation: {self.name}/{name}")
        result = []
        for match in matches:
            flags, x, y, w, h, ox, oy, ow, oh = struct.unpack_from("<9H", self.binary, match.end())
            assert flags == 0, f"Unsupported rotated/flagged frame: {name}"
            filename = next(filename for offset, filename in reversed(self.page_headers) if offset < match.start())
            page = filename
            if page not in self.pages:
                self.pages[page] = Image.open(self.assets / "atlas" / filename).convert("RGBA")
            image = Image.new("RGBA", (ow, oh))
            image.paste(self.pages[page].crop((x, y, x + w, y + h)), (ox, oy))
            result.append((int(match[1]) if match.lastindex else 0, image))
        result.sort(key=lambda pair: pair[0])
        assert [index for index, _ in result] == list(range(len(result))), name
        return result


def prepare_extra(assets, generated, database):
    drafts = json.loads((Path(__file__).parent / "draft_weapons.json").read_text(encoding="utf-8"))["weapons"]
    tracks = {k.lower(): v for k, v in json.loads((assets / "atlas/king_tracks.json").read_text(encoding="utf-8-sig")).items()}
    windows = {row["animId"].lower(): row["modes"] for sheet in database["sheets"] if sheet["name"] == "backFrames" for row in sheet["lines"]}
    atlases = {"Body": Atlas(assets, "king"), "VFX_Slash": Atlas(assets, "fxWeapon")}
    animations, anchors, fronts = {}, {}, {}
    references, checked = {}, 0
    raw = Path(__file__).resolve().parents[1] / "01_Raw_Materials"
    for directory in [raw / "Dead_Organized_Assets", raw / "Dead_VFX_Weapon"]:
        for path in directory.rglob("*.png"):
            match = re.match(r"(.+)_(\d+)-=-", path.name)
            if match:
                references.setdefault((match[1].lower(), int(match[2])), []).append(path)
    for weapon in drafts:
        for stage in weapon["stages"]:
            for node, name in (("Body", stage["body"]), ("VFX_Slash", stage["vfx"])):
                if name in animations:
                    continue
                frames = atlases[node].frames(name)
                start = 0
                if node == "VFX_Slash":
                    while start < len(frames) and frames[start][1].getchannel("A").getbbox() is None:
                        start += 1
                    end = len(frames)
                    while end > start and frames[end - 1][1].getchannel("A").getbbox() is None:
                        end -= 1
                    frames = frames[start:end]
                assert frames, name
                safe_name = name.replace("/", "__")
                directory = generated / "ExpansionAssets" / node / safe_name
                directory.mkdir(parents=True, exist_ok=True)
                paths = []
                for index, image in frames:
                    candidates = references.get((name.split("/")[-1].lower(), index), [])
                    if candidates:
                        assert any(Image.open(path).convert("RGBA").tobytes() == image.tobytes() for path in candidates), f"Atlas/reference pixel mismatch: {name}/{index}"
                        checked += 1
                    target = directory / f"{index:04}.png"
                    image.save(target)
                    resource = "res://Generated/" + target.relative_to(generated).as_posix()
                    paths.append(resource)
                    if node == "Body":
                        points = tracks[name.lower()]["headBone"]
                        assert len(points) >= (index + 1) * 3, name
                        anchors[resource] = [points[index * 3] - image.width / 2, points[index * 3 + 1] - image.height / 2]
                        fronts[resource] = any(w["mode"] == 0 and w["startFrame"] <= index <= (w["endFrame"] or 999)
                                               for w in windows.get(name.lower(), []))
                animations[name] = {"node": node, "fps": 30.0, "frames": paths,
                                    "source_indices": [index for index, _ in frames], "trimmed_lead": start}
    print(f"EXTRA_ATLAS_READY: {len(drafts)} weapons, {len(animations)} animations, {sum(len(a['frames']) for a in animations.values())} original frames, {checked} independent pixel-reference checks")
    return drafts, animations, anchors, fronts
