---
title: Give replacement definitions an explicit release identity before retiring a native indexer
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-63ec7555
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannParser.cs
  - src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileCardigannParityFixture.cs
---

## Decided
Add an optional release GUID field to the Cardigann parser and prove a FunFile candidate definition against the native request and parsed result. Existing definitions keep their download URL GUID when no field is supplied. This leaves currently configured providers alone while making a later definition correction possible.

## Tried and ruled out
Treating matching title and download fields as sufficient parity. FunFile's native GUID is its details URL, and changing it can affect deduplication and release identity. A test-only YAML edit also cannot change the updating definition the app actually loads.

## Watch out
The candidate sets the IMDb title flag and derives GUID from parsed details only in a synthetic fixture. Authenticated login, live search results, and other page shapes remain unproved. Do not switch saved rows or imports until a corrected definition is shipped and those paths are verified.

In the code: `src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannParser.cs` accepts `guid`; `src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileCardigannParityFixture.cs` compares the candidate; `docs/refactoring-plan.md` keeps the runtime conversion gate open. Commit `cbbb43e27` is the parser and fixture slice.
