---
title: Container fatal startup uses the existing immediate exit flag
author: SixtyAteWhiskey
date: 2026-11-01
branch: codex/container-fatal-startup-exit-20261031
id: l-c026ec68
scope: repo
status: active
src: agent
touches:
  - docker/entrypoint.sh
---

## Decided
Pass the existing immediate exit startup flag from both container entrypoint paths. The console already uses it to exit with code 3 after a nonrecoverable startup failure, while direct desktop launches retain their wait for intervention and utility modes retain their own exit codes.

In the code: docker/entrypoint.sh passes `-exitimmediately` to `/app/bin/Radarr`; `ConsoleApp.Exit` checks `StartupContext.ExitImmediately` only for `NonRecoverableFailure`.

## Tried and ruled out
Changing the console's general failure wait would also alter interactive desktop behavior. The container entrypoint is the narrower scope.

## Watch out
The image health check only reports unhealthy; a restart policy needs the main process to exit. A malformed disposable config reached the fatal handler and exited with status 3 in about 1.3 seconds with the flag; without it, the process was still in the intervention wait at five seconds.
