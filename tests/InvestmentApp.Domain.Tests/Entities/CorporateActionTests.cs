using FluentAssertions;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Domain.Tests.Entities;

public class CorporateActionTests
{
    private static readonly DateTime Ex = new(2026, 6, 10);

    [Fact]
    public void CashDividend_QuyDoiPhanTramTheoMenhGia()
    {
        var action = CorporateAction.CashDividend(
            "p1", "u1", "sab", percentOfPar: 5m, exDate: Ex,
            settlementDate: new DateTime(2026, 7, 10), taxRatePercent: 5m);

        action.Symbol.Should().Be("SAB");
        action.AmountPerShare.Should().Be(500m);
        action.NetPerShare.Should().Be(475m);
        action.Multiplier.Should().Be(1m);
        action.DeclaredText.Should().Be("5%");
    }

    [Fact]
    public void StockDividend_TinhMultiplierTuTyLeTong()
    {
        var action = CorporateAction.StockDividend(
            "p1", "u1", "HPG", ratioOld: 100m, ratioNew: 130m, exDate: Ex, settlementDate: null);

        action.Multiplier.Should().Be(1.3m);
        action.AmountPerShare.Should().BeNull();
    }

    [Fact]
    public void StockSplit_TinhMultiplier()
    {
        var action = CorporateAction.StockSplit("p1", "u1", "VNM", 1m, 2m, Ex, null);
        action.Multiplier.Should().Be(2m);
    }

    [Fact]
    public void CashDividend_SoTienKhongDuong_ThiNem()
    {
        var act = () => CorporateAction.CashDividend("p1", "u1", "SAB", 0m, Ex, null, 5m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StockDividend_TyLeMoiKhongLonHonCu_ThiNem()
    {
        var act = () => CorporateAction.StockDividend("p1", "u1", "HPG", 100m, 100m, Ex, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NgayVeTruocNgayGDKHQ_ThiNem()
    {
        var act = () => CorporateAction.StockDividend(
            "p1", "u1", "HPG", 100m, 130m, Ex, Ex.AddDays(-1));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkSettled_GhiNgayVeThucTe()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100m, 130m, Ex, null);
        action.MarkSettled(new DateTime(2026, 7, 20));

        action.SettledAt.Should().Be(new DateTime(2026, 7, 20));
    }

    [Fact]
    public void MarkSettled_TruocNgayGDKHQ_ThiNem()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100m, 130m, Ex, null);
        var act = () => action.MarkSettled(Ex.AddDays(-1));
        act.Should().Throw<ArgumentException>();
    }
}
