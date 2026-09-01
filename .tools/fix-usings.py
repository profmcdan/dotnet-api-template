#!/usr/bin/env python3
"""Move `using` directives that sit below a file-scoped namespace back above it."""
import sys, pathlib, re

def fix(path: pathlib.Path) -> bool:
    text = path.read_text()
    lines = text.split("\n")
    ns_idx = next((i for i, l in enumerate(lines) if l.startswith("namespace ")), None)
    if ns_idx is None:
        return False
    moved = [l for l in lines[ns_idx + 1:] if l.startswith("using ") and l.rstrip().endswith(";")]
    if not moved:
        return False
    rest = [l for l in lines[ns_idx + 1:] if not (l.startswith("using ") and l.rstrip().endswith(";"))]
    head = lines[:ns_idx]
    existing = [l for l in head if l.startswith("using ")]
    other_head = [l for l in head if not l.startswith("using ")]
    usings = sorted(set(existing + moved), key=lambda u: (not u.startswith("using System"), u))
    while rest and rest[0].strip() == "":
        rest.pop(0)
    out = [l for l in other_head if l.strip()] + usings + ["", lines[ns_idx], ""] + rest
    path.write_text("\n".join(out).rstrip("\n") + "\n")
    return True

roots = [pathlib.Path(p) for p in sys.argv[1:]] or [pathlib.Path("src"), pathlib.Path("tests")]
changed = 0
for root in roots:
    for f in root.rglob("*.cs"):
        if "/obj/" in str(f) or "/bin/" in str(f):
            continue
        if fix(f):
            changed += 1
print(f"fixed {changed} files")
