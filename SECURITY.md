# Security

Keep Plattarr on a private network. Use a VPN or an authenticating TLS proxy
for remote access; do not expose it directly to a public WAN or DMZ. Follow
the [first-run authentication guide](docs/first-run.md).

Treat the API key and authenticated UI as administrative access. Keep runtime
configuration, databases, backups, and download-client credentials outside Git.
Never include them in screenshots, issue bodies, or public logs. Password
hashes and salts are sensitive even though they are not plaintext passwords.

Release downloads may reach a private indexer only at its explicitly configured
origin (scheme, hostname, and port). Redirects elsewhere retain public-address
checks. Configure only indexer origins you trust; feed-provided URLs do not
grant access to other private services.

Report a vulnerability through
[GitHub's private vulnerability reporting](https://github.com/SixtyAteWhiskey/plattarr/security/advisories/new).
Include the affected image revision, steps using synthetic data, and the
security impact. Do not publish a working credential or private media details.

Container users should update to a reviewed image revision. The built-in
updater does not install Plattarr container releases. Keep a verified database
backup and the prior image before upgrading; consult the release notes before
rolling back a database migration.

Repository metadata handling and scan limits are documented in the
[public metadata policy](docs/public-release-metadata-policy.md).
