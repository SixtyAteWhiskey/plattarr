---
title: Describe long blocked indexers without publishing their names
author: SixtyAteWhiskey
date: 2026-10-31
branch: codex/queue-error-audit-20261030
id: l-f0a54a5d
scope: repo
status: active
src: agent
touches:
  - src/NzbDrone.Api.Test/v3/Health/HealthResourceFixture.cs
  - src/NzbDrone.Core.Test/HealthCheck/Checks/IndexerLongTermStatusCheckFixture.cs
  - src/NzbDrone.Core.Test/Localization/LocalizationServiceFixture.cs
  - src/NzbDrone.Core/HealthCheck/Checks/IndexerLongTermStatusCheck.cs
  - src/NzbDrone.Core/Localization/Core/en.json
---

## Decided
For the partial long-term indexer health warning, report the observed blocked state and direct the owner to Indexers and local logs. Use a new localization key without provider-name tokens because the health message is returned by the shared API, and configured names can contain private details. Treat connectivity, credentials, and service trouble as possibilities, not proven causes.

In the code: `IndexerLongTermStatusCheck.Check()` uses `GetAvailableProviders()` joined to `GetBlockedProviders()` and an `InitialFailure` older than six hours. `HealthResourceMapper.ToResource()` copies the message into the health API.

## Tried and ruled out
Reusing the existing translated single-client template with a blank or sanitized `indexerNames` token was ruled out: translations can still print the token or repeat the old assertion, and stripping names loses the context needed for accurate redaction.

## Watch out
The blocked state is current, but the six-hour age belongs to the first recorded failure, not a proven uninterrupted outage. Do not infer a specific failed credential, network path, or provider from this check. Keep detailed identities in local settings and logs rather than the shared health response.
