---
title: Profile whole queue refresh before changing the next Hygiene read
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-1d6a6ccc
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueRefreshBenchmark.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueStrikeService.cs
  - tools/env.sh
  - src/NzbDrone.Core.Test/Radarr.Core.Test.csproj
---

## Decided
The next Hygiene performance change should target per-row retention deletion. A real `QueueService.Handle` run with a synthetic 100,000-row state and action history spent about 2.75 seconds of a 2.77-second first refresh in strike observation while deleting 999 expired states. The same fixture's later refreshes took about 22 ms after the expired rows were gone.

## Tried and ruled out
The earlier SQL-only read profile suggested more read-path tuning might be next, but it excluded writes and recovery. The whole-service measurement showed that the first-refresh cost was dominated by retention work, so another read-query or index change is not justified by that metric alone.

## Watch out
The benchmark uses an isolated migrated SQLite test database, 20 healthy queue items, one recoverable action, and delayed failed actions. It does not invoke a download client or replacement search, and its process RSS deltas are not retained .NET heap. Preserve the strict 30-day cutoff and refresh current queue states before deleting expired ones.

In the code: `src/NzbDrone.Core.Test/QueueTests/HygieneQueueRefreshBenchmark.cs`; `src/NzbDrone.Core/Hygiene/HygieneQueueStrikeService.cs`; run `. tools/env.sh && dotnet test src/NzbDrone.Core.Test/Radarr.Core.Test.csproj -c Release --no-restore --filter FullyQualifiedName~HygieneQueueRefreshBenchmark --logger 'console;verbosity=normal'`.
