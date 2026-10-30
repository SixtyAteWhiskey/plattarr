---
title: Keep configured native indexers until replacement is proven
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-292c67c0
scope: repo
status: active
src: agent
touches:
  - docs/pruned-providers.md
  - docs/refactoring-plan.md
  - docs/importer.md
  - src/NzbDrone.Core/ThingiProvider/ProviderFactory.cs
  - src/NzbDrone.Core/IndexerVersions/IndexerDefinitionUpdateService.cs
  - src/NzbDrone.Core/Upstream/IndexersStage.cs
---

## Decided
Keep Knaben and SubsPlease while they are documented as configured and no working Cardigann replacement has been verified. Before removing any native indexer, compare a current sanitized configuration list with what Plattarr actually loads and verify the candidate's import path. A historical list is insufficient to prove current use.

## Tried and ruled out
Using the historical Prowlarr row counts alone to select deletions: they identify two native providers in use but do not include the current configured list or all Cardigann definition IDs. Name matching against upstream YAML alone also cannot prove a working replacement.

## Watch out
Removing a native implementation can delete its stored provider row on startup and make the upstream importer skip it. Review migrations, imports, tests, generated resources, and packaging before any removal.

In the code: `docs/refactoring-plan.md` section 4 and the 2026-10-29 indexer inventory; `docs/importer.md` Prowlarr row counts; `src/NzbDrone.Core/ThingiProvider/ProviderFactory.cs` `RemoveMissingImplementations`; `src/NzbDrone.Core/IndexerVersions/IndexerDefinitionUpdateService.cs` version 12 request and definition blocklist; `src/NzbDrone.Core/Upstream/IndexersStage.cs` provider import.
