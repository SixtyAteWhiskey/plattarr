---
title: Preserve the working production indexer behavior
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-8aa6e19a
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileCardigannParityFixture.cs
  - src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannParser.cs
---

## Decided
Keep production indexer behavior as it currently works and avoid changes to it when possible, per the owner's direction. Retain the native FunFile provider; future replacement work must satisfy the existing compatibility and conversion gates before any transition.

## Tried and ruled out
The optional Cardigann release GUID parser change was removed from this rollout because it changes shared indexer parsing and is not needed to preserve the current working provider. The candidate definition remains only a fixture, not an active replacement.

## Watch out
A matching configuration alone does not establish unchanged search behavior. Avoid live indexer tests or searches for development; use disposable evidence and compare provider settings across any approved rollout.

In the code: src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannParser.cs; src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileCardigannParityFixture.cs; docs/refactoring-plan.md.
