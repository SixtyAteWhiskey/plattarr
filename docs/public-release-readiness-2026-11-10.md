# Public release readiness — 2026-11-10 candidate

This record updates the older release-gate checkpoints for the candidate in
PR #38. It is source and validation evidence, not approval to merge, publish,
or deploy. Production observations were read only.

| Gate | Candidate evidence | Boundary |
| --- | --- | --- |
| First-run ownership | A fresh non-loopback instance requires authentication at startup; the first-run guide uses loopback setup and an authenticating proxy. Host regression tests cover the bind/auth combinations. | Existing configured instances retain their configured bind and authentication choices. |
| Sign-in protection | Earlier slices added Forms throttling and upgraded the password work factor with rehash on sign-in. | Routine owner sign-in passed previously; no production stress test was run. |
| Calendar credentials | Generated Movie, TV, and Music subscription links use derived feed keys. This candidate rejects legacy full API keys on calendar feed routes. | Old full-key subscription URLs must be replaced by their owners. |
| Outbound and logs | Existing destination checks, response-size caps, and UDP syslog cleansing remain covered by focused fixtures. The candidate removes stack details from API error responses. | Trusted proxy behavior retains its documented weaker destination boundary. |
| Cover paths | Windows CI checks canonical containment and native resize; macOS CI checks native resize. | Exact-head checks are required before merging. |
| Image dependencies | The current source has no ImageSharp dependency after the SkiaSharp migration. NuGet audit of `Radarr.Core` reports no vulnerable packages. .NET SDK 10.0.404 and runtime/package 10.0.15 pins replace the vulnerable 10.0.14 set. | Final multi-architecture image checks and high/critical scans are required before merging. |
| Frontend build dependencies | Patched lockfile resolutions and removal of the deprecated PostCSS 6 color plugin reduced npm high/critical advisory instances from 45 to zero. Typecheck, stylelint, and production webpack build pass locally. | Audit still reports 19 moderate and 7 low instances in development dependencies; Node is not in the runtime image. Review these in normal dependency maintenance. |
| Public metadata | A targeted current-tree review found no owner host, home path, private address, or private snapshot marker in tracked documentation, deployment examples, or workflows. The owner accepted documented exposure in older public Git commits; see the metadata policy. | A newly discovered credential or private detail requires its own response. |
| Compatibility scope | Existing native indexers remain. The observed Prowlarr management consumer contract is GET-only; unobserved feed and mutable routes are not represented as complete. | Do not remove native indexers or promise full Prowlarr replacement without conversion and behavior proof. |
| Production acceptance | Natural Movie/TV scheduler activity is observed read only. Scheduled Music and cutoff-unmet upgrades remain default off in this candidate. | No production behavior is claimed for the new schedules before a separate rollout and observation. |

At the 2026-11-11 UTC read, TV had 25 naturally scheduled activity rows
spaced 60.4–60.5 minutes apart. The first was disabled; the remaining 24
selected 24 distinct episodes. Seven rows each reported one grab, and the
latest two reported no grab. The newest run had 152 eligible episodes and
selected one. Movie had two hourly `no-candidate` rows with zero dispatches or
grabs; each recorded the same three skips: one retry limit, one searched
recently, and one unavailable. Activity proves one selected search per active
TV run and the recorded Movie skips. It does not identify whether an individual
TV candidate was excluded by an active download or recent grab; those gates
remain covered by fixture tests.

The exact PR head must pass backend and frontend validation, Windows/macOS
cover checks, amd64/arm64 image startup and cover smoke, and high/critical image
scans. Keep the candidate as a draft until those checks finish. A production
rollout needs a separate decision and the private snapshot/rollback procedure
in the handoff.
