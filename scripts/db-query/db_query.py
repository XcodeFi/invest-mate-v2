#!/usr/bin/env python3
"""Read-only MongoDB query tool for bug-fix skill.

Usage examples:
  python db_query.py --env dev list-collections
  python db_query.py --env test find Users --filter '{"email":"a@b.com"}' --limit 5
  python db_query.py --env dev count Trades --filter '{"userId":"..."}'
  python db_query.py --env dev distinct Trades --field symbol
  python db_query.py --env dev sample Trades --size 3
  python db_query.py --env dev aggregate Trades --pipeline '[{"$match":{"symbol":"VNM"}},{"$limit":5}]'
  python db_query.py --env dev schema Trades --sample-size 50
  python db_query.py --env prod --i-know-this-is-prod count users

Only read operations are exposed. `$out` / `$merge` stages in aggregate are rejected.
`--env prod` requires `--i-know-this-is-prod` to guard against accidental queries.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path
from typing import Any

from bson import ObjectId, json_util
from pymongo import MongoClient
from pymongo.read_preferences import ReadPreference

REPO_ROOT = Path(__file__).resolve().parents[2]
APPSETTINGS_DEV = REPO_ROOT / "src" / "InvestmentApp.Api" / "appsettings.Development.json"
APPSETTINGS_PROD = REPO_ROOT / "src" / "InvestmentApp.Api" / "appsettings.Production.json"
CONFIG_JSON = Path(__file__).resolve().parent / "config.json"

FORBIDDEN_AGG_STAGES = {"$out", "$merge"}


def _strip_json_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)
    text = re.sub(r"(^|[^:])//[^\n]*", lambda m: m.group(1), text)
    return text


def _load_json_loose(path: Path) -> dict:
    return json.loads(_strip_json_comments(path.read_text(encoding="utf-8")))


def load_env_config(env: str) -> tuple[str, str]:
    if not APPSETTINGS_DEV.exists():
        raise SystemExit(f"Missing {APPSETTINGS_DEV}. Expected dev connection string there.")
    app = _load_json_loose(APPSETTINGS_DEV)
    uri = (app.get("ConnectionStrings") or {}).get("MongoDb")
    dev_db = (app.get("MongoDb") or {}).get("DatabaseName")
    if not uri or not dev_db:
        raise SystemExit("appsettings.Development.json missing ConnectionStrings:MongoDb or MongoDb:DatabaseName.")

    overrides = _load_json_loose(CONFIG_JSON) if CONFIG_JSON.exists() else {}
    env_cfg = (overrides.get(env) or {})

    if env == "dev":
        return env_cfg.get("uri", uri), env_cfg.get("database", dev_db)
    if env == "test":
        return env_cfg.get("uri", uri), env_cfg.get("database", f"{dev_db}_Test")
    if env == "prod":
        if not APPSETTINGS_PROD.exists():
            raise SystemExit(
                f"Missing {APPSETTINGS_PROD}. Prod connection must live there (file is gitignored)."
            )
        prod_app = _load_json_loose(APPSETTINGS_PROD)
        prod_uri = (prod_app.get("ConnectionStrings") or {}).get("MongoDb")
        prod_db = (prod_app.get("MongoDb") or {}).get("DatabaseName")
        if not prod_uri or not prod_db:
            raise SystemExit(
                "appsettings.Production.json missing ConnectionStrings:MongoDb or MongoDb:DatabaseName."
            )
        return env_cfg.get("uri", prod_uri), env_cfg.get("database", prod_db)
    raise SystemExit(f"Unknown env '{env}'. Use 'dev', 'test', or 'prod'.")


def _parse_json_arg(raw: str | None, default: Any) -> Any:
    if raw is None:
        return default
    return json_util.loads(raw)


def _dump(doc: Any) -> str:
    return json_util.dumps(doc, indent=2, ensure_ascii=False)


def _get_client(uri: str) -> MongoClient:
    return MongoClient(uri, read_preference=ReadPreference.SECONDARY_PREFERRED, serverSelectionTimeoutMS=8000)


def cmd_list_collections(db, args) -> None:
    names = sorted(db.list_collection_names())
    print(_dump(names))


def cmd_find(db, args) -> None:
    q = _parse_json_arg(args.filter, {})
    proj = _parse_json_arg(args.projection, None)
    sort = _parse_json_arg(args.sort, None)
    cursor = db[args.collection].find(q, proj)
    if sort:
        cursor = cursor.sort(list(sort.items()) if isinstance(sort, dict) else sort)
    cursor = cursor.limit(args.limit)
    print(_dump(list(cursor)))


def cmd_count(db, args) -> None:
    q = _parse_json_arg(args.filter, {})
    print(_dump({"count": db[args.collection].count_documents(q)}))


def cmd_distinct(db, args) -> None:
    q = _parse_json_arg(args.filter, {})
    values = db[args.collection].distinct(args.field, q)
    print(_dump(values))


def cmd_sample(db, args) -> None:
    docs = list(db[args.collection].aggregate([{"$sample": {"size": args.size}}]))
    print(_dump(docs))


def cmd_aggregate(db, args) -> None:
    pipeline = _parse_json_arg(args.pipeline, [])
    if not isinstance(pipeline, list):
        raise SystemExit("--pipeline must be a JSON array of stages.")
    for stage in pipeline:
        if not isinstance(stage, dict):
            raise SystemExit("Each pipeline stage must be a JSON object.")
        for key in stage:
            if key in FORBIDDEN_AGG_STAGES:
                raise SystemExit(f"Stage {key} is forbidden (write operation).")
    cursor = db[args.collection].aggregate(pipeline)
    print(_dump(list(cursor)))


def cmd_schema(db, args) -> None:
    """Infer a lightweight schema by sampling docs."""
    sample = list(db[args.collection].aggregate([{"$sample": {"size": args.sample_size}}]))
    fields: dict[str, set[str]] = {}

    def walk(prefix: str, value: Any) -> None:
        if isinstance(value, dict):
            for k, v in value.items():
                walk(f"{prefix}.{k}" if prefix else k, v)
        else:
            fields.setdefault(prefix, set()).add(type(value).__name__)

    for doc in sample:
        walk("", doc)
    summary = {k: sorted(v) for k, v in sorted(fields.items())}
    print(_dump({"sampled": len(sample), "fields": summary}))


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Read-only MongoDB query tool (dev/test/prod).")
    p.add_argument("--env", choices=["dev", "test", "prod"], required=True, help="Which database to hit.")
    p.add_argument(
        "--i-know-this-is-prod",
        action="store_true",
        help="Required confirmation flag when --env prod. Without it, prod queries are refused.",
    )
    sub = p.add_subparsers(dest="op", required=True)

    sub.add_parser("list-collections", help="List all collections.")

    f = sub.add_parser("find", help="Find documents.")
    f.add_argument("collection")
    f.add_argument("--filter", help="JSON filter. Default {}.")
    f.add_argument("--projection", help="JSON projection.")
    f.add_argument("--sort", help='JSON sort, e.g. {"createdAt": -1}')
    f.add_argument("--limit", type=int, default=20)

    c = sub.add_parser("count", help="Count documents.")
    c.add_argument("collection")
    c.add_argument("--filter", help="JSON filter. Default {}.")

    d = sub.add_parser("distinct", help="Distinct values for a field.")
    d.add_argument("collection")
    d.add_argument("--field", required=True)
    d.add_argument("--filter", help="JSON filter. Default {}.")

    s = sub.add_parser("sample", help="Random sample documents.")
    s.add_argument("collection")
    s.add_argument("--size", type=int, default=3)

    a = sub.add_parser("aggregate", help="Run an aggregation pipeline (read-only stages).")
    a.add_argument("collection")
    a.add_argument("--pipeline", required=True, help="JSON array of stages.")

    sc = sub.add_parser("schema", help="Infer field names/types from a sample.")
    sc.add_argument("collection")
    sc.add_argument("--sample-size", type=int, default=50)

    return p


HANDLERS = {
    "list-collections": cmd_list_collections,
    "find": cmd_find,
    "count": cmd_count,
    "distinct": cmd_distinct,
    "sample": cmd_sample,
    "aggregate": cmd_aggregate,
    "schema": cmd_schema,
}


def main(argv: list[str] | None = None) -> int:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")
    args = build_parser().parse_args(argv)

    if args.env == "prod" and not args.i_know_this_is_prod:
        raise SystemExit(
            "Refusing to query PROD without --i-know-this-is-prod. "
            "If you truly intend to query production, re-run with that flag."
        )
    if args.env != "prod" and args.i_know_this_is_prod:
        raise SystemExit("--i-know-this-is-prod is only valid with --env prod.")

    uri, database = load_env_config(args.env)
    if args.env == "prod":
        print("!" * 60, file=sys.stderr)
        print("! WARNING: querying PRODUCTION. Read-only, but data is LIVE.", file=sys.stderr)
        print("!" * 60, file=sys.stderr)
    print(f"[db-query] env={args.env} database={database}", file=sys.stderr)
    client = _get_client(uri)
    try:
        db = client[database]
        HANDLERS[args.op](db, args)
    finally:
        client.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
