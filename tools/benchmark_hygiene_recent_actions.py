#!/usr/bin/env python3
"""Compare the Hygiene journal's old and bounded recent-action reads in SQLite."""

import sqlite3
import statistics
import time


def measure(connection, sql, parameters, old):
    samples = []
    rows_read = 0
    for _ in range(10):
        started = time.perf_counter()
        rows = connection.execute(sql, parameters).fetchall()
        rows_read = len(rows)
        if old:
            rows = sorted(rows, key=lambda row: row[0], reverse=True)[:100]
        samples.append((time.perf_counter() - started) * 1000)
    return round(statistics.median(samples), 3), rows_read


def main():
    connection = sqlite3.connect(":memory:")
    connection.execute(
        "CREATE TABLE HygieneActions (Id INTEGER PRIMARY KEY, DownloadClientId INTEGER, "
        "DownloadId TEXT, Status TEXT, Checkpoint TEXT, RecoveryPath TEXT)"
    )
    connection.execute(
        "CREATE INDEX IX_HygieneActions_Client_Download_Status "
        "ON HygieneActions(DownloadClientId, DownloadId, Status)"
    )

    for count in (100, 10000, 100000):
        connection.execute("DELETE FROM HygieneActions")
        connection.executemany(
            "INSERT INTO HygieneActions (DownloadClientId, DownloadId, Status, Checkpoint, RecoveryPath) "
            "VALUES (?, ?, ?, ?, ?)",
            ((number % 4 + 1, f"download-{number}", "completed", "replacement-requested", "")
             for number in range(count)),
        )
        connection.commit()
        print(f"actions={count}, requested=100")
        for label, sql, parameters, old in (
            ("full journal", "SELECT * FROM HygieneActions", (), True),
            ("recent only", "SELECT * FROM HygieneActions ORDER BY Id DESC LIMIT ?", (100,), False),
        ):
            milliseconds, rows_read = measure(connection, sql, parameters, old)
            plan = connection.execute("EXPLAIN QUERY PLAN " + sql, parameters).fetchall()
            print(f"  {label}: median {milliseconds} ms; {rows_read} rows materialized; plan={plan}")


if __name__ == "__main__":
    main()
