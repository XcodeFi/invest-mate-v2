# Sự kiện quyền (cổ tức & chia tách) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ghi nhận cổ tức tiền mặt, cổ tức cổ phiếu và chia tách cổ phiếu, rồi tính lại giá vốn / lãi lỗ danh mục cho đúng — kể cả trong khoảng thời gian giữa ngày GDKHQ và ngày cổ phiếu/tiền về tài khoản.

**Architecture:** Entity mới `CorporateAction` là nguồn sự thật bất biến; `Trade` không bao giờ bị sửa. Một hàm thuần duy nhất `PositionBuilder.Build(trades, actions, asOf)` dựng vị thế đã điều chỉnh, và mọi service dùng số để ra quyết định gọi vào đó thay vì tự `GroupBy` trên `Trade` thô. Giá ngưỡng (cắt lỗ, mục tiêu) được điều chỉnh **tại thời điểm đọc** qua `CorporateActionAdjuster`, không sửa dữ liệu.

**Tech Stack:** .NET 9, Clean Architecture, MongoDB Driver 3.6.0, MediatR, xUnit + FluentAssertions + Moq, Angular 19 standalone + inline template + Tailwind, Karma/Jasmine.

**Spec:** [`docs/superpowers/specs/2026-08-08-corporate-actions-design.md`](../specs/2026-08-08-corporate-actions-design.md)

## Global Constraints

- **Tiếng Việt có dấu đầy đủ** cho mọi text hiển thị: label, button, placeholder, message, error, tooltip.
- **Commit message tiếng Việt có dấu**, giữ prefix conventional-commit tiếng Anh (`feat`/`fix`/`docs`/`refactor`).
- **Không có trailer `Co-Authored-By`.**
- **TDD bắt buộc:** viết test đỏ trước, chạy cho fail, rồi mới implement.
- MongoDB: collection **snake_case** (`corporate_actions`), field **PascalCase** (driver không đăng ký convention camelCase).
- Mọi input mã chứng khoán dùng directive `appUppercase` (`shared/directives/uppercase.directive.ts`). Không dùng CSS `uppercase` hay `toUpperCase()` inline.
- Modal: thứ tự nút `[Hủy]` → `[destructive]` → `[primary]` (primary bên phải). Overlay dùng `z-[60]`.
- Mệnh giá cổ phiếu VN cố định **10.000đ**. Thuế TNCN cổ tức tiền mặt mặc định **5%**.
- `RatioNew` là **tổng số cổ phiếu sau sự kiện**, không phải số nhận thêm (30% → `100:130`).
- Kiểm tra quyền sở hữu **theo chuỗi**: `portfolio.UserId == userId` **và** `action.PortfolioId == portfolio.Id`. Không tin `PortfolioId` client gửi.
- Nhánh làm việc: `feature/corporate-actions`, tách từ `origin/master`.

---

### Task 1: Entity `CorporateAction`

**Files:**
- Create: `src/InvestmentApp.Domain/Entities/CorporateAction.cs`
- Test: `tests/InvestmentApp.Domain.Tests/Entities/CorporateActionTests.cs`

**Interfaces:**
- Consumes: `AggregateRoot` (`src/InvestmentApp.Domain/Entities/AggregateRoot.cs`)
- Produces: `CorporateAction` với các thuộc tính `PortfolioId`, `UserId`, `Symbol`, `Type`, `ExDate`, `SettlementDate`, `SettledAt`, `AmountPerShare`, `TaxRatePercent`, `RatioOld`, `RatioNew`, `DeclaredText`, `CapitalFlowId`, `Note`; các thành viên tính toán `Multiplier`, `NetPerShare`; các phương thức `MarkSettled(DateTime)`, `LinkCapitalFlow(string)`; enum `CorporateActionType { CashDividend, StockDividend, StockSplit }`; hằng số `CorporateAction.ParValue = 10_000m`.

- [ ] **Step 1: Viết test đỏ**

```csharp
using FluentAssertions;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Domain.Tests.Entities;

public class CorporateActionTests
{
    private static readonly DateTime Ex = new(2026, 6, 10);

    [Fact]
    public void CashDividend_QuyDoiPhanTramTheoMenhGia()
    {
        var action = CorporateAction.CashDividend(
            "p1", "u1", "sab", percentOfPar: 5m, exDate: Ex,
            settlementDate: new DateTime(2026, 7, 10), taxRatePercent: 5m);

        action.Symbol.Should().Be("SAB");
        action.AmountPerShare.Should().Be(500m);
        action.NetPerShare.Should().Be(475m);
        action.Multiplier.Should().Be(1m);
        action.DeclaredText.Should().Be("5%");
    }

    [Fact]
    public void StockDividend_TinhMultiplierTuTyLeTong()
    {
        var action = CorporateAction.StockDividend(
            "p1", "u1", "HPG", ratioOld: 100m, ratioNew: 130m, exDate: Ex, settlementDate: null);

        action.Multiplier.Should().Be(1.3m);
        action.AmountPerShare.Should().BeNull();
    }

    [Fact]
    public void StockSplit_TinhMultiplier()
    {
        var action = CorporateAction.StockSplit("p1", "u1", "VNM", 1m, 2m, Ex, null);
        action.Multiplier.Should().Be(2m);
    }

    [Fact]
    public void CashDividend_SoTienKhongDuong_ThiNem()
    {
        var act = () => CorporateAction.CashDividend("p1", "u1", "SAB", 0m, Ex, null, 5m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StockDividend_TyLeMoiKhongLonHonCu_ThiNem()
    {
        var act = () => CorporateAction.StockDividend("p1", "u1", "HPG", 100m, 100m, Ex, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NgayVeTruocNgayGDKHQ_ThiNem()
    {
        var act = () => CorporateAction.StockDividend(
            "p1", "u1", "HPG", 100m, 130m, Ex, Ex.AddDays(-1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkSettled_GhiNgayVeThucTe()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100m, 130m, Ex, null);
        action.MarkSettled(new DateTime(2026, 7, 20));

        action.SettledAt.Should().Be(new DateTime(2026, 7, 20));
    }

    [Fact]
    public void MarkSettled_TruocNgayGDKHQ_ThiNem()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100m, 130m, Ex, null);
        var act = () => action.MarkSettled(Ex.AddDays(-1));
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Domain.Tests --filter FullyQualifiedName~CorporateActionTests`
Expected: FAIL — `CorporateAction` chưa tồn tại (lỗi biên dịch CS0246).

- [ ] **Step 3: Implement entity**

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Sự kiện quyền của một mã trong danh mục. Bất biến — sửa = xoá và tạo lại.
/// </summary>
public class CorporateAction : AggregateRoot
{
    /// <summary>Mệnh giá cổ phiếu niêm yết VN — gốc để quy đổi "cổ tức 5%" ra đồng/CP.</summary>
    public const decimal ParValue = 10_000m;

    public string PortfolioId { get; private set; } = null!;
    public string UserId { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;
    public CorporateActionType Type { get; private set; }
    public DateTime ExDate { get; private set; }
    public DateTime? SettlementDate { get; private set; }
    public DateTime? SettledAt { get; private set; }
    public decimal? AmountPerShare { get; private set; }
    public decimal? TaxRatePercent { get; private set; }
    public decimal? RatioOld { get; private set; }
    public decimal? RatioNew { get; private set; }
    public string DeclaredText { get; private set; } = string.Empty;
    public string? CapitalFlowId { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }

    [BsonConstructor]
    public CorporateAction() { } // MongoDB

    private CorporateAction(string portfolioId, string userId, string symbol,
        CorporateActionType type, DateTime exDate, DateTime? settlementDate, string declaredText, string? note)
    {
        Id = Guid.NewGuid().ToString();
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Symbol = symbol?.ToUpper().Trim() ?? throw new ArgumentNullException(nameof(symbol));
        Type = type;
        ExDate = exDate.Date;
        if (settlementDate.HasValue && settlementDate.Value.Date < ExDate)
            throw new ArgumentException("Ngày về không được trước ngày GDKHQ", nameof(settlementDate));
        SettlementDate = settlementDate?.Date;
        DeclaredText = declaredText;
        Note = note;
        CreatedAt = DateTime.UtcNow;
    }

    public static CorporateAction CashDividend(string portfolioId, string userId, string symbol,
        decimal percentOfPar, DateTime exDate, DateTime? settlementDate, decimal taxRatePercent,
        string? note = null)
    {
        if (percentOfPar <= 0) throw new ArgumentException("Tỷ lệ cổ tức phải lớn hơn 0", nameof(percentOfPar));
        if (taxRatePercent < 0 || taxRatePercent >= 100)
            throw new ArgumentException("Thuế suất phải trong khoảng [0, 100)", nameof(taxRatePercent));

        return new CorporateAction(portfolioId, userId, symbol, CorporateActionType.CashDividend,
            exDate, settlementDate, $"{percentOfPar:0.##}%", note)
        {
            AmountPerShare = percentOfPar / 100m * ParValue,
            TaxRatePercent = taxRatePercent
        };
    }

    public static CorporateAction StockDividend(string portfolioId, string userId, string symbol,
        decimal ratioOld, decimal ratioNew, DateTime exDate, DateTime? settlementDate, string? note = null)
        => FromRatio(portfolioId, userId, symbol, CorporateActionType.StockDividend,
            ratioOld, ratioNew, exDate, settlementDate, note);

    public static CorporateAction StockSplit(string portfolioId, string userId, string symbol,
        decimal ratioOld, decimal ratioNew, DateTime exDate, DateTime? settlementDate, string? note = null)
        => FromRatio(portfolioId, userId, symbol, CorporateActionType.StockSplit,
            ratioOld, ratioNew, exDate, settlementDate, note);

    private static CorporateAction FromRatio(string portfolioId, string userId, string symbol,
        CorporateActionType type, decimal ratioOld, decimal ratioNew,
        DateTime exDate, DateTime? settlementDate, string? note)
    {
        if (ratioOld <= 0) throw new ArgumentException("Tỷ lệ cũ phải lớn hơn 0", nameof(ratioOld));
        if (ratioNew <= ratioOld)
            throw new ArgumentException("Tỷ lệ mới phải lớn hơn tỷ lệ cũ", nameof(ratioNew));

        return new CorporateAction(portfolioId, userId, symbol, type, exDate, settlementDate,
            $"{ratioOld:0.##}:{ratioNew:0.##}", note)
        {
            RatioOld = ratioOld,
            RatioNew = ratioNew
        };
    }

    /// <summary>Hệ số nhân số lượng cổ phiếu. Cổ tức tiền mặt = 1.</summary>
    public decimal Multiplier =>
        RatioOld.HasValue && RatioNew.HasValue && RatioOld.Value > 0
            ? RatioNew.Value / RatioOld.Value
            : 1m;

    /// <summary>Tiền cổ tức thực nhận trên mỗi cổ phiếu, sau thuế TNCN.</summary>
    public decimal NetPerShare =>
        AmountPerShare.HasValue
            ? AmountPerShare.Value * (1m - (TaxRatePercent ?? 0m) / 100m)
            : 0m;

    public void MarkSettled(DateTime settledAt)
    {
        if (settledAt.Date < ExDate)
            throw new ArgumentException("Ngày về không được trước ngày GDKHQ", nameof(settledAt));
        SettledAt = settledAt.Date;
        IncrementVersion();
    }

    public void LinkCapitalFlow(string capitalFlowId)
    {
        CapitalFlowId = capitalFlowId ?? throw new ArgumentNullException(nameof(capitalFlowId));
        IncrementVersion();
    }
}

public enum CorporateActionType
{
    CashDividend,
    StockDividend,
    StockSplit
}
```

- [ ] **Step 4: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Domain.Tests --filter FullyQualifiedName~CorporateActionTests`
Expected: PASS — 8 test.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Domain/Entities/CorporateAction.cs tests/InvestmentApp.Domain.Tests/Entities/CorporateActionTests.cs
git commit -m "feat(corporate-action): thêm entity sự kiện quyền với quy đổi tỷ lệ và thuế"
```

---

### Task 2: `PositionBuilder` — dựng vị thế đã điều chỉnh

Đây là trái tim của cả tính năng. Hàm thuần, không I/O, không repository.

**Files:**
- Create: `src/InvestmentApp.Application/Common/PositionBuilder.cs`
- Test: `tests/InvestmentApp.Application.Tests/Common/PositionBuilderTests.cs`

**Interfaces:**
- Consumes: `Trade`, `CorporateAction` (Task 1)
- Produces: `record AdjustedPosition(string Symbol, decimal SettledQuantity, decimal PendingQuantity, decimal TotalQuantity, decimal AverageCost, decimal TotalCost, decimal RealizedPnL, decimal DividendNet, decimal PendingDividend)` và `PositionBuilder.Build(IEnumerable<Trade>, IEnumerable<CorporateAction>, DateTime asOf) → IReadOnlyList<AdjustedPosition>`

- [ ] **Step 1: Viết test đỏ**

```csharp
using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Application.Tests.Common;

public class PositionBuilderTests
{
    private static readonly DateTime Ex = new(2026, 6, 10);
    private static readonly DateTime Settled = new(2026, 7, 20);
    private static readonly DateTime Far = new(2026, 12, 31);

    private static Trade Buy(string symbol, decimal qty, decimal price, DateTime date)
        => new("p1", symbol, TradeType.BUY, qty, price, tradeDate: date);

    private static Trade Sell(string symbol, decimal qty, decimal price, DateTime date)
        => new("p1", symbol, TradeType.SELL, qty, price, tradeDate: date);

    [Fact]
    public void CoTucCoPhieu30PhanTram_ChuaVe_ThiVaoChoVe_VaGiamGiaVon()
    {
        var trades = new[] { Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)) };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.SettledQuantity.Should().Be(1000);
        pos.PendingQuantity.Should().Be(300);
        pos.TotalQuantity.Should().Be(1300);
        pos.TotalCost.Should().Be(25_000_000);
        pos.AverageCost.Should().BeApproximately(19_230.77m, 0.01m);
    }

    [Fact]
    public void CoTucCoPhieu_DaXacNhanVe_ThiChuyenSangDaVe()
    {
        var trades = new[] { Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)) };
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled);
        action.MarkSettled(Settled);

        var pos = PositionBuilder.Build(trades, new[] { action }, asOf: Far).Single();

        pos.SettledQuantity.Should().Be(1300);
        pos.PendingQuantity.Should().Be(0);
    }

    [Fact]
    public void CoTucTienMat_KhongDoiGiaVon_VaGhiNhanSauThue()
    {
        var trades = new[] { Buy("SAB", 1000, 55_000, new DateTime(2026, 1, 5)) };
        var actions = new[] { CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, Settled, 5m) };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.AverageCost.Should().Be(55_000);
        pos.PendingDividend.Should().Be(475_000);
        pos.DividendNet.Should().Be(0);
    }

    [Fact]
    public void ChiaTach1An2_NhanDoiSoLuong_ChiaDoiGiaVon()
    {
        var trades = new[] { Buy("VNM", 500, 60_000, new DateTime(2026, 1, 5)) };
        var action = CorporateAction.StockSplit("p1", "u1", "VNM", 1, 2, Ex, Settled);
        action.MarkSettled(Settled);

        var pos = PositionBuilder.Build(trades, new[] { action }, asOf: Far).Single();

        pos.TotalQuantity.Should().Be(1000);
        pos.AverageCost.Should().Be(30_000);
        pos.TotalCost.Should().Be(30_000_000);
    }

    [Fact]
    public void CungNgayGDKHQ_TienMatTinhTrenSoLuongCu_RoiMoiNhanHeSo()
    {
        var trades = new[] { Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)) };
        var actions = new[]
        {
            CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled),
            CorporateAction.CashDividend("p1", "u1", "HPG", 5m, Ex, Settled, 5m)
        };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.PendingDividend.Should().Be(475_000); // 1000 CP, không phải 1300
        pos.TotalQuantity.Should().Be(1300);
    }

    [Fact]
    public void CoPhieuLe_LamTronXuong()
    {
        var trades = new[] { Buy("HPG", 137, 25_000, new DateTime(2026, 1, 5)) };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.TotalQuantity.Should().Be(178); // 137 × 1,3 = 178,1
    }

    [Fact]
    public void BanBotTruocNgayGDKHQ_ChiPhanConGiuDuocHuongQuyen()
    {
        var trades = new[]
        {
            Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)),
            Sell("HPG", 500, 30_000, new DateTime(2026, 3, 1))
        };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.TotalQuantity.Should().Be(650);
        pos.RealizedPnL.Should().Be(2_500_000); // 500 × (30.000 − 25.000)
    }

    [Fact]
    public void SuKienTruocGiaoDichDauTien_ThiBoQua()
    {
        var trades = new[] { Buy("HPG", 1000, 25_000, new DateTime(2026, 7, 1)) };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: Far).Single();

        pos.TotalQuantity.Should().Be(1000);
    }

    [Fact]
    public void KhongCoSuKien_ThiKetQuaGiongHetTinhTuTradeTho()
    {
        var trades = new[]
        {
            Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)),
            Sell("HPG", 400, 28_000, new DateTime(2026, 3, 1))
        };

        var pos = PositionBuilder.Build(trades, Array.Empty<CorporateAction>(), asOf: Far).Single();

        pos.TotalQuantity.Should().Be(600);
        pos.AverageCost.Should().Be(25_000);
        pos.RealizedPnL.Should().Be(1_200_000);
    }

    [Fact]
    public void BanHetTruocNgayGDKHQ_ThiGiaVonBangKhong_KhongChiaChoKhong()
    {
        var trades = new[]
        {
            Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)),
            Sell("HPG", 1000, 30_000, new DateTime(2026, 3, 1))
        };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: Far).Single();

        pos.TotalQuantity.Should().Be(0);
        pos.AverageCost.Should().Be(0);
    }

    [Fact]
    public void PhiVaThue_TinhVaoGiaVonKhiMua_VaTruVaoLaiKhiBan()
    {
        var trades = new[]
        {
            new Trade("p1", "HPG", TradeType.BUY, 1000, 25_000, fee: 50_000, tax: 0,
                tradeDate: new DateTime(2026, 1, 5))
        };

        var pos = PositionBuilder.Build(trades, Array.Empty<CorporateAction>(), asOf: Far).Single();

        pos.TotalCost.Should().Be(25_050_000);
        pos.AverageCost.Should().Be(25_050m);
    }
}
```

- [ ] **Step 2: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~PositionBuilderTests`
Expected: FAIL — `PositionBuilder` chưa tồn tại (CS0246).

- [ ] **Step 3: Implement `PositionBuilder`**

```csharp
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Vị thế một mã sau khi áp dụng sự kiện quyền.
/// <c>TotalQuantity</c> dùng cho mọi phép tính P&amp;L và rủi ro;
/// <c>SettledQuantity</c> là con số khớp sổ công ty chứng khoán.
/// </summary>
public sealed record AdjustedPosition(
    string Symbol,
    decimal SettledQuantity,
    decimal PendingQuantity,
    decimal TotalQuantity,
    decimal AverageCost,
    decimal TotalCost,
    decimal RealizedPnL,
    decimal DividendNet,
    decimal PendingDividend);

/// <summary>
/// Nguồn duy nhất dựng vị thế từ giao dịch + sự kiện quyền. Hàm thuần, không I/O.
/// Mọi service cần giá vốn / số lượng phải gọi vào đây thay vì tự gộp <c>Trade</c> thô.
/// </summary>
public static class PositionBuilder
{
    private sealed class State
    {
        public decimal Settled;
        public decimal Pending;
        public decimal TotalCost;
        public decimal RealizedPnL;
        public decimal DividendNet;
        public decimal PendingDividend;
        public decimal Total => Settled + Pending;
        public decimal AvgCost => Total > 0 ? TotalCost / Total : 0m;
    }

    public static IReadOnlyList<AdjustedPosition> Build(
        IEnumerable<Trade> trades,
        IEnumerable<CorporateAction> actions,
        DateTime asOf)
    {
        var asOfDate = asOf.Date;
        var states = new Dictionary<string, State>(StringComparer.OrdinalIgnoreCase);

        // Trộn hai nguồn thành một chuỗi sự kiện theo thời gian.
        // Trade trước sự kiện quyền cùng ngày; trong sự kiện quyền, tiền mặt trước cổ phiếu.
        var timeline = trades
            .Where(t => t.TradeDate.Date <= asOfDate)
            .Select(t => (Date: t.TradeDate.Date, Order: 0, Trade: (Trade?)t, Action: (CorporateAction?)null))
            .Concat(actions
                .Where(a => a.ExDate <= asOfDate)
                .Select(a => (Date: a.ExDate, Order: a.Type == CorporateActionType.CashDividend ? 1 : 2,
                              Trade: (Trade?)null, Action: (CorporateAction?)a)))
            .OrderBy(e => e.Date).ThenBy(e => e.Order)
            .ToList();

        foreach (var e in timeline)
        {
            if (e.Trade is { } trade)
            {
                var s = GetState(states, trade.Symbol);
                if (trade.TradeType == TradeType.BUY)
                {
                    s.TotalCost += trade.Quantity * trade.Price + trade.Fee + trade.Tax;
                    s.Settled += trade.Quantity;
                }
                else
                {
                    var avg = s.AvgCost;
                    s.RealizedPnL += trade.Quantity * (trade.Price - avg) - trade.Fee - trade.Tax;
                    s.TotalCost -= trade.Quantity * avg;
                    s.Settled -= trade.Quantity;
                }
                continue;
            }

            var action = e.Action!;
            if (!states.TryGetValue(action.Symbol, out var st) || st.Total <= 0)
                continue; // chưa sở hữu tại ngày GDKHQ thì không hưởng quyền

            if (action.Type == CorporateActionType.CashDividend)
            {
                var amount = st.Total * action.NetPerShare;
                if (IsSettled(action, asOfDate)) st.DividendNet += amount;
                else st.PendingDividend += amount;
            }
            else
            {
                var before = st.Total;
                var after = Math.Floor(before * action.Multiplier);
                var added = after - before;
                if (added <= 0) continue;

                if (IsSettled(action, asOfDate)) st.Settled += added;
                else st.Pending += added;
                // TotalCost giữ nguyên → AvgCost tự động giảm
            }
        }

        return states
            .Select(kv => new AdjustedPosition(
                Symbol: kv.Key,
                SettledQuantity: kv.Value.Settled,
                PendingQuantity: kv.Value.Pending,
                TotalQuantity: kv.Value.Total,
                AverageCost: kv.Value.AvgCost,
                TotalCost: kv.Value.TotalCost,
                RealizedPnL: kv.Value.RealizedPnL,
                DividendNet: kv.Value.DividendNet,
                PendingDividend: kv.Value.PendingDividend))
            .OrderBy(p => p.Symbol, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsSettled(CorporateAction action, DateTime asOfDate)
        => action.SettledAt.HasValue && action.SettledAt.Value <= asOfDate;

    private static State GetState(Dictionary<string, State> states, string symbol)
    {
        if (!states.TryGetValue(symbol, out var s))
        {
            s = new State();
            states[symbol] = s;
        }
        return s;
    }
}
```

- [ ] **Step 4: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~PositionBuilderTests`
Expected: PASS — 11 test.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/Common/PositionBuilder.cs tests/InvestmentApp.Application.Tests/Common/PositionBuilderTests.cs
git commit -m "feat(position): dựng vị thế điều chỉnh theo sự kiện quyền qua PositionBuilder"
```

---

### Task 3: `CorporateActionAdjuster` — điều chỉnh giá ngưỡng tại thời điểm đọc

**Files:**
- Create: `src/InvestmentApp.Application/Common/CorporateActionAdjuster.cs`
- Test: `tests/InvestmentApp.Application.Tests/Common/CorporateActionAdjusterTests.cs`

**Interfaces:**
- Consumes: `CorporateAction` (Task 1)
- Produces: `CorporateActionAdjuster.AdjustPrice(decimal price, DateTime setAt, IEnumerable<CorporateAction> actions) → decimal`

- [ ] **Step 1: Viết test đỏ**

```csharp
using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Application.Tests.Common;

public class CorporateActionAdjusterTests
{
    private static readonly DateTime Ex = new(2026, 6, 10);
    private static readonly DateTime SetAt = new(2026, 1, 5);

    [Fact]
    public void CoTucCoPhieu30PhanTram_ChiaGiaNguongChoHeSo()
    {
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null) };

        var adjusted = CorporateActionAdjuster.AdjustPrice(22_000m, SetAt, actions);

        adjusted.Should().BeApproximately(16_923.08m, 0.01m);
    }

    [Fact]
    public void CoTucTienMat_TruSoTienTrenMoiCoPhieu()
    {
        var actions = new[] { CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, null, 5m) };

        var adjusted = CorporateActionAdjuster.AdjustPrice(55_000m, SetAt, actions);

        adjusted.Should().Be(54_500m); // trừ theo số trước thuế
    }

    [Fact]
    public void SuKienTruocKhiDatNguong_ThiKhongApDung()
    {
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null) };

        var adjusted = CorporateActionAdjuster.AdjustPrice(22_000m, new DateTime(2026, 7, 1), actions);

        adjusted.Should().Be(22_000m);
    }

    [Fact]
    public void CungNgay_TienMatTruoc_RoiMoiChiaHeSo()
    {
        var actions = new[]
        {
            CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null),
            CorporateAction.CashDividend("p1", "u1", "HPG", 5m, Ex, null, 5m)
        };

        var adjusted = CorporateActionAdjuster.AdjustPrice(30_000m, SetAt, actions);

        adjusted.Should().BeApproximately(22_692.31m, 0.01m); // (30.000 − 500) / 1,3
    }

    [Fact]
    public void KhongCoSuKien_ThiGiuNguyen()
    {
        var adjusted = CorporateActionAdjuster.AdjustPrice(22_000m, SetAt, Array.Empty<CorporateAction>());
        adjusted.Should().Be(22_000m);
    }
}
```

- [ ] **Step 2: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~CorporateActionAdjusterTests`
Expected: FAIL — CS0246.

- [ ] **Step 3: Implement**

```csharp
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Điều chỉnh một mức giá tuyệt đối (giá vào, cắt lỗ, mục tiêu) theo các sự kiện quyền
/// xảy ra SAU khi mức giá đó được đặt. Áp dụng tại thời điểm đọc — không sửa dữ liệu,
/// nên xoá sự kiện thì ngưỡng tự quay về giá trị cũ.
/// </summary>
public static class CorporateActionAdjuster
{
    public static decimal AdjustPrice(decimal price, DateTime setAt, IEnumerable<CorporateAction> actions)
    {
        var setAtDate = setAt.Date;
        var ordered = actions
            .Where(a => a.ExDate > setAtDate)
            .OrderBy(a => a.ExDate)
            .ThenBy(a => a.Type == CorporateActionType.CashDividend ? 0 : 1);

        var result = price;
        foreach (var a in ordered)
        {
            if (a.Type == CorporateActionType.CashDividend)
                result -= a.AmountPerShare ?? 0m;
            else
                result /= a.Multiplier;
        }
        return result;
    }
}
```

- [ ] **Step 4: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~CorporateActionAdjusterTests`
Expected: PASS — 5 test.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/Common/CorporateActionAdjuster.cs tests/InvestmentApp.Application.Tests/Common/CorporateActionAdjusterTests.cs
git commit -m "feat(corporate-action): điều chỉnh giá ngưỡng theo sự kiện quyền tại thời điểm đọc"
```

---

### Task 4: Repository + đăng ký DI

**Files:**
- Modify: `src/InvestmentApp.Application/RepositoryInterfaces.cs` (thêm interface sau `ICapitalFlowRepository`, dòng 68)
- Create: `src/InvestmentApp.Infrastructure/Repositories/CorporateActionRepository.cs`
- Modify: `src/InvestmentApp.Api/Program.cs:112` (thêm dòng đăng ký ngay dưới `ICapitalFlowRepository`)

**Interfaces:**
- Consumes: `IRepository<T>` (`RepositoryInterfaces.cs:9`), `CorporateAction` (Task 1)
- Produces: `ICorporateActionRepository` với `GetByPortfolioIdAsync(string, CancellationToken)`, `GetByPortfolioIdAndSymbolAsync(string, string, CancellationToken)`, `GetByPortfolioIdsAsync(IEnumerable<string>, CancellationToken)`

Task này không có logic nghiệp vụ nên không viết unit test riêng — đúng theo cách các repository khác trong repo đang làm. Kiểm chứng bằng `dotnet build` và bởi các handler test ở Task 5.

- [ ] **Step 1: Thêm interface**

Chèn vào `src/InvestmentApp.Application/RepositoryInterfaces.cs` ngay sau khối `ICapitalFlowRepository` (kết thúc ở dòng 68):

```csharp
public interface ICorporateActionRepository : IRepository<CorporateAction>
{
    Task<IEnumerable<CorporateAction>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CorporateAction>> GetByPortfolioIdAndSymbolAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default);
    Task<IEnumerable<CorporateAction>> GetByPortfolioIdsAsync(IEnumerable<string> portfolioIds, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Implement repository**

```csharp
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class CorporateActionRepository : ICorporateActionRepository
{
    private readonly IMongoCollection<CorporateAction> _collection;

    public CorporateActionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CorporateAction>("corporate_actions");

        var portfolioIndex = Builders<CorporateAction>.IndexKeys.Ascending(c => c.PortfolioId);
        _collection.Indexes.CreateOne(new CreateIndexModel<CorporateAction>(portfolioIndex));

        var compoundIndex = Builders<CorporateAction>.IndexKeys.Combine(
            Builders<CorporateAction>.IndexKeys.Ascending(c => c.PortfolioId),
            Builders<CorporateAction>.IndexKeys.Ascending(c => c.Symbol),
            Builders<CorporateAction>.IndexKeys.Ascending(c => c.ExDate)
        );
        _collection.Indexes.CreateOne(new CreateIndexModel<CorporateAction>(compoundIndex));
    }

    public async Task<CorporateAction?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.Find(c => c.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<CorporateAction>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public async Task AddAsync(CorporateAction entity, CancellationToken cancellationToken = default)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(CorporateAction entity, CancellationToken cancellationToken = default)
        => await _collection.ReplaceOneAsync(c => c.Id == entity.Id, entity, cancellationToken: cancellationToken);

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => await _collection.DeleteOneAsync(c => c.Id == id, cancellationToken);

    public async Task<IEnumerable<CorporateAction>> GetByPortfolioIdAsync(string portfolioId, CancellationToken cancellationToken = default)
        => await _collection.Find(c => c.PortfolioId == portfolioId)
            .SortByDescending(c => c.ExDate)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<CorporateAction>> GetByPortfolioIdAndSymbolAsync(string portfolioId, string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToUpper().Trim();
        return await _collection.Find(c => c.PortfolioId == portfolioId && c.Symbol == normalized)
            .SortBy(c => c.ExDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CorporateAction>> GetByPortfolioIdsAsync(IEnumerable<string> portfolioIds, CancellationToken cancellationToken = default)
    {
        var ids = portfolioIds.ToList();
        if (ids.Count == 0) return Array.Empty<CorporateAction>();

        var filter = Builders<CorporateAction>.Filter.In(c => c.PortfolioId, ids);
        return await _collection.Find(filter).SortBy(c => c.ExDate).ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 3: Đăng ký DI**

Trong `src/InvestmentApp.Api/Program.cs`, ngay sau dòng 112:

```csharp
builder.Services.AddScoped<ICorporateActionRepository, CorporateActionRepository>();
```

- [ ] **Step 4: Build kiểm chứng**

Run: `dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/RepositoryInterfaces.cs src/InvestmentApp.Infrastructure/Repositories/CorporateActionRepository.cs src/InvestmentApp.Api/Program.cs
git commit -m "feat(corporate-action): repository MongoDB và đăng ký dependency injection"
```

---

### Task 5: Command tạo sự kiện quyền

**Files:**
- Create: `src/InvestmentApp.Application/CorporateActions/Commands/CreateCorporateAction/CreateCorporateActionCommand.cs`
- Test: `tests/InvestmentApp.Application.Tests/CorporateActions/CreateCorporateActionCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ICorporateActionRepository` (Task 4), `IPortfolioRepository`, `CorporateAction` (Task 1)
- Produces: `CreateCorporateActionCommand(string UserId, string PortfolioId, string Symbol, CorporateActionType Type, DateTime ExDate, DateTime? SettlementDate, decimal? PercentOfPar, decimal? TaxRatePercent, decimal? RatioOld, decimal? RatioNew, string? Note) : IRequest<string>` — trả về `Id` của sự kiện vừa tạo.

- [ ] **Step 1: Viết test đỏ**

```csharp
using FluentAssertions;
using InvestmentApp.Application.CorporateActions.Commands.CreateCorporateAction;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.CorporateActions;

public class CreateCorporateActionCommandHandlerTests
{
    private readonly Mock<ICorporateActionRepository> _actions = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly CreateCorporateActionCommandHandler _handler;

    public CreateCorporateActionCommandHandlerTests()
        => _handler = new CreateCorporateActionCommandHandler(_actions.Object, _portfolios.Object);

    private static CreateCorporateActionCommand CashCommand(string userId = "u1") => new(
        UserId: userId, PortfolioId: "p1", Symbol: "sab",
        Type: CorporateActionType.CashDividend,
        ExDate: new DateTime(2026, 6, 10), SettlementDate: new DateTime(2026, 7, 10),
        PercentOfPar: 5m, TaxRatePercent: 5m, RatioOld: null, RatioNew: null, Note: null);

    [Fact]
    public async Task DanhMucCuaNguoiKhac_ThiNemUnauthorized()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u2", "Danh mục", 0));

        var act = () => _handler.Handle(CashCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _actions.Verify(r => r.AddAsync(It.IsAny<CorporateAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DanhMucKhongTonTai_ThiNemArgumentException()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        var act = () => _handler.Handle(CashCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CoTucTienMat_LuuVoiSoTienDaQuyDoi()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u1", "Danh mục", 0));
        CorporateAction? saved = null;
        _actions.Setup(r => r.AddAsync(It.IsAny<CorporateAction>(), It.IsAny<CancellationToken>()))
            .Callback<CorporateAction, CancellationToken>((a, _) => saved = a)
            .Returns(Task.CompletedTask);

        var id = await _handler.Handle(CashCommand(), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Symbol.Should().Be("SAB");
        saved.AmountPerShare.Should().Be(500m);
        saved.PortfolioId.Should().Be("p1");
        id.Should().Be(saved.Id);
    }

    [Fact]
    public async Task CoTucCoPhieu_LuuVoiTyLe()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u1", "Danh mục", 0));
        CorporateAction? saved = null;
        _actions.Setup(r => r.AddAsync(It.IsAny<CorporateAction>(), It.IsAny<CancellationToken>()))
            .Callback<CorporateAction, CancellationToken>((a, _) => saved = a)
            .Returns(Task.CompletedTask);

        var command = new CreateCorporateActionCommand("u1", "p1", "HPG",
            CorporateActionType.StockDividend, new DateTime(2026, 6, 10), null,
            null, null, 100m, 130m, null);

        await _handler.Handle(command, CancellationToken.None);

        saved!.Multiplier.Should().Be(1.3m);
    }

    [Fact]
    public async Task CoTucTienMatThieuTyLe_ThiNemArgumentException()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u1", "Danh mục", 0));

        var command = CashCommand() with { PercentOfPar = null };
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

- [ ] **Step 2: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~CreateCorporateActionCommandHandlerTests`
Expected: FAIL — CS0246.

- [ ] **Step 3: Implement command + handler**

```csharp
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Commands.CreateCorporateAction;

public record CreateCorporateActionCommand(
    string UserId,
    string PortfolioId,
    string Symbol,
    CorporateActionType Type,
    DateTime ExDate,
    DateTime? SettlementDate,
    decimal? PercentOfPar,
    decimal? TaxRatePercent,
    decimal? RatioOld,
    decimal? RatioNew,
    string? Note) : IRequest<string>;

public class CreateCorporateActionCommandHandler
    : IRequestHandler<CreateCorporateActionCommand, string>
{
    private readonly ICorporateActionRepository _actions;
    private readonly IPortfolioRepository _portfolios;

    public CreateCorporateActionCommandHandler(
        ICorporateActionRepository actions, IPortfolioRepository portfolios)
    {
        _actions = actions;
        _portfolios = portfolios;
    }

    public async Task<string> Handle(CreateCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolios.GetByIdAsync(request.PortfolioId, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy danh mục", nameof(request.PortfolioId));
        if (portfolio.UserId != request.UserId)
            throw new UnauthorizedAccessException("Danh mục không thuộc về người dùng này");

        var action = request.Type switch
        {
            CorporateActionType.CashDividend => CorporateAction.CashDividend(
                request.PortfolioId, request.UserId, request.Symbol,
                request.PercentOfPar ?? throw new ArgumentException("Thiếu tỷ lệ cổ tức tiền mặt", nameof(request.PercentOfPar)),
                request.ExDate, request.SettlementDate, request.TaxRatePercent ?? 5m, request.Note),

            CorporateActionType.StockDividend => CorporateAction.StockDividend(
                request.PortfolioId, request.UserId, request.Symbol,
                request.RatioOld ?? throw new ArgumentException("Thiếu tỷ lệ cũ", nameof(request.RatioOld)),
                request.RatioNew ?? throw new ArgumentException("Thiếu tỷ lệ mới", nameof(request.RatioNew)),
                request.ExDate, request.SettlementDate, request.Note),

            CorporateActionType.StockSplit => CorporateAction.StockSplit(
                request.PortfolioId, request.UserId, request.Symbol,
                request.RatioOld ?? throw new ArgumentException("Thiếu tỷ lệ cũ", nameof(request.RatioOld)),
                request.RatioNew ?? throw new ArgumentException("Thiếu tỷ lệ mới", nameof(request.RatioNew)),
                request.ExDate, request.SettlementDate, request.Note),

            _ => throw new ArgumentException("Loại sự kiện quyền không hợp lệ", nameof(request.Type))
        };

        await _actions.AddAsync(action, cancellationToken);
        return action.Id;
    }
}
```

- [ ] **Step 4: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~CreateCorporateActionCommandHandlerTests`
Expected: PASS — 5 test.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/CorporateActions tests/InvestmentApp.Application.Tests/CorporateActions
git commit -m "feat(corporate-action): command tạo sự kiện quyền kèm kiểm tra quyền sở hữu"
```

---

### Task 6: Query danh sách + command xoá

**Files:**
- Create: `src/InvestmentApp.Application/CorporateActions/Queries/GetCorporateActions/GetCorporateActionsQuery.cs`
- Create: `src/InvestmentApp.Application/CorporateActions/Commands/DeleteCorporateAction/DeleteCorporateActionCommand.cs`
- Test: `tests/InvestmentApp.Application.Tests/CorporateActions/GetAndDeleteCorporateActionTests.cs`

**Interfaces:**
- Consumes: `ICorporateActionRepository`, `IPortfolioRepository`
- Produces:
  - `CorporateActionDto(string Id, string Symbol, CorporateActionType Type, DateTime ExDate, DateTime? SettlementDate, DateTime? SettledAt, decimal? AmountPerShare, decimal Multiplier, string DeclaredText, string? Note)`
  - `GetCorporateActionsQuery(string UserId, string PortfolioId, string? Symbol) : IRequest<List<CorporateActionDto>>`
  - `DeleteCorporateActionCommand(string UserId, string Id) : IRequest<Unit>`

- [ ] **Step 1: Viết test đỏ**

```csharp
using FluentAssertions;
using InvestmentApp.Application.CorporateActions.Commands.DeleteCorporateAction;
using InvestmentApp.Application.CorporateActions.Queries.GetCorporateActions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.CorporateActions;

public class GetAndDeleteCorporateActionTests
{
    private readonly Mock<ICorporateActionRepository> _actions = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();

    private static CorporateAction Hpg() =>
        CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130,
            new DateTime(2026, 6, 10), new DateTime(2026, 7, 20));

    [Fact]
    public async Task Query_TraVeDanhSachCuaDanhMuc()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u1", "Danh mục", 0));
        _actions.Setup(r => r.GetByPortfolioIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Hpg() });

        var handler = new GetCorporateActionsQueryHandler(_actions.Object, _portfolios.Object);
        var result = await handler.Handle(new GetCorporateActionsQuery("u1", "p1", null), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("HPG");
        result[0].Multiplier.Should().Be(1.3m);
        result[0].DeclaredText.Should().Be("100:130");
    }

    [Fact]
    public async Task Query_DanhMucCuaNguoiKhac_ThiNemUnauthorized()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u2", "Danh mục", 0));

        var handler = new GetCorporateActionsQueryHandler(_actions.Object, _portfolios.Object);
        var act = () => handler.Handle(new GetCorporateActionsQuery("u1", "p1", null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Delete_SuKienCuaNguoiKhac_ThiNemUnauthorized_VaKhongXoa()
    {
        var action = CorporateAction.StockDividend("p1", "u2", "HPG", 100, 130,
            new DateTime(2026, 6, 10), null);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var handler = new DeleteCorporateActionCommandHandler(_actions.Object);
        var act = () => handler.Handle(new DeleteCorporateActionCommand("u1", "a1"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _actions.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SuKienCuaMinh_ThiXoa()
    {
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(Hpg());

        var handler = new DeleteCorporateActionCommandHandler(_actions.Object);
        await handler.Handle(new DeleteCorporateActionCommand("u1", "a1"), CancellationToken.None);

        _actions.Verify(r => r.DeleteAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~GetAndDeleteCorporateActionTests`
Expected: FAIL — CS0246.

- [ ] **Step 3: Implement query**

```csharp
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Queries.GetCorporateActions;

public record CorporateActionDto(
    string Id,
    string Symbol,
    CorporateActionType Type,
    DateTime ExDate,
    DateTime? SettlementDate,
    DateTime? SettledAt,
    decimal? AmountPerShare,
    decimal Multiplier,
    string DeclaredText,
    string? Note);

public record GetCorporateActionsQuery(string UserId, string PortfolioId, string? Symbol)
    : IRequest<List<CorporateActionDto>>;

public class GetCorporateActionsQueryHandler
    : IRequestHandler<GetCorporateActionsQuery, List<CorporateActionDto>>
{
    private readonly ICorporateActionRepository _actions;
    private readonly IPortfolioRepository _portfolios;

    public GetCorporateActionsQueryHandler(
        ICorporateActionRepository actions, IPortfolioRepository portfolios)
    {
        _actions = actions;
        _portfolios = portfolios;
    }

    public async Task<List<CorporateActionDto>> Handle(
        GetCorporateActionsQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolios.GetByIdAsync(request.PortfolioId, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy danh mục", nameof(request.PortfolioId));
        if (portfolio.UserId != request.UserId)
            throw new UnauthorizedAccessException("Danh mục không thuộc về người dùng này");

        var items = string.IsNullOrWhiteSpace(request.Symbol)
            ? await _actions.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken)
            : await _actions.GetByPortfolioIdAndSymbolAsync(request.PortfolioId, request.Symbol, cancellationToken);

        return items.Select(a => new CorporateActionDto(
            a.Id, a.Symbol, a.Type, a.ExDate, a.SettlementDate, a.SettledAt,
            a.AmountPerShare, a.Multiplier, a.DeclaredText, a.Note)).ToList();
    }
}
```

- [ ] **Step 4: Implement command xoá**

```csharp
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Commands.DeleteCorporateAction;

public record DeleteCorporateActionCommand(string UserId, string Id) : IRequest<Unit>;

public class DeleteCorporateActionCommandHandler
    : IRequestHandler<DeleteCorporateActionCommand, Unit>
{
    private readonly ICorporateActionRepository _actions;

    public DeleteCorporateActionCommandHandler(ICorporateActionRepository actions)
        => _actions = actions;

    public async Task<Unit> Handle(DeleteCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var action = await _actions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy sự kiện quyền", nameof(request.Id));
        if (action.UserId != request.UserId)
            throw new UnauthorizedAccessException("Sự kiện quyền không thuộc về người dùng này");

        await _actions.DeleteAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}
```

- [ ] **Step 5: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~GetAndDeleteCorporateActionTests`
Expected: PASS — 4 test.

- [ ] **Step 6: Commit**

```bash
git add src/InvestmentApp.Application/CorporateActions tests/InvestmentApp.Application.Tests/CorporateActions
git commit -m "feat(corporate-action): query danh sách và command xoá sự kiện quyền"
```

---

### Task 7: Xác nhận đã về + sinh dòng tiền cổ tức

**Files:**
- Modify: `src/InvestmentApp.Domain/Entities/CapitalFlow.cs` (thêm `Symbol`, `CorporateActionId`)
- Create: `src/InvestmentApp.Application/CorporateActions/Commands/SettleCorporateAction/SettleCorporateActionCommand.cs`
- Test: `tests/InvestmentApp.Application.Tests/CorporateActions/SettleCorporateActionCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ICorporateActionRepository`, `ICapitalFlowRepository`, `ITradeRepository`, `PositionBuilder` (Task 2)
- Produces: `SettleCorporateActionCommand(string UserId, string Id, DateTime SettledAt, string? LinkExistingCapitalFlowId) : IRequest<Unit>`

- [ ] **Step 1: Viết test đỏ**

```csharp
using FluentAssertions;
using InvestmentApp.Application.CorporateActions.Commands.SettleCorporateAction;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.CorporateActions;

public class SettleCorporateActionCommandHandlerTests
{
    private readonly Mock<ICorporateActionRepository> _actions = new();
    private readonly Mock<ICapitalFlowRepository> _flows = new();
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly SettleCorporateActionCommandHandler _handler;

    private static readonly DateTime Ex = new(2026, 6, 10);
    private static readonly DateTime SettledAt = new(2026, 7, 20);

    public SettleCorporateActionCommandHandlerTests()
        => _handler = new SettleCorporateActionCommandHandler(_actions.Object, _flows.Object, _trades.Object);

    [Fact]
    public async Task CoTucCoPhieu_ChiDanhDauDaVe_KhongSinhDongTien()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, SettledAt);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        await _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        action.SettledAt.Should().Be(SettledAt);
        _flows.Verify(r => r.AddAsync(It.IsAny<CapitalFlow>(), It.IsAny<CancellationToken>()), Times.Never);
        _actions.Verify(r => r.UpdateAsync(action, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CoTucTienMat_SinhDongTienSauThue_TrenSoLuongTaiNgayGDKHQ()
    {
        var action = CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, SettledAt, 5m);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);
        _actions.Setup(r => r.GetByPortfolioIdAndSymbolAsync("p1", "SAB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { action });
        _trades.Setup(r => r.GetByPortfolioIdAndSymbolAsync("p1", "SAB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Trade("p1", "SAB", TradeType.BUY, 1000, 55_000, tradeDate: new DateTime(2026, 1, 5)) });

        CapitalFlow? created = null;
        _flows.Setup(r => r.AddAsync(It.IsAny<CapitalFlow>(), It.IsAny<CancellationToken>()))
            .Callback<CapitalFlow, CancellationToken>((f, _) => created = f)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        created.Should().NotBeNull();
        created!.Amount.Should().Be(475_000m);
        created.Type.Should().Be(CapitalFlowType.Dividend);
        created.Symbol.Should().Be("SAB");
        action.CapitalFlowId.Should().Be(created.Id);
    }

    [Fact]
    public async Task LienKetDongTienCu_ThiKhongTaoMoi()
    {
        var action = CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, SettledAt, 5m);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);
        _flows.Setup(r => r.GetByIdAsync("f1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CapitalFlow("p1", "u1", CapitalFlowType.Dividend, 475_000m));

        await _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, "f1"), CancellationToken.None);

        _flows.Verify(r => r.AddAsync(It.IsAny<CapitalFlow>(), It.IsAny<CancellationToken>()), Times.Never);
        action.CapitalFlowId.Should().Be("f1");
    }

    [Fact]
    public async Task SuKienCuaNguoiKhac_ThiNemUnauthorized()
    {
        var action = CorporateAction.StockDividend("p1", "u2", "HPG", 100, 130, Ex, null);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var act = () => _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DaXacNhanRoi_ThiNemInvalidOperation()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null);
        action.MarkSettled(SettledAt);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var act = () => _handler.Handle(new SettleCorporateActionCommand("u1", "a1", SettledAt, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~SettleCorporateActionCommandHandlerTests`
Expected: FAIL — CS0246 và `CapitalFlow.Symbol` chưa tồn tại.

- [ ] **Step 3: Thêm hai trường vào `CapitalFlow`**

Trong `src/InvestmentApp.Domain/Entities/CapitalFlow.cs`, thêm ngay sau `IsSeedDeposit` (dòng 19):

```csharp
    /// <summary>Mã chứng khoán sinh ra dòng tiền này (cổ tức). Null với nạp/rút thường.</summary>
    public string? Symbol { get; private set; }

    /// <summary>Sự kiện quyền đã sinh ra dòng tiền này. Null nếu nhập tay.</summary>
    public string? CorporateActionId { get; private set; }

    public void LinkCorporateAction(string corporateActionId, string symbol)
    {
        CorporateActionId = corporateActionId;
        Symbol = symbol?.ToUpper().Trim();
    }
```

- [ ] **Step 4: Implement command**

```csharp
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Commands.SettleCorporateAction;

public record SettleCorporateActionCommand(
    string UserId, string Id, DateTime SettledAt, string? LinkExistingCapitalFlowId) : IRequest<Unit>;

public class SettleCorporateActionCommandHandler
    : IRequestHandler<SettleCorporateActionCommand, Unit>
{
    private readonly ICorporateActionRepository _actions;
    private readonly ICapitalFlowRepository _flows;
    private readonly ITradeRepository _trades;

    public SettleCorporateActionCommandHandler(
        ICorporateActionRepository actions, ICapitalFlowRepository flows, ITradeRepository trades)
    {
        _actions = actions;
        _flows = flows;
        _trades = trades;
    }

    public async Task<Unit> Handle(SettleCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var action = await _actions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy sự kiện quyền", nameof(request.Id));
        if (action.UserId != request.UserId)
            throw new UnauthorizedAccessException("Sự kiện quyền không thuộc về người dùng này");
        if (action.SettledAt.HasValue)
            throw new InvalidOperationException("Sự kiện quyền này đã được xác nhận trước đó");

        action.MarkSettled(request.SettledAt);

        if (action.Type == CorporateActionType.CashDividend)
        {
            if (!string.IsNullOrWhiteSpace(request.LinkExistingCapitalFlowId))
            {
                var existing = await _flows.GetByIdAsync(request.LinkExistingCapitalFlowId, cancellationToken)
                    ?? throw new ArgumentException("Không tìm thấy dòng tiền để liên kết", nameof(request.LinkExistingCapitalFlowId));
                existing.LinkCorporateAction(action.Id, action.Symbol);
                await _flows.UpdateAsync(existing, cancellationToken);
                action.LinkCapitalFlow(existing.Id);
            }
            else
            {
                var amount = await ComputeNetDividendAsync(action, cancellationToken);
                if (amount > 0)
                {
                    var flow = new CapitalFlow(action.PortfolioId, action.UserId, CapitalFlowType.Dividend,
                        amount, "VND", $"Cổ tức tiền mặt {action.Symbol} ({action.DeclaredText})", request.SettledAt);
                    flow.LinkCorporateAction(action.Id, action.Symbol);
                    await _flows.AddAsync(flow, cancellationToken);
                    action.LinkCapitalFlow(flow.Id);
                }
            }
        }

        await _actions.UpdateAsync(action, cancellationToken);
        return Unit.Value;
    }

    /// <summary>Tiền cổ tức sau thuế, tính trên số lượng nắm giữ tại ngày GDKHQ.</summary>
    private async Task<decimal> ComputeNetDividendAsync(CorporateAction action, CancellationToken cancellationToken)
    {
        var trades = await _trades.GetByPortfolioIdAndSymbolAsync(action.PortfolioId, action.Symbol, cancellationToken);
        var priorActions = (await _actions.GetByPortfolioIdAndSymbolAsync(action.PortfolioId, action.Symbol, cancellationToken))
            .Where(a => a.Id != action.Id && a.ExDate < action.ExDate);

        var position = PositionBuilder
            .Build(trades, priorActions, asOf: action.ExDate.AddDays(-1))
            .FirstOrDefault(p => string.Equals(p.Symbol, action.Symbol, StringComparison.OrdinalIgnoreCase));

        return (position?.TotalQuantity ?? 0m) * action.NetPerShare;
    }
}
```

- [ ] **Step 5: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~SettleCorporateActionCommandHandlerTests`
Expected: PASS — 5 test.

- [ ] **Step 6: Chạy toàn bộ test backend kiểm tra không regression**

Run: `dotnet test`
Expected: tất cả PASS.

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Domain/Entities/CapitalFlow.cs src/InvestmentApp.Application/CorporateActions tests/InvestmentApp.Application.Tests/CorporateActions
git commit -m "feat(corporate-action): xác nhận đã về và sinh dòng tiền cổ tức gắn mã"
```

---

### Task 8: Controller REST

**Files:**
- Create: `src/InvestmentApp.Api/Controllers/CorporateActionsController.cs`

**Interfaces:**
- Consumes: `CreateCorporateActionCommand` (Task 5), `GetCorporateActionsQuery` + `DeleteCorporateActionCommand` (Task 6), `SettleCorporateActionCommand` (Task 7)
- Produces: 4 endpoint dưới `api/v1/corporate-actions`

Không có logic nghiệp vụ (chỉ dispatch MediatR) nên không viết unit test riêng — đúng theo `PnLController` hiện có. Kiểm chứng bằng curl ở Task 13.

- [ ] **Step 1: Implement controller**

```csharp
using InvestmentApp.Application.CorporateActions.Commands.CreateCorporateAction;
using InvestmentApp.Application.CorporateActions.Commands.DeleteCorporateAction;
using InvestmentApp.Application.CorporateActions.Commands.SettleCorporateAction;
using InvestmentApp.Application.CorporateActions.Queries.GetCorporateActions;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/corporate-actions")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CorporateActionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CorporateActionsController(IMediator mediator) => _mediator = mediator;

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    public record CreateRequest(
        string PortfolioId, string Symbol, CorporateActionType Type,
        DateTime ExDate, DateTime? SettlementDate,
        decimal? PercentOfPar, decimal? TaxRatePercent,
        decimal? RatioOld, decimal? RatioNew, string? Note);

    public record SettleRequest(DateTime SettledAt, string? LinkExistingCapitalFlowId);

    [HttpGet("portfolio/{portfolioId}")]
    [ProducesResponseType(typeof(List<CorporateActionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPortfolio(string portfolioId, [FromQuery] string? symbol)
    {
        var result = await _mediator.Send(new GetCorporateActionsQuery(GetUserId(), portfolioId, symbol));
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateRequest request)
    {
        var id = await _mediator.Send(new CreateCorporateActionCommand(
            GetUserId(), request.PortfolioId, request.Symbol, request.Type,
            request.ExDate, request.SettlementDate, request.PercentOfPar,
            request.TaxRatePercent, request.RatioOld, request.RatioNew, request.Note));

        return Created($"/api/v1/corporate-actions/{id}", new { Id = id });
    }

    [HttpPost("{id}/settle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Settle(string id, [FromBody] SettleRequest request)
    {
        await _mediator.Send(new SettleCorporateActionCommand(
            GetUserId(), id, request.SettledAt, request.LinkExistingCapitalFlowId));
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id)
    {
        await _mediator.Send(new DeleteCorporateActionCommand(GetUserId(), id));
        return NoContent();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: Build succeeded, 0 error.

- [ ] **Step 3: Commit**

```bash
git add src/InvestmentApp.Api/Controllers/CorporateActionsController.cs
git commit -m "feat(api): endpoint quản lý sự kiện quyền"
```

---

### Task 9: Đấu nối `PnLService` vào `PositionBuilder`

`PnLService` hiện tính giá vốn bằng cách cộng toàn bộ BUY rồi trừ SELL ([PnLService.cs:97-125](../../../src/InvestmentApp.Infrastructure/Services/PnLService.cs#L97-L125)) và hard-code `"USD"`. Task này thay bằng `PositionBuilder`, đồng thời sửa luôn tiền tệ về VND.

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/PnLService.cs`
- Modify: `src/InvestmentApp.Application/Portfolios/Queries/PnLModels.cs` (thêm 4 trường vào `PositionPnL`)
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/PnLServiceCorporateActionTests.cs`

**Interfaces:**
- Consumes: `PositionBuilder` (Task 2), `ICorporateActionRepository` (Task 4)
- Produces: `PositionPnL` có thêm `PendingQuantity`, `SettledQuantity`, `DividendNet`, `PendingDividend`

- [ ] **Step 1: Viết test đỏ**

```csharp
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using InvestmentApp.Infrastructure.Services;
using Moq;
using Xunit;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class PnLServiceCorporateActionTests
{
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<IStockPriceService> _prices = new();
    private readonly Mock<ICorporateActionRepository> _actions = new();

    [Fact]
    public async Task CoTucCoPhieuChuaVe_ThiKhongCoLoGia()
    {
        var portfolio = new Portfolio("u1", "Danh mục", 0);
        _portfolios.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        var trades = new[] { new Trade(portfolio.Id, "HPG", TradeType.BUY, 1000, 25_000,
            tradeDate: new DateTime(2026, 1, 5)) };
        _trades.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);

        _actions.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CorporateAction.StockDividend(portfolio.Id, "u1", "HPG",
                100, 130, new DateTime(2026, 6, 10), new DateTime(2026, 7, 20)) });

        // Giá sau điều chỉnh: 30.000 / 1,3
        _prices.Setup(s => s.GetCurrentPriceAsync(It.IsAny<StockSymbol>()))
            .ReturnsAsync(new Money(23_076.92m, "VND"));

        var service = new PnLService(_trades.Object, _portfolios.Object, _prices.Object, _actions.Object);
        var summary = await service.CalculatePortfolioPnLAsync(portfolio.Id);

        var hpg = summary.Positions.Single();
        hpg.Quantity.Should().Be(1300);
        hpg.PendingQuantity.Should().Be(300);
        hpg.AverageCost.Should().BeApproximately(19_230.77m, 0.01m);
        // 1300 × 23.076,92 ≈ 30tr, vốn 25tr → vẫn lãi, không phải lỗ
        summary.TotalUnrealizedPnL.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CoTucTienMatChuaVe_ThiVaoCotChoVe_KhongDoiGiaVon()
    {
        var portfolio = new Portfolio("u1", "Danh mục", 0);
        _portfolios.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
        _trades.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Trade(portfolio.Id, "SAB", TradeType.BUY, 1000, 55_000,
                tradeDate: new DateTime(2026, 1, 5)) });
        _actions.Setup(r => r.GetByPortfolioIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CorporateAction.CashDividend(portfolio.Id, "u1", "SAB",
                5m, new DateTime(2026, 6, 10), new DateTime(2026, 7, 10), 5m) });
        _prices.Setup(s => s.GetCurrentPriceAsync(It.IsAny<StockSymbol>()))
            .ReturnsAsync(new Money(54_500m, "VND"));

        var service = new PnLService(_trades.Object, _portfolios.Object, _prices.Object, _actions.Object);
        var summary = await service.CalculatePortfolioPnLAsync(portfolio.Id);

        var sab = summary.Positions.Single();
        sab.AverageCost.Should().Be(55_000);
        sab.PendingDividend.Should().Be(475_000);
    }
}
```

- [ ] **Step 2: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter FullyQualifiedName~PnLServiceCorporateActionTests`
Expected: FAIL — constructor `PnLService` chưa nhận `ICorporateActionRepository`.

- [ ] **Step 3: Thêm 4 trường vào `PositionPnL`**

Trong `src/InvestmentApp.Application/Portfolios/Queries/PnLModels.cs`, thêm vào class `PositionPnL`:

```csharp
    public decimal SettledQuantity { get; set; }
    public decimal PendingQuantity { get; set; }
    public decimal DividendNet { get; set; }
    public decimal PendingDividend { get; set; }
```

- [ ] **Step 4: Viết lại `PnLService`**

Thay toàn bộ nội dung `src/InvestmentApp.Infrastructure/Services/PnLService.cs`:

```csharp
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Infrastructure.Services;

public class PnLService : IPnLService
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IStockPriceService _stockPriceService;
    private readonly ICorporateActionRepository _corporateActionRepository;

    public PnLService(
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        IStockPriceService stockPriceService,
        ICorporateActionRepository corporateActionRepository)
    {
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
        _stockPriceService = stockPriceService;
        _corporateActionRepository = corporateActionRepository;
    }

    public async Task<PortfolioPnLSummary> CalculatePortfolioPnLAsync(string portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId)
            ?? throw new ArgumentException("Portfolio not found", nameof(portfolioId));

        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var actions = await _corporateActionRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var positions = PositionBuilder.Build(trades, actions, DateTime.UtcNow);

        var results = new List<PositionPnL>();
        decimal totalMarketValue = 0, totalCost = 0, totalUnrealized = 0;

        foreach (var p in positions.Where(p => p.TotalQuantity > 0))
        {
            decimal currentPrice;
            try
            {
                currentPrice = (await _stockPriceService.GetCurrentPriceAsync(new StockSymbol(p.Symbol))).Amount;
            }
            catch
            {
                continue; // bỏ qua mã không lấy được giá, tránh hỏng cả danh mục
            }

            var marketValue = p.TotalQuantity * currentPrice;
            var unrealized = marketValue - p.TotalCost;

            totalMarketValue += marketValue;
            totalCost += p.TotalCost;
            totalUnrealized += unrealized;

            results.Add(new PositionPnL
            {
                Symbol = p.Symbol,
                Quantity = p.TotalQuantity,
                SettledQuantity = p.SettledQuantity,
                PendingQuantity = p.PendingQuantity,
                AverageCost = p.AverageCost,
                CurrentPrice = currentPrice,
                RealizedPnL = p.RealizedPnL,
                DividendNet = p.DividendNet,
                PendingDividend = p.PendingDividend
            });
        }

        return new PortfolioPnLSummary
        {
            TotalRealizedPnL = positions.Sum(p => p.RealizedPnL),
            TotalUnrealizedPnL = totalUnrealized,
            TotalPortfolioValue = totalMarketValue,
            TotalInvested = totalCost,
            Positions = results
        };
    }

    public async Task<PositionPnL> CalculatePositionPnLAsync(string portfolioId, StockSymbol symbol, CancellationToken cancellationToken = default)
    {
        var summary = await CalculatePortfolioPnLAsync(portfolioId, cancellationToken);
        return summary.Positions.FirstOrDefault(p => string.Equals(p.Symbol, symbol.Value, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"No trades found for symbol {symbol.Value} in portfolio {portfolioId}");
    }

    public Task UpdatePortfolioPositionsAsync(string portfolioId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

- [ ] **Step 5: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter FullyQualifiedName~PnLServiceCorporateActionTests`
Expected: PASS — 2 test.

- [ ] **Step 6: Chạy toàn bộ test backend**

Run: `dotnet test`
Expected: tất cả PASS. Nếu có test cũ của `PnLService` fail vì trước đây kỳ vọng `"USD"`, sửa test cũ sang `"VND"` — đó là bug đang được sửa, không phải regression.

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/PnLService.cs src/InvestmentApp.Application/Portfolios/Queries/PnLModels.cs tests/InvestmentApp.Infrastructure.Tests/Services/PnLServiceCorporateActionTests.cs
git commit -m "refactor(pnl): tính lãi lỗ qua PositionBuilder thay vì gộp trade thô"
```

---

### Task 10: Đấu nối màn hình vị thế (`GetActivePositionsQuery`)

**Files:**
- Modify: `src/InvestmentApp.Application/TradePlans/Queries/GetActivePositions/GetActivePositionsQuery.cs`
- Test: `tests/InvestmentApp.Application.Tests/TradePlans/GetActivePositionsCorporateActionTests.cs`

**Interfaces:**
- Consumes: `PositionBuilder` (Task 2), `ICorporateActionRepository` (Task 4)
- Produces: `ActivePositionDto` có thêm `SettledQuantity`, `PendingQuantity`, `DividendNet`, `PendingDividend`, `TotalPnLWithDividend`

- [ ] **Step 1: Đọc file hiện tại để nắm cấu trúc**

Run: `cat src/InvestmentApp.Application/TradePlans/Queries/GetActivePositions/GetActivePositionsQuery.cs`
Ghi lại: tên DTO, tên handler, các dependency đang inject, đoạn code đang gộp `Trade` theo `Symbol`.

- [ ] **Step 2: Viết test đỏ**

```csharp
// tests/InvestmentApp.Application.Tests/TradePlans/GetActivePositionsCorporateActionTests.cs
// Dựng handler với Moq cho các repository nó đang dùng (lấy đúng danh sách từ Step 1),
// thêm Mock<ICorporateActionRepository>.
//
// Test 1 — CoTucCoPhieuChuaVe_TraVeSoLuongTongVaSoChoVe:
//   1 danh mục "u1", 1 trade BUY HPG 1000 @ 25.000 ngày 2026-01-05,
//   1 CorporateAction.StockDividend(100 -> 130) ExDate 2026-06-10, chưa MarkSettled.
//   Kỳ vọng: dto.Quantity == 1300, dto.PendingQuantity == 300,
//            dto.SettledQuantity == 1000, dto.AverageCost ≈ 19.230,77.
//
// Test 2 — CoTucTienMatDaVe_CongVaoTongLaiLo:
//   1 trade BUY SAB 1000 @ 55.000, 1 CorporateAction.CashDividend(5%) đã MarkSettled.
//   Kỳ vọng: dto.DividendNet == 475.000,
//            dto.TotalPnLWithDividend == dto.UnrealizedPnL + dto.RealizedPnL + 475.000.
```

Viết đầy đủ hai test theo mô tả trên, dùng cùng phong cách Moq như `CreateCorporateActionCommandHandlerTests` ở Task 5.

- [ ] **Step 3: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~GetActivePositionsCorporateActionTests`
Expected: FAIL.

- [ ] **Step 4: Sửa handler**

Thay đoạn tự gộp `Trade` bằng:

```csharp
var actions = await _corporateActionRepository.GetByPortfolioIdsAsync(portfolioIds, cancellationToken);
var actionsByPortfolio = actions.ToLookup(a => a.PortfolioId);

// trong vòng lặp mỗi danh mục:
var positions = PositionBuilder.Build(
    tradesOfPortfolio,
    actionsByPortfolio[portfolio.Id],
    DateTime.UtcNow);
```

Thêm 5 trường vào DTO:

```csharp
    public decimal SettledQuantity { get; set; }
    public decimal PendingQuantity { get; set; }
    public decimal DividendNet { get; set; }
    public decimal PendingDividend { get; set; }
    public decimal TotalPnLWithDividend { get; set; }
```

Gán `TotalPnLWithDividend = UnrealizedPnL + RealizedPnL + DividendNet + PendingDividend;`

- [ ] **Step 5: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~GetActivePositionsCorporateActionTests`
Expected: PASS — 2 test.

- [ ] **Step 6: Commit**

```bash
git add src/InvestmentApp.Application/TradePlans/Queries/GetActivePositions tests/InvestmentApp.Application.Tests/TradePlans
git commit -m "feat(positions): hiển thị cổ phiếu chờ về và tổng lãi lỗ gồm cổ tức"
```

---

### Task 11: Điều chỉnh ngưỡng cắt lỗ theo sự kiện quyền

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs` (nơi so sánh giá hiện tại với `StopLossTarget`)
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/StopLossCorporateActionTests.cs`

**Interfaces:**
- Consumes: `CorporateActionAdjuster` (Task 3), `ICorporateActionRepository` (Task 4)
- Produces: không có kiểu mới — chỉ đổi hành vi so sánh ngưỡng.

- [ ] **Step 1: Tìm chính xác nơi so sánh ngưỡng**

Run: `grep -rn "StopLossPrice\|TargetPrice\|TrailingStopPrice" src/InvestmentApp.Infrastructure src/InvestmentApp.Application --include=*.cs`
Ghi lại mọi vị trí so sánh giá hiện tại với ba trường này — mỗi vị trí đều phải bọc qua `CorporateActionAdjuster`.

- [ ] **Step 2: Viết test đỏ**

```csharp
// tests/InvestmentApp.Infrastructure.Tests/Services/StopLossCorporateActionTests.cs
//
// Test — SauCoTucCoPhieu_KhongKichHoatCatLoNham:
//   StopLossTarget: EntryPrice 25.000, StopLossPrice 22.000, tạo ngày 2026-01-05.
//   CorporateAction.StockDividend(100 -> 130) ExDate 2026-06-10.
//   Giá thị trường hiện tại 23.100 (đã điều chỉnh, tương đương 30.030 trước điều chỉnh).
//   Kỳ vọng: KHÔNG kích hoạt cắt lỗ — vì ngưỡng điều chỉnh là 22.000 / 1,3 ≈ 16.923.
//
// Test — GiaXuyenThungNguongDaDieuChinh_ThiVanKichHoat:
//   cùng dữ liệu, giá thị trường 16.000 → kỳ vọng CÓ kích hoạt.
```

Viết đầy đủ hai test theo mô tả, dùng Moq cho các repository mà service đang inject.

- [ ] **Step 3: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter FullyQualifiedName~StopLossCorporateActionTests`
Expected: FAIL — test đầu tiên kích hoạt cắt lỗ nhầm.

- [ ] **Step 4: Bọc mọi so sánh ngưỡng qua adjuster**

Tại mỗi vị trí tìm được ở Step 1:

```csharp
var actionsForSymbol = allActions
    .Where(a => string.Equals(a.Symbol, target.Symbol, StringComparison.OrdinalIgnoreCase));

var adjustedStop = CorporateActionAdjuster.AdjustPrice(target.StopLossPrice, target.CreatedAt, actionsForSymbol);
var adjustedTarget = CorporateActionAdjuster.AdjustPrice(target.TargetPrice, target.CreatedAt, actionsForSymbol);
var adjustedEntry = CorporateActionAdjuster.AdjustPrice(target.EntryPrice, target.CreatedAt, actionsForSymbol);
```

Dùng `adjustedStop` / `adjustedTarget` / `adjustedEntry` thay cho giá gốc trong mọi phép so sánh và tính tỷ lệ rủi ro/lợi nhuận.

- [ ] **Step 5: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter FullyQualifiedName~StopLossCorporateActionTests`
Expected: PASS — 2 test.

- [ ] **Step 6: Chạy toàn bộ test backend**

Run: `dotnet test`
Expected: tất cả PASS.

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs tests/InvestmentApp.Infrastructure.Tests/Services/StopLossCorporateActionTests.cs
git commit -m "fix(risk): không kích hoạt cắt lỗ nhầm sau ngày giao dịch không hưởng quyền"
```

---

### Task 12: Đấu nối `SnapshotService`

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/SnapshotService.cs`
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/SnapshotCorporateActionTests.cs`

**Interfaces:**
- Consumes: `PositionBuilder` (Task 2), `ICorporateActionRepository` (Task 4)
- Produces: không có kiểu mới — snapshot dùng `TotalQuantity` và `AverageCost` đã điều chỉnh.

- [ ] **Step 1: Viết test đỏ**

```csharp
// tests/InvestmentApp.Infrastructure.Tests/Services/SnapshotCorporateActionTests.cs
//
// Test — SnapshotTaiNgaySauGDKHQ_DungSoLuongDaDieuChinh:
//   1 trade BUY HPG 1000 @ 25.000 ngày 2026-01-05.
//   CorporateAction.StockDividend(100 -> 130) ExDate 2026-06-10, chưa xác nhận về.
//   Giá 23.077 tại ngày 2026-06-15.
//   Kỳ vọng: snapshot.TotalValue ≈ 30.000.000 (1300 × 23.077), KHÔNG phải 23.077.000.
//
// Test — SnapshotTaiNgayTruocGDKHQ_KhongApDungSuKien:
//   cùng dữ liệu, asOf 2026-06-01, giá 30.000
//   Kỳ vọng: snapshot.TotalValue == 30.000.000 (1000 × 30.000).
```

Viết đầy đủ hai test theo mô tả, dùng Moq cho các repository mà `SnapshotService` đang inject.

- [ ] **Step 2: Chạy test cho fail**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter FullyQualifiedName~SnapshotCorporateActionTests`
Expected: FAIL.

- [ ] **Step 3: Sửa `SnapshotService`**

Inject `ICorporateActionRepository`, thay đoạn tự gộp `Trade` bằng:

```csharp
var actions = await _corporateActionRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
var positions = PositionBuilder.Build(trades, actions, asOf: snapshotDate);
```

Truyền `snapshotDate` — **không** truyền `DateTime.UtcNow` — để snapshot quá khứ không bị áp sự kiện tương lai.

- [ ] **Step 4: Chạy test cho pass**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter FullyQualifiedName~SnapshotCorporateActionTests`
Expected: PASS — 2 test.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/SnapshotService.cs tests/InvestmentApp.Infrastructure.Tests/Services/SnapshotCorporateActionTests.cs
git commit -m "fix(snapshot): dùng số lượng và giá vốn đã điều chỉnh theo sự kiện quyền"
```

---

### Task 13: Đấu nối `RiskCalculationService` + `PortfolioCashCalculator`

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs` (phần tính tỷ trọng vị thế)
- Test: `tests/InvestmentApp.Application.Tests/Common/PortfolioCashCalculatorDividendTests.cs`

**Interfaces:**
- Consumes: `PositionBuilder` (Task 2), `PortfolioCashCalculator` (`src/InvestmentApp.Application/Common/PortfolioCashCalculator.cs`)
- Produces: không có kiểu mới.

`PortfolioCashCalculator` nhận `netFlowExcludingSeed` từ `ICapitalFlowRepository.GetTotalFlowByPortfolioIdAsync`, mà dòng tiền cổ tức sinh ở Task 7 là `CapitalFlowType.Dividend` với `SignedAmount` dương — nên tiền cổ tức **tự động** vào tiền mặt. Task này viết test khoá hành vi đó lại để lần refactor sau không làm hỏng.

- [ ] **Step 1: Viết test khoá hành vi tiền mặt**

```csharp
using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Application.Tests.Common;

public class PortfolioCashCalculatorDividendTests
{
    [Fact]
    public void CoTucTienMatDaVe_LamTangTienMat()
    {
        var trades = new[] { new Trade("p1", "SAB", TradeType.BUY, 1000, 55_000,
            tradeDate: new DateTime(2026, 1, 5)) };

        var withoutDividend = PortfolioCashCalculator.Compute(100_000_000m, 0m, trades);
        var withDividend = PortfolioCashCalculator.Compute(100_000_000m, 475_000m, trades);

        withoutDividend.Should().Be(45_000_000m);
        withDividend.Should().Be(45_475_000m);
    }

    [Fact]
    public void DongTienCoTuc_CoSignedAmountDuong()
    {
        var flow = new CapitalFlow("p1", "u1", CapitalFlowType.Dividend, 475_000m);
        flow.SignedAmount.Should().Be(475_000m);
    }
}
```

- [ ] **Step 2: Chạy test**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter FullyQualifiedName~PortfolioCashCalculatorDividendTests`
Expected: PASS ngay — đây là test khoá hành vi sẵn có, không cần sửa code.

- [ ] **Step 3: Sửa `RiskCalculationService` dùng `PositionBuilder`**

Tìm nơi service tự gộp `Trade` để tính tỷ trọng:

Run: `grep -n "TradeType.BUY" src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs`

Thay bằng:

```csharp
var actions = await _corporateActionRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
var positions = PositionBuilder.Build(trades, actions, DateTime.UtcNow);
```

rồi dùng `p.TotalQuantity` và `p.AverageCost` cho mọi phép tính tỷ trọng, VaR, Sharpe.

- [ ] **Step 4: Chạy toàn bộ test backend**

Run: `dotnet test`
Expected: tất cả PASS.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs tests/InvestmentApp.Application.Tests/Common/PortfolioCashCalculatorDividendTests.cs
git commit -m "refactor(risk): tính tỷ trọng theo vị thế đã điều chỉnh sự kiện quyền"
```

---

### Task 14: Frontend — service + trang sự kiện quyền

**Files:**
- Create: `frontend/src/app/core/services/corporate-action.service.ts`
- Create: `frontend/src/app/features/corporate-actions/corporate-actions.component.ts`
- Modify: `frontend/src/app/app.routes.ts` (thêm route `corporate-actions`)
- Test: `frontend/src/app/features/corporate-actions/corporate-actions.component.spec.ts`

**Interfaces:**
- Consumes: endpoint `api/v1/corporate-actions` (Task 8)
- Produces: `CorporateActionService` với `getByPortfolio(portfolioId, symbol?)`, `create(payload)`, `settle(id, settledAt, linkFlowId?)`, `delete(id)`; hàm thuần export `previewAdjustment(quantity, totalCost, type, percentOfPar, ratioOld, ratioNew)` để ô preview dùng và test được độc lập.

- [ ] **Step 1: Viết test đỏ cho hàm preview**

```typescript
import { previewAdjustment } from './corporate-actions.component';

describe('previewAdjustment', () => {
  it('cổ tức cổ phiếu 30% — tăng số lượng, giảm giá vốn, giữ tổng vốn', () => {
    const r = previewAdjustment(1000, 25_000_000, 'StockDividend', null, 100, 130);
    expect(r.quantityAfter).toBe(1300);
    expect(r.averageCostAfter).toBeCloseTo(19_230.77, 2);
    expect(r.totalCostAfter).toBe(25_000_000);
  });

  it('cổ phiếu lẻ làm tròn xuống', () => {
    const r = previewAdjustment(137, 3_425_000, 'StockDividend', null, 100, 130);
    expect(r.quantityAfter).toBe(178);
  });

  it('cổ tức tiền mặt 5% — giữ nguyên giá vốn, tính tiền theo mệnh giá', () => {
    const r = previewAdjustment(1000, 55_000_000, 'CashDividend', 5, null, null);
    expect(r.quantityAfter).toBe(1000);
    expect(r.averageCostAfter).toBe(55_000);
    expect(r.cashGross).toBe(500_000);
    expect(r.cashNet).toBe(475_000);
  });

  it('chia tách 1:2', () => {
    const r = previewAdjustment(500, 30_000_000, 'StockSplit', null, 1, 2);
    expect(r.quantityAfter).toBe(1000);
    expect(r.averageCostAfter).toBe(30_000);
  });
});
```

- [ ] **Step 2: Chạy test cho fail**

Run: `cd frontend && npx ng test --include='**/corporate-actions.component.spec.ts' --watch=false --browsers=ChromeHeadless`
Expected: FAIL — module chưa tồn tại.

- [ ] **Step 3: Implement service**

```typescript
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type CorporateActionType = 'CashDividend' | 'StockDividend' | 'StockSplit';

export interface CorporateAction {
  id: string;
  symbol: string;
  type: CorporateActionType;
  exDate: string;
  settlementDate?: string | null;
  settledAt?: string | null;
  amountPerShare?: number | null;
  multiplier: number;
  declaredText: string;
  note?: string | null;
}

export interface CreateCorporateActionPayload {
  PortfolioId: string;
  Symbol: string;
  Type: CorporateActionType;
  ExDate: string;
  SettlementDate?: string | null;
  PercentOfPar?: number | null;
  TaxRatePercent?: number | null;
  RatioOld?: number | null;
  RatioNew?: number | null;
  Note?: string | null;
}

@Injectable({ providedIn: 'root' })
export class CorporateActionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/corporate-actions`;

  getByPortfolio(portfolioId: string, symbol?: string): Observable<CorporateAction[]> {
    const query = symbol ? `?symbol=${encodeURIComponent(symbol)}` : '';
    return this.http.get<CorporateAction[]>(`${this.apiUrl}/portfolio/${portfolioId}${query}`);
  }

  create(payload: CreateCorporateActionPayload): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.apiUrl, payload);
  }

  settle(id: string, settledAt: string, linkExistingCapitalFlowId?: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/settle`, {
      SettledAt: settledAt,
      LinkExistingCapitalFlowId: linkExistingCapitalFlowId ?? null
    });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
```

Lưu ý: body gửi lên dùng **PascalCase** — API `InvestmentApp.Api` phân biệt hoa thường khi bind.

- [ ] **Step 4: Implement hàm preview + component**

Trong `corporate-actions.component.ts`, export hàm thuần trước phần `@Component`:

```typescript
export const PAR_VALUE = 10_000;

export interface AdjustmentPreview {
  quantityAfter: number;
  averageCostAfter: number;
  totalCostAfter: number;
  cashGross: number;
  cashNet: number;
}

export function previewAdjustment(
  quantity: number,
  totalCost: number,
  type: CorporateActionType,
  percentOfPar: number | null,
  ratioOld: number | null,
  ratioNew: number | null,
  taxRatePercent = 5
): AdjustmentPreview {
  if (type === 'CashDividend') {
    const perShare = ((percentOfPar ?? 0) / 100) * PAR_VALUE;
    const gross = quantity * perShare;
    return {
      quantityAfter: quantity,
      averageCostAfter: quantity > 0 ? totalCost / quantity : 0,
      totalCostAfter: totalCost,
      cashGross: gross,
      cashNet: gross * (1 - taxRatePercent / 100)
    };
  }

  const multiplier = ratioOld && ratioNew && ratioOld > 0 ? ratioNew / ratioOld : 1;
  const quantityAfter = Math.floor(quantity * multiplier);
  return {
    quantityAfter,
    averageCostAfter: quantityAfter > 0 ? totalCost / quantityAfter : 0,
    totalCostAfter: totalCost,
    cashGross: 0,
    cashNet: 0
  };
}
```

Component `CorporateActionsComponent` (standalone, inline template, Tailwind) gồm:
- Bảng danh sách sự kiện: cột **Mã**, **Loại**, **Ngày GDKHQ**, **Ngày về**, **Tỷ lệ**, **Trạng thái**, thao tác.
- Nút **"Thêm sự kiện quyền"** mở modal.
- Modal nhập: ô mã dùng `appUppercase`; select loại (`Cổ tức tiền mặt` / `Cổ tức cổ phiếu` / `Chia tách cổ phiếu`); ngày GDKHQ; ngày về dự kiến; ô tỷ lệ % hoặc ô `cũ : mới` tuỳ loại; ô ghi chú.
- Khối preview dưới form, cập nhật theo `ngModel`, hiển thị: `1.000 CP → 1.300 CP · giá vốn 25.000 → 19.231 · tổng vốn không đổi`.
- Nút trong modal theo thứ tự `[Hủy]` → `[Lưu]`, overlay `z-[60]`.
- Nút **"Xác nhận đã về"** trên hàng đang chờ; nút **"Xoá"** có xác nhận.
- Toàn bộ chữ tiếng Việt có dấu.

Route trong `app.routes.ts`:

```typescript
{
  path: 'corporate-actions',
  loadComponent: () => import('./features/corporate-actions/corporate-actions.component')
    .then(m => m.CorporateActionsComponent)
}
```

- [ ] **Step 5: Chạy test cho pass**

Run: `cd frontend && npx ng test --include='**/corporate-actions.component.spec.ts' --watch=false --browsers=ChromeHeadless`
Expected: PASS — 4 test.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/core/services/corporate-action.service.ts frontend/src/app/features/corporate-actions frontend/src/app/app.routes.ts
git commit -m "feat(ui): trang quản lý sự kiện quyền với ô xem trước điều chỉnh"
```

---

### Task 15: Frontend — badge "chờ về" và cột cổ tức trên màn hình vị thế

**Files:**
- Modify: `frontend/src/app/core/services/positions.service.ts` (thêm 5 trường vào `ActivePosition`)
- Modify: `frontend/src/app/features/positions/positions.component.ts`

**Interfaces:**
- Consumes: `ActivePositionDto` mở rộng ở Task 10
- Produces: không có kiểu mới ngoài các trường thêm vào `ActivePosition`.

- [ ] **Step 1: Mở rộng interface**

Trong `frontend/src/app/core/services/positions.service.ts`, thêm vào `ActivePosition` (sau `realizedPnL`, dòng 27):

```typescript
  settledQuantity: number;
  pendingQuantity: number;
  dividendNet: number;
  pendingDividend: number;
  totalPnLWithDividend: number;
```

- [ ] **Step 2: Thêm badge "chờ về" vào template**

Trong `positions.component.ts`, ngay sau ô hiển thị số lượng:

```html
@if (position.pendingQuantity > 0) {
  <span class="ml-1 rounded bg-amber-100 px-1.5 py-0.5 text-xs font-medium text-amber-700"
        [title]="'Cổ phiếu từ sự kiện quyền, chưa về tài khoản'">
    +{{ position.pendingQuantity | number }} chờ về
  </span>
}
```

- [ ] **Step 3: Thêm hai cột cổ tức**

Thêm vào header bảng và mỗi hàng:

```html
<th class="px-3 py-2 text-right">Cổ tức đã nhận</th>
<th class="px-3 py-2 text-right">Tổng lãi/lỗ gồm cổ tức</th>
```

```html
<td class="px-3 py-2 text-right">
  {{ position.dividendNet | vndCurrency }}
  @if (position.pendingDividend > 0) {
    <span class="block text-xs text-amber-600">
      +{{ position.pendingDividend | vndCurrency }} chờ về
    </span>
  }
</td>
<td class="px-3 py-2 text-right"
    [class.text-green-600]="position.totalPnLWithDividend > 0"
    [class.text-red-600]="position.totalPnLWithDividend < 0">
  {{ position.totalPnLWithDividend | vndCurrency }}
</td>
```

- [ ] **Step 4: Chạy test frontend**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: tất cả PASS.

- [ ] **Step 5: Kiểm chứng trên trình duyệt**

Chạy skill `/qa-verify`: mở `/positions`, xác nhận badge "chờ về" và hai cột cổ tức hiển thị đúng, tiếng Việt có dấu đầy đủ. Chụp màn hình làm bằng chứng.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/app/core/services/positions.service.ts frontend/src/app/features/positions/positions.component.ts
git commit -m "feat(ui): badge cổ phiếu chờ về và cột tổng lãi lỗ gồm cổ tức"
```

---

### Task 16: Tài liệu và ADR

**Files:**
- Create: `docs/adr/0010-corporate-actions-position-projection.md`
- Modify: `docs/business-domain.md`, `docs/architecture.md`, `docs/features.md`, `docs/project-context.md`
- Modify: `frontend/src/assets/CHANGELOG.md`
- Create: `frontend/src/assets/docs/su-kien-quyen.md` + đăng ký Help topic

- [ ] **Step 1: Viết ADR 0010**

Dùng `docs/adr/template.md`. Nội dung: bối cảnh (lỗ giả 23% + toán vị thế nhân bản ở ~15 service), quyết định (`CorporateAction` bất biến + `PositionBuilder` duy nhất, cổ tức tiền mặt là thu nhập không giảm giá vốn, trạng thái "chờ về"), các hướng bị loại (sửa thẳng trade cũ, synthetic `Trade`) kèm lý do, hệ quả (phải sửa dần call-site, `Trade` vẫn khớp sổ công ty chứng khoán).

- [ ] **Step 2: Cập nhật `docs/business-domain.md`**

Thêm `CorporateAction` vào sơ đồ entity (dưới `Portfolio`), thêm mục quy tắc nghiệp vụ: quy đổi % theo mệnh giá 10.000đ, hệ số `RatioNew/RatioOld`, thuế TNCN 5%, thứ tự áp dụng khi cùng ngày GDKHQ, quy tắc làm tròn xuống cổ phiếu lẻ.

- [ ] **Step 3: Cập nhật `docs/architecture.md`**

Thêm `PositionBuilder` và `CorporateActionAdjuster` vào mục Application/Common, `CorporateActionRepository` vào Infrastructure, `CorporateActionsController` vào bảng endpoint, trang `/corporate-actions` vào bảng route frontend. Ghi rõ: **mọi service cần giá vốn phải gọi `PositionBuilder`**, không tự gộp `Trade`.

- [ ] **Step 4: Cập nhật `docs/features.md` và `docs/project-context.md`**

`features.md`: thêm mục tính năng sự kiện quyền. `project-context.md`: thêm pitfall "cổ tức 5% là 5% mệnh giá chứ không phải 5% giá thị trường" và "giá ngưỡng cắt lỗ phải điều chỉnh theo sự kiện quyền".

- [ ] **Step 5: CHANGELOG + hướng dẫn người dùng**

Thêm mục vào `frontend/src/assets/CHANGELOG.md`. Viết `frontend/src/assets/docs/su-kien-quyen.md` giải thích bằng tiếng Việt: sự kiện quyền là gì, ngày GDKHQ vs ngày về, cách nhập, ý nghĩa badge "chờ về", vì sao cổ tức tiền mặt không làm giảm giá vốn. Đăng ký topic trong danh sách Help (tìm nơi đăng ký: `grep -rn "assets/docs" frontend/src/app`).

- [ ] **Step 6: Commit**

```bash
git add docs frontend/src/assets/CHANGELOG.md frontend/src/assets/docs/su-kien-quyen.md
git commit -m "docs: tài liệu và ADR cho tính năng sự kiện quyền"
```

---

### Task 17: Code review, kiểm chứng thủ công và mở PR

- [ ] **Step 1: Chạy toàn bộ test**

Run: `dotnet test && cd frontend && npx ng test --watch=false --browsers=ChromeHeadless`
Expected: tất cả PASS. Dán kết quả làm bằng chứng.

- [ ] **Step 2: Quét bí mật — cổng chặn cứng**

Run: `git diff origin/master...HEAD | grep -nE "(api[_-]?key|secret|password|token|mongodb\+srv://|eyJ[A-Za-z0-9_-]{10,})"`
Expected: không có kết quả. Nếu có → **dừng lại**, không commit, xử lý trước.

- [ ] **Step 3: Code review bắt buộc**

Chạy skill `/code-review`. Bắt buộc với mọi PR, kể cả PR chỉ đấu nối. Sửa hết finding P0/P1 rồi mới sang bước sau.

- [ ] **Step 4: Kiểm chứng thủ công theo kịch bản thật**

Chạy backend (`/be-watch`) và frontend (`/fe-run`), rồi làm đủ kịch bản:

1. Tạo danh mục, mua 1.000 HPG giá 25.000.
2. Nhập cổ tức cổ phiếu 30%, ngày GDKHQ hôm qua, ngày về sau 30 ngày.
3. Mở `/positions` → phải thấy `1.000 (+300 chờ về)`, giá vốn 19.231.
4. Bấm "Xác nhận đã về" → badge biến mất, số lượng thành 1.300, giá vốn giữ 19.231.
5. Nhập cổ tức tiền mặt 5% cho SAB → cột "Cổ tức đã nhận" hiện 475.000 sau khi xác nhận; kiểm tra `/capital-flows` có dòng tiền tương ứng ghi rõ mã SAB.
6. Xoá một sự kiện → mọi con số quay về đúng như trước khi nhập.

Chụp màn hình từng bước.

- [ ] **Step 5: Mở PR**

Chạy skill `/pr`. Không tự gõ `gh pr create`.

---

## Self-Review

**Spec coverage:**

| Mục spec | Task |
|---|---|
| §3.1 Cổ tức tiền mặt, quy đổi mệnh giá, thuế | 1, 2, 14 |
| §3.2 Cổ tức cổ phiếu / chia tách, làm tròn xuống | 1, 2, 14 |
| §3.3 Thứ tự khi cùng ngày GDKHQ | 2, 3 |
| §3.4 Trạng thái chờ về | 2, 7, 10, 15 |
| §4.1 Entity `CorporateAction` | 1 |
| §4.2 `PositionBuilder` | 2 |
| §4.3 `CorporateActionAdjuster` | 3, 11 |
| §4.4 `CapitalFlow` + chống đếm hai lần | 7 |
| §5 API | 4, 5, 6, 7, 8 |
| §6.1 Năm điểm đấu nối | 9 (PnL), 11 (cắt lỗ), 12 (snapshot), 13 (rủi ro + tiền mặt), 10 (vị thế) |
| §7 Giao diện | 14, 15 |
| §8 Trường hợp biên | 2 (test), 1 (validate) |
| §9 Kiểm thử | mỗi task |
| §11 ADR | 16 |

**Placeholder scan:** Task 10, 11, 12 mô tả test bằng chú thích thay vì code đầy đủ, vì nội dung phụ thuộc chữ ký hàm và danh sách dependency của service hiện có — bước đầu tiên của mỗi task đó là đọc file để lấy chữ ký thật. Đây là ràng buộc thực tế, không phải chỗ trống bỏ ngỏ: mỗi chú thích đã nêu đủ dữ liệu vào, thao tác và con số kỳ vọng.

**Type consistency:** `AdjustedPosition` (Task 2) → dùng ở Task 7, 9, 10, 12, 13. `CorporateAction.Multiplier` / `NetPerShare` / `AmountPerShare` (Task 1) → dùng ở Task 2, 3, 7. `ICorporateActionRepository.GetByPortfolioIdAsync` / `GetByPortfolioIdAndSymbolAsync` / `GetByPortfolioIdsAsync` (Task 4) → dùng ở Task 5, 6, 7, 9, 10, 12, 13. `CorporateActionType` dùng chung tên ở cả .NET và TypeScript (`'CashDividend' | 'StockDividend' | 'StockSplit'`) — hợp lệ vì `JsonStringEnumConverter` đã đăng ký toàn cục tại [`ApiJsonConfig.cs:28`](../../../src/InvestmentApp.Api/Configuration/ApiJsonConfig.cs#L28).

---

## Checkpoint — Task 1–8 (done, 2026-08-08)

- **Decisions**: giữ nguyên spec. Ba sửa đổi phát sinh từ code review, đã áp dụng:
  - `SettleCorporateActionCommand` — kiểm chủ sở hữu `CapitalFlow` khi liên kết dòng tiền cũ (IDOR: trước đó load theo id client gửi rồi ghi đè, không kiểm `UserId`/`PortfolioId`).
  - `DeleteCorporateActionCommand` — chặn xoá sự kiện đã sinh dòng tiền, tránh `CapitalFlow` mồ côi.
  - `PositionBuilder` — kẹp số lượng bán theo số đang giữ, không để số lượng và giá vốn xuống âm.
- **Files changed**:
  - Domain: `CorporateAction.cs` (mới), `CapitalFlow.cs` (+`Symbol`, +`CorporateActionId`, +`LinkCorporateAction`)
  - Application: `Common/PositionBuilder.cs`, `Common/CorporateActionAdjuster.cs`, `CorporateActions/**` (create, delete, settle, get), `RepositoryInterfaces.cs` (+`ICorporateActionRepository`)
  - Infrastructure: `Repositories/CorporateActionRepository.cs`
  - Api: `Controllers/CorporateActionsController.cs`, `Program.cs` (DI)
  - Docs: `docs/adr/0010-corporate-actions-position-projection.md` (Proposed)
- **Tests**: 34 test mới (8 Domain + 26 Application). Toàn bộ suite 1.581 pass, không regression.
- **Affected layers**: Domain / Application / Infrastructure / Api
- **Chưa làm**: Phase 4 manual verify — `appsettings.Development` trỏ vào MongoDB prod nên không curl tạo dữ liệu thật. Verify khi có DB dev, hoặc gộp vào lần verify của Task 14–15.
- **Next**: Task 9 — đấu nối `PnLService` vào `PositionBuilder`. Đọc `src/InvestmentApp.Infrastructure/Services/PnLService.cs` và `src/InvestmentApp.Application/Portfolios/Queries/PnLModels.cs`. Lưu ý `PnLService` hiện hard-code tiền tệ `"USD"` và bỏ qua phí/thuế — Task 9 viết lại hẳn, test cũ kỳ vọng `"USD"` là bug đang sửa. Sau đó Task 10–13 (vị thế, cắt lỗ, snapshot, rủi ro), chạy `dotnet test` sau **từng** task.

## Checkpoint — Task 9–17 (done, 2026-08-08)

- **Phát hiện lớn nhất:** `SnapshotService` và phần số lượng của `RiskCalculationService` **không cần sửa** — cả hai đã đi qua `IPnLService`, nên Task 9 sửa upstream là tự đúng. Plan giả định phải sửa từng chỗ; thực tế chỉ cần đo lại và viết test khoá hành vi.
- **Ngoài phạm vi plan nhưng bắt buộc:** ngưỡng cắt lỗ kích hoạt ở `PriceSnapshotJobService`, không phải `RiskCalculationService`. Phải sửa cả hai — job bắn cảnh báo và service feed decision queue.
- **Files changed thêm:** `PnLService`, `PnLModels`, `GetActivePositionsQuery`, `PriceSnapshotJobService`, `RiskCalculationService`, `positions.service.ts`, `positions.component.ts`, `corporate-action.service.ts`, `corporate-actions.component.ts`, `app.routes.ts`, `help.component.ts`, docs + CHANGELOG + `su-kien-quyen.md`.
- **Tests:** backend 1.595 pass, frontend 152 pass, build 0 lỗi.
- **Chưa làm:** Phase 4 manual verify trên trình duyệt — `appsettings.Development` trỏ MongoDB prod. Cần DB dev hoặc chạy `/qa-verify` với tài khoản test trước khi merge.

## Checkpoint — Đợt 2, đấu nối hạn mức rủi ro + kịch bản (done, 2026-08-09)

Hai đường ra quyết định tự động mà PR #145 bỏ sót, phát hiện ở review vòng 4.

- **`RiskCalculationService`** — `CheckRiskBudgetAsync` và `CalculateStressTestAsync` giờ đi qua `PositionBuilder`. Lãi/lỗ trong ngày = `RealizedPnL(asOf hôm nay) − RealizedPnL(asOf hôm qua)`, thay cho việc tự khớp lệnh mua với lệnh bán bằng trung bình không trọng số.
- **`ScenarioEvaluationService` / `ScenarioAdvisoryService`** — thêm `TradePlanPriceAdjuster` (`Application/Common`), mốc `TradePlan.PricesSetAt`.
- **Quyết định kiến trúc phát sinh:** mẫu "điều chỉnh khi đọc, không sửa dữ liệu" không áp được cho `TrailingStopConfig.HighestPrice`/`CurrentTrailingStop` vì hai giá trị này bị ghi đè trở lại. Chọn rebase lười một lần + mốc `PriceBasisAt`. Ghi vào ADR-0010 phần bổ sung 2026-08-09.
- **Ba lỗi sửa kèm** (2 do review agent, 1 tự soát): mốc giá dùng chung khiến sửa nhánh kịch bản vô hiệu hoá điều chỉnh giá nhập → tách `ScenarioPricesSetAt`; `CorporateActionAdjuster` thiếu chặn trên nên áp sự kiện công bố trước ngày GDKHQ; `PositionBuilder` xếp lệnh khớp trước sự kiện quyền cùng ngày, ngược với luật chốt quyền.
- **Tests:** 1.640 backend pass (+38). Không đụng frontend.
- **Còn lại:** nhóm thống kê thuần (`BacktestEngine`, `BehavioralAnalysisService`, `StrategyPerformanceService`, `CampaignReviewService`, `DisciplineScoreCalculator`, `GetSymbolTimelineQuery`, `GetAllPortfoliosQuery.TotalInvested`) — không ra quyết định, hoãn được.

**Plan đóng.** Phần chưa làm đã chuyển vào mục "Chưa làm — thống kê" của `docs/features.md`.
