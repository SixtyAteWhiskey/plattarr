---
title: Manual TV search must include unmonitored missing episodes
author: SixtyAteWhiskey
date: 2026-11-10
branch: codex/proactive-search-foundation
id: l-29f08a23
scope: repo
status: active
src: agent
touches:
  - docs/proactive-search.md
  - frontend/src/ProactiveSearch/ProactiveSearch.tsx
  - src/NzbDrone.Core.Test/TvTests/SearchTests/TvEpisodeSearchServiceFixture.cs
  - src/NzbDrone.Core/Tv/Search/EpisodeSearchService.cs
  - src/NzbDrone.Core/Tv/Search/MissingEpisodeSearchCommand.cs
  - src/NzbDrone.Core/Tv/Search/SeriesSearchService.cs
---

## Decided
A quick action offered on a skipped TV show should use a series-scoped manual missing-episode search that explicitly includes unmonitored episodes. The user chose a skipped show to search, so the action must send indexer queries even when monitoring caused the skip reason. Keep the override manual and scoped to one series, and retain queue and aired/missing filters.

## Tried and ruled out
The regular SeriesSearch command was used first. On production, several attempted shows had monitored series and seasons but all aired missing episodes were unmonitored. SeriesSearch completed in the same second with zero downloads and no indexer search log, because its monitored-only season path excluded those episodes. Switching the quick action to that command with clearer wording would still leave the user's action ineffective.

## Watch out
A search command's successful completion with zero reports downloaded does not prove an indexer was queried. Check IndexerReleaseFetcher logs or search-request mocks. TV manual search may issue multiple provider queries when a show spans seasons, so keep the confirmation. The command's ignore-monitoring override must never apply to a scheduled or all-series search.

In the code: src/NzbDrone.Core/Tv/Search/SeriesSearchService.cs, src/NzbDrone.Core/Tv/Search/EpisodeSearchService.cs, src/NzbDrone.Core/Tv/Search/MissingEpisodeSearchCommand.cs, frontend/src/ProactiveSearch/ProactiveSearch.tsx.
