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

1. Expand TV to season/episode search units while keeping one bounded
   provider-facing dispatch per scheduled run and excluding active queue
   items and recent grabs. Music albums and Movie cutoff-unmet upgrades are
   covered by the separate slices below.
2. Add per-kind cycle progress, then expose quiet hours. Test
   that manual and replacement searches do not incorrectly advance a cycle.

The recent-search-time lists still use `LastSearchTime` for Movies or episodes
missing a file. The separate scheduled activity lists are durable journals and
retain runs after a selected item acquires a file.

## Music and Movie upgrade slices

Music now has a separate album preview, activity list, and scheduled search
switch. The switch defaults off. A scheduled run searches at most one missing,
released album whose album and artist are monitored. Albums in the queue,
grabbed within the configured window, searched within 24 hours, or associated
with three distinct failed downloads in seven days are skipped. The same
eligibility calculation feeds the preview and execution. Music keeps its own
60-minute default interval, active-download limit of five, and 24-hour
recent-grab window. Manual album searches do not add scheduled activity.

Movie quality-cutoff upgrades use another default-off switch, preview, and
activity list. This schedule selects from Movies with an existing file below
their quality profile cutoff, so it cannot turn the missing-Movie schedule
into a bulk upgrade search. It reuses the Movie availability, monitoring,
queue, recent-grab, retry-limit, and 24-hour search cooldown checks. At most
one Movie search is dispatched per run. Its default pacing and bounds match
the other kinds, but its interval and gates are persisted separately. Manual
Movie searches do not add upgrade activity.

Both new scheduled paths record disabled, paused, empty, no-grab, grabbed, and
failed runs. A preview read does not dispatch a provider request. TV
season/episode units, cycle progress, and quiet hours remain later slices.
