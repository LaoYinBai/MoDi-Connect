from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from scripts.fonts.collect_characters import (
    collect_required_characters,
    gb2312_hanzi,
    to_unicodes,
)


class CollectCharactersTests(unittest.TestCase):
    def test_gb2312_baseline_has_exactly_6763_hanzi(self) -> None:
        characters = gb2312_hanzi()

        self.assertEqual(6763, len(characters))
        self.assertEqual(6763, len(set(characters)))
        self.assertIn("墨", characters)
        self.assertIn("堤", characters)

    def test_collects_baseline_ui_sources_and_manual_additions_stably(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            repo_root = Path(directory)
            source_files = {
                "android/app/src/main/java/Screen.kt": 'Text("安卓🎵")',
                "android/app/src/main/res/values/strings.xml": '<string name="mode">万能模式</string>',
                "windows/MoDi.Presentation/View.axaml": '<TextBlock Text="Windows 窗體" />',
                "windows/MoDi.Desktop/Content/help.md": "正文：龍",
                "test-ui/UITest/Demo.cs": 'const string Label = "测试 UI";',
            }
            for relative_path, content in source_files.items():
                path = repo_root / relative_path
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding="utf-8")

            excluded = repo_root / "android/app/build/generated/Excluded.kt"
            excluded.parent.mkdir(parents=True)
            excluded.write_text('Text("𠮷")', encoding="utf-8")

            unit_test = repo_root / "android/app/src/test/java/FontContractTest.kt"
            unit_test.parent.mkdir(parents=True)
            unit_test.write_text('assertEquals("𪚥", actual)', encoding="utf-8")

            extra_path = repo_root / "scripts/fonts/extra-characters.txt"
            extra_path.parent.mkdir(parents=True)
            extra_path.write_text("龘\n𠀀\n", encoding="utf-8")

            characters = collect_required_characters(repo_root, extra_path)

        self.assertEqual(characters, "".join(sorted(set(characters), key=ord)))
        self.assertTrue(set(chr(codepoint) for codepoint in range(0x20, 0x7F)).issubset(characters))
        self.assertTrue(set(gb2312_hanzi()).issubset(characters))
        self.assertTrue(set("，。！？；：‘’“”（）【】《》￥…—·、✓").issubset(characters))
        self.assertTrue(set("安卓🎵万能模式Windows窗體正文龍测试UI龘𠀀").issubset(characters))
        self.assertNotIn("𠮷", characters)
        self.assertNotIn("𪚥", characters)

    def test_formats_fonttools_unicode_list(self) -> None:
        self.assertEqual("U+0020,U+0041,U+4E2D,U+1F600", to_unicodes("😀中A "))


if __name__ == "__main__":
    unittest.main()
