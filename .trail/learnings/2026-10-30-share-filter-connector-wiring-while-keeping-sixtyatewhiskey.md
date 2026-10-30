---
title: Share filter connector wiring while keeping media choices explicit
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-d17ad732
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - frontend/src/Components/Filter/createConnectedFilterModal.tsx
  - frontend/src/Movie/Index/MovieIndexFilterModal.tsx
  - frontend/src/Series/Index/SeriesIndexFilterModal.tsx
  - frontend/src/Artist/Index/ArtistIndexFilterModal.tsx
---

## Decided
Use one connector for the repeated library index filter wiring, with each media module supplying its own state selectors, action creator, and custom-filter type. This removes the repeated selectors and dispatch callback without moving Movie, TV, or Music rules into a shared abstraction.

## Tried and ruled out
A broad manual-import extraction was ruled out for this slice. Similar-looking rows contain different media selection and validation behavior, so a common component would need separate behavior proof before it can safely replace them.

## Watch out
The library filter buttons are disabled on an empty fixture. A route render can verify the connector mounts, but it cannot establish filter selection or custom-filter editing on populated libraries. Keep that acceptance open until a populated fixture exercises all three kinds.

In the code: `frontend/src/Components/Filter/createConnectedFilterModal.tsx`; `frontend/src/Movie/Index/MovieIndexFilterModal.tsx`; `frontend/src/Series/Index/SeriesIndexFilterModal.tsx`; `frontend/src/Artist/Index/ArtistIndexFilterModal.tsx`; `docs/refactoring-plan.md`. The custom-filter types are `movieIndex`, `series`, and `artist`.
