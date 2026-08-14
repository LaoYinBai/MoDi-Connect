"""Fail-closed verification for committed cross-platform font artifacts."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import struct
import sys
from pathlib import Path
from typing import Iterable

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from scripts.fonts.collect_characters import collect_required_characters, gb2312_hanzi


ARTIFACT_ROOT = Path("assets/fonts/android-res/font")
ARTIFACT_LICENSE_ROOT = Path("assets/fonts/android-res/raw")
ARTIFACT_LOCK = Path("assets/fonts/font-artifacts.lock.json")
SOURCE_LOCK = Path("scripts/fonts/font-sources.lock.json")


class FontVerificationError(RuntimeError):
    """Raised when a source or generated font violates the locked contract."""


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def sha256_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest().upper()


def source_lock_sha256(source_lock: dict) -> str:
    canonical = json.dumps(source_lock, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return sha256_text(canonical)


def _u16(data: bytes, offset: int) -> int:
    return struct.unpack_from(">H", data, offset)[0]


def _i16(data: bytes, offset: int) -> int:
    return struct.unpack_from(">h", data, offset)[0]


def _u32(data: bytes, offset: int) -> int:
    return struct.unpack_from(">I", data, offset)[0]


def _tables(data: bytes) -> dict[str, tuple[int, int]]:
    if len(data) < 12:
        raise FontVerificationError("font header is truncated")
    count = _u16(data, 4)
    result: dict[str, tuple[int, int]] = {}
    for index in range(count):
        record = 12 + index * 16
        if record + 16 > len(data):
            raise FontVerificationError("font table directory is truncated")
        tag = data[record : record + 4].decode("ascii", errors="strict")
        offset, length = _u32(data, record + 8), _u32(data, record + 12)
        if offset + length > len(data):
            raise FontVerificationError(f"font table {tag} is truncated")
        result[tag] = (offset, length)
    return result


def font_codepoints(path: Path) -> set[int]:
    data = path.read_bytes()
    tables = _tables(data)
    if "cmap" not in tables:
        raise FontVerificationError(f"font has no cmap: {path}")
    base, _ = tables["cmap"]
    count = _u16(data, base + 2)
    offsets = {_u32(data, base + 4 + index * 8 + 4) for index in range(count)}
    result: set[int] = set()
    for relative in offsets:
        offset = base + relative
        format_number = _u16(data, offset)
        if format_number == 4:
            length = _u16(data, offset + 2)
            segment_count = _u16(data, offset + 6) // 2
            ends = offset + 14
            starts = ends + segment_count * 2 + 2
            deltas = starts + segment_count * 2
            ranges = deltas + segment_count * 2
            for index in range(segment_count):
                start = _u16(data, starts + index * 2)
                end = _u16(data, ends + index * 2)
                delta = _i16(data, deltas + index * 2)
                range_offset = _u16(data, ranges + index * 2)
                if start == 0xFFFF and end == 0xFFFF:
                    continue
                for codepoint in range(start, end + 1):
                    if range_offset == 0:
                        glyph = (codepoint + delta) & 0xFFFF
                    else:
                        pointer = ranges + index * 2 + range_offset + (codepoint - start) * 2
                        glyph = _u16(data, pointer) if pointer + 2 <= offset + length else 0
                        if glyph:
                            glyph = (glyph + delta) & 0xFFFF
                    if glyph:
                        result.add(codepoint)
        elif format_number == 12:
            groups = _u32(data, offset + 12)
            for index in range(groups):
                start, end, glyph = struct.unpack_from(">III", data, offset + 16 + index * 12)
                if glyph == 0 and start == end:
                    continue
                result.update(range(start, end + 1))
    return result


def font_names(path: Path, name_ids: Iterable[int] = (1, 4, 6, 16)) -> set[str]:
    data = path.read_bytes()
    tables = _tables(data)
    if "name" not in tables:
        raise FontVerificationError(f"font has no name table: {path}")
    base, _ = tables["name"]
    count, storage = _u16(data, base + 2), _u16(data, base + 4)
    wanted = set(name_ids)
    result: set[str] = set()
    for index in range(count):
        record = base + 6 + index * 12
        platform, _, _, name_id, length, relative = struct.unpack_from(">HHHHHH", data, record)
        if name_id not in wanted:
            continue
        raw = data[base + storage + relative : base + storage + relative + length]
        try:
            text = raw.decode("utf-16-be" if platform in (0, 3) else "mac_roman").strip("\x00")
        except (UnicodeDecodeError, LookupError):
            continue
        if text:
            result.add(text)
    return result


def load_source_lock(repo_root: Path) -> dict:
    path = repo_root / SOURCE_LOCK
    try:
        lock = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise FontVerificationError(f"cannot read source lock: {path}: {error}") from error
    if lock.get("schemaVersion") != 1 or len(lock.get("fonts", ())) != 5:
        raise FontVerificationError("source lock must contain exactly five schema-v1 fonts")
    return lock


def resolve_library_root(source_lock: dict, library_root: Path | None) -> Path:
    if library_root is not None:
        return library_root.resolve()
    environment = os.environ.get(source_lock["libraryRootEnvironmentVariable"])
    return Path(environment or source_lock["defaultLibraryRoot"]).resolve()


def verify_sources(repo_root: Path, library_root: Path | None = None) -> tuple[dict, Path]:
    source_lock = load_source_lock(repo_root)
    resolved_library = resolve_library_root(source_lock, library_root)
    for font in source_lock["fonts"]:
        for entry_name in ("sourceFile", "sourceArchive", "license"):
            entry = font.get(entry_name)
            if entry is None:
                continue
            path = resolved_library / Path(entry["path"])
            if not path.is_file():
                raise FontVerificationError(f"missing {entry_name} for {font['id']}: {path}")
            actual = sha256_file(path)
            if actual != entry["sha256"]:
                label = "source SHA-256" if entry_name == "sourceFile" else f"{entry_name} SHA-256"
                raise FontVerificationError(
                    f"{label} mismatch for {font['id']}: expected {entry['sha256']}, actual {actual}"
                )
            expected_bytes = entry.get("bytes")
            if expected_bytes is not None and path.stat().st_size != expected_bytes:
                raise FontVerificationError(f"source byte length mismatch for {font['id']}: {path}")
        source_path = resolved_library / Path(font["sourceFile"]["path"])
        coverage = font_codepoints(source_path)
        gb_missing = {ord(character) for character in gb2312_hanzi()} - coverage
        if gb_missing:
            raise FontVerificationError(f"{font['id']} source is missing {len(gb_missing)} GB2312 Hanzi")
        if not set(font["familyNames"]).intersection(font_names(source_path)):
            raise FontVerificationError(f"source family name mismatch for {font['id']}")
    return source_lock, resolved_library


def verify_fonts(
    repo_root: Path,
    library_root: Path | None = None,
    require_sources: bool = False,
) -> dict:
    repo_root = repo_root.resolve()
    source_lock = load_source_lock(repo_root)
    resolved_library: Path | None = None
    if require_sources:
        source_lock, resolved_library = verify_sources(repo_root, library_root)
    manifest_path = repo_root / ARTIFACT_LOCK
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise FontVerificationError(f"cannot read artifact lock: {manifest_path}: {error}") from error
    required = collect_required_characters(repo_root, repo_root / "scripts/fonts/extra-characters.txt")
    if manifest.get("characterSetSha256") != sha256_text(required):
        raise FontVerificationError("UI character set changed; rebuild font artifacts")
    if manifest.get("characterCount") != len(required):
        raise FontVerificationError("artifact character count is stale")
    if manifest.get("sourceLockSha256") != source_lock_sha256(source_lock):
        raise FontVerificationError("font source lock changed; rebuild font artifacts")
    source_by_id = {entry["id"]: entry for entry in source_lock["fonts"]}
    artifacts = manifest.get("artifacts", [])
    if {entry.get("id") for entry in artifacts} != set(source_by_id):
        raise FontVerificationError("artifact lock must contain the same five font IDs as the source lock")
    required_codepoints = set(map(ord, required))
    gb_codepoints = set(map(ord, gb2312_hanzi()))
    expected_license_files = {entry.get("licenseFileName") for entry in artifacts}
    if None in expected_license_files or len(expected_license_files) != 5:
        raise FontVerificationError("artifact lock must declare exactly five font license files")
    license_root = repo_root / ARTIFACT_LICENSE_ROOT
    actual_license_files = {path.name for path in license_root.iterdir() if path.is_file()}
    if actual_license_files != expected_license_files:
        raise FontVerificationError("packaged font license file set differs from the artifact lock")
    for artifact in artifacts:
        font_id = artifact["id"]
        source = source_by_id[font_id]
        path = repo_root / ARTIFACT_ROOT / artifact["fileName"]
        if not path.is_file():
            raise FontVerificationError(f"missing font artifact for {font_id}: {path}")
        if sha256_file(path) != artifact["sha256"] or path.stat().st_size != artifact["bytes"]:
            raise FontVerificationError(f"artifact hash or length mismatch for {font_id}")
        if not source["subsetAllowed"] and artifact["sha256"] != source["sourceFile"]["sha256"]:
            raise FontVerificationError(f"vendor font is not an exact source copy: {font_id}")
        coverage = font_codepoints(path)
        allowed_missing = set(artifact.get("sourceMissingCodepoints", []))
        missing = (required_codepoints - allowed_missing) - coverage
        if missing:
            preview = "".join(chr(value) for value in sorted(missing)[:32])
            raise FontVerificationError(f"artifact {font_id} lost required characters: {preview}")
        if gb_codepoints - coverage:
            raise FontVerificationError(f"artifact {font_id} does not cover GB2312 6763")
        expected_family = artifact["familyName"]
        if expected_family not in font_names(path):
            raise FontVerificationError(f"artifact family name mismatch for {font_id}: {expected_family}")
        license_path = license_root / artifact["licenseFileName"]
        if (
            sha256_file(license_path) != artifact.get("licenseSha256")
            or license_path.stat().st_size != artifact.get("licenseBytes")
        ):
            raise FontVerificationError(f"packaged license hash or length mismatch for {font_id}")
        if font_id == "source-han-serif" and allowed_missing:
            raise FontVerificationError("default Source Han Serif may not miss any required character")
        if resolved_library is not None:
            source_path = resolved_library / Path(source["sourceFile"]["path"])
            actual_source_missing = required_codepoints - font_codepoints(source_path)
            if actual_source_missing != allowed_missing:
                raise FontVerificationError(f"source coverage declaration is stale for {font_id}")
            if not source["subsetAllowed"] and path.read_bytes() != source_path.read_bytes():
                raise FontVerificationError(f"vendor font bytes differ from local source: {font_id}")
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--font-library", type=Path)
    parser.add_argument("--require-sources", action="store_true")
    args = parser.parse_args()
    try:
        manifest = verify_fonts(args.repo_root, args.font_library, args.require_sources)
    except FontVerificationError as error:
        parser.exit(1, f"font verification failed: {error}\n")
    print(f"verified {len(manifest['artifacts'])} font artifacts ({manifest['characterCount']} characters)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
