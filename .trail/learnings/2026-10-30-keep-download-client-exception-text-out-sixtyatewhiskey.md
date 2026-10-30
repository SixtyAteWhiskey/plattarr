---
title: Keep download client exception text out of System Health
author: SixtyAteWhiskey
date: 2026-10-30
branch: codex/actionable-import-errors
id: l-713b3484
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/HealthCheck/Checks/DownloadClientCheckFixture.cs
  - src/NzbDrone.Core/HealthCheck/Checks/DownloadClientCheck.cs
  - src/NzbDrone.Core/Localization/Core/en.json
---

## Decided
When reading a download client's items throws, System Health should report that the read failed and suggest checking the client, its connection settings, and local logs. Keep the detailed exception only in local logging. Use a new localization key with no private-value tokens so existing translated templates cannot echo raw exception text.

## Tried and ruled out
Keeping the old localized template with a sanitized exception argument was ruled out because its placeholder structure still encourages passing raw details later. Calling the client offline was ruled out because a failure while reading items can also come from settings, authentication, or an invalid response.

## Watch out
The health error covers any exception from the client's item read. Do not treat the suggested causes as diagnosed facts. The Health API copies the health message directly, so values passed into that message are shared diagnostics.

In the code: `DownloadClientCheck.Check` catches `GetItems` exceptions, logs the full exception locally, and now uses `DownloadClientCheckUnableToReadItemsMessage` from `Localization/Core/en.json`; `HealthResourceMapper.ToResource` copies `Message` to the API. `DownloadClientCheckFixture` uses a disposable private-looking URL and client name to verify they stay out of the health message.
