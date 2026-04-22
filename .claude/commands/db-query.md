---
description: Read-only MongoDB query tool for bug-fix / debugging. Supports dev, test, and prod (with confirmation). Never mutates data.
---

# db-query — Read-only Mongo query for bug-fix skill

Use this when you need to inspect live data while fixing a bug — e.g. reproduce a user-reported issue, check actual field values, count records, compare dev vs. test.

**This tool is READ-ONLY.** It exposes only `find`, `count`, `distinct`, `sample`, `aggregate` (with `$out`/`$merge` rejected), `list-collections`, and `schema`. There is no path to insert/update/delete.

## Choosing env

All three envs share the same Atlas cluster — only the database name differs. Names are defined in [`scripts/db-query/config.json`](../../scripts/db-query/config.json):

- `--env dev` → `InvestmentApp`
- `--env test` → `InvestmentApp_Test`
- `--env prod` → `InvestmentApp_prod` — **requires `--i-know-this-is-prod`**

Connection URI comes from `src/InvestmentApp.Api/appsettings.Development.json` (dev) or `appsettings.Production.json` (prod; gitignored, local only).

**Always match the env of the bug you're fixing.** If the user reports a bug on test, query test. Don't assume dev data matches.

**Prod guardrail:** any `--env prod` invocation WITHOUT `--i-know-this-is-prod` is refused. Before using the flag: confirm with the user that you should touch prod, and prefer a tight `--filter` + `--limit` so the query stays narrow.

## Usage

```bash
# Run from repo root
python scripts/db-query/db_query.py --env <dev|test> <op> [args...]
```

### Common ops

```bash
# List all collections in the selected DB
python scripts/db-query/db_query.py --env dev list-collections

# Find: filter (JSON), optional projection/sort/limit
python scripts/db-query/db_query.py --env dev find Trades \
  --filter '{"symbol":"VNM"}' \
  --projection '{"_id":1,"symbol":1,"quantity":1,"createdAt":1}' \
  --sort '{"createdAt":-1}' \
  --limit 10

# Count
python scripts/db-query/db_query.py --env dev count Trades --filter '{"userId":"abc"}'

# Distinct values of a field
python scripts/db-query/db_query.py --env dev distinct Trades --field symbol

# Random sample (quick schema peek)
python scripts/db-query/db_query.py --env dev sample Trades --size 3

# Aggregate (read-only stages only; $out/$merge rejected)
python scripts/db-query/db_query.py --env dev aggregate Trades \
  --pipeline '[{"$match":{"symbol":"VNM"}},{"$group":{"_id":"$action","n":{"$sum":1}}}]'

# Infer schema from a sample
python scripts/db-query/db_query.py --env dev schema Trades --sample-size 50

# Prod — requires explicit confirm flag
python scripts/db-query/db_query.py --env prod --i-know-this-is-prod \
  count users --filter '{"Email":"user@example.com"}'
```

### ObjectId / Date / EJSON

Filters are parsed with MongoDB Extended JSON. Use the `$oid` / `$date` wrappers:

```bash
--filter '{"_id":{"$oid":"507f1f77bcf86cd799439011"}}'
--filter '{"createdAt":{"$gte":{"$date":"2026-01-01T00:00:00Z"}}}'
```

## Prerequisites

First-time setup (one-shot):

```bash
python -m pip install pymongo dnspython
```

## Rules (for Claude using this skill)

- Prefer `sample` or `schema` first if you don't know the collection shape — don't dump unbounded `find`.
- Always pass `--limit` on `find`; default is 20 but smaller is better.
- Redact secrets / PII before pasting results back to the user.
- Never try to call write ops — the tool doesn't expose them and will just fail.
- If `--env test` returns zero docs where dev has data, the test DB is probably empty — say so instead of silently falling back to dev.