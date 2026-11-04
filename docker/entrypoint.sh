#!/bin/sh
# Plattarr container entrypoint.
# Applies PUID, PGID, and UMASK, then starts the app with /config as its data folder.
set -eu

PUID="${PUID:-1000}"
PGID="${PGID:-1000}"
UMASK="${UMASK:-002}"

umask "$UMASK"

if [ "$(id -u)" = "0" ]; then
    groupmod -o -g "$PGID" plattarr
    usermod -o -u "$PUID" plattarr

    # Top level only. A recursive chown on a large /config is slow, and /data is never touched.
    mkdir -p /config
    chown plattarr:plattarr /config

    exec su-exec plattarr:plattarr /app/bin/Radarr -nobrowser -data=/config -exitimmediately "$@"
fi

# Already running as a non-root user (for example, `user:` set in Compose).
exec /app/bin/Radarr -nobrowser -data=/config -exitimmediately "$@"
