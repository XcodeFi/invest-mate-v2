#!/bin/bash
# Phase 5 deploy script — run AFTER PR #100 merged + Cloud Build deploy succeeded.
# Idempotent-ish: safe to re-run if a step failed mid-way.
#
# Prerequisites:
#   - gcloud authenticated (gcloud auth login)
#   - Project set (gcloud config set project <PROJECT_ID>)
#   - APIs enabled: cloudscheduler, run, iam, secretmanager
#
# Run from project root:
#   bash docs/plans/done/worker-to-scheduler-deploy.sh

set -euo pipefail

PROJECT_ID=$(gcloud config get-value project)
REGION="${REGION:-asia-southeast1}"
SCHEDULER_SA="invest-mate-scheduler@${PROJECT_ID}.iam.gserviceaccount.com"

echo "🔧 Project: $PROJECT_ID"
echo "🔧 Region:  $REGION"
echo "🔧 SA:      $SCHEDULER_SA"
echo ""

# --- Bước 2: SA + run.invoker ---
echo "▶ Step 1/5: Create scheduler service account"
gcloud iam service-accounts create invest-mate-scheduler \
  --display-name="Cloud Scheduler caller for invest-mate-api" \
  2>/dev/null || echo "  (already exists, skipping)"

echo "▶ Step 2/5: Grant run.invoker on invest-mate-api"
gcloud run services add-iam-policy-binding invest-mate-api \
  --member="serviceAccount:${SCHEDULER_SA}" \
  --role="roles/run.invoker" \
  --region="${REGION}" \
  --quiet

# --- Bước 3: Cron jobs ---
API_URL=$(gcloud run services describe invest-mate-api --region="${REGION}" --format='value(status.url)')
echo "▶ Step 3/5: Create 3 Cloud Scheduler jobs (API URL: $API_URL)"

create_job() {
  local name=$1 schedule=$2 path=$3
  gcloud scheduler jobs create http "$name" \
    --location="${REGION}" \
    --schedule="$schedule" --time-zone="UTC" \
    --uri="${API_URL}${path}" --http-method=POST \
    --oidc-service-account-email="${SCHEDULER_SA}" \
    --oidc-token-audience="${API_URL}" \
    2>/dev/null || echo "  ($name already exists, skipping)"
}

create_job invest-mate-prices        "*/15 2-7 * * 1-5" /internal/jobs/prices
create_job invest-mate-snapshot      "0 0 * * *"        /internal/jobs/snapshot
create_job invest-mate-exchange-rate "0 1 * * *"        /internal/jobs/exchange-rate

# --- Bước 4: Env vars + xoá Worker ---
echo "▶ Step 4/5: Update API env vars (allowlist + audience) — CRITICAL"
gcloud run services update invest-mate-api \
  --region="${REGION}" \
  --update-env-vars="Jobs__AllowedSchedulerSAs=${SCHEDULER_SA},Jobs__ExpectedAudience=${API_URL}" \
  --quiet

echo "▶ Step 5/5: Delete legacy invest-mate-worker service"
gcloud run services delete invest-mate-worker --region="${REGION}" --quiet \
  2>/dev/null || echo "  (worker service not found, skipping)"

# --- Smoke test ---
echo ""
echo "🔥 Smoke test: trigger prices job manually"
gcloud scheduler jobs run invest-mate-prices --location="${REGION}"

echo ""
echo "✅ Deploy complete. Verify next:"
echo "   1. Check API logs:  gcloud run services logs read invest-mate-api --region=${REGION} --limit=20"
echo "   2. Check Mongo:     stock_prices.lastUpdated should be fresh"
echo "   3. Monitor billing: GCP Console → Billing → Reports → filter Cloud Run"
echo "   4. After 7 days:    confirm vCPU-seconds < 360K/month (free tier)"
