# tools

Scripts for building and checking Plattarr in a fresh Linux container. None of them touch a
real instance, and none holds a host name, address or key.

| Script | Use |
|---|---|
| `dev-setup.sh [--frontend]` | Install the pinned .NET SDK, restore the solution, optionally `yarn install`. |
| `env.sh` | Source it (`. tools/env.sh`) to get the .NET environment in the current shell. |
| `verify.sh` | Pre-push check: non-incremental Release build with analyzers, then Api, Host and Core tests. |
| `genspec.sh` | Regenerate `src/Radarr.Api.V3/openapi.json` from a Debug build. |

Run `verify.sh` in the background and wait for its last line (`done`); see the header of the script.
The frontend gates (`yarn lint-fix`, `npx tsc --noEmit`, `yarn build`) run from the repository root.
