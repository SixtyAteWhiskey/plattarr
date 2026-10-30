# Hygiene queue monitoring

Hygiene is built into Plattarr and appears as a top-level page in the main
navigation. It shows a queue assessment of tracked and pending downloads,
persisted strikes, scoped monitoring rules, and the action journal. The preview
shows aggregate counts by reason before the per-download explanation and
recommendation. These counts are informational. With the global gate off, the
queue preview and monitoring are read only and `wouldAct` remains false.

The **Queue preview** tab shows a compact read-only list of configured rules,
the current queue assessment, and the action journal. **Manage automation**
opens the **Automation settings** tab, which contains the global action gate
and the full rule editors. The tabs do not change monitoring or cleanup state.

The [Cleanuparr parity verification](hygiene-parity-2026-10-26.md) records
which behaviors are proven on the current checkout and which feature areas
remain different or unimplemented.

## Monitoring rules

Create rules on the Hygiene page or through `/api/v3/hygiene/rule`. A rule names
one of `client-failed`, `import-blocked`, `download-warning`,
`download-stalled`, `download-slow`, `download-unstable`, or
`download-metadata`; a media kind
(Movies, TV, Music, or all); a download client (or all); a strike limit; and a
minimum number of minutes between strikes. New rules are disabled. A rule
enabled for a specific kind and client takes priority over one for just the
kind, just the client, or all. A disabled specific rule also overrides a broad
enabled rule.

Action-capable rules have a **Delete source files when this rule acts** option.
It defaults off for new rules and all rules that existed before migration 17.
When enabled, Hygiene asks the download client to remove the job and its source
files. This affects future actions only; it does not remove files left behind
by previous actions. `download-warning` also requires its separate **Allow
automatic cleanup for download warnings** switch. Existing rules keep this
switch off after migration, even if monitoring was already enabled. The rule,
strike threshold, global action gate, and existing torrent
safety checks must still allow an action before any removal occurs.

Each saved rule card shows how many current queue assessments select that
specific rule and, when enabled, how many have reached its strike threshold.
This read-only preview uses the same matched rule ID and eligibility decision
as the queue preview; edits do not change the summary until saved.

Each queue refresh observes active downloads. A strike starts at one for a
matching problem and increases only after the configured interval with the
same problem. Progress resets strikes for failures and stalled downloads. For
a slow-download rule, progress does not reset strikes while the measured rate
remains below its threshold; a recovered rate or different reason resets the
count. Disabling the matching rule also resets it. Strike state is stored by
download-client ID and download ID; records not seen for 30 days are removed.
Pending releases have no client download ID and do not accumulate strikes.

`download-unstable` is a separate rule for repeated slow or stalled signals on
one download. It defaults to 12 strikes at least 5 minutes apart, starts
disabled, and has its own source-file deletion and replacement-search choices.
The cumulative count survives changes between slow and stalled and healthy
intervals; disabling the rule or switching to a different scoped cumulative
rule clears it. A matching slow-download rule supplies
the speed threshold for slow signals. The queue preview shows the cumulative
count while this rule is enabled. When a current slow or stalled signal reaches
both the consecutive and cumulative limits, the consecutive rule takes priority.
The cumulative rule can act only while a slow or stalled signal is current.

`download-stalled` is reported when qBittorrent explicitly reports its
`stalledDL` state. `download-slow` is reported when the active rate is below
the minimum KiB/s configured on a matching slow-download rule. The current
rate is available from qBittorrent; clients that do not report a rate remain
eligible for stalled and other assessments, but are not classified as slow.
Both reasons can be acted on when their matching rule is enabled, its strike
threshold is reached, the global action gate is on, and the download is tracked
and safe to remove. Private or unverified torrent releases remain manual-review
items. Stalled/slow actions keep source files by default, or ask the client to
delete them when the matching rule opts in, then use the existing failed-download
path for blocklisting and any configured replacement search.

`download-metadata` identifies qBittorrent `metaDL` and `forcedMetaDL`
states. A new rule starts disabled; enabled rules count consecutive observations
at the configured interval and reset when metadata is no longer being fetched.
The first observation records the first strike, so a three-strike rule needs at
least two complete intervals. The same torrent privacy, tracked-download, and
global gate checks apply as for stalled downloads. The client state is checked
again immediately before removal, and cleanup pauses if metadata completed or
the torrent disappeared. Source-file deletion defaults off.

`GET /api/v3/hygiene/preview` reports the current reason, count, matching
rule threshold, eligibility, and an explanation. `GET`, `POST`, `PUT /{id}`,
and `DELETE /{id}` on `/api/v3/hygiene/rule` manage monitoring rules. The
normal API authentication applies.

## Action gate and journal

Migration 13 adds `HygieneActions`. The global action gate is available at
`/api/v3/hygiene/config` and defaults off. A scoped rule must still be enabled
and reach its strike threshold. The preview and executor share the same
`IHygieneQueueDecisionService` assessments, so `wouldAct` identifies the items
the executor would process under the current settings.

The executor handles tracked `client-failed`, `import-blocked`,
`download-stalled`, `download-slow`, `download-unstable`,
`download-metadata`, and explicitly opted-in `download-warning` items.
Download warnings include generic client or import warnings, so review a rule’s
scope and the reported warning before enabling cleanup. For an eligible item,
it removes the job from the download client, keeping its files by default or deleting them when the matched
rule opts in, records the failure through the existing per-kind failed-download
handlers, and stops tracking it. Those handlers record a blocklist entry.
Each rule can turn off the automatic replacement search. When the rule leaves
search on, the existing failed-download settings still determine whether a
search runs. Migration 19 keeps search on for existing rules, and new rules
default to the same behavior while starting disabled for monitoring. The rule's
search choice is recorded on each action. Turning search off before a prepared
action retries can suppress it; turning search on cannot escalate an action
prepared with search off. Private or unverified
torrent releases remain manual-review items. Pending releases and unmatched
downloads are never acted on.

Each action records its media kind, reason, authorizing rule, source-file deletion
and replacement-search choices, current checkpoint, status, and recovery
guidance in the main database. Checkpoints
allow later queue refreshes to continue after client removal if a later step
failed. Enabling deletion later cannot make an existing prepared action delete
files. Disabling deletion before client removal changes that action to keep
files. Existing journal entries default to keeping files.
`GET /api/v3/hygiene/action` returns the latest entries. If the global
gate is turned off mid-action, the action pauses at its last checkpoint;
re-enabling the gate resumes the journal, while an unstarted action also needs
its original rule to remain eligible. Failures keep a safe error type and a
recovery path; raw exception text and download paths are not persisted. The
System log records the action ID, media kind, reason, client ID, checkpoint,
and error type. Failed journal entries expose a concise stage explanation and
manual recovery steps in the expandable **Failure details** section.

Migration 18 records the replacement command ID, target item IDs, terminal
search status, grab count, and completion time for new actions. The action's
`completed` status and `replacement-requested` checkpoint describe cleanup and
the failed-download handoff; neither proves that a search completed, a release
was grabbed, or a file was imported. The journal shows these as separate
observations. A completed search with zero grabs is explicit, whether an
indexer returned no rows or every row was rejected. Search failures remain
distinct from cleanup failures.

When a rule skips search, the failure and blocklist are still recorded. New
actions then use the `replacement-skipped` checkpoint and expose a recorded
`skipped` search state instead of implying that a search was requested.

For TV, the read-only action response groups later grab history by download ID
and counts only overlap with that action's targeted episode IDs. It then shows
import and failure history for each matching download. A one-episode regrab of
a seven-episode pack is therefore one of seven covered, not seven recovered.
An import entry confirms an import event; it does not assert that the file is
still present. Older journal entries lack a stored command link. When an
unambiguous command remains in the short-lived command table, the API labels
the match as inferred; otherwise search completion stays unverified. Later
matching grabs can still be shown without attributing them to that search.
The per-rule file-deletion choice records what was requested from the client,
not independent proof that former source paths were removed.

`tools/liveproof/run.py hygiene` is a read-only rollout check. It confirms the
global gate is off and no action remains failed or incomplete. Disposable
Movie, TV, and Music recovery tests cover successful removal while retaining
files, client-removal retry, and journal resumption after a later failed-
download step. The `hygiene-recovery` proof passed on a disposable localhost
Movie instance with synthetic Newznab and SABnzbd fixtures. It changes the
mock SAB category from Movie to TV after the grab, then verifies that grab
history keeps the action Movie-scoped, the source remains on disk, the original
is blocklisted, and a distinct replacement is queued in the Movie category.
The replacement remains in the synthetic client; this does not validate a
real client's transfer behavior. The action gate remains off by default;
rollout is a separate step. The scratch proof does not validate live tracker
behavior or shared-instance settings.
