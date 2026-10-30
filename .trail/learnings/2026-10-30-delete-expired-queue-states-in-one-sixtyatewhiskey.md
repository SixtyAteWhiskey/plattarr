---
title: Delete expired queue states in one cutoff query after refreshing current downloads
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-b4af8013
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueStateRepositoryFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueStrikeServiceFixture.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueStrikeService.cs
  - src/NzbDrone.Core/Hygiene/HygieneRepositories.cs
  - tools/env.sh
  - src/NzbDrone.Core.Test/Radarr.Core.Test.csproj
---

## Decided
Delete expired Hygiene queue states with a single database predicate using the same strict 30-day cutoff, after updating states for downloads currently in the queue. This keeps a returning download's state while avoiding an expired-row read and one delete per old state.

## Tried and ruled out
Continuing the read-then-delete loop was ruled out by a whole-service SQLite profile: with 100,000 states and 999 expired absent states, strike observation dominated the first refresh at about 2.7 seconds. Batching fetched IDs would still materialize expired rows; a direct cutoff delete expresses the retention rule without that read.

## Watch out
Current queue state updates must precede the cutoff delete. The cutoff is strict (`LastSeenAt < cutoff`), and a state exactly at the cutoff must remain. The existing benchmark uses synthetic SQLite histories; PostgreSQL and real download-client behavior were not profiled.

In the code: `IHygieneQueueStateRepository.DeleteExpired`, `HygieneQueueStrikeService.Observe`, `HygieneQueueRefreshBenchmark`; `. tools/env.sh && dotnet test src/NzbDrone.Core.Test/Radarr.Core.Test.csproj -c Release --no-build --filter FullyQualifiedName~HygieneQueueRefreshBenchmark`.
