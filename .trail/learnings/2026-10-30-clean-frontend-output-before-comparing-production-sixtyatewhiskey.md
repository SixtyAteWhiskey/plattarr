---
title: Clean frontend output before comparing production builds
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-caf2ec6b
scope: repo
status: active
src: agent
touches:
  - tools/benchmark_frontend_routes.py
  - frontend/build/webpack.config.js
---

## Decided
Move aside the existing frontend output before a production webpack comparison, then restore the clean production output after any baseline swap. This keeps total file-size measurements tied to one build mode.

## Tried and ruled out
Running the production webpack command over a preceding development build was ruled out because webpack replaced the production assets but left unrelated development files behind. The served production assets matched after a clean rebuild, but counting the mixed directory overstated total output.

## Watch out
A browser route pass can still appear correct while stale files remain in the output directory. Compare asset hashes and file counts, and use the clean directory for total-output claims.

In the code: `corepack yarn webpack --config ./frontend/build/webpack.config.js --env production --json <stats-file>` writes `_output/UI`; the mixed directory had 131 files and 69,938,867 bytes, while a clean production build had 98 files and 22,955,736 bytes.
