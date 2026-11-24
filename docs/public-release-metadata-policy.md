# Public release metadata policy

The tracked repository may contain source, synthetic fixtures, generic deployment
examples, version and commit references, and aggregate performance measurements
from disposable test instances. It must not contain a maintainer's host name,
account name, private address, mount topology, exact library inventory, active
provider identifier, real media filename, credential, or private service URL.
Use placeholders in examples and keep production snapshots outside Git with
owner-only access.

The current-tree review generalized the development Compose example and the
owner-specific importer and indexer audit notes. The November release audit
also authorized a targeted rewrite of maintained branch history for known
deployment markers and the maintainer's personal commit email. A private,
verified recovery bundle preserves the original history. Routine release work
must not repeat a history rewrite without an explicit privacy reason: existing
commit links, tags, forks, and signatures depend on those IDs.

GitHub pull-request refs, cached commit views, forks, and existing clones are
outside the maintained branch rewrite. They can retain old metadata. Branch
cleanup is not a claim that every historical copy has been erased; service-side
retention needs GitHub's assistance where applicable. A leaked credential
must be revoked even if its text is removed.

## Historical exposure decision — 2026-11-10

The repository is already public. A targeted current-tree search of tracked
documentation, deployment examples, and workflow files found no maintainer
host name, home path, private address, or private snapshot marker from the
earlier review. Generic test network addresses and fixture data remain. The
earlier full reachable-history scan found no privileged token pattern in its
covered text, but old commits still contain superseded deployment details.

The owner initially accepted that residual exposure. The broader November 23
privacy cleanup superseded that decision for the maintained branch and editable
repository metadata. Neither decision permits publishing a credential or a
new private detail; investigate and revoke a real credential through its own
service if one is found.

Before a public release, run a tracked-tree search for host names, user names,
private network ranges, home and media paths, configured provider IDs, and
credential formats. Review matches in context: common words in synthetic
fixtures can be false positives. Keep the review output private when it
contains a real value.

## Ongoing checks

CI scans added commit history with a pinned, checksum-verified Gitleaks binary
before validation and image publication. Reports redact detected values.
Runtime configuration, databases, key files, and private Git bundles are
excluded from the Docker build context and local Git defaults. GitHub secret
scanning, push protection, and Dependabot security updates supplement these
checks; none replaces review of screenshots, fixtures, logs, or release notes.
