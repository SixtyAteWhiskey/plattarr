---
title: Scope Hygiene decisions by client and download together
author: SixtyAteWhiskey
date: 2026-10-29
branch: refactor/settings-route-loading
id: l-6c060804
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/Hygiene/HygieneRepositories.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueDecisionService.cs
  - docs/refactoring-plan.md
---

## Decided
Fetch queue states by the exact client and download pairs in the evaluated queue. Preview and execution share this decision service, so the same state selection is used in both paths. Batch at 200 identities to stay below older SQLite parameter limits while keeping the common queue to one query.

## Tried and ruled out
The full-state read scaled with total state history on synthetic fixtures, even when only 20 queue identities mattered. Searching by download ID alone would ignore the unique index's leading client column and could mix equal download IDs from different clients. One query per identity would raise query count unnecessarily.

## Watch out
Strike observation still loads all states because it also retires entries older than 30 days. The decision lookup does not solve that retention path. Synthetic SQLite read times do not establish whole-refresh latency, memory, or PostgreSQL performance.

In the code: `HygieneQueueStateRepository.FindByIdentities`, `HygieneQueueDecisionService.Evaluate`, `tools/benchmark_hygiene_state_lookup.py`; 20 identities across 100, 10,000, and 100,000 synthetic state rows, with batches of 200.
