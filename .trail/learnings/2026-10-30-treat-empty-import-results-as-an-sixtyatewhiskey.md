---
title: Treat empty import results as an ambiguous failure
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-2c2cbc51
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/Download/CompletedDownloadServiceTests/ImportFixture.cs
  - src/NzbDrone.Core/Download/CompletedDownloadService.cs
---

## Decided
Explain that no files were imported and suggest checking the download location and file eligibility because an empty importer result can mean either an inaccessible or missing path or an accessible location with no imported files.

## Tried and ruled out
Treating an empty result as proof that files were ineligible was ruled out by a read-only production observation: the relevant logged path was missing inside the container while its parent was accessible, and the corresponding importer returned no results.

## Watch out
Do not infer that permissions are the cause solely because an existence check fails. Preserve the existing import-pending warning state: turning this ambiguous result into import-blocked could make an enabled Hygiene rule perform destructive cleanup. Avoid embedding private paths in the new message.

In the code: `DownloadedEpisodesImportService.ProcessPath` returns an empty list on an inaccessible path, and `CompletedDownloadService.Import` handles empty results for all media kinds.
