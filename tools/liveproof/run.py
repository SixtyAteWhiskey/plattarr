#!/usr/bin/env python3
"""Live proof against a running Plattarr instance, over its HTTP API. See README.md.

    python3 tools/liveproof/run.py smoke covers
    python3 tools/liveproof/run.py baseline --expect-baseline 71,57,64
    python3 tools/liveproof/run.py fingerprint
    python3 tools/liveproof/run.py restore
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import checks  # noqa: E402
import hygiene_recovery  # noqa: E402
from client import Plattarr  # noqa: E402
from journal import Journal  # noqa: E402

ALL = ("smoke", "covers", "baseline", "defaults", "fingerprint", "hygiene", "hygiene-recovery", "restore")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("checks", nargs="+", choices=ALL, metavar="check", help="one or more of: " + ", ".join(ALL))
    parser.add_argument("--expect-baseline", metavar="TOTAL,APPROVED,MAPPED", help="compare the release search to these counts")
    parser.add_argument("--baseline-movie", type=int, default=1, help="movie id for the baseline release search (default 1)")
    parser.add_argument("--allow-health", default="", metavar="SOURCE,SOURCE", help="health sources that may be present")
    parser.add_argument("--allow-missing-covers", action="store_true", help="report cover files not on disk as a warning")
    parser.add_argument("--confirm-disposable", action="store_true",
                        help="confirm that hygiene-recovery may modify this disposable localhost instance")
    args = parser.parse_args()

    api = Plattarr()
    journal = Journal()
    report = checks.Report()

    if journal.entries and "restore" not in args.checks:
        print(f"WARN  the journal {journal.path} holds {len(journal.entries)} unrestored change(s) from an earlier run; "
              "run `restore` first")

    for name in args.checks:
        print(f"== {name}")
        if name == "smoke":
            checks.smoke(api, report, allow_health=tuple(s for s in args.allow_health.split(",") if s))
        elif name == "covers":
            checks.covers(api, report, allow_missing=args.allow_missing_covers)
        elif name == "baseline":
            expect = [int(n) for n in args.expect_baseline.split(",")] if args.expect_baseline else None
            checks.baseline(api, report, expect, args.baseline_movie)
        elif name == "defaults":
            checks.defaults(api, report)
        elif name == "fingerprint":
            checks.fingerprint(api, report, journal)
        elif name == "hygiene":
            checks.hygiene(api, report)
        elif name == "hygiene-recovery":
            hygiene_recovery.run(api, report, confirmed=args.confirm_disposable)
        elif name == "restore":
            failures = journal.restore(api)
            report.check(failures == 0, "restore", "journal empty" if not failures else "some entries could not be restored")

    print(f"== {'PASS' if report.failures == 0 else 'FAIL'} ({report.failures} failure(s))")
    return 1 if report.failures else 0


if __name__ == "__main__":
    sys.exit(main())
