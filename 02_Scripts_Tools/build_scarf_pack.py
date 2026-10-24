"""Add presentation resources to an approved Godot 4.5 PCK without reimporting its assets."""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def index_pack(blob: bytes):
    magic, version = struct.unpack_from("<II", blob)
    assert magic == 0x43504447 and version == 3, "Expected Godot PCK v3"
    flags, base, directory = struct.unpack_from("<IQQ", blob, 20)
    assert flags == 2, "Only unencrypted, relative-base PCKs are supported"
    count, = struct.unpack_from("<I", blob, directory)
    position = directory + 4
    entries = {}
    for _ in range(count):
        length, = struct.unpack_from("<I", blob, position)
        position += 4
        name = blob[position:position + length].rstrip(b"\0").decode("utf-8")
        position += length
        offset, size, digest, entry_flags = struct.unpack_from("<QQ16sI", blob, position)
        position += 36
        assert entry_flags == 0, "Encrypted/removal entries are unsupported"
        assert name not in entries, "Duplicate PCK entry"
        data = blob[base + offset:base + offset + size]
        assert len(data) == size and hashlib.md5(data).digest() == digest, f"Invalid entry: {name}"
        entries[name] = (offset, size, digest, entry_flags)
    return base, directory, entries


def build(baseline: Path, additions: Path, destination: Path):
    original = baseline.read_bytes()
    base, directory, entries = index_pack(original)
    original_entries = dict(entries)
    result = bytearray(original[:directory])
    added = []
    for source in sorted(additions.rglob("*")):
        if source.suffix not in {".json", ".gd", ".png", ".ogg"}:
            continue
        name = "Hcxmmx_King/Presentation/" + source.relative_to(additions).as_posix()
        assert name not in entries, f"Would overwrite baseline resource: {name}"
        result.extend(b"\0" * (-len(result) % 16))
        data = source.read_bytes()
        entries[name] = (len(result) - base, len(data), hashlib.md5(data).digest(), 0)
        result.extend(data)
        added.append(name)
    result.extend(b"\0" * (-len(result) % 16))
    new_directory = len(result)
    result.extend(struct.pack("<I", len(entries)))
    for name, metadata in sorted(entries.items()):
        encoded = name.encode("utf-8") + b"\0"
        encoded += b"\0" * (-len(encoded) % 4)
        result.extend(struct.pack("<I", len(encoded)))
        result.extend(encoded)
        result.extend(struct.pack("<QQ16sI", *metadata))
    struct.pack_into("<Q", result, 32, new_directory)
    _, _, verified = index_pack(result)
    assert all(verified[name] == entry for name, entry in original_entries.items())
    # Header's index pointer changes; every original file payload stays byte-for-byte intact.
    assert result[112:directory] == original[112:directory]
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(result)
    report = {"baseline": str(baseline.resolve()), "baseline_sha256": hashlib.sha256(original).hexdigest(),
              "output_sha256": hashlib.sha256(result).hexdigest(), "unchanged_entries": len(original_entries),
              "added": added}
    destination.parent.with_name(destination.parent.name + "-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"PCK PASS: {len(original_entries)} baseline entries unchanged; {len(added)} additions; {destination}")


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=root / "artifacts/scarf-integration/package/Hcxmmx_King_Skin.pck")
    options = parser.parse_args()
    build(options.baseline, root / "03-godot-project/Hcxmmx_King/Presentation", options.output)
