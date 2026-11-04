# Release download request policy

Release download requests require HTTP or HTTPS and a public destination.
Private, loopback, link-local, shared-address, multicast, and reserved address
ranges are rejected. The check runs before each direct request and redirect.
Direct connections use a separate connection pool and check the resolved
addresses again at connection time, so a DNS change between preflight and
connection cannot select a private address. The common indexer download path,
its native preliminary requests, and direct torrent/NZB downloads use this
policy. Magnet links remain local client input and do not make an HTTP request.

The configured HTTP, SOCKS, or FlareSolverr indexer proxy is treated as a
trusted network boundary. Plattarr rejects non-HTTP URLs, localhost names, and
private literal addresses before handing the request to that proxy. It does
not resolve other proxy-bound names locally and cannot verify the proxy's DNS
choice or redirect target. The owner selected this compatibility behavior for existing
proxy-backed indexers. Do not configure an untrusted proxy or let it reach
private services if indexer responses can be hostile.

Decoded indexer feed and authentication responses are capped at 16 MiB;
release-file responses are capped at 32 MiB, including chunked bodies without
a declared length. A body over the cap fails before it can be fully buffered.
These are per-response limits, not a total transfer budget across retries or
redirects. Source-side request timeouts and the existing redirect limits still
apply.
