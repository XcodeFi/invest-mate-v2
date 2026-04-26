# ADR-0001 — Replace dedicated Worker service with API endpoints triggered by Cloud Scheduler

- **Status:** Accepted
- **Date:** 2026-04-26
- **Related plan:** [`docs/plans/worker-to-scheduler-migration.md`](../plans/worker-to-scheduler-migration.md)
- **Affected layers:** Api / Infrastructure / Infra-external

## Context

`invest-mate-worker` được tạo từ ngày deploy đầu lên Cloud Run với cấu hình `--min-instances=1 --no-cpu-throttling` (always-on). Container chạy 24/7 chỉ để host các `BackgroundService` poll/loop và thực thi `SnapshotService`, `PriceSnapshotJob`, `ExchangeRateJob`, `BacktestJob`.

**Vấn đề:** owner đang dùng GCP free tier. 1 vCPU × 86.400s × 30 ngày = **~2,6M vCPU-seconds/tháng** → vượt **360K free tier ~7×**. Trong khi workload thật sự chỉ ~5–10 phút/ngày.

**Constraints:**
- Solo-user app; không cần HA, không cần queue scale.
- `BacktestJob` cần on-demand (vài lần/tuần).
- 3 cron job khác (snapshot, prices, exchange rate) chạy theo lịch cố định.
- Cloud Scheduler free tier: 3 jobs miễn phí — vừa đủ.

## Options Considered

### Option A — API endpoints + Cloud Scheduler (gộp vào API)

- **Pros:**
  - Hoàn toàn miễn phí (3 Scheduler jobs free, API CPU-seconds đã có dư trong free tier).
  - 1 service ít hơn để maintain — không phải build/deploy thêm container.
  - Tái dùng DI, auth middleware, repositories có sẵn trong API.
  - OIDC từ Cloud Scheduler tích hợp native với Cloud Run (auto-verified bằng Google JWKS).
- **Cons:**
  - API phải lộ thêm internal endpoints (mitigation: OIDC + service-account allowlist).
  - Backtest in-API cần `--cpu-always-allocated` để không bị pause khi instance scale-down (vẫn nằm trong free tier).
  - Mất separation of concerns giữa user-facing và background work.

### Option B — Cloud Run Jobs (separate from Services) + Cloud Scheduler

- **Pros:**
  - Giữ tách biệt code Worker khỏi API.
  - Cloud Run Jobs chỉ bill khi chạy → cũng free tier-friendly.
- **Cons:**
  - Phức tạp hơn (build thêm image, Scheduler invoke qua gcloud command thay vì HTTP).
  - Mỗi job = 1 image hoặc env var demux logic → thêm overhead.
  - Backtest trigger từ API → phải call `gcloud run jobs execute` (cần SDK + auth) hoặc dùng Cloud Tasks → phức tạp hơn nhiều cho 1 user.

### Option C — Worker service `--min-instances=0` + external pinger

- **Pros:**
  - Code Worker không đổi nhiều — chỉ thêm HTTP endpoint để wake.
- **Cons:**
  - Worker không phải web app → phải thêm hosted web layer cho mỗi instance.
  - Cold start mỗi 15 phút (~5–10s với .NET 9) ăn vào job execution time.
  - Vẫn có 2 service phải maintain.
  - GitHub Actions cron không reliable (drift, max ~5–15 phút sai số).

## Decision

**We choose Option A.**

Lý do chính: với solo-user app và workload ngắn (< 30s/job), maintain riêng 1 Worker service là over-engineering. Gộp vào API tận dụng được toàn bộ infrastructure đã có (DI, auth, MongoDB), miễn phí 100%, ít moving parts. OIDC từ Cloud Scheduler là native auth path cho Cloud Run → bảo mật tốt mà không phải tự xoay với API key.

Trade-off chấp nhận: API container có thêm 4 internal endpoints + 1 background queue service cho backtest. Cô lập bằng OIDC + service-account allowlist + route prefix `/internal/jobs/*` để dễ audit.

## Consequences

**Positive:**
- Chi phí Cloud Run dự kiến từ ~$X/tháng (overrun) → $0 (free tier).
- Đơn giản hoá deploy: 2 service thay vì 3 (api, frontend; bỏ worker).
- Tests dễ viết hơn — gọi trực tiếp service interface thay vì test BackgroundService timing.

**Negative / Trade-offs:**
- API process gánh thêm cron logic; cần monitor riêng cho `BacktestQueueService` không leak memory.
- Phụ thuộc Cloud Scheduler — nếu Google rate-limit hoặc đổi billing model, phải migrate. Mitigation: logic trong service interfaces, có thể trigger từ bất kỳ nguồn nào (GitHub Actions, cron-job.org).
- Mất khả năng scale Worker độc lập với API. Hiện không cần — solo user.

**Follow-ups:**
- Migration to run: `gcloud run services delete invest-mate-worker` sau deploy đầu tiên thành công.
- Tests to add: list trong plan section 8.
- Docs to update: `docs/architecture.md` bỏ Worker khỏi service map.
- Monitor: Cloud Run billing 7–14 ngày sau migration để confirm free tier.

## References

- Plan: [`docs/plans/worker-to-scheduler-migration.md`](../plans/worker-to-scheduler-migration.md)
- PR: TBD
- External:
  - [Cloud Scheduler with OIDC for Cloud Run](https://cloud.google.com/run/docs/triggering/using-scheduler)
  - [Cloud Run free tier limits](https://cloud.google.com/run/pricing#tables)
