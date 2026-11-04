#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-3.0-only
set -euo pipefail
root=$(cd "$(dirname "$0")/.." && pwd)
mkdir -p "$root/.tools/tmp" "$root/.tools/home/.local/share/NuGet/Migrations"
# Bypass a migration already irrelevant to this isolated package cache.
touch "$root/.tools/home/.local/share/NuGet/Migrations/1"
if [[ ! -x "$root/.tools/dotnet/dotnet" ]]; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$root/.tools/dotnet-install.sh"
  bash "$root/.tools/dotnet-install.sh" --version 8.0.427 --install-dir "$root/.tools/dotnet"
fi
if [[ ! -f "$root/.tools/bepinex/BepInEx/core/BepInEx.dll" ]]; then
  curl -fsSL https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip -o "$root/.tools/bepinex.zip"
  unzip -q "$root/.tools/bepinex.zip" -d "$root/.tools/bepinex"
fi
if [[ ! -x "$root/.tools/ilspy/ilspycmd" ]]; then
  "$root/dotnet.sh" tool install ilspycmd --version 9.1.0.7988 --tool-path "$root/.tools/ilspy"
fi
