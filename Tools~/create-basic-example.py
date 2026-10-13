#!/usr/bin/env python3
"""Create a standalone Unity project containing the complete MUI Examples workbench."""

import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile


PACKAGE_NAME = "com.mzbswh.mui"


def write_json(path, document):
    path.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="new project directory outside the MUI package")
    args = parser.parse_args()

    package_root = Path(__file__).resolve().parent.parent
    sample = package_root / "Samples~"
    package = json.loads((package_root / "package.json").read_text(encoding="utf-8"))
    sample_path = Path("Assets/Samples") / package["displayName"] / package["version"] / "MUI Examples"
    output = args.output.expanduser().resolve()
    if output == package_root or package_root in output.parents or output in package_root.parents:
        parser.error("The output must be outside the MUI package directory.")
    if output.exists():
        parser.error(f"The output already exists: {output}")
    if not output.parent.is_dir():
        parser.error(f"The output parent does not exist: {output.parent}")

    package_path = os.path.relpath(package_root, output / "Packages").replace(os.sep, "/")
    package_reference = f"file:{package_path}"
    staging = Path(tempfile.mkdtemp(prefix=".mui-examples-", dir=output.parent))
    try:
        (staging / "Packages").mkdir()
        settings = staging / "ProjectSettings"
        settings.mkdir()
        (settings / "ProjectVersion.txt").write_text("m_EditorVersion: 2022.3.62f3\n", encoding="utf-8")
        assets = staging / "Assets"
        assets.mkdir()
        shutil.copytree(sample, staging / sample_path)
        write_json(staging / "Packages/manifest.json", {"dependencies": {
            PACKAGE_NAME: package_reference,
            "com.unity.ugui": "1.0.0",
        }})

        (staging / "README.md").write_text(
            "# MUI Examples\n\n"
            "用 Unity 2022.3.62f3 打开此目录，然后打开 "
            f"{sample_path.as_posix()}/Scenes/MUIExamples.unity 并进入 Play 模式。"
            f"从左侧选择 11 个功能课程，右侧查看演示、配置操作和对应代码；可编辑实际 Prefab，教程位于 {sample_path.as_posix()}/Scripts。\n\n"
            "Packages 清单引用创建此项目时使用的 MUI 源码目录。"
            "源码目录移动后请重新生成此项目。\n",
            encoding="utf-8",
        )
        staging.rename(output)
    finally:
        if staging.exists():
            shutil.rmtree(staging)

    print(output)


if __name__ == "__main__":
    main()
