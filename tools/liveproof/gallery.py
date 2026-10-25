#!/usr/bin/env python3
"""Capture a walkthrough of the Plattarr UI: one screenshot per main page and media kind, plus the interactive flows
that are hard to reach by hand. Writes <GALLERY_OUT>/shots/*.jpg and <GALLERY_OUT>/manifest.json; gallery_html.py turns
those into one page.

    GALLERY_BASE=http://127.0.0.1:7880 GALLERY_OUT=/tmp/gallery python3 tools/liveproof/gallery.py

Point it at a SCRATCH instance with External authentication (never the real stack: it needs no sign-in there, and it
must not change anything). It clicks only through read-only screens: modals are opened and closed, never confirmed, and the
queue page runs on mocked API answers. Needs the `playwright` Python package and Chromium.
"""
import json
import os
import re
import sys
import time

from playwright.sync_api import sync_playwright

BASE = os.environ.get("GALLERY_BASE", "http://127.0.0.1:7880").rstrip("/")
OUT = os.path.expanduser(os.environ.get("GALLERY_OUT", "gallery-out"))
VIEWPORT = {"width": 1400, "height": 900}
MAX_HEIGHT = 2200   # full-page shots are cut here so one very long page does not dominate the gallery
KINDS = ("Movies", "TV", "Music")

# A text node that is one CamelCase word is almost always a translation key that has no text yet.
RAW_KEY = re.compile(r"[A-Z][a-z]+(?:[A-Z][a-z0-9]+)+")
BRAND_WORDS = {
    "JavaScript", "TypeScript", "MusicBrainz", "FlareSolverr", "qBittorrent", "GitHub", "SignalR", "OpenAPI", "DuckDuckGo",
    "YouTube", "SoundCloud", "BitTorrent", "AudioScrobbler", "AllMusic", "TheAudioDB", "MyAnimeList", "iCal", "AniList",
    "TheMovieDB", "FanArt", "AnimeTosho", "OnDeck", "uTorrent", "SABnzbd", "NzbGet", "NzbDrone", "WebHook", "CustomScript",
    "PostgreSQL", "SQLite", "NetBSD", "FreeBSD", "iOS", "macOS", "DiscogS", "Discogs", "MusicBrainzID", "PlayStation",
    "WavPack", "AppData", "RadarrAPI", "SkyHook", "TheTVDB", "TVDb", "TMDb", "IMDb",
}
# Section headings (legends and h1-h3) in page order: two in a row with the same text is a duplicated section.
HEADINGS = """() => {
  const sel = 'legend, [class*=legend], h1, h2, h3';
  return [...document.querySelectorAll(sel)]
    .filter(e => !(e.parentElement && e.parentElement.closest(sel)))   // a legend and its inner wrapper are one heading
    .map(e => e.innerText.trim()).filter(Boolean);
}"""
TEXT_NODES = """() => {
  const out = [], walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  for (let n = walker.nextNode(); n; n = walker.nextNode()) {
    const tag = n.parentElement && n.parentElement.tagName;
    const text = n.nodeValue.trim();
    if (text && text.length < 80 && tag !== 'SCRIPT' && tag !== 'STYLE') out.push(text);
  }
  return out;
}"""
# Answers that mean nothing is wrong: a cover that is not downloaded yet, and the optional update feed.
EXPECTED_BAD = re.compile(r"/(MediaCover|MediaCoverProxy)/|/api/v3/update|/api/v3/system/backup|/api/v3/log/file")

shots = []
STRINGS = {}   # the served translations, loaded once by main()


class Page:
    """A Playwright page that collects what went wrong while it was open."""

    def __init__(self, browser):
        self.context = browser.new_context(viewport=VIEWPORT)
        self.page = self.context.new_page()
        self.errors, self.bad = [], []
        self.page.on("console", lambda m: self.errors.append(m.text[:200]) if m.type == "error" else None)
        self.page.on("pageerror", lambda e: self.errors.append("pageerror: " + str(e)[:200]))
        self.page.on("response", self._response)

    def _response(self, r):
        if r.status >= 400 and not EXPECTED_BAD.search(r.url):
            self.bad.append(f"{r.status} {r.request.method} {r.url.replace(BASE, '')[:110]}")

    def drain(self):
        errors, bad = list(self.errors), list(self.bad)
        self.errors.clear()
        self.bad.clear()
        return errors, bad

    def go(self, path, settle=2500):
        self.page.goto(BASE + path, wait_until="domcontentloaded", timeout=60000)
        self.wait(settle)

    def wait(self, settle=2000):
        try:
            self.page.wait_for_load_state("networkidle", timeout=15000)
        except Exception:
            pass
        self.page.wait_for_timeout(settle)

    def text(self):
        return self.page.inner_text("body")

    def duplicate_headings(self):
        """Headings that appear twice in a row, which means a section was drawn twice."""
        found, previous = [], None
        for heading in self.page.evaluate(HEADINGS):
            if heading == previous and heading not in found:
                found.append(heading)
            previous = heading
        return found

    def raw_keys(self, strings):
        """(certain, maybe): text nodes that are a translation key shown instead of its text, and CamelCase words to look at.

        Certain: the whole node is a key whose served text differs from the key (the key is known and was not applied).
        Maybe: the whole node is one CamelCase word that is not a key and not a brand name."""
        nodes = set(self.page.evaluate(TEXT_NODES))
        certain = sorted(n for n in nodes if n in strings and strings[n] != n)
        maybe = sorted(n for n in nodes if RAW_KEY.fullmatch(n) and n not in strings and n not in BRAND_WORDS)
        return certain, maybe

    def kind(self, label):
        """Click the Movies / TV / Music switch. Returns False when the page has none."""
        button = self.page.get_by_role("button", name=re.compile(rf"^{label}$"))
        if button.count() == 0:
            return False
        button.first.click(timeout=5000)
        self.wait(1500)
        return True

    def api(self, path):
        return self.page.evaluate("p => fetch(p, {headers: {'X-Api-Key': window.Radarr.apiKey}}).then(r => r.json())", path)

    def escape(self):
        self.page.keyboard.press("Escape")
        self.page.wait_for_timeout(500)


def shot(p, name, group, title, path, look, kind=None, full=True, note=""):
    """Take the screenshot and record it with what went wrong while the page loaded."""
    errors, bad = p.drain()
    raw, maybe = p.raw_keys(STRINGS)
    dup = p.duplicate_headings()
    file = f"{len(shots) + 1:03d}-{name}.jpg"
    os.makedirs(os.path.join(OUT, "shots"), exist_ok=True)
    options = {"path": os.path.join(OUT, "shots", file), "type": "jpeg", "quality": 62}
    if full:
        height = min(p.page.evaluate("document.documentElement.scrollHeight"), MAX_HEIGHT)
        # The app scrolls inside its own container on some pages; fall back to the viewport when the page is not taller.
        options.update(full_page=True, clip={"x": 0, "y": 0, "width": VIEWPORT["width"], "height": max(height, VIEWPORT["height"])})
    p.page.screenshot(**options)
    shots.append({"file": file, "id": name, "group": group, "title": title, "path": path, "kind": kind, "look": look,
                  "note": note, "errors": errors[:4], "bad": bad[:4], "raw": raw[:8], "maybe": maybe[:8], "dup": dup[:4]})
    flag = "!" if (errors or bad or raw or dup) else " "
    print(f"{flag} {file}  {title}" + (f"   errors={len(errors)} bad={len(bad)} raw={raw[:3]} dup={dup[:2]}" if flag == "!" else "")
          + (f"   (look at: {maybe[:3]})" if maybe else ""), flush=True)


def page_set(p, group, title, path, look, kinds=(None,), name=None, full=True, before=None):
    """Capture a page once per media kind (None = as it opens)."""
    name = name or re.sub(r"[^a-z0-9]+", "-", (group + "-" + title).lower()).strip("-")
    for kind in kinds:
        p.go(path)
        if kind and not p.kind(kind):
            continue
        if before:
            before(p)
        label = f"{title} ({kind})" if kind else title
        shot(p, f"{name}-{kind.lower()}" if kind else name, group, label, path, look, kind=kind, full=full)


def with_switcher(p, path):
    """The kinds a page offers: all three when it has the switch, otherwise one capture as it opens."""
    p.go(path)
    return KINDS if p.page.get_by_role("button", name=re.compile(r"^Music$")).count() else (None,)


def mock_queue(p):
    """Answer the queue API with one downloading item and one import problem per media kind, from real library records."""
    movie = (p.api("/api/v3/movie") or [None])[0]
    series = (p.api("/api/v3/series") or [None])[0]
    artist = (p.api("/api/v3/artist") or [None])[0]
    album = (p.api(f"/api/v3/album?artistId={artist['id']}") or [None])[0] if artist else None
    episode = (p.api(f"/api/v3/episode?seriesId={series['id']}") or [None])[0] if series else None
    now = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    common = {"status": "downloading", "trackedDownloadStatus": "ok", "trackedDownloadState": "downloading", "statusMessages": [],
              "protocol": "torrent", "downloadClient": "qBittorrent", "indexer": "Example Indexer", "added": now,
              "size": 4_400_000_000, "sizeleft": 1_100_000_000, "timeleft": "00:21:05", "estimatedCompletionTime": now,
              "customFormats": [], "customFormatScore": 0, "languages": [{"id": 1, "name": "English"}]}
    problem = dict(common, status="completed", trackedDownloadStatus="warning", trackedDownloadState="importBlocked", sizeleft=0,
                   statusMessages=[{"title": "Example.Release.1080p.WEB-DL-GRP",
                                    "messages": ["Failed to import: Read-only file system : '/data/Importing/Example.Release.mkv'"]}])
    records = []
    if movie:
        records += [dict(common, id=101, title="Sintel.2010.1080p.WEB-DL.x264-GRP", downloadId="A1", movieId=movie["id"], movie=movie,
                         quality={"quality": {"id": 3, "name": "WEBDL-1080p"}, "revision": {"version": 1, "real": 0}}),
                    dict(problem, id=102, title="Sintel.2010.2160p.WEB-DL.x265-GRP", downloadId="A2", movieId=movie["id"], movie=movie,
                         quality={"quality": {"id": 19, "name": "WEBDL-2160p"}, "revision": {"version": 1, "real": 0}})]
    if series and episode:
        records += [dict(common, id=201, title="Game.of.Thrones.S01E01.1080p.WEB-DL.x264-GRP", downloadId="B1", seriesId=series["id"],
                         series=series, episodeId=episode["id"], episode=episode, seasonNumber=1,
                         quality={"quality": {"id": 3, "name": "WEBDL-1080p"}, "revision": {"version": 1, "real": 0}}),
                    dict(problem, id=202, title="Game.of.Thrones.S01E02.1080p.WEB-DL.x264-GRP", downloadId="B2", seriesId=series["id"],
                         series=series, episodeId=episode["id"], episode=episode, seasonNumber=1,
                         quality={"quality": {"id": 3, "name": "WEBDL-1080p"}, "revision": {"version": 1, "real": 0}})]
    if artist and album:
        audio = {"quality": {"id": 6, "name": "FLAC"}, "revision": {"version": 1, "real": 0}}
        records += [dict(common, id=301, title="The Black Keys - Album (2022) FLAC", downloadId="C1", artistId=artist["id"], artist=artist,
                         albumId=album["id"], album=album, quality=audio),
                    dict(problem, id=302, title="The Black Keys - Album (2021) FLAC", downloadId="C2", artistId=artist["id"], artist=artist,
                         albumId=album["id"], album=album, quality=audio)]

    def handler(route):
        url = route.request.url
        if route.request.method == "GET" and re.search(r"/api/v3/queue(\?|$)", url):
            route.fulfill(json={"page": 1, "pageSize": 20, "sortKey": "timeleft", "sortDirection": "ascending",
                                "totalRecords": len(records), "records": records})
        else:
            route.continue_()

    p.page.route(re.compile(r".*/api/v3/queue(\?.*)?$"), handler)
    return len(records)


def main():
    os.makedirs(OUT, exist_ok=True)
    with sync_playwright() as pw:
        browser = pw.chromium.launch()
        p = Page(browser)

        # What is in the library decides the detail pages.
        p.go("/")
        status = p.api("/api/v3/system/status")
        movie = (p.api("/api/v3/movie") or [None])[0]
        series = (p.api("/api/v3/series") or [None])[0]
        artist = (p.api("/api/v3/artist") or [None])[0]
        album = (p.api(f"/api/v3/album?artistId={artist['id']}") or [None])[0] if artist else None
        STRINGS.update((p.api("/api/v3/localization") or {}).get("Strings", {}))
        p.drain()
        print(f"{status['appName']} {status['version']} migration {status['migrationVersion']}", flush=True)

        # --- Movies
        page_set(p, "Movies", "Library", "/", "Poster grid: every poster loads (no grey boxes), the toolbar (view, sort, filter, options) works, the sidebar marks Movies.")
        page_set(p, "Movies", "Add movie", "/add/new", "The search box is focused; typing a title lists results with posters.")
        page_set(p, "Movies", "Discover", "/add/discover", "Recommendation lists; empty is fine when no list is configured.")
        page_set(p, "Movies", "Collections", "/collections", "Collections page opens without an error.")
        page_set(p, "Movies", "Import", "/add/import", "Library import starts at a root folder picker.")
        if movie:
            page_set(p, "Movies", "Movie details", f"/movie/{movie['titleSlug']}", "Header with fanart and poster, then the Files, History and Search sections.")

        # --- TV
        page_set(p, "TV", "Library", "/tv", "Poster grid with progress bars; the toolbar and sidebar mark TV.")
        page_set(p, "TV", "Add TV", "/tv/add/new", "The search box is focused; typing a title lists results.")
        page_set(p, "TV", "Import", "/tv/add/import", "Library import starts at a root folder picker.")
        if series:
            page_set(p, "TV", "TV details", f"/series/{series['titleSlug']}", "Seasons and episodes listed, monitoring toggles, banner and poster.")

        # --- Music
        page_set(p, "Music", "Library", "/music", "Artist grid or table; posters load; the sidebar marks Music.")
        page_set(p, "Music", "Add artist", "/music/add/new", "The search box is focused; typing an artist name lists results.")
        page_set(p, "Music", "Unmapped files", "/music/unmapped", "Table of files not matched to a track; empty is fine.")
        if artist:
            page_set(p, "Music", "Artist details", f"/artist/{artist['foreignArtistId']}", "Albums grouped by type, monitoring toggles, artist toolbar.")
        if album:
            page_set(p, "Music", "Album details", f"/album/{album['foreignAlbumId']}", "Track list with quality and file columns; album toolbar.")

        # --- Pages that follow the Movies / TV / Music switch
        shared = [("Calendar", "/calendar", "Month grid; items of the chosen kind only."),
                  ("Queue", "/activity/queue", "Table of downloads; empty is fine here (the flows section shows it filled)."),
                  ("History", "/activity/history", "Event table; the event filter lists the chosen kind's events."),
                  ("Blocklist", "/activity/blocklist", "Table of blocklisted releases."),
                  ("Missing", "/wanted/missing", "Items without files; kind-specific columns."),
                  ("Cutoff unmet", "/wanted/cutoffunmet", "Items with files below the cutoff.")]
        for title, path, look in shared:
            page_set(p, "Activity and wanted", title, path, look, kinds=with_switcher(p, path))

        # --- Settings
        settings = [("Media management", "/settings/mediamanagement", "Naming, folders, file handling for the chosen kind. Music: tag writing and fingerprinting."),
                    ("Profiles", "/settings/profiles", "Quality / audio quality and delay or metadata profiles for the chosen kind."),
                    ("Quality", "/settings/quality", "Quality sizes."),
                    ("Custom formats", "/settings/customformats", "Custom formats list."),
                    ("Indexers", "/settings/indexers", "Indexer cards; adding one opens a schema picker."),
                    ("Download clients", "/settings/downloadclients", "Client cards."),
                    ("Import lists", "/settings/importlists", "List cards for the chosen kind."),
                    ("Connect", "/settings/connect", "Notification cards."),
                    ("Metadata", "/settings/metadata", "Metadata consumers for the chosen kind."),
                    ("Tags", "/settings/tags", "Tag list."),
                    ("General", "/settings/general", "Host, security, proxy, logging, updates."),
                    ("UI", "/settings/ui", "Calendar, dates, language, theme.")]
        for title, path, look in settings:
            page_set(p, "Settings", title, path, look, kinds=with_switcher(p, path))

        # --- System
        for title, path, look in [("Status", "/system/status", "Health list (empty is the goal), about, disk space, more info."),
                                  ("Tasks", "/system/tasks", "Scheduled tasks, including each kind's refresh and rescan."),
                                  ("Backup", "/system/backup", "Backups list."),
                                  ("Updates", "/system/updates", "Update history."),
                                  ("Events", "/system/events", "Log table."),
                                  ("Log files", "/system/logs/files", "Log file list.")]:
            page_set(p, "System", title, path, look)

        # --- Flows
        # 1. Add artist: search for one that is not in the library, then open the add dialog.
        p.go("/music/add/new")
        p.kind("Music")
        box = p.page.locator("input[class*=searchInput]").last
        box.fill("radiohead")
        p.wait(6000)
        shot(p, "flow-add-music-results", "Flows", "Add music: search results", "/music/add/new",
             "Results for 'radiohead': artists and albums, with posters and a link to each.", kind="Music", full=False)
        try:
            p.page.locator("[class*=searchResult], [class*=AddNewArtistSearchResult]").first.click(timeout=5000)
            p.wait(2500)
            shot(p, "flow-add-music-modal", "Flows", "Add music: dialog", "/music/add/new",
                 "Root folder, monitor, new item monitoring, quality and metadata profile, tags, and an Add button. Nothing is added.",
                 kind="Music", full=False)
            p.escape()
        except Exception as error:
            shots.append({"file": None, "id": "flow-add-music-modal", "group": "Flows", "title": "Add music: dialog", "path": "/music/add/new",
                          "kind": "Music", "look": "", "note": "Could not open the dialog: " + str(error)[:160], "errors": [], "bad": [], "raw": [], "maybe": [], "dup": []})
            print("! add music dialog:", str(error)[:120], flush=True)

        # 2. Queue with mocked rows, then the remove dialog.
        count = mock_queue(p)
        p.go("/activity/queue")
        shot(p, "flow-queue", "Flows", "Queue (mocked rows)", "/activity/queue",
             "Movie, series and music downloads in one table: one downloading item and one with an import problem per kind; the problem row shows the reason after the colon.",
             full=False, note=f"{count} mocked rows; the server queue is empty.")
        p.go("/activity/queue")
        try:
            # The toolbar button stays disabled until a row is ticked; the first checkbox is "select all".
            p.page.locator("input[type=checkbox]").nth(1).check(force=True, timeout=5000)
            p.page.get_by_role("button", name="Remove Selected").click(timeout=5000)
        except Exception as error:
            print("! remove dialog:", str(error)[:120], flush=True)
        p.wait(1200)
        shot(p, "flow-queue-remove", "Flows", "Queue: remove dialog", "/activity/queue",
             "Removal method and blocklist options. Nothing is removed.", full=False)
        p.page.unroute(re.compile(r".*/api/v3/queue(\?.*)?$"))

        # 3. Notification event toggles: the Add Connection picker, then the Webhook form.
        p.go("/settings/connect")
        try:
            p.page.locator("[class*=addNotification]").first.click(timeout=5000)
            p.wait(1500)
            shot(p, "flow-connect-picker", "Flows", "Connect: add connection", "/settings/connect",
                 "The list of notification types.", full=False)
            # The whole tile is a link under its label; click the link of the tile named Webhook.
            tile = p.page.locator("[class*=AddNotificationItem-notification]", has_text=re.compile(r"^\s*Webhook"))
            tile.locator("[class*=underlay]").first.click(timeout=5000)
            p.wait(2000)
            shot(p, "flow-connect-webhook", "Flows", "Connect: Webhook event toggles", "/settings/connect",
                 "The event toggles: movie, series and music events each appear once, with sensible names. Nothing is saved.", full=False)
            # Scroll whichever element inside the dialog actually overflows.
            p.page.evaluate("""() => {
              const box = [...document.querySelectorAll('[class*=modal] *')].find(e => e.scrollHeight > e.clientHeight + 20 && /auto|scroll/.test(getComputedStyle(e).overflowY));
              if (box) box.scrollBy(0, 700);
            }""")
            p.page.wait_for_timeout(500)
            shot(p, "flow-connect-webhook-2", "Flows", "Connect: Webhook (further down)", "/settings/connect",
                 "More event toggles and the URL / method / credentials fields.", full=False)
        except Exception as error:
            print("! connect dialog:", str(error)[:120], flush=True)

        browser.close()

    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"app": f"{status['appName']} {status['version']}", "migration": status["migrationVersion"],
                   "taken": time.strftime("%Y-%m-%d %H:%M UTC", time.gmtime()), "shots": shots}, f, indent=1)
    flagged = [s for s in shots if s["errors"] or s["bad"] or s["raw"] or s.get("dup")]
    print(f"\n{len(shots)} screenshots, {len(flagged)} with findings -> {OUT}")


if __name__ == "__main__":
    sys.exit(main())
