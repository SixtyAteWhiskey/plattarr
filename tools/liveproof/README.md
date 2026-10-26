# liveproof

Regression checks against a **running** Plattarr instance, over its HTTP API. Run them after every deploy to the dev stack or
against a scratch copy; never against a live *arr app or an instance whose data you care about. Python 3 standard library only,
so they run anywhere that can reach the instance. The repository holds no host names, addresses or keys: the target comes from
environment variables.

```bash
export PLATTARR_URL=http://127.0.0.1:7880                      # default
export PLATTARR_CONFIG_XML=<the instance's config dir>/config.xml   # or PLATTARR_API_KEY=<key>

python3 tools/liveproof/run.py smoke covers
python3 tools/liveproof/run.py baseline --expect-baseline 71,57,64
python3 tools/liveproof/run.py fingerprint
python3 tools/liveproof/run.py hygiene
python3 tools/liveproof/run.py hygiene-recovery --confirm-disposable
python3 tools/liveproof/run.py restore
```

Output is one line per check (`PASS`, `FAIL`, `WARN`, `INFO`); the exit status is 1 when anything failed. The key is read from the
environment or `config.xml`, sent only as the `X-Api-Key` header, and scrubbed from error text.

| Check | What it proves | Changes the instance |
|---|---|---|
| `smoke` | Version and migration are reported, the health page is empty (`--allow-health Source,Source` to tolerate some), and no error was logged since the process started. | No |
| `covers` | Every media kind keeps its covers in its own folder (`MediaCover/{id}` for movies, `Series/{id}`, `Artists/{id}`, `Albums/{id}`), no folder is shared by two kinds, and every image URL the API returns is served as an image (fetched through `/api/v3/mediacover/...`, the controller behind the same files; the static `/MediaCover/...` path answers an API key with the UI page). After an upgrade that moves a folder, files are missing until the item is refreshed: use `--allow-missing-covers` to see that as a warning. | No |
| `baseline` | `RssSync` completes, then the release search for one movie (`--baseline-movie`, default 1) returns the expected counts (`--expect-baseline total,approved,mapped`; without it the counts are only printed). The counts follow the indexers: when they move, run the previous image against the same query before blaming a change. | Runs a command |
| `defaults` | The shipped defaults (`allowFingerprinting` is `never`). **Only meaningful on an instance with an empty config**: an instance whose settings page was saved stores the values it showed. | No |
| `fingerprint` | `FpcalcCheck` warns when fingerprinting is on and `fpcalc` is missing, and clears when it is `never`. Creates a Music root folder (`LIVEPROOF_MUSIC_ROOT`, default `/testdata/music`, which must exist on the instance) if there is none, flips the setting, and restores everything. | Yes, journaled |
| `hygiene` | Confirms the global Hygiene action gate is off and the action journal contains no incomplete or failed actions. This is a read-only rollout check; review failed entries in the Hygiene page before clearing them. | No |
| `hygiene-recovery` | On an empty localhost scratch instance, creates a synthetic Movie, local Newznab/SABnzbd fixtures, and a one-strike rule; confirms the gate starts off, then enables it for one failed download and checks the journal, source-file retention, failed history, blocklist, and replacement search. The gate is reset to off in `finally`. | Yes, scratch instance only |
| `restore` | Replays the undo journal. | Undoes |

`hygiene-recovery` requires `--confirm-disposable`, a localhost `PLATTARR_URL`, and an empty scratch instance with the Hygiene action gate off. It uses only local HTTP fixtures for the indexer and download client; adding its Movie metadata uses the normal movie lookup. The proof leaves its completed journal, history, blocklist and temporary root in the disposable app so they can be inspected. Discard that app and the `/tmp/plattarr-liveproof-hygiene-*` directory when finished. It does not contact or change a production client or indexer.

## The walkthrough gallery

`gallery.py` opens every main page (per media kind where the page has the Movies / TV / Music switch) and the flows that are
hard to reach by hand, and saves one screenshot of each. `gallery_html.py` turns the run into one self-contained page.

```bash
GALLERY_BASE=http://127.0.0.1:7880 GALLERY_OUT=/tmp/gallery python3 tools/liveproof/gallery.py
python3 tools/liveproof/gallery_html.py /tmp/gallery /tmp/gallery/gallery.html
```

Point it at a **scratch** instance with External authentication, never at the live stack: the browser needs no sign-in there, and
the run must not change anything. It opens dialogs and never confirms one, and the queue page runs on mocked API answers. It needs
the `playwright` Python package and Chromium. Each shot records what went wrong while the page loaded:

| Finding | Meaning |
|---|---|
| `errors` | A console error or an uncaught exception. |
| `bad` | A failed API or page request (cover requests and the update feed are expected to fail on a bare instance). |
| `raw` | A text node that is a translation key shown instead of its text (the key is in `/api/v3/localization` and its text differs). |
| `dup` | The same section heading twice in a row, which means a section was drawn twice. |
| `maybe` | A single CamelCase word that is not a key: logger names on the Events page, or a missing translation. Read these by eye. |

The checks find what they know about. Open the screenshots too: the raw key, the doubled heading and the movie-only help text above
were all found by looking.

## Seerr contract replay

`seerr_contract.py` replays the Servarr requests used by Seerr 3.5.1's Radarr and Sonarr adapters: the shared status, profile,
root-folder, queue and tag calls; movie and series lookup, add, read, update and delete; Sonarr language profiles and episode
monitoring; and `RefreshMonitoredDownloads`. It sends the API key in the `apikey` query parameter, as Seerr does. The contract
is based on the pinned [Seerr 3.5.1 Servarr API sources](https://github.com/seerr-team/seerr/tree/v3.5.1/server/api/servarr).

The script refuses any target except `localhost:7881`. Start a fresh scratch instance from the code under review, copy its
`config.xml` path into `PLATTARR_SEERR_CONFIG_XML`, then run:

```bash
PLATTARR_SEERR_URL=http://127.0.0.1:7881 \
PLATTARR_SEERR_CONFIG_XML=/tmp/plattarr-seerr/config/config.xml \
python3 tools/liveproof/seerr_contract.py
```

It creates and removes one temporary movie and series, updates one temporary tag, and posts `RefreshMonitoredDownloads` for
both compatibility bases. The scratch copy must have download clients disabled; discard that copy after the replay.

## Indexer flag parity

`indexer_parity.py` compares Prowlarr's search response with Plattarr's
Movie-release response for the same query. Set `PROWLARR_URL`,
`PROWLARR_API_KEY`, `PLATTARR_URL`, and `PLATTARR_API_KEY` in the environment,
then run:

```bash
python3 tools/liveproof/indexer_parity.py --query "Sample Movie" --movie-id 1
```

It reads credentials only from the environment, does not save responses, and
never prints download URLs. Rows match on title, indexer name, and protocol;
unmatched rows are reported separately because results can change between
requests. A zero-match run is a failure, not evidence of parity. It compares
Prowlarr's named flags and torrent volume factors with Plattarr's mapped enum
flags. Do not run the request against a production instance if the search could
expose private release names in the terminal. For local, already-sanitized
snapshots, use `--prowlarr-json` and `--plattarr-json` instead.

The harness has also been exercised against local Prowlarr and Plattarr APIs
using the same synthetic Torznab feed. That checks the request and mapping path;
it does not stand in for a same-indexer comparison against real tracker results.
Change the mapper only after a sanitized same-indexer comparison shows a
divergence.

## Ombi contract replay

`ombi_contract.py` replays the Radarr v3 and Sonarr v3 calls used by Ombi's
AddOnly request flow. Its request paths, `X-Api-Key` authentication, and payloads
are pinned to the stable [Ombi v4.53.10 release](https://github.com/Ombi-app/Ombi/releases/tag/v4.53.10),
using its [RadarrV3Api](https://github.com/Ombi-app/Ombi/blob/v4.53.10/src/Ombi.Api.External/ExternalApis/Radarr/RadarrV3Api.cs),
[SonarrV3Api](https://github.com/Ombi-app/Ombi/blob/v4.53.10/src/Ombi.Api.External/ExternalApis/Sonarr/SonarrV3Api.cs),
[MovieSender](https://github.com/Ombi-app/Ombi/blob/v4.53.10/src/Ombi.Core/Senders/MovieSender.cs),
and [TvSender](https://github.com/Ombi-app/Ombi/blob/v4.53.10/src/Ombi.Core/Senders/TvSender.cs).

The replay covers connection status, profiles, root folders, language profiles,
tags, adding a movie and series, reading the new series and episodes, and
updating season and episode monitoring. Metadata lookups are setup calls made
by the harness to select valid disposable records; Ombi itself gets that
metadata from its request catalog. The script does not send search commands:
its request bodies match Ombi's AddOnly mode. It removes created media with
`deleteFiles=false` and then removes the temporary tags. It logs only route
and status, never API keys, response bodies, or filesystem paths.

This is a source-pinned replay, not a live Ombi packet capture. It refuses
every target except `http://localhost:7882` and requires an explicit
disposable-instance flag. Start a fresh Plattarr scratch instance on that port,
provide its `config.xml`, and run:

```bash
PLATTARR_OMBI_URL=http://127.0.0.1:7882 \
PLATTARR_OMBI_CONFIG_XML=/tmp/plattarr-ombi/config/config.xml \
python3 tools/liveproof/ombi_contract.py --confirm-disposable
```

Do not configure download clients or indexers on the scratch instance. A
passing replay checks Plattarr's request and response contract; it does not
establish compatibility with other Ombi releases or exercise the search-command
branch. Plattarr may fetch metadata and artwork for its public test records from
configured metadata providers. The replay does not contact indexers or download
clients.

## The undo journal

A proof writes each change to the journal (`LIVEPROOF_STATE`, default `./liveproof-state.json`, mode 600) **before** it edits a
resource (the original) or right after it creates one (the delete URL), and saves the file after every entry. `restore` replays it
in reverse and removes an entry only after it succeeded, so it is safe to run twice and it recovers a run that died half-way. A
check that changes something restores in a `finally` and also leaves the journal empty; if a journal is not empty at the start of
a run, the runner says so. Keep the journal file out of the repository: it can hold full resources.

## Next increments

The wider media flows (Movies, TV and Music: add, grab, import, failure, notifications, clean-up) are still recipes in the project
playbook. They move here one at a time with their own disposable listeners and journal coverage. The focused Hygiene recovery
fixture exercises the real Plattarr API, a local synthetic SABnzbd failure, failed history, blocklisting, and the existing
replacement search. Core fixtures exercise the action executor for Movies, TV, and Music, and the failed-download source routing
for each media kind. Private and unverified torrent releases remain manual-review items. `run.py hygiene` remains the read-only
idle check for an instance where the action gate is off.
