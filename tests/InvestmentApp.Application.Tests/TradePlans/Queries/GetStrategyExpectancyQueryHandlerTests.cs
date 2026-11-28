using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetStrategyExpectancy;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

public class GetStrategyExpectancyQueryHandlerTests
{
    private readonly Mock<ITradePlanRepository> _plans = new();
    private readonly Mock<IStrategyRepository> _strategies = new();

    private GetStrategyExpectancyQueryHandler Sut() => new(_plans.Object, _strategies.Object);

    /// <summary>Kế hoạch đã review với R mong muốn: 1R = 1.000 nên PnL = r × 1.000.</summary>
    private static TradePlan Reviewed(string? strategyId, double r, string direction = "Buy", string userId = "u1")
    {
        var plan = direction == "Buy"
            ? new TradePlan(userId, "VNM", "Buy", 100m, 90m, 130m, 100, strategyId: strategyId,
                thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R")
            : new TradePlan(userId, "VNM", "Sell", 100m, 110m, 70m, 100, strategyId: strategyId,
                thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");
        plan.MarkReady();
        plan.MarkInProgress();
        plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = (decimal)r * 1_000m });
        return plan;
    }

    private static TradePlan Unreviewed(string strategyId)
    {
        var plan = new TradePlan("u1", "VNM", "Buy", 100m, 90m, 130m, 100, strategyId: strategyId,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");
        plan.MarkReady();
        return plan;
    }

    private void Setup(IEnumerable<TradePlan> plans, params (string Id, string Name)[] strategies)
    {
        _plans.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(plans.ToList());
        var list = strategies.Select(s =>
        {
            var st = new Strategy("u1", s.Name, "mô tả", "vào", "ra", "rủi ro", "Swing", "Trending");
            st.GetType().GetProperty("Id")!.SetValue(st, s.Id);
            return st;
        }).ToList();
        _strategies.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(list);
    }

    [Fact]
    public async Task only_reviewed_buy_plans_of_caller_count()
    {
        Setup(new[]
        {
            Reviewed("s1", 2),
            Reviewed("s1", -1),
            Reviewed("s1", 3, direction: "Sell"),   // lệnh bán: bỏ qua
            Unreviewed("s1"),                        // chưa review: bỏ qua
            Reviewed("s1", 5, userId: "someone-else"),   // không phải của người gọi
        }, ("s1", "Chiến lược 1"));

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Total.N.Should().Be(2);
        result.Strategies.Single().N.Should().Be(2);
    }

    [Fact]
    public async Task open_review_excluded_from_expectancy()
    {
        // Review còn cổ phiếu chưa bán: R chỉ của phần đã bán, không đại diện cả chiến dịch → không vào mẫu.
        var open = Reviewed("s1", 5);
        open.ReviewData!.OpenQuantity = 500m;
        var closed = Reviewed("s1", 1);
        closed.ReviewData!.OpenQuantity = 0m;           // review mới đã đóng hết
        var legacy = Reviewed("s1", 3);                 // review cũ: OpenQuantity null → giữ nguyên
        Setup(new[] { open, closed, legacy }, ("s1", "Breakout"));

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Total.N.Should().Be(2);
        result.Total.ExpectancyR.Should().Be(2m);       // (1 + 3) / 2, không có 5
    }

    [Fact]
    public async Task oversold_review_is_skipped_and_its_null_r_never_becomes_a_zero_sample()
    {
        // Hình dạng b4ad04c1: PnL khớp được 0, RealizedR null, có cờ bán vượt. Suy lại từ PnL sẽ ra 0R giả (một mẫu "hoà").
        var oversold = Reviewed("s1", 0);
        oversold.ReviewData!.RealizedR = null;
        oversold.ReviewData.OversoldQuantity = 14_500m;
        oversold.ReviewData.OpenQuantity = 0m;
        oversold.ReviewData.Flags = new List<string> { "thieu-lenh-mua-lien-ket" };
        Setup(new[] { Reviewed("s1", 2), oversold }, ("s1", "Breakout"));

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Total.N.Should().Be(1, "mẫu bán vượt không được tính, kể cả như hoà");
        result.Total.Draws.Should().Be(0);
        result.Total.ExpectancyR.Should().Be(2m);
    }

    [Fact]
    public async Task sell_plan_with_a_stored_realized_r_is_still_excluded()
    {
        // RMath không tính được R cho lệnh bán, nhưng một review đã LƯU RealizedR thì RMath trả luôn giá trị đó.
        // Chỉ bộ lọc Buy của handler chặn được ca này.
        var sell = Reviewed("s1", 3, direction: "Sell");
        sell.ReviewData!.RealizedR = 3m;
        sell.ReviewData.RiskBasis = "chot-ke-hoach";
        Setup(new[] { Reviewed("s1", 1), sell }, ("s1", "Breakout"));

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Total.N.Should().Be(1, "chiến dịch lệnh bán chưa hỗ trợ trong thống kê R");
        result.Total.ExpectancyR.Should().Be(1m);
    }

    [Fact]
    public async Task soft_deleted_plans_are_excluded()
    {
        var deleted = Reviewed("s1", 4);
        deleted.SoftDelete();
        Setup(new[] { Reviewed("s1", 1), deleted }, ("s1", "Breakout"));

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Total.N.Should().Be(1);
    }

    [Fact]
    public async Task another_users_plan_with_a_stored_realized_r_is_excluded_even_if_the_repository_returns_it()
    {
        var foreign = Reviewed("s1", 9, userId: "someone-else");
        foreign.ReviewData!.RealizedR = 9m;
        Setup(new[] { Reviewed("s1", 1), foreign }, ("s1", "Breakout"));

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Total.N.Should().Be(1);
        result.Total.ExpectancyR.Should().Be(1m);
    }

    [Fact]
    public async Task groups_by_strategy_with_none_bucket()
    {
        Setup(new[]
        {
            Reviewed("s1", 2), Reviewed("s1", -1),
            Reviewed("s2", 1),
            Reviewed(null, -1), Reviewed(null, 2), Reviewed(null, 0),
        }, ("s1", "Breakout"), ("s2", "Pullback"));

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Strategies.Select(s => (s.StrategyName, s.N)).Should().BeEquivalentTo(new[]
        {
            ("Breakout", 2), ("Pullback", 1), ("Không gắn chiến lược", 3),
        });
        result.Strategies.Single(s => s.StrategyId == null).StrategyName.Should().Be("Không gắn chiến lược");
        result.Total.N.Should().Be(6, "nhóm không gắn chiến lược vẫn tính trong tổng");
    }

    [Fact]
    public async Task strategy_that_no_longer_exists_keeps_its_own_bucket()
    {
        Setup(new[] { Reviewed("gone", 1) });

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Strategies.Single().StrategyId.Should().Be("gone");
        result.Strategies.Single().StrategyName.Should().Be("Chiến lược không còn tồn tại");
    }

    [Fact]
    public async Task pf_null_when_no_losses()
    {
        Setup(new[] { Reviewed("s1", 1), Reviewed("s1", 2) }, ("s1", "Breakout"));

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Strategies.Single().ProfitFactorR.Should().BeNull();
        result.Strategies.Single().ExpectancyR.Should().Be(1.5m);
    }

    [Fact]
    public async Task n0_all_null()
    {
        Setup(Array.Empty<TradePlan>());

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Strategies.Should().BeEmpty();
        result.Total.N.Should().Be(0);
        result.Total.ExpectancyR.Should().BeNull();
        result.Total.WinRate.Should().BeNull();
    }

    [Fact]
    public async Task plans_without_a_computable_r_are_skipped_not_counted_as_zero()
    {
        var noRisk = new TradePlan("u1", "VNM", "Buy", 100m, 100m, 130m, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");
        noRisk.MarkReady();
        noRisk.MarkInProgress();
        noRisk.Execute("t1");
        noRisk.MarkReviewed(new CampaignReviewData { PnLAmount = 500m });
        Setup(new[] { Reviewed("s1", 2), noRisk });

        var result = await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        result.Total.N.Should().Be(1, "không có 1R thì không có R: loại khỏi mẫu chứ không tính 0R");
    }

    [Fact]
    public async Task caller_id_is_passed_to_both_repositories()
    {
        Setup(Array.Empty<TradePlan>());

        await Sut().Handle(new GetStrategyExpectancyQuery { UserId = "u1" }, CancellationToken.None);

        _plans.Verify(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
        _strategies.Verify(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
        _plans.VerifyNoOtherCalls();
    }
}
