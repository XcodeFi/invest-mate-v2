using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class PriceSnapshotJobServiceTests
{
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IStockPriceRepository> _priceRepo = new();
    private readonly Mock<IMarketIndexRepository> _indexRepo = new();
    private readonly Mock<IMarketDataProvider> _marketData = new();
    private readonly Mock<IStopLossTargetRepository> _slRepo = new();
    private readonly Mock<ICorporateActionRepository> _corporateActionRepo = new();
    private readonly Mock<ISignalSeriesWarmup> _warmup = new();
    private readonly PriceSnapshotJobService _sut;

    public PriceSnapshotJobServiceTests()
    {
        // Mặc định: không có sự kiện quyền — các test cũ giữ nguyên kỳ vọng
        _corporateActionRepo
            .Setup(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CorporateAction>());

        _sut = new PriceSnapshotJobService(
            _tradeRepo.Object,
            _priceRepo.Object,
            _indexRepo.Object,
            _marketData.Object,
            _slRepo.Object,
            _corporateActionRepo.Object,
            _warmup.Object,
            NullLogger<PriceSnapshotJobService>.Instance);

        _warmup.Setup(w => w.WarmNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SignalWarmupOutcome(0, "ok"));
        _slRepo.Setup(r => r.GetUntriggeredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StopLossTarget>());
        _marketData.Setup(m => m.GetIndexDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketIndexData?)null);
    }

    private static Trade CreateTrade(string symbol)
        => new("portfolio-1", symbol, TradeType.BUY, 100m, 50_000m, tradeDate: DateTime.UtcNow);

    private static StockPriceData CreatePriceData(string symbol, decimal close)
        => new()
        {
            Symbol = symbol,
            Date = DateTime.UtcNow.Date,
            Open = close, High = close, Low = close, Close = close, Volume = 1000
        };

    [Fact]
    public async Task RunAsync_NoTrades_ReturnsEmptyResult_AndDoesNotCallProvider()
    {
        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());

        var result = await _sut.RunAsync(CancellationToken.None);

        result.SymbolsFetched.Should().Be(0);
        result.PricesPersisted.Should().Be(0);
        _marketData.Verify(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _priceRepo.Verify(r => r.UpsertAsync(It.IsAny<StockPrice>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_WithTrades_FetchesDistinctSymbolsAndUpsertsPrices()
    {
        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTrade("FPT"), CreateTrade("FPT"), CreateTrade("VNM") });
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData>
            {
                ["FPT"] = CreatePriceData("FPT", 120_000m),
                ["VNM"] = CreatePriceData("VNM", 70_000m)
            });

        var result = await _sut.RunAsync(CancellationToken.None);

        result.SymbolsFetched.Should().Be(2);
        result.PricesPersisted.Should().Be(2);
        _priceRepo.Verify(r => r.UpsertAsync(It.IsAny<StockPrice>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_WithTrades_UpdatesMarketIndicesWhenAvailable()
    {
        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTrade("FPT") });
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData> { ["FPT"] = CreatePriceData("FPT", 120_000m) });
        _marketData.Setup(m => m.GetIndexDataAsync("VNINDEX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketIndexData
            {
                IndexSymbol = "VNINDEX", Date = DateTime.UtcNow.Date,
                PriorClose = 1200m, High = 1220m, Low = 1180m, Close = 1210m, Volume = 1_000_000
            });
        _marketData.Setup(m => m.GetIndexDataAsync("VN30", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketIndexData?)null);

        var result = await _sut.RunAsync(CancellationToken.None);

        result.IndicesUpdated.Should().Be(1);
        _indexRepo.Verify(r => r.UpsertAsync(
            It.Is<MarketIndex>(mi => mi.IndexSymbol == "VNINDEX"), It.IsAny<CancellationToken>()),
            Times.Once);
        _indexRepo.Verify(r => r.UpsertAsync(
            It.Is<MarketIndex>(mi => mi.IndexSymbol == "VN30"), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_PriceBelowStopLoss_TriggersStopLossAndUpdates()
    {
        var slt = new StopLossTarget(
            tradeId: "trade-1", portfolioId: "p-1", userId: "u-1", symbol: "FPT",
            entryPrice: 120_000m, stopLossPrice: 115_000m, targetPrice: 130_000m);

        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTrade("FPT") });
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData> { ["FPT"] = CreatePriceData("FPT", 114_000m) });
        _slRepo.Setup(r => r.GetUntriggeredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { slt });

        var result = await _sut.RunAsync(CancellationToken.None);

        result.StopLossTriggered.Should().Be(1);
        slt.IsStopLossTriggered.Should().BeTrue();
        _slRepo.Verify(r => r.UpdateAsync(slt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_PriceAboveTarget_TriggersTargetAndUpdates()
    {
        var slt = new StopLossTarget(
            tradeId: "trade-1", portfolioId: "p-1", userId: "u-1", symbol: "FPT",
            entryPrice: 120_000m, stopLossPrice: 115_000m, targetPrice: 130_000m);

        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTrade("FPT") });
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData> { ["FPT"] = CreatePriceData("FPT", 131_000m) });
        _slRepo.Setup(r => r.GetUntriggeredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { slt });

        var result = await _sut.RunAsync(CancellationToken.None);

        result.TargetsTriggered.Should().Be(1);
        slt.IsTargetTriggered.Should().BeTrue();
        _slRepo.Verify(r => r.UpdateAsync(slt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_PriceWithinRange_DoesNotTrigger()
    {
        var slt = new StopLossTarget(
            tradeId: "trade-1", portfolioId: "p-1", userId: "u-1", symbol: "FPT",
            entryPrice: 120_000m, stopLossPrice: 115_000m, targetPrice: 130_000m);

        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTrade("FPT") });
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData> { ["FPT"] = CreatePriceData("FPT", 120_000m) });
        _slRepo.Setup(r => r.GetUntriggeredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { slt });

        var result = await _sut.RunAsync(CancellationToken.None);

        result.StopLossTriggered.Should().Be(0);
        result.TargetsTriggered.Should().Be(0);
        slt.IsStopLossTriggered.Should().BeFalse();
        slt.IsTargetTriggered.Should().BeFalse();
        _slRepo.Verify(r => r.UpdateAsync(It.IsAny<StopLossTarget>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_StopLossPriceZero_DoesNotTriggerStopLoss()
    {
        // Stop-loss = 0 means "no stop-loss configured" → must not trigger even at price 0
        var slt = new StopLossTarget(
            tradeId: "trade-1", portfolioId: "p-1", userId: "u-1", symbol: "FPT",
            entryPrice: 120_000m, stopLossPrice: 0m, targetPrice: 130_000m);

        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTrade("FPT") });
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData> { ["FPT"] = CreatePriceData("FPT", 1_000m) });
        _slRepo.Setup(r => r.GetUntriggeredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { slt });

        var result = await _sut.RunAsync(CancellationToken.None);

        result.StopLossTriggered.Should().Be(0);
        slt.IsStopLossTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_TradeSymbolDifferentCase_MatchesPriceLookupCaseInsensitively()
    {
        // Trade symbol stored uppercase; provider returns same; SLT uses mixed case → must still match
        var slt = new StopLossTarget(
            tradeId: "trade-1", portfolioId: "p-1", userId: "u-1", symbol: "fpt",
            entryPrice: 120_000m, stopLossPrice: 115_000m, targetPrice: 130_000m);

        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CreateTrade("FPT") });
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData> { ["FPT"] = CreatePriceData("FPT", 114_000m) });
        _slRepo.Setup(r => r.GetUntriggeredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { slt });

        var result = await _sut.RunAsync(CancellationToken.None);

        result.StopLossTriggered.Should().Be(1);
    }

    // ───── lấy trước chuỗi giá cho tỉ lệ nền (warm-up) ─────

    private PriceSnapshotJobService SutWith(TimeSpan warmupTimeout)
        => new(_tradeRepo.Object, _priceRepo.Object, _indexRepo.Object, _marketData.Object, _slRepo.Object,
            _corporateActionRepo.Object, _warmup.Object, NullLogger<PriceSnapshotJobService>.Instance, warmupTimeout);

    private void OneTradeOneSymbol()
    {
        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { CreateTrade("FPT") });
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData> { ["FPT"] = CreatePriceData("FPT", 100_000m) });
    }

    [Fact]
    public async Task warmup_runs_after_the_prices_work()
    {
        OneTradeOneSymbol();

        await _sut.RunAsync(CancellationToken.None);

        _warmup.Verify(w => w.WarmNextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task warmup_runs_after_the_prices_work_not_before()
    {
        OneTradeOneSymbol();
        var order = new List<string>();
        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("prices"))
            .ReturnsAsync(new Dictionary<string, StockPriceData> { ["FPT"] = CreatePriceData("FPT", 100_000m) });
        _warmup.Setup(w => w.WarmNextAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("warmup"))
            .ReturnsAsync(new SignalWarmupOutcome(0, "ok"));

        await _sut.RunAsync(CancellationToken.None);

        order.Should().Equal("prices", "warmup");
    }

    [Fact]
    public async Task warmup_also_runs_when_there_are_no_symbols_to_snapshot()
    {
        _tradeRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Trade>());

        var result = await _sut.RunAsync(CancellationToken.None);

        result.Should().Be(PriceSnapshotJobResult.Empty);
        _warmup.Verify(w => w.WarmNextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task prices_job_result_unaffected_by_warmup_exception()
    {
        OneTradeOneSymbol();
        _warmup.Setup(w => w.WarmNextAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("warm-up nổ"));

        var result = await _sut.RunAsync(CancellationToken.None);

        result.SymbolsFetched.Should().Be(1);
        result.PricesPersisted.Should().Be(1);
    }

    [Fact]
    public async Task a_warmup_that_overruns_its_deadline_is_cut_off_without_failing_the_job()
    {
        OneTradeOneSymbol();
        _warmup.Setup(w => w.WarmNextAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) => { await Task.Delay(Timeout.Infinite, ct); return new SignalWarmupOutcome(0, "x"); });

        var result = await SutWith(TimeSpan.FromMilliseconds(50)).RunAsync(CancellationToken.None);

        result.PricesPersisted.Should().Be(1);
    }

    [Fact]
    public async Task a_cancelled_request_is_not_swallowed_by_the_warmup_isolation()
    {
        OneTradeOneSymbol();
        using var cts = new CancellationTokenSource();
        _warmup.Setup(w => w.WarmNextAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); await Task.Yield(); return new SignalWarmupOutcome(0, "x"); });

        var act = async () => await _sut.RunAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
