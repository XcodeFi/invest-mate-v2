using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>
/// Unit tests for the controller actions only — verifies that each endpoint delegates to
/// the right service and shapes the response correctly. The OIDC + allowlist enforcement
/// is wired via attribute + middleware and is exercised manually in Phase 5 against real
/// Cloud Scheduler (full TestServer + JWKS faking is too heavy for the value it adds).
/// </summary>
public class InternalJobsControllerTests
{
    private readonly Mock<IPriceSnapshotJobService> _priceJob = new();
    private readonly Mock<ISnapshotService> _snapshotService = new();
    private readonly Mock<ICurrencyService> _currencyService = new();
    private readonly Mock<IScenarioEvaluationService> _scenarioService = new();
    private readonly InternalJobsController _sut;

    public InternalJobsControllerTests()
    {
        _sut = new InternalJobsController(
            _priceJob.Object,
            _snapshotService.Object,
            _currencyService.Object,
            _scenarioService.Object,
            NullLogger<InternalJobsController>.Instance);
    }

    [Fact]
    public async Task RunPrices_DelegatesToService_AndReturnsResult()
    {
        var expected = new PriceSnapshotJobResult(
            SymbolsFetched: 5, PricesPersisted: 5, IndicesUpdated: 1,
            StopLossTriggered: 0, TargetsTriggered: 0);
        _priceJob.Setup(s => s.RunAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.RunPrices(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
        _priceJob.Verify(s => s.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSnapshot_DelegatesToService_AndReturns200()
    {
        _snapshotService.Setup(s => s.TakeAllSnapshotsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.RunSnapshot(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _snapshotService.Verify(s => s.TakeAllSnapshotsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunExchangeRate_DelegatesToCurrencyService_AndReturns200()
    {
        _currencyService.Setup(s => s.RefreshRatesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.RunExchangeRate(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _currencyService.Verify(s => s.RefreshRatesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunScenarioEval_DelegatesToService_AndReturnsCount()
    {
        _scenarioService.Setup(s => s.EvaluateAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScenarioEvaluationResult>
            {
                new() { TradePlanId = "tp-1", NodeId = "n-1", UserId = "u", Symbol = "FPT", ActionType = "ENTRY", Label = "x" },
                new() { TradePlanId = "tp-2", NodeId = "n-2", UserId = "u", Symbol = "VNM", ActionType = "EXIT",  Label = "y" }
            });

        var result = await _sut.RunScenarioEval(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(new { TriggeredCount = 2 });
    }
}
