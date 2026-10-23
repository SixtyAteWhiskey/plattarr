#!/usr/bin/env bash
# The check to run before every push: a non-incremental solution build with the analyzers on
# (warnings are errors), then the Api, Host and Core unit tests. Takes about 5 minutes.
# Run it in the background and wait for the last line:
#   tools/verify.sh > /tmp/verify.log 2>&1 &
#   timeout 580 bash -c "until grep -q '^done' /tmp/verify.log; do sleep 5; done"
# Set FILTER to run a subset of Core tests, for example FILTER=FullyQualifiedName~ModuleCategory.
set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$root/tools/env.sh"
cd "$root"

cat_filter='Category!=IntegrationTest&Category!=ManualTest&Category!=AutomationTest&Category!=DiskAccessTest'

echo "== build"
if ! dotnet build src/Radarr.sln -c Release --no-restore --no-incremental; then
    echo "BUILD FAILED"
    echo "done"
    exit 1
fi

mkdir -p _tests/net10.0
cp _output/net10.0/Radarr.Mono.* _tests/net10.0/ 2>/dev/null || true

status=0
for project in NzbDrone.Api.Test NzbDrone.Host.Test NzbDrone.Core.Test; do
    echo "== test $project"
    f="$cat_filter"
    if [ "$project" = "NzbDrone.Core.Test" ] && [ -n "${FILTER:-}" ]; then
        f="($cat_filter)&${FILTER}"
    fi
    dotnet test "src/$project/${project/NzbDrone./Radarr.}.csproj" -c Release --no-build --filter "$f" || status=1
done

echo "== summary: $([ $status -eq 0 ] && echo PASS || echo FAIL)"
echo "done"
exit $status
