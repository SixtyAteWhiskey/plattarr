---
title: Explain missing import locations without blaming the client
author: SixtyAteWhiskey
date: 2026-10-30
branch: codex/actionable-import-errors
id: l-8e81d71d
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/Download/CompletedDownloadServiceTests/ImportFixture.cs
  - src/NzbDrone.Core.Test/Download/CompletedDownloadServiceTests/ProcessFixture.cs
  - src/NzbDrone.Core/Download/CompletedDownloadService.cs
---

## Decided
When a completed download has no usable import location, say what Plattarr observed and give checks for the client's completed-download location and path mappings. When the location has the wrong operating-system path format, say it cannot be used here without repeating the value.

## Tried and ruled out
Calling the download client the cause was ruled out because the import item can be transformed by the client's `GetImportItem` before validation. Echoing the rejected path in the queue warning was ruled out because queue messages can be shared and the path can identify private storage or media.

## Watch out
The path-format check does not prove that a file exists, that a mount is healthy, or that a mapping is missing. Keep these as checks for the user, not asserted causes. Validate both the readiness and import entry points, since each invokes path validation.

In the code: `CompletedDownloadService.ValidatePath` receives `ImportItem.OutputPath` after `ProvideImportItemService.ProvideImportItem`; `CompletedDownloadServiceTests/ProcessFixture.cs` and `ImportFixture.cs` cover the warnings.
