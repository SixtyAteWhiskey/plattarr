---
title: "Use Prowlarr's feed as the source for indexer definitions"
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-89fece95
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core/IndexerVersions/IndexerDefinitionUpdateService.cs
  - src/NzbDrone.Core.Test/Files/Indexers/FunFile/funfile-candidate.yml
---

## Decided
Keep Prowlarr's definition feed as the source for FunFile because Plattarr already downloads and resolves indexers from that feed. Correct the upstream definition and supporting parser/schema only after authenticated behavior is proven. The local candidate remains an offline parity fixture.

## Tried and ruled out
Bundling the candidate definition into Plattarr was ruled out because it would add a second maintained copy of an indexer already delivered by Prowlarr and could drift with feed updates. The attempted code was reverted.

## Watch out
A YAML-only upstream change cannot preserve native release identity because both active schemas reject an explicit GUID field and Prowlarr's parser still takes release GUID from the download URL. The feed currently serves the pinned v11 FunFile bytes through its version 12 endpoint.

In the code: src/NzbDrone.Core/IndexerVersions/IndexerDefinitionUpdateService.cs downloads the package and resolves definitions; src/NzbDrone.Core.Test/Files/Indexers/FunFile/funfile-candidate.yml holds the offline candidate; docs/refactoring-plan.md records the parity and delivery gates. The live endpoint checked was https://indexers.prowlarr.com/master/12/funfile with SHA-256 96d57562b70fe227e9f9d69d288408619270951a80a706723b40c8363945898f.
