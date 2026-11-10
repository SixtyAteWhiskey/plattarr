# Proactive Search rollout

The first slice gave the existing hourly missing-Movie search a read-only
eligibility preview. The preview and scheduled
command use `MovieProactiveEligibility` for monitored, availability, queue,
recent-search, and failed-download decisions. The page shows counts, the next
ten candidates, search times for missing Movies, and paged skip reasons with a
title filter. Reading the preview makes no search request and changes no
search state.

The preview now lives under Hygiene → Proactive search, with separate Movie and
TV subsections. The former `/proactive-search` URL redirects to that tab. The
TV subsection groups aired, missing episodes by show, displays candidate and
skip reasons, and shows recent episode search times. Movie and TV skipped rows offer
manual searches using the existing Movie and Missing Episode commands. The TV
action searches aired missing episodes of one show, including unmonitored
episodes, while skipping episodes already in the queue. Fully queued items do
not offer a duplicate search. Movie retry-limit overrides and all TV show
searches require confirmation because a TV search may make several indexer
requests. Accepting a command only means the search was queued, not that a
release was grabbed or imported. The action is disabled while its command is
running and becomes available again when the command ends, unless the item is
in the download queue.

Movie scheduled search has a separate persisted enable switch, on by default.
Its default interval remains 60 minutes, with one provider-facing Movie search
per run. TV has its own switch, off by default, and also defaults to 60 minutes.
The interval can be set from 5 to 1440 minutes independently for each kind.
The scheduler updates its next-run cadence when settings change; changing the
interval does not immediately dispatch a search. The 24-hour per-item
search cooldown and the limit of three distinct failed downloads within seven
days remain fixed.

Each kind also has an active-download limit (default 5, range 1–100) and a
recent-grab exclusion window (default 24 hours, range 1–168). A scheduled run
pauses before any provider request when the queue has reached the active limit.
Active queue downloads are counted by distinct download ID, including queued,
paused, and downloading items; completed and failed items do not count. A recent
grab excludes that Movie or TV episode until its window expires, even when it
has left the queue. These gates apply to scheduled missing-item searches only.
Manual searches and failed-download replacement searches keep their existing
rules. A configured interval never increases the one-search-per-run bound.
Every scheduled Movie run is recorded in a durable recent-activity list, including
disabled and no-candidate runs. Entries show the selected Movie when there is one,
the eligible and missing counts, grabs, and a failure message if the attempt fails.
Manual Movie searches do not add entries to this list. Paused runs record the
active limit and count; empty runs record the counts for each exclusion reason.
The eligibility preview remains read-only and does not add entries. It shows
the current scheduled pause reason and the recent-grab reason on affected rows.

Scheduled TV search records every run, including disabled and empty runs, in the recent
activity list. When enabled, a run makes one bounded search dispatch for at most
one aired missing episode, even when an episode has several scene mappings.
The episode and show must be monitored, the episode must be outside the active
queue and unsearched for 24 hours, and the show must have fewer than three
distinct failed downloads in the last seven days, or a grab within the configured
window. The activity list records
the selected show and episode, whether a release was grabbed, and failures.
Manual show searches still override monitoring and do not use or change this
scheduled gate. The TV preview and activity use the same exclusion and pause
reasons as the Movie path.

## Cleanuparr comparison and next slices

[Cleanuparr Seeker](https://cleanuparr.github.io/docs/configuration/seeker/)
currently searches missing and cutoff-unmet items across Radarr, Sonarr, and
Lidarr. Its per-instance preview explains exclusions; its cycle progress,
recent-grab window, active-download limit, minimum cycle time, quiet hours,
and round-robin pacing provide further safety. Plattarr now exposes interval,
active-download, and recent-grab controls while keeping its conservative Movie
default.

1. Expand the shared candidate model to Movie cutoff-unmet upgrades, TV
   season/episode search units, and Music albums. Reuse their existing search commands
   while making one provider-facing request per scheduled run. Exclude active
   queue items and recent grabs for each kind.
2. Add per-kind cycle progress, then expose quiet hours. Test
   that manual and replacement searches do not incorrectly advance a cycle.

The recent-search-time lists still use `LastSearchTime` for Movies or episodes
missing a file. The separate scheduled activity lists are durable journals and
retain runs after a selected item acquires a file.
