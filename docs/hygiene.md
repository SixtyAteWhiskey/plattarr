# Hygiene queue monitoring

Hygiene is built into Plattarr. Activity > Hygiene shows a queue assessment of
tracked and pending downloads, persisted strikes, scoped monitoring rules, and
the action journal. With the global gate off, the queue preview and monitoring
are read only and `wouldAct` remains false.

## Monitoring rules

Create rules on the Hygiene page or through `/api/v3/hygiene/rule`. A rule names
one of `client-failed`, `import-blocked`, or `download-warning`; a media kind
(Movies, TV, Music, or all); a download client (or all); a strike limit; and a
minimum number of minutes between strikes. New rules are disabled. A rule
enabled for a specific kind and client takes priority over one for just the
kind, just the client, or all. A disabled specific rule also overrides a broad
enabled rule.

Each queue refresh observes active downloads. A strike starts at one for a
matching problem and increases only after the configured interval with the
same problem and no download progress. Progress, a different reason, recovery,
or disabling the matching rule resets the count. Strike state is stored by
download-client ID and download ID; records not seen for 30 days are removed.
Pending releases have no client download ID and do not accumulate strikes.

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

The first executor slice handles tracked `client-failed` and `import-blocked`
items. Generic `download-warning` items stay monitor-only. For an eligible
item, it removes the job from the download client **without deleting its
files**, records the failure through the existing per-kind failed-download
handlers, and stops tracking it. Those handlers record a blocklist entry and
request the normal replacement search when the existing failed-download
settings allow it. Private or unverified torrent releases remain manual-review
items. Pending releases and unmatched downloads are never acted on.

Each action records its media kind, reason, authorizing rule, current
checkpoint, status, and recovery guidance in the main database. Checkpoints
allow later queue refreshes to continue after client removal if a later step
failed. `GET /api/v3/hygiene/action` returns the latest entries. If the global
gate is turned off mid-action, the action pauses at its last checkpoint;
re-enabling the gate resumes the journal, while an unstarted action also needs
its original rule to remain eligible. Failures keep a safe error type and a
recovery path; raw exception text and download paths are not persisted.

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
