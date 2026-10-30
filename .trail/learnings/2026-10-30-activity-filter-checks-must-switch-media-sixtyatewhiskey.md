---
title: Activity filter checks must switch media kinds
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-e47a18da
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - frontend/src/Components/MediaKindSwitcher.tsx
  - frontend/src/App/AppRoutes.tsx
  - frontend/src/Components/Menu/FilterMenu.tsx
  - tools/check_frontend_routes.py
---

## Decided
When checking shared Activity filter wiring, switch the media kind and open each Custom Filter modal for every History and Blocklist view affected by the change. The shared routes initially show Movies, so a route-only pass cannot establish TV or Music coverage. Exercise the API-error state for each view as well.

## Tried and ruled out
The existing route and viewport check alone was ruled out as sufficient evidence: it opens History and Blocklist with the default media kind and never opens their filter modals.

## Watch out
Music Blocklist has no custom-filter connector, while Music History does. Count the affected views from the actual connector usages rather than assuming a full three-by-two matrix.

In the code: `frontend/src/Components/MediaKindSwitcher.tsx`, `frontend/src/App/AppRoutes.tsx`, `frontend/src/Components/Menu/FilterMenu.tsx`, and `tools/check_frontend_routes.py`. The focused disposable Chrome pass exercised five views at 1440 and 480 pixels and blocked `/api/v3/history*` or `/api/v3/blocklist*` to check load-error alerts.
