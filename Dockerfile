# syntax=docker/dockerfile:1.7
#
# Plattarr Phase 0 image: an unchanged Radarr `develop` build, packaged so the
# CI and image pipeline can be proven before any porting starts.
#
# The .NET and Node stages run on the build host's architecture and
# cross-compile for the target, so only the small runtime stage runs under QEMU.

# DOTNET_SDK_VERSION must match global.json. NODE_VERSION matches
# nodeVersion in azure-pipelines.yml.
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
RUN printf 'PackageVersion=%s\nPackageAuthor=Plattarr\nUpdateMethod=Docker\nBranch=develop\n' \
      "${PLATTARR_VERSION:-dev}" > /app/package_info

COPY docker/entrypoint.sh /entrypoint.sh
RUN chmod 0755 /entrypoint.sh

ENV PUID=1000 \
    PGID=1000 \
    UMASK=002 \
    TZ=Etc/UTC

VOLUME ["/config"]
EXPOSE 7878

HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
  CMD wget -qO- http://127.0.0.1:7878/ping || exit 1

ENTRYPOINT ["/entrypoint.sh"]
