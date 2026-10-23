#!/usr/bin/env bash
# One-time setup of a fresh build container: installs the .NET SDK that global.json pins and
# restores the solution. Pass --frontend to run `yarn install` too.
# Usage: tools/dev-setup.sh [--frontend]
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
sdk="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$root/global.json" | head -1)"

if [ ! -x /root/.dotnet/dotnet ] || ! /root/.dotnet/dotnet --list-sdks | grep -q "^$sdk"; then
    curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --version "$sdk"
fi

. "$root/tools/env.sh"
cd "$root"
dotnet restore src/Radarr.sln

if [ "${1:-}" = "--frontend" ]; then
    yarn install --frozen-lockfile
fi

echo "setup done (SDK $sdk)"
