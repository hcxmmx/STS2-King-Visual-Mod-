"""Read-only targeted recovery of the original game's scarf implementation.

Requires the local crashlink checkout in artifacts/scarf-research/crashlink.
Only generated research files are written; game binaries are never modified/run.
"""
from pathlib import Path
import argparse
import hashlib
import sys


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    workspace = Path(__file__).resolve().parents[1]
    research = workspace / "artifacts/scarf-research"
    sys.path.insert(0, str(research / "crashlink"))
    from crashlink.core import Bytecode
    from crashlink import disasm
    from crashlink.decomp.function import IRFunction
    from crashlink.pseudo import pseudo

    binary = Path(r"D:\Dead Cells\deadcells.exe")
    print("Reading original bytecode; no game code is executed.", flush=True)
    parser = argparse.ArgumentParser()
    parser.add_argument("--functions", nargs="*", type=int)
    parser.add_argument("--timing", action="store_true")
    options = parser.parse_args()
    code = Bytecode.from_path(str(binary))
    (research / "original-binary-sha256.txt").write_text(hashlib.sha256(binary.read_bytes()).hexdigest(), encoding="ascii")
    indices = set(options.functions or [1983, 1984, 2153, 4871, 4874, 4876, 4877, 5856, 18918, 18919])
    if options.timing:
        from crashlink.core import Obj
        for typ in code.types:
            definition = typ.definition
            if not isinstance(definition, Obj):
                continue
            name = definition.name.resolve(code)
            if name in ["Const", "$Const"]:
                key = definition._global.value - 1
                print("TIMING_CONSTANTS", name, definition._global.value, code.initialized_globals.get(key), code.initialized_globals.get(key + 1), flush=True)
            if name in ["libs.Process", "libs.$Process", "Process", "$Process", "pr.$Game", "tool.hero.HeroWeaponsManager"]:
                print("TIMING_CLASS", name, [(p.name.resolve(code), p.findex.value) for p in definition.protos], flush=True)
                for binding in definition.bindings:
                    print("TIMING_BINDING", name, binding.field.resolve_obj(code, definition).name.resolve(code), binding.findex.value, flush=True)
                    if name == "libs.$Process" and binding.field.resolve_obj(code, definition).name.resolve(code) in ["updateAll", "_update", "__constructor__"]:
                        indices.add(binding.findex.value)
                for proto in definition.protos:
                    if proto.name.resolve(code) in ["updateAll", "_update", "update", "set_baseFps", "setBaseFps", "onWeaponUse"]:
                        indices.add(proto.findex.value)
    for index in sorted(indices):
        function = code.fn(index)
        name = code.full_func_name(function).replace("$", "")
        out = research / "original-disasm" / (name + ".txt")
        out.parent.mkdir(exist_ok=True)
        out.write_text(disasm.func(code, function), encoding="utf-8")
        print("DISASSEMBLED", name, flush=True)
        if index != 2153:
            target = research / "original-pseudo" / (name + ".hx")
            target.parent.mkdir(exist_ok=True)
            target.write_text(pseudo(IRFunction(code, function)), encoding="utf-8")
            print("RECOVERED", name, flush=True)


if __name__ == "__main__":
    main()
