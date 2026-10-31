---
title: Describe read-only mount observations without exposing paths
author: SixtyAteWhiskey
date: 2026-10-31
branch: codex/system-health-diagnostics
id: l-5fdf52ae
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core/HealthCheck/Checks/MountCheck.cs
  - src/NzbDrone.Core/Localization/Core/en.json
  - src/Radarr.Api.V3/Health/HealthResource.cs
---

## Decided
System Health should say that a movie path's mount reports read-only, then suggest checking mount settings and filesystem health as possibilities. The check reads mount options; it does not attempt a write or establish why the mount is read-only. The shared API message should omit mount names and movie paths, even though those details were previously appended.

## Tried and ruled out
Keeping the existing message with mount names and movie paths would disclose private storage layout through the health API. Wording the alert as a proven write failure would claim an operation this check never performs.

## Watch out
A read-only mount is an observed state, not proof of a particular root cause. Keep the error result and wiki target, and use disposable mount fixtures to verify both read-only and writable cases plus API presentation.

In the code: `MountCheck.Check` in `src/NzbDrone.Core/HealthCheck/Checks/MountCheck.cs` reads `IDiskProvider.GetMount(...).MountOptions.IsReadOnly`; `HealthResourceMapper.ToResource` in `src/Radarr.Api.V3/Health/HealthResource.cs` passes `Message` through. The message key is `MountMovieReadOnlyHealthCheckMessage`.
