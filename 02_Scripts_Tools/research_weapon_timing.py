"""Targeted read-only HashLink disassembly for the local weapon timing preview."""
from pathlib import Path
import sys


def main():
    root = Path(__file__).resolve().parents[1]
    sys.path.insert(0, str(root / "artifacts/scarf-research/crashlink"))
    from crashlink.core import Bytecode, Obj
    from crashlink import disasm
    code = Bytecode.from_path(r"D:\Dead Cells\deadcells.exe")
    selected = {1499, 1870, 1875, 3752, 1210, 1924, 4904}
    for typ in code.types:
        definition = typ.definition
        if isinstance(definition, Obj) and definition.name.resolve(code) == "tool.skill.OldSkill":
            methods = [(p.name.resolve(code), p.findex.value) for p in definition.protos]
            methods += [(b.field.resolve_obj(code, definition).name.resolve(code), b.findex.value) for b in definition.bindings]
            print("OLD_SKILL_METHODS", methods, flush=True)
            selected.update(index for name, index in methods if "update" in name.lower())
    output = root / "artifacts/scarf-research/original-disasm"
    for index in sorted(selected):
        function = code.fn(index)
        name = code.full_func_name(function).replace("$", "")
        if "<" in name:
            continue
        text = disasm.func_header(code, function) + "\n" + "\n".join(
            disasm.fmt_op(code, function.regs, op, i,
                          debug=function.debuginfo.value if function.debuginfo else None, func=function)
            for i, op in enumerate(function.ops))
        (output / (name + ".txt")).write_text(text, encoding="utf-8")
        print("DUMP", index, name, flush=True)


if __name__ == "__main__":
    main()
