---
title: Finish the current feed-backed replacement before scouting another provider
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-d5d2a029
scope: repo
status: active
src: agent
touches:
  - docs/refactoring-plan.md
  - src/NzbDrone.Core/IndexerVersions/IndexerDefinitionUpdateService.cs
  - src/NzbDrone.Core.Test/Files/Indexers/FunFile/funfile-candidate.yml
---

## Decided
Keep the refactoring plan focused on completing FunFile through Prowlarr's definition feed. When its feed and authenticated parity gates are open, report those gates and stop this slice instead of starting a different native-indexer replacement.

## Tried and ruled out
Screening HD-Torrents as another retirement candidate was ruled out in this session. It did not advance the next FunFile step, and the temporary regression test was removed after the user corrected the scope.

## Watch out
A definition already appearing in Prowlarr's feed does not prove that removing a native provider preserves saved rows, imports, search behavior, or release identity. The current FunFile candidate still needs upstream schema/parser/definition delivery and authenticated site evidence.

In the code: docs/refactoring-plan.md section 4 tracks the FunFile gate; src/NzbDrone.Core/IndexerVersions/IndexerDefinitionUpdateService.cs uses the Prowlarr master version 12 feed; src/NzbDrone.Core.Test/Files/Indexers/FunFile/funfile-candidate.yml is an offline parity fixture. The temporary HDTorrentsRequestGeneratorFixture.cs was deleted before commit.
