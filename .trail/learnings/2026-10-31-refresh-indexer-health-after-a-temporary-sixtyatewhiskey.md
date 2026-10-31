---
title: Refresh indexer health after a temporary block expires
author: SixtyAteWhiskey
date: 2026-10-31
branch: codex/queue-error-audit-20261030
id: l-12f21cf4
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/HealthCheck/HealthCheckServiceFixture.cs
  - src/NzbDrone.Core.Test/JobTests/TaskManagerFixture.cs
  - src/NzbDrone.Core/HealthCheck/HealthCheckService.cs
  - src/NzbDrone.Core/Jobs/TaskManager.cs
---

## Decided
Run only the two indexer status health checks every five minutes. Their warning cache needs a periodic reevaluation because an expired backoff deadline changes `isDisabled` immediately but emits no status-change event. Keep the existing six-hour full health schedule, and do not test or search a live indexer to clear the warning.

In the code: `IndexerStatusCheck` and `IndexerLongTermStatusCheck` read `GetBlockedProviders()`. `IndexerResourceMapper` reads `IsDisabled()`. `ProviderStatusBase.IsDisabled()` compares `DisabledTill` with UTC now. `HealthCheckService.Results()` returns cached findings. `TaskManager` schedules full `CheckHealthCommand` every 360 minutes; `CheckIndexerStatusHealthCommand` schedules the two indexer checks every five minutes.

## Tried and ruled out
A read-only production comparison found the health API still reporting `IndexerStatusCheck` for Knaben while the indexer API reported `isDisabled=false`. The stored deadline had expired about 24 minutes earlier, and the next full health check was hours away. Running a live indexer test was ruled out by the production boundary and would not fix the refresh gap. Shortening the full health interval was ruled out because it would run unrelated checks more often.

## Watch out
An Enabled indexer card only means the provider is configured for use and is not currently blocked; it does not prove connectivity. The targeted refresh bounds stale indexer health warnings to roughly five minutes after expiry, subject to task scheduling, and it performs no provider network call. Do not publish keys, host details, or raw indexer errors from production observations.
