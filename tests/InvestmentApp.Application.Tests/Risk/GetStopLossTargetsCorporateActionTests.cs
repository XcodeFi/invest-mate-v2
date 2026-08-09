using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetStopLossTargets;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.Risk;

/// <summary>
/// Endpoint stop-loss-targets phải điều chỉnh ngưỡng giống RiskCalculationService,
/// không thì hai bề mặt đọc cùng một StopLossTarget ra hai con số khác nhau.
/// </summary>
public class GetStopLossTargetsCorporateActionTests
{
    private readonly Mock<IStopLossTargetRepository> _targets = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ICorporateActionRepository> _actions = new();

    private static readonly DateTime SetAt = new(2026, 1, 5);
    private static readonly DateTime Ex = new(2026, 6, 10);

    private GetStopLossTargetsQueryHandler Sut() =>
        new(_targets.Object, _portfolios.Object, _actions.Object);

    private static StopLossTarget HpgTarget()
    {
        // Giá vào 25.000, cắt lỗ 22.000, mục tiêu 40.000 — đặt trước ngày GDKHQ
        var target = new StopLossTarget("t1", "p1", "u1", "HPG", 25_000m, 22_000m, 40_000m);
        typeof(StopLossTarget).GetProperty(nameof(StopLossTarget.UpdatedAt))!.SetValue(target, SetAt);
        return target;
    }

    private void Setup(params CorporateAction[] actions)
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u1", "Danh mục", 0));
        _targets.Setup(r => r.GetByPortfolioIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { HpgTarget() });
        _actions.Setup(r => r.GetByPortfolioIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions);
    }

    [Fact]
    public async Task SauCoTucCoPhieu_NguongDuocDieuChinh()
    {
        Setup(CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null));

        var result = await Sut().Handle(
            new GetStopLossTargetsQuery { PortfolioId = "p1", UserId = "u1" }, CancellationToken.None);

        var item = result.Items.Single();
        item.EntryPrice.Should().BeApproximately(19_230.77m, 0.01m);   // 25.000 / 1,3
        item.StopLossPrice.Should().BeApproximately(16_923.08m, 0.01m); // 22.000 / 1,3
        item.TargetPrice.Should().BeApproximately(30_769.23m, 0.01m);   // 40.000 / 1,3
        // Tỷ lệ R:R bất biến vì cả ba mốc cùng chia một hệ số:
        // (40.000 − 25.000) / (25.000 − 22.000) = 5, trước và sau điều chỉnh đều vậy
        item.RiskRewardRatio.Should().BeApproximately(5m, 0.01m);
    }

    [Fact]
    public async Task KhongCoSuKienQuyen_ThiGiuNguyenGiaGoc()
    {
        Setup();

        var result = await Sut().Handle(
            new GetStopLossTargetsQuery { PortfolioId = "p1", UserId = "u1" }, CancellationToken.None);

        var item = result.Items.Single();
        item.EntryPrice.Should().Be(25_000m);
        item.StopLossPrice.Should().Be(22_000m);
        item.TargetPrice.Should().Be(40_000m);
    }

    [Fact]
    public async Task DanhMucCuaNguoiKhac_ThiNem()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u2", "Danh mục", 0));

        var act = () => Sut().Handle(
            new GetStopLossTargetsQuery { PortfolioId = "p1", UserId = "u1" }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
