---
title: Match indexer replacements by definition ID and preserve stored providers
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-912af880
scope: repo
status: active
src: agent
touches:
  - docs/pruned-providers.md
  - docs/refactoring-plan.md
  - src/NzbDrone.Core/Indexers/IndexerFactory.cs
  - src/NzbDrone.Core/ThingiProvider/ProviderFactory.cs
---

## Decided
Use the Cardigann definition ID from the live schema to identify replacement candidates. Keep native implementations until an explicit conversion of existing stored provider rows and their settings has been verified. The live production inventory has enabled Knaben and SubsPlease, so they remain retained.

## Tried and ruled out
Matching by display name alone missed HD-Space and HD-Torrents because their Cardigann labels differ from the native class names. Removing classes just because they are absent from the current owner's configuration was ruled out: another installation may still have stored rows for them.

## Watch out
An obsolete native class may be hidden from the add-indexer schema but still load existing rows. Deleting its type makes startup remove those rows; the importer also rejects the missing implementation. Cardigann discoverability proves neither settings conversion nor search parity.

In the code: `GET /api/v3/indexer` and `/api/v3/indexer/schema` on the production instance were queried read-only and only aggregate counts and definition IDs were used; `src/NzbDrone.Core/Indexers/IndexerFactory.cs` filters obsolete defaults; `src/NzbDrone.Core/ThingiProvider/ProviderFactory.cs` removes missing implementations; `docs/refactoring-plan.md` records the five candidate IDs and replacement work.
