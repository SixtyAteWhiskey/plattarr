---
title: Follow Prowlarr definitions as the source of truth
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-86a1fed2
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
---

## Decided
Keep indexer definitions aligned with Prowlarr's definitions. The owner chose close compatibility because Plattarr models its indexer functionality on Prowlarr.

## Tried and ruled out
Maintaining a separate Plattarr-specific corrected definition was rejected because it can drift from the upstream feed.

## Watch out
Do not remove a working native provider until the upstream definition and conversion paths have been verified. Upstream limitations may require keeping the existing implementation while compatibility is resolved.

In the code: docs/refactoring-plan.md records the FunFile parity and delivery gates.
