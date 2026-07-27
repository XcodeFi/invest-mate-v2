# Handoff — 2026-07-26 · Bản tin hằng ngày: sửa lỗi tiền mặt + bổ sung context quyết định

## State now

Branch **`feature/digest-decision-context`** (tách từ `origin/master`, **không có upstream** — cố ý, để VS Code Sync không push thẳng lên master). **13 commit, working tree sạch, chưa push, chưa mở PR.**

Toàn bộ test xanh: **1.508 pass / 0 fail** (Domain 740 · Application 236 · Infrastructure 353 · Api 179).

### Việc đã làm

**Bug gốc:** bản tin đọc tiền mặt **chỉ** từ `FinancialProfile.Accounts` loại `IdleCash`, không bao giờ đọc tiền trong tài khoản chứng khoán. Tiền thu từ lệnh bán vô hình → bản tin báo `idle_cash = 0`, advisor kết luận "không còn dư địa xoay xở". Nặng hơn: con số đó là account balance của position sizing nên **mọi khối lượng gợi ý đều thấp hơn thực tế**.

- `PortfolioCashCalculator` (Application/Common) — hàm thuần `InitialCapital + flows − grossBuys + grossSells`, 6 unit test.
- `<cash_and_net_worth>` tách `<portfolio_cash>` khỏi `<idle_cash>`, **luôn in** kể cả khi chưa có hồ sơ tài chính (trước đây cả block bị bọc trong `if (profile != null)`).
- Thêm: `<portfolio_overview>` bóc theo danh mục + `<realized_pnl>`, `<positions>` (danh mục/KL/giá vốn/%DM/khoảng cách SL), `<recent_trades>` 14 ngày, `<decision_queue>`, `<risk_alerts>` theo luật rủi ro, `<drill_down>`.
- `systemPrompt` thêm mục 7 "Luật đọc dữ liệu" — `n/a` ≠ 0, luôn nêu tên danh mục, đọc `<recent_trades>` trước khi nhận định vị thế.
- Dependency mới của `AiAssistantService`: `ICapitalFlowRepository`, `IMediator` (verified: **không** circular DI — không handler nào phụ thuộc `IAiAssistantService`).
- ADR-0007 ghi nhận 2 công thức cash cùng tồn tại.

### Ba lỗi tự bắt được trong lúc làm (không phải bug gốc)

1. **`<return>` sai mẫu số** — cộng lãi/lỗ đã thực hiện vào tử số nhưng mẫu số vẫn là giá vốn phần **đang nắm** → -39,7% trong khi vị thế thực chỉ lỗ -19,8%. Đã tách thành `<unrealized_return>` (trên giá vốn đang nắm) và `<total_return>` (trên tổng tiền đã mua, theo `PerformanceMetricsService:216`). **Bắt được nhờ soi payload thật bằng mắt, assertion không thấy.**
2. **Tiền mặt thiếu trình bày như đủ** — 2 danh mục mà 1 fetch lỗi thì `<total_cash>` có ghi chú "chưa đầy đủ" nhưng `<portfolio_cash>` in con số trần, rồi đi thẳng vào sizing. Cùng hình thái với bug gốc, thu nhỏ. Đã thêm caveat cho cả `portfolio_cash` và `investable_capital`.
3. **Cột "Giá trị" trong `<recent_trades>`** là gộp, chưa trừ phí/thuế (144.275.000 vs tiền thực về 143.915.000). Đã đổi nhãn thành "Giá trị gộp (chưa gồm phí/thuế)".

## Next steps

1. **Push + mở PR vào master** — chưa làm, đang chờ bạn đồng ý.
2. Sau khi merge: điền số PR vào ADR-0007 (`PR: #XX`) và mục References của spec.
3. **PR riêng sau này:** chuyển `RiskCalculationService:91` + `SnapshotService:53` sang `PortfolioCashCalculator` để hết phân kỳ công thức. Cần test cho snapshot lịch sử đã persist trước khi đổi. Xong thì cập nhật ADR-0007 thành `Superseded`.

## Blockers / gotchas

- **Không verify được bằng curl.** Endpoint `POST /api/v1/ai/daily-digest` xác thực bằng **ApiKey scheme** (`X-Api-Key`), không phải JWT — `MintStableJwt` chỉ mint JWT nên không dùng được ở đây, và mint API key thì phải ghi vào prod DB. Thay bằng `AiAssistantServiceDigestWiringTests`: dựng service thật với repo mock, tái hiện đúng kịch bản HHV, 10 test. Chạy lại được mãi, mạnh hơn một lần curl. Nếu muốn verify live thì cần bạn cấp `X-Api-Key`.
- `appsettings.Development.json` trỏ `DatabaseName=InvestmentApp_prod`. Lần mint JWT có đọc prod (read-only). Token đã xoá khỏi scratchpad.
- **Doc drift có sẵn, không sửa vì ngoài phạm vi:** `frontend/src/assets/docs/cong-cu-ho-tro.md:94` vẫn ghi Dashboard dùng "Bản tin hàng ngày", nhưng theo `docs/architecture.md:431` Dashboard đã chuyển sang `portfolio-critique`.
- Pattern của codebase này: formatter bản tin là **static thuần** trên `AiAssistantService`, test gọi trực tiếp không mock. Test cũ không hề khởi tạo service → thêm ctor dependency không làm vỡ test nào. Giữ pattern này khi thêm section mới.

## Files

- `src/InvestmentApp.Application/Common/PortfolioCashCalculator.cs` (mới)
- `src/InvestmentApp.Infrastructure/Services/DigestModels.cs` (mới)
- `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` (formatters + `BuildDailyBriefingContext` + ctor)
- `tests/InvestmentApp.Application.Tests/Common/PortfolioCashCalculatorTests.cs` (mới)
- `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDigestWiringTests.cs` (mới)
- `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs`
- `docs/adr/0007-portfolio-cash-formula-divergence.md` (mới)
- `docs/superpowers/specs/2026-07-26-daily-digest-decision-context-design.md` (mới)
- `docs/superpowers/plans/2026-07-26-daily-digest-decision-context.md` (mới)
- `docs/architecture.md`, `docs/business-domain.md`, `frontend/src/assets/CHANGELOG.md` (v2.68.0)
