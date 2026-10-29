#!/usr/bin/env python3
"""Compare preview state reads on a disposable SQLite queue-state fixture."""

import sqlite3
import statistics
import time


def measure(connection, sql, parameters):
    samples = []
    materialized = 0
    for _ in range(10):
        started = time.perf_counter()
        rows = connection.execute(sql, parameters).fetchall()
        samples.append((time.perf_counter() - started) * 1000)
        materialized = len(rows)
    return round(statistics.median(samples), 3), materialized


def main():
    connection = sqlite3.connect(":memory:")
    connection.executescript(
        "CREATE TABLE HygieneQueueStates (Id INTEGER PRIMARY KEY, DownloadClientId INTEGER, "
        "DownloadId TEXT, LastSeenAt TEXT, ReasonCode TEXT, Strikes INTEGER); "
        "CREATE UNIQUE INDEX IX_HygieneQueueStates_Client_Download "
        "ON HygieneQueueStates(DownloadClientId, DownloadId);"
    )
    identities = [(number % 4 + 1, f"download-{number}") for number in range(20)]
    parameters = [part for identity in identities for part in identity]
    identity_query = "SELECT * FROM HygieneQueueStates WHERE " + " OR ".join(
        '("DownloadClientId" = ? AND "DownloadId" = ?)' for _ in identities
    )
    queries = (
        ("full state table", "SELECT * FROM HygieneQueueStates", ()),
        ("queue identities", identity_query, parameters),
    )

    for count in (100, 10000, 100000):
        connection.execute("DELETE FROM HygieneQueueStates")
        connection.executemany(
            "INSERT INTO HygieneQueueStates (DownloadClientId, DownloadId, LastSeenAt, ReasonCode, Strikes) "
            "VALUES (?,?,?,?,?)",
            (
                (number % 4 + 1, f"download-{number}", "2026-10-29", "client-failed", 3)
                for number in range(count)
            ),
        )
        connection.commit()
        print(f"states={count}, queue identities={len(identities)}")
        for label, sql, values in queries:
            milliseconds, materialized = measure(connection, sql, values)
            plan = connection.execute("EXPLAIN QUERY PLAN " + sql, values).fetchall()
            print(f"  {label}: median {milliseconds} ms; {materialized} rows materialized; "
                  f"plan={plan[0][-1]}")
            if label == "queue identities":
                print(f"  identity seek: {plan[2][-1]}")


if __name__ == "__main__":
    main()
