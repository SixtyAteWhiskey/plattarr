---
title: Keep deferred Settings loading after slow connection measurement
author: SixtyAteWhiskey
date: 2026-10-29
branch: refactor/settings-route-loading
id: l-00199b52
scope: repo
status: active
src: agent
touches:
  - frontend/src/App/AppRoutes.tsx
---

## Decided
Keep Settings routes loaded on demand because a disposable Chrome comparison against the original production bundle showed about 104 KB less initial browser transfer and a 541 ms earlier median Movies render on a simulated 1.6 Mbit/s connection. The first Settings visit was about 180 ms slower there, while a repeat Settings visit stayed near 21 ms; that is an acceptable tradeoff for this slice.

## Tried and ruled out
Treating the earlier single cold browser run as proof of faster parsing was ruled out because Chrome script-duration ranges overlap and the metric does not isolate parsing. The slower profile exposed the first Settings navigation cost that the unthrottled run hid.

## Watch out
The browser benchmark uses a disposable empty fixture and a visible Movies-content marker, so it cannot prove production data render speed or exact paint timing. Preserve the first-versus-repeat Settings distinction when evaluating another route-loading change.

In the code: `frontend/src/App/AppRoutes.tsx`, `tools/benchmark_frontend_routes.py`, `docs/refactoring-plan.md`. Run `python3 tools/benchmark_frontend_routes.py --profile fast --samples 5` and `python3 tools/benchmark_frontend_routes.py --profile slow --samples 3` after a production UI and Release backend build.
