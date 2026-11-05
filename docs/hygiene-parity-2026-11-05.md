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
| Failed-import reasons and rescue | Include/exclude plain case-insensitive substrings filter queue messages. An opt-in force-import path for tracked Movie and TV downloads checks the three documented rescue phrases, resolved media, every manual-import candidate and rejection, then requires two processed observations before it journals a command request. It waits for later matching import history, holds strikes during active attempts, limits retries, and returns to strikes when attempts are exhausted or no safe candidate exists. Music and private or unverified torrents remain outside this path. |
| Slow and stalled safety | Completion range and minimum/maximum torrent size now hold out-of-scope or unknown-size items. Slow rules can use elapsed hours as an alternate signal. qBittorrent alternate-speed mode holds slow strikes by default, including when the mode request fails. Per-rule privacy scopes and configurable progress-reset amounts remain open. |
| Private torrents and category change | Rules hold private torrents by default and can explicitly select removal or a qBittorrent or SABnzbd post-import category change. Unverified releases always stay held. Public torrents can also select category change. The client category is checked after the request; the journal records this branch without blocklisting or searching. This differs from Cleanuparr's per-feature choices. |
| File-list content policy | qBittorrent and SABnzbd job file lists feed both preview and executor. A separate action switch starts off, also requires the global gate, and journals mixed-file skipping or whole-download removal. qBittorrent Web API v2 marks blocked files priority 0; SABnzbd removes matched NZB file entries. Both verify the resulting file list. Whole-download removal uses the existing failed-download blocklist and replacement-search path. Private or unverified torrents are held. Web API v1 lacks file skipping. SABnzbd lists NZB file entries, which may not reflect the names of unpacked contents. |
| qBittorrent excluded names | Explicit or opt-in hourly sync loads an inline or file/URL list, compares and updates Web API v2's `excluded_file_names`, and leaves its enable switch unchanged. Missing or invalid source content stops sync before any client update. |

The new features are opt-in. Migrations 23–26 give existing rules neutral values
and keep old journal entries outside the new content and category branches.
Content inspection, content actions, force-import rescue, and hourly sync start
disabled. Force-import requests are confirmed only by later matching import
history; the command state alone is not treated as success. Production was not
changed or exercised.
