"""Collect the deterministic character set shared by Android and Windows UI fonts."""

from __future__ import annotations

from pathlib import Path
from typing import Iterable


PRINTABLE_ASCII = "".join(chr(codepoint) for codepoint in range(0x20, 0x7F))
COMMON_UI_SYMBOLS = "，。！？；：‘’“”（）【】《》￥…—·、～「」『』〈〉〔〕［］｛｝－×÷≠≤≥±℃°%‰→←↑↓•✓○□■◇◆"
SOURCE_EXTENSIONS = frozenset({".kt", ".xml", ".axaml", ".cs", ".md"})
SOURCE_ROOTS = (
    Path("android/app/src/main"),
    Path("windows/MoDi.Presentation"),
    Path("windows/MoDi.Desktop/Content"),
    Path("test-ui/UITest"),
)
EXCLUDED_PARTS = frozenset(
    {
        ".git",
        ".gradle",
        ".idea",
        ".vs",
        "archive",
        "bin",
        "build",
        "generated",
        "obj",
    }
)


def gb2312_hanzi() -> str:
    """Return the 6,763 level-one and level-two GB2312 Han characters."""

    characters: set[str] = set()
    for lead in range(0xB0, 0xF8):
        for trail in range(0xA1, 0xFF):
            try:
                characters.add(bytes((lead, trail)).decode("gb2312"))
            except UnicodeDecodeError:
                continue
    return _stable(characters)


def _source_files(repo_root: Path) -> Iterable[Path]:
    for relative_root in SOURCE_ROOTS:
        root = repo_root / relative_root
        if not root.exists():
            continue
        for path in root.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in SOURCE_EXTENSIONS:
                continue
            relative_parts = {part.lower() for part in path.relative_to(root).parts[:-1]}
            if relative_parts & EXCLUDED_PARTS:
                continue
            yield path


def _manual_characters(extra_path: Path) -> str:
    if not extra_path.exists():
        return ""
    lines = extra_path.read_text(encoding="utf-8-sig").splitlines()
    return "".join(line for line in lines if not line.lstrip().startswith("#"))


def _visible_characters(text: str) -> set[str]:
    return {character for character in text if character.isprintable() and not character.isspace()}


def _stable(characters: Iterable[str]) -> str:
    return "".join(sorted(set(characters), key=ord))


def collect_required_characters(repo_root: Path, extra_path: Path) -> str:
    """Collect baseline, source and manually supplied characters in codepoint order."""

    characters = set(PRINTABLE_ASCII)
    characters.update(gb2312_hanzi())
    characters.update(COMMON_UI_SYMBOLS)
    characters.update(_visible_characters(_manual_characters(extra_path)))
    for source_path in _source_files(repo_root.resolve()):
        characters.update(_visible_characters(source_path.read_text(encoding="utf-8-sig")))
    return _stable(characters)


def to_unicodes(characters: str) -> str:
    """Return a stable fontTools-compatible Unicode range list."""

    return ",".join(f"U+{ord(character):04X}" for character in _stable(characters))
