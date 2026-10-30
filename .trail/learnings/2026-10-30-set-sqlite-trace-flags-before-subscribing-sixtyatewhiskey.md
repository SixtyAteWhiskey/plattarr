---
title: Set SQLite trace flags before subscribing to statement and row events
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-d760f1ec
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueRefreshBenchmark.cs
---

## Decided
For a whole-service SQLite benchmark, attach a Trace2 handler to each connection opened by the test database and enable statement and row flags before subscribing. Count only during the measured refresh, after fixture seeding. This captures SQL from repositories that open their own connections.

## Tried and ruled out
Subscribing to Trace2 with its default flags failed immediately with `InvalidOperationException: cannot add trace2 event handler with no flags set`. Counting only a single connection would also miss repository calls, because each repository operation opens a fresh connection.

## Watch out
Trace2 row events count result rows returned by SQLite, not every row scanned internally. The callback adds some timing overhead, so compare before and after with the same instrumented harness and workload.

In the code: `src/NzbDrone.Core.Test/QueueTests/HygieneQueueRefreshBenchmark.cs`; `SQLiteConnection.TraceFlags = SQLiteTraceFlags.SQLITE_TRACE_STMT | SQLiteTraceFlags.SQLITE_TRACE_ROW` before `connection.Trace2 += ...`.
