---
title: Keep the indexer catalog shared and place media tabs with their options
author: SixtyAteWhiskey
date: 2026-11-01
branch: codex/container-fatal-startup-exit-20261031
id: l-77cbe7c0
scope: repo
status: active
src: agent
touches:
  - frontend/src/Settings/Indexers/IndexerSettings.tsx
  - frontend/src/Settings/Indexers/Indexers/Indexer.css
  - frontend/src/Settings/Indexers/Indexers/Indexer.css.d.ts
  - frontend/src/Settings/Indexers/Indexers/Indexer.tsx
  - frontend/src/Settings/Indexers/Indexers/Indexers.tsx
  - frontend/src/Settings/Indexers/Options/IndexerOptions.tsx
  - frontend/src/typings/Indexer.ts
  - src/NzbDrone.Core/Localization/Core/en.json
  - src/Radarr.Api.V3/Indexers/IndexerResource.cs
---

## Decided
Show one shared list of configured indexers. Put Movies, TV, and Music switching inside the Options section, because that switch changes the options form rather than the catalog. Show media category badges on each indexer card so the catalog explains which search categories it can cover.

## Tried and ruled out
The old page-level media switch made the same indexer cards appear to be separate lists. Deriving badges from raw category IDs in the frontend would miss per-media category overrides, so the API computes them with the category service used by search.

## Watch out
A category badge indicates search category support. Actual searches can still exclude an indexer because of its search-mode flags, tags on the item, or temporary unavailability. Do not call the badges an exact per-item active-indexer count.

In the code: `frontend/src/Settings/Indexers/IndexerSettings.tsx`, `frontend/src/Settings/Indexers/Options/IndexerOptions.tsx`, `frontend/src/Settings/Indexers/Indexers/Indexer.tsx`, and `src/Radarr.Api.V3/Indexers/IndexerResource.cs`.
