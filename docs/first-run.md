# Plattarr first-run guide

This guide traces one Movie or TV request from search to library refresh. Music
uses the same download and import path, with an Artist and Album instead of a
Movie or Series. The checks below use read-only API calls unless labeled
otherwise.

## Request-to-library lifecycle

1. **Create the request.** Add a Movie or TV show in Plattarr, or submit one
   through a configured request app. Choose its root folder, profile, and
   monitoring options. Music uses an Artist and Album.
2. **Find and review a release.** An interactive search lists returned
   candidates and their approval or rejection reasons. Automatic search can
   choose a candidate without this review step. No candidates means the query
   returned no releases; a rejected candidate was returned but did not pass the
   configured rules.
3. **Grab and download.** When a release is grabbed, Plattarr sends it to the
   configured download client with the media kind's category. Follow progress
   and import state in **Activity > Queue**.
4. **Import the completed files.** After the client reports completion,
   Plattarr matches the files to the requested item and applies the configured
   import and rename rules. If the queue reports an import warning, inspect the
   warning and use Manual Import to review the file mapping before changing
   files.
5. **Confirm library placement.** **Activity > History** records the grab and
   import. The item's detail page and file list show whether the imported file
   is in the expected library path.
6. **Refresh connected libraries.** Configured Emby/Jellyfin notifications can
   trigger a library refresh after import. Plex uses its own scheduled or
   manual scan. A request app may need to sync with its media server before it
   displays the item as available; check that app after the library scan.

The setup sections below connect the paths, client, indexer, media server, and
request app used by this flow. Request-app compatibility depends on the tested
routes and request modes documented for that app.

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
interactive search according to how you use it. Plattarr uses these indexers
internally but does not currently expose Prowlarr-compatible per-indexer
Torznab or Newznab feeds. The planned `/{indexerId}/api` feed route and the
Prowlarr management API (`/api/v1/indexer`) are not implemented. Do not point
external clients at either route yet.

Treat indexer API keys and download URLs as secrets. Do not paste them, full
search responses, or feed URLs into public logs or issue reports. The indexer
parity check below compares only titles, indexer names, protocol, and flag
attributes.

Read-only checks in the UI: run an interactive search for a test Movie, Series,
or Album, open a result's details, and confirm the indexer, protocol, category,
seeders where available, and approval/rejection reason. A missing result means
the query returned nothing; it is different from a result that was rejected by
a profile or release rule.

## 4. Add a test request and inspect the release

Add a test Movie or Series from the Plattarr library UI, or connect Seerr
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
| Remote path mapping health warning | The client reports a completed-download path Plattarr cannot access. A download category may have its own save path. | Compare the client's reported path with the container mounts and Plattarr's remote-path mappings. Correct the mount, mapping, or category save path; confirm the directory exists before retrying an import. Do not move or delete files as a diagnostic. |

An import failure does not mean the file is safe to delete. Confirm the client
job, the files on disk, and the media mapping before cleanup. Hygiene actions
are gated separately and default off. A Movie recovery path has passed in a
disposable scratch instance; enabling actions on a shared instance remains a
separate rollout decision.

## 6. Connect media servers

### Jellyfin and Emby

1. Create libraries in the media server pointing to the files Plattarr imports.
   The server and Plattarr can use different paths for the same files, but both
   must be able to access them.
2. In **Settings > Connect**, add **Emby / Jellyfin**. Enter the host and port
   as reachable from Plattarr, enable SSL if appropriate, enter the server API
   key, and set the advanced URL Base if the server uses one.
3. Enable **Update Library** and the **On File Import** event. Select other
   events, such as rename, if you want them to trigger updates too. If the
   server sees a different library path, set the advanced map-from and map-to
   paths to the Plattarr and media-server paths respectively.
4. For Jellyfin, turn off **Send Notifications**, which it does not support;
   library updates are a separate setting. Test and save the connection. After
   an import, check that the item appears in the
   server library. A successful connection test alone does not prove an import
   or library update.

### Plex

Configure Plex libraries to read the imported files, and use Plex's scheduled
or manual library scan. Confirm that the item appears after the scan. Plattarr
does not currently provide a native Plex library-refresh connection.

## 7. Connect Seerr (formerly Jellyseerr) or Ombi

1. In Plattarr, prepare a Movie root folder and quality profile, and a TV root
   folder and profile if you want TV requests. Find the Plattarr API key and
   configured URL Base in **Settings > General**. Keep the key private.
2. In the request app, add a Radarr server for Movies and a Sonarr server for
   TV. Use the Plattarr host and port reachable from that app, the Plattarr
   API key, and URL bases `/radarr` and `/sonarr` respectively. If Plattarr
   itself uses a URL base, put it first: for example, `/plattarr/radarr` and
   `/plattarr/sonarr`. Configure SSL to match the connection.
3. Test each connection, then select the appropriate root folder, quality
   profile, monitoring and any other request options in the request app. A
   connection test and profile list do not prove a request, download, or import.
   Verify the intended flow on a disposable setup before relying on it.

These paths expose Plattarr's Radarr- and Sonarr-compatible APIs; they are not
separate Radarr or Sonarr installations. They are independent of the unimplemented
Prowlarr management API and per-indexer Torznab/Newznab feeds. The following
read-only checks test the compatibility endpoints and list available choices:

```bash
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/radarr/api/v3/system/status"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/radarr/api/v3/qualityprofile"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/radarr/api/v3/rootfolder"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/system/status"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/qualityprofile"
curl -fsS -H "X-Api-Key: $PLATTARR_API_KEY" "$PLATTARR_URL/sonarr/api/v3/rootfolder"
```

`PLATTARR_URL` includes Plattarr's own URL base, if configured. Seerr sends the
API key as an `apikey` query parameter; the compatibility routes also accept it
in the header as shown above. Avoid exposing URLs containing keys in logs.

### Compatibility evidence and limits

A disposable replay of the Seerr 3.5.1 Radarr and Sonarr adapter contract
covered profile and root-folder reads, movie and TV lookup, add, update and
delete, episode monitoring, and download refresh. It was a contract replay,
not a complete Seerr-to-library download. Other versions and request modes
need separate verification. See the [Seerr replay](../tools/liveproof/README.md#seerr-contract-replay).

A disposable Ombi 4.53.10 instance sent an AddOnly movie request and a TV
request for one episode successfully to those compatibility bases. The
[sanitized capture and replay](../tools/liveproof/README.md#ombi-contract-replay)
do not establish compatibility for other versions, 4K, search-on-add, or a
production configuration. Neither proof establishes a real download or import.

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
- Seerr's Radarr/Sonarr read endpoints work, or its request flow is replayed in scratch.
- No service is exposed through a direct DMZ or public-WAN port.
