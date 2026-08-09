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
    public void MocNgay_LaNuaDemUtc_DeMongoRoundTripKhongLechMuiGio()
    {
        // Mongo ghi DateTime local rồi đọc lại thành UTC. Nếu ExDate là nửa đêm giờ local
        // (+07) thì xuống DB thành 17:00Z hôm trước, đọc lên không còn nửa đêm và mọi
        // so sánh ở biên lệch đúng 1 ngày — tiền cổ tức tính sai số lượng.
        var local = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Local);
        var action = CorporateAction.CashDividend("p1", "u1", "SAB", 5m, local,
            new DateTime(2026, 7, 10, 9, 30, 0, DateTimeKind.Local), 5m);
        action.MarkSettled(new DateTime(2026, 7, 12, 15, 45, 0, DateTimeKind.Local));

        action.ExDate.Kind.Should().Be(DateTimeKind.Utc);
        action.ExDate.TimeOfDay.Should().Be(TimeSpan.Zero);
        action.SettlementDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
        action.SettlementDate!.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
        action.SettledAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        action.SettledAt!.Value.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void MarkSettled_TruocNgayGDKHQ_ThiNem()
    {
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100m, 130m, Ex, null);
        var act = () => action.MarkSettled(Ex.AddDays(-1));
        act.Should().Throw<ArgumentException>();
    }
}
