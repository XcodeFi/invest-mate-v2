using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetCorrelation;
using InvestmentApp.Application.Risk.Queries.GetDrawdown;
using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Application.Risk.Queries.GetPortfolioRisk;
using InvestmentApp.Application.Risk.Queries.GetRiskBudget;
using InvestmentApp.Application.Risk.Queries.GetRiskProfile;
using InvestmentApp.Application.Risk.Queries.GetSectorExposureForPlan;
using InvestmentApp.Application.Risk.Queries.GetStopLossTargets;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using InvestmentApp.Application.TradePlans.Queries.GetEstimateCalibration;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioAdvisories;
using InvestmentApp.Application.TradePlans.Queries.GetStrategyExpectancy;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class RiskToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetPortfolioRisk_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<PortfolioRiskSummary, GetPortfolioRiskQuery>(
            _mediator, out var sent, new PortfolioRiskSummary());
        await RiskTools.GetPortfolioRisk("p1", _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.PortfolioId.Should().Be("p1");
    }

    [Fact]
    public async Task GetStopLossTargets_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<StopLossTargetsDto, GetStopLossTargetsQuery>(
            _mediator, out var sent, new StopLossTargetsDto());
        await RiskTools.GetStopLossTargets("p2", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-2");
        sent()!.PortfolioId.Should().Be("p2");
    }

    [Fact]
    public async Task GetTrailingStopAlerts_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<TrailingStopAlertsResult, GetTrailingStopAlertsQuery>(
            _mediator, out var sent, new TrailingStopAlertsResult());
        await RiskTools.GetTrailingStopAlerts("p3", _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-3");
        sent()!.PortfolioId.Should().Be("p3");
    }

    [Fact]
    public async Task GetVolatilitySizing_PassesAllFourArgs_AndUserId()
    {
        McpTestContext.Capture<VolatilitySizingResult, GetVolatilitySizingForPlanQuery>(
            _mediator, out var sent, new VolatilitySizingResult { Symbol = "FPT" });

        await RiskTools.GetVolatilitySizing("p5", "FPT", 100_000m, 250,
            _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);

        sent()!.UserId.Should().Be("u-5", "danh tính lấy từ ApiKey, không từ tham số");
        sent()!.PortfolioId.Should().Be("p5");
        sent()!.Symbol.Should().Be("FPT");
        sent()!.EntryPrice.Should().Be(100_000m);
        sent()!.Quantity.Should().Be(250);
    }

    [Fact]
    public async Task GetScenarioAdvisories_SetsUserId()
    {
        McpTestContext.Capture<List<ScenarioAdvisory>, GetScenarioAdvisoriesQuery>(
            _mediator, out var sent, new List<ScenarioAdvisory>());
        await RiskTools.GetScenarioAdvisories(_mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-4");
    }

    // ---------- đợt 2: rủi ro mở rộng. UserId CHỈ lấy từ người gọi, không bao giờ từ tham số tool ----------

    [Fact]
    public async Task GetRiskProfile_SetsUserId_FromTheCaller_AndPortfolioId()
    {
        McpTestContext.Capture<RiskProfileDto?, GetRiskProfileQuery>(_mediator, out var sent, null);
        await RiskTools.GetRiskProfile("p-rp", _mediator.Object, McpTestContext.WithUser("u-rp"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-rp");
        sent()!.PortfolioId.Should().Be("p-rp");
    }

    [Fact]
    public async Task GetDrawdown_SetsUserId_FromTheCaller_AndPortfolioId()
    {
        McpTestContext.Capture<DrawdownResult, GetDrawdownQuery>(_mediator, out var sent, new DrawdownResult());
        await RiskTools.GetDrawdown("p-dd", _mediator.Object, McpTestContext.WithUser("u-dd"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-dd");
        sent()!.PortfolioId.Should().Be("p-dd");
    }

    [Fact]
    public async Task GetCorrelation_SetsUserId_FromTheCaller_AndPortfolioId()
    {
        McpTestContext.Capture<CorrelationMatrix, GetCorrelationQuery>(_mediator, out var sent, new CorrelationMatrix());
        await RiskTools.GetCorrelation("p-co", _mediator.Object, McpTestContext.WithUser("u-co"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-co");
        sent()!.PortfolioId.Should().Be("p-co");
    }

    [Fact]
    public async Task GetPortfolioOptimization_SetsUserId_FromTheCaller_AndPortfolioId()
    {
        McpTestContext.Capture<PortfolioOptimizationResult, GetPortfolioOptimizationQuery>(
            _mediator, out var sent, new PortfolioOptimizationResult());
        await RiskTools.GetPortfolioOptimization("p-op", _mediator.Object, McpTestContext.WithUser("u-op"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-op");
        sent()!.PortfolioId.Should().Be("p-op");
    }

    [Fact]
    public async Task GetRiskBudget_SetsUserId_FromTheCaller_AndPortfolioId()
    {
        McpTestContext.Capture<RiskBudgetStatus, GetRiskBudgetQuery>(_mediator, out var sent, new RiskBudgetStatus());
        await RiskTools.GetRiskBudget("p-rb", _mediator.Object, McpTestContext.WithUser("u-rb"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-rb");
        sent()!.PortfolioId.Should().Be("p-rb");
    }

    [Fact]
    public async Task GetSectorExposure_SetsUserId_FromTheCaller_AndPassesSymbolAndAddValue()
    {
        McpTestContext.Capture<SectorExposureForPlan, GetSectorExposureForPlanQuery>(
            _mediator, out var sent, new SectorExposureForPlan());
        await RiskTools.GetSectorExposure("p-se", " hpg ", 9_000_000m, _mediator.Object,
            McpTestContext.WithUser("u-se"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-se");
        sent()!.PortfolioId.Should().Be("p-se");
        sent()!.Symbol.Should().Be("HPG");
        sent()!.AddValue.Should().Be(9_000_000m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSectorExposure_BlankSymbol_IsRefusedWithAClearMessage(string symbol)
    {
        var act = async () => await RiskTools.GetSectorExposure("p-se", symbol, 1m, _mediator.Object,
            McpTestContext.WithUser("u-se"), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("symbol");
        _mediator.Verify(m => m.Send(It.IsAny<GetSectorExposureForPlanQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStrategyExpectancy_SetsUserId_FromTheCaller()
    {
        McpTestContext.Capture<StrategyExpectancyDto, GetStrategyExpectancyQuery>(
            _mediator, out var sent, new StrategyExpectancyDto());
        await RiskTools.GetStrategyExpectancy(_mediator.Object, McpTestContext.WithUser("u-ex"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-ex");
    }

    [Fact]
    public void CorrelationMatrix_SerializesThroughTheMcpJsonOptions_EvenWithTheTwoDimensionalMatrix()
    {
        // decimal[,] là kiểu System.Text.Json không hỗ trợ: nếu kết quả tool chứa nó mà không có cách xử lý thì tools/call nổ ở bước trả kết quả,
        // đúng lúc agent hỏi tương quan của một danh mục có nhiều mã.
        var matrix = new CorrelationMatrix
        {
            PortfolioId = "p1",
            Symbols = new List<string> { "FPT", "VNM" },
            Matrix = new decimal[,] { { 1m, 0.4m }, { 0.4m, 1m } },
            Pairs = new List<CorrelationPair> { new() { Symbol1 = "FPT", Symbol2 = "VNM", Correlation = 0.4m } }
        };

        var act = () => System.Text.Json.JsonSerializer.Serialize(matrix, ModelContextProtocol.McpJsonUtilities.DefaultOptions);

        act.Should().NotThrow();
        act().Should().Contain("FPT").And.Contain("0.4");
    }

    [Fact]
    public async Task GetEstimateCalibration_SetsUserId_FromTheCaller_AndPassesStrategyId()
    {
        McpTestContext.Capture<InvestmentApp.Domain.Services.CalibrationResult, GetEstimateCalibrationQuery>(
            _mediator, out var sent, InvestmentApp.Domain.Services.Calibration.Compute(Array.Empty<(decimal, decimal)>()));
        await RiskTools.GetEstimateCalibration(_mediator.Object, McpTestContext.WithUser("u-cal"), CancellationToken.None, "s-1");
        sent()!.UserId.Should().Be("u-cal");
        sent()!.StrategyId.Should().Be("s-1");
    }

    [Fact]
    public async Task GetEstimateCalibration_StrategyId_DefaultsToNull()
    {
        McpTestContext.Capture<InvestmentApp.Domain.Services.CalibrationResult, GetEstimateCalibrationQuery>(
            _mediator, out var sent, InvestmentApp.Domain.Services.Calibration.Compute(Array.Empty<(decimal, decimal)>()));
        await RiskTools.GetEstimateCalibration(_mediator.Object, McpTestContext.WithUser("u-cal"), CancellationToken.None);
        sent()!.StrategyId.Should().BeNull();
    }

    [Fact]
    public void CalibrationResult_SerializesThroughTheMcpJsonOptions_WithoutBrierBelowTheThreshold()
    {
        var result = InvestmentApp.Domain.Services.Calibration.Compute(new List<(decimal, decimal)> { (0.6m, 2m), (0.6m, -1m) });

        var json = System.Text.Json.JsonSerializer.Serialize(result, ModelContextProtocol.McpJsonUtilities.DefaultOptions);

        json.Should().Contain("\"n\":2").And.Contain("\"buckets\"");
        json.Should().NotContain("brier", "dưới 15 mẫu thì không có brier (vắng mặt), không phải 0");
        json.Should().Contain("\"sufficient\":false");
    }
}
