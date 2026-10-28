#!/usr/bin/env python3
"""Replay the GET-only per-indexer Newznab capabilities contract.

This checker is intentionally limited to loopback targets. It never performs a
search or grab and never prints the API key, response body, or full request URL.
"""

import argparse
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET


def local_name(tag):
    return tag.rsplit("}", 1)[-1].lower()


class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file_pointer, code, message, headers, new_url):
        return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--indexer-id", type=int, default=1)
    args = parser.parse_args()

    if args.indexer_id < 1:
        raise SystemExit("--indexer-id must be a positive integer")

    base_url = os.environ.get("PLATTARR_FEED_URL", "http://127.0.0.1:17881").rstrip("/")
    api_key = os.environ.get("PLATTARR_API_KEY")
    if not api_key:
        raise SystemExit("set PLATTARR_API_KEY for the isolated instance")

    parsed = urllib.parse.urlparse(base_url)
    if parsed.scheme not in ("http", "https") or parsed.hostname not in ("127.0.0.1", "localhost", "::1"):
        raise SystemExit("refusing to replay outside an isolated loopback instance")

    query = urllib.parse.urlencode({"t": "caps", "apikey": api_key})
    path = f"/{args.indexer_id}/api"
    request = urllib.request.Request(
        f"{base_url}{path}?{query}",
        headers={"Accept": "application/xml", "User-Agent": "PlattarrIndexerFeedContract/1.0"},
        method="GET",
    )

    try:
        opener = urllib.request.build_opener(NoRedirectHandler())
        with opener.open(request, timeout=15) as response:
            status = response.status
            content_type = response.headers.get_content_type().lower()
            payload = response.read()
    except urllib.error.HTTPError as error:
        status = error.code
        content_type = error.headers.get_content_type().lower()
        payload = error.read()
    except urllib.error.URLError as error:
        raise SystemExit(f"GET {path}?t=caps could not connect: {error.reason}") from error

    root = None
    try:
        root = local_name(ET.fromstring(payload).tag)
    except ET.ParseError:
        if re.search(br"(?:<!doctype\s+html|<html\b)", payload[:1024], re.IGNORECASE):
            root = "html"

    print(f"GET {path}?t=caps -> HTTP {status}; content-type={content_type}; root={root or 'unparseable'}")

    if status == 200 and root == "caps" and content_type in ("application/xml", "text/xml"):
        print("PASS per-indexer Newznab capabilities contract")
        return 0

    if content_type == "text/html" or root == "html":
        print("FAIL frontend HTML fallback is not a Newznab capabilities response")
    else:
        print("FAIL response is not a Newznab capabilities document")
    return 1


if __name__ == "__main__":
    sys.exit(main())
