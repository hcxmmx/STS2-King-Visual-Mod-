"""Read local HashLink weapon overrides without executing game code."""
from pathlib import Path
import json
import re
import sys


def main():
    root = Path(__file__).resolve().parents[1]
    sys.path.insert(0, str(root / "artifacts/scarf-research/crashlink"))
    from crashlink.core import Bytecode, Obj
    from crashlink import disasm
    code = Bytecode.from_path(r"D:\Dead Cells\deadcells.exe")
    output = root / "artifacts/weapon-expansion/research"
    output.mkdir(parents=True, exist_ok=True)
    catalog = {}
    for typ in code.types:
        definition = typ.definition
        if not isinstance(definition, Obj):
            continue
        name = definition.name.resolve(code)
        if not re.search(r"tool\.weap\.(Rapier|Tombstone|KingScepter|Pan|Katana|Scissor|Nunch)", name):
            continue
        methods = [(p.name.resolve(code), p.findex.value) for p in definition.protos]
        methods += [(b.field.resolve_obj(code, definition).name.resolve(code), b.findex.value) for b in definition.bindings]
        catalog[name] = methods
        for method, index in methods:
            function = code.fn(index)
            text = disasm.func_header(code, function) + "\n" + "\n".join(
                disasm.fmt_op(code, function.regs, op, i,
                              debug=function.debuginfo.value if function.debuginfo else None, func=function)
                for i, op in enumerate(function.ops))
            (output / f"{name}.{method}.{index}.txt").write_text(text, encoding="utf-8")
        print("WEAPON_OVERRIDE", name, len(methods), flush=True)
    (output / "methods.json").write_text(json.dumps(catalog, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
