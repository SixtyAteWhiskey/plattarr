---
title: Compare Hygiene preview and execution on the same queue snapshot
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-c7557be2
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueActionServiceFixture.cs
  - src/Radarr.Api.V3/Hygiene/HygienePreviewController.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
---

## Decided
Compare fresh preview candidates with newly created actions using the same tracked queue snapshot because the executor receives only tracked refresh items and eligibility can change between requests. Verify a rule disabled after preview separately because execution must recheck before it creates a journal action.

## Tried and ruled out
Treating every preview `WouldAct` result as a promise of a later new action was ruled out because a completed journal entry prevents duplicate execution, and a rule or gate can change between requests.

## Watch out
Preview also includes pending items, while queue refresh processes tracked items. Compare the tracked snapshot when checking selection parity because raw preview and executor item counts can differ. Keep journal idempotency as a separate assertion.

In the code: `src/NzbDrone.Core.Test/QueueTests/HygieneQueueActionServiceFixture.cs` proves the same-snapshot candidate set and rule-change recheck. `src/Radarr.Api.V3/Hygiene/HygienePreviewController.cs` includes pending items; `src/NzbDrone.Core/Hygiene/HygieneActionService.cs` checks the journal and current eligibility.
