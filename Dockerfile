# syntax=docker/dockerfile:1.7
#
# Plattarr image. Phase 0: Radarr `develop` with Plattarr branding, port 7880,
# and upstream error reporting and analytics turned off.
#
# The .NET and Node stages run on the build host's architecture and
# cross-compile for the target, so only the small runtime stage runs under QEMU.

# Keep the SDK version aligned with global.json. The image workflow uses these
# same build arguments.
ARG DOTNET_SDK_VERSION=10.0.402
ARG NODE_VERSION=24
ARG ALPINE_VERSION=3.22

# ---------------------------------------------------------------------------
# Frontend: webpack writes to _output/UI
# ---------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM node:${NODE_VERSION}-alpine AS frontend
WORKDIR /src
COPY package.json yarn.lock .yarnrc tsconfig.json ./
RUN yarn install --frozen-lockfile --network-timeout 120000
COPY frontend/ frontend/
RUN yarn run build --env production

# ---------------------------------------------------------------------------
# Backend: self-contained publish for the target musl RID
# ---------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION} AS backend
ARG TARGETARCH
ARG PLATTARR_VERSION=""
WORKDIR /src
COPY . .

# build.sh reads RADARRVERSION to stamp AssemblyVersion. Empty keeps 10.0.0.*.
ENV RADARRVERSION=${PLATTARR_VERSION}

RUN case "${TARGETARCH}" in \
      amd64) echo linux-musl-x64   > /tmp/rid ;; \
      arm64) echo linux-musl-arm64 > /tmp/rid ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac

# --backend deletes _output/, so the UI is copied in afterward.
RUN ./build.sh --backend -f net10.0 -r "$(cat /tmp/rid)"
COPY --from=frontend /src/_output/UI _output/UI
RUN ./build.sh --packages -f net10.0 -r "$(cat /tmp/rid)" \
 && mkdir -p /out \
 && cp -r "_artifacts/$(cat /tmp/rid)/net10.0/Radarr" /out/bin

# ---------------------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------------------
FROM alpine:${ALPINE_VERSION}
ARG PLATTARR_VERSION=""

RUN apk add --no-cache \
      ca-certificates \
      icu-libs \
      shadow \
      sqlite-libs \
      su-exec \
      tzdata \
 && addgroup -g 1000 plattarr \
 && adduser -D -H -u 1000 -G plattarr -h /config plattarr

# Radarr reads ../package_info only when its folder is named "bin".
# UpdateMethod=Docker disables the in-app updater, which would otherwise
# replace this build with upstream Radarr.
COPY --from=backend /out/bin /app/bin

# ffprobe arrives from the NuGet package with owner-only execute, and the app
# runs as the unprivileged plattarr user. Without this every media info read
# fails with "Permission denied", which breaks sample detection and imports.
# Fail the image build if ffprobe is missing or cannot be run by an unprivileged
# user; media inspection and imports depend on it.
RUN find /app/bin -name ffprobe -exec chmod a+x {} + \
 && test -x /app/bin/ffprobe \
 && su-exec nobody /app/bin/ffprobe -version > /dev/null

RUN printf 'PackageVersion=%s\nPackageAuthor=Plattarr\nUpdateMethod=Docker\nBranch=develop\n' \
      "${PLATTARR_VERSION:-dev}" > /app/package_info

COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod 0755 /entrypoint.sh

ENV PUID=1000 \
    PGID=1000 \
    UMASK=002 \
    TZ=Etc/UTC

VOLUME ["/config"]
EXPOSE 7880

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD wget -qO- http://127.0.0.1:7880/ping || exit 1

ENTRYPOINT ["/entrypoint.sh"]
