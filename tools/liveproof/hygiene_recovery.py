#!/usr/bin/env python3
"""Exercise one failed download through Hygiene on an empty localhost scratch instance."""
import json
import tempfile
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlsplit

from client import ApiError, scrub


FAILED_RELEASE_TITLE = "The.Matrix.1999.1080p.WEB-DL.x264-F7-FAILED"
REPLACEMENT_RELEASE_TITLE = "The.Matrix.1999.1080p.WEB-DL.x264-F7-REPLACEMENT"

CAPS = b'''<?xml version="1.0" encoding="UTF-8"?>
<caps><server version="1.0" title="Disposable liveproof"/><limits max="1000" default="1000"/>
<searching><search available="yes" supportedParams="q imdbid tmdbid"/></searching>
<categories><category id="2000" name="Movies"><subcat id="2040" name="HD"/></category></categories></caps>'''
NZB = b'''<?xml version="1.0" encoding="UTF-8"?>
<nzb xmlns="http://www.newzbin.com/DTD/2003/nzb"><head/><file poster="fixture" date="1" subject="fixture">
<groups><group>alt.test</group></groups><segments><segment bytes="1" number="1">fixture</segment></segments></file></nzb>'''


class FixtureState:
    def __init__(self, downloads_root):
        self.downloads_root = Path(downloads_root)
        self.downloads_root.mkdir(parents=True, exist_ok=True)
        self.jobs = []
        self.replacement_jobs = []
        self.removed = []
        self.files = []
        self.search_requests = 0
        self.phase = "failed"
        self.lock = threading.Lock()

    def add_failed_job(self, category, title):
        with self.lock:
            number = len(self.files) + 1
            job_id = f"LIVEPROOF-{number}"
            job_dir = self.downloads_root / job_id
            job_dir.mkdir(parents=True, exist_ok=True)
            source = job_dir / "retained-source-data.bin"
            source.write_bytes(b"Disposable Hygiene recovery proof")
            self.files.append(str(source))
            self.jobs.append({
                "fail_message": "synthetic fixture failure",
                "bytes": 1073741824,
                "category": category,
                "nzb_name": title,
                "download_time": 1,
                "storage": str(job_dir),
                "status": "Failed",
                "nzo_id": job_id,
                "name": title,
            })
            return job_id

    def add_replacement_job(self, category):
        with self.lock:
            job_id = f"LIVEPROOF-REPLACEMENT-{len(self.replacement_jobs) + 1}"
            self.replacement_jobs.append({
                "status": "Downloading",
                "index": len(self.replacement_jobs),
                "timeleft": "0:00:00",
                "mb": 1024,
                "filename": REPLACEMENT_RELEASE_TITLE,
                "priority": "Normal",
                "cat": category,
                "mbleft": 1024,
                "percentage": 0,
                "nzo_id": job_id,
            })
            return job_id

    def switch_category(self, job_id, category):
        with self.lock:
            job = next((item for item in self.jobs if item["nzo_id"] == job_id), None)
            if job is None:
                return None
            job["category"] = category
            return dict(job)

    def set_phase(self, phase):
        with self.lock:
            self.phase = phase

    def snapshot(self):
        with self.lock:
            return {
                "jobs": [dict(job) for job in self.jobs],
                "replacementJobs": [dict(job) for job in self.replacement_jobs],
                "removed": [dict(job) for job in self.removed],
                "files": list(self.files),
                "filesPresent": [Path(path).is_file() for path in self.files],
                "searchRequests": self.search_requests,
                "phase": self.phase,
            }


class DisposableServices:
    def __init__(self, downloads_root):
        self.state = FixtureState(downloads_root)
        state = self.state

        class Handler(BaseHTTPRequestHandler):
            def send_body(self, payload, content_type):
                self.send_response(200)
                self.send_header("Content-Type", content_type)
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)

            def send_json(self, payload):
                self.send_body(json.dumps(payload).encode("utf-8"), "application/json")

            def do_GET(self):
                parsed = urlsplit(self.path)
                query = parse_qs(parsed.query)

                if parsed.path == "/newznab/api":
                    if query.get("t", [""])[0] == "caps":
                        return self.send_body(CAPS, "application/xml; charset=utf-8")
                    with state.lock:
                        state.search_requests += 1
                        release_title = (FAILED_RELEASE_TITLE if state.phase == "failed"
                                         else REPLACEMENT_RELEASE_TITLE)
                        release_guid = ("liveproof-failed" if state.phase == "failed"
                                        else "liveproof-replacement")
                        release_file = ("failed.nzb" if state.phase == "failed"
                                        else "replacement.nzb")
                    rss = f'''<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0"><channel><title>Disposable Newznab</title><description>Synthetic release</description>
<link>http://127.0.0.1:{self.server.server_port}</link>
<item><title>{release_title}</title><guid isPermaLink="false">{release_guid}</guid>
<link>http://127.0.0.1:{self.server.server_port}/{release_file}</link>
<comments>http://127.0.0.1:{self.server.server_port}/details?id={release_guid}</comments>
<pubDate>Tue, 20 Oct 2026 03:00:00 GMT</pubDate><enclosure url="http://127.0.0.1:{self.server.server_port}/release.nzb" length="512" type="application/x-nzb"/>
<category>2000</category><size>1073741824</size><attr name="tmdbid" value="603"/></item></channel></rss>'''.encode("utf-8")
                    return self.send_body(rss, "application/xml; charset=utf-8")

                if parsed.path in ("/release.nzb", "/failed.nzb", "/replacement.nzb"):
                    return self.send_body(NZB, "application/x-nzb")

                if parsed.path == "/sab/api":
                    mode = query.get("mode", [""])[0]
                    if mode == "version":
                        return self.send_json({"version": "4.5.0"})
                    if mode == "queue":
                        if query.get("name", [""])[0] == "delete":
                            return self.send_json({"status": True})
                        with state.lock:
                            slots = [dict(job) for job in state.replacement_jobs]
                        return self.send_json({"queue": {"paused": False, "slots": slots, "my_home": str(state.downloads_root)}})
                    if mode == "history":
                        if query.get("name", [""])[0] == "delete":
                            job_id = query.get("value", [""])[0]
                            with state.lock:
                                job = next((item for item in state.jobs if item["nzo_id"] == job_id), None)
                                state.removed.append({
                                    "id": job_id,
                                    "deleteFiles": query.get("del_files", [""])[0],
                                    "archive": query.get("archive", [""])[0],
                                })
                                if job:
                                    state.jobs.remove(job)
                            return self.send_json({"status": True})
                        with state.lock:
                            jobs = [dict(job) for job in state.jobs]
                        return self.send_json({"history": {"paused": False, "slots": jobs}})
                    if mode == "get_config":
                        category_paths = {
                            "plattarr-liveproof-movies": "movies",
                            "plattarr-liveproof-tv": "tv",
                            "plattarr-liveproof-music": "music",
                        }
                        for relative_path in category_paths.values():
                            (state.downloads_root / relative_path).mkdir(parents=True, exist_ok=True)
                        config = {
                            "misc": {
                                "complete_dir": str(state.downloads_root),
                                "enable_tv_sorting": False,
                                "enable_movie_sorting": False,
                                "enable_date_sorting": False,
                                "tv_categories": [],
                                "movie_categories": [],
                                "date_categories": [],
                            },
                            "categories": [
                                {"name": name, "dir": relative_path}
                                for name, relative_path in category_paths.items()
                            ],
                            "servers": [], "sorters": [],
                        }
                        return self.send_json({"config": config})

                if parsed.path == "/liveproof/state":
                    return self.send_json(state.snapshot())

                self.send_body(b"not found", "text/plain")

            def do_POST(self):
                parsed = urlsplit(self.path)
                query = parse_qs(parsed.query)
                if parsed.path == "/sab/api" and query.get("mode", [""])[0] == "addfile":
                    length = int(self.headers.get("Content-Length", "0"))
                    if length:
                        self.rfile.read(length)
                    category = query.get("cat", ["plattarr-liveproof-movies"])[0]
                    if state.snapshot()["phase"] == "failed":
                        job_id = state.add_failed_job(category, FAILED_RELEASE_TITLE)
                    else:
                        job_id = state.add_replacement_job(category)
                    return self.send_json({"status": True, "nzo_ids": [job_id]})

                self.send_json({"status": True})

            def log_message(self, *_):
                pass

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.server.daemon_threads = True
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)

    @property
    def base_url(self):
        return f"http://127.0.0.1:{self.server.server_port}"

    def start(self):
        self.thread.start()

    def close(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join(timeout=2)


class ProofError(RuntimeError):
    pass


def _require(report, condition, name, detail):
    report.check(condition, name, detail)
    if not condition:
        raise ProofError(name)


def _get_actions(api):
    return api.get("/api/v3/hygiene/action?limit=500")


def _set_gate(api, enabled):
    api.put("/api/v3/hygiene/config/1", {"id": 1, "actionsEnabled": enabled})


def run(api, report, confirmed=False):
    """Verify category-switched failure recovery and replacement on an empty scratch app."""
    parsed = urlsplit(api.base)
    loopback = parsed.hostname in ("127.0.0.1", "localhost", "::1")
    if not confirmed or not loopback:
        report.fail("hygiene-recovery.safety", "requires --confirm-disposable and a localhost PLATTARR_URL")
        return

    services = None
    gate_owned = False
    try:
        config = api.get("/api/v3/hygiene/config")
        existing = {
            "movies": api.get("/api/v3/movie"),
            "indexers": api.get("/api/v3/indexer"),
            "downloadClients": api.get("/api/v3/downloadclient"),
            "rootFolders": api.get("/api/v3/rootfolder"),
            "actions": _get_actions(api),
        }
        nonempty = [f"{name}={len(values)}" for name, values in existing.items() if values]
        _require(report, not nonempty and config.get("actionsEnabled") is False,
                 "hygiene-recovery.empty_scratch",
                 "empty app, no journal entries, and action gate off" if not nonempty and config.get("actionsEnabled") is False
                 else "refused a populated app or an already-open action gate")

        scratch = Path(tempfile.mkdtemp(prefix="plattarr-liveproof-hygiene-"))
        movie_root = scratch / "movies"
        movie_root.mkdir()
        downloads_root = scratch / "downloads"
        services = DisposableServices(downloads_root)
        services.start()
        _require(report, api.post("/api/v3/rootfolder", {"path": str(movie_root)}) is not None,
                 "hygiene-recovery.root", "temporary media root created")

        indexer = next((item for item in api.get("/api/v3/indexer/schema")
                        if item.get("implementation") == "Newznab" and item.get("name") == "Generic Newznab"), None)
        _require(report, indexer is not None, "hygiene-recovery.newznab_schema", "generic Newznab schema available")
        indexer["name"] = "Liveproof Newznab"
        indexer["enable"] = True
        for field in indexer.get("fields", []):
            if field["name"] == "baseUrl":
                field["value"] = services.base_url
            elif field["name"] == "apiPath":
                field["value"] = "/newznab/api"
            elif field["name"] == "apiKey":
                field["value"] = "liveproof-fixture-key"
        created_indexer = api.post("/api/v3/indexer", indexer)
        _require(report, created_indexer.get("id", 0) > 0,
                 "hygiene-recovery.newznab", "synthetic local indexer configured")

        client = next((item for item in api.get("/api/v3/downloadclient/schema")
                       if item.get("implementation") == "Sabnzbd"), None)
        _require(report, client is not None, "hygiene-recovery.sab_schema", "SABnzbd schema available")
        client["name"] = "Liveproof SAB"
        client["enable"] = True
        for field in client.get("fields", []):
            name = field.get("name")
            if name == "host":
                field["value"] = "127.0.0.1"
            elif name == "port":
                field["value"] = services.server.server_port
            elif name == "apiKey":
                field["value"] = "liveproof-fixture-key"
            elif name == "urlBase":
                field["value"] = "sab"
            elif name == "movieCategory":
                field["value"] = "plattarr-liveproof-movies"
            elif name == "tvCategory":
                field["value"] = "plattarr-liveproof-tv"
            elif name == "musicCategory":
                field["value"] = "plattarr-liveproof-music"
        created_client = api.post("/api/v3/downloadclient?forceSave=true", client)
        _require(report, created_client.get("id", 0) > 0 and created_client.get("enable") is True,
                 "hygiene-recovery.sab", "synthetic local download client enabled")

        profiles = api.get("/api/v3/qualityprofile")
        quality_profile = next((item["id"] for item in profiles if item.get("name") == "Any"), profiles[0]["id"])
        lookup = api.get("/api/v3/movie/lookup?term=The%20Matrix")
        movie = next((item for item in lookup if item.get("tmdbId") == 603), None)
        _require(report, movie is not None, "hygiene-recovery.movie_metadata", "scratch movie metadata found")
        movie.update({
            "qualityProfileId": quality_profile,
            "rootFolderPath": str(movie_root),
            "monitored": True,
            "addOptions": {"searchForMovie": False},
        })
        added_movie = api.post("/api/v3/movie", movie)
        movie_id = added_movie["id"]

        rule = api.post("/api/v3/hygiene/rule", {
            "reasonCode": "client-failed",
            "mediaKind": 1,
            "downloadClientId": created_client["id"],
            "enabled": True,
            "strikeLimit": 1,
            "strikeIntervalMinutes": 1,
        })
        _require(report, rule.get("id", 0) > 0, "hygiene-recovery.rule", "one-strike Movie/client rule created")

        empty_refresh = api.run_command("RefreshMonitoredDownloads", seconds=90)
        _require(report, empty_refresh.get("status") == "completed" and not _get_actions(api),
                 "hygiene-recovery.gate-off", "the default-off gate produces no actions on an empty queue")

        release_rows = api.get(f"/api/v3/release?movieId={movie_id}")
        release = next((item for item in release_rows
                        if item.get("indexer") == "Liveproof Newznab" and item.get("protocol") == "usenet"), None)
        _require(report, release is not None and release.get("approved") is True,
                 "hygiene-recovery.synthetic_release", "approved local synthetic release found")
        baseline_searches = services.state.snapshot()["searchRequests"]
        api.post("/api/v3/release", release)

        failed_job = services.state.snapshot()["jobs"][-1]
        history_deadline = time.time() + 30
        grabbed = False
        while time.time() < history_deadline:
            page = api.get("/api/v3/history?page=1&pageSize=100&sortKey=date&sortDirection=descending")
            grabbed = any(item.get("downloadId") == failed_job["nzo_id"] and
                          item.get("eventType", "").lower() == "grabbed" for item in page.get("records", []))
            if grabbed:
                break
            time.sleep(0.5)
        _require(report, grabbed, "hygiene-recovery.grab-history", "Movie grab history is recorded before the category switch")

        switched = services.state.switch_category(failed_job["nzo_id"], "plattarr-liveproof-tv")
        _require(report, switched is not None and switched.get("category") == "plattarr-liveproof-tv",
                 "hygiene-recovery.category-switch", "failed Movie grab now reports the TV client category")

        # The replacement search must see a different release from the same local indexer.
        services.state.set_phase("replacement")
        _set_gate(api, True)
        gate_owned = True
        refreshed = api.run_command("RefreshMonitoredDownloads", seconds=90)
        _require(report, refreshed.get("status") == "completed",
                 "hygiene-recovery.refresh", "category-switched failure action and replacement refresh completed")

        deadline = time.time() + 60
        actions = []
        while time.time() < deadline:
            actions = _get_actions(api)
            if actions and actions[0].get("status") in ("completed", "failed", "paused"):
                break
            time.sleep(1)

        action = next((item for item in actions if item.get("downloadId") == failed_job["nzo_id"]), None)
        _require(report, action is not None and action.get("status") == "completed" and
                 action.get("mediaKind") == 1 and action.get("reasonCode") == "client-failed" and
                 action.get("checkpoint") == "replacement-requested" and action.get("recoveryPath"),
                 "hygiene-recovery.journal", "Movie action completed at replacement checkpoint with a durable recovery path")

        fixture_state = services.state.snapshot()
        removed = next((item for item in fixture_state["removed"] if item.get("id") == action["downloadId"]), None)
        files_left = bool(fixture_state["filesPresent"]) and all(fixture_state["filesPresent"])
        _require(report, removed is not None and removed.get("deleteFiles") == "0" and files_left,
                 "hygiene-recovery.source-retained", "SAB job removed with deleteFiles=0; source file remains")

        history = api.get("/api/v3/history?page=1&pageSize=100&sortKey=date&sortDirection=descending")
        history_records = history.get("records", [])
        failed_history = any(item.get("downloadId") == action["downloadId"] and
                             item.get("eventType", "").lower() == "downloadfailed" for item in history_records)
        _require(report, failed_history, "hygiene-recovery.failed-history", "Movie failed-download history recorded")

        blocklist = api.get("/api/v3/blocklist?page=1&pageSize=100&sortKey=date&sortDirection=descending")
        blocklist_records = blocklist.get("records", [])
        blocked = any(item.get("movieId") == movie_id and item.get("sourceTitle") == FAILED_RELEASE_TITLE
                      for item in blocklist_records)
        _require(report, blocked, "hygiene-recovery.blocklist", "matching Movie release added to the blocklist")

        search_deadline = time.time() + 30
        while time.time() < search_deadline:
            if services.state.snapshot()["replacementJobs"]:
                break
            time.sleep(1)
        fixture_state = services.state.snapshot()
        replacement_jobs = fixture_state["replacementJobs"]
        replacement_queued = (fixture_state["searchRequests"] > baseline_searches and len(replacement_jobs) == 1 and
                              replacement_jobs[0].get("filename") == REPLACEMENT_RELEASE_TITLE and
                              replacement_jobs[0].get("cat") == "plattarr-liveproof-movies")
        _require(report, replacement_queued, "hygiene-recovery.replacement-search",
                 "a distinct replacement was queued once in the original Movie category")

        api.run_command("RefreshMonitoredDownloads", seconds=90)
        preview = api.get("/api/v3/hygiene/preview")
        replacement_assessment = next((item for item in preview if item.get("title") == REPLACEMENT_RELEASE_TITLE), None)
        replacement_kind = replacement_assessment and str(replacement_assessment.get("kind", "")).casefold() in ("1", "movie", "movies")
        _require(report, replacement_kind,
                 "hygiene-recovery.replacement-route", "replacement is tracked as a Movie")

        services.state.set_phase("failed")
        after = api.get(f"/api/v3/release?movieId={movie_id}")
        blocked_release = next((item for item in after if item.get("title") == FAILED_RELEASE_TITLE), None)
        _require(report, blocked_release is not None and blocked_release.get("approved") is False and
                 any("blocklist" in reason.lower() for reason in blocked_release.get("rejections", [])),
                 "hygiene-recovery.replacement-filter", "the failed release is rejected by the normal blocklist decision")
    except ProofError:
        pass
    except (ApiError, OSError, ValueError, KeyError, StopIteration) as error:
        report.fail("hygiene-recovery", scrub(f"{type(error).__name__}: {error}"))
    except Exception as error:  # Keep unexpected live-proof failures readable and redact key-shaped strings.
        report.fail("hygiene-recovery", scrub(f"{type(error).__name__}: {error}"))
    finally:
        if gate_owned:
            try:
                _set_gate(api, False)
                report.ok("hygiene-recovery.gate-reset", "global action gate returned to off")
            except Exception as error:
                report.fail("hygiene-recovery.gate-reset", scrub(str(error)))
        if services is not None:
            services.close()
