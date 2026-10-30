# Pruned providers

Plattarr is meant to feel like the *arr apps merged into one lightweight app, so
code for providers the owner does not run was removed in the "Prune providers"
commit (the commit that adds this file). Nothing here is lost: it is all in git
history, and the code was removed whole (folders), so a restore is a checkout of
those folders plus the fixes the compiler asks for.

The rule used: keep what the owner's running stack uses, plus the smallest set
that keeps each framework real (a framework with no implementation is dead code
that no test exercises).

## Kept

| Area | Kept | Why |
|---|---|---|
| Download clients | qBittorrent, SABnzbd | qBittorrent is in use. SABnzbd keeps the Usenet client base, the NZB path and the single-category filtering rule (9i) exercised. |
| Notifications | Webhook, Custom Script, Discord, MediaBrowser (Emby and Jellyfin) | The first three have golden tests that pin their wire format. MediaBrowser is the media server family in use. |
| Metadata consumers | Xbmc (Kodi NFO, also read by Jellyfin and Emby) | The one format every media server reads. |
| Import lists | TMDb, RSS import, import exclusions | Keeps the import list framework tested. Both need no third-party account. |
| Indexers | Everything (Prowlarr's layer, Cardigann, Newznab, Torznab, and native definitions) | Not pruned yet. A 2026-10-25 owner snapshot is inventoried in [the refactoring plan](refactoring-plan.md#2026-10-29-indexer-pruning-inventory-first-pass); the current configured list is still needed before removing a provider. |

## Removed

- Download clients (15): Aria2, Blackhole (torrent and Usenet, with `ScanWatchFolder`), Deluge,
  Download Station (torrent and Usenet), Flood, Freebox Download, Hadouken, NZBGet, NzbVortex,
  Pneumatic, rTorrent, RQBit, Transmission, uTorrent, Vuze.
- Notifications (23): Apprise, Email, Gotify, Join, Mailgun, Notifiarr, Ntfy, Plex, Prowl,
  PushBullet, Pushcut, Pushover, Pushsafer, SendGrid, Signal, Simplepush, Slack, Synology
  Indexer, Telegram, Trakt, Twitter, Xbmc (Kodi notify). Also the Trakt collection builder hook
  and the Slack URL health check.
- Metadata consumers (4): Kometa, MediaBrowser (legacy), Roksbox, WDTV, and the Kometa
  deprecation health check.
- Import lists (8 plus Plex RSS): Trakt, Simkl, Plex, CouchPotato, StevenLu, Radarr (another
  instance), Radarr lists v1 and v2. The Discover page now draws on TMDb popular only.

## Restoring one

1. `git log --diff-filter=A -- docs/pruned-providers.md` gives the prune commit; use its parent
   as the source.
2. `git checkout <parent> -- src/NzbDrone.Core/Download/Clients/<Name> src/NzbDrone.Core.Test/Download/DownloadClientTests/<Name>Tests`
   (notifications and import lists work the same way).
3. Build the solution and fix what the compiler reports. Download clients need the
   `ModuleDownloadClientSettings<T>` pattern from 9i (category entries per media type), which is
   already how the code in the parent is written.
4. Remember `ModuleBoundaryFixture`: a restored type that reaches into module code needs the
   same generalization the others got; never add an allowlist entry.
5. For a download client, also add its row to `DownloadClientSettingsConverter` (the importer's
   copy of migration 003's key map; only the clients that exist are listed) and a case to
   `UpstreamClientSettingsMigrationFixture`, so a Radarr client of that kind imports with the
   same settings migration 003 gave an in-place client.

## Things that still know the removed names

- Migration 003 (`module_download_client_categories`) still carries the key map for every
  client that existed. It is a shipped migration and stays as it is. Its fixture now reads
  the removed clients' rows as JSON.
- The importer for `radarr.db` and `prowlarr.db` must skip a provider whose implementation no
  longer exists (and say so), not fail. `DownloadClientSettingsConverter.Handles` is false for
  the removed clients, which is how the planner will tell them apart.
- Other languages' `Localization/Core/*.json` still hold keys for the removed providers; they
  are harmless and synced from `en.json` by the translation tool.
