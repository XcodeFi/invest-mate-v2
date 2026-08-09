using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Application.Tests.Common;

public class CorporateActionAdjusterTests
{
    private static readonly DateTime Ex = new(2026, 6, 10);
    private static readonly DateTime SetAt = new(2026, 1, 5);

    [Fact]
    public void CoTucCoPhieu30PhanTram_ChiaGiaNguongChoHeSo()
    {
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null) };

        var adjusted = CorporateActionAdjuster.AdjustPrice(22_000m, SetAt, actions);

        adjusted.Should().BeApproximately(16_923.08m, 0.01m);
    }

    [Fact]
    public void CoTucTienMat_TruSoTienTrenMoiCoPhieu()
    {
        var actions = new[] { CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, null, 5m) };

        var adjusted = CorporateActionAdjuster.AdjustPrice(55_000m, SetAt, actions);

        adjusted.Should().Be(54_500m); // trừ theo số trước thuế
    }

    [Fact]
    public void SuKienTruocKhiDatNguong_ThiKhongApDung()
    {
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null) };

        var adjusted = CorporateActionAdjuster.AdjustPrice(22_000m, new DateTime(2026, 7, 1), actions);

        adjusted.Should().Be(22_000m);
    }

    [Fact]
    public void CungNgay_TienMatTruoc_RoiMoiChiaHeSo()
    {
        var actions = new[]
        {
            CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null),
            CorporateAction.CashDividend("p1", "u1", "HPG", 5m, Ex, null, 5m)
        };

        var adjusted = CorporateActionAdjuster.AdjustPrice(30_000m, SetAt, actions);

        adjusted.Should().BeApproximately(22_692.31m, 0.01m); // (30.000 − 500) / 1,3
    }

    [Fact]
    public void KhongCoSuKien_ThiGiuNguyen()
    {
        var adjusted = CorporateActionAdjuster.AdjustPrice(22_000m, SetAt, Array.Empty<CorporateAction>());
        adjusted.Should().Be(22_000m);
    }
}
