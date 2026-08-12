using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class VolatilityBudgetServiceTests
{
    private const string PortfolioId = "port-1";

    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<ICorporateActionRepository> _actionRepo = new();
    private readonly Mock<IStockPriceRepository> _priceRepo = new();
    private readonly Mock<IRiskProfileRepository> _profileRepo = new();
    private readonly Mock<IMarketDataProvider> _marketData = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private VolatilityBudgetService CreateService() => new(
        _tradeRepo.Object, _actionRepo.Object, _priceRepo.Object, _profileRepo.Object,
        _marketData.Object, _cache, Mock.Of<ILogger<VolatilityBudgetService>>());

    public VolatilityBudgetServiceTests()
    {
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _actionRepo.Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CorporateAction>());
        _profileRepo.Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskProfile?)null);
        _priceRepo.Setup(r => r.GetBySymbolAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StockPrice>());
        _marketData.Setup(m => m.GetDailyHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockPriceData>());
    }

    // ---------- dựng dữ liệu ----------

    /// <summary>Chuỗi giá dao động đều, đủ dài để qua ngưỡng quan sát tối thiểu.</summary>
    private static List<StockPrice> Series(string symbol, int count = 60, decimal start = 50_000m,
        decimal swing = 0.02m, int phase = 0)
    {
        var prices = new List<StockPrice>();
        var price = start;
        var date = DateTime.UtcNow.Date.AddDays(-count);
        for (var i = 0; i < count; i++)
        {
            price *= 1 + ((i + phase) % 2 == 0 ? swing : -swing);
            prices.Add(new StockPrice(symbol, date.AddDays(i), price, price, price, price, 1_000));
        }
        return prices;
    }

    /// <summary>Chuỗi có một phiên GDKHQ chưa điều chỉnh — giá rơi một nửa trong một phiên.</summary>
    private static List<StockPrice> SeriesWithCorporateActionJump(string symbol, int count = 60)
    {
        var prices = Series(symbol, count, start: 150_000m, swing: 0.01m);
        for (var i = count / 2; i < count; i++)
            prices[i] = new StockPrice(symbol, prices[i].Date,
                prices[i].Close / 2m, prices[i].Close / 2m, prices[i].Close / 2m, prices[i].Close / 2m, 1_000);
        return prices;
    }

    private void GivenLocalPrices(string symbol, List<StockPrice> prices) =>
        _priceRepo.Setup(r => r.GetBySymbolAsync(symbol, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prices);

    private void GivenHolding(string symbol, decimal quantity, decimal price)
    {
        var existing = _tradeRepo.Object.GetByPortfolioIdAsync(PortfolioId, default).Result.ToList();
        existing.Add(new Trade(PortfolioId, symbol, TradeType.BUY, quantity, price,
            tradeDate: DateTime.UtcNow.Date.AddDays(-120)));
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
    }

    // ---------- ca kiểm thử ----------

    [Fact]
    public async Task EnoughLocalHistory_DoesNotCallProvider()
    {
        GivenLocalPrices("FPT", Series("FPT"));

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        result.DataQuality.Should().Be(VolatilityDataQuality.Full);
        _marketData.Verify(m => m.GetDailyHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SparseLocalHistory_BackfillsFromProviderAndPersists()
    {
        // Kho cục bộ chỉ có 10 phiên — dưới ngưỡng, phải đi lấy về.
        GivenLocalPrices("FPT", Series("FPT", count: 10));
        var backfilled = Series("FPT").Select(p => new StockPriceData
        {
            Symbol = "FPT", Date = p.Date, Open = p.Open, High = p.High, Low = p.Low, Close = p.Close, Volume = p.Volume
        }).ToList();

        var callCount = 0;
        _priceRepo.Setup(r => r.GetBySymbolAsync("FPT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? Series("FPT", count: 10) : Series("FPT"));
        _marketData.Setup(m => m.GetDailyHistoryAsync("FPT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(backfilled);

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        _marketData.Verify(m => m.GetDailyHistoryAsync("FPT", It.IsAny<CancellationToken>()), Times.Once);
        _priceRepo.Verify(r => r.UpsertAsync(It.IsAny<StockPrice>(), It.IsAny<CancellationToken>()),
            Times.Exactly(backfilled.Count));
        result.DataQuality.Should().Be(VolatilityDataQuality.Full);
    }

    [Fact]
    public async Task ProviderReturnsNothing_IsInsufficientAndDoesNotThrow()
    {
        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "NEWCO", 20_000m, 500);

        result.DataQuality.Should().Be(VolatilityDataQuality.Insufficient);
        result.MissingSymbols.Should().Contain("NEWCO");
        result.CurrentVolatilityPercent.Should().BeNull();
        result.ProjectedVolatilityPercent.Should().BeNull();
        result.MaxQuantityWithinBudget.Should().BeNull();
        result.IsUnconstrainedByVolatility.Should().BeFalse(
            "thiếu dữ liệu KHÁC hẳn không bị ràng buộc — gộp hai cái là biến 'không biết' thành 'thoải mái'");
    }

    [Fact]
    public async Task ProviderThrows_IsInsufficientNotFiveHundred()
    {
        _marketData.Setup(m => m.GetDailyHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("24hmoney down"));

        var act = async () => await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        var result = await act.Should().NotThrowAsync();
        result.Subject.DataQuality.Should().Be(VolatilityDataQuality.Insufficient);
    }

    [Fact]
    public async Task ProviderThrows_SeparatesFetchFailureFromMissingHistory()
    {
        // Hai ca cùng cho DataQuality = Insufficient nhưng KHÁC nhau về sự thật, nên phải khác nhau
        // về chỗ ghi: nguồn hỏng thì mã đó vào FetchFailedSymbols, không vào MissingSymbols. Gộp
        // lại là panel nói "FPT chưa đủ lịch sử giá" trong khi FPT có thừa — người dùng đọc xong
        // kết luận sai rằng mã này mới hoặc thanh khoản kém.
        _marketData.Setup(m => m.GetDailyHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("24hmoney down"));

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        result.FetchFailedSymbols.Should().Contain("FPT");
        result.MissingSymbols.Should().NotContain("FPT",
            "lấy hỏng không phải là thiếu lịch sử — nói nhầm là nói sai sự thật về mã đó");
    }

    [Fact]
    public async Task RepositoryWriteFails_IsNotReportedAsSourceFailure()
    {
        // Lấy từ nguồn THÀNH CÔNG, chỉ ghi vào kho hỏng. Gán nhãn "nguồn dữ liệu đang lỗi" cho ca
        // này là trỏ sai chỗ — đúng kiểu nhập nhèm mà FetchFailedSymbols sinh ra để dẹp.
        _marketData.Setup(m => m.GetDailyHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Series("FPT", 80).Select(p => new StockPriceData
            {
                Symbol = p.Symbol, Date = p.Date,
                Open = p.Open, High = p.High, Low = p.Low, Close = p.Close, Volume = p.Volume
            }).ToList());
        _priceRepo.Setup(r => r.UpsertAsync(It.IsAny<StockPrice>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("mongo timeout"));

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        result.FetchFailedSymbols.Should().NotContain("FPT",
            "kho ghi hỏng không phải nguồn hỏng — nói nhầm là chỉ sai chỗ cần sửa");
    }

    [Fact]
    public async Task ProviderReturnsEmptyList_IsMissingNotFetchFailure()
    {
        // Ca đối chứng: nguồn trả về ĐÚNG cấu trúc nhưng rỗng. Đây mới thật sự là "mã chưa có lịch
        // sử". Không có ca này thì test trên vẫn xanh khi ai đó dồn hết mọi thứ vào FetchFailed.
        _marketData.Setup(m => m.GetDailyHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockPriceData>());

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "NEWCO", 20_000m, 500);

        result.MissingSymbols.Should().Contain("NEWCO");
        result.FetchFailedSymbols.Should().BeEmpty();
    }

    [Fact]
    public async Task OneHoldingMissingHistory_IsPartialAndStillComputes()
    {
        GivenLocalPrices("FPT", Series("FPT"));
        GivenHolding("HPG", 1_000, 28_000m);
        GivenHolding("GHOST", 500, 10_000m);
        GivenLocalPrices("HPG", Series("HPG", phase: 1));
        // GHOST không có lịch sử ở đâu cả.

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        result.DataQuality.Should().Be(VolatilityDataQuality.Partial);
        result.MissingSymbols.Should().Contain("GHOST");
        result.CurrentVolatilityPercent.Should().NotBeNull("HPG vẫn đủ dữ liệu để ước lượng");
    }

    [Fact]
    public async Task SecondCallWithinTtl_HitsCacheInsteadOfRepository()
    {
        GivenLocalPrices("FPT", Series("FPT"));
        var service = CreateService();

        await service.GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);
        await service.GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 200);

        // Panel bị gọi lại mỗi nhịp debounce 500ms — lần thứ hai không được chạm kho nữa.
        _priceRepo.Verify(r => r.GetBySymbolAsync("FPT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CorporateActionJump_IsFilteredAndFlagged()
    {
        // Ca chống hồi quy trọng tâm (ADR-0014 §3.2): một phiên −50% chưa điều chỉnh thổi σ lên
        // nhiều lần. Không lọc thì σ ra ba chữ số và trần khối lượng vô nghĩa, mà không gì báo hiệu.
        GivenLocalPrices("VHM", SeriesWithCorporateActionJump("VHM"));

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "VHM", 75_000m, 100);

        result.AdjustedSymbols.Should().Contain("VHM");
        result.DataQuality.Should().Be(VolatilityDataQuality.Partial);
    }

    [Fact]
    public async Task BudgetIsDerivedFromRiskProfileDrawdown()
    {
        GivenLocalPrices("FPT", Series("FPT"));
        _profileRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RiskProfile(PortfolioId, "user-1", maxDrawdownAlertPercent: 20m));

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        result.SourceMaxDrawdownPercent.Should().Be(20m);
        result.BudgetVolatilityPercent.Should().BeApproximately(42.2m, 0.2m);
    }

    [Fact]
    public async Task NoRiskProfile_FallsBackToDefaultDrawdown()
    {
        GivenLocalPrices("FPT", Series("FPT"));

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        result.SourceMaxDrawdownPercent.Should().Be(10m);
        result.BudgetVolatilityPercent.Should().BeApproximately(21.1m, 0.1m);
    }

    [Fact]
    public async Task AlreadyHeldSymbol_IsNotCountedTwice()
    {
        GivenLocalPrices("FPT", Series("FPT"));
        GivenHolding("FPT", 1_000, 100_000m);

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        // Mã đang xét bị loại khỏi rổ đang giữ rồi cộng lại ở bước chiếu. Nếu đếm hai lần thì danh
        // mục "hiện tại" đã gồm cả lệnh chưa đặt.
        result.CapitalWeightPercent.Should().Be(100m,
            "FPT là vị thế duy nhất và nó bị loại khỏi rổ nền, nên toàn bộ giá trị sau lệnh là của nó");
    }

    [Fact]
    public async Task EmptyPortfolio_StillReturnsBudgetAndSymbolNumbers()
    {
        GivenLocalPrices("FPT", Series("FPT"));

        var result = await CreateService().GetSizingForPlanAsync(PortfolioId, "FPT", 100_000m, 100);

        result.BudgetVolatilityPercent.Should().BeGreaterThan(0m);
        result.CorrelationWithPortfolio.Should().BeNull("danh mục rỗng thì không có gì để tương quan");
        result.PortfolioAlreadyOverBudget.Should().BeFalse();
    }
}
