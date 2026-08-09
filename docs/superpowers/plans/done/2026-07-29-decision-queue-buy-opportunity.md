# Decision Queue — Bắt cơ hội mua & chặn vị thế không stop-loss — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decision Queue chứa được cả phía vào lệnh (`BuyOpportunity`) lẫn cảnh báo vị thế chưa đặt stop-loss (`MissingStopLoss`), và item không gắn trade plan resolve xong thì im đến hết ngày.

**Architecture:** Ba thay đổi độc lập trên cùng một handler. (1) Thêm tập suppression thứ ba cho journal entry symbol-only — sửa bug có sẵn. (2) `MissingStopLoss` tái dùng `PortfolioRiskSummary` đã fetch, không tốn thêm I/O. (3) `BuyOpportunity` thêm hai dependency (`IWatchlistRepository`, `IStockPriceService`) và chạy song song trong `Task.WhenAll` sẵn có. Frontend chuyển `typeLabel`/`getActionRoute` từ chuỗi `if` fallthrough sang `Record<DecisionType, …>` để type mới thiếu nhãn là lỗi biên dịch.

**Tech Stack:** .NET 9, MediatR, xUnit + FluentAssertions + Moq, Angular 19 standalone + inline template, Karma/Jasmine.

**Spec:** [`docs/superpowers/specs/2026-07-29-decision-queue-buy-opportunity-design.md`](../../specs/done/2026-07-29-decision-queue-buy-opportunity-design.md)

## Global Constraints

- **TDD bắt buộc:** Red → Green → Refactor. Viết test fail trước, chạy để xác nhận fail, rồi mới implement.
- **Text tiếng Việt có dấu đầy đủ** cho mọi chuỗi hiển thị (`Headline`, nhãn FE). Không viết không dấu.
- **Commit message tiếng Việt có dấu**, giữ prefix conventional-commit tiếng Anh (`feat`/`fix`/`docs`).
- **Không thêm `Co-Authored-By`** vào commit.
- **Không đổi signature MCP tool** `get_decision_queue` — chỉ đổi `[Description]`.
- **Không tự commit/push** khi chưa được người dùng duyệt từng lần.
- Chạy `dotnet test tests/InvestmentApp.Application.Tests` sau mỗi task backend.
- Toàn bộ test suppression hiện có phải **vẫn xanh mà không sửa một dòng nào** (trừ helper `MakeDecisionJournal` được mở rộng theo kiểu cộng thêm).

---

### Task 1: Sửa bug suppression cho entry symbol-only

Bug có sẵn, độc lập với tính năng mới, nên làm trước. `ResolveDecisionCommand.HandleHoldWithJournalAsync` khi không có `TradePlanId` để `portfolioId` null, còn `LoadResolvedTodayAsync` lại lọc bỏ đúng những entry đó → `StopLossHit` resolve xong hiện lại ngay.

**Files:**
- Modify: `src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs:105-133` (`LoadResolvedTodayAsync`) và `:84-90` (mệnh đề filter)
- Test: `tests/InvestmentApp.Application.Tests/Decisions/GetDecisionQueueQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `JournalEntry.Tags` (`List<string>`, public getter — `src/InvestmentApp.Domain/Entities/JournalEntry.cs:32`), `JournalEntry.PortfolioId`, `JournalEntry.TradePlanId`, `JournalEntry.Symbol`
- Produces: `LoadResolvedTodayAsync` đổi kiểu trả về thành 3-tuple `(HashSet<string> PlanIds, HashSet<(string Symbol, string PortfolioId)> SymPort, HashSet<(string Symbol, string Type)> SymType)`. Task 2 và Task 3 không gọi trực tiếp hàm này nhưng dựa vào việc tập thứ ba đã tồn tại.

- [ ] **Step 1: Mở rộng helper test `MakeDecisionJournal` để nhận trigger tag**

Sửa helper tại `GetDecisionQueueQueryHandlerTests.cs:366-378`. Thêm tham số **có giá trị mặc định** ở cuối để mọi lời gọi hiện có không phải đổi:

```csharp
private static JournalEntry MakeDecisionJournal(
    string symbol,
    string? portfolioId = null,
    string? tradePlanId = null,
    DateTime? timestamp = null,
    string? triggerType = null)
{
    var tags = new List<string> { "decision-hold" };
    if (triggerType != null) tags.Add($"trigger:{triggerType}");

    return new JournalEntry(
        userId: UserId,
        symbol: symbol,
        entryType: JournalEntryType.Decision,
        title: $"Quyết định — {symbol}",
        content: "test resolve marker",
        portfolioId: portfolioId,
        tradePlanId: tradePlanId,
        tags: tags,
        timestamp: timestamp ?? DateTime.UtcNow);
}
```

- [ ] **Step 2: Viết hai test fail**

Thêm vào cuối class, ngay sau `Handle_ScenarioTriggerWithDecisionJournalLinkedToPlan_IsSuppressed`:

```csharp
[Fact]
public async Task Handle_StopLossHitResolvedSymbolOnly_IsSuppressed()
{
    // Đường thật của ResolveDecision cho StopLossHit: HandleHoldWithJournalAsync đi nhánh
    // symbol-only nên journal không có portfolioId lẫn tradePlanId — chỉ còn tag trigger:{Type}.
    // Trước fix, entry này rơi khỏi cả hai tập suppression nên card hiện lại sau refresh.
    var portfolio = MakePortfolio("p1", "Main");
    SetupPortfolios(portfolio);
    SetupRiskSummary(portfolio.Id, MakePosition("FPT", 89.5m, 89.4m, -0.1m));
    SetupDecisionJournal(MakeDecisionJournal(
        "FPT", triggerType: "StopLossHit", timestamp: VnTodayStartUtc().AddHours(2)));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().BeEmpty();
}

[Fact]
public async Task Handle_SymbolOnlyResolveOfDifferentType_DoesNotSuppress()
{
    // Suppression phải theo (symbol, type). Resolve một loại quyết định khác trên cùng mã
    // không được làm im cảnh báo stop-loss.
    var portfolio = MakePortfolio("p1", "Main");
    SetupPortfolios(portfolio);
    SetupRiskSummary(portfolio.Id, MakePosition("FPT", 89.5m, 89.4m, -0.1m));
    SetupDecisionJournal(MakeDecisionJournal(
        "FPT", triggerType: "BuyOpportunity", timestamp: VnTodayStartUtc().AddHours(2)));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().HaveCount(1);
    result.Items[0].Type.Should().Be(DecisionType.StopLossHit);
}
```

- [ ] **Step 3: Chạy test để xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~GetDecisionQueueQueryHandlerTests"`

Expected: `Handle_StopLossHitResolvedSymbolOnly_IsSuppressed` FAIL (`Expected collection to be empty, but found 1 item`). `Handle_SymbolOnlyResolveOfDifferentType_DoesNotSuppress` PASS (pass sẵn vì suppression chưa chạy — nó là test chống hồi quy cho Step 4, không phải test Red).

- [ ] **Step 4: Thêm hằng số và tập suppression thứ ba**

Trong `GetDecisionQueueQueryHandler`, thêm hằng bên cạnh các hằng có sẵn (sau `VnUtcOffsetHours` ở `:48`):

```csharp
/// <summary>Tag mà ResolveDecision ghi kèm mọi journal resolve: "trigger:{DecisionType}".</summary>
private const string TriggerTagPrefix = "trigger:";
```

Thay toàn bộ `LoadResolvedTodayAsync` (`:105-133`) bằng:

```csharp
/// <summary>
/// Load Decision-type journal entries created on or after start-of-VN-day. Build three
/// suppression sets:
///   - planIds: matches ScenarioTrigger / ThesisReviewDue items by TradePlanId.
///   - symPort: (Symbol, PortfolioId) pairs — matches items resolved kèm portfolio.
///   - symType: (Symbol, DecisionType) pairs từ tag "trigger:" — dành riêng cho entry
///     không có cả portfolioId lẫn tradePlanId, thứ mà hai tập trên không bắt được.
/// </summary>
private async Task<(HashSet<string> PlanIds,
                    HashSet<(string Symbol, string PortfolioId)> SymPort,
                    HashSet<(string Symbol, string Type)> SymType)>
    LoadResolvedTodayAsync(string userId, CancellationToken ct)
{
    var vnNow = DateTime.UtcNow.AddHours(VnUtcOffsetHours);
    var vnDayStartUtc = vnNow.Date.AddHours(-VnUtcOffsetHours);

    var journals = await _journalRepo.GetByUserIdAsync(userId, ct);
    var todayDecisions = journals
        .Where(j => j.EntryType == JournalEntryType.Decision && j.Timestamp >= vnDayStartUtc)
        .ToList();

    var planIds = todayDecisions
        .Where(j => !string.IsNullOrEmpty(j.TradePlanId))
        .Select(j => j.TradePlanId!)
        .ToHashSet();

    var symPort = todayDecisions
        .Where(j => !string.IsNullOrEmpty(j.PortfolioId))
        .Select(j => (j.Symbol, j.PortfolioId!))
        .ToHashSet();

    var symType = todayDecisions
        .Where(j => string.IsNullOrEmpty(j.PortfolioId) && string.IsNullOrEmpty(j.TradePlanId))
        .Select(j => (
            j.Symbol,
            Tag: j.Tags.FirstOrDefault(t => t.StartsWith(TriggerTagPrefix, StringComparison.Ordinal))))
        .Where(x => x.Tag != null)
        .Select(x => (x.Symbol, Type: x.Tag![TriggerTagPrefix.Length..]))
        .ToHashSet();

    return (planIds, symPort, symType);
}
```

- [ ] **Step 5: Thêm mệnh đề filter**

Thay `:86-90` trong `Handle`:

```csharp
var (suppressedPlanIds, suppressedSymbolPortfolio, suppressedSymbolType) = resolvedTodayTask.Result;
var unsuppressed = deduped.Where(i =>
    !(i.TradePlanId != null && suppressedPlanIds.Contains(i.TradePlanId))
    && !suppressedSymbolPortfolio.Contains((i.Symbol, i.PortfolioId))
    && !suppressedSymbolType.Contains((i.Symbol, i.Type.ToString()))
).ToList();
```

- [ ] **Step 6: Chạy toàn bộ test file**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~GetDecisionQueueQueryHandlerTests"`

Expected: TẤT CẢ PASS. Đặc biệt kiểm ba test suppression cũ vẫn xanh **mà không sửa chúng** — `Handle_StopLossHitWithRecentDecisionJournalToday_IsSuppressed`, `Handle_StopLossHitWithDecisionJournalForDifferentPortfolio_NotSuppressed`, `Handle_DecisionJournalFromYesterday_NotSuppressed`. Nếu một trong ba đỏ, tập thứ ba đang bắt quá rộng — kiểm lại điều kiện `IsNullOrEmpty` trên **cả hai** trường.

- [ ] **Step 7: Commit** *(chờ người dùng duyệt trước khi chạy)*

```bash
git add src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs tests/InvestmentApp.Application.Tests/Decisions/GetDecisionQueueQueryHandlerTests.cs
git commit -m "fix(decision): suppress được item resolve theo symbol không gắn plan"
```

---

### Task 2: Thêm `MissingStopLoss`

Không tốn thêm I/O — tái dùng `summary.Positions` mà `LoadStopLossItemsAsync` đã fetch.

**Files:**
- Modify: `src/InvestmentApp.Application/Decisions/DTOs/DecisionItemDto.cs:7-28`
- Modify: `src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs:135-184` (`LoadStopLossItemsAsync`)
- Test: `tests/InvestmentApp.Application.Tests/Decisions/GetDecisionQueueQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `PositionRiskItem.StopLossPrice` (`decimal?`), `.CurrentPrice` (`decimal`), `.Symbol` (`string`) — `src/InvestmentApp.Application/Common/Interfaces/IRiskCalculationService.cs:70-80`
- Produces: `DecisionType.MissingStopLoss`, `DecisionType.BuyOpportunity` (khai báo cả hai ở task này để Task 3 chỉ việc dùng). Id format `MissingStopLoss:{portfolioId}:{symbol}`.

- [ ] **Step 1: Viết lại test có sẵn + thêm test mới**

⚠️ `Handle_PositionWithoutSL_NotIncluded` (`:107-117`) khẳng định **đúng hành vi ta đang đổi**. Thay thế nguyên khối đó bằng:

```csharp
[Fact]
public async Task Handle_PositionWithoutSL_AddsMissingStopLossWarning()
{
    // Trước đây vị thế không SL bị `continue` bỏ qua — queue rỗng đọc như "an toàn"
    // trong khi thực tế là "rủi ro chưa đo được". Giờ nó phải hiện ra.
    var portfolio = MakePortfolio("p1", "Main");
    SetupPortfolios(portfolio);
    SetupRiskSummary(portfolio.Id, MakePosition("MWG", stopLossPrice: null, currentPrice: 50m, distanceToSlPercent: 0m));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().HaveCount(1);
    var item = result.Items[0];
    item.Type.Should().Be(DecisionType.MissingStopLoss);
    item.Severity.Should().Be(DecisionSeverity.Warning);
    item.Symbol.Should().Be("MWG");
    item.PortfolioId.Should().Be("p1");
    item.TradePlanId.Should().BeNull();
    item.Headline.Should().Contain("chưa đặt stop-loss");
}

[Fact]
public async Task Handle_PositionWithoutSLAndZeroPrice_NotIncluded()
{
    // Giá fail fetch là "chưa biết", không phải "thiếu SL". Không được báo động nhầm.
    var portfolio = MakePortfolio("p1", "Main");
    SetupPortfolios(portfolio);
    SetupRiskSummary(portfolio.Id, MakePosition("MWG", stopLossPrice: null, currentPrice: 0m, distanceToSlPercent: 0m));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().BeEmpty();
}

[Fact]
public async Task Handle_PositionWithSL_DoesNotAlsoEmitMissingStopLoss()
{
    // Hai type loại trừ nhau theo định nghĩa — không được sinh cả hai cho cùng vị thế.
    var portfolio = MakePortfolio("p1", "Main");
    SetupPortfolios(portfolio);
    SetupRiskSummary(portfolio.Id, MakePosition("FPT", stopLossPrice: 89.5m, currentPrice: 89.4m, distanceToSlPercent: -0.1m));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().HaveCount(1);
    result.Items[0].Type.Should().Be(DecisionType.StopLossHit);
}
```

- [ ] **Step 2: Chạy test để xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~GetDecisionQueueQueryHandlerTests"`

Expected: FAIL biên dịch — `DecisionType.MissingStopLoss` chưa tồn tại.

- [ ] **Step 3: Thêm hai giá trị enum + sửa comment `Info`**

Trong `DecisionItemDto.cs`, thêm vào cuối `DecisionType` (giữ nguyên thứ tự ba giá trị đầu — chúng serialize theo tên nên thêm cuối là an toàn):

```csharp
    /// <summary>Thesis hết hạn review hoặc invalidation rule đến hạn check.</summary>
    ThesisReviewDue,

    /// <summary>Mã trong watchlist có giá ≤ mục tiêu mua — cơ hội vào lệnh.</summary>
    BuyOpportunity,

    /// <summary>Vị thế đang mở nhưng chưa đặt stop-loss — rủi ro chưa được giới hạn.</summary>
    MissingStopLoss
}
```

Và sửa comment của `Info` (`:27-28`) vì nó không còn "reserved":

```csharp
    /// <summary>Thông tin — cơ hội mua, xếp dưới mọi cảnh báo rủi ro.</summary>
    Info
```

- [ ] **Step 4: Tách nhánh trong vòng lặp position**

Trong `LoadStopLossItemsAsync`, thay khối `foreach (var pos in summary.Positions)` (`:153-180`) bằng:

```csharp
foreach (var pos in summary.Positions)
{
    // Guard: RiskCalculationService trả DistanceToStopLossPercent=0 khi CurrentPrice<=0
    // (mã kém thanh khoản hoặc fetch giá lỗi). Giá không biết thì không kết luận gì —
    // đặt trước mọi nhánh để áp cho cả MissingStopLoss.
    if (pos.CurrentPrice <= 0) continue;

    if (pos.StopLossPrice == null)
    {
        items.Add(new DecisionItemDto
        {
            Id = $"MissingStopLoss:{portfolio.Id}:{pos.Symbol}",
            Type = DecisionType.MissingStopLoss,
            Severity = DecisionSeverity.Warning,
            Symbol = pos.Symbol,
            PortfolioId = portfolio.Id,
            PortfolioName = portfolio.Name,
            Headline = $"{pos.Symbol} chưa đặt stop-loss (giá {pos.CurrentPrice:N0})",
            ThesisOrReason = null,
            CurrentPrice = pos.CurrentPrice,
            PlannedExitPrice = null,
            TradePlanId = null,
            DueAt = now,
            CreatedAt = now
        });
        continue;
    }

    if (pos.DistanceToStopLossPercent > StopLossWarningThresholdPercent) continue;

    var hit = pos.DistanceToStopLossPercent <= StopLossCriticalThresholdPercent;
    items.Add(new DecisionItemDto
    {
        Id = $"StopLossHit:{portfolio.Id}:{pos.Symbol}",
        Type = DecisionType.StopLossHit,
        Severity = hit ? DecisionSeverity.Critical : DecisionSeverity.Warning,
        Symbol = pos.Symbol,
        PortfolioId = portfolio.Id,
        PortfolioName = portfolio.Name,
        Headline = hit
            ? $"{pos.Symbol} đã thủng SL {pos.StopLossPrice:N0} (giá {pos.CurrentPrice:N0})"
            : $"{pos.Symbol} cách SL {pos.DistanceToStopLossPercent:0.0}% (SL {pos.StopLossPrice:N0})",
        ThesisOrReason = null,
        CurrentPrice = pos.CurrentPrice,
        PlannedExitPrice = pos.StopLossPrice,
        TradePlanId = null,
        DueAt = now,
        CreatedAt = now
    });
}
```

- [ ] **Step 5: Chạy test**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~GetDecisionQueueQueryHandlerTests"`

Expected: TẤT CẢ PASS. `Handle_PositionWithZeroCurrentPrice_NotIncluded` (`:260-272`) vẫn xanh vì vị thế đó **có** SL — guard mới chỉ dời lên sớm hơn, không đổi kết quả.

- [ ] **Step 6: Commit** *(chờ người dùng duyệt)*

```bash
git add src/InvestmentApp.Application/Decisions tests/InvestmentApp.Application.Tests/Decisions
git commit -m "feat(decision): cảnh báo vị thế chưa đặt stop-loss trong hàng đợi quyết định"
```

---

### Task 3: Thêm `BuyOpportunity`

Nguồn duy nhất cần I/O mới. Handler nhận thêm hai dependency.

**Files:**
- Modify: `src/InvestmentApp.Application/Decisions/Queries/GetDecisionQueue/GetDecisionQueueQuery.cs` (constructor, `Handle`, thêm `LoadBuyOpportunityItemsAsync`)
- Test: `tests/InvestmentApp.Application.Tests/Decisions/GetDecisionQueueQueryHandlerTests.cs`

**Interfaces:**
- Consumes:
  - `IWatchlistRepository.GetByUserIdAsync(string userId, CancellationToken)` → `Task<IEnumerable<Watchlist>>` (`src/InvestmentApp.Application/RepositoryInterfaces.cs:157`)
  - `Watchlist.Items` → collection of `WatchlistItem { Symbol, Note, TargetBuyPrice (decimal?), TargetSellPrice, AddedAt }` (`src/InvestmentApp.Domain/ValueObjects/WatchlistItem.cs`)
  - `IStockPriceService.GetCurrentPricesAsync(IEnumerable<StockSymbol>)` → `Task<Dictionary<string, Money>>` — **không nhận CancellationToken** (`src/InvestmentApp.Application/Common/Interfaces/IStockPriceService.cs:8`)
  - `StockSymbol(string)` ctor và `Money.Amount` (`decimal`) — dùng như `ResolveDecisionCommand.cs:153-154`
- Produces: `DecisionType.BuyOpportunity` item với `PortfolioId = string.Empty`, `TradePlanId = null`, `Severity = Info`, Id format `BuyOpportunity:{symbol}`.

- [ ] **Step 1: Thêm mock + sửa constructor trong test**

Thêm hai field mock cạnh các mock có sẵn (`:19-24`):

```csharp
    private readonly Mock<IWatchlistRepository> _watchlistRepo = new();
    private readonly Mock<IStockPriceService> _priceService = new();
```

Sửa lời gọi constructor trong ctor test (`:30-32`):

```csharp
        _handler = new GetDecisionQueueQueryHandler(
            _portfolioRepo.Object, _planRepo.Object, _riskService.Object, _advisoryService.Object,
            _journalRepo.Object, _mediator.Object, _watchlistRepo.Object, _priceService.Object);
```

Thêm hai default vào khối defaults (sau `:44`):

```csharp
        _watchlistRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Watchlist>());
        _priceService.Setup(s => s.GetCurrentPricesAsync(It.IsAny<IEnumerable<StockSymbol>>()))
            .ReturnsAsync(new Dictionary<string, Money>());
```

Thêm `using InvestmentApp.Domain.ValueObjects;` vào đầu file.

- [ ] **Step 2: Thêm helper dựng watchlist**

Thêm vào vùng Helpers (sau `SetupRiskSummary`, `:345`):

```csharp
    private static Watchlist MakeWatchlist(params WatchlistItem[] items)
    {
        var wl = new Watchlist(UserId, "Theo dõi");
        foreach (var i in items) wl.AddItem(i.Symbol, i.Note, i.TargetBuyPrice, i.TargetSellPrice);
        return wl;
    }

    private static WatchlistItem MakeWatchItem(string symbol, decimal? targetBuy, string? note = null)
        => new() { Symbol = symbol, TargetBuyPrice = targetBuy, Note = note, AddedAt = DateTime.UtcNow };

    private void SetupWatchlist(params WatchlistItem[] items)
    {
        _watchlistRepo.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeWatchlist(items) });
    }

    private void SetupPrices(params (string Symbol, decimal Price)[] prices)
    {
        _priceService.Setup(s => s.GetCurrentPricesAsync(It.IsAny<IEnumerable<StockSymbol>>()))
            .ReturnsAsync(prices.ToDictionary(p => p.Symbol, p => new Money(p.Price)));
    }
```

Chữ ký đã đối chiếu: `Watchlist.AddItem(string symbol, string? note = null, decimal? targetBuyPrice = null, decimal? targetSellPrice = null)` (`Watchlist.cs:42`) và `Watchlist(string userId, string name, …)` (`:21`). `new Money(decimal)` mặc định currency VND (`MoneyTests.Constructor_WithAmountOnly_ShouldDefaultCurrencyToVND`). `AddItem` tự `ToUpper().Trim()` symbol — helper không cần chuẩn hoá thêm.

- [ ] **Step 3: Viết bốn test fail**

```csharp
[Fact]
public async Task Handle_WatchlistPriceAtOrBelowTarget_AddsInfoBuyOpportunity()
{
    SetupWatchlist(MakeWatchItem("VNM", targetBuy: 60_000m, note: "Chờ về vùng hỗ trợ"));
    SetupPrices(("VNM", 59_500m));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().HaveCount(1);
    var item = result.Items[0];
    item.Type.Should().Be(DecisionType.BuyOpportunity);
    item.Severity.Should().Be(DecisionSeverity.Info);
    item.Symbol.Should().Be("VNM");
    item.PortfolioId.Should().BeEmpty();
    item.TradePlanId.Should().BeNull();
    item.ThesisOrReason.Should().Be("Chờ về vùng hỗ trợ");
    item.Headline.Should().Contain("mục tiêu mua");
}

[Fact]
public async Task Handle_WatchlistWithoutTarget_NoOpportunity()
{
    SetupWatchlist(MakeWatchItem("MSN", targetBuy: null));
    SetupPrices(("MSN", 60_000m));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().BeEmpty();
}

[Fact]
public async Task Handle_WatchlistPriceAboveTarget_NoOpportunity()
{
    SetupWatchlist(MakeWatchItem("BVH", targetBuy: 40_000m));
    SetupPrices(("BVH", 44_000m));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().BeEmpty();
}

[Fact]
public async Task Handle_PriceServiceThrows_QueueStillReturnsOtherSources()
{
    // Nguyên tắc "block nào chậm/hỏng thì vắng mặt, không làm hỏng cả bản tin".
    var portfolio = MakePortfolio("p1", "Main");
    SetupPortfolios(portfolio);
    SetupRiskSummary(portfolio.Id, MakePosition("FPT", 89.5m, 89.4m, -0.1m));
    SetupWatchlist(MakeWatchItem("VNM", targetBuy: 60_000m));
    _priceService.Setup(s => s.GetCurrentPricesAsync(It.IsAny<IEnumerable<StockSymbol>>()))
        .ThrowsAsync(new HttpRequestException("provider down"));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().HaveCount(1);
    result.Items[0].Type.Should().Be(DecisionType.StopLossHit);
}

[Fact]
public async Task Handle_OpportunitySortsBelowRiskWarnings()
{
    var portfolio = MakePortfolio("p1", "Main");
    SetupPortfolios(portfolio);
    SetupRiskSummary(portfolio.Id, MakePosition("MWG", stopLossPrice: null, currentPrice: 50m, distanceToSlPercent: 0m));
    SetupWatchlist(MakeWatchItem("VNM", targetBuy: 60_000m));
    SetupPrices(("VNM", 59_000m));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().HaveCount(2);
    result.Items[0].Type.Should().Be(DecisionType.MissingStopLoss);   // Warning lên trước
    result.Items[1].Type.Should().Be(DecisionType.BuyOpportunity);    // Info xuống sau
}

[Fact]
public async Task Handle_SameSymbolOpportunityAndStopLoss_BothSurvive()
{
    // BuyOpportunity có PortfolioId rỗng nên thoát dedupe (Dedupe bỏ qua nhóm PortfolioId rỗng).
    // Đúng mong muốn: "FPT chạm mục tiêu mua" và "FPT thủng SL" là hai việc khác nhau,
    // không được nuốt nhau — kể cả khi trùng mã.
    var portfolio = MakePortfolio("p1", "Main");
    SetupPortfolios(portfolio);
    SetupRiskSummary(portfolio.Id, MakePosition("FPT", 89.5m, 89.4m, -0.1m));
    SetupWatchlist(MakeWatchItem("FPT", targetBuy: 90_000m));
    SetupPrices(("FPT", 89_000m));

    var result = await _handler.Handle(new GetDecisionQueueQuery { UserId = UserId }, CancellationToken.None);

    result.Items.Should().HaveCount(2);
    result.Items.Select(i => i.Type).Should()
        .Contain(DecisionType.StopLossHit).And
        .Contain(DecisionType.BuyOpportunity);
}
```

- [ ] **Step 4: Chạy test để xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~GetDecisionQueueQueryHandlerTests"`

Expected: FAIL biên dịch — constructor chưa nhận 8 tham số.

- [ ] **Step 5: Thêm dependency vào handler**

Thêm field, hằng timeout, và tham số constructor:

```csharp
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IStockPriceService _priceService;

    /// <summary>Trần chờ giá watchlist — quá hạn thì phần cơ hội vắng mặt, queue vẫn trả.</summary>
    private const int WatchlistPriceTimeoutSeconds = 5;

    public GetDecisionQueueQueryHandler(
        IPortfolioRepository portfolioRepo,
        ITradePlanRepository planRepo,
        IRiskCalculationService riskService,
        IScenarioAdvisoryService advisoryService,
        IJournalEntryRepository journalRepo,
        IMediator mediator,
        IWatchlistRepository watchlistRepo,
        IStockPriceService priceService)
    {
        _portfolioRepo = portfolioRepo;
        _planRepo = planRepo;
        _riskService = riskService;
        _advisoryService = advisoryService;
        _journalRepo = journalRepo;
        _mediator = mediator;
        _watchlistRepo = watchlistRepo;
        _priceService = priceService;
    }
```

- [ ] **Step 6: Viết `LoadBuyOpportunityItemsAsync`**

Thêm method mới cạnh các `Load…Async` khác:

```csharp
/// <summary>
/// Cơ hội mua từ watchlist: mã có mục tiêu mua và giá hiện tại đã ≤ mục tiêu.
/// Chỉ fetch giá cho mã CÓ mục tiêu — mã không đặt mục tiêu không thể sinh cơ hội.
/// </summary>
private async Task<List<DecisionItemDto>> LoadBuyOpportunityItemsAsync(string userId, CancellationToken ct)
{
    var watchlists = await _watchlistRepo.GetByUserIdAsync(userId, ct);

    var targets = watchlists
        .SelectMany(w => w.Items)
        .Where(i => i.TargetBuyPrice is > 0)
        .GroupBy(i => i.Symbol, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First())
        .ToList();

    if (targets.Count == 0) return new List<DecisionItemDto>();

    Dictionary<string, Money> raw;
    try
    {
        // GetCurrentPricesAsync không nhận CancellationToken — bọc timeout thủ công.
        raw = await _priceService
            .GetCurrentPricesAsync(targets.Select(t => new StockSymbol(t.Symbol)))
            .WaitAsync(TimeSpan.FromSeconds(WatchlistPriceTimeoutSeconds), ct);
    }
    catch (Exception) when (!ct.IsCancellationRequested)
    {
        // Giá hỏng hoặc quá chậm → phần cơ hội vắng mặt, các nguồn khác vẫn trả bình thường.
        return new List<DecisionItemDto>();
    }

    // Provider có thể trả key khác hoa/thường với symbol đã gửi.
    var prices = new Dictionary<string, Money>(raw, StringComparer.OrdinalIgnoreCase);

    var now = DateTime.UtcNow;
    var items = new List<DecisionItemDto>();

    foreach (var t in targets)
    {
        if (!prices.TryGetValue(t.Symbol, out var money)) continue;
        var price = money.Amount;
        if (price <= 0) continue;
        if (price > t.TargetBuyPrice!.Value) continue;

        items.Add(new DecisionItemDto
        {
            Id = $"BuyOpportunity:{t.Symbol}",
            Type = DecisionType.BuyOpportunity,
            Severity = DecisionSeverity.Info,
            Symbol = t.Symbol,
            PortfolioId = string.Empty,
            PortfolioName = string.Empty,
            Headline = $"{t.Symbol} giá {price:N0} ≤ mục tiêu mua {t.TargetBuyPrice.Value:N0}",
            ThesisOrReason = t.Note,
            CurrentPrice = price,
            PlannedExitPrice = null,
            TradePlanId = null,
            DueAt = now,
            CreatedAt = now
        });
    }

    return items;
}
```

- [ ] **Step 7: Nối vào `Handle`**

Sửa khối `Task.WhenAll` (`:70-80`):

```csharp
        var stopLossTask = LoadStopLossItemsAsync(portfolios, cancellationToken);
        var advisoriesTask = LoadAdvisoryItemsAsync(request.UserId, portfolios, cancellationToken);
        var reviewsTask = LoadThesisReviewItemsAsync(request.UserId, cancellationToken);
        var opportunitiesTask = LoadBuyOpportunityItemsAsync(request.UserId, cancellationToken);
        var resolvedTodayTask = LoadResolvedTodayAsync(request.UserId, cancellationToken);

        await Task.WhenAll(stopLossTask, advisoriesTask, reviewsTask, opportunitiesTask, resolvedTodayTask);

        var combined = stopLossTask.Result
            .Concat(advisoriesTask.Result)
            .Concat(reviewsTask.Result)
            .Concat(opportunitiesTask.Result)
            .ToList();
```

- [ ] **Step 8: Chạy test**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter "FullyQualifiedName~GetDecisionQueueQueryHandlerTests"`

Expected: TẤT CẢ PASS.

- [ ] **Step 9: Chạy toàn bộ suite Application để bắt hồi quy DI**

Run: `dotnet test tests/InvestmentApp.Application.Tests`

Expected: PASS. Nếu có test khác dựng `GetDecisionQueueQueryHandler` trực tiếp, nó sẽ đỏ vì constructor đổi — sửa lời gọi đó theo cùng cách ở Step 1.

- [ ] **Step 10: Chạy API test suite (DI resolve thật)**

Run: `dotnet test tests/InvestmentApp.Api.Tests`

Expected: PASS. `IWatchlistRepository` và `IStockPriceService` đều đã đăng ký sẵn trong DI (được dùng bởi `AiAssistantService` và `ResolveDecisionCommandHandler`) nên không phải thêm registration. Nếu đỏ vì không resolve được, thêm registration ở `Program.cs` / module DI tương ứng.

- [ ] **Step 11: Commit** *(chờ người dùng duyệt)*

```bash
git add src/InvestmentApp.Application/Decisions tests/InvestmentApp.Application.Tests/Decisions
git commit -m "feat(decision): đưa cơ hội mua từ watchlist vào hàng đợi quyết định"
```

---

### Task 4: Frontend — bỏ fallthrough, render hai type mới

**Files:**
- Modify: `frontend/src/app/core/services/decision.service.ts:13`
- Modify: `frontend/src/app/features/dashboard/widgets/decision-queue.component.ts:217-245`
- Test: `frontend/src/app/features/dashboard/widgets/decision-queue.component.spec.ts`

**Interfaces:**
- Consumes: `DecisionType` union từ `decision.service.ts`; `DecisionItemDto.type`, `.symbol`
- Produces: không có — đây là lớp lá.

- [ ] **Step 1: Mở rộng union type**

Trong `decision.service.ts`, thay dòng 13:

```typescript
export type DecisionType =
  | 'StopLossHit'
  | 'ScenarioTrigger'
  | 'ThesisReviewDue'
  | 'BuyOpportunity'
  | 'MissingStopLoss';
```

- [ ] **Step 2: Viết test fail**

File spec đã có sẵn factory `mockItem(over: Partial<DecisionItemDto>)` ở `:26-41` và hàm `setup(queue, streak)` ở `:49` — dùng lại, đừng dựng mới. Thêm vào trong `describe('DecisionQueueComponent')`:

```typescript
it('trả nhãn tiếng Việt cho mọi loại quyết định', () => {
  setup({ items: [], totalCount: 0 }, { streakDays: 0, hasData: false });
  expect(component.typeLabel('StopLossHit')).toBe('Stop-loss');
  expect(component.typeLabel('ScenarioTrigger')).toBe('Kịch bản');
  expect(component.typeLabel('ThesisReviewDue')).toBe('Review thesis');
  expect(component.typeLabel('BuyOpportunity')).toBe('Cơ hội mua');
  expect(component.typeLabel('MissingStopLoss')).toBe('Thiếu stop-loss');
});

it('điều hướng đúng màn cho hai loại mới', () => {
  setup({ items: [], totalCount: 0 }, { streakDays: 0, hasData: false });
  expect(component.getActionRoute(mockItem({ type: 'BuyOpportunity' }))).toEqual(['/watchlist']);
  expect(component.getActionRoute(mockItem({ type: 'MissingStopLoss' }))).toEqual(['/risk-dashboard']);
});
```

Đọc chữ ký thật của `setup(...)` và `DisciplineStreakDto` ở `:49-60` rồi chỉnh hai lời gọi `setup` cho khớp. File đã có test #5 *"Severity/type label đúng tiếng Việt"* (xem docblock `:8`) — nếu test đó đã kiểm ba nhãn cũ thì mở rộng nó thay vì thêm test trùng.

- [ ] **Step 3: Chạy test để xác nhận fail**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/decision-queue.component.spec.ts'`

Expected: FAIL — `typeLabel('BuyOpportunity')` trả `'Review thesis'` do fallthrough.

- [ ] **Step 4: Chuyển sang `Record` để mất key là lỗi biên dịch**

Trong `DecisionQueueComponent`, thay `typeLabel` (`:217-221`) và `getActionRoute` (`:228-232`):

```typescript
  /** Record thay vì chuỗi if: thêm DecisionType mới mà quên nhãn sẽ lỗi biên dịch,
   *  thay vì âm thầm dán nhãn sai như bản fallthrough trước đây. */
  private static readonly TYPE_LABELS: Record<DecisionType, string> = {
    StopLossHit: 'Stop-loss',
    ScenarioTrigger: 'Kịch bản',
    ThesisReviewDue: 'Review thesis',
    BuyOpportunity: 'Cơ hội mua',
    MissingStopLoss: 'Thiếu stop-loss',
  };

  private static readonly TYPE_ROUTES: Record<DecisionType, string[]> = {
    StopLossHit: ['/risk-dashboard'],
    ScenarioTrigger: ['/trade-plan'],
    ThesisReviewDue: ['/symbol-timeline'],
    BuyOpportunity: ['/watchlist'],
    MissingStopLoss: ['/risk-dashboard'],
  };

  typeLabel(t: DecisionType): string {
    return DecisionQueueComponent.TYPE_LABELS[t];
  }

  getActionRoute(item: DecisionItemDto): string[] {
    return DecisionQueueComponent.TYPE_ROUTES[item.type];
  }
```

- [ ] **Step 5: Thêm nhánh params cho hai type mới**

Trong `getActionParams` (`:234-245`), thêm ngay trước `return {};`:

```typescript
    if (item.type === 'BuyOpportunity' || item.type === 'MissingStopLoss') {
      return { symbol: item.symbol };
    }
```

- [ ] **Step 6: Kiểm badge severity `Info` có style**

Đọc template inline của component, tìm chỗ tô màu theo `item.severity`. Nếu chỉ xử lý `Critical` và `Warning`, thêm nhánh `Info` với tông trung tính (xám/xanh nhạt) để card cơ hội không bị đọc nhầm là cảnh báo. Nếu đã có nhánh mặc định phù hợp thì không sửa gì.

- [ ] **Step 7: Chạy test**

Run: `cd frontend && npx ng test --watch=false --browsers=ChromeHeadless --include='**/decision-queue.component.spec.ts'`

Expected: PASS.

- [ ] **Step 8: Kiểm biên dịch toàn frontend**

Run: `cd frontend && npx ng build`

Expected: build thành công, không lỗi type.

- [ ] **Step 9: Commit** *(chờ người dùng duyệt)*

```bash
git add frontend/src/app/core/services/decision.service.ts frontend/src/app/features/dashboard/widgets/
git commit -m "feat(dashboard): render thẻ cơ hội mua và thiếu stop-loss trong hàng đợi quyết định"
```

---

### Task 5: Mô tả MCP + tài liệu + ADR

**Files:**
- Modify: `src/InvestmentApp.Api/Mcp/DecisionTools.cs:16`
- Modify: `docs/architecture.md`, `docs/business-domain.md`, `docs/features.md`
- Modify: `frontend/src/assets/CHANGELOG.md`
- Modify/Create: user guide trong `frontend/src/assets/docs/` + đăng ký Help topic
- Create: `docs/adr/<số tiếp theo>-decision-queue-entry-side-signals.md`

- [ ] **Step 1: Cập nhật `[Description]` của MCP tool**

```csharp
    [McpServerTool(Name = "get_decision_queue", ReadOnly = true)]
    [Description("Hàng đợi quyết định hôm nay — gộp StopLoss + Scenario + Thesis-review + Cơ hội mua (watchlist chạm mục tiêu) + Vị thế thiếu stop-loss, đã dedupe, sort theo severity. Trả lời câu 'hôm nay cần quyết gì'.")]
```

Signature **không đổi** → inputSchema không đổi.

- [ ] **Step 2: Chạy test doc-drift / discovery của MCP**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter "FullyQualifiedName~Mcp"`

Expected: PASS. Nếu có test khoá cứng chuỗi description, cập nhật chuỗi kỳ vọng cho khớp.

- [ ] **Step 3: Viết ADR**

Đọc [`docs/adr/README.md`](../../../adr/README.md) để lấy số thứ tự và [`docs/adr/template.md`](../../../adr/template.md) để lấy cấu trúc. Nội dung cần chốt:

- **Bối cảnh:** Decision Queue chỉ có tín hiệu phía thoát hàng; vị thế thiếu SL vô hình; entry symbol-only không suppress được.
- **Quyết định:** thêm `BuyOpportunity` (Info) + `MissingStopLoss` (Warning); thêm tập suppression thứ ba theo `(symbol, type)` từ tag `trigger:` cho entry không portfolio/không plan.
- **Phương án đã cân nhắc và loại:** (a) thay hẳn khoá suppression bằng `(symbol, type)` — loại vì phá ngữ nghĩa suppression theo từng danh mục; (b) thêm tín hiệu kỹ thuật RSI/MACD làm nguồn cơ hội — loại vì nhiễu cao và phụ thuộc độ tin cậy provider.
- **Hệ quả:** `DecisionSeverity.Info` không còn "reserved cho V2"; FE bắt buộc dùng `Record` nên type mới về sau sẽ gây lỗi biên dịch thay vì dán nhãn sai.

- [ ] **Step 4: Cập nhật tài liệu**

| File | Nội dung |
|---|---|
| `docs/architecture.md` | Nguồn của Decision Queue: 3 → 5. Ghi thêm hai dependency mới của handler. |
| `docs/business-domain.md` | Quy tắc sinh `BuyOpportunity` (giá ≤ `TargetBuyPrice`, cần giá > 0) và `MissingStopLoss` (vị thế mở, `StopLossPrice == null`, giá > 0). |
| `docs/features.md` | Mô tả tính năng từ góc người dùng. |
| `frontend/src/assets/CHANGELOG.md` | Mục mới cho bản phát hành. |
| `frontend/src/assets/docs/…` | Hướng dẫn: đặt "Mục tiêu mua" trong watchlist để nhận thẻ cơ hội; ý nghĩa thẻ "Thiếu stop-loss". Đăng ký Help topic. |

- [ ] **Step 5: Commit** *(chờ người dùng duyệt)*

```bash
git add src/InvestmentApp.Api/Mcp/DecisionTools.cs docs/ frontend/src/assets/
git commit -m "docs: ghi nhận tín hiệu phía vào lệnh trong hàng đợi quyết định"
```

---

## Sau khi ship — việc người dùng phải làm

Code xong queue vẫn chưa sinh `BuyOpportunity` nào cho tới khi có mục tiêu mua. Đây là quyết định đầu tư, không phải quyết định kỹ thuật:

| Việc | Mã | Công cụ |
|---|---|---|
| Đặt `TargetBuyPrice` | VNM, MSN, BVH | `update_watchlist_item` |
| Đặt stop-loss | MWG, HPG, HHV | trade plan / risk dashboard |

Thẻ `MissingStopLoss` từ Task 2 chính là thứ nhắc việc thứ hai — nên nó ship trước.

---

## Checkpoint — Task 1–5 (2026-08-09)

- **Trạng thái:** Task 1–5 code xong. Task 1 đã commit (`08020a1`), Task 2–5 chưa commit.
- **Tests:** Application 246/246, Api 193/193, Frontend 21/21. Build FE sạch.
- **Lệch so với plan:**
  - Câu hỏi treo về `IsDeleted` đã giải: `WatchlistRepository.GetByUserIdAsync` **đã** lọc `!w.IsDeleted` (`:49`) → không thêm filter.
  - Code review đề nghị inject `ILogger` vào handler — **bác**, vì 0/137 handler trong Application dùng `ILogger` và `AiAssistantService` dùng đúng idiom `catch { skip }` cho khối enrichment tùy chọn.
  - Thêm ngoài plan: fallback runtime `?? 'Khác'` / `?? ['/symbol-timeline']` cho `Record` lookup, phòng FE cache cũ gặp API đã thêm type mới.
  - Thêm ngoài plan: vá doc-drift XML comment ở `DecisionItemDto`, `GetDecisionQueueQuery`, `DecisionsController` (đều còn ghi "3 nguồn").
- **Docs đã cập nhật:** `architecture.md`, `business-domain.md`, `features.md`, `CHANGELOG.md` (v2.69.0), `frontend/src/assets/docs/quan-ly-rui-ro.md`, ADR-0009.
- **Phase 4 — verify trên dữ liệu prod (acc test) đã xong.** Chạy localhost API với `Jwt__Key` prod truyền qua env (không ghi vào repo), gọi `GET /api/v1/decisions/queue`:

  ```
  [1] MissingStopLoss  Warning  HHV  pf='1eb8c52c'  HHV chưa đặt stop-loss (giá 10,050)
  [2] BuyOpportunity   Info     HHV  pf='(rỗng)'    HHV giá 10,050 ≤ mục tiêu mua 11,000
  ```

  Một kết quả chứng minh 3 việc: `BuyOpportunity` chạy thật, thứ tự Warning-trên-Info đúng, và dedupe không nuốt nhau dù **cùng mã**. Dashboard render đúng hai thẻ, nhãn tiếng Việt đủ dấu, nút BÁN ẩn ở cả hai (không có `tradePlanId`).

  Để lấp ô `BuyOpportunity` phải tạo watchlist tạm trên DB prod cho acc test — **đã xoá sau khi verify**, queue trở về baseline 1 item.

  **Chưa verify được:** vị thế **có** SL không sinh trùng `MissingStopLoss` — acc test không có vị thế nào có SL. Chỉ unit test phủ.

- **Next:** commit + PR.

---

## Ghi chú thẩm định plan

Các điểm đã đối chiếu với code thật trong lúc lập plan:

- `PositionRiskItem` có `StopLossPrice` (`decimal?`), `CurrentPrice` (`decimal`) — `IRiskCalculationService.cs:70-80`.
- `IWatchlistRepository.GetByUserIdAsync` trả `Task<IEnumerable<Watchlist>>` — `RepositoryInterfaces.cs:157`.
- `IStockPriceService.GetCurrentPricesAsync` **không** nhận `CancellationToken` — `IStockPriceService.cs:8`. Đây là lý do có `WaitAsync` thủ công ở Task 3 Step 6.
- `JournalEntry.Tags` là `List<string>` với public getter — `JournalEntry.cs:32`.
- `ResolveDecisionCommand` **đã** ghi tag `trigger:{Type}` ở cả hai nhánh (`:176`, `:236`) → journal prod hiện có đã mang tag, không cần backfill.
- Ba route `/risk-dashboard`, `/watchlist`, `/symbol-timeline` đều tồn tại — `app.routes.ts:106,131,141`.
- Tên class FE là `DecisionQueueComponent` — `decision-queue.component.ts:166`.
- `Watchlist.AddItem(string, string?, decimal?, decimal?)` và `Watchlist(string userId, string name, …)` — `Watchlist.cs:42, :21`.
- `new Money(decimal)` mặc định currency VND — `MoneyTests.Constructor_WithAmountOnly_ShouldDefaultCurrencyToVND`.
- FE spec đã có factory `mockItem(over)` và hàm `setup(queue, streak)` — `decision-queue.component.spec.ts:26, :49`.

**Còn phải quyết khi thực thi:** `IWatchlistRepository.GetByUserIdAsync` có tự lọc `Watchlist.IsDeleted` hay không (`Watchlist.cs:14`). Kiểm `WatchlistRepository` — nếu **không** lọc, thêm `.Where(w => !w.IsDeleted)` vào `LoadBuyOpportunityItemsAsync`, nếu không watchlist đã xoá vẫn sinh thẻ cơ hội. `AiAssistantService` hiện cũng không lọc rõ ràng nên đừng suy ra từ đó.
