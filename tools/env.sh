# Source this file: `. tools/env.sh`. Sets the .NET environment the other scripts and
# your own commands need. The cloud shell keeps nothing between commands, so source it in each.
export DOTNET_ROOT=/root/.dotnet
export PATH=/root/.dotnet:$PATH
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
PLATTARR_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export PLATTARR_ROOT
