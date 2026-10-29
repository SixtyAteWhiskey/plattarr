#!/usr/bin/env python3
"""Compare interrupted-action journal reads on a disposable SQLite fixture."""

import sqlite3
import statistics
import time


def measure(connection, sql, filter_in_python):
    samples = []
    materialized = 0
    unfinished = 0
    for _ in range(10):
        started = time.perf_counter()
        actions = connection.execute(sql).fetchall()
        materialized = len(actions)
        if filter_in_python:
            actions = [action for action in actions if action[3] != "completed"]
        samples.append((time.perf_counter() - started) * 1000)
        unfinished = len(actions)
    return round(statistics.median(samples), 3), materialized, unfinished


def main():
    connection = sqlite3.connect(":memory:")
    connection.executescript(
        "CREATE TABLE HygieneActions (Id INTEGER PRIMARY KEY, DownloadClientId INTEGER, "
        "DownloadId TEXT, Status TEXT, ReasonCode TEXT, RecoveryPath TEXT); "
        "CREATE INDEX IX_HygieneActions_Client_Download_Status "
        "ON HygieneActions(DownloadClientId, DownloadId, Status);"
    )
    queries = (
        ("full journal", "SELECT * FROM HygieneActions", True),
        ("incomplete only", "SELECT * FROM HygieneActions WHERE Status != 'completed'", False),
    )

    for count in (100, 10000, 100000):
        connection.execute("DELETE FROM HygieneActions")
        connection.executemany(
            "INSERT INTO HygieneActions (DownloadClientId, DownloadId, Status, ReasonCode, RecoveryPath) "
            "VALUES (1,?,?,?,?)",
            (
                (f"download-{number}", "failed" if number % 1000 == 0 else "completed",
                 "client-failed", "synthetic recovery details" * 5)
                for number in range(count)
            ),
        )
        connection.commit()
        print(f"actions={count}, unfinished={count // 1000 + (count % 1000 > 0)}")
        for label, sql, filter_in_python in queries:
            milliseconds, materialized, unfinished = measure(connection, sql, filter_in_python)
            plan = connection.execute("EXPLAIN QUERY PLAN " + sql).fetchall()
            print(f"  {label}: median {milliseconds} ms; {materialized} rows materialized; "
                  f"{unfinished} unfinished; plan={plan}")


if __name__ == "__main__":
    main()
