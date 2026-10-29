#!/usr/bin/env python3
"""Measure startup, RSS, and empty-fixture API latency on disposable loopback data.

Build Release first: `. tools/env.sh && dotnet build src/Radarr.sln -c Release`.
This script is Linux-only because it reads /proc for RSS.
"""

import json
import math
import pathlib
import shutil
import socket
import sqlite3
import statistics
import subprocess
import tempfile
import time
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
DOTNET = shutil.which("dotnet") or str(pathlib.Path.home() / ".dotnet/dotnet")
API_KEY = "00000000000000000000000000000000"
ROUTES = [
    "/ping",
    "/api/v3/system/status",
    "/api/v3/queue",
    "/api/v3/hygiene/preview",
    "/api/v3/movie",
]


def read_rss(pid):
    status = pathlib.Path(f"/proc/{pid}/status").read_text()
    line = next(line for line in status.splitlines() if line.startswith("VmRSS:"))
    return int(line.split()[1])


def count_fixture_rows(database):
    counts = {}
    with sqlite3.connect(database) as connection:
        for table in (
            "HygieneActions",
            "HygieneQueueStates",
            "HygieneRules",
            "Movies_Movies",
            "Tv_Series",
            "Music_Artists",
        ):
            counts[table] = connection.execute(f'SELECT count(*) FROM "{table}"').fetchone()[0]
    return counts


def main():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        port = listener.getsockname()[1]
    base_url = f"http://127.0.0.1:{port}"

    with tempfile.TemporaryDirectory(prefix="plattarr-refactor-baseline-") as temp_dir:
        data = pathlib.Path(temp_dir)
        (data / "config.xml").write_text(
            "<Config>"
            "<BindAddress>127.0.0.1</BindAddress>"
            f"<Port>{port}</Port>"
            f"<ApiKey>{API_KEY}</ApiKey>"
            "<AuthenticationMethod>External</AuthenticationMethod>"
            "</Config>"
        )

        def request(path):
            url = base_url + path
            req = urllib.request.Request(url, headers={"X-Api-Key": API_KEY})
            with urllib.request.urlopen(req, timeout=10) as response:
                return response.status, len(response.read())

        def stop(process, log):
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
            log.close()

        def start():
            log = (data / "app.log").open("ab")
            process = subprocess.Popen(
                [DOTNET, str(ROOT / "_output/net10.0/Radarr.dll"), "-nobrowser", f"-data={data}"],
                cwd=ROOT,
                stdout=log,
                stderr=subprocess.STDOUT,
            )
            began = time.perf_counter()
            try:
                while time.perf_counter() - began < 120:
                    if process.poll() is not None:
                        raise RuntimeError(f"app exited with status {process.returncode}")
                    try:
                        status, _ = request("/ping")
                        if status == 200:
                            return process, (time.perf_counter() - began) * 1000, log
                    except Exception:
                        time.sleep(0.02)
                raise RuntimeError("startup timed out")
            except Exception:
                stop(process, log)
                raise

        process, first_start_ms, log = start()
        stop(process, log)
        time.sleep(0.3)

        warm_starts = []
        for run in range(5):
            process, elapsed_ms, log = start()
            warm_starts.append(elapsed_ms)
            if run < 4:
                stop(process, log)
                time.sleep(0.3)

        try:
            time.sleep(5)
            steady_rss = read_rss(process.pid)
            results = {}
            for path in ROUTES:
                timings = []
                response_sizes = []
                for _ in range(60):
                    began = time.perf_counter()
                    status, size = request(path)
                    if status != 200:
                        raise RuntimeError(f"{path} returned {status}")
                    timings.append((time.perf_counter() - began) * 1000)
                    response_sizes.append(size)
                ordered = sorted(timings)
                results[path] = {
                    "p50_ms": round(statistics.median(timings), 3),
                    "p95_ms": round(ordered[math.ceil(0.95 * len(ordered)) - 1], 3),
                    "max_ms": round(max(timings), 3),
                    "response_bytes": max(response_sizes),
                }

            revision = subprocess.check_output(
                ["git", "rev-parse", "--short", "HEAD"], cwd=ROOT, text=True
            ).strip()
            print(json.dumps({
                "revision": revision,
                "build": "Release",
                "fixture_rows": count_fixture_rows(data / "radarr.db"),
                "first_start_ms": round(first_start_ms, 1),
                "warm_start_ms": [round(value, 1) for value in warm_starts],
                "steady_rss_kib": steady_rss,
                "after_requests_rss_kib": read_rss(process.pid),
                "request_samples_per_route": 60,
                "routes": results,
            }, indent=2))
        finally:
            stop(process, log)


if __name__ == "__main__":
    main()
