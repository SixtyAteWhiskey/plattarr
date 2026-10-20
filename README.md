# Plattarr

Plattarr is a personal homelab project that combines the functions of
Prowlarr, Radarr, Sonarr, and Lidarr in one Docker container, with one UI and
one database.

> **Status:** Phase 0. The image is Radarr `develop` with Plattarr branding.
> Movie management works as it does in Radarr. Indexer management (Prowlarr),
> TV (Sonarr), and music (Lidarr) are not merged yet.

## Modification notice

This program is a modified version of [Radarr](https://github.com/Radarr/Radarr),
forked from the Radarr `develop` branch at commit `14afe52` (2026-10-19).
It will also include modified code from
[Prowlarr](https://github.com/Prowlarr/Prowlarr),
[Sonarr](https://github.com/Sonarr/Sonarr), and
[Lidarr](https://github.com/Lidarr/Lidarr).

Changes by the Plattarr project, most recent first:

- **2026-10-20:** Renamed the product to Plattarr in the UI. Changed the
  default port from 7878 to 7880. Turned off error reporting to the upstream
  Radarr Sentry project and turned off upstream analytics.
- **2026-10-20:** Added a multi-arch container image build
  (`Dockerfile`, `docker/`, `.github/workflows/image.yml`) and a dev Compose
  stack (`deploy/dev/`). Removed the upstream GitHub bot workflows.

Plattarr is not affiliated with or supported by the Radarr, Prowlarr, Sonarr,
or Lidarr teams. Report Plattarr problems in this repository, not upstream.

## Run it

The image is published to GitHub Container Registry for `linux/amd64` and
`linux/arm64`:

```yaml
services:
  plattarr:
    image: ghcr.io/sixtyatewhiskey/plattarr:develop
    container_name: plattarr
    environment:
      - PUID=1000
      - PGID=1000
      - UMASK=002
      - TZ=America/Denver
    volumes:
      - ./config:/config
      - /mnt/data:/data
    ports:
      - "7880:7880"
    restart: unless-stopped
```

Mount your downloads and media as one `/data` volume so imports can use
hardlinks and atomic moves.

Don't point Plattarr at an existing Radarr `/config` folder. It runs database
migrations against whatever it finds there.

## License

Plattarr is licensed under the [GNU GPL v3](LICENSE), the same license as the
upstream projects. Upstream copyright notices are retained.

- Radarr: Copyright 2010-2025 the Radarr contributors
