---
title: Keep warning cleanup separate from existing monitoring
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-2626a43c
scope: repo
status: active
src: agent
touches:
  - docs/hygiene.md
  - frontend/src/Activity/Hygiene/Hygiene.tsx
  - frontend/src/Activity/Hygiene/HygieneRuleSummary.tsx
  - frontend/src/Activity/Hygiene/HygieneRules.tsx
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueActionServiceFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneQueueDecisionServiceFixture.cs
  - src/NzbDrone.Core.Test/QueueTests/HygieneRuleServiceFixture.cs
  - src/NzbDrone.Core/Hygiene/HygieneActionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneQueueDecisionService.cs
  - src/NzbDrone.Core/Hygiene/HygieneRule.cs
---

## Decided
Make automatic cleanup of download warnings a separate per-rule opt-in that defaults off because previously enabled warning rules were monitor-only, and existing strike counts can already exceed the action threshold when the application upgrades.

## Tried and ruled out
Making every enabled warning rule immediately action-capable would silently turn existing monitoring into client removal and possibly source-file deletion or replacement searches at the next refresh. Preserving monitoring while requiring a new explicit opt-in avoids that upgrade surprise.

## Watch out
An opt-in rule can act on strikes already accumulated while monitoring, so its editor must explain that actions can occur on the next refresh. Keep the global gate, current assessment, tracked-client checks and private or unverified torrent protection. When the opt-in is off, preserve existing rule settings and monitoring.

In the code: migration 021 sets `WarningCleanupEnabled` false, and `HygieneQueueDecisionService` uses it before allowing `download-warning` actions. `HygieneRules.tsx` exposes the separate switch.
