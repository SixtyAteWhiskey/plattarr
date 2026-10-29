---
title: Use the existing action index before adding a migration
author: SixtyAteWhiskey
date: 2026-10-29
branch: refactor/settings-route-loading
id: l-bec25510
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueActionServiceFixture.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneRepositories.cs
---

## Decided
For the first Hygiene queue refresh slice, filter the latest-action check by download client, download ID, and reason in SQL. The existing index starts with client and download, so SQLite already seeks to a small identity group. This removes repeated full-journal materialization without a schema migration.

## Tried and ruled out
Keeping the full-journal read was measured on synthetic action histories and scaled with total journal size. A new composite index was unnecessary for this slice because the existing index supports the selective portion of the query.

## Watch out
This is only the eligible-action identity check. Interrupted-action recovery, strike observation, and preview still perform full reads. The synthetic SQLite timing does not establish whole-refresh latency or memory savings; profile those paths separately before changing them.

In the code: `HygieneActionRepository.FindLatest`, `HygieneQueueActionService.Execute`, `tools/benchmark_hygiene_action_lookup.py`; 100 and 10,000 action rows, 20 candidate lookups.
