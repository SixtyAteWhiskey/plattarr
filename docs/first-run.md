# Plattarr first-run guide

This guide traces one Movie or TV request from search to library refresh. Music
uses the same download and import path, with an Artist and Album instead of a
Movie or Series. The checks below use read-only API calls unless labeled
otherwise.

## Before connecting services

Keep Plattarr, your download client, and your media server on a private network.
Do not expose the application directly through a DMZ or public WAN port. Use a
VPN or a reverse proxy with TLS and authentication, and keep the API key private.
When services run in containers, mount the same download and library locations
into each service at compatible paths. A path Plattarr cannot read or a media
server cannot see will break imports or library scans.

Back up Plattarr before importing existing state or changing paths. In Settings
> General, set a stable URL base only when your proxy serves Plattarr below a
path prefix. In Settings > Media Management, confirm completed-download
handling, file permissions, and the rename options you want.
The built-in update page does not install Plattarr packages yet; use the
package manager, container registry, or release instructions for the way you
installed Plattarr.

## 1. Add one library path for each media kind

In Settings > Media Management, review the root folders used for Movies, TV,
and Music. In each section of the library, confirm the path is the location
mounted inside the Plattarr process. Add one test item and confirm it resolves
to that path before connecting a request app.

Read-only checks:

```bash
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/rootfolder"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/movie"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/rootfolder"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/lidarr/api/v1/rootfolder"
```

`PLATTARR_URL` should be the local service URL, including any configured URL
base. Use the API key as a request header for these checks.

## 2. Connect a download client and verify categories

Add your client in Settings > Download Clients. Use the address reachable from
Plattarr, not a browser-only hostname. Use the connection test before saving.
Keep a separate category for Movies, TV, and Music where the client supports
categories, and match the categories in the corresponding Plattarr module.
Avoid changing a category after a grab: Plattarr keeps a download's original
media kind when grab history links it to the queue.

Read-only checks:

```bash
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/downloadclient"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/queue?page=1&pageSize=20"
```

Check the returned client name and queue state. Do not use a queue-removal call
as a diagnostic; it can change a client job or its files.

## 3. Add indexers and check one search

Add indexers in Settings > Indexers. Test each indexer and set the categories
appropriate to Movies, TV, and Music. Enable RSS, automatic search, or
interactive search according to how you use it. Configure Prowlarr-compatible
clients with the indexer's Torznab or Newznab feed URL. Plattarr serves those
feeds at `/{indexerId}/api`; the Prowlarr management API (`/api/v1/indexer`)
is not implemented.

Check a feed's capabilities and categories without grabbing anything:

```bash
curl -fsS "$PLATTARR_URL/$INDEXER_ID/api?t=caps&apikey=$PLATTARR_API_KEY"
curl -fsS "$PLATTARR_URL/$INDEXER_ID/api?t=search&q=Sample&apikey=$PLATTARR_API_KEY"
```

Treat the API key in a URL as a secret. Do not paste full feed URLs, search
responses, or download URLs into public logs or issue reports. The indexer
parity check below compares only titles, indexer names, protocol, and flag
attributes.

Read-only checks in the UI: run an interactive search for a test Movie, Series,
or Album, open a result's details, and confirm the indexer, protocol, category,
seeders where available, and approval/rejection reason. A missing result means
the query returned nothing; it is different from a result that was rejected by
a profile or release rule.

## 4. Add a test request and inspect the release

Add a test Movie or Series from the Plattarr library UI, or connect Jellyseerr
to the compatibility API described below. Select the quality and metadata
profiles, root folder, and monitoring options that fit the library. Run an
interactive search and review why each candidate passed or failed before
grabbing it.

Read-only checks:

```bash
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/movie?term=Sample"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/series?term=Sample"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/history?page=1&pageSize=20"
```

The History call shows grabs and imports. A blocked release remains visible as
a rejected candidate; a missing search result has no candidate to inspect.

## 5. Follow a download through queue and import

After a grab, confirm the client receives the expected category and save path.
The Queue view shows download progress and import state. Once the client reports
completion, Plattarr scans the completed-download path, matches the file to the
requested media, imports it, and applies the configured rename rules. The
configured Emby or Jellyfin notification can trigger a library refresh after
import. Plex can scan the same library path on its normal scan schedule; this
guide does not claim a Plattarr Plex library-refresh integration.

Read-only checks:

```bash
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/queue?page=1&pageSize=20"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/history?page=1&pageSize=50"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/api/v3/moviefile"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/episodefile"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/lidarr/api/v1/trackfile"
```

The queue should clear after a successful import. History should show the grab
and import, and the appropriate file endpoint should return the imported file.
In Emby, Jellyfin, or Plex, confirm that the corresponding library points at
the same mounted library path and that the item is visible after a scan.

### Tell these failures apart

| What you see | Meaning | Safe next check |
|---|---|---|
| Search returns no candidates | The indexer did not return a result for that query/category, or it is unavailable. | Check the indexer's connection and categories, then retry the interactive search. |
| Candidate is rejected or blocklisted | A result exists, but a quality, custom-format, delay, or blocklist rule rejected it. | Open the candidate details and read its rejection reason; adjust only the intended profile/rule. |
| Download client reports failure | The client accepted a grab but reports a failed job or unavailable payload. | Inspect the client's message and Plattarr History; use the normal remove/blocklist controls only after confirming the release. |
| Import is blocked | The client completed, but Plattarr could not match or import one or more files. | Read the queue's import message and use Manual Import to inspect the mapping before changing files. |

An import failure does not mean the file is safe to delete. Confirm the client
job, the files on disk, and the media mapping before cleanup. Hygiene actions
are gated separately and default off. A Movie recovery path has passed in a
disposable scratch instance; enabling actions on a shared instance remains a
separate rollout decision.

## 6. Connect media servers

### Jellyfin

Install/configure Jellyfin to read the same library paths Plattarr writes. In
Plattarr's Settings > Connect, add the Emby/Jellyfin notification, use the
Jellyfin server URL and API key, select the library refresh events, and run the
provider test. The current provider uses the MediaBrowser protocol for
Jellyfin/Emby library refresh and notification events.

### Emby

Use the same path and API-key setup as Jellyfin. Add the Emby/Jellyfin
notification provider, test the connection, and enable the refresh events you
want. The server must be able to see the imported paths as its own library.

### Plex

Configure Plex libraries to point to the same media paths. Use Plex's own
scheduled or manual library scan to verify visibility after import. Plattarr
does not currently provide a native Plex library-refresh connection.

## 7. Connect Jellyseerr or Ombi

### Jellyseerr

In Jellyseerr, add Plattarr as a Radarr server for Movies and/or a Sonarr server
for TV. Use Plattarr's reachable URL and URL base `/radarr` or `/sonarr`, and
the Plattarr API key. For example, the read-only compatibility routes are:

```bash
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/radarr/api/v3/system/status"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/radarr/api/v3/qualityprofile"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/radarr/api/v3/rootfolder"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/system/status"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/qualityprofile"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/rootfolder"
```

Seerr's Radarr and Sonarr adapters send the API key as an `apikey` query
parameter. Both URL bases accept that placement as well as the standard API
key header. The read-only calls above prove connectivity and list the profile
and root-folder choices. `tools/liveproof/seerr_contract.py` replays the
adapter's read and mutation contract only against a disposable instance on
`localhost:7881`; see [liveproof](../tools/liveproof/README.md) before running
it.

### Ombi

Ombi v4.53.10 sent AddOnly movie and one-episode TV requests successfully to
the `/radarr` and `/sonarr` API bases of a disposable Plattarr instance. The
actual methods and payload shapes were captured without credentials and added
to the [contract replay](../tools/liveproof/README.md#ombi-contract-replay).
This verifies that specific request flow. Test a shared installation's own
profiles, root paths, and tags before connecting Ombi there; 4K and
search-on-add requests have not been checked.

## 8. Compare indexer flags across Prowlarr and Plattarr

For a useful comparison, search for the same Movie and query in Prowlarr and
Plattarr close together in time. Set API keys in the environment so they do
not appear in shell history or output:

```bash
export PROWLARR_URL=http://127.0.0.1:9696
export PROWLARR_API_KEY=<Prowlarr key>
export PLATTARR_URL=http://127.0.0.1:7880
export PLATTARR_API_KEY=<Plattarr key>
python3 tools/liveproof/indexer_parity.py --query "Sample Movie" --movie-id 1
```

The script reads both responses in memory, prints no API keys or release URLs,
and compares matched rows by title, indexer, and protocol. It reports the raw
Prowlarr named flags and volume factors beside Plattarr's mapped flags. A
mismatch is evidence to investigate in `IndexerFlagsMapper`; unmatched rows
are reported separately because results can change between searches. Never
save unsanitized response JSON in the repository: Prowlarr download URLs may
contain credentials.

For an offline comparison, pass two JSON arrays containing only `title`,
`indexer`, `protocol`, `indexerFlags`, and where returned by Prowlarr,
`downloadVolumeFactor`, `uploadVolumeFactor`, and `scene`:

```bash
python3 tools/liveproof/indexer_parity.py \
  --prowlarr-json /tmp/prowlarr-sanitized.json \
  --plattarr-json /tmp/plattarr-sanitized.json
```

## First-run checklist

- The Plattarr root folders point to paths visible in the container.
- One download client tests successfully, with the intended categories.
- One indexer tests successfully and returns the test title in an interactive search.
- A test release is reviewed and grabbed; the queue shows its client and category.
- History records the grab and import, and the media server sees the imported file.
- Jellyseerr's Radarr/Sonarr read endpoints work, or its request flow is replayed in scratch.
- No service is exposed through a direct DMZ or public-WAN port.
