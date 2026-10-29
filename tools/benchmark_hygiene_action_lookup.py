#!/usr/bin/env python3
"""Compare the queue action identity lookup on disposable SQLite journals."""

import sqlite3
import statistics
import time


def measure(connection, lookup, identities, use_identity_query):
    elapsed = []
    rows_read = []
    for _ in range(10):
        start = time.perf_counter()
        read = 0
        for download_id in identities:
            parameters = (1, download_id, "client-failed") if use_identity_query else ()
            rows = connection.execute(lookup, parameters).fetchall()
            read += len(rows)
            matches = (
                row for row in rows
                if row[1] == 1 and row[2] == download_id and row[4] == "client-failed"
            )
            max(matches, key=lambda row: row[0], default=None)
        elapsed.append((time.perf_counter() - start) * 1000)
        rows_read.append(read)
    return round(statistics.median(elapsed), 3), rows_read[0]


def main():
    connection = sqlite3.connect(":memory:")
    connection.executescript(
        "CREATE TABLE HygieneActions (Id INTEGER PRIMARY KEY, DownloadClientId INTEGER, "
        "DownloadId TEXT, Status TEXT, ReasonCode TEXT); "
        "CREATE INDEX IX_HygieneActions_Client_Download_Status "
        "ON HygieneActions(DownloadClientId, DownloadId, Status);"
    )
    original = "SELECT * FROM HygieneActions"
    bounded = "SELECT * FROM HygieneActions WHERE DownloadClientId=? AND DownloadId=? AND ReasonCode=?"
    identities = [f"download-{number}" for number in range(20)]

    for count in (100, 10000):
        connection.execute("DELETE FROM HygieneActions")
        connection.executemany(
            "INSERT INTO HygieneActions (DownloadClientId, DownloadId, Status, ReasonCode) VALUES (?,?,?,?)",
            (
                (number % 4 + 1, f"download-{number % 2000}", "completed", "client-failed")
                for number in range(count)
            ),
        )
        connection.commit()
        print(f"actions={count}, candidate lookups={len(identities)}")
        for label, sql, use_identity_query in (
            ("full journal", original, False),
            ("identity query", bounded, True),
        ):
            milliseconds, rows = measure(connection, sql, identities, use_identity_query)
            print(f"  {label}: median {milliseconds} ms / refresh, {rows} rows materialized")
            parameters = (1, identities[0], "client-failed") if use_identity_query else ()
            plan = connection.execute("EXPLAIN QUERY PLAN " + sql, parameters).fetchall()
            print(f"  query plan: {plan}")


if __name__ == "__main__":
    main()
