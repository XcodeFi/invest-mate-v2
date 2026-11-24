using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.DeleteTradePlan;
using InvestmentApp.Application.TradePlans.Commands.ExecuteLot;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.TradePlans.Commands.TriggerExitTarget;
using InvestmentApp.Application.TradePlans.Commands.TriggerScenarioNode;
using InvestmentApp.Application.TradePlans.Commands.UpdateReviewLessons;
using InvestmentApp.Application.TradePlans.Commands.UpdateStopLoss;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>
/// Thao tác trên kế hoạch không có và trên kế hoạch không thuộc người gọi phải cho CÙNG một phản hồi
/// (KeyNotFoundException, cùng thông điệp → 404), và không được ghi gì.
/// </summary>
public class PlanNotFoundUniformTests
{
    private const string Me = "user-1";
    private const string Other = "user-2";

    private readonly Mock<ITradePlanRepository> _plans = new();
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<IStrategyRepository> _strategies = new();
    private readonly Mock<ICampaignReviewService> _review = new();
    private readonly Mock<ICompanyDossierGate> _gate = new();

    private static TradePlan PlanOf(string userId) => new(userId, "VNM", "Buy", 80_000m, 75_000m, 90_000m, 100,
        thesis: "Mua mẫu cho test phản hồi thống nhất — luận điểm đủ dài tối thiểu");

    public static IEnumerable<object[]> Handlers() => new[]
    {
        new object[] { "ReviewTradePlan" },
        new object[] { "DeleteTradePlan" },
        new object[] { "ExecuteLot" },
        new object[] { "TriggerExitTarget" },
        new object[] { "TriggerScenarioNode" },
        new object[] { "UpdateReviewLessons" },
        new object[] { "UpdateStopLoss" },
        new object[] { "UpdateTradePlan" },
    };

    private Task Run(string handler, string planId, string userId)
    {
        var ct = default(CancellationToken);
        return handler switch
        {
            "ReviewTradePlan" => new ReviewTradePlanCommandHandler(_plans.Object, _trades.Object, _portfolios.Object, _review.Object)
                .Handle(new ReviewTradePlanCommand { PlanId = planId, UserId = userId }, ct),
            "DeleteTradePlan" => new DeleteTradePlanCommandHandler(_plans.Object)
                .Handle(new DeleteTradePlanCommand { Id = planId, UserId = userId }, ct),
            "ExecuteLot" => new ExecuteLotCommandHandler(_plans.Object, _trades.Object, _portfolios.Object)
                .Handle(new ExecuteLotCommand { PlanId = planId, UserId = userId }, ct),
            "TriggerExitTarget" => new TriggerExitTargetCommandHandler(_plans.Object, _trades.Object, _portfolios.Object)
                .Handle(new TriggerExitTargetCommand { PlanId = planId, UserId = userId }, ct),
            "TriggerScenarioNode" => new TriggerScenarioNodeCommandHandler(_plans.Object, _trades.Object, _portfolios.Object)
                .Handle(new TriggerScenarioNodeCommand { PlanId = planId, UserId = userId }, ct),
            "UpdateReviewLessons" => new UpdateReviewLessonsCommandHandler(_plans.Object)
                .Handle(new UpdateReviewLessonsCommand { PlanId = planId, UserId = userId }, ct),
            "UpdateStopLoss" => new UpdateStopLossCommandHandler(_plans.Object)
                .Handle(new UpdateStopLossCommand { PlanId = planId, UserId = userId }, ct),
            "UpdateTradePlan" => new UpdateTradePlanCommandHandler(_plans.Object, _portfolios.Object, _strategies.Object, _gate.Object)
                .Handle(new UpdateTradePlanCommand { Id = planId, UserId = userId }, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(handler))
        };
    }

    private static async Task<Exception> Thrown(Func<Task> act)
    {
        try { await act(); }
        catch (Exception e) { return e; }
        throw new Xunit.Sdk.XunitException("Không ném lỗi nào");
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public async Task Missing_and_foreign_plans_give_the_same_exception_type_and_message(string handler)
    {
        var foreign = PlanOf(Other);
        _plans.Setup(r => r.GetByIdAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);
        _plans.Setup(r => r.GetByIdAsync("khong-co", It.IsAny<CancellationToken>())).ReturnsAsync((TradePlan?)null);

        var foreignEx = await Thrown(() => Run(handler, foreign.Id, Me));
        var missingEx = await Thrown(() => Run(handler, "khong-co", Me));

        foreignEx.Should().BeOfType<KeyNotFoundException>(handler);
        missingEx.Should().BeOfType<KeyNotFoundException>(handler);
        // cùng khuôn thông điệp, chỉ khác id người gọi đã biết
        foreignEx.Message.Replace(foreign.Id, "X").Should().Be(missingEx.Message.Replace("khong-co", "X"), handler);
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public async Task Foreign_plan_writes_nothing_and_touches_no_other_repository(string handler)
    {
        var foreign = PlanOf(Other);
        _plans.Setup(r => r.GetByIdAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);

        await Thrown(() => Run(handler, foreign.Id, Me));

        _plans.Verify(r => r.GetByIdAsync(foreign.Id, It.IsAny<CancellationToken>()), Times.Once);
        _plans.VerifyNoOtherCalls();            // không UpdateAsync / DeleteAsync
        _trades.VerifyNoOtherCalls();
        _portfolios.VerifyNoOtherCalls();
        _strategies.VerifyNoOtherCalls();
        _review.VerifyNoOtherCalls();
        _gate.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public async Task Missing_plan_writes_nothing(string handler)
    {
        _plans.Setup(r => r.GetByIdAsync("khong-co", It.IsAny<CancellationToken>())).ReturnsAsync((TradePlan?)null);

        await Thrown(() => Run(handler, "khong-co", Me));

        _plans.Verify(r => r.GetByIdAsync("khong-co", It.IsAny<CancellationToken>()), Times.Once);
        _plans.VerifyNoOtherCalls();
        _trades.VerifyNoOtherCalls();
        _portfolios.VerifyNoOtherCalls();
    }
}
