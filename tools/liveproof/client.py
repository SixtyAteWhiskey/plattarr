"""Minimal Plattarr API client for the live-proof scripts. Standard library only.

The API key never appears in output: it is read from the environment or from a config.xml,
sent only as the X-Api-Key header, and scrubbed from every error message.
"""
import json
import os
import re
import time
import urllib.error
import urllib.request

KEY_PATTERN = re.compile(r"[A-Fa-f0-9]{32}")
FINISHED = ("completed", "failed", "aborted", "cancelled", "orphaned")


def scrub(text):
    """Replace anything shaped like an API key."""
    return KEY_PATTERN.sub("<redacted>", text)


class ApiError(Exception):
    def __init__(self, status, method, path, body):
        super().__init__(scrub(f"{method} {path} -> HTTP {status}: {body[:300]}"))
        self.status = status
        self.body = body


def read_api_key(config_xml):
    with open(os.path.expanduser(config_xml), encoding="utf-8") as f:
        match = re.search(r"<ApiKey>(.*?)</ApiKey>", f.read())
    if not match:
        raise SystemExit(f"no <ApiKey> in {config_xml}")
    return match.group(1)


class Plattarr:
    def __init__(self, base_url=None, api_key=None, config_xml=None):
        self.base = (base_url or os.environ.get("PLATTARR_URL", "http://127.0.0.1:7880")).rstrip("/")
        config_xml = config_xml or os.environ.get("PLATTARR_CONFIG_XML")
        key = api_key or os.environ.get("PLATTARR_API_KEY") or (read_api_key(config_xml) if config_xml else None)
        if not key:
            raise SystemExit("set PLATTARR_API_KEY or PLATTARR_CONFIG_XML (path to the instance's config.xml)")
        self._key = key

    def __repr__(self):
        return f"Plattarr({self.base})"

    def raw(self, method, path, body=None, timeout=120):
        """Return (status, headers, bytes) for any status code; raise only on connection errors."""
        data = json.dumps(body).encode() if body is not None else None
        request = urllib.request.Request(self.base + path, method=method, data=data,
                                         headers={"X-Api-Key": self._key, "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return response.status, response.headers, response.read()
        except urllib.error.HTTPError as error:
            return error.code, error.headers, error.read()

    def call(self, method, path, body=None, timeout=120):
        """Return the parsed JSON body (None when empty); raise ApiError for HTTP 4xx and 5xx."""
        status, _, payload = self.raw(method, path, body, timeout)
        text = payload.decode("utf-8", "replace")
        if status >= 400:
            raise ApiError(status, method, path, text)
        return json.loads(text) if text else None

    def get(self, path, **kw):
        return self.call("GET", path, **kw)

    def post(self, path, body, **kw):
        return self.call("POST", path, body, **kw)

    def put(self, path, body, **kw):
        return self.call("PUT", path, body, **kw)

    def delete(self, path, **kw):
        return self.call("DELETE", path, **kw)

    def wait_command(self, command_id, seconds=170):
        deadline = time.time() + seconds
        while time.time() < deadline:
            command = self.get(f"/api/v3/command/{command_id}")
            if command["status"] in FINISHED:
                return command
            time.sleep(1)
        raise TimeoutError(f"command {command_id} still running after {seconds}s")

    def run_command(self, name, seconds=170, **fields):
        command = self.post("/api/v3/command", {"name": name, **fields})
        return self.wait_command(command["id"], seconds)
