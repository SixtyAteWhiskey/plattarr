"""The live-proof checks. Each takes the API client and a Report; none prints a key or a secret."""
import os
import time
from urllib.parse import urlsplit

from client import ApiError, scrub


class Report:
    def __init__(self):
        self.failures = 0

    def _line(self, tag, name, detail):
        print(f"{tag:5} {name}" + (f"  {detail}" if detail else ""))

    def ok(self, name, detail=""):
        self._line("PASS", name, detail)

    def warn(self, name, detail=""):
        self._line("WARN", name, detail)

    def info(self, name, detail=""):
        self._line("INFO", name, detail)

    def fail(self, name, detail=""):
        self.failures += 1
        self._line("FAIL", name, detail)

    def check(self, condition, name, detail=""):
        (self.ok if condition else self.fail)(name, detail)
        return condition


def smoke(api, report, allow_health=()):
    """Version and migration, an empty health page, and no errors logged since the process started."""
    status = api.get("/api/v3/system/status")
    report.info("smoke.status", f"{status['appName']} {status['version']} migration {status['migrationVersion']} "
                                f"{status['databaseType']} docker={status['isDocker']}")

    health = [h for h in api.get("/api/v3/health") if h["source"] not in allow_health]
    report.check(not health, "smoke.health", "empty" if not health else
                 "; ".join(f"{h['source']} ({h['type']}): {h['message'][:100]}" for h in health))

    started = status.get("startTime", "")
    page = api.get("/api/v3/log?level=error&page=1&pageSize=50&sortKey=time&sortDirection=descending")
    records = [r for r in page.get("records", []) if r["time"] >= started]
    report.check(not records, "smoke.log", f"no errors since {started[:19]}" if not records else
                 "; ".join(f"{r['logger']}: {r['message'][:100]}" for r in records[:3]))


# What each media kind's cover URLs must look like (the part after /MediaCover/).
COVER_FOLDERS = {
    "movie": lambda i: (str(i),),
    "series": lambda i: ("Series", str(i)),
    "artist": lambda i: ("Artists", str(i)),
    "album": lambda i: ("Albums", str(i)),
}


def _library(api):
    """Yield (kind, id, title, image urls) for everything in the library."""
    for movie in api.get("/api/v3/movie"):
        yield "movie", movie["id"], movie["title"], [i["url"] for i in movie.get("images", []) if i.get("url")]
    for series in api.get("/api/v3/series"):
        yield "series", series["id"], series["title"], [i["url"] for i in series.get("images", []) if i.get("url")]
    for artist in api.get("/api/v3/artist"):
        yield "artist", artist["id"], artist["artistName"], [i["url"] for i in artist.get("images", []) if i.get("url")]
        for album in api.get(f"/api/v3/album?artistId={artist['id']}"):
            yield "album", album["id"], album["title"], [i["url"] for i in album.get("images", []) if i.get("url")]


def covers(api, report, allow_missing=False):
    """Every media kind keeps its covers in its own folder, and the URLs the API hands out are served."""
    counts, wrong, folders, files = {}, [], {}, {}
    for kind, item_id, title, urls in _library(api):
        counts[kind] = counts.get(kind, 0) + 1
        for url in urls:
            path = urlsplit(url).path
            at = path.find("/MediaCover/")
            parts = path[at + len("/MediaCover/"):].split("/") if at >= 0 else []
            if at < 0 or tuple(parts[:-1]) != COVER_FOLDERS[kind](item_id):
                wrong.append(f"{kind} {item_id} {title!r}: {path}")
                continue
            folders.setdefault("/".join(parts[:-1]).lower(), set()).add(kind)
            files[url] = (kind, item_id, title)

    report.info("covers.library", ", ".join(f"{k}={v}" for k, v in sorted(counts.items())) + f", images={len(files)}")
    report.check(not wrong, "covers.folder_per_kind", "all URLs in their own kind's folder" if not wrong else
                 f"{len(wrong)} wrong, first: {wrong[0]}")
    shared = {f: k for f, k in folders.items() if len(k) > 1}
    report.check(not shared, "covers.no_shared_folder", "no folder is used by two kinds" if not shared else str(shared))

    served, missing, denied, other = 0, [], 0, []
    for url, (kind, item_id, title) in sorted(files.items()):
        status, headers, _ = api.raw("GET", url)
        if status == 200 and headers.get("Content-Type", "").startswith("image/"):
            served += 1
        elif status == 404:
            missing.append(f"{kind} {item_id} {title!r}: {urlsplit(url).path}")
        elif status in (401, 403):
            denied += 1
        else:
            other.append(f"{status} {urlsplit(url).path}")
    report.check(denied == 0, "covers.auth", "image requests accepted" if not denied else
                 f"{denied} requests refused (401/403): is the API key accepted for /MediaCover on this instance?")
    report.check(not other, "covers.status", "served as images" if not other else f"unexpected: {other[:3]}")
    if missing:
        # After an upgrade that moves a folder, the files come back when the item is refreshed.
        (report.warn if allow_missing else report.fail)("covers.missing", f"{len(missing)} of {len(files)} not on disk, first: {missing[0]}")
    else:
        report.ok("covers.served", f"{served} of {len(files)} images served")


def baseline(api, report, expect=None, movie_id=1):
    """RssSync, then the release search for one movie. Pass expect=(total, approved, mapped) to compare."""
    command = api.run_command("RssSync")
    report.check(command["status"] == "completed", "baseline.rss_sync", command["status"])
    try:
        releases = api.get(f"/api/v3/release?movieId={movie_id}", timeout=180)
    except ApiError as error:
        report.fail("baseline.release", str(error))
        return
    got = (len(releases), sum(1 for r in releases if r.get("approved")), sum(1 for r in releases if r.get("mappedMovieId")))
    detail = f"movie {movie_id}: {got[0]} releases, {got[1]} approved, {got[2]} mapped"
    if expect:
        report.check(got == tuple(expect), "baseline.releases", f"{detail} (expected {tuple(expect)})")
    else:
        report.info("baseline.releases", detail)


# Defaults a fresh instance must ship with. Run `defaults` only against an instance with an empty config:
# an instance whose settings page was ever saved stores the values it showed then.
FRESH_DEFAULTS = {"allowFingerprinting": "never"}


def defaults(api, report):
    settings = api.get("/api/v3/config/mediamanagement")
    for key, expected in FRESH_DEFAULTS.items():
        report.check(settings.get(key) == expected, f"defaults.{key}", f"{settings.get(key)!r} (expected {expected!r})")


def _poll(seconds, condition):
    deadline = time.time() + seconds
    while time.time() < deadline:
        found = condition()
        if found is not None:
            return found
        time.sleep(2)
    return None


def fingerprint(api, report, journal, music_root=None):
    """FpcalcCheck warns when fingerprinting is on and fpcalc is missing, and stays quiet when it is off.

    Needs a Music root folder (created and removed by the proof; the folder must exist on the instance).
    Everything it changes is journaled and restored at the end."""
    music_root = music_root or os.environ.get("LIVEPROOF_MUSIC_ROOT", "/testdata/music")
    settings = api.get("/api/v3/config/mediamanagement")
    path = f"/api/v3/config/mediamanagement/{settings['id']}"
    report.info("fingerprint.start", f"allowFingerprinting={settings['allowFingerprinting']}")

    def mode(value):
        body = api.get("/api/v3/config/mediamanagement")
        body["allowFingerprinting"] = value
        api.put(path, body)

    def warning():
        return [h for h in api.get("/api/v3/health") if h["source"] == "FpcalcCheck"]

    try:
        if not api.get("/api/v3/music/rootfolder"):
            quality = api.get("/api/v3/music/qualityprofile")
            metadata = api.get("/api/v3/metadataprofile")
            try:
                root = api.post("/api/v3/music/rootfolder", {
                    "name": "liveproof", "path": music_root,
                    "defaultQualityProfileId": quality[0]["id"], "defaultMetadataProfileId": metadata[0]["id"],
                    "defaultMonitorOption": "all", "defaultNewItemMonitorOption": "all", "defaultTags": []})
            except ApiError as error:
                report.fail("fingerprint.root_folder", f"{error} (create {music_root} on the instance or set LIVEPROOF_MUSIC_ROOT)")
                return
            journal.created(f"/api/v3/music/rootfolder/{root['id']}")

        journal.will_edit(path, settings)

        mode("newFiles")
        started = time.time()
        found = _poll(40, lambda: warning() or None)
        if report.check(found is not None, "fingerprint.warns_when_on", "no FpcalcCheck warning within 40 s" if found is None else
                        f"{found[0]['type']}: {found[0]['message'][:80]} (after {time.time() - started:.0f} s)"):
            # The reason is the stable part of the contract; the text may change.
            report.check("fpcalc" in found[0]["message"].lower(), "fingerprint.message_names_fpcalc", found[0]["message"][:80])

        mode("never")
        started = time.time()
        cleared = _poll(40, lambda: True if not warning() else None)
        report.check(bool(cleared), "fingerprint.quiet_when_never", "FpcalcCheck cleared after "
                     f"{time.time() - started:.0f} s" if cleared else "warning still present 40 s after setting never")
    finally:
        failures = journal.restore(api, log=lambda line: print(scrub(f"      {line}")))
        report.check(failures == 0, "fingerprint.restore", "settings and root folder put back" if not failures else "restore failed: run `run.py restore`")
