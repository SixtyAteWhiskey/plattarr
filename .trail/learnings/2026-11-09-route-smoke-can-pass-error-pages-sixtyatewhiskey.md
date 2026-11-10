---
title: Route smoke can pass error pages under rapid navigation
author: SixtyAteWhiskey
date: 2026-11-09
branch: codex/proactive-search-foundation
id: l-7af347c7
scope: repo
status: active
src: agent
touches:
  - docs/proactive-search.md
  - frontend/src/App/AppRoutes.tsx
  - frontend/src/Components/Page/Sidebar/PageSidebar.tsx
  - frontend/src/ProactiveSearch/ProactiveSearch.css
  - frontend/src/ProactiveSearch/ProactiveSearch.css.d.ts
  - frontend/src/ProactiveSearch/ProactiveSearch.tsx
  - src/NzbDrone.Api.Test/v3/ProactiveSearch/ProactiveSearchPreviewControllerFixture.cs
  - src/NzbDrone.Core.Test/MovieTests/SearchTests/MovieProactiveEligibilityFixture.cs
  - src/NzbDrone.Core/Movies/Search/MovieProactiveEligibility.cs
  - src/NzbDrone.Core/Movies/Search/MoviesSearchService.cs
---

## Decided
Validate a new route in isolation at desktop and narrow widths when the full route sweep starts producing translation API errors. Inspect the page title and visible content as well as the script's pass flag.

## Tried and ruled out
The full sweep of 66 route/viewport visits reported success even when many pages displayed "Failed to load translations from API". An earlier sweep also accepted a JSON 404 as a valid page. Its current error check only looks for a specific error page, loading text, JavaScript exceptions, empty text, and overflow, so a nonempty API error can pass. Running the new route alone rendered correctly at both widths without exceptions or overflow.

## Watch out
The disposable app needs the Linux Console and Mono assemblies, plus built UI files in the host output directory, before the smoke script can show real pages. Building only the API or Console leaves missing assets or assemblies and can mislead route checks.

In the code: tools/check_frontend_routes.py, src/NzbDrone.Console/Radarr.Console.csproj, src/NzbDrone.Mono/Radarr.Mono.csproj, frontend/build/webpack.config.js.
