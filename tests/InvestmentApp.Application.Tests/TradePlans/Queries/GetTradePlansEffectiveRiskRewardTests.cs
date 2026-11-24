using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

public class GetTradePlansEffectiveRiskRewardTests
{
    private static TradePlan Plan(string direction, decimal entry, decimal stop, decimal target, decimal? storedRr)
        => new("u1", "VNM", direction, entry, stop, target, 100,
            thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R", riskRewardRatio: storedRr);

    private static async Task<List<TradePlanDto>> Read(params TradePlan[] plans)
    {
        var repo = new Mock<ITradePlanRepository>();
        repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(plans.ToList());
        var handler = new GetTradePlansQueryHandler(repo.Object);
        return (await handler.Handle(new GetTradePlansQuery { UserId = "u1" }, CancellationToken.None)).ToList();
    }

    [Fact]
    public async Task dto_exposes_computed_rr_and_keeps_the_stored_value_untouched()
    {
        var dto = (await Read(Plan("Buy", 100m, 90m, 120m, storedRr: 5m))).Single();

        dto.EffectiveRiskReward.Should().Be(2.0m);
        dto.RiskRewardComputed.Should().BeTrue();
        dto.RiskRewardRatio.Should().Be(5m, "giá trị người dùng đã gõ không bị ghi đè hay giấu");
    }

    [Fact]
    public async Task dto_falls_back_to_stored_rr_for_sell_plans()
    {
        var dto = (await Read(Plan("Sell", 100m, 110m, 80m, storedRr: 3m))).Single();

        dto.EffectiveRiskReward.Should().Be(3m);
        dto.RiskRewardComputed.Should().BeFalse();
    }

    [Fact]
    public async Task dto_effective_rr_null_when_not_computable_and_not_stored()
    {
        var dto = (await Read(Plan("Buy", 100m, 100m, 120m, storedRr: null))).Single();

        dto.EffectiveRiskReward.Should().BeNull();
        dto.RiskRewardComputed.Should().BeFalse();
    }
}
