using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.AbortTradePlan;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

/// <summary>
/// Test #18 (+ extras) trong plan Vin-discipline §Phase V1 Application.
/// Handler gọi entity.AbortWithThesisInvalidation, raise event, return result DTO.
/// </summary>
public class AbortTradePlanCommandHandlerTests
{
    private readonly Mock<ITradePlanRepository> _tradePlanRepo;
    private readonly AbortTradePlanCommandHandler _handler;

    public AbortTradePlanCommandHandlerTests()
    {
        _tradePlanRepo = new Mock<ITradePlanRepository>();
        _handler = new AbortTradePlanCommandHandler(_tradePlanRepo.Object);
    }

    private static TradePlan CreateReadyPlan(string userId = "user-1")
    {
        var plan = new TradePlan(userId, "VNM", "Buy",
            80_000m, 75_000m, 90_000m, 100,
            accountBalance: 100_000_000m);
        plan.SetThesis("Mua VNM vì EPS Q1 +22% YoY và ROE duy trì trên 20%, ngành sữa hồi phục");
        plan.SetInvalidationCriteria(new List<InvalidationRule>
        {
            new() { Trigger = InvalidationTrigger.EarningsMiss,
                    Detail = "BCTC Q1 EPS tăng trưởng dưới 20% YoY so với kỳ vọng" }
        });
        plan.MarkReady();
        return plan;
    }

    [Fact]
    public async Task Handle_ValidReadyPlan_ShouldAbortAndReturnResult()
    {
        var plan = CreateReadyPlan();
        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var command = new AbortTradePlanCommand
        {
            PlanId = plan.Id,
            UserId = "user-1",
            Trigger = "EarningsMiss",
            Detail = "BCTC Q1 lỗ lần đầu tiên sau 13 năm, LN giảm > 80% YoY so với dự phóng"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.PlanId.Should().Be(plan.Id);
        result.Status.Should().Be("Cancelled");
        result.Trigger.Should().Be("EarningsMiss");
        plan.Status.Should().Be(TradePlanStatus.Cancelled);
        _tradePlanRepo.Verify(r => r.UpdateAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PlanNotFound_ShouldThrowKeyNotFound()
    {
        _tradePlanRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradePlan?)null);

        var command = new AbortTradePlanCommand
        {
            PlanId = "missing",
            UserId = "user-1",
            Trigger = "Manual",
            Detail = "Tự nhận xét thesis sai sau review danh mục cuối tháng"
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_WrongUser_ShouldThrowUnauthorized()
    {
        var plan = CreateReadyPlan(userId: "user-1");
        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var command = new AbortTradePlanCommand
        {
            PlanId = plan.Id,
            UserId = "different-user",
            Trigger = "Manual",
            Detail = "Kẻ gian cố gắng abort plan của người khác"
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _tradePlanRepo.Verify(r => r.UpdateAsync(It.IsAny<TradePlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidTrigger_ShouldThrowArgumentException()
    {
        var plan = CreateReadyPlan();
        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var command = new AbortTradePlanCommand
        {
            PlanId = plan.Id,
            UserId = "user-1",
            Trigger = "NotARealTrigger",
            Detail = "Detail đủ dài nhưng trigger sai giá trị enum"
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid trigger*");
    }

    [Fact]
    public async Task Handle_TriggerCaseInsensitive_ShouldWork()
    {
        var plan = CreateReadyPlan();
        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var command = new AbortTradePlanCommand
        {
            PlanId = plan.Id,
            UserId = "user-1",
            Trigger = "earningsmiss",  // lowercase — vẫn phải parse được
            Detail = "BCTC Q1 lỗ lần đầu tiên sau 13 năm, LN giảm > 80% YoY so với dự phóng"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Trigger.Should().Be("EarningsMiss");
    }

    [Fact]
    public async Task Handle_ExecutedPlan_ShouldRaiseEventWithoutStatusChange()
    {
        var plan = CreateReadyPlan();
        plan.MarkInProgress();
        plan.Execute("trade-1");
        _tradePlanRepo.Setup(r => r.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var command = new AbortTradePlanCommand
        {
            PlanId = plan.Id,
            UserId = "user-1",
            Trigger = "TrendBreak",
            Detail = "Giá đóng cửa dưới MA200 kèm volume > 2× TB20 phiên, tín hiệu downtrend"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        // Executed plan giữ nguyên status (B3 fix)
        result.Status.Should().Be("Executed");
        result.TradeIdsAffected.Should().Contain("trade-1");
        plan.Status.Should().Be(TradePlanStatus.Executed);
    }
}
