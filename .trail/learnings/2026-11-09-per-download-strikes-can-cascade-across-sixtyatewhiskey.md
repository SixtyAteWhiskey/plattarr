---
title: Per-download strikes can cascade across one missing movie
author: SixtyAteWhiskey
date: 2026-11-09
branch: codex/hygiene-missing-search-retry-guard
id: l-3411539b
scope: repo
status: active
src: agent
touches:
  - docs/hygiene.md
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueActionServiceFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueRefreshBenchmark.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - src/NzbDrone.Core/Movies/MoviesModule.cs
  - src/NzbDrone.Core/Movies/Search/MoviesSearchService.cs
---

## Decided
Cap automatic replacement searches by recent distinct failed downloads for the movie, and pace proactive missing-Movie searches with a failure cooldown. A production journal showed 30 distinct torrents being removed and followed by another search over about three hours; the last search grabbed nothing and the monitored movie remained without a file.

## Tried and ruled out
Raising only the per-download strike threshold would slow each attempt but would not bound the number of replacement attempts, because each new download has a fresh strike state. Disabling all replacement searches would also suppress the useful first recovery attempts.
Reading Movie history directly inside Hygiene failed the core/module boundary test; the retry count belongs behind the shared download-history service.

## Watch out
An action marked completed and a search reporting a grab do not prove an import. Keep cleanup and blocklisting active when the movie-level replacement limit is reached, and preserve manual search for an owner review. Distinct download IDs matter; same-hash repeats have a separate guard.

In the code: HygieneQueueActionService.Continue calls MarkAsFailed and decides skipRedownload; DownloadHistoryRepository.CountRecentFailedDownloads scopes the retry count; MovieSearchService executes MissingMoviesSearchCommand; MoviesModule registers scheduled tasks; MovieSearchSafety counts recent failures for scheduled search.
