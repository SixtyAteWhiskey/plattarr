---
title: Trust only the configured local indexer origin for release downloads
author: SixtyAteWhiskey
date: 2026-11-24
branch: codex/final-release-20261123
id: l-64da4bf4
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Common/Http/PublicDestinationPolicy.cs
  - src/NzbDrone.Common/Http/HttpRequest.cs
  - src/NzbDrone.Common/Http/Dispatchers/ManagedHttpDispatcher.cs
  - src/NzbDrone.Core/Indexers/HttpIndexerBase.cs
  - src/NzbDrone.Core/Indexers/IndexerHttpClient.cs
  - src/NzbDrone.Core/Download/DownloadClientBase.cs
  - src/NzbDrone.Core/Download/UsenetClientBase.cs
  - src/NzbDrone.Core/Download/TorrentClientBase.cs
  - tools/liveproof/hygiene_recovery.py
---

## Decided
Keep release-download protection against private destinations while allowing the exact origin an administrator configured for an indexer. Carry that origin separately from the feed-supplied URL, and recalculate the permission for each redirect and outbound connection.

## Tried and ruled out
The disposable recovery proof initially failed at the release grab, before Hygiene could run: blanket public-address enforcement rejected its explicitly configured loopback Newznab indexer. Removing destination validation would reopen access to unrelated private services. Routing the proof through a special proxy would hide a real local-indexer regression instead of fixing it.

## Watch out
Compare scheme, hostname, and effective port, not string prefixes. A redirect to a different private port or host must still fail before sending. The direct downloader path and the indexer fetch path both need the configured origin; the live proof exercises both. The pre-proxy literal-address check must honor the same origin boundary as the dispatcher; both gates are exercised by the live proof. Keep the real three-strike interval in the proof rather than mutating the clock or stored strike state.

In the code: PublicDestinationPolicy.IsConfiguredOrigin, HttpRequest.ConfiguredOrigin, ManagedHttpDispatcher.GetResponseAsync, HttpIndexerBase.Download, and DownloadClientBase.GetConfiguredIndexerOrigin. tools/liveproof/hygiene_recovery.py uses local fixtures and the supported three-strike minimum.
