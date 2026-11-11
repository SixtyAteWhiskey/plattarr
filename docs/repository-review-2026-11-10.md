# Repository review — 2026-11-10

Base revision: `4fc1567af6fa322f49ca30e7948a554ee0ce333c` (`develop`, PR #35).

## Status and approach

Plattarr serves Movies, TV, and Music from one application. The private
multi-architecture image is in use; public release acceptance remains open.
PR #35 added gated, paced Movie and TV proactive search with eligibility
previews and activity. The Movie scheduler is on by default; the TV scheduler
is off by default. Each scheduled run dispatches at most one search. The
current approach keeps media-specific rules in their modules and puts shared
safety checks around automated actions. Continue with bounded, measured
changes rather than broad deduplication of modules.

## Tracked-tree inventory and pruning

The tracked tree contains 7,276 paths: 4,103 under `src`, 3,021 under
`frontend`, 53 under `.trail`, and the rest in documentation, tooling, assets,
and packaging. A file-by-file path, size, extension, and SHA-256 inventory
found no empty tracked file, backup suffix, temporary file, or draft artifact.
Exact-content duplicates include assets installed to different locations,
module-specific CSS, test configuration, and fixtures. Those are not evidence
of removable code by themselves.

The frontend reference scan checked CSS filenames across tracked JS, TS,
TSX, CSS, HTML, and EJS. It excluded direct imports, CSS composition, and
page-linked fonts. Fourteen CSS modules had no reference. Seven corresponding
type declarations had no consumers. This change removes those 21 files (119
lines). The paired production webpack builds produced byte-for-byte identical
output files and SHA-256 hashes. Typecheck, ESLint, stylelint, and the frozen
Yarn install passed. This confirms that the removals do not affect the current
frontend bundle. The audit is static and does not establish that every
remaining source definition is reachable at runtime.

Direct package declarations were also checked against source and build
configuration. No dependency was removed: for example, `loader-utils` is used
by the custom CSS loader, `core-js` is required by Babel's build path, and
the touch drag backend is a peer of the multi-backend preset even though the
app imports that preset rather than the peer directly. Removing them based on
an import count would break or destabilize the build.

## Prioritized next milestone

1. **Production acceptance for PR #35 and this cleanup.** Verify the exact
   deployed revision, health, configuration and action-state parity, and a
   routine scheduled cycle without triggering a live search. Observe normal
   Movie pacing and confirm TV remains off unless configured.
2. **Complete bounded proactive-search parity.** Add Music and quality-cutoff
   upgrade eligibility in independent slices, using the same preview,
   scheduling, recent-grab, and active-download gates. Keep automatic actions
   off until their rules are configured and covered by isolated proofs.
3. **Close public-release gates.** Finish outstanding compatibility-contract,
   privacy/redaction, image, and exposure checks in
   [the release-gate record](pre-public-release-gates-2026-10-27.md). Recheck
   the current implementation against each dated finding before changing it.
4. **Retire providers only after conversion parity.** Keep native indexer
   definitions until stored-row and import conversion plus real replacement
   behavior are proven, as specified in [the refactoring plan](refactoring-plan.md).

This inventory is a broad repository scan and a focused deletion proof. It is
not a manual semantic review of all 7,276 files or a claim that all remaining
files are necessary forever.
