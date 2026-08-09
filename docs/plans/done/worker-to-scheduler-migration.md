# Migrate Worker → API endpoints + Cloud Scheduler (free-tier friendly)

**Status:** Done — 2026-04-26 (Phases 1–4, 6 code-complete; Phase 5 = manual gcloud setup, awaiting deploy)
**Trigger:** Cloud Run free tier overrun on always-on Worker service
**Related ADR:** [`docs/adr/0001-worker-to-scheduler.md`](../../adr/0001-worker-to-scheduler.md)

---

## 1. Problem

`invest-mate-worker` đang deploy với `--min-instances=1 --no-cpu-throttling` ([cloudbuild.yaml:158-163](../../../cloudbuild.yaml#L158-L163)) → chạy 24/7.

**Chi phí thực tế:**
- 1 vCPU × 86.400s × 30 ngày ≈ **2,6M vCPU-seconds/tháng**
- Cloud Run free tier: **360K vCPU-seconds/tháng**
- → Vượt ~7× free tier, trong khi workload thật sự chỉ ~5–10 phút/ngày.

**Workload Worker hiện tại:**

| Job | Tần suất | Thời gian/lần |
|---|---|---|
| `Worker.cs` (snapshot + prices + scenario + tokens) | Mỗi 15 phút (24/7) | ~5–15s |
| `PriceSnapshotJob` | Mỗi 15 phút giờ chợ VN (02:00–08:00 UTC, T2–T6) | ~5–10s |
| `ExchangeRateJob` | 01:00 UTC mỗi ngày | ~2s |
| `BacktestJob` | Poll 10s, chạy khi có Pending (on-demand) | Variable |

→ Idle ~99% thời gian. Always-on = lãng phí.

---

## 2. Goal & success criteria

**Mục tiêu:** Trigger được tất cả job hiện tại, **chi phí Cloud Run ≤ free tier**, không degrade chức năng.

**Success criteria:**

1. Cloud Run free tier (vCPU-seconds, requests, GB-seconds) cho Investment Mate **không vượt** trong 30 ngày sau migration.
2. `PriceSnapshotJob` vẫn chạy đúng cadence (15 phút trong giờ chợ VN T2–T6) → kiểm tra qua `stock_prices.lastUpdated` không cũ hơn 16 phút trong giờ chợ.
3. `ExchangeRateJob` chạy 1 lần/ngày → kiểm tra qua `exchange_rates.lastUpdated`.
4. `SnapshotService.TakeAllSnapshotsAsync` chạy ≥ 1 lần/ngày → `portfolio_snapshots` có doc mới mỗi ngày.
5. `BacktestJob`: backtest Pending của user được pick up trong < 60s sau khi submit.
6. Endpoint chỉ Cloud Scheduler (OIDC) hoặc internal call gọi được — không lộ public.

---

## 3. Approach

### 3.1 Architecture diagram

**Trước:**
```
[invest-mate-api]   ←— public traffic
[invest-mate-worker] ←— always-on, BackgroundService loop (idle 99%)
[invest-mate-frontend]
```

**Sau:**
```
[Cloud Scheduler] ──OIDC POST──▶ [invest-mate-api]
   prices-job   (*/15 2-7 * * 1-5 UTC)   /internal/jobs/prices
   exchange    (0 1 * * *)                /internal/jobs/exchange-rate
   snapshot    (0 0 * * *)                /internal/jobs/snapshot

[invest-mate-api] (1 service)
   ├── Public API (auth: JWT)
   ├── /internal/jobs/* (auth: OIDC service-account)
   └── On-demand backtest trigger inline (< 60s) hoặc Cloud Run Job nếu lâu
```

### 3.2 Components

**(a) New API endpoints** — `Controllers/InternalJobsController.cs`:

| Endpoint | Calls | Auth |
|---|---|---|
| `POST /internal/jobs/snapshot` | `ISnapshotService.TakeAllSnapshotsAsync` | OIDC `[Authorize(Policy="GcpScheduler")]` |
| `POST /internal/jobs/prices` | Logic của `PriceSnapshotJob.RunAsync` | OIDC |
| `POST /internal/jobs/exchange-rate` | Logic của `ExchangeRateJob.RunAsync` | OIDC |
| `POST /internal/jobs/scenario-eval` | `IScenarioEvaluationService.EvaluateAllAsync` | OIDC |

Idempotent — gọi nhiều lần không gây side effect (snapshot service đã check `existing snapshot for today` rồi).

**(b) OIDC auth middleware** — verify Google-issued ID token from Scheduler:

- Add 2nd JWT bearer scheme `"GcpOidc"`: validate issuer `https://accounts.google.com`, audience = API base URL, signing keys từ Google JWKS endpoint.
- Authorization policy `"GcpScheduler"`: require scheme `GcpOidc` + email claim ∈ allowlist `Jobs:AllowedSchedulerSAs`.
- Default scheme JWT (user) vẫn không đổi — chỉ khi `[Authorize(AuthenticationSchemes="GcpOidc",Policy="GcpScheduler")]` thì mới chạy OIDC validation.

**(c) Backtest job** — chuyển từ poll → on-demand:

Hiện tại: `BacktestsController.Create` lưu `Status=Pending`, Worker poll 10s và chạy.

Sau: `BacktestsController.Create` → push background task chạy ngay trong API process (Hangfire-style thuần `Task.Run` + scope, hoặc `IHostedService` queue trong API).

**Vấn đề:** Cloud Run instance có thể bị scale down giữa chừng → backtest đang chạy bị kill.

**Mitigation:**
- Set `--cpu-always-allocated` cho API service để background task không bị paused (Cloud Run cho phép). Vẫn miễn phí khi không có request.
- Hoặc: keep Worker service NHƯNG set `--min-instances=0`, dùng Cloud Run Jobs (`gcloud run jobs execute backtest-job`) trigger từ API khi có Pending. Phức tạp hơn nhưng tách biệt hơn.

→ **Khuyến nghị:** Phase 1 dùng inline `Task.Run` trong API + `cpu-always-allocated`. Nếu sau này backtest > 60s thường xuyên, migrate sang Cloud Run Jobs (Phase out-of-scope).

**(d) Cloud Scheduler config** — chạy bằng `gcloud` CLI 1 lần (không qua cloudbuild):

```bash
SA_EMAIL=invest-mate-scheduler@${PROJECT_ID}.iam.gserviceaccount.com
API_URL=https://invest-mate-api-${HASH}-${REGION}.a.run.app

# Service account chuyên cho Scheduler
gcloud iam service-accounts create invest-mate-scheduler \
  --display-name="Cloud Scheduler caller for invest-mate-api"

gcloud run services add-iam-policy-binding invest-mate-api \
  --member="serviceAccount:${SA_EMAIL}" \
  --role="roles/run.invoker" \
  --region=${REGION}

# Jobs
gcloud scheduler jobs create http invest-mate-prices \
  --schedule="*/15 2-7 * * 1-5" --time-zone="UTC" \
  --uri="${API_URL}/internal/jobs/prices" --http-method=POST \
  --oidc-service-account-email=${SA_EMAIL} \
  --oidc-token-audience="${API_URL}"

gcloud scheduler jobs create http invest-mate-exchange-rate \
  --schedule="0 1 * * *" --time-zone="UTC" \
  --uri="${API_URL}/internal/jobs/exchange-rate" --http-method=POST \
  --oidc-service-account-email=${SA_EMAIL} \
  --oidc-token-audience="${API_URL}"

gcloud scheduler jobs create http invest-mate-snapshot \
  --schedule="0 0 * * *" --time-zone="UTC" \
  --uri="${API_URL}/internal/jobs/snapshot" --http-method=POST \
  --oidc-service-account-email=${SA_EMAIL} \
  --oidc-token-audience="${API_URL}"
```

3 jobs vừa khít free tier Scheduler (3 free).

**(e) cloudbuild.yaml changes:**

- ❌ Xoá build-worker, push-worker, deploy-worker steps.
- ❌ Xoá `Dockerfile.worker` và `src/InvestmentApp.Worker/` project.
- ✅ API service: thêm `--cpu-always-allocated` (cho background backtest task).

**(f) BackgroundService classes — fate:**

| Class | Action |
|---|---|
| `Worker.cs` | Delete |
| `PriceSnapshotJob.cs` | **Extract logic vào `IPriceSnapshotJobService`** (Infrastructure), gọi từ controller. Delete BackgroundService class. |
| `ExchangeRateJob.cs` | Same — extract → `IExchangeRateJobService`. |
| `BacktestJob.cs` | Convert thành `BacktestQueueService` (singleton, queue + worker task) trong API. |

---

## 4. Implementation phases

### Phase 1 — Extract job logic vào services (TDD)

1. Create `IPriceSnapshotJobService` + `PriceSnapshotJobService` (Infrastructure). Move logic từ `PriceSnapshotJob.cs`. Tests in `Infrastructure.Tests/Services/PriceSnapshotJobServiceTests.cs`.
2. Same cho `IExchangeRateJobService`. Tests.
3. Same cho `IScenarioEvalJobService` (gói `IScenarioEvaluationService.EvaluateAllAsync`).
4. **Verify:** `dotnet test` xanh.

### Phase 2 — OIDC middleware + InternalJobsController (TDD)

1. Add NuGet: `Microsoft.AspNetCore.Authentication.JwtBearer` đã có. Thêm config block `GcpOidc` scheme.
2. Add `Jobs:AllowedSchedulerSAs` to `appsettings.json` (placeholder), env var `Jobs__AllowedSchedulerSAs` set lúc deploy.
3. Add `InternalJobsController` với 4 endpoints, all `[Authorize(AuthenticationSchemes="GcpOidc",Policy="GcpScheduler")]`.
4. Tests in `InvestmentApp.Api.Tests` (or integration test) — call endpoint without OIDC → 401; with valid mock OIDC → 200; with email không trong allowlist → 403.
5. **Verify:** `dotnet test` + manual `curl localhost` (without auth → 401).

### Phase 3 — BacktestQueueService trong API

1. Create `BacktestQueueService : BackgroundService, IBacktestQueue` trong `InvestmentApp.Api` (singleton, in-memory `Channel<string>`).
2. `BacktestsController.Create` → enqueue `backtestId` thay vì rely on Worker poll.
3. Background loop: dequeue → `BacktestEngine.Run`.
4. Tests: queue picks up enqueued items; multiple enqueues processed sequentially.
5. **Verify:** Manual test on dev — submit backtest, log shows `BacktestQueueService picked up X within < 5s`.

### Phase 4 — Delete Worker project + cloudbuild update

1. Delete `src/InvestmentApp.Worker/` project + `Dockerfile.worker`.
2. Remove from `InvestmentApp.sln`.
3. Update `cloudbuild.yaml` — strip worker steps, add `--cpu-always-allocated` to API.
4. **Verify:** `dotnet build` succeeds, `dotnet test` xanh.

### Phase 5 — Deploy + Cloud Scheduler setup

1. Set env var `Jobs__AllowedSchedulerSAs=invest-mate-scheduler@...iam.gserviceaccount.com` cho API service trong `cloudbuild.yaml`.
2. Trigger Cloud Build deploy → API có endpoints mới, Worker service tự xoá khỏi GCP (user xoá manual hoặc `gcloud run services delete invest-mate-worker`).
3. Run `gcloud scheduler jobs create ...` cho 3 jobs (block 3.2(d)).
4. **Verify:**
   - `gcloud scheduler jobs run invest-mate-prices` → check `stock_prices` updated.
   - 24h sau: check `exchange_rates`, `portfolio_snapshots`, `stock_prices` đều fresh.
5. Monitor billing 7 ngày → confirm under free tier.

### Phase 6 — Docs + cleanup

1. Update [`docs/architecture.md`](../../architecture.md) — bỏ `InvestmentApp.Worker` khỏi service list, thêm Internal Jobs section.
2. Update [`docs/business-domain.md`](../../business-domain.md) nếu cần (job docs).
3. Mark this plan done, move to `docs/plans/done/`.
4. Update [`frontend/src/assets/CHANGELOG.md`](../../../frontend/src/assets/CHANGELOG.md) — Internal infra change, no user-facing UI change.

---

## 5. Files touched

**Backend:**
- `src/InvestmentApp.Infrastructure/Services/PriceSnapshotJobService.cs` *(new)*
- `src/InvestmentApp.Infrastructure/Services/ExchangeRateJobService.cs` *(new)*
- `src/InvestmentApp.Application/Common/Interfaces/IPriceSnapshotJobService.cs` *(new)*
- `src/InvestmentApp.Application/Common/Interfaces/IExchangeRateJobService.cs` *(new)*
- `src/InvestmentApp.Api/Controllers/InternalJobsController.cs` *(new)*
- `src/InvestmentApp.Api/Auth/GcpOidcExtensions.cs` *(new — register scheme + policy)*
- `src/InvestmentApp.Api/Services/BacktestQueueService.cs` *(new)*
- `src/InvestmentApp.Api/Controllers/BacktestsController.cs` *(modify — enqueue)*
- `src/InvestmentApp.Api/Program.cs` *(modify — register OIDC scheme, BacktestQueue, internal services)*
- `src/InvestmentApp.Api/appsettings.json` *(modify — add `Jobs:AllowedSchedulerSAs` placeholder)*
- `src/InvestmentApp.Worker/` *(delete entire project)*
- `Dockerfile.worker` *(delete)*
- `InvestmentApp.sln` *(modify — remove Worker project)*

**Tests:**
- `tests/InvestmentApp.Infrastructure.Tests/Services/PriceSnapshotJobServiceTests.cs` *(new)*
- `tests/InvestmentApp.Infrastructure.Tests/Services/ExchangeRateJobServiceTests.cs` *(new)*
- Optionally: `tests/InvestmentApp.Api.Tests/InternalJobsControllerTests.cs` *(new — auth tests)*

**Infra:**
- `cloudbuild.yaml` *(modify)*
- `gcloud scheduler` commands run manually (one-time setup)

**Docs:**
- `docs/adr/0001-worker-to-scheduler.md` *(new)*
- `docs/architecture.md` *(modify)*
- `docs/plans/done/worker-to-scheduler-migration.md` *(this file, moved when done)*
- `frontend/src/assets/CHANGELOG.md` *(modify)*

---

## 6. Out of scope

- Replace polling-based BacktestJob with Cloud Tasks queue or Cloud Run Jobs — Phase 1 dùng inline queue, đủ với 1 user. Khi cần scale, migrate riêng.
- Migration tool config Scheduler từ Terraform / Pulumi — manual `gcloud` đủ với scope hiện tại.
- Health check endpoint riêng cho Scheduler — Cloud Scheduler retry khi 5xx, log trên GCP. Phase 2 nếu thấy cần.
- Multi-region deployment.

---

## 7. Risks

| Risk | Mitigation |
|---|---|
| **OIDC validation sai** → Scheduler gọi 401, jobs không chạy | Test kỹ Phase 2 với mock OIDC; có endpoint debug `/internal/jobs/_whoami` log claims (xoá sau verify). |
| **Backtest in-API bị Cloud Run kill** giữa chừng khi instance scale down | `--cpu-always-allocated` (vẫn free tier nếu API < 360K vCPU-s) + idempotent backtest engine (re-enqueue Pending khi instance start). |
| **Cron timezone confusion** — VN giờ chợ vs UTC | Cloud Scheduler dùng `--time-zone=UTC`, dùng cron 02-07 UTC = 09-14 ICT. Đặt comment rõ trong gcloud command. |
| **Quên xoá Worker service trên GCP** sau deploy | Phase 5 step 2 explicit `gcloud run services delete invest-mate-worker`. |
| **Mất env var `Jobs__AllowedSchedulerSAs`** lúc deploy → mọi OIDC fail / mọi OIDC pass | Validate startup: nếu list empty trong Production → throw hoặc warn rõ ràng. |

---

## 8. Validation / acceptance

**Backend tests (xUnit):**
- `PriceSnapshotJobServiceTests.RunAsync_FetchesPricesForActivePortfolioSymbols`
- `PriceSnapshotJobServiceTests.RunAsync_NoActivePortfolios_DoesNothing`
- `ExchangeRateJobServiceTests.RunAsync_RefreshesRates`
- `InternalJobsControllerTests.PostSnapshot_NoAuth_Returns401`
- `InternalJobsControllerTests.PostSnapshot_ValidOidc_Returns200`
- `InternalJobsControllerTests.PostSnapshot_OidcFromUnknownSA_Returns403`
- `BacktestQueueServiceTests.Enqueue_Dequeues_AndRunsEngine`

**Manual verification (Phase 5):**
1. `gcloud scheduler jobs run invest-mate-prices` → check Cloud Run logs API show `[InternalJobs] prices triggered by SA=...`, `stock_prices.lastUpdated` mới.
2. Submit backtest qua UI → log `BacktestQueueService picked up <id>`, kết quả xuất hiện trong < 60s.
3. 24h sau: 3 cron đã chạy, data fresh, billing dashboard show < free tier.

---

## 9. Effort estimate

- Phase 1 (extract services): 2–3h
- Phase 2 (OIDC + controller): 3–4h
- Phase 3 (Backtest queue): 2h
- Phase 4 (delete + cloudbuild): 1h
- Phase 5 (deploy + Scheduler): 1–2h (manual testing GCP)
- Phase 6 (docs): 1h

**Total:** ~10–13h, 2 sessions.
