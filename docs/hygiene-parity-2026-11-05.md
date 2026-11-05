# Hygiene and Cleanuparr parity — 2026-11-05

This review compares the current built-in Hygiene implementation with
Cleanuparr's official [Queue Cleaner](https://cleanuparr.github.io/docs/configuration/queue-cleaner/),
[Malware Blocker](https://cleanuparr.github.io/docs/configuration/malware-blocker/),
and [Blacklist Synchronizer](https://cleanuparr.github.io/docs/configuration/blacklist-sync/)
documentation. It is a code and focused-test comparison, not a live
side-by-side run.

| Behavior | Built-in Hygiene status |
| --- | --- |
| Existing queue cleanup | Metadata, client failure, blocked import, warnings with opt-in, stalled, slow, and cumulative unstable strikes remain. The global gate, scoped rule, minimum three strikes, preview, journal, blocklisting, and configured replacement search still guard actions. |
| Ignored downloads | Rule-scoped exact ID/hash, category, tag, tracker domain, and client-name values now suppress strikes and actions. qBittorrent exposes tag and tracker data; other clients provide only fields they already report. Cleanuparr uses a global ignore list across its features. |
| Failed-import reasons | Include/exclude plain case-insensitive substrings now filter queue messages before strikes and actions. Cleanuparr's force-import rescue and retry workflow remains open. |
| Slow and stalled safety | Completion range and minimum/maximum torrent size now hold out-of-scope or unknown-size items. Slow rules can use elapsed hours as an alternate signal. qBittorrent alternate-speed mode holds slow strikes by default, including when the mode request fails. Per-rule privacy scopes and configurable progress-reset amounts remain open. |
| Private torrents and category change | Hygiene still holds private or unverified torrents from automatic removal. Cleanuparr offers separate ignore/delete-private choices and a post-import category-change path. Those need an explicit journaled branch before enabling. |
| File-list content policy | A read-only qBittorrent preview evaluates inline or file/URL blocklist or allowlist patterns and the mixed-content choice. It does not yet skip individual files or remove a blocked download; no content action enters the Hygiene journal. |
| qBittorrent excluded names | Explicit or opt-in hourly sync loads an inline or file/URL list, compares and updates Web API v2's `excluded_file_names`, and leaves its enable switch unchanged. Missing or invalid source content stops sync before any client update. |

The new features are opt-in. Migration 23 gives existing rules neutral values.
The content preview and hourly sync start disabled. Production was not changed or exercised.
