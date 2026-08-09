# Daily Digest Decision Context — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sửa lỗi bản tin hằng ngày báo tiền mặt = 0 dù danh mục đang giữ tiền, và bổ sung các khối context giúp AI advisor phán đoán đúng thay vì kết luận sai.

**Architecture:** Chỉ sửa `BuildDailyBriefingContext` trong `AiAssistantService` (Infrastructure). Theo đúng pattern đã có của codebase: mỗi khối payload là một **static pure formatter** trên `AiAssistantService`, unit-test trực tiếp không cần mock; phần orchestration (fetch song song + timeout) verify ở tầng full-stack. Thêm một hàm thuần `PortfolioCashCalculator` ở Application để cả Infrastructure và test dùng chung. **Không đổi signature MCP tool** → không có rủi ro inputSchema regression.

**Tech Stack:** .NET 9, xUnit + FluentAssertions + Moq, MediatR, MongoDB Driver 3.6.0, ModelContextProtocol.AspNetCore 2.0.0-rc.1

**Spec:** [`docs/superpowers/specs/2026-07-26-daily-digest-decision-context-design.md`](../../specs/done/2026-07-26-daily-digest-decision-context-design.md)

## Global Constraints

- **Mọi text hiển thị phải là tiếng Việt có dấu đầy đủ** (labels, header bảng, thông báo). Không viết không dấu.
- **Không bao giờ in `0` cho giá trị chưa fetch được.** Dùng `n/a`. Đây chính là hình thái của bug gốc — một số thiếu bị trình bày như sự thật.
- **Không cắt danh sách im lặng.** Mọi chỗ có cap phải in rõ số dòng đã bỏ.
- **Không đổi signature MCP tool `get_daily_digest`** — vẫn `Task<AiContextResult> GetDailyDigest(IAiAssistantService, IHttpContextAccessor, CancellationToken)`.
- **Giữ budget timeout 10s** và pattern `ContinueWith` + `IsCompletedSuccessfully` + `WaitAsync(timeout, ct)` hiện có.
- Format số: `:N0` cho VND, `:+0.0;-0.0` cho phần trăm có dấu, `:+#,0;-#,0` cho VND có dấu — giống code hiện tại.
- Commit message bằng tiếng Anh. **Không** thêm trailer `Co-Authored-By`.
- Mỗi task chạy được test riêng và commit riêng.

---

## File Structure

| File | Trách nhiệm |
|---|---|
| **Create** `src/InvestmentApp.Application/Common/PortfolioCashCalculator.cs` | Hàm thuần tính tiền mặt danh mục từ vốn + flow + lệnh. Không I/O. |
| **Create** `src/InvestmentApp.Infrastructure/Services/DigestModels.cs` | Các record view-model chỉ dùng để render bản tin (`PortfolioDigestRow`, `PositionDigestRow`, `TradeDigestRow`). |
| **Modify** `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` | Thêm static formatter cho từng khối mới; sửa `FormatCashNetWorthSection`; thêm 2 ctor dependency; wire vào `BuildDailyBriefingContext`. |
| **Create** `tests/InvestmentApp.Application.Tests/Common/PortfolioCashCalculatorTests.cs` | Test hàm thuần tính cash. |
| **Modify** `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs` | Test mọi formatter mới + cập nhật test `FormatCashNetWorthSection` cũ. |
| **Create** `docs/adr/NNNN-portfolio-cash-formula-divergence.md` | ADR ghi nhận 2 công thức cash cùng tồn tại. |

> **Lưu ý namespace:** trong project này, file dưới `Application/Common/Interfaces/` lại khai báo `namespace InvestmentApp.Application.Interfaces` (không khớp folder). File mới `Common/PortfolioCashCalculator.cs` khai báo `namespace InvestmentApp.Application.Common` — dùng `using InvestmentApp.Application.Common;` khi tham chiếu. Nếu build lỗi CS0246, chạy `grep -n "^namespace" <file>` để xác nhận thay vì suy từ đường dẫn.

---

### Task 1: `PortfolioCashCalculator` — hàm thuần tính tiền mặt danh mục

Đây là task sửa **bug gốc**. Công thức lấy nguyên từ `CashFlowAdjustedReturnService.cs:432`.

**Files:**
- Create: `src/InvestmentApp.Application/Common/PortfolioCashCalculator.cs`
- Test: `tests/InvestmentApp.Application.Tests/Common/PortfolioCashCalculatorTests.cs`

**Interfaces:**
- Consumes: `InvestmentApp.Domain.Entities.Trade` (`TradeType`, `Quantity`, `Price`, `Fee`, `Tax`)
- Produces: `PortfolioCashCalculator.Compute(decimal initialCapital, decimal netFlowExcludingSeed, IEnumerable<Trade> trades) → decimal`

- [ ] **Step 1: Viết test fail**

```csharp
using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Common;

public class PortfolioCashCalculatorTests
{
    private static Trade Buy(decimal qty, decimal price, decimal fee = 0, decimal tax = 0)
        => new("p1", "HHV", TradeType.BUY, qty, price, fee, tax);

    private static Trade Sell(decimal qty, decimal price, decimal fee = 0, decimal tax = 0)
        => new("p1", "HHV", TradeType.SELL, qty, price, fee, tax);

    [Fact]
    public void NoTradesNoFlows_ReturnsInitialCapital()
    {
        PortfolioCashCalculator.Compute(500_000_000m, 0m, Array.Empty<Trade>())
            .Should().Be(500_000_000m);
    }

    [Fact]
    public void BuyOnly_SubtractsGrossIncludingFeeAndTax()
    {
        // 1.000 × 10.000 = 10.000.000 + fee 15.000 + tax 5.000 = 10.020.000
        PortfolioCashCalculator.Compute(50_000_000m, 0m, new[] { Buy(1_000m, 10_000m, 15_000m, 5_000m) })
            .Should().Be(39_980_000m);
    }

    [Fact]
    public void PartialSell_AddsNetProceedsAfterFeeAndTax()
    {
        // Tái hiện kịch bản HHV: mua 29.000 @ 12.426, bán 14.500 @ 9.950.
        // buys  = 29.000 × 12.426 = 360.354.000
        // sells = 14.500 × 9.950  = 144.275.000 − fee 250.000 − tax 110.000 = 143.915.000
        var trades = new[] { Buy(29_000m, 12_426m), Sell(14_500m, 9_950m, 250_000m, 110_000m) };

        PortfolioCashCalculator.Compute(500_000_000m, 0m, trades)
            .Should().Be(500_000_000m - 360_354_000m + 143_915_000m);
    }

    [Fact]
    public void AddsNetFlowExcludingSeed()
    {
        PortfolioCashCalculator.Compute(100_000_000m, 20_000_000m, Array.Empty<Trade>())
            .Should().Be(120_000_000m);
    }

    [Fact]
    public void NegativeNetFlow_Withdrawal_ReducesCash()
    {
        PortfolioCashCalculator.Compute(100_000_000m, -30_000_000m, Array.Empty<Trade>())
            .Should().Be(70_000_000m);
    }

    [Fact]
    public void FullExitAtProfit_CashEqualsInitialPlusRealizedGain()
    {
        // Mua 1.000 @ 10.000 = 10.000.000; bán hết @ 12.000 = 12.000.000 → lãi 2.000.000
        var trades = new[] { Buy(1_000m, 10_000m), Sell(1_000m, 12_000m) };

        PortfolioCashCalculator.Compute(10_000_000m, 0m, trades)
            .Should().Be(12_000_000m);
    }
}
```

- [ ] **Step 2: Chạy test, xác nhận FAIL**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~PortfolioCashCalculatorTests"`
Expected: FAIL — build error `CS0246: The type or namespace name 'PortfolioCashCalculator' could not be found`

- [ ] **Step 3: Implement tối thiểu**

```csharp
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Tiền mặt còn lại trong một danh mục. Công thức thống nhất với capital-flows hero card
/// và <c>CashFlowAdjustedReturnService</c> — có tính lãi/lỗ đã thực hiện.
/// </summary>
public static class PortfolioCashCalculator
{
    /// <param name="netFlowExcludingSeed">
    /// Tổng nạp/rút SAU khi tạo danh mục. Truyền từ
    /// <c>ICapitalFlowRepository.GetTotalFlowByPortfolioIdAsync</c> — hàm đó đã lọc seed deposit,
    /// nên vốn ban đầu không bị đếm hai lần.
    /// </param>
    public static decimal Compute(decimal initialCapital, decimal netFlowExcludingSeed, IEnumerable<Trade> trades)
    {
        decimal grossBuys = 0m, grossSells = 0m;

        foreach (var t in trades)
        {
            if (t.TradeType == TradeType.BUY)
                grossBuys += t.Quantity * t.Price + t.Fee + t.Tax;
            else
                grossSells += t.Quantity * t.Price - t.Fee - t.Tax;
        }

        return initialCapital + netFlowExcludingSeed - grossBuys + grossSells;
    }
}
```

- [ ] **Step 4: Chạy test, xác nhận PASS**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~PortfolioCashCalculatorTests"`
Expected: PASS — 6 passed

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/Common/PortfolioCashCalculator.cs tests/InvestmentApp.Application.Tests/Common/PortfolioCashCalculatorTests.cs
git commit -m "feat(digest): add PortfolioCashCalculator for portfolio cash balance"
```

---

### Task 2: View-model record cho bản tin

Các record thuần để formatter nhận dữ liệu đã chuẩn hoá. `decimal?` = `n/a` (chưa fetch được), **không phải** 0.

**Files:**
- Create: `src/InvestmentApp.Infrastructure/Services/DigestModels.cs`

**Interfaces:**
- Produces: `PortfolioDigestRow`, `PositionDigestRow`, `TradeDigestRow` trong namespace `InvestmentApp.Infrastructure.Services`

- [ ] **Step 1: Tạo file**

```csharp
namespace InvestmentApp.Infrastructure.Services;

/// <summary>Một dòng danh mục trong &lt;portfolio_overview&gt;. Cash null = chưa lấy được (n/a).</summary>
public sealed record PortfolioDigestRow(
    string Name,
    decimal MarketValue,
    decimal? Cash,
    decimal UnrealizedPnL,
    decimal RealizedPnL);

/// <summary>
/// Một dòng vị thế trong &lt;positions&gt;. Ghép <c>PositionPnL</c> với <c>PositionRiskItem</c>.
/// Các trường nullable = risk service không trả được (n/a), TRỪ StopLossPrice:
/// null ở đó nghĩa là user CHƯA ĐẶT stop-loss — một tín hiệu rủi ro thật.
/// </summary>
public sealed record PositionDigestRow(
    string Symbol,
    string PortfolioName,
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal MarketValue,
    decimal UnrealizedPnL,
    decimal UnrealizedPnLPercent,
    decimal? PositionSizePercent,
    decimal? StopLossPrice,
    decimal? DistanceToStopLossPercent,
    bool RiskDataAvailable);

/// <summary>Một lệnh trong &lt;recent_trades&gt;.</summary>
public sealed record TradeDigestRow(
    DateTime TradeDate,
    string PortfolioName,
    string Symbol,
    bool IsBuy,
    decimal Quantity,
    decimal Price,
    decimal GrossValue);
```

- [ ] **Step 2: Build để chắc chắn biên dịch được**

Run: `dotnet build src/InvestmentApp.Infrastructure/InvestmentApp.Infrastructure.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/DigestModels.cs
git commit -m "feat(digest): add digest view-model records"
```

---

### Task 3: `FormatCashNetWorthSection` — tách `portfolio_cash` khỏi `idle_cash`

Sửa signature. Block phải **luôn in** `portfolio_cash` + `investable_capital`; net-worth/health chỉ in khi có hồ sơ tài chính.

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs:81-93`
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs:90-102` (thay test cũ)

**Interfaces:**
- Consumes: không
- Produces: `AiAssistantService.FormatCashNetWorthSection(decimal investableCapital, decimal? portfolioCash, decimal? idleCash, decimal? netWorth, decimal? totalAssets, decimal? totalDebt, int? healthScore) → string`

- [ ] **Step 1: Thay test cũ bằng test mới (fail)**

Xoá `FormatCashNetWorthSection_ContainsTagsAndValues` hiện có (dòng 90-102) và thay bằng:

```csharp
    // --- FormatCashNetWorthSection: tách portfolio_cash (TK chứng khoán) vs idle_cash (hồ sơ tài chính) ---

    [Fact]
    public void FormatCashNetWorthSection_WithProfile_RendersBothCashSources()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 500_000_000m, portfolioCash: 287_903_688m, idleCash: 50_000_000m,
            netWorth: 300_000_000m, totalAssets: 320_000_000m, totalDebt: 20_000_000m, healthScore: 78);

        section.Should().Contain("<cash_and_net_worth>");
        section.Should().Contain("</cash_and_net_worth>");
        section.Should().Contain("<portfolio_cash>287,903,688 VND</portfolio_cash>");
        section.Should().Contain("<idle_cash>50,000,000 VND</idle_cash>");
        section.Should().Contain("<investable_capital>500,000,000 VND</investable_capital>");
        section.Should().Contain("78");
    }

    [Fact]
    public void FormatCashNetWorthSection_NoProfile_StillRendersPortfolioCashAndCapital()
    {
        // Bug gốc: cả block bị bọc trong `if (profile != null)` → user chưa có hồ sơ tài chính
        // thì mất sạch thông tin tiền. Nay portfolio_cash phải luôn hiện.
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 452_903_688m, portfolioCash: 287_903_688m, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null);

        section.Should().Contain("<portfolio_cash>287,903,688 VND</portfolio_cash>");
        section.Should().Contain("<investable_capital>452,903,688 VND</investable_capital>");
        section.Should().NotContain("idle_cash");
        section.Should().NotContain("net_worth");
        section.Should().NotContain("health_score");
    }

    [Fact]
    public void FormatCashNetWorthSection_CashUnavailable_RendersNaNotZero()
    {
        // Luật cứng: không in 0 cho giá trị chưa fetch được.
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 164_960_000m, portfolioCash: null, idleCash: null,
            netWorth: null, totalAssets: null, totalDebt: null, healthScore: null);

        section.Should().Contain("<portfolio_cash>n/a</portfolio_cash>");
        section.Should().NotContain("<portfolio_cash>0 VND</portfolio_cash>");
    }
```

- [ ] **Step 2: Chạy test, xác nhận FAIL**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatCashNetWorthSection"`
Expected: FAIL — build error, không có overload nhận 7 tham số

- [ ] **Step 3: Sửa formatter**

Thay toàn bộ method tại `AiAssistantService.cs:81-93`:

```csharp
    /// <summary>
    /// Section vốn đầu tư khả dụng + net-worth cho bản tin.
    /// portfolio_cash (tiền trong tài khoản chứng khoán) luôn in — kể cả khi chưa có hồ sơ tài chính.
    /// idle_cash / net-worth / health chỉ in khi có hồ sơ. null → "n/a", không bao giờ in 0.
    /// </summary>
    public static string FormatCashNetWorthSection(decimal investableCapital, decimal? portfolioCash,
        decimal? idleCash, decimal? netWorth, decimal? totalAssets, decimal? totalDebt, int? healthScore)
    {
        static string Vnd(decimal? v) => v.HasValue ? $"{v.Value:N0} VND" : "n/a";

        var sb = new StringBuilder();
        sb.AppendLine("<cash_and_net_worth>");
        sb.AppendLine($"  <portfolio_cash>{Vnd(portfolioCash)}</portfolio_cash>");
        if (idleCash.HasValue)
            sb.AppendLine($"  <idle_cash>{Vnd(idleCash)}</idle_cash>");
        sb.AppendLine($"  <investable_capital>{investableCapital:N0} VND</investable_capital>");
        if (netWorth.HasValue)
            sb.AppendLine($"  <net_worth>{Vnd(netWorth)}</net_worth>");
        if (totalAssets.HasValue)
            sb.AppendLine($"  <total_assets>{Vnd(totalAssets)}</total_assets>");
        if (totalDebt.HasValue)
            sb.AppendLine($"  <total_debt>{Vnd(totalDebt)}</total_debt>");
        if (healthScore.HasValue)
            sb.AppendLine($"  <health_score>{healthScore.Value}/100</health_score>");
        sb.Append("</cash_and_net_worth>");
        return sb.ToString();
    }
```

- [ ] **Step 4: Chạy test, xác nhận PASS**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatCashNetWorthSection"`
Expected: PASS — 3 passed

> Call site tại `AiAssistantService.cs:1747` sẽ chưa biên dịch được cho tới Task 9. Nếu muốn build xanh giữa các task, tạm truyền `portfolioCash: null` và các giá trị hồ sơ như cũ; Task 9 sẽ thay bằng giá trị thật.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs
git commit -m "feat(digest): split portfolio_cash from idle_cash in cash section"
```

---

### Task 4: `FormatPortfolioOverviewSection` — bóc theo danh mục + realized P&L

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` (thêm static method cạnh các formatter khác, quanh dòng 93)
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs`

**Interfaces:**
- Consumes: `PortfolioDigestRow` (Task 2)
- Produces: `AiAssistantService.FormatPortfolioOverviewSection(IReadOnlyList<PortfolioDigestRow> rows, decimal totalInvested) → string`

- [ ] **Step 1: Viết test fail**

```csharp
    // --- FormatPortfolioOverviewSection: tổng quan + bóc theo danh mục + realized P&L ---

    [Fact]
    public void FormatPortfolioOverviewSection_RendersTotalsAndPerPortfolioRows()
    {
        var rows = new List<PortfolioDigestRow>
        {
            new("24hmoney", MarketValue: 144_565_000m, Cash: 283_023_788m,
                UnrealizedPnL: -35_600_000m, RealizedPnL: -35_922_000m),
            new("Swing Trading", MarketValue: 20_395_000m, Cash: 4_879_900m,
                UnrealizedPnL: -4_700_000m, RealizedPnL: 0m),
        };

        var section = AiAssistantService.FormatPortfolioOverviewSection(rows, totalInvested: 205_270_000m);

        section.Should().Contain("<portfolio_overview>");
        section.Should().Contain("</portfolio_overview>");
        section.Should().Contain("<portfolios>2</portfolios>");
        section.Should().Contain("<total_market_value>164,960,000 VND</total_market_value>");
        section.Should().Contain("<total_cash>287,903,688 VND</total_cash>");
        section.Should().Contain("<total_capital>452,863,688 VND</total_capital>");
        section.Should().Contain("<realized_pnl>-35,922,000 VND</realized_pnl>");
        section.Should().Contain("name=\"24hmoney\"");
        section.Should().Contain("cash=\"283,023,788\"");
        section.Should().Contain("realized=\"-35,922,000\"");
    }

    [Fact]
    public void FormatPortfolioOverviewSection_CashUnavailableForOnePortfolio_ShowsNaAndExcludesFromTotal()
    {
        // Trades repo của 1 danh mục lỗi → cash danh mục đó n/a; total_cash chỉ cộng phần lấy được
        // và phải nói rõ là chưa đầy đủ.
        var rows = new List<PortfolioDigestRow>
        {
            new("A", 100_000_000m, Cash: 10_000_000m, UnrealizedPnL: 0m, RealizedPnL: 0m),
            new("B", 50_000_000m, Cash: null, UnrealizedPnL: 0m, RealizedPnL: 0m),
        };

        var section = AiAssistantService.FormatPortfolioOverviewSection(rows, totalInvested: 150_000_000m);

        section.Should().Contain("cash=\"n/a\"");
        section.Should().Contain("<total_cash>10,000,000 VND (chưa đầy đủ: 1 danh mục không lấy được)</total_cash>");
    }

    [Fact]
    public void FormatPortfolioOverviewSection_ZeroInvested_OmitsReturnInsteadOfDividingByZero()
    {
        var rows = new List<PortfolioDigestRow> { new("A", 0m, 0m, 0m, 0m) };

        var section = AiAssistantService.FormatPortfolioOverviewSection(rows, totalInvested: 0m);

        section.Should().NotContain("<return>");
    }
```

- [ ] **Step 2: Chạy test, xác nhận FAIL**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatPortfolioOverviewSection"`
Expected: FAIL — `CS0117: 'AiAssistantService' does not contain a definition for 'FormatPortfolioOverviewSection'`

- [ ] **Step 3: Implement**

```csharp
    /// <summary>Tổng quan danh mục + bóc từng danh mục. Cash null của một danh mục → "n/a", không cộng vào tổng.</summary>
    public static string FormatPortfolioOverviewSection(IReadOnlyList<PortfolioDigestRow> rows, decimal totalInvested)
    {
        var totalMarketValue = rows.Sum(r => r.MarketValue);
        var totalUnrealized = rows.Sum(r => r.UnrealizedPnL);
        var totalRealized = rows.Sum(r => r.RealizedPnL);
        var knownCash = rows.Where(r => r.Cash.HasValue).Sum(r => r.Cash!.Value);
        var missingCashCount = rows.Count(r => !r.Cash.HasValue);

        var cashText = missingCashCount == 0
            ? $"{knownCash:N0} VND"
            : $"{knownCash:N0} VND (chưa đầy đủ: {missingCashCount} danh mục không lấy được)";

        var sb = new StringBuilder();
        sb.AppendLine("<portfolio_overview>");
        sb.AppendLine($"  <portfolios>{rows.Count}</portfolios>");
        sb.AppendLine($"  <total_invested>{totalInvested:N0} VND</total_invested>");
        sb.AppendLine($"  <total_market_value>{totalMarketValue:N0} VND</total_market_value>");
        sb.AppendLine($"  <total_cash>{cashText}</total_cash>");
        sb.AppendLine($"  <total_capital>{totalMarketValue + knownCash:N0} VND</total_capital>");
        sb.AppendLine($"  <unrealized_pnl>{totalUnrealized:+#,0;-#,0} VND</unrealized_pnl>");
        sb.AppendLine($"  <realized_pnl>{totalRealized:+#,0;-#,0} VND</realized_pnl>");
        if (totalInvested > 0)
            sb.AppendLine($"  <return>{(totalUnrealized + totalRealized) / totalInvested * 100:+0.0;-0.0}%</return>");

        foreach (var r in rows)
        {
            var cash = r.Cash.HasValue ? $"{r.Cash.Value:N0}" : "n/a";
            sb.AppendLine($"  <portfolio name=\"{r.Name}\" market_value=\"{r.MarketValue:N0}\" " +
                          $"cash=\"{cash}\" unrealized=\"{r.UnrealizedPnL:+#,0;-#,0}\" " +
                          $"realized=\"{r.RealizedPnL:+#,0;-#,0}\" />");
        }

        sb.Append("</portfolio_overview>");
        return sb.ToString();
    }
```

- [ ] **Step 4: Chạy test, xác nhận PASS**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatPortfolioOverviewSection"`
Expected: PASS — 3 passed

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs
git commit -m "feat(digest): add per-portfolio overview with realized PnL and cash"
```

---

### Task 5: `FormatPositionsSection` — bảng vị thế đủ cột

Thay `<top_positions>`. Cap 15 dòng, in rõ số dòng bị bỏ.

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs`
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs`

**Interfaces:**
- Consumes: `PositionDigestRow` (Task 2)
- Produces: `AiAssistantService.FormatPositionsSection(IReadOnlyList<PositionDigestRow> rows) → string`

- [ ] **Step 1: Viết test fail**

```csharp
    // --- FormatPositionsSection: bảng vị thế có danh mục + %DM + khoảng cách SL ---

    private static PositionDigestRow Pos(string symbol, string portfolio, decimal? sizePct = 12m,
        decimal? sl = 11_000m, decimal? distSl = 8.5m, bool riskOk = true)
        => new(symbol, portfolio, Quantity: 14_500m, AverageCost: 12_426m, CurrentPrice: 9_970m,
               MarketValue: 144_565_000m, UnrealizedPnL: -35_600_000m, UnrealizedPnLPercent: -19.7m,
               PositionSizePercent: sizePct, StopLossPrice: sl, DistanceToStopLossPercent: distSl,
               RiskDataAvailable: riskOk);

    [Fact]
    public void FormatPositionsSection_RendersPortfolioNameQuantityCostAndRiskColumns()
    {
        var section = AiAssistantService.FormatPositionsSection(new[] { Pos("HHV", "24hmoney", 87.6m) });

        section.Should().Contain("<positions>");
        section.Should().Contain("</positions>");
        section.Should().Contain("| Mã | Danh mục | KL | Giá vốn | Giá | Giá trị | %DM | L/L % | L/L VND | SL | Cách SL |");
        section.Should().Contain("HHV");
        section.Should().Contain("24hmoney");
        section.Should().Contain("14,500");
        section.Should().Contain("12,426");
        section.Should().Contain("87.6%");
        section.Should().Contain("-19.7%");
    }

    [Fact]
    public void FormatPositionsSection_NoStopLoss_ShowsExplicitNotSetNotBlank()
    {
        // StopLossPrice null = user CHƯA ĐẶT SL → phải nói rõ, đây là tín hiệu rủi ro
        var section = AiAssistantService.FormatPositionsSection(
            new[] { Pos("MWG", "Swing Trading", sl: null, distSl: null) });

        section.Should().Contain("chưa đặt");
    }

    [Fact]
    public void FormatPositionsSection_RiskDataUnavailable_ShowsNaNotZero()
    {
        var section = AiAssistantService.FormatPositionsSection(
            new[] { Pos("FPT", "24hmoney", sizePct: null, sl: null, distSl: null, riskOk: false) });

        section.Should().Contain("n/a");
        section.Should().NotContain("0.0%");
    }

    [Fact]
    public void FormatPositionsSection_MoreThan15Rows_StatesHowManyOmitted()
    {
        var rows = Enumerable.Range(1, 18).Select(i => Pos($"S{i:00}", "24hmoney")).ToList();

        var section = AiAssistantService.FormatPositionsSection(rows);

        section.Should().Contain("còn 3 vị thế khác không hiển thị");
    }

    [Fact]
    public void FormatPositionsSection_Empty_ReturnsEmpty()
    {
        AiAssistantService.FormatPositionsSection(Array.Empty<PositionDigestRow>()).Should().BeEmpty();
    }
```

- [ ] **Step 2: Chạy test, xác nhận FAIL**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatPositionsSection"`
Expected: FAIL — `CS0117: 'AiAssistantService' does not contain a definition for 'FormatPositionsSection'`

- [ ] **Step 3: Implement**

```csharp
    private const int PositionsCap = 15;

    /// <summary>
    /// Bảng vị thế. Phân biệt rõ 3 trạng thái: có số, "chưa đặt" (user chưa set SL),
    /// và "n/a" (risk service không trả được).
    /// </summary>
    public static string FormatPositionsSection(IReadOnlyList<PositionDigestRow> rows)
    {
        if (rows.Count == 0) return string.Empty;

        var shown = rows.OrderByDescending(r => r.MarketValue).Take(PositionsCap).ToList();
        var omitted = rows.Count - shown.Count;

        var sb = new StringBuilder();
        sb.AppendLine("<positions>");
        sb.AppendLine("| Mã | Danh mục | KL | Giá vốn | Giá | Giá trị | %DM | L/L % | L/L VND | SL | Cách SL |");
        sb.AppendLine("|-----|----------|-----|---------|-----|---------|-----|-------|---------|-----|---------|");

        foreach (var r in shown)
        {
            var sizePct = r.PositionSizePercent.HasValue ? $"{r.PositionSizePercent.Value:0.0}%" : "n/a";
            var sl = r.StopLossPrice.HasValue ? $"{r.StopLossPrice.Value:N0}"
                   : r.RiskDataAvailable ? "chưa đặt" : "n/a";
            var distSl = r.DistanceToStopLossPercent.HasValue ? $"{r.DistanceToStopLossPercent.Value:+0.0;-0.0}%"
                       : r.RiskDataAvailable ? "chưa đặt" : "n/a";

            sb.AppendLine($"| {r.Symbol} | {r.PortfolioName} | {r.Quantity:N0} | {r.AverageCost:N0} | " +
                          $"{r.CurrentPrice:N0} | {r.MarketValue:N0} | {sizePct} | " +
                          $"{r.UnrealizedPnLPercent:+0.0;-0.0}% | {r.UnrealizedPnL:+#,0;-#,0} | {sl} | {distSl} |");
        }

        if (omitted > 0)
            sb.AppendLine($"(còn {omitted} vị thế khác không hiển thị — gọi list_positions để xem đủ)");

        sb.Append("</positions>");
        return sb.ToString();
    }
```

- [ ] **Step 4: Chạy test, xác nhận PASS**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatPositionsSection"`
Expected: PASS — 5 passed

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs
git commit -m "feat(digest): add positions table with portfolio, weight and stop-loss distance"
```

---

### Task 6: `FormatRecentTradesSection` — lệnh 14 ngày gần nhất

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs`
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs`

**Interfaces:**
- Consumes: `TradeDigestRow` (Task 2)
- Produces: `AiAssistantService.FormatRecentTradesSection(IReadOnlyList<TradeDigestRow> rows) → string`

- [ ] **Step 1: Viết test fail**

```csharp
    // --- FormatRecentTradesSection: lệnh gần đây (làm hiện việc "đã bán nửa HHV") ---

    [Fact]
    public void FormatRecentTradesSection_RendersSellTradeWithPortfolioAndValue()
    {
        var rows = new List<TradeDigestRow>
        {
            new(new DateTime(2026, 7, 24), "24hmoney", "HHV", IsBuy: false,
                Quantity: 14_500m, Price: 9_950m, GrossValue: 144_275_000m),
        };

        var section = AiAssistantService.FormatRecentTradesSection(rows);

        section.Should().Contain("<recent_trades>");
        section.Should().Contain("</recent_trades>");
        section.Should().Contain("24/07/2026");
        section.Should().Contain("24hmoney");
        section.Should().Contain("HHV");
        section.Should().Contain("BÁN");
        section.Should().Contain("14,500");
        section.Should().Contain("144,275,000");
    }

    [Fact]
    public void FormatRecentTradesSection_BuyTrade_LabelledMua()
    {
        var rows = new List<TradeDigestRow>
        {
            new(new DateTime(2026, 7, 20), "Swing Trading", "HPG", true, 1_000m, 25_000m, 25_000_000m),
        };

        AiAssistantService.FormatRecentTradesSection(rows).Should().Contain("MUA");
    }

    [Fact]
    public void FormatRecentTradesSection_SortedNewestFirst()
    {
        var rows = new List<TradeDigestRow>
        {
            new(new DateTime(2026, 7, 10), "A", "OLD", true, 1m, 1_000m, 1_000m),
            new(new DateTime(2026, 7, 24), "A", "NEW", true, 1m, 1_000m, 1_000m),
        };

        var section = AiAssistantService.FormatRecentTradesSection(rows);

        section.IndexOf("NEW", StringComparison.Ordinal)
            .Should().BeLessThan(section.IndexOf("OLD", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatRecentTradesSection_MoreThan20Rows_StatesHowManyOmitted()
    {
        var rows = Enumerable.Range(1, 23)
            .Select(i => new TradeDigestRow(new DateTime(2026, 7, 20), "A", $"S{i:00}", true, 1m, 1_000m, 1_000m))
            .ToList();

        AiAssistantService.FormatRecentTradesSection(rows).Should().Contain("còn 3 lệnh khác");
    }

    [Fact]
    public void FormatRecentTradesSection_Empty_ReturnsEmpty()
    {
        AiAssistantService.FormatRecentTradesSection(Array.Empty<TradeDigestRow>()).Should().BeEmpty();
    }
```

- [ ] **Step 2: Chạy test, xác nhận FAIL**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatRecentTradesSection"`
Expected: FAIL — `CS0117: 'AiAssistantService' does not contain a definition for 'FormatRecentTradesSection'`

- [ ] **Step 3: Implement**

```csharp
    private const int RecentTradesCap = 20;

    /// <summary>Lệnh gần đây. Lọc theo cửa sổ ngày thực hiện ở call site; ở đây chỉ render.</summary>
    public static string FormatRecentTradesSection(IReadOnlyList<TradeDigestRow> rows)
    {
        if (rows.Count == 0) return string.Empty;

        var shown = rows.OrderByDescending(r => r.TradeDate).Take(RecentTradesCap).ToList();
        var omitted = rows.Count - shown.Count;

        var sb = new StringBuilder();
        sb.AppendLine("<recent_trades>");
        sb.AppendLine("| Ngày | Danh mục | Mã | Loại | KL | Giá | Giá trị |");
        sb.AppendLine("|------|----------|-----|------|-----|-----|---------|");

        foreach (var r in shown)
            sb.AppendLine($"| {r.TradeDate:dd/MM/yyyy} | {r.PortfolioName} | {r.Symbol} | " +
                          $"{(r.IsBuy ? "MUA" : "BÁN")} | {r.Quantity:N0} | {r.Price:N0} | {r.GrossValue:N0} |");

        if (omitted > 0)
            sb.AppendLine($"(còn {omitted} lệnh khác — gọi get_trades_by_portfolio để xem đủ)");

        sb.Append("</recent_trades>");
        return sb.ToString();
    }
```

- [ ] **Step 4: Chạy test, xác nhận PASS**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatRecentTradesSection"`
Expected: PASS — 5 passed

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs
git commit -m "feat(digest): add recent trades section"
```

---

### Task 7: `FormatDecisionQueueSection` — hàng đợi quyết định

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs`
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs`

**Interfaces:**
- Consumes: `InvestmentApp.Application.Decisions.DTOs.DecisionItemDto` (`Type`, `Severity`, `Symbol`, `PortfolioName`, `Headline`, `ThesisOrReason`, `CurrentPrice`, `PlannedExitPrice`, `DueAt`), enum `DecisionType` (`StopLossHit`, `ScenarioTrigger`, `ThesisReviewDue`), enum `DecisionSeverity` (`Critical`, `Warning`, `Info`)
- Produces: `AiAssistantService.FormatDecisionQueueSection(IReadOnlyList<DecisionItemDto> items) → string`

Thêm `using InvestmentApp.Application.Decisions.DTOs;` vào đầu `AiAssistantService.cs` và test file.

- [ ] **Step 1: Viết test fail**

```csharp
    // --- FormatDecisionQueueSection: "hôm nay cần quyết gì" ---

    [Fact]
    public void FormatDecisionQueueSection_RendersCriticalFirstWithPortfolioAndHeadline()
    {
        var items = new List<DecisionItemDto>
        {
            new()
            {
                Id = "ThesisReviewDue:tp2", Type = DecisionType.ThesisReviewDue,
                Severity = DecisionSeverity.Warning, Symbol = "FPT", PortfolioName = "24hmoney",
                Headline = "FPT quá hạn review thesis 4 ngày", ThesisOrReason = "Chờ KQKD Q2",
            },
            new()
            {
                Id = "StopLossHit:tp1", Type = DecisionType.StopLossHit,
                Severity = DecisionSeverity.Critical, Symbol = "HHV", PortfolioName = "24hmoney",
                Headline = "HHV xuyên SL 10.000 (giá 9.970)", ThesisOrReason = "Hạ tầng hưởng lợi đầu tư công",
                CurrentPrice = 9_970m, PlannedExitPrice = 10_000m,
            },
        };

        var section = AiAssistantService.FormatDecisionQueueSection(items);

        section.Should().Contain("<decision_queue>");
        section.Should().Contain("</decision_queue>");
        section.Should().Contain("HHV xuyên SL 10.000 (giá 9.970)");
        section.Should().Contain("24hmoney");
        section.Should().Contain("Hạ tầng hưởng lợi đầu tư công");
        section.Should().Contain("Chờ KQKD Q2");
        // Critical phải đứng trước Warning
        section.IndexOf("HHV", StringComparison.Ordinal)
            .Should().BeLessThan(section.IndexOf("FPT", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatDecisionQueueSection_LabelsSeverityInVietnamese()
    {
        var items = new List<DecisionItemDto>
        {
            new() { Id = "x", Type = DecisionType.ScenarioTrigger, Severity = DecisionSeverity.Critical,
                    Symbol = "MWG", PortfolioName = "A", Headline = "MWG trigger bán 30%" },
        };

        AiAssistantService.FormatDecisionQueueSection(items).Should().Contain("Gấp");
    }

    [Fact]
    public void FormatDecisionQueueSection_Empty_ReturnsEmpty()
    {
        AiAssistantService.FormatDecisionQueueSection(Array.Empty<DecisionItemDto>()).Should().BeEmpty();
    }
```

- [ ] **Step 2: Chạy test, xác nhận FAIL**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatDecisionQueueSection"`
Expected: FAIL — `CS0117: 'AiAssistantService' does not contain a definition for 'FormatDecisionQueueSection'`

- [ ] **Step 3: Implement**

```csharp
    /// <summary>Hàng đợi quyết định — đã dedupe + sort sẵn ở query, ở đây sort lại theo severity cho chắc.</summary>
    public static string FormatDecisionQueueSection(IReadOnlyList<DecisionItemDto> items)
    {
        if (items.Count == 0) return string.Empty;

        static string SeverityVi(DecisionSeverity s) => s switch
        {
            DecisionSeverity.Critical => "Gấp",
            DecisionSeverity.Warning => "Cần để ý",
            _ => "Thông tin",
        };

        static string TypeVi(DecisionType t) => t switch
        {
            DecisionType.StopLossHit => "Chạm stop-loss",
            DecisionType.ScenarioTrigger => "Scenario trigger",
            _ => "Đến hạn review thesis",
        };

        var sb = new StringBuilder();
        sb.AppendLine("<decision_queue>");

        foreach (var it in items.OrderBy(i => i.Severity).ThenBy(i => i.Symbol, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"  [{SeverityVi(it.Severity)}] {TypeVi(it.Type)} — {it.Headline} (danh mục: {it.PortfolioName})");
            if (!string.IsNullOrWhiteSpace(it.ThesisOrReason))
                sb.AppendLine($"    Lý do gốc: {it.ThesisOrReason}");
        }

        sb.Append("</decision_queue>");
        return sb.ToString();
    }
```

> `OrderBy(i => i.Severity)` hoạt động vì enum khai báo theo thứ tự `Critical = 0, Warning = 1, Info = 2`.

- [ ] **Step 4: Chạy test, xác nhận PASS**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatDecisionQueueSection"`
Expected: PASS — 3 passed

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs
git commit -m "feat(digest): add decision queue section"
```

---

### Task 8: `FormatRiskAlertsSection` + `FormatDrillDownSection`

Nâng cảnh báo từ "lỗ > 5%" thành luật theo rủi ro. Gộp `drill_down` vào cùng task vì nó là một chuỗi tĩnh nhỏ.

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs`
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs`

**Interfaces:**
- Consumes: `PositionDigestRow` (Task 2)
- Produces: `AiAssistantService.FormatRiskAlertsSection(IReadOnlyList<PositionDigestRow> rows) → string`, `AiAssistantService.FormatDrillDownSection() → string`

- [ ] **Step 1: Viết test fail**

```csharp
    // --- FormatRiskAlertsSection: luật theo rủi ro thay vì ngưỡng lỗ tuyệt đối ---

    [Fact]
    public void FormatRiskAlertsSection_BreachedStopLoss_FlaggedFirst()
    {
        var rows = new[]
        {
            Pos("HHV", "24hmoney", sizePct: 87.6m, sl: 10_000m, distSl: -0.3m),
            Pos("HPG", "Swing Trading", sizePct: 5m, sl: 20_000m, distSl: 12m),
        };

        var section = AiAssistantService.FormatRiskAlertsSection(rows);

        section.Should().Contain("<risk_alerts>");
        section.Should().Contain("</risk_alerts>");
        section.Should().Contain("HHV");
        section.Should().Contain("xuyên stop-loss");
        section.Should().NotContain("HPG");   // an toàn trên mọi luật → không cảnh báo
    }

    [Fact]
    public void FormatRiskAlertsSection_NearStopLossWithin3Percent_Flagged()
    {
        var section = AiAssistantService.FormatRiskAlertsSection(
            new[] { Pos("MWG", "A", sizePct: 5m, sl: 50_000m, distSl: 2.1m) });

        section.Should().Contain("sát stop-loss");
    }

    [Fact]
    public void FormatRiskAlertsSection_ConcentrationAtLeast30Percent_FlaggedWithActualPercent()
    {
        var section = AiAssistantService.FormatRiskAlertsSection(
            new[] { Pos("HHV", "24hmoney", sizePct: 87.6m, sl: 5_000m, distSl: 50m) });

        section.Should().Contain("tập trung quá mức");
        section.Should().Contain("87.6%");
    }

    [Fact]
    public void FormatRiskAlertsSection_MissingStopLoss_Flagged()
    {
        var section = AiAssistantService.FormatRiskAlertsSection(
            new[] { Pos("FPT", "24hmoney", sizePct: 5m, sl: null, distSl: null) });

        section.Should().Contain("chưa đặt stop-loss");
    }

    [Fact]
    public void FormatRiskAlertsSection_LossAtOrBeyondMinus15Percent_Flagged()
    {
        var row = Pos("MWG", "A", sizePct: 5m, sl: 5_000m, distSl: 40m) with { UnrealizedPnLPercent = -16m };

        AiAssistantService.FormatRiskAlertsSection(new[] { row }).Should().Contain("lỗ nặng");
    }

    [Fact]
    public void FormatRiskAlertsSection_LossOnlyMinus8Percent_NotFlagged()
    {
        // Ngưỡng cũ -5% quá nhiễu; nay -15%
        var row = Pos("MWG", "A", sizePct: 5m, sl: 5_000m, distSl: 40m) with { UnrealizedPnLPercent = -8m };

        AiAssistantService.FormatRiskAlertsSection(new[] { row }).Should().BeEmpty();
    }

    [Fact]
    public void FormatRiskAlertsSection_NoAlerts_ReturnsEmpty()
    {
        var row = Pos("HPG", "A", sizePct: 5m, sl: 20_000m, distSl: 15m) with { UnrealizedPnLPercent = 3m };

        AiAssistantService.FormatRiskAlertsSection(new[] { row }).Should().BeEmpty();
    }

    // --- FormatDrillDownSection: cho agent biết còn tool nào để tra sâu hơn ---

    [Fact]
    public void FormatDrillDownSection_ListsToolsForDeeperQuestions()
    {
        var section = AiAssistantService.FormatDrillDownSection();

        section.Should().Contain("<drill_down>");
        section.Should().Contain("</drill_down>");
        section.Should().Contain("get_performance");
        section.Should().Contain("get_technical_analysis");
        section.Should().Contain("get_discipline_score");
    }
```

- [ ] **Step 2: Chạy test, xác nhận FAIL**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatRiskAlertsSection|FullyQualifiedName~FormatDrillDownSection"`
Expected: FAIL — `CS0117` cho cả hai method

- [ ] **Step 3: Implement**

```csharp
    private const decimal NearStopLossPercent = 3m;
    private const decimal ConcentrationPercent = 30m;
    private const decimal HeavyLossPercent = -15m;

    /// <summary>
    /// Cảnh báo rủi ro theo mức nghiêm trọng. Một vị thế có thể trúng nhiều luật —
    /// in tất cả các luật nó trúng để agent thấy đủ bức tranh.
    /// </summary>
    public static string FormatRiskAlertsSection(IReadOnlyList<PositionDigestRow> rows)
    {
        var lines = new List<string>();

        foreach (var r in rows.Where(r => r.DistanceToStopLossPercent <= 0)
                              .OrderBy(r => r.DistanceToStopLossPercent))
            lines.Add($"  🔴 {r.Symbol} ({r.PortfolioName}): đã xuyên stop-loss — giá {r.CurrentPrice:N0}, SL {r.StopLossPrice:N0}");

        foreach (var r in rows.Where(r => r.DistanceToStopLossPercent > 0
                                       && r.DistanceToStopLossPercent <= NearStopLossPercent)
                              .OrderBy(r => r.DistanceToStopLossPercent))
            lines.Add($"  🟠 {r.Symbol} ({r.PortfolioName}): sát stop-loss — còn {r.DistanceToStopLossPercent:0.0}%");

        foreach (var r in rows.Where(r => r.PositionSizePercent >= ConcentrationPercent)
                              .OrderByDescending(r => r.PositionSizePercent))
            lines.Add($"  ⚠️ {r.Symbol} ({r.PortfolioName}): tập trung quá mức — {r.PositionSizePercent!.Value:0.0}% danh mục");

        foreach (var r in rows.Where(r => r.RiskDataAvailable && !r.StopLossPrice.HasValue))
            lines.Add($"  ⚠️ {r.Symbol} ({r.PortfolioName}): chưa đặt stop-loss");

        foreach (var r in rows.Where(r => r.UnrealizedPnLPercent <= HeavyLossPercent)
                              .OrderBy(r => r.UnrealizedPnLPercent))
            lines.Add($"  📉 {r.Symbol} ({r.PortfolioName}): lỗ nặng {r.UnrealizedPnLPercent:+0.0;-0.0}% ({r.UnrealizedPnL:+#,0;-#,0} VND)");

        if (lines.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("<risk_alerts>");
        foreach (var l in lines) sb.AppendLine(l);
        sb.Append("</risk_alerts>");
        return sb.ToString();
    }

    /// <summary>
    /// Cho agent biết dữ liệu nào KHÔNG có trong bản tin và tool nào tra được —
    /// để nó không trả lời chắc chắn từ dữ liệu mình không có.
    /// </summary>
    public static string FormatDrillDownSection()
        => """
           <drill_down>
             Bản tin chỉ chứa dữ liệu đủ để quyết định. Cần sâu hơn thì gọi tool:
             - Hiệu suất lịch sử (TWR/CAGR/Sharpe/max drawdown): get_performance, get_adjusted_return
             - Đường vốn, lợi nhuận theo tháng: get_equity_curve, get_monthly_returns
             - Phân tích kỹ thuật, lịch sử giá: get_technical_analysis, get_stock_price_history
             - Kỷ luật, streak: get_discipline_score, get_discipline_streak
             - Dòng thời gian một mã: get_symbol_timeline
             - Vị thế/lệnh đầy đủ: list_positions, get_trades_by_portfolio
           </drill_down>
           """;
```

- [ ] **Step 4: Chạy test, xác nhận PASS**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter "FullyQualifiedName~FormatRiskAlertsSection|FullyQualifiedName~FormatDrillDownSection"`
Expected: PASS — 8 passed

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs
git commit -m "feat(digest): upgrade risk alerts to risk-based rules, add drill-down hints"
```

---

### Task 9: Wire tất cả vào `BuildDailyBriefingContext`

Task duy nhất chạm orchestration. Thêm 2 dependency, fetch thêm 4 nguồn song song, lắp formatter, sửa nền vốn của position sizing.

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs:14-31` (khai báo field), constructor, `:1665-1860` (`BuildDailyBriefingContext`)
- Modify: `src/InvestmentApp.Api/Program.cs` — chỉ kiểm tra, thường không cần sửa nếu DI đã đăng ký `ICapitalFlowRepository` và MediatR

**Interfaces:**
- Consumes: mọi formatter từ Task 3–8; `PortfolioCashCalculator.Compute` (Task 1); `ICapitalFlowRepository.GetTotalFlowByPortfolioIdAsync(string, CancellationToken) → Task<decimal>`; `IRiskCalculationService.GetPortfolioRiskSummaryAsync(string, CancellationToken) → Task<PortfolioRiskSummary>`; `IMediator.Send(new GetDecisionQueueQuery { UserId = ... }, ct) → Task<DecisionQueueDto>`
- Produces: payload bản tin hoàn chỉnh (không có API mới ra ngoài)

- [ ] **Step 1: Kiểm tra DI đã đăng ký sẵn hay chưa**

```bash
grep -rn "ICapitalFlowRepository\|AddMediatR" src/InvestmentApp.Api/Program.cs src/InvestmentApp.Infrastructure/DependencyInjection.cs 2>/dev/null
```

Nếu `ICapitalFlowRepository` chưa đăng ký thì thêm cùng chỗ các repository khác. `IMediator` chắc chắn đã có vì controller đang dùng. **Không** thêm dependency vào `DigestTools.cs` — mọi tham số của `[McpServerTool]` không được DI-đăng ký sẽ rò rỉ vào inputSchema.

- [ ] **Step 2: Thêm 2 field + tham số constructor**

Thêm vào khối field (sau dòng 31):

```csharp
    private readonly ICapitalFlowRepository _capitalFlowRepo;
    private readonly IMediator _mediator;
```

Thêm 2 tham số vào cuối danh sách tham số constructor và gán:

```csharp
        _capitalFlowRepo = capitalFlowRepo;
        _mediator = mediator;
```

Thêm using: `using MediatR;`, `using InvestmentApp.Application.Decisions.DTOs;`, `using InvestmentApp.Application.Decisions.Queries.GetDecisionQueue;`, `using InvestmentApp.Application.Common;`

- [ ] **Step 3: Thêm fetch song song trong `BuildDailyBriefingContext`**

Ngay sau `var portfolioList = portfolios.ToList();` (dòng 1671), thêm — giữ đúng pattern `ContinueWith` hiện có:

```csharp
        // Trades + capital flows: cần cho tiền mặt danh mục VÀ cho <recent_trades> (lọc lại in-memory,
        // không tốn thêm call). Risk summary: %DM + khoảng cách SL.
        var tradeTasks = portfolioList.Select(p =>
            _tradeRepo.GetByPortfolioIdAsync(p.Id, ct)
                .ContinueWith(t => t.IsCompletedSuccessfully ? t.Result.ToList() : null, TaskContinuationOptions.ExecuteSynchronously)
        ).ToList();
        var flowTasks = portfolioList.Select(p =>
            _capitalFlowRepo.GetTotalFlowByPortfolioIdAsync(p.Id, ct)
                .ContinueWith(t => t.IsCompletedSuccessfully ? (decimal?)t.Result : null, TaskContinuationOptions.ExecuteSynchronously)
        ).ToList();
        var riskTasks = portfolioList.Select(p =>
            _riskService.GetPortfolioRiskSummaryAsync(p.Id, ct)
                .ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : null, TaskContinuationOptions.ExecuteSynchronously)
        ).ToList();
        var decisionTask = _mediator.Send(new GetDecisionQueueQuery { UserId = userId }, ct)
            .ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : null, TaskContinuationOptions.ExecuteSynchronously);
```

Thêm các task này vào `Task.WhenAll` hiện có (dòng 1691) — cùng một budget timeout, **không** mở window mới:

```csharp
            await Task.WhenAll(pnlTasks.Cast<Task>()
                    .Concat(tradeTasks).Concat(flowTasks).Concat(riskTasks)
                    .Append(decisionTask)
                    .Append(plansTask).Append(watchlistsTask).Append(profileTask).Append(marketTask))
                .WaitAsync(timeout, ct);
```

- [ ] **Step 4: Thay khối tổng hợp portfolio + cash + positions**

Thay đoạn dòng 1708-1774 (từ `// Portfolio overview` tới hết `<top_positions>`/`<risk_alerts>`) bằng:

```csharp
        // Gom từng danh mục: PnL + cash + risk. Index khớp nhau vì cùng dựng từ portfolioList.
        var portfolioRows = new List<PortfolioDigestRow>();
        var positionRows = new List<PositionDigestRow>();
        var tradeRows = new List<TradeDigestRow>();
        decimal totalInvested = 0;
        var tradeCutoff = DateTime.UtcNow.Date.AddDays(-RecentTradeWindowDays);

        for (var i = 0; i < portfolioList.Count; i++)
        {
            var p = portfolioList[i];
            var pnl = pnlTasks[i].IsCompletedSuccessfully ? pnlTasks[i].Result : null;
            var trades = tradeTasks[i].IsCompletedSuccessfully ? tradeTasks[i].Result : null;
            var netFlow = flowTasks[i].IsCompletedSuccessfully ? flowTasks[i].Result : null;
            var risk = riskTasks[i].IsCompletedSuccessfully ? riskTasks[i].Result : null;

            if (pnl != null) totalInvested += pnl.TotalInvested;

            // Cash chỉ tính khi có ĐỦ trades + netFlow; thiếu một trong hai → n/a, tuyệt đối không in 0.
            decimal? cash = trades != null && netFlow.HasValue
                ? PortfolioCashCalculator.Compute(p.InitialCapital, netFlow.Value, trades)
                : null;

            portfolioRows.Add(new PortfolioDigestRow(
                p.Name, pnl?.TotalMarketValue ?? 0m, cash,
                pnl?.TotalUnrealizedPnL ?? 0m, pnl?.TotalRealizedPnL ?? 0m));

            var riskBySymbol = risk?.Positions.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

            foreach (var pos in pnl?.Positions ?? new List<PositionPnL>())
            {
                PositionRiskItem? item = null;
                if (riskBySymbol != null && riskBySymbol.TryGetValue(pos.Symbol, out var found))
                    item = found;

                positionRows.Add(new PositionDigestRow(
                    pos.Symbol, p.Name, pos.Quantity, pos.AverageCost, pos.CurrentPrice,
                    pos.MarketValue, pos.UnrealizedPnL, pos.UnrealizedPnLPercentage,
                    PositionSizePercent: item?.PositionSizePercent,
                    StopLossPrice: item?.StopLossPrice,
                    // Khoảng cách tới SL chỉ có nghĩa khi SL đã được đặt.
                    DistanceToStopLossPercent: item is { StopLossPrice: not null }
                        ? item.DistanceToStopLossPercent
                        : null,
                    RiskDataAvailable: risk != null));
            }

            foreach (var t in (trades ?? new List<Trade>()).Where(t => t.TradeDate.Date >= tradeCutoff))
                tradeRows.Add(new TradeDigestRow(
                    t.TradeDate, p.Name, t.Symbol, t.TradeType == TradeType.BUY,
                    t.Quantity, t.Price, t.Quantity * t.Price));
        }

        sb.AppendLine();
        sb.AppendLine(FormatPortfolioOverviewSection(portfolioRows, totalInvested));

        // Cash + net-worth. investableCapital là account balance cho position sizing bên dưới —
        // BUG GỐC: trước đây thiếu toàn bộ tiền mặt danh mục nên mọi khối lượng gợi ý đều thấp hơn thực tế.
        var totalMarketValue = portfolioRows.Sum(r => r.MarketValue);
        decimal? totalCash = portfolioRows.All(r => !r.Cash.HasValue) && portfolioRows.Count > 0
            ? null
            : portfolioRows.Where(r => r.Cash.HasValue).Sum(r => r.Cash!.Value);

        var profile = profileTask.IsCompletedSuccessfully ? profileTask.Result : null;
        decimal? idleCash = profile?.Accounts
            .Where(a => a.Type == FinancialAccountType.IdleCash).Sum(a => a.Balance);

        var investableCapital = totalMarketValue + (totalCash ?? 0m) + (idleCash ?? 0m);

        sb.AppendLine();
        sb.AppendLine(FormatCashNetWorthSection(
            investableCapital, totalCash, idleCash,
            profile?.GetNetWorth(totalMarketValue), profile?.GetTotalAssets(totalMarketValue),
            profile?.GetTotalDebt(), profile?.CalculateHealthScore(totalMarketValue)));

        var positionsSection = FormatPositionsSection(positionRows);
        if (positionsSection.Length > 0) { sb.AppendLine(); sb.AppendLine(positionsSection); }

        var tradesSection = FormatRecentTradesSection(tradeRows);
        if (tradesSection.Length > 0) { sb.AppendLine(); sb.AppendLine(tradesSection); }

        var decisionQueue = decisionTask.IsCompletedSuccessfully ? decisionTask.Result : null;
        if (decisionQueue != null)
        {
            var decisionSection = FormatDecisionQueueSection(decisionQueue.Items);
            if (decisionSection.Length > 0) { sb.AppendLine(); sb.AppendLine(decisionSection); }
        }

        var alertsSection = FormatRiskAlertsSection(positionRows);
        if (alertsSection.Length > 0) { sb.AppendLine(); sb.AppendLine(alertsSection); }
```

Thêm hằng số cạnh các cap khác:

```csharp
    private const int RecentTradeWindowDays = 14;
```

Xoá dòng khai báo cũ `decimal totalInvested = 0, totalValue = 0, totalPnL = 0;` và `var allPositions = new List<PositionPnL>();` cùng vòng `foreach (var task in pnlTasks)` đã bị thay thế.

- [ ] **Step 5: Thêm `<drill_down>` và cập nhật systemPrompt**

Ngay trước `var systemPrompt = BasePrompt + @"` (dòng 1848):

```csharp
        sb.AppendLine();
        sb.AppendLine(FormatDrillDownSection());
```

Sửa systemPrompt — thêm luật đọc dữ liệu vào cuối chuỗi hiện có, trước dấu `";`:

```
7. **Luật đọc dữ liệu (bắt buộc)**:
   - `n/a` nghĩa là CHƯA LẤY ĐƯỢC dữ liệu, KHÔNG phải bằng 0. Không được kết luận ""không có"" từ `n/a` — hãy nói rõ là chưa có dữ liệu.
   - Tiền khả dụng = `<portfolio_cash>` (tiền trong tài khoản chứng khoán) + `<idle_cash>` (tiền ngoài, từ hồ sơ tài chính). Đừng nói ""hết tiền"" khi `<portfolio_cash>` còn số dư.
   - Luôn nêu TÊN DANH MỤC khi khuyên mua/bán, vì mỗi vị thế thuộc một danh mục cụ thể.
   - Đọc `<recent_trades>` trước khi nhận định một vị thế — có thể người dùng vừa bán bớt.
   - `<decision_queue>` là việc cần quyết hôm nay: đưa lên đầu phần ""Cần hành động ngay"".
   - Nếu cần dữ liệu không có trong bản tin, gọi tool ở `<drill_down>` thay vì suy đoán.
```

- [ ] **Step 6: Build + chạy toàn bộ test**

Run: `dotnet build && dotnet test tests/InvestmentApp.Infrastructure.Tests tests/InvestmentApp.Application.Tests tests/InvestmentApp.Api.Tests`
Expected: Build succeeded; tất cả test pass. Nếu `McpToolDiscoveryTests` fail → có dependency rò rỉ vào inputSchema, quay lại Step 1 và bỏ tham số vừa thêm khỏi `DigestTools`.

- [ ] **Step 7: Verify thật bằng curl (bắt buộc trước khi PR)**

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/InvestmentApp.Api &
# Mint JWT theo skill qa-verify (MintStableJwt), rồi:
curl -s -X POST http://localhost:5000/api/v1/ai/daily-digest \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" -d '{}' \
  | python -c "import sys,json; print(json.load(sys.stdin)['userMessage'])"
```

Kiểm tra bằng mắt: `<portfolio_cash>` **khác 0**, có dòng `<portfolio name=... cash=... />` cho từng danh mục, `<recent_trades>` hiện lệnh bán HHV 24/07, `<positions>` có cột danh mục. Dán output vào PR description.

> ⚠️ `appsettings.Development.json` trỏ `DatabaseName=InvestmentApp_prod` — đây là read-only endpoint nên an toàn, nhưng **không** chạy lệnh ghi nào trong lúc verify.

- [ ] **Step 8: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs
git commit -m "fix(digest): include portfolio cash in daily digest and investable capital

Digest read idle cash only from the personal finance profile, so proceeds
from sold positions were invisible. Investable capital fed position sizing,
making every suggested quantity too low. Now computes per-portfolio cash
from initial capital, capital flows and gross trades."
```

---

### Task 10: ADR + tài liệu

**Files:**
- Create: `docs/adr/NNNN-portfolio-cash-formula-divergence.md` (N = số tiếp theo, xem `docs/adr/README.md`)
- Modify: `docs/architecture.md`, `docs/business-domain.md`, `frontend/src/assets/CHANGELOG.md`

- [ ] **Step 1: Xác định số ADR tiếp theo**

```bash
ls docs/adr/ | grep -E "^[0-9]{4}" | sort | tail -3; cat docs/adr/template.md
```

- [ ] **Step 2: Viết ADR theo template**

Nội dung phải có:
- **Context:** ba nơi tính tiền mặt danh mục; `CashFlowAdjustedReturnService:432` dùng `Initial + flows − grossBuys + grossSells` (có realized P&L), còn `RiskCalculationService:91` và `SnapshotService:53` dùng `Initial + flows − TotalInvested` (không có realized, vì `TotalInvested` chỉ phản ánh vị thế đang mở).
- **Decision:** bản tin dùng công thức có realized qua `PortfolioCashCalculator`. Không sửa risk/snapshot ở PR này.
- **Consequences:** cho danh mục đã có vị thế chốt, `portfolio_cash` trong bản tin **sẽ lệch** so với cash mà API risk trả về. Đây là chấp nhận có ý thức, không phải bug mới. Kế hoạch hợp nhất: chuyển risk + snapshot sang `PortfolioCashCalculator` ở một PR riêng có test cho số liệu snapshot lịch sử.
- **Đã verify:** `GetTotalFlowByPortfolioIdAsync` lọc `!f.IsSeedDeposit` (`CapitalFlowRepository.cs:63`) nên vốn ban đầu không bị đếm hai lần.

- [ ] **Step 3: Cập nhật `docs/architecture.md`**

Trong phần mô tả `AiAssistantService`: thêm `ICapitalFlowRepository` + `IMediator` vào danh sách dependency, ghi chú đây là chỗ đầu tiên Infrastructure dùng `IMediator` và lý do (một điểm hợp nhất duy nhất để MCP tool và HTTP endpoint không lệch nhau).

- [ ] **Step 4: Cập nhật `docs/business-domain.md`**

Thêm định nghĩa hai khái niệm tiền, nói rõ đây là hai túi tiền khác nhau:
- `portfolio_cash` — tiền chưa giải ngân trong tài khoản chứng khoán, suy ra từ vốn ban đầu + nạp/rút + lệnh mua/bán.
- `idle_cash` — tiền mặt nhàn rỗi ngoài tài khoản chứng khoán, người dùng tự khai trong hồ sơ tài chính (`FinancialAccountType.IdleCash`).
- `investable_capital` = giá trị thị trường + `portfolio_cash` + `idle_cash`.

- [ ] **Step 5: Cập nhật `frontend/src/assets/CHANGELOG.md`**

Prepend entry (tiếng Việt có dấu):

```markdown
### Sửa lỗi — Bản tin hằng ngày báo sai tiền mặt

- Bản tin trước đây chỉ đọc tiền mặt từ hồ sơ tài chính cá nhân nên tiền thu về từ các lệnh bán bị bỏ sót, dẫn tới báo "không có tiền mặt" và gợi ý khối lượng mua thấp hơn thực tế.
- Bản tin nay tách rõ **tiền trong tài khoản chứng khoán** và **tiền nhàn rỗi ngoài tài khoản**.
- Bổ sung: bóc số liệu theo từng danh mục, lãi/lỗ đã thực hiện, lệnh 14 ngày gần nhất, hàng đợi quyết định, % tập trung và khoảng cách tới cắt lỗ cho mỗi vị thế.
```

- [ ] **Step 6: Commit**

```bash
git add docs/adr/ docs/architecture.md docs/business-domain.md frontend/src/assets/CHANGELOG.md
git commit -m "docs: record cash formula divergence ADR and digest changes"
```

---

## Self-Review

**1. Spec coverage**

| Mục spec | Task |
|---|---|
| §1.1 bug `portfolio_cash` | 1, 3, 9 |
| §1.2 per-portfolio + realized | 4, 9 |
| §1.3 concentration, SL distance, decision queue, recent trades | 5, 6, 7, 8, 9 |
| §4.1 dependency mới | 9 |
| §5 payload từng block | 3–8, wire ở 9 |
| §5.1 cap + in rõ số dòng bỏ | 5, 6 |
| §6 công thức cash | 1 |
| §6.3 ADR phân kỳ | 10 |
| §6.4 trích helper | 1 |
| §7 sửa nền vốn sizing | 9 Step 4 |
| §8 suy giảm mềm, không in 0 | 3, 4, 5, 9 Step 4 |
| §9 kế hoạch test | test trong 1, 3–8; guard MCP schema ở 9 Step 6 |
| §11 tài liệu | 10 |

Không có mục nào của spec thiếu task.

**2. Placeholder scan** — không có TBD/TODO; mọi step code đều có code block thật; `NNNN` trong tên file ADR có step riêng để tra số thật.

**3. Type consistency** — `PortfolioDigestRow`/`PositionDigestRow`/`TradeDigestRow` định nghĩa ở Task 2, dùng nguyên tên field ở Task 4–8 và Task 9. `PortfolioCashCalculator.Compute` cùng signature ở Task 1 và Task 9. `FormatCashNetWorthSection` 7 tham số nhất quán giữa Task 3 và Task 9. Helper test `Pos(...)` định nghĩa ở Task 5, dùng lại ở Task 8 — **hai task này phải chạy theo đúng thứ tự**.

**Thứ tự task bắt buộc:** 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10. Task 2 phải xong trước 4–8 (record). Task 9 phải sau tất cả formatter. Task 3 làm call site cũ tạm không biên dịch được cho tới Task 9 — đã ghi cách giữ build xanh ở Task 3 Step 4.
