#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-3.0-only
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
export DOTNET_ROOT="$root/.tools/dotnet" DOTNET_CLI_HOME="$root/.tools/home" NUGET_PACKAGES="$root/.tools/packages" TMPDIR="$root/.tools/tmp"
export XDG_DATA_HOME="$root/.tools/home/.local/share"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
exec "$DOTNET_ROOT/dotnet" "$@"
