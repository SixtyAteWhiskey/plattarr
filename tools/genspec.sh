#!/usr/bin/env bash
# Regenerates src/Radarr.Api.V3/openapi.json. Needed whenever a controller or resource is added
# or changed. Only a Debug build writes the spec. Port 7880 must be free.
# Compare the result with `git diff --stat src/Radarr.Api.V3/openapi.json`: it should hold only
# the additions you made.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$root/tools/env.sh"
cd "$root"

dotnet build src/Radarr.sln -c Debug --no-restore -p:RunAnalyzers=false -p:TreatWarningsAsErrors=false

data="$(mktemp -d)"
cat > "$data/config.xml" <<XML
<Config>
  <BindAddress>127.0.0.1</BindAddress>
  <Port>7880</Port>
  <ApiKey>00000000000000000000000000000000</ApiKey>
  <AuthenticationMethod>External</AuthenticationMethod>
</Config>
XML

dotnet _output/net10.0/Radarr.dll -nobrowser -data="$data" > "$data/run.log" 2>&1 &
pid=$!
trap 'kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true; rm -rf "$data"' EXIT

timeout 180 bash -c 'until curl -sf http://127.0.0.1:7880/ping >/dev/null; do sleep 2; done'
curl -sf http://127.0.0.1:7880/docs/v3/openapi.json -o "$root/src/Radarr.Api.V3/openapi.json"
echo "openapi.json written"
