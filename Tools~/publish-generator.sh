#!/usr/bin/env bash
set -euo pipefail
mui_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet build "$mui_root/Generators~/MUI.Generators/MUI.Generators.csproj" -c Release --nologo --disable-build-servers -m:1 -p:NuGetAudit=false
mkdir -p "$mui_root/Analyzers"
cp "$mui_root/Generators~/MUI.Generators/bin/Release/netstandard2.0/MUI.Generators.dll" "$mui_root/Analyzers/MUI.Generators.dll"
