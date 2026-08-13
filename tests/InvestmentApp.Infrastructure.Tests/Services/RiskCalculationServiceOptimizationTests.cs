using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class RiskCalculationServiceOptimizationTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<ITradeRepository> _tradeRepo;
    private readonly Mock<IStockPriceService> _stockPriceService;
    private readonly Mock<IStopLossTargetRepository> _slTargetRepo;
    private readonly Mock<IPortfolioSnapshotRepository> _snapshotRepo;
    private readonly Mock<IStockPriceRepository> _stockPriceRepo;
    private readonly Mock<IPnLService> _pnlService;
    private readonly Mock<ICapitalFlowRepository> _capitalFlowRepo;
    private readonly Mock<IRiskProfileRepository> _riskProfileRepo;
    private readonly Mock<IFundamentalDataProvider> _fundamentalProvider;
    private readonly Mock<IComprehensiveStockDataProvider> _comprehensiveProvider;
    private readonly Mock<IMarketDataProvider> _marketDataProvider;
    private readonly Mock<ICorporateActionRepository> _corporateActionRepository;
    // DefaultValue.Empty: repo trả list rỗng thay vì null, nên các test không liên quan
    // tới kế hoạch không phải khai stub riêng.
    private readonly Mock<ITradePlanRepository> _tradePlanRepo = new() { DefaultValue = DefaultValue.Empty };
    private readonly Mock<ILogger<RiskCalculationService>> _logger;
    private readonly RiskCalculationService _sut;

    private const string PortfolioId = "portfolio-1";
    private const string UserId = "user-1";

    public RiskCalculationServiceOptimizationTests()
    {
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _tradeRepo = new Mock<ITradeRepository>();
        _stockPriceService = new Mock<IStockPriceService>();
        _slTargetRepo = new Mock<IStopLossTargetRepository>();
        _snapshotRepo = new Mock<IPortfolioSnapshotRepository>();
        _stockPriceRepo = new Mock<IStockPriceRepository>();
        _pnlService = new Mock<IPnLService>();
        _capitalFlowRepo = new Mock<ICapitalFlowRepository>();
        _riskProfileRepo = new Mock<IRiskProfileRepository>();
        _fundamentalProvider = new Mock<IFundamentalDataProvider>();
        _comprehensiveProvider = new Mock<IComprehensiveStockDataProvider>();
        _marketDataProvider = new Mock<IMarketDataProvider>();
        _corporateActionRepository = new Mock<ICorporateActionRepository>();
        _corporateActionRepository
            .Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CorporateAction>());
        _logger = new Mock<ILogger<RiskCalculationService>>();

        _sut = new RiskCalculationService(
            _portfolioRepo.Object,
            _tradeRepo.Object,
            _stockPriceService.Object,
            _slTargetRepo.Object,
            _snapshotRepo.Object,
            _stockPriceRepo.Object,
            _pnlService.Object,
            _capitalFlowRepo.Object,
            _riskProfileRepo.Object,
            _fundamentalProvider.Object,
            _comprehensiveProvider.Object,
            _marketDataProvider.Object,
            _corporateActionRepository.Object,
            _tradePlanRepo.Object,
            new MemoryCache(new MemoryCacheOptions()),
            _logger.Object);
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private void SetupPortfolio(decimal initialCapital = 100_000_000m)
    {
        var portfolio = new Portfolio(UserId, "Test Portfolio", initialCapital);
        _portfolioRepo.Setup(r => r.GetByIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
    }

    private void SetupRiskProfile(decimal maxPositionSize = 20m, decimal maxSectorExposure = 40m)
    {
        var profile = new RiskProfile(PortfolioId, UserId, maxPositionSize, maxSectorExposure, 10m, 2m, 5m);
        _riskProfileRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
    }

    private void SetupTrades(params string[] symbols)
    {
        var trades = symbols.Select(s => new Trade(PortfolioId, s, TradeType.BUY, 100, 50000m)).ToList();
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trades);
    }

    private void SetupPositionPnL(string symbol, decimal quantity, decimal currentPrice, decimal marketValue)
    {
        _pnlService.Setup(s => s.CalculatePositionPnLAsync(
                PortfolioId, It.Is<StockSymbol>(ss => ss.Value == symbol), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PositionPnL
            {
                Symbol = symbol,
                Quantity = quantity,
                CurrentPrice = currentPrice,
                AverageCost = currentPrice * 0.9m,
                RealizedPnL = 0
            });
    }

    private void SetupPortfolioPnL(decimal totalValue, decimal totalInvested)
    {
        _pnlService.Setup(s => s.CalculatePortfolioPnLAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PortfolioPnLSummary
            {
                TotalPortfolioValue = totalValue,
                TotalInvested = totalInvested,
                Positions = new List<PositionPnL>()
            });
    }

    // Dựng ngành qua IComprehensiveStockDataProvider — provider mà Program.cs đăng ký thật.
    // Trước đây helper này dựng IFundamentalDataProvider, nhưng interface đó được đăng ký là
    // NoOpFundamentalDataProvider (luôn trả null) nên các test ngành xanh mà production thì
    // mọi vị thế rơi vào rổ "Không xác định" và hạn mức ngành chưa từng bắn.
    private void SetupIndustry(string symbol, string? industry)
    {
        _comprehensiveProvider
            .Setup(p => p.GetComprehensiveDataAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData
            {
                Company = new CompanyOverview { Industry = industry }
            });
    }

    private void SetupEmptyStopLossTargets()
    {
        _slTargetRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StopLossTarget>());
    }

    private void SetupEmptySnapshots()
    {
        _snapshotRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PortfolioSnapshotEntity>());
    }

    private void SetupCapitalFlows()
    {
        _capitalFlowRepo.Setup(r => r.GetTotalFlowByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
    }

    private void SetupEmptyPriceHistory()
    {
        _stockPriceRepo.Setup(r => r.GetBySymbolAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StockPrice>());
    }

    // ─── Portfolio Optimization Tests ─────────────────────────────────

    [Fact]
    public async Task GetPortfolioOptimizationAsync_ConcentratedPosition_ReturnsConcentrationAlert()
    {
        // Arrange: VIC at 35% of portfolio (limit 20%)
        SetupPortfolio();
        SetupRiskProfile(maxPositionSize: 20m);
        SetupTrades("VIC", "FPT");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 500, 70_000m, 35_000_000m); // 35%
        SetupPositionPnL("FPT", 1000, 65_000m, 65_000_000m); // 65%
        SetupIndustry("VIC", "Bất động sản");
        SetupIndustry("FPT", "Công nghệ");
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();
        SetupEmptyPriceHistory();

        // Act
        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Assert
        result.Should().NotBeNull();
        result.ConcentrationAlerts.Should().NotBeEmpty();
        result.ConcentrationAlerts.Should().Contain(a => a.Symbol == "VIC" || a.Symbol == "FPT");
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_ConcentratedPosition_SeverityDangerWhenOver150Percent()
    {
        // Arrange: VIC at ~35% with 10% limit → 350% of limit → danger
        // totalValue = max(100M + (100M + 0 - 80M), 100M) = 120M
        // VIC = 500*84000 = 42M → 42/120 = 35% → 35% > 10%*1.5 = 15% → danger
        SetupPortfolio();
        SetupRiskProfile(maxPositionSize: 10m);
        SetupTrades("VIC", "FPT");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 500, 84_000m, 42_000_000m);
        SetupPositionPnL("FPT", 1000, 58_000m, 58_000_000m);
        SetupIndustry("VIC", "Bất động sản");
        SetupIndustry("FPT", "Công nghệ");
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();
        SetupEmptyPriceHistory();

        // Act
        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Assert
        var vicAlert = result.ConcentrationAlerts.FirstOrDefault(a => a.Symbol == "VIC");
        vicAlert.Should().NotBeNull();
        vicAlert!.Severity.Should().Be("danger");
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_SectorOverweight_ReturnsSectorAlert()
    {
        // Arrange: 2 real estate stocks = 55% combined (limit 40%)
        SetupPortfolio();
        SetupRiskProfile(maxSectorExposure: 40m);
        SetupTrades("VIC", "VHM", "FPT");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 300, 100_000m, 30_000_000m); // 30%
        SetupPositionPnL("VHM", 250, 100_000m, 25_000_000m); // 25%
        SetupPositionPnL("FPT", 600, 75_000m, 45_000_000m);  // 45%
        SetupIndustry("VIC", "Bất động sản");
        SetupIndustry("VHM", "Bất động sản");
        SetupIndustry("FPT", "Công nghệ");
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();
        SetupEmptyPriceHistory();

        // Act
        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Assert
        result.SectorExposures.Should().NotBeEmpty();
        var realEstate = result.SectorExposures.FirstOrDefault(s => s.Sector == "Bất động sản");
        realEstate.Should().NotBeNull();
        realEstate!.IsOverweight.Should().BeTrue();
        realEstate.Symbols.Should().Contain("VIC").And.Contain("VHM");
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_IndustryFromRegisteredProvider_ReturnsSectorAlert()
    {
        // Y nguyên tình huống của test SectorOverweight ở trên, khác ĐÚNG một điều: ngành đến từ
        // IComprehensiveStockDataProvider. Test kia dựng IFundamentalDataProvider và xanh, nhưng
        // interface đó được đăng ký là NoOpFundamentalDataProvider (Program.cs) nên trên production
        // luôn trả null — hạn mức ngành vì thế chưa từng bắn dù test vẫn xanh.
        SetupPortfolio();
        SetupRiskProfile(maxSectorExposure: 40m);
        SetupTrades("VIC", "VHM", "FPT");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 300, 100_000m, 30_000_000m); // 30%
        SetupPositionPnL("VHM", 250, 100_000m, 25_000_000m); // 25%
        SetupPositionPnL("FPT", 600, 75_000m, 45_000_000m);  // 45%
        SetupIndustry("VIC", "Bất động sản");
        SetupIndustry("VHM", "Bất động sản");
        SetupIndustry("FPT", "Công nghệ");
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();
        SetupEmptyPriceHistory();

        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Mẫu số là 120tr, không phải 100tr: totalValue = giá trị vị thế 100tr + tiền mặt 20tr
        // (InitialCapital 100tr − TotalInvested 80tr). 55tr/120tr = 45,83% và vẫn vượt hạn mức 40%.
        var realEstate = result.SectorExposures.FirstOrDefault(s => s.Sector == "Bất động sản");
        realEstate.Should().NotBeNull();
        realEstate!.ExposurePercent.Should().Be(45.83m);
        realEstate.IsOverweight.Should().BeTrue();
        realEstate.Symbols.Should().Contain("VIC").And.Contain("VHM");
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_UnknownIndustryOverLimit_ShouldFlagOverweight()
    {
        // Không biết mình đang dồn vào đâu là một thông tin, không phải sự vắng mặt của thông tin:
        // 55% danh mục ở rổ "Không xác định" phải hiện vượt hạn mức (spec Q2).
        SetupPortfolio();
        SetupRiskProfile(maxSectorExposure: 40m);
        SetupTrades("VIC", "VHM");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 300, 100_000m, 30_000_000m); // 30%
        SetupPositionPnL("VHM", 250, 100_000m, 25_000_000m); // 25%
        // Không dựng ngành cho mã nào — provider trả null, đúng như production hiện tại.
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();
        SetupEmptyPriceHistory();

        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // 55tr trên tổng 120tr (100tr vị thế + 20tr tiền mặt) = 45,83%, vượt hạn mức 40%.
        var unknown = result.SectorExposures.FirstOrDefault(s => s.Sector == "Không xác định");
        unknown.Should().NotBeNull();
        unknown!.ExposurePercent.Should().Be(45.83m);
        unknown.IsOverweight.Should().BeTrue();
    }

    // ─── Sector exposure cho một lệnh dự kiến ─────────────────────────

    private void SetupSectorForPlanScenario()
    {
        SetupPortfolio();
        SetupRiskProfile(maxSectorExposure: 40m);
        SetupTrades("VIC", "VHM", "FPT");
        SetupPortfolioPnL(100_000_000m, 80_000_000m); // totalValue = 100tr vị thế + 20tr tiền mặt = 120tr
        SetupPositionPnL("VIC", 300, 100_000m, 30_000_000m);
        SetupPositionPnL("VHM", 250, 100_000m, 25_000_000m);
        SetupPositionPnL("FPT", 600, 75_000m, 45_000_000m);
        SetupIndustry("VIC", "Bất động sản");
        SetupIndustry("VHM", "Bất động sản");
        SetupIndustry("FPT", "Công nghệ");
    }

    [Fact]
    public async Task GetSectorExposureForPlanAsync_AddingToHeldSector_ShouldNotAddPlanSizeToDenominator()
    {
        // Ca phân biệt spec Q3. totalValue đã gồm tiền mặt, nên mua bằng tiền trong danh mục
        // không làm tổng đổi: TotalInvested tăng và tiền mặt giảm cùng một lượng.
        // Đúng:  (55tr + 9tr) / 120tr = 53,33%
        // Sai:   (55tr + 9tr) / (120tr + 9tr) = 49,61%  ← nếu cộng addValue vào mẫu số
        SetupSectorForPlanScenario();

        var result = await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);

        result.Sector.Should().Be("Bất động sản");
        result.CurrentPercent.Should().Be(45.83m);
        result.ProjectedPercent.Should().Be(53.33m);
        result.LimitPercent.Should().Be(40m);
    }

    [Fact]
    public async Task GetSectorExposureForPlanAsync_PlannedSymbolAlreadyHeld_ShouldResolveIndustryOnce()
    {
        // Mỗi lời gọi GetComprehensiveDataAsync là 9 request HTTP song song sang 24hmoney chỉ để
        // đọc một chuỗi Industry. Mã đang gõ nằm luôn trong danh sách đang giữ, nên tra hai lần
        // là hai lượt fetch y hệt nhau — trên endpoint bị gọi lại mỗi nhịp debounce 500ms.
        SetupSectorForPlanScenario();

        await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);

        _comprehensiveProvider.Verify(
            p => p.GetComprehensiveDataAsync("VIC", It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task GetSectorExposureForPlanAsync_CalledTwice_ShouldServeIndustryFromCache()
    {
        // Nhãn ngành gần như không đổi, mà form gọi lại endpoint mỗi nhịp debounce. Không cache
        // thì mỗi lần ngừng gõ là (N+1) × 9 request ra provider ngoài.
        SetupSectorForPlanScenario();

        await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);
        await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);

        foreach (var symbol in new[] { "VIC", "VHM", "FPT" })
        {
            _comprehensiveProvider.Verify(
                p => p.GetComprehensiveDataAsync(symbol, It.IsAny<CancellationToken>()),
                Times.AtMostOnce(), $"ngành của {symbol} phải lấy từ cache ở lượt thứ hai");
        }
    }

    [Fact]
    public async Task GetSectorExposureForPlanAsync_WhenIndustryLookupFails_ShouldNotCacheTheFailure()
    {
        // Cache một lần tra lỗi (provider timeout) sẽ đóng băng mã đó thành "không rõ ngành"
        // suốt TTL, tức một lỗi mạng nhất thời làm hỏng cảnh báo tập trung trong nhiều giờ.
        SetupSectorForPlanScenario();
        _comprehensiveProvider
            .SetupSequence(p => p.GetComprehensiveDataAsync("VIC", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("provider timeout"))
            .ReturnsAsync(new ComprehensiveStockData { Company = new CompanyOverview { Industry = "Bất động sản" } });

        var first = await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);
        var second = await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);

        first.Sector.Should().BeNull("lượt đầu provider lỗi");
        second.Sector.Should().Be("Bất động sản", "lỗi không được cache — lượt sau phải tra lại");
    }

    [Fact]
    public async Task GetSectorExposureForPlanAsync_WhenOneHoldingHasNoPrice_ShouldSkipItNotThrow()
    {
        // PnLService ném ArgumentException khi không tra được giá (mã huỷ niêm yết, feed hụt).
        // Một mã như vậy không được làm sập cả endpoint — nhất là endpoint bị gọi lại mỗi nhịp
        // debounce 500ms trên form. Đường optimization đã bọc try/catch, đường này phải tương tự.
        SetupSectorForPlanScenario();
        _pnlService.Setup(s => s.CalculatePositionPnLAsync(
                PortfolioId, It.Is<StockSymbol>(ss => ss.Value == "VHM"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Price not available for symbol VHM"));

        var result = await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);

        // VHM bị bỏ qua nên ngành chỉ còn VIC 30tr trên tổng 120tr = 25%.
        result.CurrentPercent.Should().Be(25m);
        result.SameSectorSymbols.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSectorExposureForPlanAsync_ShouldExcludePlannedSymbolFromSameSectorSymbols()
    {
        SetupSectorForPlanScenario();

        var result = await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);

        result.SameSectorSymbols.Should().BeEquivalentTo(new[] { "VHM" });
    }

    [Fact]
    public async Task GetSectorExposureForPlanAsync_WhenTotalValueIsZero_ShouldReturnNullPercentsNotZero()
    {
        // spec Q4: 0% nói "chưa giữ gì ngành này", null nói "chưa tính được" — không gộp hai câu.
        SetupPortfolio(initialCapital: 0m);
        SetupRiskProfile(maxSectorExposure: 40m);
        SetupTrades("VIC");
        SetupPortfolioPnL(0m, 0m);
        SetupPositionPnL("VIC", 300, 100_000m, 30_000_000m);
        SetupIndustry("VIC", "Bất động sản");

        var result = await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);

        result.CurrentPercent.Should().BeNull();
        result.ProjectedPercent.Should().BeNull();
    }

    [Fact]
    public async Task GetSectorExposureForPlanAsync_WhenProviderHasNoIndustry_ShouldReturnNullSectorAndNullPercents()
    {
        SetupPortfolio();
        SetupRiskProfile(maxSectorExposure: 40m);
        SetupTrades("VIC");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 300, 100_000m, 30_000_000m);
        // Không dựng ngành — provider trả null.

        var result = await _sut.GetSectorExposureForPlanAsync(PortfolioId, "VIC", 9_000_000m);

        result.Sector.Should().BeNull();
        result.CurrentPercent.Should().BeNull();
        result.ProjectedPercent.Should().BeNull();
        result.LimitPercent.Should().Be(40m);
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_HighCorrelation_ReturnsCorrelationWarning()
    {
        // Arrange: 2 stocks with high correlation (setup price history to produce >0.7)
        SetupPortfolio();
        SetupRiskProfile();
        SetupTrades("VIC", "VHM");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 500, 100_000m, 50_000_000m);
        SetupPositionPnL("VHM", 500, 100_000m, 50_000_000m);
        SetupIndustry("VIC", "Bất động sản");
        SetupIndustry("VHM", "Bất động sản");
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();

        // Setup correlated price histories (both go up together)
        var baseDate = DateTime.UtcNow.AddDays(-90);
        var vicPrices = Enumerable.Range(0, 30).Select(i =>
            new StockPrice("VIC", baseDate.AddDays(i * 3), 95000 + i * 1000m, 96000 + i * 1000m,
                94000 + i * 1000m, 95500 + i * 1000m, 1000000, "Test")).ToList();
        var vhmPrices = Enumerable.Range(0, 30).Select(i =>
            new StockPrice("VHM", baseDate.AddDays(i * 3), 48000 + i * 500m, 49000 + i * 500m,
                47000 + i * 500m, 48500 + i * 500m, 500000, "Test")).ToList();

        _stockPriceRepo.Setup(r => r.GetBySymbolAsync("VIC", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vicPrices);
        _stockPriceRepo.Setup(r => r.GetBySymbolAsync("VHM", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vhmPrices);

        // Act
        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Assert
        result.CorrelationWarnings.Should().NotBeEmpty();
        result.CorrelationWarnings.Should().Contain(w =>
            (w.Symbol1 == "VIC" && w.Symbol2 == "VHM") ||
            (w.Symbol1 == "VHM" && w.Symbol2 == "VIC"));
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_WellDiversified_HighScore()
    {
        // Arrange: 4 positions in different sectors, all under limits
        SetupPortfolio();
        SetupRiskProfile(maxPositionSize: 30m, maxSectorExposure: 40m);
        SetupTrades("FPT", "VNM", "ACB", "GAS");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("FPT", 300, 80_000m, 24_000_000m);  // 24%
        SetupPositionPnL("VNM", 300, 90_000m, 27_000_000m);  // 27%
        SetupPositionPnL("ACB", 800, 30_000m, 24_000_000m);  // 24%
        SetupPositionPnL("GAS", 250, 100_000m, 25_000_000m); // 25%
        SetupIndustry("FPT", "Công nghệ");
        SetupIndustry("VNM", "Hàng tiêu dùng");
        SetupIndustry("ACB", "Ngân hàng");
        SetupIndustry("GAS", "Dầu khí");
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();
        SetupEmptyPriceHistory();

        // Act
        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Assert
        result.DiversificationScore.Should().BeGreaterThan(70m);
        result.ConcentrationAlerts.Should().BeEmpty();
        result.SectorExposures.Should().OnlyContain(s => !s.IsOverweight);
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_NoPositions_ReturnsEmptyResult()
    {
        // Arrange
        SetupPortfolio();
        SetupRiskProfile();
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade>());
        SetupPortfolioPnL(100_000_000m, 0m);
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();

        // Act
        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Assert
        result.Should().NotBeNull();
        result.ConcentrationAlerts.Should().BeEmpty();
        result.SectorExposures.Should().BeEmpty();
        result.CorrelationWarnings.Should().BeEmpty();
        result.DiversificationScore.Should().Be(0m);
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_NoRiskProfile_UsesDefaults()
    {
        // Arrange: No risk profile → use defaults (20%, 40%)
        SetupPortfolio();
        _riskProfileRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskProfile?)null);
        SetupTrades("VIC");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 500, 70_000m, 35_000_000m); // 35%
        SetupIndustry("VIC", "Bất động sản");
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();
        SetupEmptyPriceHistory();

        // Act
        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Assert — with default 20% limit, 35% should trigger alert
        result.ConcentrationAlerts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetPortfolioOptimizationAsync_GeneratesRecommendations()
    {
        // Arrange: concentrated position
        SetupPortfolio();
        SetupRiskProfile(maxPositionSize: 20m);
        SetupTrades("VIC");
        SetupPortfolioPnL(100_000_000m, 80_000_000m);
        SetupPositionPnL("VIC", 500, 70_000m, 35_000_000m);
        SetupIndustry("VIC", "Bất động sản");
        SetupEmptyStopLossTargets();
        SetupEmptySnapshots();
        SetupCapitalFlows();
        SetupEmptyPriceHistory();

        // Act
        var result = await _sut.GetPortfolioOptimizationAsync(PortfolioId);

        // Assert
        result.Recommendations.Should().NotBeEmpty();
    }

    // ─── Trailing Stop Alerts Tests ───────────────────────────────────

    [Fact]
    public async Task GetTrailingStopAlertsAsync_PriceNearTrailingStop_ReturnsDangerAlert()
    {
        // Arrange: trailing stop at 95k, current price at 96k → 1.04% distance → danger
        SetupPortfolio();
        var slTarget = new StopLossTarget("trade1", PortfolioId, UserId, "FPT", 100_000m, 90_000m, 120_000m);
        slTarget.UpdateTrailingStop(5m, 100_000m); // trailing stop at 95k

        _slTargetRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StopLossTarget> { slTarget });

        _stockPriceService.Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == "FPT")))
            .ReturnsAsync(new Money(96_000m, "VND"));

        // Act
        var result = await _sut.GetTrailingStopAlertsAsync(PortfolioId);

        // Assert
        result.Should().NotBeNull();
        result.Alerts.Should().HaveCount(1);
        result.Alerts[0].Symbol.Should().Be("FPT");
        result.Alerts[0].Severity.Should().Be("danger");
        result.Alerts[0].DistancePercent.Should().BeApproximately(1.04m, 0.1m);
    }

    [Fact]
    public async Task GetTrailingStopAlertsAsync_PriceRisen_SuggestsNewTrailingStop()
    {
        // Arrange: trailing stop at 95k (set when price was 100k), price now 110k
        // New trailing stop should be 110k * 0.95 = 104.5k
        SetupPortfolio();
        var slTarget = new StopLossTarget("trade1", PortfolioId, UserId, "FPT", 100_000m, 90_000m, 120_000m);
        slTarget.UpdateTrailingStop(5m, 100_000m); // trailing stop at 95k

        _slTargetRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StopLossTarget> { slTarget });

        _stockPriceService.Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == "FPT")))
            .ReturnsAsync(new Money(110_000m, "VND"));

        // Act
        var result = await _sut.GetTrailingStopAlertsAsync(PortfolioId);

        // Assert
        result.Alerts.Should().HaveCount(1);
        result.Alerts[0].ShouldUpdatePrice.Should().BeTrue();
        result.Alerts[0].NewTrailingStopPrice.Should().Be(104_500m);
    }

    [Fact]
    public async Task GetTrailingStopAlertsAsync_NoTrailingStops_ReturnsEmpty()
    {
        // Arrange: SL target without trailing stop
        SetupPortfolio();
        var slTarget = new StopLossTarget("trade1", PortfolioId, UserId, "FPT", 100_000m, 90_000m, 120_000m);
        // No UpdateTrailingStop call → TrailingStopPercent is null

        _slTargetRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StopLossTarget> { slTarget });

        // Act
        var result = await _sut.GetTrailingStopAlertsAsync(PortfolioId);

        // Assert
        result.Alerts.Should().BeEmpty();
        result.TotalActiveTrailingStops.Should().Be(0);
    }

    [Fact]
    public async Task GetTrailingStopAlertsAsync_TriggeredStop_IsExcluded()
    {
        // Arrange: triggered stop should not appear in alerts
        SetupPortfolio();
        var slTarget = new StopLossTarget("trade1", PortfolioId, UserId, "FPT", 100_000m, 90_000m, 120_000m);
        slTarget.UpdateTrailingStop(5m, 100_000m);
        slTarget.TriggerStopLoss();

        _slTargetRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StopLossTarget> { slTarget });

        // Act
        var result = await _sut.GetTrailingStopAlertsAsync(PortfolioId);

        // Assert
        result.Alerts.Should().BeEmpty();
        result.TotalActiveTrailingStops.Should().Be(0);
    }

    [Fact]
    public async Task GetTrailingStopAlertsAsync_PriceFarFromStop_ReturnsSafeSeverity()
    {
        // Arrange: trailing stop at 95k, price at 110k → ~13.6% → safe
        SetupPortfolio();
        var slTarget = new StopLossTarget("trade1", PortfolioId, UserId, "FPT", 100_000m, 90_000m, 120_000m);
        slTarget.UpdateTrailingStop(5m, 100_000m);

        _slTargetRepo.Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StopLossTarget> { slTarget });

        _stockPriceService.Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == "FPT")))
            .ReturnsAsync(new Money(110_000m, "VND"));

        // Act
        var result = await _sut.GetTrailingStopAlertsAsync(PortfolioId);

        // Assert
        result.Alerts[0].Severity.Should().Be("safe");
    }
}
