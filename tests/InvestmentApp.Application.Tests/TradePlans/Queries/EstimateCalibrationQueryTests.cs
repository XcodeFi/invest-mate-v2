using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetEstimateCalibration;
using InvestmentApp.Application.TradePlans.Queries.GetKellyFromHistory;
using InvestmentApp.Application.TradePlans.Queries.GetStrategyExpectancy;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;
using MediatR;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

public class EstimateCalibrationQueryTests
{
    private const string Me = "u1";
    private readonly Mock<ITradePlanRepository> _plans = new();

    private GetEstimateCalibrationQueryHandler Sut() => new(_plans.Object);

    /// <summary>Kế hoạch mua đã review, ước lượng thắng <paramref name="winPercent"/>%, kết quả thật <paramref name="r"/> R (1R = 1.000).</summary>
    private static TradePlan Reviewed(double winPercent, double r, string? strategyId = "s1", string userId = Me,
        string direction = "Buy", bool withEstimate = true)
    {
        var plan = direction == "Buy"
            ? new TradePlan(userId, "VNM", "Buy", 100m, 90m, 130m, 100, strategyId: strategyId, thesis: "Luận điểm mẫu đủ dài cho test độ khớp")
            : new TradePlan(userId, "VNM", "Sell", 100m, 110m, 70m, 100, strategyId: strategyId, thesis: "Luận điểm mẫu đủ dài cho test độ khớp");
        if (withEstimate && direction == "Buy")
            plan.SetOutcomeScenarios(new[]
            {
                new OutcomeScenario { Label = "Thắng", ProbabilityPercent = (decimal)winPercent, ExitPrice = 130m },
                new OutcomeScenario { Label = "Thua", ProbabilityPercent = 100m - (decimal)winPercent, ExitPrice = 90m },
            });
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = (decimal)r * 1_000m });
        return plan;
    }

    private void Setup(params TradePlan[] plans)
        => _plans.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(plans.ToList());

    private Task<CalibrationResult> Run(string? strategyId = null)
        => Sut().Handle(new GetEstimateCalibrationQuery { UserId = Me, StrategyId = strategyId }, default);

    [Fact]
    public async Task calibration_only_caller_reviewed_locked_buy()
    {
        var unlocked = new TradePlan(Me, "VNM", "Buy", 100m, 90m, 130m, 100, thesis: "Luận điểm mẫu đủ dài cho test độ khớp");
        unlocked.SetOutcomeScenarios(new[]
        {
            new OutcomeScenario { Label = "Thắng", ProbabilityPercent = 60m, ExitPrice = 130m },
            new OutcomeScenario { Label = "Thua", ProbabilityPercent = 40m, ExitPrice = 90m },
        });
        unlocked.MarkReady();                         // có Estimate nhưng chưa khớp lệnh → chưa khoá
        var deleted = Reviewed(60, 2);
        deleted.SoftDelete();
        Setup(
            Reviewed(60, 2),                          // vào mẫu
            Reviewed(60, -1),                         // vào mẫu
            Reviewed(60, 2, userId: "someone-else"),  // của người khác
            Reviewed(60, 3, direction: "Sell"),       // lệnh bán
            Reviewed(60, 2, withEstimate: false),     // không có ước lượng
            unlocked, deleted);

        var result = await Run();

        result.N.Should().Be(2);
        result.Buckets[2].N.Should().Be(2, "ước 60% thuộc mức 55–70");
        result.Buckets[2].AvgEstimated.Should().Be(0.6m);
        result.Buckets[2].ActualWinRate.Should().Be(0.5m);
    }

    [Fact]
    public async Task unlocked_estimate_is_not_part_of_calibration()
    {
        var plan = new TradePlan(Me, "VNM", "Buy", 100m, 90m, 130m, 100, thesis: "Luận điểm mẫu đủ dài cho test độ khớp");
        plan.SetOutcomeScenarios(new[]
        {
            new OutcomeScenario { Label = "Thắng", ProbabilityPercent = 60m, ExitPrice = 130m },
            new OutcomeScenario { Label = "Thua", ProbabilityPercent = 40m, ExitPrice = 90m },
        });
        plan.MarkReady();
        plan.Estimate!.LockedAt.Should().BeNull();
        Setup(plan);

        (await Run()).N.Should().Be(0);
    }

    [Fact]
    public async Task a_reviewed_plan_whose_estimate_was_never_locked_is_left_out()
    {
        // Review cũ chưa có khoá ước lượng: có xác suất nhưng không đáng tin như "ước lượng lúc vào lệnh"
        var legacy = Reviewed(60, 2);
        legacy.Estimate!.LockedAt = null;
        Setup(Reviewed(60, 2), legacy);

        (await Run()).N.Should().Be(1);
    }

    [Fact]
    public async Task a_sell_plan_with_a_stored_locked_estimate_and_r_is_still_left_out()
    {
        // Chỉ bộ lọc Buy của handler chặn được ca này (RMath trả luôn RealizedR đã lưu)
        var sell = Reviewed(60, 2, direction: "Sell");
        sell.ReviewData!.RealizedR = 3m;
        sell.ReviewData.RiskBasis = "chot-ke-hoach";
        typeof(TradePlan).GetProperty("Estimate")!.SetValue(sell, new EstimateSnapshot
        {
            ImpliedWinProbability = 60m, LockedAt = DateTime.UtcNow, FeesIncluded = true
        });
        Setup(Reviewed(60, 2), sell);

        (await Run()).N.Should().Be(1);
    }

    [Fact]
    public async Task no_plan_has_an_estimate_gives_an_empty_result_not_an_error()
    {
        Setup(Reviewed(60, 2, withEstimate: false), Reviewed(60, -1, withEstimate: false));

        var result = await Run();

        result.N.Should().Be(0);
        result.Buckets.Should().HaveCount(4).And.OnlyContain(b => b.N == 0);
        result.Brier.Should().BeNull();
        result.Sufficient.Should().BeFalse();
    }

    [Fact]
    public async Task draws_are_left_out()
    {
        Setup(Reviewed(60, 0.05), Reviewed(60, 2));

        (await Run()).N.Should().Be(1);
    }

    [Fact]
    public async Task strategy_filter_keeps_only_that_strategy()
    {
        Setup(Reviewed(60, 2, strategyId: "s1"), Reviewed(60, 2, strategyId: "s2"), Reviewed(60, -1, strategyId: "s2"));

        (await Run("s1")).N.Should().Be(1);
        (await Run("s2")).N.Should().Be(2);
        (await Run()).N.Should().Be(3);
    }

    [Fact]
    public async Task foreign_strategy_id_returns_the_same_as_a_missing_one()
    {
        // Kế hoạch của người khác (strategy "s-other") không nằm trong danh sách của người gọi; id lạ cho cùng kết quả rỗng.
        Setup(Reviewed(60, 2, strategyId: "s1"));
        _plans.Setup(r => r.GetByUserIdAsync("user-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradePlan> { Reviewed(60, 2, strategyId: "s-other", userId: "user-2") });

        var foreign = await Run("s-other");
        var missing = await Run("khong-co");

        foreign.Should().BeEquivalentTo(missing);
        foreign.N.Should().Be(0);
        _plans.Verify(r => r.GetByUserIdAsync("user-2", It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class GetKellyFromHistoryQueryTests
{
    private readonly Mock<IMediator> _mediator = new();

    private GetKellyFromHistoryQueryHandler Sut() => new(_mediator.Object);

    private void Stats(ExpectancyStatsDto total, params ExpectancyStatsDto[] strategies)
        => _mediator.Setup(m => m.Send(It.Is<GetStrategyExpectancyQuery>(q => q.UserId == "u1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StrategyExpectancyDto { Total = total, Strategies = strategies.ToList() });

    private static ExpectancyStatsDto Group(string? id, int wins, int losses, decimal? avgWin = 2m, decimal? avgLoss = -1m)
        => new() { StrategyId = id, N = wins + losses, Wins = wins, Losses = losses, AvgWinR = avgWin, AvgLossR = avgLoss };

    [Fact]
    public async Task uses_the_total_when_no_strategy_is_given()
    {
        Stats(Group(null, 14, 10), Group("s1", 1, 1));

        var k = await Sut().Handle(new GetKellyFromHistoryQuery { UserId = "u1" }, default);

        k.N.Should().Be(24);
        k.HalfKellyCapped.Should().BeApproximately(0.04126m, 0.00001m);
    }

    [Fact]
    public async Task uses_the_strategy_group_when_given()
    {
        Stats(Group(null, 14, 10), Group("s1", 3, 2));

        var k = await Sut().Handle(new GetKellyFromHistoryQuery { UserId = "u1", StrategyId = "s1" }, default);

        k.N.Should().Be(5);
        k.HalfKellyCapped.Should().BeNull("n < 20");
    }

    [Fact]
    public async Task foreign_or_unknown_strategy_id_gives_the_same_as_a_strategy_with_no_trades()
    {
        Stats(Group(null, 14, 10));

        var foreign = await Sut().Handle(new GetKellyFromHistoryQuery { UserId = "u1", StrategyId = "cua-nguoi-khac" }, default);
        var missing = await Sut().Handle(new GetKellyFromHistoryQuery { UserId = "u1", StrategyId = "khong-co" }, default);

        foreign.Should().BeEquivalentTo(missing);
        foreign.N.Should().Be(0);
    }

    [Fact]
    public async Task asks_the_expectancy_query_for_the_caller_only()
    {
        Stats(Group(null, 0, 0));

        await Sut().Handle(new GetKellyFromHistoryQuery { UserId = "u1" }, default);

        _mediator.Verify(m => m.Send(It.Is<GetStrategyExpectancyQuery>(q => q.UserId != "u1"), It.IsAny<CancellationToken>()), Times.Never);
    }
}
