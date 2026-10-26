# Hygiene queue monitoring

Hygiene is built into Plattarr. Activity > Hygiene shows a read-only assessment
of tracked and pending downloads. It does not remove, blocklist, search, change
categories, or delete files. An eligible rule still reports `wouldAct: false`.

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

## Current limit and next implementation

Migration 12 creates `HygieneQueueStates` and `HygieneRules` in the main
database. This local work has not been pushed or deployed. Before enabling
automatic cleanup, add an executor that consumes the same
`IHygieneQueueDecisionService` assessments, an explicit action setting and
action journal, and isolated tests of client removal, blocklisting, replacement
search, private torrents, category changes, and import recovery.
