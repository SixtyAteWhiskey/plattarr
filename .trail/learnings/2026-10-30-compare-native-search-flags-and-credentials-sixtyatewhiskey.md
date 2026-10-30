---
title: Compare native search flags and credentials before retiring an indexer
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-7b2a606e
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core/Indexers/Definitions/FunFile.cs
  - src/NzbDrone.Core/Indexers/Definitions/CGPeers.cs
---

## Decided
Use FunFile as the first candidate for a native-to-Cardigann retirement proof because both implementations accept username and password, but keep its native implementation until converted stored rows, imports, and search results are verified. Preserve native request behavior in a focused test before changing providers.

## Tried and ruled out
Retiring a provider just because its Cardigann definition has the same ID and credential field names. FunFile's native IMDb request always enables title search and description search; the audited Cardigann definition leaves title search empty for IMDb lookup. CGPeers is a worse first candidate because its native username/password and freeleech-token settings do not map directly to the Cardigann definition's cookie login.

## Watch out
A static request difference may or may not change live results. Do not infer search parity without authenticated search and result checks. Startup deletes stored rows whose implementation is missing, so conversion must happen before deleting the native type.

In the code: `src/NzbDrone.Core/Indexers/Definitions/FunFile.cs` sends `s_title=1` and, for IMDb, `s_desc=1`; `src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileRequestGeneratorFixture.cs` locks those requests; `src/NzbDrone.Core/Indexers/Definitions/CGPeers.cs` uses `GazelleSettings`; `docs/refactoring-plan.md` tracks the open retirement work. Audited definitions: `https://github.com/Prowlarr/Indexers/blob/5f5db4e58d06be18619c3a858b9a91ade6f341ca/definitions/v11/funfile.yml` and `https://github.com/Prowlarr/Indexers/blob/5f5db4e58d06be18619c3a858b9a91ade6f341ca/definitions/v11/cgpeers.yml`.
