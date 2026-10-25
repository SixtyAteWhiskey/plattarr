#!/usr/bin/env python3
"""Replay the Servarr API calls made by Seerr 3.5.1 against a scratch Plattarr.

Run only against the isolated port-7881 instance described in the liveproof README.
The API key is sent as Seerr sends it (the `apikey` query parameter) and is never
printed. CRUD calls create and remove one temporary movie, series, and tag.
"""
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

DEFAULT_MOVIE_IDS = (27205, 550, 603, 278, 496243)
DEFAULT_TVDB_IDS = (270633, 121361, 73255, 81189, 80348, 153021)


def api_key_from_config(path):
    root = ET.parse(path).getroot()
    key = root.findtext("ApiKey")
    if not key:
        raise SystemExit(f"No ApiKey in {path}")
    return key


class Servarr:
    def __init__(self, base_url, api_key, server):
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key
        self.server = server

    def call(self, method, route, body=None):
        parts = urllib.parse.urlsplit(route)
        query = urllib.parse.parse_qsl(parts.query, keep_blank_values=True)
        query.append(("apikey", self.api_key))
        url = f"{self.base_url}/{self.server}/api/v3{parts.path}?{urllib.parse.urlencode(query)}"
        data = json.dumps(body).encode("utf-8") if body is not None else None
        request = urllib.request.Request(
            url,
            data=data,
            method=method,
            headers={"Content-Type": "application/json"},
        )

        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                payload = response.read()
                status = response.status
        except urllib.error.HTTPError as error:
            status = error.code
            payload = error.read()

        print(f"{self.server.upper():6} {method:6} {route} -> {status}")
        if status < 200 or status >= 300:
            message = payload.decode("utf-8", "replace")[:500]
            message = message.replace(self.api_key, "<redacted>")
            raise RuntimeError(f"{method} /{self.server}/api/v3{route} returned {status}: {message}")

        if not payload:
            return None
        return json.loads(payload.decode("utf-8"))


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def read_common_contract(api):
    status = api.call("GET", "/system/status")
    expected_name = "Radarr" if api.server == "radarr" else "Sonarr"
    require(status.get("appName") == expected_name, f"status appName was not {expected_name}")
    if api.server == "sonarr":
        require(status.get("version", "").startswith("4."), "Sonarr status did not report a 4.x version")

    profiles = api.call("GET", "/qualityprofile")
    require(profiles and all("id" in item and "name" in item for item in profiles), "qualityprofile shape mismatch")

    roots = api.call("GET", "/rootfolder")
    require(roots and all(item.get("path") for item in roots), "rootfolder shape mismatch")

    queue = api.call("GET", "/queue?includeEpisode=true")
    require(isinstance(queue.get("records"), list), "queue response did not contain records")
    if queue["records"]:
        kind = "movieId" if api.server == "radarr" else "seriesId"
        other_kind = "seriesId" if api.server == "radarr" else "movieId"
        require(
            all(item.get(kind) is not None and item.get(other_kind) is None for item in queue["records"]),
            "queue contained records for the other Servarr kind",
        )

    tags = api.call("GET", "/tag")
    require(isinstance(tags, list), "tag response was not a list")

    label = f"seerr-contract-{int(time.time())}"
    tag = api.call("POST", "/tag", {"label": label})
    require(tag and tag.get("id"), "tag create did not return an id")
    updated = api.call("PUT", f"/tag/{tag['id']}", {"id": tag["id"], "label": f"{label}-updated"})
    require(updated.get("label") == f"{label}-updated", "tag update did not return the new label")

    return profiles, roots


def first_accessible_root(roots):
    return next((root["path"] for root in roots if root.get("accessible", True)), roots[0]["path"])


def replay_radarr(api):
    profiles, roots = read_common_contract(api)
    movies = api.call("GET", "/movie")
    require(isinstance(movies, list), "movie response was not a list")

    existing = next((movie for movie in movies if movie.get("tmdbId")), None)
    if existing:
        by_tmdb = api.call("GET", f"/movie?tmdbId={existing['tmdbId']}")
        require(any(movie.get("id") == existing.get("id") for movie in by_tmdb), "movie tmdbId filter missed its record")
        current = api.call("GET", f"/movie/{existing['id']}")
        require(current.get("id") == existing.get("id"), "movie by-id lookup mismatch")

    existing_tmdb_ids = {movie.get("tmdbId") for movie in movies}
    tmdb_id = next((item for item in DEFAULT_MOVIE_IDS if item not in existing_tmdb_ids), None)
    require(tmdb_id is not None, "no unused movie id available for contract replay")
    lookup = api.call("GET", f"/movie/lookup?term=tmdb:{tmdb_id}")
    require(lookup and lookup[0].get("tmdbId") == tmdb_id, "movie lookup did not return the requested TMDb id")
    metadata = lookup[0]
    quality_profile_id = profiles[0]["id"]
    root_path = first_accessible_root(roots)
    movie_options = {
        "title": metadata["title"],
        "qualityProfileId": quality_profile_id,
        "profileId": quality_profile_id,
        "titleSlug": str(tmdb_id),
        "minimumAvailability": metadata.get("minimumAvailability") or "released",
        "tmdbId": tmdb_id,
        "year": metadata.get("year", 0),
        "rootFolderPath": root_path,
        "monitored": False,
        "tags": [],
        "addOptions": {"searchForMovie": False},
    }
    created = api.call("POST", "/movie", movie_options)
    movie_id = created.get("id")
    require(movie_id, "movie create did not return an id")

    try:
        movie = api.call("GET", f"/movie/{movie_id}")
        update = dict(movie)
        update.update(movie_options)
        update["monitored"] = True
        api.call("PUT", "/movie", update)
        by_tmdb = api.call("GET", f"/movie?tmdbId={tmdb_id}")
        require(any(item.get("id") == movie_id for item in by_tmdb), "movie tmdbId filter missed the new record")
    finally:
        api.call("DELETE", f"/movie/{movie_id}?deleteFiles=true&addImportExclusion=false")


def replay_sonarr(api):
    profiles, roots = read_common_contract(api)
    language_profiles = api.call("GET", "/languageprofile")
    require(
        language_profiles and all("id" in item and "name" in item for item in language_profiles),
        "languageprofile response shape mismatch",
    )
    require(
        any(item.get("id") == 1 and item.get("name") == "English" for item in language_profiles),
        "languageprofile does not match the profile used by Plattarr series",
    )

    series = api.call("GET", "/series")
    require(isinstance(series, list), "series response was not a list")
    existing_tvdb_ids = {item.get("tvdbId") for item in series}
    tvdb_id = next((item for item in DEFAULT_TVDB_IDS if item not in existing_tvdb_ids), None)
    require(tvdb_id is not None, "no unused TVDB id available for contract replay")
    lookup = api.call("GET", f"/series/lookup?term=tvdb:{tvdb_id}")
    require(lookup and lookup[0].get("tvdbId") == tvdb_id, "series lookup did not return the requested TVDB id")
    metadata = lookup[0]
    quality_profile_id = profiles[0]["id"]
    language_profile_id = language_profiles[0]["id"]
    root_path = first_accessible_root(roots)
    requested_seasons = [
        season["seasonNumber"]
        for season in metadata.get("seasons", [])
        if season.get("seasonNumber", 0) > 0
    ]
    seasons = [
        {"seasonNumber": season["seasonNumber"], "monitored": season["seasonNumber"] in requested_seasons}
        for season in metadata.get("seasons", [])
    ]
    series_options = {
        "tvdbId": tvdb_id,
        "title": metadata["title"],
        "qualityProfileId": quality_profile_id,
        "languageProfileId": language_profile_id,
        "seasons": seasons,
        "tags": [],
        "seasonFolder": True,
        "monitored": True,
        "monitorNewItems": "all",
        "rootFolderPath": root_path,
        "seriesType": metadata.get("seriesType", "standard"),
        "addOptions": {"ignoreEpisodesWithFiles": True, "searchForMissingEpisodes": False},
    }
    created = api.call("POST", "/series", series_options)
    series_id = created.get("id")
    require(series_id, "series create did not return an id")

    try:
        current = api.call("GET", f"/series/{series_id}")
        require(current.get("id") == series_id, "series by-id lookup mismatch")
        by_tvdb = api.call("GET", f"/series?tvdbId={tvdb_id}")
        require(any(item.get("id") == series_id for item in by_tvdb), "series tvdbId filter missed the new record")
        api.call("PUT", "/series", current)

        episode_series_ids = [series_id] + [
            item["id"] for item in series if item.get("id") and item["id"] != series_id
        ]
        episodes = []
        for episode_series_id in episode_series_ids:
            episodes = api.call("GET", f"/episode?seriesId={episode_series_id}")
            if episodes:
                break
        require(episodes, "no episode rows were available to replay episode/monitor")
        episode_ids = [item["id"] for item in episodes if not item.get("monitored")]
        if not episode_ids:
            episode_ids = [episodes[0]["id"]]
        api.call("PUT", "/episode/monitor", {"episodeIds": episode_ids, "monitored": True})
    finally:
        api.call("DELETE", f"/series/{series_id}?deleteFiles=true&addImportExclusion=false")


def main():
    base_url = os.environ.get("PLATTARR_SEERR_URL", "http://127.0.0.1:7881").rstrip("/")
    config_path = os.environ.get("PLATTARR_SEERR_CONFIG_XML")
    if not config_path:
        raise SystemExit("Set PLATTARR_SEERR_CONFIG_XML to the scratch instance config.xml")

    parsed = urllib.parse.urlsplit(base_url)
    if parsed.hostname not in ("127.0.0.1", "localhost", "::1") or parsed.port != 7881:
        raise SystemExit("Refusing to replay writes outside the isolated localhost:7881 scratch instance")

    api_key = api_key_from_config(config_path)
    replay_radarr(Servarr(base_url, api_key, "radarr"))
    replay_sonarr(Servarr(base_url, api_key, "sonarr"))
    Servarr(base_url, api_key, "radarr").call("POST", "/command", {"name": "RefreshMonitoredDownloads"})
    Servarr(base_url, api_key, "sonarr").call("POST", "/command", {"name": "RefreshMonitoredDownloads"})
    print("PASS Seerr 3.5.1 Servarr API contract replay")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        sys.exit(str(error))
