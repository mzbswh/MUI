#!/usr/bin/env python3
"""Create a standalone Unity project from the in-package Basic Example template."""

import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile


PACKAGE_NAME = "com.mzbswh.mui"
PROJECT_DIRECTORIES = ("Packages", "ProjectSettings")


def write_json(path, document):
    path.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="new project directory outside the MUI package")
    args = parser.parse_args()

    package_root = Path(__file__).resolve().parent.parent
    template = package_root / "ExampleProject~"
    sample = package_root / "Samples~/BasicExample"
    output = args.output.expanduser().resolve()
    if output == package_root or package_root in output.parents or output in package_root.parents:
        parser.error("The output must be outside the MUI package directory.")
    if output.exists():
        parser.error(f"The output already exists: {output}")
    if not output.parent.is_dir():
        parser.error(f"The output parent does not exist: {output.parent}")

    package_path = os.path.relpath(package_root, output / "Packages").replace(os.sep, "/")
    package_reference = f"file:{package_path}"
    staging = Path(tempfile.mkdtemp(prefix=".mui-basic-example-", dir=output.parent))
    try:
        for name in PROJECT_DIRECTORIES:
            shutil.copytree(template / name, staging / name)
        assets = staging / "Assets"
        assets.mkdir()
        shutil.copytree(sample, assets / "Basic")
        shutil.copy2(sample.with_suffix(".meta"), assets / "Basic.meta")

        manifest_path = staging / "Packages/manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["dependencies"][PACKAGE_NAME] = package_reference
        write_json(manifest_path, manifest)

        lock_path = staging / "Packages/packages-lock.json"
        lock = json.loads(lock_path.read_text(encoding="utf-8"))
        lock["dependencies"][PACKAGE_NAME]["version"] = package_reference
        write_json(lock_path, lock)

        (staging / "README.md").write_text(
            "# MUI Basic Example\n\n"
            "Open this directory with Unity 2022.3.62f3, then open "
            "Assets/Basic/Basic.unity and enter Play Mode. "
            "Done completes the page with result 42; Open page opens it again.\n\n"
            "The Packages manifest references the MUI source directory used to create this project. "
            "Regenerate this project if that directory moves.\n",
            encoding="utf-8",
        )
        staging.rename(output)
    finally:
        if staging.exists():
            shutil.rmtree(staging)

    print(output)


if __name__ == "__main__":
    main()
