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

Movie scheduled search now has a separate persisted enable switch, on by default
to preserve the existing hourly job. Its policy remains fixed: one missing Movie
per hourly run, at least 24 hours between searches of one Movie, and a pause
after three distinct failed downloads within seven days. Manual searches retain
their existing behavior when the scheduled switch is off. The page labels these limits as fixed
and does not imply that Music or upgrades are already automated.

Scheduled TV search has its own persisted enable switch, off by default. Its
hourly job records every run, including disabled and empty runs, in the recent
activity list. When enabled, a run makes one bounded search dispatch for at most
one aired missing episode, even when an episode has several scene mappings.
The episode and show must be monitored, the episode must be outside the active
queue and unsearched for 24 hours, and the show must have fewer than three
distinct failed downloads in the last seven days. The activity list records
the selected show and episode, whether a release was grabbed, and failures.
Manual show searches still override monitoring and do not use or change this
scheduled gate. The TV rate and safety limits are fixed for this slice.

## Cleanuparr comparison and next slices

[Cleanuparr Seeker](https://cleanuparr.github.io/docs/configuration/seeker/)
currently searches missing and cutoff-unmet items across Radarr, Sonarr, and
Lidarr. Its per-instance preview explains exclusions; its cycle progress,
recent-grab window, active-download limit, minimum cycle time, quiet hours,
and round-robin pacing provide further safety. This slice starts with
visibility and keeps Plattarr's existing conservative Movie rate.

1. Add configurable pacing while preserving the existing hourly Movie policy
   as the default. Keep replacement search behavior separate from proactive gates.
2. Expand the shared candidate model to Movie cutoff-unmet upgrades, TV
   season/episode search units, and Music albums. Reuse their existing search commands
   while making one provider-facing request per scheduled run. Exclude active
   queue items and recent grabs for each kind.
3. Add per-kind cycle progress, then expose quiet hours and configurable limits. Test
   that manual and replacement searches do not incorrectly advance a cycle.

The current recent-search lists use `LastSearchTime` for Movies or episodes
still missing a file. They are not durable activity journals and do not show
searches for items that later acquired files.
