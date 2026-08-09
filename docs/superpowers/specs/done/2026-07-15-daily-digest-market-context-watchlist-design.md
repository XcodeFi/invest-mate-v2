# Design — Daily digest: market context + enriched watchlist

**Date:** 2026-07-15
**Status:** Approved (design), pending implementation plan
**Scope:** Backend only (`invest-mate-v2`). Enrich the shared daily-briefing context.

## Goal

Cho bản tin sáng (`POST /api/v1/ai/daily-digest`, và chat "bản tin hằng ngày" trong app) có **bối cảnh thị trường** và **watchlist đầy đủ**, để Claude:

1. Quyết định **tái cơ cấu danh mục** chính xác — phân biệt "cả thị trường đỏ" vs "chỉ mã của mình yếu".
2. **Luôn săn cơ hội** — thấy mã đang theo dõi nào gần giá mục tiêu mua.

## Current state

`BuildDailyDigestAsync` (Infrastructure/Services/AiAssistantService.cs) ủy quyền cho shared `BuildDailyBriefingContext(userId, null, ct)`. Builder hiện phát các section: `<date>`, `<portfolio_overview>`, cash/net-worth, `<top_positions>`, `<risk_alerts>`, `<pending_plans>` (kèm position sizing), `<watchlist_alerts>`.

Hai khoảng trống:
- **Không có section thị trường nào.**
- `<watchlist_alerts>` chỉ hiện khi giá **đã chạm** `TargetBuyPrice`/`TargetSellPrice`. Nếu chưa đặt target hoặc chưa chạm → không có gì (live test 15/07 trống vì account test chưa đặt target).

## Design

Hai thay đổi trong **shared** `BuildDailyBriefingContext` (nên digest + in-app briefing chat cùng hưởng — cố ý, nhất quán với Slice 3).

### 1. `<market_context>` — MỚI (rich)

Nguồn: `IMarketDataProvider.GetIndexDataAsync("VNINDEX", ct)` → `MarketIndexData?`.
- **Thêm DI dependency** `IMarketDataProvider` vào `AiAssistantService` (đã đăng ký sẵn trong DI; dùng bởi `GetMarketIndexQuery`, `PriceSnapshotJobService`).
- Fetch song song cùng các task hiện có, chịu chung timeout 10s (`WaitAsync`), `null` → **bỏ qua section** (không vỡ digest).

Định dạng (chỉ append khi có dữ liệu):
```
<market_context>
  <vnindex>{Close:N2} ({ChangePercent:+0.0;-0.0}%)</vnindex>
  <breadth>Tăng {Advance} / Giảm {Decline} / Trần {Ceiling} / Sàn {Floor}</breadth>
  <foreign_net>{ForeignBuyValue - ForeignSellValue:+#,0;-#,0} tỷ (mua {ForeignBuyValue:N0} / bán {ForeignSellValue:N0})</foreign_net>
</market_context>
```
(Đơn vị khối ngoại: **tỷ VND** — đúng đơn vị `MarketIndexData` trả về.)

### 2. `<watchlist>` — NÂNG CẤP

Thay `<watchlist_alerts>` (chỉ báo đã-chạm) bằng section liệt kê đầy đủ + giữ dấu hiệu chạm:
- Lấy các item watchlist (dedupe theo symbol, cap 10 — ưu tiên item có `TargetBuyPrice`).
- Fetch giá song song qua `_stockInfoProvider.GetStockDetailAsync(symbol, ct)` (pattern hiện có), chung timeout.
- Mỗi item: giá hiện tại, %thay đổi, và **khoảng cách tới `TargetBuyPrice`** (`(price - target)/target`), đánh dấu 📉 khi ≤ target (cơ hội mua) / 📈 khi ≥ `TargetSellPrice` (cơ hội bán).

Định dạng:
```
<watchlist>
| Mã | Giá | %ngày | Mục tiêu mua | Cách target |
|----|-----|-------|--------------|-------------|
| HPG | 26,500 | +0.8% | 25,000 | +6.0% |
| ... |
  📉 SSI: giá 28,000 ≤ mục tiêu mua 28,500 (cơ hội)
</watchlist>
```
Item không có giá (fetch fail) vẫn hiện symbol + target, cột giá để trống.

### 3. systemPrompt

Cập nhật khối `Nhiệm vụ` của daily-briefing:
- Mục "Cần hành động ngay" + mục mới **"Bối cảnh thị trường"**: dùng `<market_context>` để phán đoán tái cơ cấu — nếu VN-Index giảm mạnh + độ rộng tiêu cực thì thận trọng cắt lỗ đồng loạt; nếu chỉ mã mình yếu thì cân nhắc riêng.
- Mục "Cơ hội hôm nay": dựa `<watchlist>` (mã gần/đạt target mua) + `pending_plans`.

## Data sources (đã xác minh)

| Dữ liệu | API | Ghi chú |
|---|---|---|
| VN-Index + breadth + foreign | `IMarketDataProvider.GetIndexDataAsync("VNINDEX")` → `MarketIndexData?` | Hmoney map VNINDEX→"10"; null nếu lỗi |
| Giá watchlist item | `IStockInfoProvider.GetStockDetailAsync(symbol)` | Đã dùng trong watchlist_alerts |
| Watchlist | `IWatchlistRepository.GetByUserIdAsync` | Đã inject; `WatchlistItem{Symbol,TargetBuyPrice?,TargetSellPrice?}` |

## Resilience

Mọi fetch mới bọc trong pattern hiện có: `ContinueWith` trả null on fault + `Task.WhenAll(...).WaitAsync(timeout, ct)` + `catch(TimeoutException)` → "continue with whatever completed". Section chỉ append khi có dữ liệu. Không được để market/price outage làm hỏng toàn bộ digest.

## Testing (TDD — bắt buộc)

`tests/InvestmentApp.Infrastructure.Tests/Services/` — mở rộng `AiAssistantServiceDailyDigestTests` (Moq):
1. Có VN-Index data → `<market_context>` xuất hiện với điểm/độ rộng/khối ngoại đúng.
2. `IMarketDataProvider` trả null → không có `<market_context>`, các section khác vẫn nguyên.
3. Watchlist có item + giá → `<watchlist>` liệt kê + tính khoảng cách target đúng; item giá ≤ TargetBuy → đánh dấu cơ hội.
4. Watchlist rỗng → không có `<watchlist>`.
5. Regression: portfolio/cash/pending_plans/sizing giữ nguyên.

## Out of scope

- Không thêm sector performance, top-fluctuation, index intraday chart (YAGNI — có thể thêm sau nếu cần).
- Không đổi endpoint contract (`AiContextResult{systemPrompt,userMessage,errorMessage}` giữ nguyên) → NPU `daily_digest.py` không phải sửa.
- Không đổi UI.

## Docs to update on ship

`docs/business-domain.md` (digest context), `docs/features.md`, `docs/architecture.md` (AiAssistantService thêm IMarketDataProvider dep), `frontend/src/assets/CHANGELOG.md`. Cân nhắc ADR nếu coi việc thêm dep + đổi shared context là quyết định đáng ghi (nhiều khả năng KHÔNG cần — thuần mở rộng, không đổi contract).
