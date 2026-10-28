# Pre-public release gates — 2026-10-27

This checkpoint records read-only release evidence and two bounded local
implementation slices. It is not a publication approval. The checkout remains
an intentionally dirty `develop` tree based on
`e047064010a78fb74b076c232c587a34be6e9d04`; `origin/develop` remains at that
Git revision and production remains on immutable image `sha-e047064`. Nothing
in this checkpoint was
committed, pushed, published, deployed, rotated, or exercised against
production.

## Gate inventory

| Roadmap item | Status | Evidence or boundary |
|---|---|---|
| 3. Observed Prowlarr management contract | Consumer discovery complete; bounded GET capture/replay actionable | Two released clients were found using GET-only `/api/v1` routes. Mutable management routes and the separate feed remain unobserved and out of scope. |
| 4. Native Hygiene parity | One bounded slice complete; broader parity remains partial | Stored and newly configured strike limits now have a minimum of three. The global action gate remains off, files are preserved, and stalled/slow rules remain monitor-only. |
| 5. UI refresh | One bounded slice complete | System Backup now uses the shared shell. Updates, Events, Log Files, Wanted, and 11 legacy Settings destinations remain. |
| 6. Accessibility in touched slices | Complete for the Backup and Hygiene changes; shared legacy debt remains | Backup dialog naming/focus/status, named row actions, label/control association, alerts, non-color state, checkbox description, and responsive table containment were checked with writes blocked. |
| 7. Pre-public evidence | Partial with material blockers | Source/history, dependencies, compatibility authorization, exposure guidance, and resource measurements are recorded below. Container-image inspection and advisory-specific runtime checks remain genuinely blocked or deferred. |

## 3. Prowlarr management contract discovery

Seerr is not the consumer for this surface. The existing GET-only replay still
shows that `/{indexerId}/api?t=caps` returns the frontend HTML shell rather than
a Newznab/Torznab capability feed.

Two real released clients were identified:

- `tikibozo/prowlarr-indexer-report` v0.6.3 sends `X-Api-Key` and reads
  `/api/v1/indexer`, `/api/v1/appprofile`, `/api/v1/indexerstats`, and paged
  `/api/v1/history?eventType=1&page=...&pageSize=1000&sortKey=date&sortDirection=descending`.
  Its refresh flow consumes all four responses. See its pinned
  [`prowlarr.py`](https://github.com/tikibozo/prowlarr-indexer-report/blob/2787ecf55b29904cced387cd5d50cbb92b71280c/app/prowlarr.py#L68-L149)
  and [refresh flow](https://github.com/tikibozo/prowlarr-indexer-report/blob/2787ecf55b29904cced387cd5d50cbb92b71280c/app/prowlarr.py#L390-L406).
- Homepage's Prowlarr widget reads only
  `/api/v1/indexerstats?apikey=...` and aggregates the returned statistics. See
  its pinned [widget definition](https://github.com/gethomepage/homepage/blob/c1cb232fc57719c5303baaa82d97910c844c3519/src/widgets/prowlarr/widget.js#L3-L14)
  and [consumer](https://github.com/gethomepage/homepage/blob/c1cb232fc57719c5303baaa82d97910c844c3519/src/widgets/prowlarr/component.jsx#L7-L44).

Observed response use is narrow: indexer identity/configuration fields,
application-profile identity and automatic-search state, aggregate query/grab
statistics, and history records plus pagination totals. No POST, PUT, PATCH, or
DELETE contract was observed. A bounded capture/replay of the observed GET
routes is now actionable but was not folded into these slices. Implementing a
write surface or feed contract would be speculative and remains blocked
pending a real consumer trace.

## 4. Hygiene parity slice: minimum three strikes

This slice adopts Cleanuparr's safety-oriented minimum of three observations
without broadening action authority:

- migration `015_hygiene_minimum_strikes` raises stored values below three to
  three and leaves higher values unchanged;
- backend validation and the numeric control now enforce `3..100`;
- migration and service tests cover both the repair and rejection boundary;
- the immediate-action checkbox now has a separate accessible description
  explaining that it only records strikes and that the global action gate is
  independent.

No file-deletion behavior was added. The global gate remains off.
`download-stalled` and `download-slow` remain read-only monitoring reasons.
Disposable fixtures proved the migration and validation behavior; production
data and providers were not used.

## 5–6. System Backup shell and accessibility

The System Backup route now has one H1, lead copy, a labeled H2 content region,
visible table column labels, alert semantics for failures, and non-color backup
type text. Row actions have filename-specific accessible names. The restore
dialog now has a programmatic name, a file input label/control association,
explicit pending/in-progress/completed/failed text in a live region, contained
keyboard focus, an explicitly named close control, and focus restoration.
Routes and create/restore/delete handlers are unchanged.

Write-blocked loopback acceptance covered:

- light, dark, and color-impaired presentation;
- 1200/993/992/769/768/480-pixel boundaries with local table scrolling and no
  document overflow;
- semantic headings, accessible row actions, dialog label, file control, and
  visible status text;
- keyboard focus cycling, Escape close, and return to the Restore Backup
  trigger;
- a disposable one-byte manual backup fixture.

The proxy blocked three SignalR negotiation POSTs and observed no backup or
provider action write. The fixture was removed after proof.

## 7. Pre-public release evidence

### Security review

A standard, single-pass Codex Security scan completed with partial coverage and
10 validated findings. No security fix or credential rotation was made.

1. **High:** a network-reachable fresh instance defaults to wildcard binding
   and no authentication; `/initialize.json` discloses the unrestricted API
   key, enabling an ownership/setup race.
2. **Medium:** Movie, TV, and Music calendar subscription URLs delegate the
   unrestricted global API key rather than a read-only feed credential.
3. **Medium:** Forms login has no server-side request limiter, lockout, or
   backoff.
4. **Medium:** passwords use a fixed 10,000-iteration PBKDF2-HMAC-SHA512 work
   factor and successful logins are not rehashed to a stronger setting.
5. **Medium:** a malicious or compromised configured indexer can supply a
   download URL or redirect to loopback/private destinations; automatic RSS
   processing makes blind server-side requests possible.
6. **Medium:** automatic gzip/Brotli decompression is followed by unbounded
   full-body buffering, permitting memory exhaustion from a malicious indexer
   response.
7. **Medium:** the optional UDP syslog target bypasses the local message
   cleanser. It can emit raw API-key query parameters at the default Debug
   level, SignalR access tokens at Trace, and selected failed-OIDC identity
   fields at Error.
8. **Medium:** authenticated media-cover filenames lack canonical containment;
   encoded backslashes or rooted paths may traverse on Windows. Runtime Windows
   reproduction is still deferred.
9. **Low:** selected API errors preserve `exception.ToString()` and expose
   stack details to authenticated clients.
10. **Low:** tracked public-release examples and reports disclose maintainer
    workstation, topology, library, indexer, path, and deployment-state
    details.

The scan did not find a compatibility-route authentication bypass. It also
rejected two candidate claims: invalid host-config ModelState does not retain
valid sibling credentials, and the current `_blank`/`noreferrer` link sink did
not execute an indexer-controlled JavaScript URL in the tested browser.

### PII and secret review

The read-only scanner covered 7,071 current tracked paths and every one of the
193,968 reachable Git objects. It scanned 82,558 text blobs no larger than
4 MiB, totaling about 1.36 GB. Binary blobs and individual blobs above 4 MiB
were not byte-complete.

- No private key or AWS, GitHub, GitLab, Slack, Stripe-live, Google, npm, or
  PyPI token pattern was found in the covered current/history text.
- Current source contains three credential-like shared client identifiers for
  TMDB, Headphones VIP, and AcoustID. They appear to be public/shared client
  identifiers rather than privileged owner secrets, but still carry policy or
  quota risk and need an owner decision before publication. Their values were
  not printed or used.
- Reachable history contains removed credentials for several retired upstream
  reporting, social, metadata, and logging services. Validity/revocation was
  not tested and no value was printed or contacted.
- Current tracked examples and audit documentation contain real maintainer and
  deployment details, including a username, host label, hardware class, mount
  topology, timezone, library counts, and integration inventory. These need a
  public-release redaction decision.

No credential was rotated because the evidence does not establish ownership,
validity, or an authorized rotation path.

### Dependency and container checks

- The official npm advisory feed returned 34 advisory instances: 16 high, 15
  moderate, and 3 low. Every affected path is rooted in top-level development
  dependencies used for build, lint, or live reload; no Node runtime is shipped
  in the application image. They remain actionable build-chain debt.
- The .NET vulnerability audit found one affected package:
  `SixLabors.ImageSharp` 3.1.12, direct in `Radarr.Core` and transitive in
  runtime projects, under four high and two moderate advisories. Production
  resize configuration registers JPEG, PNG, and WebP and skips metadata, which
  narrows some named paths but does not prove all six advisories unreachable.
  Upgrade/reachability testing remains a separate owner-authorized slice. The
  advisories are
  [GHSA-v76p-62qx-wwq2](https://github.com/advisories/GHSA-v76p-62qx-wwq2),
  [GHSA-gwg2-r3hj-4w44](https://github.com/advisories/GHSA-gwg2-r3hj-4w44),
  [GHSA-j3p4-wp97-rph4](https://github.com/advisories/GHSA-j3p4-wp97-rph4),
  [GHSA-j9gm-c75j-xc9q](https://github.com/advisories/GHSA-j9gm-c75j-xc9q),
  [GHSA-wmxv-xphr-5c9q](https://github.com/advisories/GHSA-wmxv-xphr-5c9q), and
  [GHSA-jjfr-hcj7-qf5w](https://github.com/advisories/GHSA-jjfr-hcj7-qf5w).
- Container/image inspection is genuinely blocked on this host: no local image
  tar or OCI image is present, and Docker, Podman, containerd clients, Skopeo,
  Crane, ORAS, Syft, Grype, Trivy, Cosign, Dive, and Dockle are unavailable.
  The immutable production image was not pulled or contacted to work around
  that boundary.

### Compatibility-route authentication and authorization

A disposable schema-15 loopback instance exercised canonical, `/radarr`, and
`/sonarr` status routes. Every route returned 401 without a key and 200 with
the correct key; `/sonarr` retained its expected compatibility identity. No
authentication or authorization bypass was observed. The scratch instance was
removed.

### Exposure guidance

The present defaults bind all interfaces and permit no-auth first-run setup.
Until the high-severity ownership finding is resolved, a fresh instance must
not be reachable from an untrusted network. Deployment guidance should require
private-network access or an authenticated reverse proxy before first start,
then explicit application authentication. Public port publishing, calendar
URLs containing the global key, and verbose UDP syslog are not safe defaults
for an internet-facing deployment.

This is guidance, not proof that the current private production deployment is
publicly reachable.

### Startup, memory, bundle, and hot paths

Measurements came from a disposable loopback Release instance and current
frontend output:

- warm start to `/ping`: **1,119 ms**;
- steady RSS/HWM: **197,508 KiB**, 21 threads;
- RSS after hot-path sampling: **206,344 KiB**;
- frontend output: **49,351,605 bytes** across 46 files;
- JavaScript: **46,507,163 bytes**, concatenated gzip **10,524,978 bytes**;
- CSS: **866,889 bytes**, concatenated gzip **57,837 bytes**;
- largest JavaScript assets: **25,315,925** and **20,951,350 bytes**.

Thirty sequential GETs per route produced these loopback timings:

| Route | p50 | p95 | max |
|---|---:|---:|---:|
| `/ping` | 0.512 ms | 0.777 ms | 9.648 ms |
| status | 0.993 ms | 8.204 ms | 31.550 ms |
| backups | 0.622 ms | 1.323 ms | 13.102 ms |
| Hygiene preview | 1.128 ms | 13.650 ms | 14.135 ms |
| queue | 0.975 ms | 7.647 ms | 130.055 ms |

All sampled requests returned 200. These are local baselines, not production
capacity claims.

## Verification completed

- Focused Hygiene Core/migration tests: 34 passed.
- Full backend gate: Release build with zero warnings/errors; API 209 passed;
  Host 78 passed and 5 skipped; Core 10,801 passed and 53 skipped.
- Full frontend gate: typecheck, repository lint, stylelint, clean production
  build, localization JSON parsing, and `git diff --check` passed.
- System Backup write-blocked browser acceptance passed across themes,
  keyboard focus, semantic state, and exact responsive boundaries.

## Remaining decisions and blockers

Before a public release, the owner should separately authorize and prioritize:

1. the high-severity first-run ownership/authentication design;
2. scoped calendar feed credentials and Forms-login throttling;
3. outbound destination and response-size policy;
4. ImageSharp upgrade/reachability testing and build-chain dependency updates;
5. public metadata/history policy and any evidence-based credential revocation;
6. a container-image scan on a host with an inspectable image and scanners;
7. Windows media-cover traversal reproduction and remediation design.

These gates remain open; this checkpoint does not declare the combined roadmap
work or a public release complete.
