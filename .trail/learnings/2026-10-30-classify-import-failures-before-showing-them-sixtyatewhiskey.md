---
title: Classify import failures before showing them in the queue
author: SixtyAteWhiskey
date: 2026-10-30
branch: codex/actionable-import-errors
id: l-6486f004
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/MediaFiles/MovieImport/ImportApprovedMoviesFixture.cs
  - src/NzbDrone.Core.Test/MusicTests/MediaFiles/ImportApprovedTracksFixture.cs
  - src/NzbDrone.Core.Test/TvTests/MediaFiles/EpisodeImport/ImportApprovedEpisodesFixture.cs
  - src/NzbDrone.Core/Movies/MediaFiles/MovieImport/ImportApprovedMovie.cs
  - src/NzbDrone.Core/Music/MediaFiles/TrackImport/ImportApprovedTracks.cs
  - src/NzbDrone.Core/Tv/MediaFiles/EpisodeImport/ImportApprovedEpisodes.cs
---

## Decided
Use exception types to give specific, safe next steps for failed movie, TV, and music imports. File access, missing file or folder, general I/O, and unexpected failures have different advice because the exception type supports those facts without requiring its raw message.

## Tried and ruled out
Appending the exception message to the queue result was ruled out: filesystem exceptions include private absolute paths, and other exception messages can contain URLs or credentials. The queue needs an actionable explanation without copying those values.

## Watch out
Keep full exceptions in local logs for diagnosis, but treat import result errors as user-facing and potentially shareable. Do not infer an exact cause such as a full disk from a generic IOException. If adding another import path, apply the same boundary before returning errors.

In the code: `ImportFailureMessage.For` in `src/NzbDrone.Core/MediaFiles/ImportFailureMessage.cs` serves the three `ImportApproved*` paths; focused fixtures cover path-bearing exceptions and unexpected messages.
