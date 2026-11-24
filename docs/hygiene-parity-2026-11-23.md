# Hygiene and Cleanuparr parity — 2026-11-23

Hygiene provides integrated queue recovery and content protection for Plattarr's
Movies, TV, and Music modules. **It is not a complete Cleanuparr replacement.**
The outstanding features below require implementation and acceptance work;
they are not ordinary maintenance patches.

This review checked the current source, regression fixtures, and Cleanuparr's
official [Queue Cleaner](https://cleanuparr.github.io/docs/configuration/queue-cleaner/),
[Seeker](https://cleanuparr.github.io/docs/configuration/seeker/),
[Download Cleaner](https://cleanuparr.github.io/docs/configuration/download-cleaner/),
and [client capability matrix](https://cleanuparr.github.io/docs/supported-apps/).
It does not claim a side-by-side executable conformance test.

| Area | Verified Plattarr behavior | Remaining difference |
| --- | --- | --- |
| Queue recovery | Scoped, persistent strikes; failed jobs and imports; metadata, stalled, slow, and cumulative signals; progress resets; ignore filters; private-torrent and category policies; retry-safe journal; blocklist and replacement outcomes. Native failure handling defers to an active Hygiene failure rule. | Counting follows configured time intervals rather than Cleanuparr job runs. Untracked jobs remain held. There is no errored-torrent resume attempt before removal. |
| Connection outages | qBittorrent network status now holds strikes and automatic actions, including a second check before removal. Unknown modern status fails closed. | This is a client-specific BitTorrent check, not Cleanuparr's configurable Internet connectivity probe. Other clients do not supply the same signal. |
| Import rescue | Opt-in Movie/TV rescue, constrained candidate validation, settling observations, attempt limits, and later import-history confirmation. | Music rescue and private/unverified torrent rescue are not supported. |
| Content protection | Inline/file/URL blocklists and allowlists, read-only previews, separately gated actions, mixed-file handling, and qBittorrent exclusions synchronization. | Supported adapters are qBittorrent and SABnzbd. SAB file entries cannot establish unpacked-content allowlist safety. Private/unverified torrent actions are held. |
| Replacement search | Existing per-kind failed-download handlers; journaled commands, outcomes, grabs, and TV episode coverage; bounded repeated-failure recovery. | No shared round-robin replacement queue with priority over every proactive schedule. |
| Proactive search | Missing Movies, TV episodes, Music albums, and separate Movie cutoff upgrades; one search per run, independent schedules, active-download and recent-grab gates, previews, and durable activity. | No durable cycle/reset model, quiet hours, configurable selection strategies, season search units, TV/Music scheduled upgrades, or custom-format-gap selection. |
| Completed-download retention | Existing client/indexer ratio and seeding-time handling remains available. | No ordered Hygiene seeding-rule engine with stop/delete actions, tracker/tag scope, minimum seeders, and inactivity guards equivalent to Download Cleaner. |
| Unlinked and orphan cleanup | Existing library/database housekeeping remains separate from download cleanup. | No cross-client unlinked-download scanner or orphan quarantine/review/purge workflow. Library housekeeping is not evidence of these features. |
| Visibility | Queue and rule previews, reason counts, journal pagination, actionable recovery explanations, and downloader System Health. | No complete Cleanuparr strike/removal/cycle notification and statistics contract. |
| Client coverage | Existing Plattarr clients retain their normal downloading/import behavior. | Full Hygiene capability parity across Deluge, Transmission, rTorrent, and uTorrent is not established. |

The prior [queue/content review](hygiene-parity-2026-11-05.md) remains useful for
the detailed implemented safeguards. The
[proactive-search design](proactive-search.md) records the current schedule
boundaries. Neither document should be read as full-product parity.

Automatic settings remain opt-in where documented. This audit does not enable
new schedules, alter existing rules, or authorize orphan deletion. New
destructive workflows require disposable filesystem/client proofs, cross-seed
and hardlink safeguards, recovery records, and a usable preview before rollout.
