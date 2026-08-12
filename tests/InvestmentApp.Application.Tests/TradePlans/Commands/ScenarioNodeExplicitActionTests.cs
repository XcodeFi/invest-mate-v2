using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>
/// Hành động là quyết định nên không được có mặc định ngầm — node thiếu actionType từng im lặng
/// trở thành "bán 50% vị thế". Đơn vị đo (method của trailing) thì vẫn được phép bỏ trống.
/// </summary>
public class ScenarioNodeExplicitActionTests
{
    private static CreateTradePlanCommand Cmd(params ScenarioNodeDto[] nodes) => new()
    {
        UserId = "u1",
        Symbol = "ANV",
        EntryPrice = 20000m,
        StopLoss = 18000m,
        Target = 26000m,
        Quantity = 800,
        Direction = "Buy",
        MarketCondition = "Trending",
        ConfidenceLevel = 5,
        ExitStrategyMode = "Advanced",
        ScenarioNodes = nodes.ToList()
    };

    private static async Task<TradePlan> HandleAsync(CreateTradePlanCommand cmd)
    {
        TradePlan? saved = null;
        var repo = new Mock<ITradePlanRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()))
            .Callback<TradePlan, CancellationToken>((p, _) => saved = p);

        var handler = new CreateTradePlanCommandHandler(
            repo.Object, Mock.Of<ITradeRepository>(), Mock.Of<ICompanyDossierGate>());
        await handler.Handle(cmd, default);
        return saved!;
    }

    [Fact]
    public void Node_Without_ActionType_Fails_Validation_Naming_All_Valid_Values()
    {
        var cmd = Cmd(new ScenarioNodeDto
        {
            NodeId = "n1", Order = 1, Label = "x",
            ConditionType = ScenarioConditionType.PriceAbove, ConditionValue = 24000m
            // ActionType cố tình bỏ trống
        });

        var result = new CreateTradePlanCommandValidator().Validate(cmd);

        result.IsValid.Should().BeFalse("thiếu actionType không được im lặng thành SellPercent");
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("actionType bắt buộc"))
            .Which.ErrorMessage.Should().Contain("AddPosition");
    }

    [Fact]
    public void Node_With_Both_Decisions_Passes_Validation()
    {
        var cmd = Cmd(new ScenarioNodeDto
        {
            NodeId = "n1", Order = 1, Label = "x",
            ConditionType = ScenarioConditionType.PriceAbove, ConditionValue = 24000m,
            ActionType = ScenarioActionType.AddPosition, ActionValue = 50m
        });

        new CreateTradePlanCommandValidator().Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task TrailingStop_With_Only_TrailValue_Defaults_Method_To_Percentage()
    {
        var saved = await HandleAsync(Cmd(new ScenarioNodeDto
        {
            NodeId = "n1", Order = 1, Label = "Trailing 8%",
            ConditionType = ScenarioConditionType.PriceAbove, ConditionValue = 24000m,
            ActionType = ScenarioActionType.ActivateTrailingStop,
            TrailingStopConfig = new TrailingStopConfigDto { TrailValue = 8m }
        }));

        saved.ScenarioNodes[0].TrailingStopConfig!.Method.Should().Be(TrailingStopMethod.Percentage);
        saved.ScenarioNodes[0].TrailingStopConfig!.TrailValue.Should().Be(8m);
    }

    [Fact]
    public async Task TrailingStop_With_Explicit_Null_Method_Defaults_To_Percentage()
    {
        var saved = await HandleAsync(Cmd(new ScenarioNodeDto
        {
            NodeId = "n1", Order = 1, Label = "Trailing 8%",
            ConditionType = ScenarioConditionType.PriceAbove, ConditionValue = 24000m,
            ActionType = ScenarioActionType.ActivateTrailingStop,
            TrailingStopConfig = new TrailingStopConfigDto { TrailValue = 8m, Method = null }
        }));

        saved.ScenarioNodes[0].TrailingStopConfig!.Method.Should().Be(TrailingStopMethod.Percentage);
    }
}
