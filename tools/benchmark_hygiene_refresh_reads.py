#!/usr/bin/env python3
"""Profile the SQL read sequence of one synthetic Hygiene queue refresh.

This models the repository reads made by strike observation, interrupted-action
recovery, decision evaluation, and eligible-action identity checks. It does not
run QueueService or measure writes, download clients, or .NET allocations.
"""

import json
import pathlib
import sqlite3
import statistics
import subprocess
import sys
import tempfile
import time


QUEUE_SIZE = 20
REPETITIONS = 5
SIZES = (100, 10_000, 100_000)
IDENTITIES = [(number % 4 + 1, f"download-{number}") for number in range(QUEUE_SIZE)]
IDENTITY_SQL = " OR ".join(
    '("DownloadClientId" = ? AND "DownloadId" = ?)' for _ in IDENTITIES
)
IDENTITY_PARAMS = tuple(value for identity in IDENTITIES for value in identity)


def create_fixture(path, count):
    with sqlite3.connect(path) as db:
        db.executescript(
            "CREATE TABLE HygieneQueueStates (Id INTEGER PRIMARY KEY, DownloadClientId INTEGER, "
            "DownloadId TEXT, LastSeenAt TEXT);"
            "CREATE UNIQUE INDEX IX_HygieneQueueStates_Client_Download "
            "ON HygieneQueueStates(DownloadClientId, DownloadId);"
            "CREATE TABLE HygieneActions (Id INTEGER PRIMARY KEY, DownloadClientId INTEGER, "
            "DownloadId TEXT, ReasonCode TEXT, Status TEXT);"
            "CREATE INDEX IX_HygieneActions_Client_Download_Status "
            "ON HygieneActions(DownloadClientId, DownloadId, Status);"
            "CREATE TABLE HygieneRules (Id INTEGER PRIMARY KEY, ReasonCode TEXT);"
        )
        db.execute("INSERT INTO HygieneRules VALUES (1, 'client-failed')")
        db.executemany(
            "INSERT INTO HygieneQueueStates (DownloadClientId, DownloadId, LastSeenAt) VALUES (?, ?, ?)",
            ((number % 4 + 1, f"download-{number}",
              "2026-09-01" if number >= QUEUE_SIZE and number % 100 == 0 else "2026-10-29")
             for number in range(count)),
        )
        db.executemany(
            "INSERT INTO HygieneActions (DownloadClientId, DownloadId, ReasonCode, Status) "
            "VALUES (?, ?, ?, ?)",
            ((number % 4 + 1, f"download-{number % 2000}", "client-failed",
              "failed" if number % 1000 == 999 else "completed")
             for number in range(count)),
        )


def rss_kib():
    line = next(line for line in pathlib.Path("/proc/self/status").read_text().splitlines()
                if line.startswith("VmRSS:"))
    return int(line.split()[1])


def run_once(db, bounded):
    query_count = 0
    rows_read = 0
    phase_rows = {}

    def read(phase, sql, params=()):
        nonlocal query_count, rows_read
        rows = db.execute(sql, params).fetchall()
        query_count += 1
        rows_read += len(rows)
        phase_rows[phase] = phase_rows.get(phase, 0) + len(rows)
        return rows

    state_query = (f"SELECT * FROM HygieneQueueStates WHERE {IDENTITY_SQL}"
                   if bounded else "SELECT * FROM HygieneQueueStates")
    state_params = IDENTITY_PARAMS if bounded else ()
    strike_states = read("strike states", state_query, state_params)
    read("strike rules", "SELECT * FROM HygieneRules")
    if bounded:
        expired = read("expired states", "SELECT * FROM HygieneQueueStates WHERE LastSeenAt < ?",
                       ("2026-09-29",))
    else:
        expired = [row for row in strike_states if row[3] < "2026-09-29"]

    if bounded:
        incomplete = read("unfinished actions", "SELECT * FROM HygieneActions WHERE Status != ?",
                          ("completed",))
    else:
        incomplete = [row for row in read("unfinished actions", "SELECT * FROM HygieneActions")
                      if row[4] != "completed"]

    decision_states = read("decision states", state_query, state_params)
    read("decision rules", "SELECT * FROM HygieneRules")
    latest = []
    for client_id, download_id in IDENTITIES:
        if bounded:
            actions = read("action identity", "SELECT * FROM HygieneActions WHERE "
                           "DownloadClientId = ? AND DownloadId = ? AND ReasonCode = ?",
                           (client_id, download_id, "client-failed"))
        else:
            actions = [row for row in read("action identity", "SELECT * FROM HygieneActions")
                       if row[1] == client_id and row[2] == download_id and row[3] == "client-failed"]
        latest.append(max((row[0] for row in actions), default=None))

    selected = {
        "strike_ids": sorted(row[0] for row in strike_states if (row[1], row[2]) in IDENTITIES),
        "expired_ids": sorted(row[0] for row in expired),
        "incomplete_ids": sorted(row[0] for row in incomplete),
        "decision_ids": sorted(row[0] for row in decision_states if (row[1], row[2]) in IDENTITIES),
        "latest_ids": latest,
    }
    return query_count, rows_read, phase_rows, selected


def worker(path, bounded):
    db = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
    db.execute("PRAGMA cache_size = 2000")
    baseline_rss = rss_kib()
    timings = []
    results = []
    for _ in range(REPETITIONS):
        started = time.perf_counter()
        results.append(run_once(db, bounded))
        timings.append((time.perf_counter() - started) * 1000)
    db.close()
    queries, rows, phase_rows, selected = results[-1]
    print(json.dumps({
        "median_ms": round(statistics.median(timings), 3),
        "range_ms": [round(min(timings), 3), round(max(timings), 3)],
        "queries": queries,
        "rows_materialized": rows,
        "rows_by_phase": phase_rows,
        "rss_growth_kib": max(0, rss_kib() - baseline_rss),
        "selected": selected,
    }))


def query_plans(path):
    queries = {
        "state identity": (f"SELECT * FROM HygieneQueueStates WHERE {IDENTITY_SQL}", IDENTITY_PARAMS),
        "expired states": ("SELECT * FROM HygieneQueueStates WHERE LastSeenAt < ?", ("2026-09-29",)),
        "unfinished actions": ("SELECT * FROM HygieneActions WHERE Status != ?", ("completed",)),
        "action identity": ("SELECT * FROM HygieneActions WHERE DownloadClientId = ? "
                            "AND DownloadId = ? AND ReasonCode = ?", (1, "download-0", "client-failed")),
    }
    with sqlite3.connect(path) as db:
        return {name: list(dict.fromkeys(row[3] for row in db.execute(
            "EXPLAIN QUERY PLAN " + sql, params)
            if row[3].startswith(("MULTI-INDEX", "SEARCH", "SCAN"))))
            for name, (sql, params) in queries.items()}


def main():
    if len(sys.argv) == 4 and sys.argv[1] == "--worker":
        worker(sys.argv[2], sys.argv[3] == "bounded")
        return

    report = {"queue_identities": QUEUE_SIZE, "repetitions": REPETITIONS, "sizes": {}}
    with tempfile.TemporaryDirectory(prefix="plattarr-hygiene-refresh-") as directory:
        for count in SIZES:
            path = pathlib.Path(directory) / "fixture.db"
            create_fixture(path, count)
            comparisons = {}
            for mode in ("full", "bounded"):
                output = subprocess.check_output(
                    [sys.executable, __file__, "--worker", str(path), mode], text=True
                )
                comparisons[mode] = json.loads(output)
            if comparisons["full"].pop("selected") != comparisons["bounded"].pop("selected"):
                raise AssertionError(f"Selected rows differ for fixture size {count}")
            report["sizes"][count] = comparisons
            if count == SIZES[0]:
                report["query_plans"] = query_plans(path)
            path.unlink()
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
