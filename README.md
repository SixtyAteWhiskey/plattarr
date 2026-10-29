# Plattarr

Plattarr brings movie, TV, and music management together with indexer and
download-client management in one self-hosted application. It is built from
Radarr and incorporates code from Prowlarr, Sonarr, and Lidarr.

**Project status:** Movies, TV, Music, the shared media workflows, and the
Radarr/Sonarr compatibility routes used by Seerr are implemented. The
multi-architecture container is in production use and is still undergoing
acceptance testing. There is no stable release channel yet; the `develop` image
is a moving build.

## Run with Docker Compose

The image is published to GitHub Container Registry for `linux/amd64` and
`linux/arm64`:

```yaml
services:
  plattarr:
    image: ghcr.io/sixtyatewhiskey/plattarr:develop
    container_name: plattarr
    environment:
      PUID: 1000
      PGID: 1000
      UMASK: "002"
      TZ: America/Denver
    volumes:
      - ./config:/config
      - /mnt/data:/data
    ports:
      - "7880:7880"
    restart: unless-stopped
```

Keep downloads and media under the same `/data` mount so imports can use
hardlinks and atomic moves. Use a new `/config` directory: Plattarr has its own
database and migrations, so an existing Radarr, Sonarr, or Lidarr config must
not be reused.

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md) for local setup and verification
commands. Project-specific implementation notes are in
[`docs/module-interface.md`](docs/module-interface.md) and
[`docs/importer.md`](docs/importer.md). The `tools/liveproof` scripts exercise
API behavior against a local or scratch instance; do not point them at a
production service.

The current performance and code-cleanup work is tracked in the
[`refactoring plan`](docs/refactoring-plan.md).

## Upstream and license

Plattarr is not affiliated with or supported by the Radarr, Prowlarr, Sonarr,
or Lidarr teams. It is licensed under the [GNU GPL v3](LICENSE). Upstream
copyright notices are retained in the source.
