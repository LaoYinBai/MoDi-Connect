#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""墨堤内容同步验证：content/（单一来源）与 Android assets 必须逐字节一致。

用法:
    python scripts/verify-content-sync.py [repo_root]

退出码 0 = 一致；1 = 存在漂移。
"""
import hashlib
import sys
from pathlib import Path

FILES = ("Stories.md", "TechnicalSupport.md", "Sponsors.md")


def main() -> int:
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
    source = root / "content"
    android = root / "android" / "app" / "src" / "main" / "assets" / "content"

    failed = False
    for name in FILES:
        src = source / name
        dst = android / name
        if not src.exists():
            print(f"[缺失] 内容源不存在: {src}")
            failed = True
            continue
        if not dst.exists():
            print(f"[缺失] Android assets 不存在: {dst}")
            failed = True
            continue
        src_hash = hashlib.sha256(src.read_bytes()).hexdigest()[:12]
        dst_hash = hashlib.sha256(dst.read_bytes()).hexdigest()[:12]
        if src_hash != dst_hash:
            print(f"[漂移] {name}: content={src_hash} assets={dst_hash}")
            failed = True
        else:
            print(f"[一致] {name}  sha256={src_hash}")

    # 反向检查：assets 里不允许出现 content/ 没有的文件（防止悄悄新增副本）
    extra = sorted(
        p.name for p in android.glob("*.md") if p.name not in FILES
    ) if android.exists() else []
    if extra:
        print(f"[多余] Android assets 存在未纳入共享源的 md: {extra}")
        failed = True

    print("结论:", "存在漂移" if failed else "双端内容一致")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
