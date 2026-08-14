"""Build deterministic application fonts from the locked local source library."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import tempfile
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from fontTools import subset
from fontTools.ttLib import TTFont

from scripts.fonts.collect_characters import collect_required_characters
from scripts.fonts.verify_fonts import (
    ARTIFACT_LICENSE_ROOT,
    ARTIFACT_LOCK,
    ARTIFACT_ROOT,
    FontVerificationError,
    font_codepoints,
    font_names,
    sha256_file,
    sha256_text,
    source_lock_sha256,
    verify_fonts,
    verify_sources,
)


OUTPUT_FILES = {
    "alimama-dongfang-dakai": "modi_title_alimama_dongfang_dakai.ttf",
    "lxgw-wenkai": "modi_function_lxgw_wenkai.ttf",
    "zhuque-fangsong": "modi_body_zhuque_fangsong.ttf",
    "genyo-mincho": "modi_annotation_genyo_mincho.otf",
    "source-han-serif": "modi_default_source_han_serif.otf",
}

OUTPUT_FAMILIES = {
    "alimama-dongfang-dakai": "Alimama DongFangDaKai",
    "lxgw-wenkai": "MoDi UI Function LXGW WenKai",
    "zhuque-fangsong": "MoDi UI Body Zhuque Fangsong",
    "genyo-mincho": "MoDi UI Annotation GenYo Mincho",
    "source-han-serif": "MoDi UI Default Source Han Serif",
}

LICENSE_OUTPUT_FILES = {
    "alimama-dongfang-dakai": "alimama_dongfang_dakai_license.txt",
    "lxgw-wenkai": "lxgw_wenkai_ofl.txt",
    "zhuque-fangsong": "zhuque_fangsong_ofl.txt",
    "genyo-mincho": "genyo_mincho_ofl.txt",
    "source-han-serif": "source_han_serif_ofl.txt",
}


def _rename_modified_font(font: TTFont, family: str) -> None:
    name = font["name"]
    for name_id in (1, 2, 3, 4, 6, 16, 17, 18, 21, 22):
        name.names = [record for record in name.names if record.nameID != name_id]
    postscript = family.replace(" ", "-")
    values = {
        1: family,
        2: "Regular",
        3: f"MoDi:{postscript}:1.0",
        4: family,
        6: postscript,
        16: family,
        17: "Regular",
    }
    for name_id, value in values.items():
        name.setName(value, name_id, 3, 1, 0x0409)
        name.setName(value, name_id, 0, 3, 0)


def _subset_font(source: Path, destination: Path, codepoints: set[int], family: str) -> None:
    font = TTFont(source, recalcTimestamp=False, lazy=False)
    try:
        options = subset.Options()
        options.hinting = True
        options.layout_features = ["*"]
        options.name_IDs = ["*"]
        options.name_languages = ["*"]
        options.name_legacy = True
        options.notdef_glyph = True
        options.notdef_outline = True
        options.recommended_glyphs = True
        options.recalc_timestamp = False
        options.canonical_order = True
        subsetter = subset.Subsetter(options=options)
        subsetter.populate(unicodes=codepoints)
        subsetter.subset(font)
        _rename_modified_font(font, family)
        font.save(destination, reorderTables=True)
    finally:
        font.close()


def build_fonts(repo_root: Path, library_root: Path | None = None) -> dict:
    repo_root = repo_root.resolve()
    source_lock, resolved_library = verify_sources(repo_root, library_root)
    required = collect_required_characters(repo_root, repo_root / "scripts/fonts/extra-characters.txt")
    required_codepoints = set(map(ord, required))
    artifacts: list[dict] = []
    artifact_parent = (repo_root / ARTIFACT_ROOT).parent
    artifact_parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".modi-font-build-", dir=artifact_parent) as directory:
        temporary = Path(directory)
        temporary_font_root = temporary / "font"
        temporary_license_root = temporary / "raw"
        temporary_font_root.mkdir()
        temporary_license_root.mkdir()
        for entry in source_lock["fonts"]:
            font_id = entry["id"]
            source = resolved_library / Path(entry["sourceFile"]["path"])
            destination = temporary_font_root / OUTPUT_FILES[font_id]
            license_source = resolved_library / Path(entry["license"]["path"])
            license_destination = temporary_license_root / LICENSE_OUTPUT_FILES[font_id]
            shutil.copyfile(license_source, license_destination)
            source_coverage = font_codepoints(source)
            missing = sorted(required_codepoints - source_coverage)
            if entry["subsetAllowed"]:
                _subset_font(
                    source,
                    destination,
                    required_codepoints.intersection(source_coverage),
                    OUTPUT_FAMILIES[font_id],
                )
            else:
                shutil.copyfile(source, destination)
            artifacts.append(
                {
                    "id": font_id,
                    "role": entry["role"],
                    "fileName": destination.name,
                    "familyName": OUTPUT_FAMILIES[font_id],
                    "bytes": destination.stat().st_size,
                    "sha256": sha256_file(destination),
                    "sourceSha256": entry["sourceFile"]["sha256"],
                    "sourceMissingCodepoints": missing,
                    "licenseFileName": license_destination.name,
                    "licenseBytes": license_destination.stat().st_size,
                    "licenseSha256": sha256_file(license_destination),
                }
            )
        for artifact in artifacts:
            font_id = artifact["id"]
            staged = temporary_font_root / artifact["fileName"]
            source = next(entry for entry in source_lock["fonts"] if entry["id"] == font_id)
            allowed_missing = set(artifact["sourceMissingCodepoints"])
            missing = (required_codepoints - allowed_missing) - font_codepoints(staged)
            if missing:
                preview = "".join(chr(value) for value in sorted(missing)[:32])
                raise FontVerificationError(f"staged artifact {font_id} lost required characters: {preview}")
            if OUTPUT_FAMILIES[font_id] not in font_names(staged):
                raise FontVerificationError(f"staged artifact family name mismatch for {font_id}")
            if not source["subsetAllowed"]:
                source_path = resolved_library / Path(source["sourceFile"]["path"])
                if staged.read_bytes() != source_path.read_bytes():
                    raise FontVerificationError(f"vendor font bytes changed while staging: {font_id}")
            if font_id == "source-han-serif" and allowed_missing:
                raise FontVerificationError("default Source Han Serif cannot provide full fallback coverage")
        manifest = {
            "schemaVersion": 1,
            "characterCount": len(required),
            "characterSetSha256": sha256_text(required),
            "sourceLockSha256": source_lock_sha256(source_lock),
            "artifacts": artifacts,
        }
        target = repo_root / ARTIFACT_ROOT
        target.mkdir(parents=True, exist_ok=True)
        license_target = repo_root / ARTIFACT_LICENSE_ROOT
        license_target.mkdir(parents=True, exist_ok=True)
        expected_license_files = {artifact["licenseFileName"] for artifact in artifacts}
        for stale_path in license_target.iterdir():
            if stale_path.is_file() and stale_path.name not in expected_license_files:
                stale_path.unlink()
        for artifact in artifacts:
            os.replace(temporary_font_root / artifact["fileName"], target / artifact["fileName"])
            os.replace(
                temporary_license_root / artifact["licenseFileName"],
                license_target / artifact["licenseFileName"],
            )
        manifest_path = repo_root / ARTIFACT_LOCK
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )
    verify_fonts(repo_root, resolved_library, require_sources=True)
    return manifest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--font-library", type=Path)
    args = parser.parse_args()
    try:
        manifest = build_fonts(args.repo_root, args.font_library)
    except FontVerificationError as error:
        parser.exit(1, f"font build failed: {error}\n")
    total_bytes = sum(artifact["bytes"] for artifact in manifest["artifacts"])
    print(f"built {len(manifest['artifacts'])} fonts, {total_bytes} bytes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
