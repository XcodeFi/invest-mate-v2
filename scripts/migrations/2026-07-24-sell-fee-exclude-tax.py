#!/usr/bin/env python3
"""Migration: stop double-counting TNCN tax on historical SELL trades (ADR-0006).

Buggy producers stored Trade.Fee = transactionFee + TNCN tax, while Tax also holds the
TNCN. Every net consumer does `- Fee - Tax` on sells → tax subtracted twice. Fix persists
Fee = transactionFee only going forward; this migration corrects existing rows:

    Fee := Fee - Tax   (for SELL trades whose Fee still includes the tax)

Idempotency guard: only rows where feeRate = Fee/(Qty*Price) > 0.2% are touched. Buggy
sells sit at ~0.25% (0.15% broker + 0.1% tax); once corrected they drop to ~0.15% and are
excluded, so re-running is a no-op. (Assumes the 0.15% broker tier — true for all current
candidates; verify the dry-run shows a uniform ~0.25% feeRate before applying to bigger tiers.)

DRY-RUN by default. To write:  python <script> --env prod --i-know-this-is-prod --apply
"""
from __future__ import annotations

import argparse
import importlib.util
import sys
from pathlib import Path

from bson import json_util
from pymongo import MongoClient

# Reuse the connection loader from the read-only db-query tool.
_DBQ = Path(__file__).resolve().parents[1] / "db-query" / "db_query.py"
_spec = importlib.util.spec_from_file_location("db_query", _DBQ)
_db_query = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_db_query)

# SELL, has tax, and Fee still includes the tax (feeRate > 0.2%). $expr computes the ratio
# from Decimal128 fields via $toDouble.
FILTER = {
    "TradeType": 1,
    "Tax": {"$gt": 0},
    "$expr": {
        "$gt": [
            {"$toDouble": "$Fee"},
            {"$multiply": [0.002, {"$multiply": [{"$toDouble": "$Quantity"}, {"$toDouble": "$Price"}]}]},
        ]
    },
}
UPDATE = [{"$set": {"Fee": {"$subtract": ["$Fee", "$Tax"]}}}]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--env", required=True, choices=["dev", "test", "prod"])
    ap.add_argument("--i-know-this-is-prod", action="store_true", dest="prod_ok")
    ap.add_argument("--apply", action="store_true", help="perform the write (default: dry-run)")
    args = ap.parse_args()

    if args.env == "prod" and not args.prod_ok:
        raise SystemExit("Refusing prod without --i-know-this-is-prod.")

    uri, dbname = _db_query.load_env_config(args.env)
    client = MongoClient(uri)
    coll = client[dbname]["trades"]

    matches = list(coll.find(FILTER, {"Symbol": 1, "Quantity": 1, "Price": 1, "Fee": 1, "Tax": 1}))
    print(f"[{args.env}] {dbname}.trades - {len(matches)} SELL row(s) with Fee still including tax:")
    for d in matches:
        amt = float(d["Quantity"].to_decimal()) * float(d["Price"].to_decimal())
        fee = float(d["Fee"].to_decimal())
        tax = float(d["Tax"].to_decimal())
        print(f"  {d['_id']} {d.get('Symbol'):>6}  amount={amt:,.0f}  Fee {fee:,.2f} -> {fee - tax:,.2f}  (Tax {tax:,.2f})")

    if not args.apply:
        print("\nDRY-RUN — no write. Backup of matched docs below; re-run with --apply to migrate.")
        Path(__file__).with_suffix(".backup.json").write_text(json_util.dumps(matches, indent=2), encoding="utf-8")
        print(f"Backup written to {Path(__file__).with_suffix('.backup.json').name}")
        return

    res = coll.update_many(FILTER, UPDATE)
    print(f"\nAPPLIED: matched={res.matched_count} modified={res.modified_count}")

    # Verify: every SELL Tax>0 should now sit at ~0.15% feeRate.
    bad = coll.count_documents(FILTER)
    print(f"Post-check: rows still matching (should be 0): {bad}")


if __name__ == "__main__":
    main()
