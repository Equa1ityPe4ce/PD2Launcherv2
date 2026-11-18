#!/usr/bin/env python3
"""
Generates BHPreviewHost/generated/D2Exports.g.h from a BH source checkout.

It reads every FUNCPTR / VARPTR / ASMPTR declared in BH's D2Ptrs.h and every
`new Patch(...)` site in BH's sources, and emits a table the mock game uses to
lay out its fake D2 modules. Only the 1.13c offset (the first one) is used,
because PD2 runs on 1.13c and the mock reports itself as 1.13c.

Usage:
    python3 gen_d2exports.py <path-to-BH-repo>/BH  <output-header>
"""
import os
import re
import subprocess
import sys

# BH's Patch::GetDllOffset indexes szDlls[] with its Dll enum. The enum has no second
# D2CMP entry, so D2SOUND (enum value 14) resolves to szDlls[14] == "D2CMP.dll".
# The mock reproduces exactly what BH resolves, so D2SOUND symbols live in the D2CMP module.
MODULE_OF = {"D2SOUND": "D2CMP"}


def module(dll):
    return MODULE_OF.get(dll, dll)

EIGHT_BYTE_TYPES = ("double", "__int64", "long long", "ULONGLONG", "LONGLONG", "QWORD")


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


def split_top(s, sep=","):
    out, depth, cur = [], 0, ""
    for ch in s:
        if ch in "({[<":
            depth += 1
        elif ch in ")}]>":
            depth -= 1
        if ch == sep and depth == 0:
            out.append(cur.strip())
            cur = ""
        else:
            cur += ch
    if cur.strip():
        out.append(cur.strip())
    return out


def macro_calls(text, name):
    """Yields the raw argument string of every NAME(...) invocation."""
    for m in re.finditer(r"^\s*" + name + r"\s*\(", text, flags=re.M):
        i = m.end()
        depth = 1
        start = i
        while depth and i < len(text):
            if text[i] == "(":
                depth += 1
            elif text[i] == ")":
                depth -= 1
            i += 1
        yield text[start:i - 1]


def parse_int(tok):
    """Parses an integer literal or simple +/- expression of literals (e.g. 0xEABE - 0xEAB6)."""
    tok = tok.strip()
    if not re.fullmatch(r"[0-9a-fA-FxX\s+\-]+", tok):
        raise ValueError(tok)
    total, sign = 0, 1
    for part in re.findall(r"[+-]|[0-9a-fA-FxX]+", tok):
        if part == "+":
            sign = 1
        elif part == "-":
            sign = -1
        else:
            total += sign * int(part, 0)
            sign = 1
    return total


def arg_bytes(arg):
    if any(t in arg for t in EIGHT_BYTE_TYPES) and "*" not in arg:
        return 8
    return 4


def calling_info(callingret, args):
    conv = "cdecl"
    if "__stdcall" in callingret or "WINAPI" in callingret:
        conv = "stdcall"
    elif "__fastcall" in callingret:
        conv = "fastcall"
    elif "__thiscall" in callingret:
        conv = "thiscall"
    inner = args.strip()
    assert inner.startswith("(") and inner.endswith(")"), args
    inner = inner[1:-1].strip()
    params = [] if inner in ("", "void", "VOID") else split_top(inner)
    sizes = [arg_bytes(p) for p in params]
    if conv == "stdcall":
        stack = sum(sizes)
    elif conv == "fastcall":
        regs, stack = 0, 0
        for s in sizes:
            if s == 4 and regs < 2:
                regs += 1
            else:
                stack += s
    elif conv == "thiscall":
        stack = sum(sizes[1:])
    else:
        stack = 0  # caller cleans
    return conv, len(params), stack


def main():
    bh, out_path = sys.argv[1], sys.argv[2]
    ptrs = strip_comments(open(os.path.join(bh, "D2Ptrs.h"), encoding="utf-8-sig", errors="replace").read())
    # Only the declarations after the macro definitions are real entries.
    ptrs = ptrs[ptrs.index("#endif", ptrs.index("_DEFINE_PTRS")):]

    funcs, vars_, asms = [], [], []
    for raw in macro_calls(ptrs, "FUNCPTR"):
        parts = split_top(raw)
        dll, name, callingret, args = parts[0], parts[1], parts[2], parts[3]
        offset = parse_int(parts[4])
        conv, nargs, stack = calling_info(callingret, args)
        funcs.append((dll, name, offset, conv, nargs, stack, (callingret + " " + args).replace('"', "'")))
    for raw in macro_calls(ptrs, "VARPTR"):
        parts = split_top(raw)
        vars_.append((parts[0], parts[1], parse_int(parts[3]), parts[2]))
    for raw in macro_calls(ptrs, "ASMPTR"):
        parts = split_top(raw)
        asms.append((parts[0], parts[1], parse_int(parts[2])))

    patches = []
    for root, _, files in os.walk(bh):
        if "ThirdParty" in root:
            continue
        for f in files:
            if not f.endswith(".cpp"):
                continue
            src = strip_comments(open(os.path.join(root, f), encoding="utf-8-sig", errors="replace").read())
            for m in re.finditer(r"new\s+Patch\s*\(\s*(\w+)\s*,\s*(\w+)\s*,\s*\{\s*([^,}]+)[^}]*\}\s*,\s*([^,]+),\s*([^)]+)\)", src):
                kind, dll, off, target, length = m.groups()
                patches.append((dll, parse_int(off), parse_int(length), kind, target.strip().replace("(int)", ""), f))

    try:
        commit = subprocess.run(["git", "-C", bh, "rev-parse", "HEAD"], capture_output=True, text=True,
                                check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        commit = "unknown"
    lines = [
        "// <auto-generated> by tools/gen_d2exports.py from BH's D2Ptrs.h and Patch sites. Do not edit.",
        f"// BH source commit: {commit}",
        "#pragma once",
        "#include \"MockTypes.h\"",
        "",
        "namespace mock {",
        "",
        "inline const FuncSymbol kFuncSymbols[] = {",
    ]
    for dll, name, off, conv, nargs, stack, sig in funcs:
        lines.append(f'    {{ Dll::{module(dll)}, "{dll}_{name}", {off:#x}, CallConv::{conv.capitalize()}, {nargs}, {stack}, "{sig}" }},')
    lines += ["};", "", "inline const VarSymbol kVarSymbols[] = {"]
    for dll, name, off, typ in vars_:
        lines.append(f'    {{ Dll::{module(dll)}, "{dll}_{name}", {off:#x}, "{typ}" }},')
    lines += ["};", "", "inline const AsmSymbol kAsmSymbols[] = {"]
    for dll, name, off in asms:
        lines.append(f'    {{ Dll::{module(dll)}, "{dll}_{name}", {off:#x} }},')
    lines += ["};", "", "inline const PatchSite kPatchSites[] = {"]
    for dll, off, length, kind, target, f in sorted(set(patches)):
        lines.append(f'    {{ Dll::{module(dll)}, {off:#x}, {length}, PatchKind::{kind}, "{target}", "{f}" }},')
    lines += ["};", "", "} // namespace mock", ""]
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "w", newline="\r\n") as fh:
        fh.write("\n".join(lines))

    # Report layout conflicts: two things claiming overlapping bytes in one fake module.
    claims = {}
    for dll, name, off, *_ in funcs:
        if off > 0:
            claims.setdefault(module(dll), []).append((off, off + 5, "func " + name))
    for dll, name, off, _ in vars_:
        if off > 0:
            claims.setdefault(module(dll), []).append((off, off + 4, "var " + name))
    for dll, off, length, kind, target, f in set(patches):
        if off > 0:
            claims.setdefault(module(dll), []).append((off, off + length + 3, "patch " + target))
    conflicts = 0
    for dll, items in claims.items():
        items.sort()
        for a, b in zip(items, items[1:]):
            if b[0] < a[1] and a[0] != b[0]:
                conflicts += 1
                print(f"overlap in {dll}: {a[2]} @{a[0]:#x} and {b[2]} @{b[0]:#x}", file=sys.stderr)
            if 0 < a[0] < 0x1000:
                print(f"offset inside PE header page in {dll}: {a[2]} @{a[0]:#x}", file=sys.stderr)
    print(f"{len(funcs)} functions, {len(vars_)} variables, {len(asms)} asm addresses, "
          f"{len(set(patches))} patch sites, {conflicts} overlaps", file=sys.stderr)


if __name__ == "__main__":
    main()
