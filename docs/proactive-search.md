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
skip reasons, and shows recent episode search times. It is a planning preview:
there is no scheduled proactive TV job yet. Movie and TV skipped rows offer
manual searches using the existing Movie and Missing Episode commands. The TV
action searches aired missing episodes of one show, including unmonitored
episodes, while skipping episodes already in the queue. Fully queued items do
not offer a duplicate search. Movie retry-limit overrides and all TV show
searches require confirmation because a TV search may make several indexer
requests. Accepting a command only means the search was queued, not that a
release was grabbed or imported.

The active automated policy is still fixed: one missing Movie per hourly run, at least
24 hours between searches of one Movie, and a pause after three distinct failed
downloads within seven days. The existing scheduled job stays active. Manual
searches retain their existing behavior. The page labels these limits as fixed
and does not imply that TV, Music, or upgrades are already automated.

## Cleanuparr comparison and next slices

[Cleanuparr Seeker](https://cleanuparr.github.io/docs/configuration/seeker/)
currently searches missing and cutoff-unmet items across Radarr, Sonarr, and
Lidarr. Its per-instance preview explains exclusions; its cycle progress,
recent-grab window, active-download limit, minimum cycle time, quiet hours,
and round-robin pacing provide further safety. This slice starts with
visibility and keeps Plattarr's existing conservative Movie rate.

1. Add a persisted proactive-search gate and pacing settings, with the
   existing hourly Movie policy as the migration default. Keep replacement
   search behavior separate from the proactive gate.
2. Expand the shared candidate model to Movie cutoff-unmet upgrades, TV
   season/episode search units, and Music albums. Reuse their existing search commands
   while making one provider-facing request per scheduled run. Exclude active
   queue items and recent grabs for each kind.
3. Persist cycle and search activity state, add per-kind progress and recent
   results to the page, then expose quiet hours and configurable limits. Test
   that manual and replacement searches do not incorrectly advance a cycle.

The current recent-search lists use `LastSearchTime` for Movies or episodes
still missing a file. They are not durable activity journals and do not show
searches for items that later acquired files.
