#!/usr/bin/env python3
"""使用仓库的 EditorConfig 格式化 C# 源码；--check 只检查，不改写文件。"""

import argparse
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="发现格式差异时返回非零退出码")
    parser.add_argument("--braces", action="store_true", help="同时逐项目执行 IDE0011 控制流大括号规则")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        parser.error("未找到 dotnet，请先安装 .NET SDK。")

    # 与生成器共用语法规则；保留注释和初始化顺序，不把普通空白检查当作成员布局检查。
    project = root / "Tools~/CodeStyle/MUI.CodeStyle.csproj"
    build = [dotnet, "build", str(project), "--disable-build-servers", "-m:1",
             "-p:UseSharedCompilation=false", "-p:ConcurrentBuild=false",
             "-p:NuGetAudit=false", "--ignore-failed-sources", "--verbosity", "quiet"]
    result = subprocess.run(build, cwd=root).returncode
    if result != 0:
        return result
    layout = [dotnet, str(project.parent / "bin/Debug/net10.0/MUI.CodeStyle.dll"), str(root)]
    if args.check:
        layout.append("--check")
    result = subprocess.run(layout, cwd=root).returncode
    if result != 0:
        return result

    sources = sorted(
        path.relative_to(root).as_posix()
        for folder in ("Runtime", "Editor", "Samples~", "Generators~", "Tools~/CodeStyle")
        for path in (root / folder).rglob("*.cs")
        if not {"bin", "obj"}.intersection(path.relative_to(root).parts)
    )
    if not sources:
        parser.error("未找到 C# 源码，拒绝返回无文件的检查成功结果。")

    # 显式传入文件，避免目录参数未被展开时出现零文件检查通过。
    command = [dotnet, "format", "whitespace", str(root), "--folder", "--include", *sources]
    if args.check:
        command.append("--verify-no-changes")
    command.extend(["--verbosity", "normal"])
    print(f"{'检查' if args.check else '格式化'} {len(sources)} 个 C# 源文件。", flush=True)
    result = subprocess.run(command, cwd=root).returncode
    if result != 0 or not args.braces:
        return result

    projects = sorted((root / "Tools~/Build").glob("*.csproj"))
    projects.extend(sorted((root / "Generators~").rglob("*.csproj")))
    for project in projects:
        command = [
            dotnet, "format", "style", str(project), "--no-restore",
            "--diagnostics", "IDE0011", "--severity", "warn", "--verbosity", "normal",
        ]
        if args.check:
            command.append("--verify-no-changes")
        print(f"{'检查' if args.check else '修复'}大括号规则：{project.relative_to(root)}", flush=True)
        result = subprocess.run(command, cwd=root).returncode
        if result != 0:
            return result
    return 0


if __name__ == "__main__":
    sys.exit(main())
