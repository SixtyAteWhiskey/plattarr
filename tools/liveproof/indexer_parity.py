#!/usr/bin/env python3
"""Compare Prowlarr release attributes with Plattarr's mapped indexer flags.

Network mode reads API keys only from environment variables and never prints
or saves release URLs. Snapshot mode accepts already-sanitized JSON arrays.
"""
import argparse
import json
import os
import sys
from collections import defaultdict
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlsplit
from urllib.request import Request, urlopen


PROWLARR_TO_PLATTARR = {
    "freeleech": "G_Freeleech",
    "halfleech": "G_Halfleech",
    "doubleupload": "G_DoubleUpload",
    "golden": "PTP_Golden",
    "approved": "PTP_Approved",
    "internal": "G_Internal",
    "ahd_internal": "AHD_Internal",
    "scene": "G_Scene",
    "freeleech75": "G_Freeleech75",
    "freeleech25": "G_Freeleech25",
    "ahd_userrelease": "AHD_UserRelease",
    "nuked": "Nuked",
}
PLATTARR_ALIASES = {value.casefold(): value for value in PROWLARR_TO_PLATTARR.values()}
PLATTARR_ALIASES.update({
    "g_freeleech": "G_Freeleech",
    "g_halfleech": "G_Halfleech",
    "g_doubleupload": "G_DoubleUpload",
    "ptp_golden": "PTP_Golden",
    "ptp_approved": "PTP_Approved",
    "g_internal": "G_Internal",
    "g_scene": "G_Scene",
    "g_freeleech75": "G_Freeleech75",
    "g_freeleech25": "G_Freeleech25",
})
BIT_FLAGS = {
    1: "G_Freeleech", 2: "G_Halfleech", 4: "G_DoubleUpload", 8: "PTP_Golden",
    16: "PTP_Approved", 32: "G_Internal", 64: "AHD_Internal", 128: "G_Scene",
    256: "G_Freeleech75", 512: "G_Freeleech25", 1024: "AHD_UserRelease", 2048: "Nuked",
}


def get_value(record, name, default=None):
    wanted = name.casefold()
    return next((value for key, value in record.items() if key.casefold() == wanted), default)


def as_records(payload, label):
    if isinstance(payload, dict):
        payload = payload.get("records", payload.get("results", payload.get("releases")))
    if not isinstance(payload, list) or not all(isinstance(item, dict) for item in payload):
        raise ValueError(f"{label} response must be a JSON array of release objects")
    return payload


def named_flags(value):
    if value is None:
        return []
    if isinstance(value, int):
        return [name for bit, name in BIT_FLAGS.items() if value & bit]
    if isinstance(value, str):
        values = value.replace("|", ",").split(",")
    elif isinstance(value, list):
        values = value
    else:
        return []
    result = []
    for item in values:
        if isinstance(item, dict):
            item = get_value(item, "name", "")
        if not isinstance(item, str):
            continue
        normalized = item.strip().casefold()
        if normalized in PROWLARR_TO_PLATTARR:
            result.append(PROWLARR_TO_PLATTARR[normalized])
        elif normalized in PLATTARR_ALIASES:
            result.append(PLATTARR_ALIASES[normalized])
        elif normalized:
            result.append(f"?{item.strip()}")
    return result


def expected_flags(record):
    flags = set(named_flags(get_value(record, "indexerFlags")))
    # Prowlarr exposes Torznab volume factors separately. This mirrors the
    # values converted by Plattarr's IndexerFlagsMapper.
    download = get_value(record, "downloadVolumeFactor")
    upload = get_value(record, "uploadVolumeFactor")
    scene = get_value(record, "scene")
    try:
        download = float(download)
    except (TypeError, ValueError):
        download = None
    try:
        upload = float(upload)
    except (TypeError, ValueError):
        upload = None
    if download == 0.0:
        flags.add("G_Freeleech")
    elif download == 0.25:
        flags.add("G_Freeleech75")
    elif download == 0.5:
        flags.add("G_Halfleech")
    elif download == 0.75:
        flags.add("G_Freeleech25")
    if upload == 2.0:
        flags.add("G_DoubleUpload")
    if scene is True:
        flags.add("G_Scene")
    return flags


def release_key(record):
    title = get_value(record, "title")
    indexer = get_value(record, "indexer")
    protocol = get_value(record, "protocol")
    if not all(isinstance(value, str) and value.strip() for value in (title, indexer, protocol)):
        return None
    return (title.strip().casefold(), indexer.strip().casefold(), protocol.strip().casefold())


def compare(prowlarr, plattarr):
    by_key = defaultdict(list)
    for record in plattarr:
        key = release_key(record)
        if key:
            by_key[key].append(record)

    matched, mismatches, unmatched = 0, [], 0
    for source in prowlarr:
        key = release_key(source)
        candidates = by_key.get(key, []) if key else []
        if not candidates:
            unmatched += 1
            continue
        target = candidates.pop(0)
        matched += 1
        expected = expected_flags(source)
        actual = set(named_flags(get_value(target, "indexerFlags")))
        if expected != actual:
            mismatches.append((source, expected, actual))

    print(f"Prowlarr releases: {len(prowlarr)}; Plattarr releases: {len(plattarr)}; matched: {matched}; unmatched: {unmatched}")
    for source, expected, actual in mismatches[:10]:
        title = str(get_value(source, "title", "?"))[:100]
        indexer = str(get_value(source, "indexer", "?"))[:60]
        raw = {
            "named": named_flags(get_value(source, "indexerFlags", [])),
            "downloadVolumeFactor": get_value(source, "downloadVolumeFactor"),
            "uploadVolumeFactor": get_value(source, "uploadVolumeFactor"),
            "scene": get_value(source, "scene"),
        }
        print(f"MISMATCH {indexer} | {title}: expected={sorted(expected)} actual={sorted(actual)} Prowlarr={json.dumps(raw, sort_keys=True)}")
    if matched == 0:
        print("FAIL: no comparable releases; use the same query and indexer names in both captures")
        return 1
    if mismatches:
        print(f"FAIL: {len(mismatches)} matched release(s) have different mapped flags")
        return 1
    print("PASS: mapped indexer flags agree for every matched release")
    if unmatched:
        print("INFO: unmatched releases are omitted because the two searches may differ by timing or filtering")
    return 0


def get_json(base_url, route, api_key):
    parsed = urlsplit(base_url)
    if (parsed.scheme.lower() not in ("http", "https") or not parsed.hostname or parsed.username or parsed.password or
            parsed.query or parsed.fragment):
        raise RuntimeError("configured service URL must be an HTTP(S) base without credentials, query, or fragment")
    base = base_url.rstrip("/")
    request = Request(base + route, headers={"X-Api-Key": api_key, "Accept": "application/json"})
    try:
        with urlopen(request, timeout=90) as response:
            return json.loads(response.read().decode("utf-8"))
    except HTTPError as error:
        host = urlsplit(base).hostname or "configured host"
        raise RuntimeError(f"{host} returned HTTP {error.code}") from None
    except (URLError, TimeoutError, json.JSONDecodeError) as error:
        host = urlsplit(base).hostname or "configured host"
        reason = "connection failed" if isinstance(error, URLError) else "response was not valid JSON"
        raise RuntimeError(f"{host}: {reason}") from None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--query", help="the same release query to run in both applications")
    parser.add_argument("--movie-id", type=int, help="Plattarr movie ID for /api/v3/release")
    parser.add_argument("--prowlarr-json", help="sanitized Prowlarr JSON response (offline comparison)")
    parser.add_argument("--plattarr-json", help="sanitized Plattarr JSON response (offline comparison)")
    args = parser.parse_args()

    offline = args.prowlarr_json or args.plattarr_json
    try:
        if offline:
            if not args.prowlarr_json or not args.plattarr_json:
                parser.error("provide both --prowlarr-json and --plattarr-json")
            with open(args.prowlarr_json, encoding="utf-8") as source:
                prowlarr_payload = json.load(source)
            with open(args.plattarr_json, encoding="utf-8") as source:
                plattarr_payload = json.load(source)
        else:
            required = ("PROWLARR_URL", "PROWLARR_API_KEY", "PLATTARR_URL", "PLATTARR_API_KEY")
            missing = [name for name in required if not os.environ.get(name)]
            if missing or not args.query or not args.movie_id:
                parser.error("network mode needs --query, --movie-id, and " + ", ".join(required) + " environment variables")
            prowlarr_route = "/api/v1/search?" + urlencode({"query": args.query, "type": "search"})
            plattarr_route = "/api/v3/release?" + urlencode({"movieId": args.movie_id})
            prowlarr_payload = get_json(os.environ["PROWLARR_URL"], prowlarr_route, os.environ["PROWLARR_API_KEY"])
            plattarr_payload = get_json(os.environ["PLATTARR_URL"], plattarr_route, os.environ["PLATTARR_API_KEY"])

        prowlarr = as_records(prowlarr_payload, "Prowlarr")
        plattarr = as_records(plattarr_payload, "Plattarr")
        return compare(prowlarr, plattarr)
    except (OSError, ValueError, RuntimeError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
