---
title: Keep provider conversion inactive until login and search behavior match
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-05944941
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core.Test/IndexerTests/IndexerServiceFixture.cs
  - src/NzbDrone.Core.Test/Upstream/IndexersStageFixture.cs
---

## Decided
Prepare one settings conversion rule for both saved FunFile rows and imported FunFile rows, but leave both runtime paths using the native provider until the Cardigann replacement's behavior is proved. A stored-row round trip and a disposable import fixture establish the data shape without exposing configured users to the replacement.

## Tried and ruled out
Switching imports or migrating saved rows immediately after the credentials mapped. Matching username and password is insufficient: the replacement definition posts through a different login path and sends a different IMDb title-search flag. Those differences could change authentication or results.

## Watch out
A successful settings conversion is not a provider retirement gate. Compare authenticated login, search requests, and parsed results first; then wire conversion into both startup migration and upstream import and verify each path before deleting the native class.

In the code: `src/NzbDrone.Core/Indexers/Definitions/FunFileSettingsConverter.cs` moves credentials into `extraFieldData` and sets `definitionFile=funfile`; `src/NzbDrone.Core.Test/IndexerTests/IndexerServiceFixture.cs` checks a stored row; `src/NzbDrone.Core.Test/Upstream/IndexersStageFixture.cs` checks the imported source shape; `docs/refactoring-plan.md` retains the open runtime and parity gate. The audited definition is `https://github.com/Prowlarr/Indexers/blob/5f5db4e58d06be18619c3a858b9a91ade6f341ca/definitions/v11/funfile.yml`.
