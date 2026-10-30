---
title: Compare actual form submission and release identity before indexer retirement
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-8ac5a43f
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannRequestGenerator.cs
  - src/NzbDrone.Core/Indexers/Definitions/FunFile.cs
  - docs/refactoring-plan.md
---

## Decided
Keep FunFile's native provider in place because the pinned Cardigann definition is not behaviorally equivalent yet. Use the same synthetic HTML to compare generated requests and parsed releases, and exercise Cardigann's form login with a disposable HTTP mock before deciding whether to switch implementations.

## Tried and ruled out
Treating the definition's `login.php` path as proof that its login POST differs from native `takelogin.php` is wrong because Cardigann first fetches the page and then submits to the form action; a synthetic form action of `takelogin.php` makes the mocked POST reach that endpoint. Matching title and download fields is insufficient because Cardigann makes the download URL the release GUID while native FunFile uses the details URL.

## Watch out
The mocked form action does not prove the live site's form or authenticated results because the HTML is synthetic. IMDb search still omits native `s_title=1`, and the GUID difference can alter release identity or deduplication. Keep saved rows and imports native until those gaps are resolved and authenticated behavior is checked.

In the code: `src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileCardigannParityFixture.cs` compares against `src/NzbDrone.Core.Test/Files/Indexers/FunFile/funfile-5f5db4e.yml`; `src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannRequestGenerator.cs` reads `login.php` and submits to the form action; `src/NzbDrone.Core/Indexers/Definitions/FunFile.cs` posts to `takelogin.php` and uses the details URL as GUID; `docs/refactoring-plan.md` tracks the remaining gate.
