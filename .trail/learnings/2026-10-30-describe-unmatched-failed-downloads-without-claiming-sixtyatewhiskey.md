---
title: Describe unmatched failed downloads without claiming who grabbed them
author: SixtyAteWhiskey
date: 2026-10-30
branch: codex/actionable-import-errors
id: l-2cb8200e
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Api.Test/v3/Queue/QueueResourceFixture.cs
  - src/NzbDrone.Core.Test/Download/FailedDownloadServiceTests/ProcessFixture.cs
  - src/NzbDrone.Core/Download/FailedDownloadService.cs
---

## Decided
When the client reports a failed or encrypted download but Plattarr cannot associate it with a grab, show the observed client state and say automatic failure handling was skipped. Suggest checking the client item, category, and Activity history before manual action.

## Tried and ruled out
Saying the download was not grabbed by Plattarr was ruled out because the lookup only establishes that the current media type has no matching grab association. The item might have another category, an outside origin, or missing history. Repeating client error details in the Queue warning was ruled out because they can contain private paths, URLs, or credentials.

## Watch out
An unknown media type or missing failed-download source also reaches this warning without querying a media history source. Keep the wording at "cannot link" and do not turn the warning into an automatic removal or redownload.

In the code: `FailedDownloadService.Check` calls `WasGrabbed` for failed or encrypted client items and writes `TrackedDownload.StatusMessages`; `QueueResourceMapper.ToResource` copies those messages into the Queue API. The disposable `ProcessFixture` and `QueueResourceFixture` cover both behavior and API serialization.
