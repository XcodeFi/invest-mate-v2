# Phase 5 — Worker → Scheduler migration (bản thực tế)

**Trạng thái:** ✅ Đã thực hiện 2026-04-26 ~08:20 UTC (15:20 ICT) qua Cloud Shell.
**Người thực hiện:** phdfieldkidpro@gmail.com
**Project:** `project-56d530ac-03df-4242-adb` ("My First Project")

> Bản này thay thế `worker-to-scheduler-deploy.md` (runbook gốc) — đã adapt cho service/region thực tế và ghi nhận kết quả từng bước.

---

## Tóm tắt khác biệt so với runbook gốc

| Item | Runbook gốc | Thực tế |
|---|---|---|
| API service | `invest-mate-api` | **`invest-mate-v2-bkk`** |
| Service region | `asia-southeast1` | **`asia-southeast3`** (Bangkok) |
| Scheduler region | `asia-southeast1` | **`asia-southeast1`** (BKK chưa support Scheduler → fallback Singapore) |
| Worker service | Có `invest-mate-worker` cần xoá | **Không tồn tại** → skip Step 5 |
| Auth model | Switch sang IAM-required + grant `run.invoker` | **Giữ public**, chỉ enforce ở app-level allowlist → skip Step 2 |
| Cách chạy | gcloud từ máy local | **Cloud Shell** trong browser |

---

## Variables đã dùng

```bash
export PROJECT_ID="project-56d530ac-03df-4242-adb"
export REGION="asia-southeast3"               # Cloud Run region
export SCHED_REGION="asia-southeast1"         # Cloud Scheduler region (BKK chưa support)
export SERVICE="invest-mate-v2-bkk"
export SCHEDULER_SA="invest-mate-scheduler@${PROJECT_ID}.iam.gserviceaccount.com"
export API_URL="https://invest-mate-v2-bkk-567557889111.asia-southeast3.run.app"
```

---

## Step 1 — Enable APIs ✅

```bash
gcloud services enable cloudscheduler.googleapis.com run.googleapis.com iam.googleapis.com
```

**Output:**
```
Operation "operations/acf.p2-567557889111-154184b6-215e-476e-a6f9-f4352695fbdf" finished successfully.
```

---

## Step 2 — Tạo Scheduler service account ✅

```bash
gcloud iam service-accounts create invest-mate-scheduler \
  --display-name="Cloud Scheduler caller for invest-mate-v2-bkk"
```

**Output:**
```
Created service account [invest-mate-scheduler].
```

→ SA email: `invest-mate-scheduler@project-56d530ac-03df-4242-adb.iam.gserviceaccount.com`

---

## ~~Step 3 — Grant `run.invoker`~~ (SKIPPED)

Service `invest-mate-v2-bkk` đang public (`run.googleapis.com/invoker-iam-disabled: "true"`). Cloud Run không enforce IAM, nên không cần `roles/run.invoker`. Scheduler vẫn gửi OIDC token (do flag `--oidc-service-account-email` + `--oidc-token-audience`), và **app-level middleware** sẽ verify JWT.

> ⚠ Trade-off: endpoint `/internal/jobs/*` exposed public. Bảo vệ duy nhất là middleware kiểm `email` + `aud` claim. Xem **Risks** ở cuối.

---

## Step 4 — Set API env vars 🚨 (CRITICAL) ✅

```bash
gcloud run services update $SERVICE \
  --region=$REGION \
  --update-env-vars="Jobs__AllowedSchedulerSAs=${SCHEDULER_SA},Jobs__ExpectedAudience=${API_URL}"
```

**Output:**
```
Deploying...
Creating Revision....................done
Routing traffic.....done
Done.
Service [invest-mate-v2-bkk] revision [invest-mate-v2-bkk-00041-knk] has been deployed and is serving 100 percent of traffic.
Service URL: https://invest-mate-v2-bkk-567557889111.asia-southeast3.run.app
```

→ Revision mới: `invest-mate-v2-bkk-00041-knk` (generation 41).

---

## Step 5 — Tạo 3 Cloud Scheduler jobs ✅

### 5.1 Prices job (mỗi 15 phút giờ chợ VN T2–T6)

```bash
gcloud scheduler jobs create http invest-mate-prices \
  --location=$SCHED_REGION \
  --schedule="*/15 2-7 * * 1-5" --time-zone="UTC" \
  --uri="${API_URL}/internal/jobs/prices" --http-method=POST \
  --oidc-service-account-email=$SCHEDULER_SA \
  --oidc-token-audience="${API_URL}"
```

→ `state: ENABLED`, `timeZone: UTC`, created `2026-04-26T08:19:39Z`.

### 5.2 Snapshot job (07:00 ICT daily = 00:00 UTC)

```bash
gcloud scheduler jobs create http invest-mate-snapshot \
  --location=$SCHED_REGION \
  --schedule="0 0 * * *" --time-zone="UTC" \
  --uri="${API_URL}/internal/jobs/snapshot" --http-method=POST \
  --oidc-service-account-email=$SCHEDULER_SA \
  --oidc-token-audience="${API_URL}"
```

→ `state: ENABLED`, created `2026-04-26T08:19:41Z`.

### 5.3 Exchange-rate job (08:00 ICT daily = 01:00 UTC)

```bash
gcloud scheduler jobs create http invest-mate-exchange-rate \
  --location=$SCHED_REGION \
  --schedule="0 1 * * *" --time-zone="UTC" \
  --uri="${API_URL}/internal/jobs/exchange-rate" --http-method=POST \
  --oidc-service-account-email=$SCHEDULER_SA \
  --oidc-token-audience="${API_URL}"
```

→ `state: ENABLED`, created `2026-04-26T08:19:52Z`.

### Verify

```bash
gcloud scheduler jobs list --location=$SCHED_REGION
```

```
ID                          SCHEDULE          STATE
invest-mate-prices          */15 2-7 * * 1-5  ENABLED
invest-mate-snapshot        0 0 * * *         ENABLED
invest-mate-exchange-rate   0 1 * * *         ENABLED
```

---

## ~~Step 6 — Xoá Worker~~ (SKIPPED)

Service `invest-mate-worker` không tồn tại trong project — chưa từng được deploy hoặc đã bị xoá trước đó. Không có gì để clean up.

Services hiện tại trong project:
- `invest-mate-v2-bkk` (asia-southeast3) — API
- `invest-mate-v2-git` (europe-west1, internal ingress) — service phụ

---

## Step 7 — Smoke test ✅

### Trigger prices manually

```bash
gcloud scheduler jobs run invest-mate-prices --location=$SCHED_REGION
```

### Verify từ Scheduler describe

```bash
gcloud scheduler jobs describe invest-mate-prices --location=$SCHED_REGION \
  --format='value(lastAttemptTime,state,status)'
```

```
lastAttemptTime: '2026-04-26T08:21:07.846552Z'
state: ENABLED
status: {}
```

→ `status: {}` (empty object) = **OK** (không có error).

### Verify từ API log

```bash
gcloud logging read 'resource.type="cloud_run_revision"
  AND resource.labels.service_name="invest-mate-v2-bkk"
  AND (httpRequest.requestUrl:"/internal/jobs/prices" OR jsonPayload.message:"InternalJobs")' \
  --limit=20 --freshness=5m
```

```
2026-04-26T08:21:05.158809Z  [INF] InternalJobs.prices triggered
2026-04-26T08:21:02.824699Z  200  https://invest-mate-v2-bkk-567557889111.asia-southeast3.run.app/internal/jobs/prices
```

→ HTTP **200** + log `InternalJobs.prices triggered` = middleware accept request, handler thực thi xong.

---

## Verify env vars persist

```bash
gcloud run services describe $SERVICE --region=$REGION \
  --format='value(spec.template.spec.containers[0].env)' | tr ',' '\n' | grep Jobs
```

```
Jobs__AllowedSchedulerSAs=invest-mate-scheduler@project-56d530ac-03df-4242-adb.iam.gserviceaccount.com
Jobs__ExpectedAudience=https://invest-mate-v2-bkk-567557889111.asia-southeast3.run.app
```

✅ Cả 2 đều có mặt trên revision đang serve.

---

## Pending verification (cần làm sau)

### 1. 24h check — confirm `snapshot` & `exchange-rate` chạy đúng giờ

Sau **00:00 UTC mai (27/04)** = 07:00 ICT, cả 3 jobs đều có ít nhất 1 attempt:

```bash
gcloud scheduler jobs list --location=asia-southeast1 \
  --format='table(name.basename(),lastAttemptTime,state,status.code)'
```

Mong đợi: `status.code` = `OK` (hoặc empty) cho cả 3.

### 2. Middleware enforce JWT thật sự không?

🚨 **Quan trọng:** Smoke test ra 200 + log "InternalJobs.prices triggered" chứng tỏ request vào được handler. **Nhưng chưa chứng minh middleware từ chối request không có JWT.**

Test thủ công từ Cloud Shell hoặc local:

```bash
curl -X POST -i $API_URL/internal/jobs/prices
```

**Mong đợi:** `401 Unauthorized` hoặc `403 Forbidden`.

**Nếu ra `200`** → middleware bị bypass → `/internal/jobs/*` đang public hoàn toàn → ai cũng có thể spam → **cần fix gấp**.

### 3. Monitor billing 7 ngày

GCP Console → Billing → Reports → filter `service = Cloud Run`, time range = 7 days.

Free tier:
- vCPU-seconds < 360K/month
- Memory GiB-seconds < 180K/month
- Requests < 2M/month

Cloud Scheduler free tier = 3 jobs (vừa đủ, không phát sinh chi phí).

---

## Rollback (nếu sự cố)

### Pause cron (giữ config, không xoá)

```bash
for job in invest-mate-prices invest-mate-snapshot invest-mate-exchange-rate; do
  gcloud scheduler jobs pause "$job" --location=asia-southeast1
done
```

### Xoá hoàn toàn

```bash
for job in invest-mate-prices invest-mate-snapshot invest-mate-exchange-rate; do
  gcloud scheduler jobs delete "$job" --location=asia-southeast1 --quiet
done
gcloud iam service-accounts delete $SCHEDULER_SA --quiet
gcloud run services update invest-mate-v2-bkk --region=asia-southeast3 \
  --remove-env-vars=Jobs__AllowedSchedulerSAs,Jobs__ExpectedAudience
```

→ ~3 phút.

### Khôi phục Worker (nếu workload cần background)

Service `invest-mate-worker` không có image cũ → **không thể redeploy nhanh**. Phải build lại từ source. Cân nhắc nếu workload không thể chuyển sang Scheduler → cần kế hoạch riêng.

---

## Risks còn lại

| Risk | Mức | Mitigation |
|---|---|---|
| Middleware không thật sự verify JWT (chỉ check existence) | 🔴 High | Manual `curl` test (mục Pending #2). Nếu fail, fix code + redeploy. |
| `/internal/jobs/*` exposed public | 🟡 Medium | App-level allowlist là defense duy nhất. Cân nhắc rate-limit hoặc switch IAM-required. |
| Scheduler ↔ Cloud Run cross-region (Singapore → Bangkok) | 🟢 Low | ~25ms latency, không ảnh hưởng cron 15-min interval. |
| Free tier exceeded | 🟢 Low | 3 jobs (limit 3). Snapshot + exchange-rate chỉ chạy 1 lần/ngày, prices ~30 lần/ngày × 22 working days ≈ 660 invocations/tháng — far below 2M requests free tier. |

---

## Step 8 — (Optional) Cron warmup để giảm cold-start

**Bối cảnh** (Bug C audit 2026-04-26): `--min-instances=0` → idle 15' → scale 0 → cold-start 5–10s gây "FE treo" khi user mở dashboard sau giờ nghỉ. Cron warmup ping `/health/live` mỗi ~14' giữ container ấm trong khung giờ làm việc, ngoài giờ chấp nhận cold-start.

**Lưu ý:** thêm job thứ 4 vượt free tier Scheduler (3 jobs) → ~$0.10/tháng.

```bash
# Use /health/live (not /health) — chỉ trả 200, KHÔNG ping Mongo → không tốn DB connection
gcloud scheduler jobs create http invest-mate-warmup \
  --location=$SCHED_REGION \
  --schedule="*/14 1-8 * * 1-5" --time-zone="UTC" \
  --uri="${API_URL}/health/live" \
  --http-method=GET \
  --attempt-deadline=30s
```

→ Cron `*/14 1-8 * * 1-5 UTC` = mỗi 14' từ 08:00–15:59 ICT, T2–T6. Tránh đụng `*/15` của prices job (offset 1 phút).

**Verify:**
```bash
# Sau ~15 phút, check Scheduler đã chạy
gcloud scheduler jobs describe invest-mate-warmup --location=$SCHED_REGION \
  --format='value(lastAttemptTime,state,status.code)'

# Check API log có warmup hits
gcloud logging read 'resource.type="cloud_run_revision"
  AND resource.labels.service_name="invest-mate-v2-bkk"
  AND httpRequest.requestUrl:"/health/live"' --limit=5 --freshness=20m
```

**Nếu user không complain cold-start nữa, có thể skip step này** — accept cold-start để giữ free tier hoàn toàn.

---

## Files / References

- ADR gốc: `docs/adr/0001-worker-to-scheduler.md`
- Plan gốc: `docs/plans/done/worker-to-scheduler-migration.md`
- Runbook gốc: `docs/plans/done/worker-to-scheduler-deploy.md`
- PR: #100
- Cloud Run service: [invest-mate-v2-bkk console](https://console.cloud.google.com/run/detail/asia-southeast3/invest-mate-v2-bkk?project=project-56d530ac-03df-4242-adb)
- Cloud Scheduler: [jobs list](https://console.cloud.google.com/cloudscheduler?project=project-56d530ac-03df-4242-adb)
- External:
  - [Cloud Scheduler with OIDC for Cloud Run](https://cloud.google.com/run/docs/triggering/using-scheduler)
  - [Cloud Scheduler available locations](https://cloud.google.com/scheduler/docs/locations)