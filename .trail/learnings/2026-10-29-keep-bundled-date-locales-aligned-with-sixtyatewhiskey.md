---
title: Keep bundled date locales aligned with selectable UI languages
author: SixtyAteWhiskey
date: 2026-10-29
branch: refactor/settings-route-loading
id: l-ef498e8c
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - frontend/build/webpack.config.js
  - src/NzbDrone.Core/Parser/IsoLanguages.cs
  - frontend/src/Language/useLanguageName.ts
---

## Decided
Limit Moment's bundled locale context to the locale files reachable from the API's UI language identifiers because the full context adds 139 locale files to the initial vendor chunk, while only 57 can serve the UI. English is built in and region codes can use their base locale.

## Tried and ruled out
Using translation JSON files as the whitelist was ruled out because the API accepts UI languages that do not have translations yet. Removing Live Reload from the production webpack configuration was ruled out because its server and injected script are active only during watch builds, so production transfer would not improve.

## Watch out
Webpack's Moment context requests are shaped like `./fr` or `./fr.js`; a regex anchored to bare `fr` matches no locales. Compare the filter against the API mapping when languages are added, because an omitted locale silently falls back to English dates. Check actual bundled modules after a build.

In the code: `frontend/build/webpack.config.js`, `src/NzbDrone.Core/Parser/IsoLanguages.cs`, `frontend/src/Language/useLanguageName.ts`. Verify with `corepack yarn webpack --config ./frontend/build/webpack.config.js --env production --json <stats-file>` and inspect the Moment locale modules in the stats.
