# tools

Scripts for building and checking Plattarr in a fresh Linux container. None of them touch a
real instance, and none holds a host name, address or key.

| Script | Use |
|---|---|
| `dev-setup.sh [--frontend]` | Install the pinned .NET SDK, restore the solution, optionally `yarn install`. |
| `env.sh` | Source it (`. tools/env.sh`) to get the .NET environment in the current shell. |
| `verify.sh` | Pre-push check: non-incremental Release build with analyzers, then Api, Host and Core tests. |
| `genspec.sh` | Regenerate `src/Radarr.Api.V3/openapi.json` from a Debug build. |
| `benchmark_refactor_baseline.py` | Measure startup, RSS, and empty-fixture API latency on a disposable loopback Release app. |
| `benchmark_frontend_routes.py` | Measure browser transfer, first render, script time, and first/repeat Settings navigation against the current production UI on a disposable loopback Release app. Requires Chrome and Python `websockets`. |

`liveproof/` is different: it talks to a running instance over HTTP (see `liveproof/README.md`). It changes only what it
journals and restores, but point it at the dev stack or a scratch instance, never at a live app.

Run `verify.sh` in the background and wait for its last line (`done`); see the header of the script.
The frontend gates (`yarn lint-fix`, `npx tsc --noEmit`, `yarn build`) run from the repository root.
