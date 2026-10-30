---
title: Test import and startup together before retiring native indexers
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-a8cd77e0
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/Upstream/IndexersStageFixture.cs
  - src/NzbDrone.Core.Test/IndexerTests/IndexerServiceFixture.cs
  - src/NzbDrone.Core/ThingiProvider/ProviderFactory.cs
  - docs/refactoring-plan.md
---

## Decided
Keep a regression gate for both native indexer import and stored-row survival before retiring a provider with a same-ID Cardigann definition. A same-ID definition is only a candidate for replacement; migration still needs settings and search parity evidence.

## Tried and ruled out
Treating a successful upstream import as sufficient retirement proof. Import checks whether the native implementation and settings type exist now, but it does not prove a future build will keep that row at startup after the native type is removed.

## Watch out
The provider factory removes stored rows with missing implementations on startup. Preserve the legacy type or complete and verify conversion before deleting it; a test that only checks the add-indexer schema misses existing rows.

In the code: `src/NzbDrone.Core.Test/Upstream/IndexersStageFixture.cs` covers five candidate native imports; `src/NzbDrone.Core.Test/IndexerTests/IndexerServiceFixture.cs` covers stored-row survival and unknown-row deletion; `src/NzbDrone.Core/ThingiProvider/ProviderFactory.cs` owns startup cleanup; `docs/refactoring-plan.md` section 4 keeps removal open.
