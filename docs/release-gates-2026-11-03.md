# Release-gate checkpoint — 2026-11-03

This is a local candidate on `codex/release-gates-20261103`, branched from
clean `develop` at `8a9ced0e1` after fetching `origin/develop`. No candidate
commit was deployed. Production observations were read only.

| Gate | Evidence | Status |
| --- | --- | --- |
| Routine sign-in and API | The owner completed a normal-feeling sign-in. The existing account's stored work factor rose from 10,000 to 220,000 iterations. Anonymous status returned 401; authenticated status and Hygiene journal returned 200. No browser login POST timing was captured. | Rehash verified; numerical login latency open. |
| Natural TV search and blocked hash | No post-deploy search command or new history row was present at the final read. No grab was initiated. | Waiting for natural traffic. |
| qBittorrent removal | The isolated qBittorrent and Hygiene fixture run passed 142 tests, including client job absence and source-path observations. The production action ID did not advance. | Fixture evidence verified; production comparison awaits a new action. |
| Calendar credentials | New Movie, TV, and Music links contain a derived feed-only key. A feed key fails general API authentication. Existing full-key links remain accepted during the owner-selected transition. | Candidate verified; legacy-link retirement remains. |
| Outbound requests | Direct release URLs and redirects reject private destinations, with another address check at connection time. Trusted proxies receive the documented weaker boundary. Decoded feeds/auth responses are capped at 16 MiB and release files at 32 MiB. | Candidate verified in focused tests; proxy residual accepted. |
| Syslog | A local UDP capture proved that message and exception secrets are removed before transmission. | Candidate verified. |
| Windows cover paths | Movie, TV, artist, and album API routes reject backslash, slash, and drive syntax in filenames. Five focused tests passed. | Candidate verified on Linux; Windows runtime acceptance open. |
| Public metadata | Development examples and owner-specific importer/indexer audit passages were generalized. The policy keeps private deployment snapshots outside Git. Published history remains unchanged. | Current-tree slice done; history exposure remains. |
| ImageSharp | Upgrading from 3.1.12 to 3.2.0 failed at build time because Six Labors now requires a key. The owner chose to leave that upgrade pending. | Blocked by build key. |
| Final image | An isolated amd64 candidate image built successfully from code head `b25de2fcf` (image ID `sha256:550efbd7b353253dd0e495f4340d8bd2b00ef8ddff5930e492a186a8402b8f0b`). Trivy 0.76.1 found six unique ImageSharp advisories (24 high and 12 medium package instances); the prior .NET 10.0.13 runtime finding was absent after updating to SDK 10.0.403 and runtime packages 10.0.14. | Inspected; release scan does not pass while ImageSharp remains. |

The nonincremental Release solution build passed with analyzers disabled because
the local checkout's baseline StyleCop configuration reports hundreds of
unrelated SA1200 errors. The image build used the repository's normal analyzer
settings and passed. API tests passed 228; Host passed 100 with five skipped;
Core passed 11,013 with 53 skipped before the final outbound patch. Focused
Common outbound tests passed 14 and focused Core download/indexer tests passed
98 after that patch. Frontend typecheck and targeted lint passed; the candidate
image's production webpack build passed.

The image was built and scanned without attaching production mounts or
recreating a running container. The candidate tag is local to the build host
and has not been published.
