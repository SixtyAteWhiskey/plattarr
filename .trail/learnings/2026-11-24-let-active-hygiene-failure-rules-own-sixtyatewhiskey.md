---
title: Let active Hygiene failure rules own the native failure handoff
author: SixtyAteWhiskey
date: 2026-11-24
branch: codex/final-release-20261123
id: l-71f3aad5
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/Download/DownloadProcessingService.cs
  - src/NzbDrone.Core.Test/Download/DownloadProcessingServiceFixture.cs
  - src/NzbDrone.Core/Hygiene/HygieneRuleSafeguards.cs
  - tools/liveproof/hygiene_recovery.py
---

## Decided
When the global Hygiene action gate and a matching client-failed rule are enabled, ordinary failure processing waits for Hygiene's strike threshold and safeguards. Keep native failure handling unchanged for monitoring-only, absent, disabled, or out-of-scope rules. A reported download-network outage holds native failure processing regardless of the Hygiene gate.

## Tried and ruled out
The real disposable proof initially produced a replacement but no Hygiene journal: native failure handling had removed the failed job before the supported three-strike threshold. One-strike fixtures and mocked action-service tests hid the race. Disabling native failure handling in the test would hide a production interaction, so the handoff was fixed instead.

## Watch out
An ignored or held job must not be removed by a second path while its active Hygiene rule owns it. Use the same media/client/privacy rule matching, including disabled-specific-rule precedence. Monitoring alone must not take over ordinary failure handling. Verify the journal, source retention, and exactly one replacement, not just that a new download appeared.

In the code: DownloadProcessingService.ShouldDeferFailure, HygieneRuleSafeguards.MatchForItem, DownloadProcessingServiceFixture, and tools/liveproof/hygiene_recovery.py.
