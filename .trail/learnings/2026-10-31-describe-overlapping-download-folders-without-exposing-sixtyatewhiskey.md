---
title: Describe overlapping download folders without exposing configured paths
author: SixtyAteWhiskey
date: 2026-10-31
branch: codex/queue-health-error-audit-20261031
id: l-631fc6e3
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Api.Test/v3/Health/HealthResourceFixture.cs
  - src/NzbDrone.Core.Test/Localization/LocalizationServiceFixture.cs
  - src/NzbDrone.Core/HealthCheck/Checks/DownloadClientRootFolderCheck.cs
  - src/NzbDrone.Core/Localization/Core/en.json
---

## Decided
When System Health detects that a download output folder equals or sits inside a media root, use guidance with no client name or path arguments. Keep the existing warning reason and wiki link so the same configuration problem is still reported.

## Tried and ruled out
Reusing the older translated template with empty or substituted values was ruled out because translations interpolate the configured client name and root path. A new key with no arguments makes the fallback English text safe for untranslated locales.

## Watch out
The check detects folder overlap, not the state of existing downloads or the cause of an import failure. The guidance should direct the owner to compare configured folders and check before retrying imports; it must not imply that changing a path moves existing files.

In the code: `DownloadClientRootFolderCheck.Check` uses `DownloadClientRootFolderOverlapHealthCheckMessage`; the older `DownloadClientRootFolderHealthCheckMessage` remains in locale files. `HealthResourceFixture` proves the serialized message excludes the disposable client name and paths, while `LocalizationServiceFixture` proves fallback wording.
