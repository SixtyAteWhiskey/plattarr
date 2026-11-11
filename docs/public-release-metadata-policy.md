# Public release metadata policy

The tracked repository may contain source, synthetic fixtures, generic deployment
examples, version and commit references, and aggregate performance measurements
from disposable test instances. It must not contain a maintainer's host name,
account name, private address, mount topology, exact library inventory, active
provider identifier, real media filename, credential, or private service URL.
Use placeholders in examples and keep production snapshots outside Git with
owner-only access.

The current-tree review generalized the development Compose example and the
owner-specific importer and indexer audit notes. It did not rewrite published
Git history. Older commits remain retrievable and may contain superseded
deployment details; deleting text in a new commit does not erase them. Review
that residual exposure separately before calling the repository ready for a
stable public release. Do not force-push a history rewrite as part of routine
release work because existing commit links, tags, forks, and signatures depend
on those IDs.

## Historical exposure decision — 2026-11-10

The repository is already public. A targeted current-tree search of tracked
documentation, deployment examples, and workflow files found no maintainer
host name, home path, private address, or private snapshot marker from the
earlier review. Generic test network addresses and fixture data remain. The
earlier full reachable-history scan found no privileged token pattern in its
covered text, but old commits still contain superseded deployment details.

The owner reviewed that residual and chose to accept the documented historical
exposure for this release. Do not rewrite published Git history for that
material. This acceptance does not apply to any credential or new private
detail discovered later; investigate and revoke a real credential through its
own service if one is found.

Before a public release, run a tracked-tree search for host names, user names,
private network ranges, home and media paths, configured provider IDs, and
credential formats. Review matches in context: common words in synthetic
fixtures can be false positives. Keep the review output private when it
contains a real value.
