#!/usr/bin/env python3
"""Replay Ombi v4.53.10's Radarr v3 and Sonarr v3 request flows.

The request methods and payloads are pinned to the stable Ombi v4.53.10 source.
This writes one disposable movie, one series, and two temporary tags, then
removes them. It is limited to a local Plattarr scratch instance on port 7882.
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET


OMBI_SOURCE_REF = "v4.53.10"
DEFAULT_MOVIE_IDS = (27205, 550, 603, 278, 496243)
DEFAULT_TVDB_IDS = (270633, 121361, 73255, 81189, 80348, 153021)


class ContractError(RuntimeError):
    pass


def require(condition, message):
    if not condition:
        raise ContractError(message)


def api_key_from_config(path):
    try:
        root = ET.parse(path).getroot()
    except (OSError, ET.ParseError):
        raise ContractError("could not read the scratch config.xml") from None

    key = root.findtext("ApiKey")
    if not key:
        raise ContractError("scratch config.xml does not contain an API key")
    return key


class Servarr:
    def __init__(self, base_url, api_key, server):
        self.api_root = f"{base_url.rstrip('/')}/{server}/api/v3"
        self.api_key = api_key
        self.server = server

    def call(self, method, route, body=None):
        url = f"{self.api_root}{route}"
        data = json.dumps(body).encode("utf-8") if body is not None else None
        request = urllib.request.Request(
            url,
            data=data,
            method=method,
            headers={
                "X-Api-Key": self.api_key,
                "Content-Type": "application/json",
            },
        )

        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                payload = response.read()
                status = response.status
        except urllib.error.HTTPError as error:
            status = error.code
            payload = b""
        except (urllib.error.URLError, TimeoutError, OSError):
            raise ContractError(f"{self.server} request failed") from None

        # Do not print response bodies, query values, API keys, or filesystem paths.
        route_path = urllib.parse.urlsplit(route).path
        print(f"{self.server.upper():6} {method:6} {route_path} -> {status}")
        if status < 200 or status >= 300:
            raise ContractError(f"{self.server} {method} {route_path} returned HTTP {status}")

        if not payload:
            return None
        try:
            return json.loads(payload.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            raise ContractError(f"{self.server} {method} {route_path} returned invalid JSON") from None


def check_app(api, expected_name):
    status = api.call("GET", "/system/status")
    require(isinstance(status, dict), f"{api.server} system status was not an object")
    require(status.get("appName") == expected_name, f"{api.server} compatibility name mismatch")


def read_profiles_and_roots(api, profile_route="/qualityprofile", root_route="/rootfolder"):
    profiles = api.call("GET", profile_route)
    roots = api.call("GET", root_route)
    require(
        isinstance(profiles, list)
        and profiles
        and all(isinstance(item, dict) and item.get("id") and item.get("name") for item in profiles),
        f"{api.server} quality-profile response shape mismatch",
    )
    require(
        isinstance(roots, list)
        and roots
        and all(isinstance(item, dict) and item.get("path") for item in roots),
        f"{api.server} root-folder response shape mismatch",
    )
    root = next((item for item in roots if item.get("accessible", True)), roots[0])
    return profiles[0], root["path"]


def create_tag(api, label, cleanups):
    existing = api.call("GET", "/tag")
    require(isinstance(existing, list), f"{api.server} tag response was not a list")

    tag = api.call("POST", "/tag", {"Label": label})
    require(isinstance(tag, dict) and tag.get("id"), f"{api.server} tag create returned no id")
    cleanups.append((api, "DELETE", f"/tag/{tag['id']}"))
    return tag


def lookup_first_unused(api, route_prefix, external_ids, existing, id_key, term_prefix):
    used = {item.get(id_key) for item in existing}
    for external_id in external_ids:
        if external_id in used:
            continue
        term = urllib.parse.quote(f"{term_prefix}:{external_id}", safe=":")
        route = f"{route_prefix}?term={term}"
        results = api.call("GET", route)
        if isinstance(results, list) and results:
            return external_id, results[0]
    raise ContractError(f"{api.server} has no unused metadata item for the replay")


def replay_radarr(api, cleanups, label):
    check_app(api, "Radarr")
    profile, root_path = read_profiles_and_roots(api, "/qualityProfile", "/rootfolder")

    # Ombi calls GetTags/CreateTag when its per-user tag option is enabled.
    tag = create_tag(api, f"{label}-radarr", cleanups)
    movies = api.call("GET", "/movie")
    require(isinstance(movies, list), "Radarr movie response was not a list")
    tmdb_id, metadata = lookup_first_unused(api, "/movie/lookup", DEFAULT_MOVIE_IDS, movies, "tmdbId", "tmdb")

    # Mirrors RadarrV3Api.AddMovie with Ombi's AddOnly option enabled. Ombi
    # omits addOptions entirely in this mode, so no search command is queued.
    movie_options = {
        "title": metadata["title"],
        "tmdbId": tmdb_id,
        "qualityProfileId": profile["id"],
        "rootFolderPath": root_path,
        "titleSlug": f"{metadata['title']}{metadata.get('year', 0)}",
        "monitored": True,
        "year": metadata.get("year", 0),
        "minimumAvailability": metadata.get("minimumAvailability") or "released",
        "sizeOnDisk": 0,
        "tags": [tag["id"]],
    }
    created = api.call("POST", "/movie", movie_options)
    require(isinstance(created, dict) and created.get("id"), "Radarr add returned no movie id")
    cleanups.append((api, "DELETE", f"/movie/{created['id']}?deleteFiles=false&addImportExclusion=false"))


def replay_sonarr(api, cleanups, label):
    check_app(api, "Sonarr")
    profile, root_path = read_profiles_and_roots(api)
    languages = api.call("GET", "/languageprofile")
    require(
        isinstance(languages, list)
        and languages
        and all(isinstance(item, dict) and item.get("id") and item.get("name") for item in languages),
        "Sonarr language-profile response shape mismatch",
    )

    # Ombi calls GetTags/CreateTag when its per-user tag option is enabled.
    tag = create_tag(api, f"{label}-sonarr", cleanups)
    series = api.call("GET", "/series")
    require(isinstance(series, list), "Sonarr series response was not a list")
    tvdb_id, metadata = lookup_first_unused(api, "/series/lookup", DEFAULT_TVDB_IDS, series, "tvdbId", "tvdb")

    season_numbers = [
        int(season["seasonNumber"])
        for season in metadata.get("seasons", [])
        if season.get("seasonNumber") is not None
    ]
    total_seasons = max(season_numbers, default=0)
    require(total_seasons > 0, "Sonarr lookup returned no regular seasons")

    # Mirrors Ombi's NewSeries and AddOptions models for one requested season.
    # It adds the series without starting a missing-episode search.
    series_options = {
        "tvdbId": tvdb_id,
        "title": metadata["title"],
        "imdbId": metadata.get("imdbId", ""),
        "cleanTitle": metadata["title"],
        "monitored": True,
        "seasonFolder": True,
        "rootFolderPath": root_path,
        "qualityProfileId": profile["id"],
        "titleSlug": metadata["title"],
        "seriesType": "standard",
        "addOptions": {
            "ignoreEpisodesWithFiles": False,
            "ignoreEpisodesWithoutFiles": False,
            "searchForMissingEpisodes": False,
        },
        "languageProfileId": languages[0]["id"],
        "tags": [tag["id"]],
        "seasons": [
            {"seasonNumber": season_number, "monitored": False}
            for season_number in range(total_seasons + 1)
        ],
    }
    created = api.call("POST", "/series/", series_options)
    require(isinstance(created, dict) and created.get("id"), "Sonarr add returned no series id")
    series_id = created["id"]
    cleanups.append((api, "DELETE", f"/series/{series_id}?deleteFiles=false&addImportExclusion=false"))

    # Ombi fetches the added series and episode rows, then updates season and
    # episode monitoring. Use season 1, which exists in the pinned lookup set.
    current = api.call("GET", f"/series/{series_id}")
    require(isinstance(current, dict) and current.get("id") == series_id, "Sonarr series readback mismatch")
    season = next((item for item in current.get("seasons", []) if item.get("seasonNumber") == 1), None)
    require(season is not None, "Sonarr added series did not contain season 1")
    season["monitored"] = True
    updated = api.call("PUT", "/series/", current)
    require(isinstance(updated, dict), "Sonarr series update returned no object")

    episodes = api.call("GET", f"/Episode?seriesId={series_id}")
    require(isinstance(episodes, list) and episodes, "Sonarr added series has no episode rows")
    season_episodes = [item for item in episodes if item.get("seasonNumber") == 1 and item.get("id")]
    require(season_episodes, "Sonarr added series has no season 1 episodes")
    episode_ids = [item["id"] for item in season_episodes]

    # Ombi first clears monitoring for the season, then monitors requested
    # episodes. Search commands remain disabled by the AddOnly scenario.
    api.call("PUT", "/episode/monitor", {"episodeIds": episode_ids, "monitored": False})
    api.call("PUT", "/episode/monitor", {"episodeIds": episode_ids, "monitored": True})


def validate_target(base_url):
    try:
        parsed = urllib.parse.urlsplit(base_url)
        port = parsed.port
    except ValueError:
        raise ContractError("PLATTARR_OMBI_URL must be a loopback URL on port 7882") from None

    if (
        parsed.scheme != "http"
        or parsed.hostname not in ("127.0.0.1", "localhost", "::1")
        or port != 7882
        or parsed.path not in ("", "/")
        or parsed.query
        or parsed.fragment
        or parsed.username
        or parsed.password
    ):
        raise ContractError("refusing to replay writes outside http://localhost:7882")


def cleanup_resources(cleanups):
    failures = []
    for api, method, route in reversed(cleanups):
        try:
            api.call(method, route)
        except ContractError as error:
            failures.append(str(error))
    return failures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--confirm-disposable", action="store_true")
    args = parser.parse_args()
    if not args.confirm_disposable:
        raise ContractError("pass --confirm-disposable only for a throwaway Plattarr instance")

    base_url = os.environ.get("PLATTARR_OMBI_URL", "http://127.0.0.1:7882").rstrip("/")
    validate_target(base_url)
    config_path = os.environ.get("PLATTARR_OMBI_CONFIG_XML")
    if not config_path:
        raise ContractError("set PLATTARR_OMBI_CONFIG_XML to the scratch config.xml")

    api_key = api_key_from_config(config_path)
    radarr = Servarr(base_url, api_key, "radarr")
    sonarr = Servarr(base_url, api_key, "sonarr")
    cleanups = []
    label = f"ombi-contract-{int(time.time())}"
    failure = None

    try:
        replay_radarr(radarr, cleanups, label)
        replay_sonarr(sonarr, cleanups, label)
    except ContractError as error:
        failure = str(error)
    except (KeyError, TypeError, ValueError):
        failure = "API response did not match the pinned contract"
    except Exception:
        failure = "unexpected replay error"

    cleanup_failures = cleanup_resources(cleanups)
    if failure:
        raise ContractError(failure)
    if cleanup_failures:
        raise ContractError("cleanup failed; discard the scratch instance")

    print(f"PASS Ombi {OMBI_SOURCE_REF} Radarr v3 and Sonarr v3 AddOnly contract replay")


if __name__ == "__main__":
    try:
        main()
    except ContractError as error:
        sys.exit(f"FAIL {error}")
