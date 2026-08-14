from __future__ import annotations

import hashlib
import json
import shutil
import tempfile
import unittest
from pathlib import Path

from fontTools.ttLib import TTFont

from scripts.fonts.build_fonts import LICENSE_OUTPUT_FILES, OUTPUT_FILES, build_fonts
from scripts.fonts.collect_characters import collect_required_characters
from scripts.fonts.verify_fonts import FontVerificationError, verify_fonts


REPO_ROOT = Path(__file__).resolve().parents[3]
SOURCE_LOCK = REPO_ROOT / "scripts/fonts/font-sources.lock.json"
LOCAL_LIBRARY = Path(r"D:\MoDi-Local-Font-Library")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


@unittest.skipUnless(LOCAL_LIBRARY.exists(), "完整本地字库不可用")
class FontPipelineTests(unittest.TestCase):
    def _repo_fixture(self, destination: Path) -> Path:
        repo = destination / "repo"
        scripts = repo / "scripts/fonts"
        scripts.mkdir(parents=True)
        shutil.copy2(SOURCE_LOCK, scripts / SOURCE_LOCK.name)
        shutil.copy2(REPO_ROOT / "scripts/fonts/extra-characters.txt", scripts / "extra-characters.txt")

        visible = repo / "android/app/src/main/java/Visible.kt"
        visible.parent.mkdir(parents=True)
        visible.write_text('Text("墨堤：龍✓")', encoding="utf-8")
        return repo

    def test_rejects_tampered_source_before_writing_artifacts(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repo = self._repo_fixture(root)
            library = root / "library"
            lock = json.loads(SOURCE_LOCK.read_text(encoding="utf-8"))
            for entry in lock["fonts"]:
                for item_name in ("sourceFile", "sourceArchive"):
                    if item_name not in entry:
                        continue
                    relative = Path(entry[item_name]["path"])
                    target = library / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(LOCAL_LIBRARY / relative, target)
                license_relative = Path(entry["license"]["path"])
                license_target = library / license_relative
                license_target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(LOCAL_LIBRARY / license_relative, license_target)

            first_source = library / Path(lock["fonts"][0]["sourceFile"]["path"])
            first_source.write_bytes(first_source.read_bytes() + b"tampered")

            with self.assertRaisesRegex(FontVerificationError, "source SHA-256"):
                build_fonts(repo, library)

            self.assertFalse((repo / "assets/fonts/android-res/font").exists())

    def test_builds_covered_fonts_keeps_vendor_font_exact_and_is_deterministic(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo = self._repo_fixture(Path(directory))

            first_manifest = build_fonts(repo, LOCAL_LIBRARY)
            verify_fonts(repo, LOCAL_LIBRARY, require_sources=True)
            first_hashes = {
                name: sha256(repo / "assets/fonts/android-res/font" / name)
                for name in OUTPUT_FILES.values()
            }

            required = set(
                collect_required_characters(
                    repo,
                    repo / "scripts/fonts/extra-characters.txt",
                )
            )
            source_entries = {
                entry["id"]: entry
                for entry in json.loads(SOURCE_LOCK.read_text(encoding="utf-8"))["fonts"]
            }
            for font_id, output_name in OUTPUT_FILES.items():
                font = TTFont(repo / "assets/fonts/android-res/font" / output_name, lazy=True)
                try:
                    coverage = set(font.getBestCmap())
                finally:
                    font.close()
                source = TTFont(
                    LOCAL_LIBRARY / source_entries[font_id]["sourceFile"]["path"],
                    lazy=True,
                )
                try:
                    expected = required.intersection(map(chr, source.getBestCmap()))
                finally:
                    source.close()
                missing = expected.difference(map(chr, coverage))
                self.assertFalse(missing, f"{font_id} lost {''.join(sorted(missing))}")

            default_font = TTFont(
                repo / "assets/fonts/android-res/font" / OUTPUT_FILES["source-han-serif"],
                lazy=True,
            )
            try:
                default_coverage = set(map(chr, default_font.getBestCmap()))
            finally:
                default_font.close()
            self.assertFalse(required - default_coverage, "默认思源宋体必须覆盖全部目标字符")

            vendor_entry = next(
                entry for entry in json.loads(SOURCE_LOCK.read_text(encoding="utf-8"))["fonts"]
                if entry["id"] == "alimama-dongfang-dakai"
            )
            self.assertEqual(
                (LOCAL_LIBRARY / vendor_entry["sourceFile"]["path"]).read_bytes(),
                (repo / "assets/fonts/android-res/font" / OUTPUT_FILES[vendor_entry["id"]]).read_bytes(),
            )
            for entry in source_entries.values():
                self.assertEqual(
                    (LOCAL_LIBRARY / entry["license"]["path"]).read_bytes(),
                    (repo / "assets/fonts/android-res/raw" / LICENSE_OUTPUT_FILES[entry["id"]]).read_bytes(),
                )

            second_manifest = build_fonts(repo, LOCAL_LIBRARY)
            second_hashes = {
                name: sha256(repo / "assets/fonts/android-res/font" / name)
                for name in OUTPUT_FILES.values()
            }
            self.assertEqual(first_manifest, second_manifest)
            self.assertEqual(first_hashes, second_hashes)


if __name__ == "__main__":
    unittest.main()
