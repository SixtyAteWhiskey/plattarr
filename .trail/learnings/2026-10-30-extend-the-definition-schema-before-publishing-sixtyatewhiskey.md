---
title: Extend the definition schema before publishing an explicit release identity
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-38f63509
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core.Test/Files/Indexers/FunFile/funfile-candidate.yml
  - src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileCardigannParityFixture.cs
  - src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannParser.cs
---

## Decided
Keep the corrected FunFile definition as a test candidate until its release identity can be delivered through the supported definition feed. The parser accepts an explicit GUID, but the current upstream schema rejects that field, so a locally passing parity test is not enough to ship the YAML.

## Tried and ruled out
Using the current upstream v11 definition unchanged: it omits the native IMDb title-search flag and uses the download URL as the release GUID. Publishing the candidate to that feed immediately is also ruled out because the current schema's search-field names exclude `guid`.

## Watch out
The public login form currently posts to the expected endpoint and loads a reCAPTCHA script, but this does not establish authenticated login or search behavior. Keep saved-row and importer conversion inactive until a disposable authenticated check and a valid definition delivery path are proved.

In the code: `src/NzbDrone.Core.Test/Files/Indexers/FunFile/funfile-candidate.yml`, `src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileCardigannParityFixture.cs`, `src/NzbDrone.Core/Indexers/Definitions/Cardigann/CardigannParser.cs`, and `docs/refactoring-plan.md`. Upstream schema: `https://github.com/Prowlarr/Indexers/blob/master/definitions/v11/schema.json`.
