---
title: Keep the queue read profile separate from full refresh timing
author: SixtyAteWhiskey
date: 2026-10-29
branch: refactor/settings-route-loading
id: l-e2f231ea
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
---

## Decided
Use the combined SQL read sequence to judge materialization and query-count changes, while leaving the full queue-refresh performance gate open. The repository reads now return far fewer rows on large synthetic histories, but this does not measure the work that executes around them.

## Tried and ruled out
Treating returned-row counts as all database rows scanned was ruled out: SQLite still scans the state-expiry and unfinished-action tables. Treating the Python/SQLite timing or RSS as .NET refresh time or memory was ruled out because the model has no state writes, recovery execution, download-client calls, or queue event overhead.

## Watch out
The read-heavy fixture assumes 20 current queue identities and 20 eligible action lookups. Profile an actual refresh, including recovery and writes, before adding an index or making another repository change based on whole-refresh performance.

In the code: `tools/benchmark_hygiene_refresh_reads.py`; `docs/refactoring-plan.md`; run `python3 tools/benchmark_hygiene_refresh_reads.py` against disposable 100, 10,000, and 100,000-row histories. The current read sequence uses 26 queries versus 25 before the bounded slices because expiry has its own query.
