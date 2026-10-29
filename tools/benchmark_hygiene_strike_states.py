#!/usr/bin/env python3
"""Compare strike-observation state reads on a disposable SQLite fixture."""

import sqlite3
import statistics
import time


def measure(connection, queries):
    samples = []
    materialized = 0
    for _ in range(10):
        started = time.perf_counter()
        rows = [connection.execute(sql, parameters).fetchall() for sql, parameters in queries]
        samples.append((time.perf_counter() - started) * 1000)
        materialized = sum(len(result) for result in rows)
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
    identity_query = "SELECT * FROM HygieneQueueStates WHERE " + " OR ".join(
        '("DownloadClientId" = ? AND "DownloadId" = ?)' for _ in identities
    )
    identity_parameters = tuple(part for identity in identities for part in identity)
    full_query = ("SELECT * FROM HygieneQueueStates", ())
    current_queries = (
        (identity_query, identity_parameters),
        ("SELECT * FROM HygieneQueueStates WHERE LastSeenAt < ?", ("2026-09-29",)),
    )

    for count in (100, 10000, 100000):
        connection.execute("DELETE FROM HygieneQueueStates")
        connection.executemany(
            "INSERT INTO HygieneQueueStates (DownloadClientId, DownloadId, LastSeenAt, ReasonCode, Strikes) "
            "VALUES (?,?,?,?,?)",
            (
                (number % 4 + 1, f"download-{number}",
                 "2026-09-01" if number % 100 == 0 else "2026-10-29", "client-failed", 3)
                for number in range(count)
            ),
        )
        connection.commit()
        print(f"states={count}, queue identities={len(identities)}, expired={count // 100}")
        for label, queries in (("full state table", (full_query,)), ("identities + expired", current_queries)):
            milliseconds, materialized = measure(connection, queries)
            plans = [connection.execute("EXPLAIN QUERY PLAN " + sql, parameters).fetchall()[0][-1]
                     for sql, parameters in queries]
            print(f"  {label}: median {milliseconds} ms; {materialized} rows materialized; plans={plans}")


if __name__ == "__main__":
    main()
