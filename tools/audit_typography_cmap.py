"""Read-only Unity typography coverage audit using only Python's standard library.

Run from any directory; defaults target this workspace's embedded game font.
Prints UTF-8 JSON to stdout. Never edits fonts, project assets, or user data.
The cmap audit reports nominal font coverage, not Unity fallback rendering.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import sys


SYMBOLS = "✓⚔←→‹›◆◇×＋−±∞⚡↻·：！？「」《》—"
STRING_LITERAL = re.compile(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"')
UNICODE_ESCAPE = re.compile(r"\\u([0-9a-fA-F]{4})|\\U([0-9a-fA-F]{8})")


def cmap_coverage(path: Path) -> tuple[set[int], list[dict], bytes]:
    data = path.read_bytes()

    def u16(offset: int) -> int:
        return struct.unpack_from(">H", data, offset)[0]

    def u32(offset: int) -> int:
        return struct.unpack_from(">I", data, offset)[0]

    if data[:4] not in (b"\x00\x01\x00\x00", b"OTTO", b"true"):
        raise ValueError("Expected a single sfnt TrueType/OpenType font, not a TTC/WOFF container.")
    tables = {}
    for index in range(u16(4)):
        tag, _checksum, offset, length = struct.unpack_from(">4sIII", data, 12 + index * 16)
        if offset + length > len(data):
            raise ValueError("Font table exceeds the file: " + repr(tag))
        tables[tag] = (offset, length)
    if b"cmap" not in tables:
        raise ValueError("Font has no cmap table.")
    base, _ = tables[b"cmap"]
    glyphs: set[int] = set()
    encodings = []
    seen = set()
    for index in range(u16(base + 2)):
        platform, encoding, offset = struct.unpack_from(">HHI", data, base + 4 + index * 8)
        subtable = base + offset
        form = u16(subtable)
        unicode_map = platform == 0 or (platform == 3 and encoding in (1, 10))
        encodings.append({"platform": platform, "encoding": encoding, "format": form, "unicode": unicode_map})
        if not unicode_map or subtable in seen:
            continue
        seen.add(subtable)
        if form == 12:
            for group in range(u32(subtable + 12)):
                start, end, first_glyph = struct.unpack_from(">III", data, subtable + 16 + group * 12)
                if end > 0x10FFFF or end < start:
                    raise ValueError("Invalid format 12 Unicode range.")
                for codepoint in range(start, end + 1):
                    if first_glyph + codepoint - start:
                        glyphs.add(codepoint)
        elif form == 4:
            count = u16(subtable + 6) // 2
            ends = subtable + 14
            starts = ends + count * 2 + 2
            deltas = starts + count * 2
            ranges = deltas + count * 2
            for segment in range(count):
                start, end = u16(starts + segment * 2), u16(ends + segment * 2)
                delta, range_offset = u16(deltas + segment * 2), u16(ranges + segment * 2)
                for codepoint in range(start, end + 1):
                    if codepoint == 0xFFFF:
                        continue
                    if range_offset:
                        glyph = u16(ranges + segment * 2 + range_offset + (codepoint - start) * 2)
                        if glyph:
                            glyph = (glyph + delta) & 0xFFFF
                    else:
                        glyph = (codepoint + delta) & 0xFFFF
                    if glyph:
                        glyphs.add(codepoint)
    if not glyphs:
        raise ValueError("No supported Unicode cmap format 4 or 12 entries were found.")
    return glyphs, encodings, data


def strings_in(path: Path) -> str:
    source = path.read_text(encoding="utf-8-sig")
    if path.suffix.lower() != ".cs":
        return source
    values = []
    for match in STRING_LITERAL.finditer(source):
        value = match.group()
        if not value.startswith('@"'):
            value = UNICODE_ESCAPE.sub(lambda item: chr(int(item.group(1) or item.group(2), 16)), value)
        values.append(value)
    return "".join(values)


def is_han(character: str) -> bool:
    codepoint = ord(character)
    return (0x3400 <= codepoint <= 0x9FFF or 0xF900 <= codepoint <= 0xFAFF or
            0x20000 <= codepoint <= 0x323AF)


def audit_group(paths: list[Path], coverage: set[int]) -> dict:
    characters = set("".join(strings_in(path) for path in paths))
    han = sorted(character for character in characters if is_han(character))
    missing_han = [character for character in han if ord(character) not in coverage]
    other_missing = sorted(character for character in characters
                           if not character.isspace() and ord(character) > 127 and
                           not is_han(character) and ord(character) not in coverage)
    return {"files": len(paths), "unique_han": len(han), "missing_han": missing_han,
            "other_missing": other_missing}


def main() -> None:
    workspace = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font", type=Path, default=workspace / "UnityClient/Assets/_Project/Resources/Fonts/XiangcuiKesong.ttf")
    parser.add_argument("--project-root", type=Path, default=workspace / "UnityClient/Assets/_Project")
    args = parser.parse_args()
    coverage, encodings, data = cmap_coverage(args.font)
    root = args.project_root
    groups = {
        "crew_story": [root / "Content/Runtime/CrewNarrativeCatalog.cs"],
        "component_story": [root / "Resources/Data/component_catalog_v2.csv"],
        "all_runtime": sorted(path for path in root.rglob("*.cs") if "Runtime" in path.relative_to(root).parts),
    }
    result = {
        "font": str(args.font.resolve()), "font_bytes": len(data),
        "sha256": hashlib.sha256(data).hexdigest(),
        "cmap_encodings": encodings, "coverage_codepoints": len(coverage),
        "symbols": {character: {"codepoint": "U+%04X" % ord(character), "covered": ord(character) in coverage}
                    for character in SYMBOLS},
        "groups": {name: audit_group(paths, coverage) for name, paths in groups.items()},
        "notes": "Source scan checks C# string literals and the component CSV; comments containing quoted text may also be included. No fallback fonts are assumed.",
    }
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
