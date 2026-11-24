---
title: Stop workflow event capture at the builder
author: SixtyAteWhiskey
date: 2026-11-24
branch: codex/final-release-20261123
id: l-4e401714
scope: repo
status: active
src: agent
touches:
  - ".github/workflows/image.yml"
  - ".github/workflows/registry-metadata.yml"
  - tools/registry_metadata.py
  - tools/tests/test_registry_metadata.py
---

## Decided
Disable Buildx's automatic GitHub Actions context capture at builder creation with provenance-add-gha=false because the builder copies the entire event into its custom provenance. Keep minimal image provenance, suppress detailed build-record uploads, and inspect published provenance because source scans do not inspect those independent artifacts.

## Tried and ruled out
A clean source history and private commit email did not remove metadata already copied into Docker build records or registry attestations. Relying only on the metadata file's default minimal mode was insufficient because the builder had already loaded the event context. Removing context at builder creation prevents that source of exposure.

## Watch out
Legacy cleanup must produce a digest-bound plan and privately preserve the referenced metadata because publication is independent of Git history. Replace tag indexes before deleting old metadata versions; otherwise existing tags can break. Preserve runtime descriptors and layers. Serialize maintenance with publication and refuse moved tags or changed plans. No raw event or production data belongs in public plans or logs.

In the code: .github/workflows/image.yml configures both builders and validates published provenance. .github/workflows/registry-metadata.yml and tools/registry_metadata.py provide reviewed metadata-only maintenance. tools/tests/test_registry_metadata.py covers preserved image contents, moved tags, and unsafe deletion plans.
