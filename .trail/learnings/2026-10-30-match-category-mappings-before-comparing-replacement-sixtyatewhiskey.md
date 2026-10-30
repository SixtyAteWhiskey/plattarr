---
title: Match category mappings before comparing replacement requests
author: SixtyAteWhiskey
date: 2026-10-30
branch: refactor/settings-route-loading
id: l-70590769
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Core.Test/IndexerTests/FunFileTests/FunFileCardigannParityFixture.cs
---

## Decided
Use the native provider's complete category map when comparing its generated search requests with a definition-based replacement. Parent categories can expand to several tracker IDs, so a partial test map gives a false request mismatch.

## Tried and ruled out
A parity fixture mapped only the directly named TV category. Its TV search appeared to differ because the replacement also selected the anime child category; the native provider selects that child category when its full map is present.

## Watch out
Keep request parity checks tied to the provider's actual capabilities, and compare the query dictionaries after both sides map the same input categories.

In the code: `FunFile.SetCapabilities` maps tracker categories 7 and 44; `FunFileCardigannParityFixture` exercises TV term and IMDb searches alongside movie, music, book, and basic searches.
