using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Application.Tests.Common;

public class PositionBuilderTests
{
    private static readonly DateTime Ex = new(2026, 6, 10);
    private static readonly DateTime Settled = new(2026, 7, 20);
    private static readonly DateTime Far = new(2026, 12, 31);

    private static Trade Buy(string symbol, decimal qty, decimal price, DateTime date)
        => new("p1", symbol, TradeType.BUY, qty, price, tradeDate: date);

    private static Trade Sell(string symbol, decimal qty, decimal price, DateTime date)
        => new("p1", symbol, TradeType.SELL, qty, price, tradeDate: date);

    [Fact]
    public void CoTucCoPhieu30PhanTram_ChuaVe_ThiVaoChoVe_VaGiamGiaVon()
    {
        var trades = new[] { Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)) };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.SettledQuantity.Should().Be(1000);
        pos.PendingQuantity.Should().Be(300);
        pos.TotalQuantity.Should().Be(1300);
        pos.TotalCost.Should().Be(25_000_000);
        pos.AverageCost.Should().BeApproximately(19_230.77m, 0.01m);
    }

    [Fact]
    public void CoTucCoPhieu_DaXacNhanVe_ThiChuyenSangDaVe()
    {
        var trades = new[] { Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)) };
        var action = CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled);
        action.MarkSettled(Settled);

        var pos = PositionBuilder.Build(trades, new[] { action }, asOf: Far).Single();

        pos.SettledQuantity.Should().Be(1300);
        pos.PendingQuantity.Should().Be(0);
    }

    [Fact]
    public void CoTucTienMat_KhongDoiGiaVon_VaGhiNhanSauThue()
    {
        var trades = new[] { Buy("SAB", 1000, 55_000, new DateTime(2026, 1, 5)) };
        var actions = new[] { CorporateAction.CashDividend("p1", "u1", "SAB", 5m, Ex, Settled, 5m) };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.AverageCost.Should().Be(55_000);
        pos.PendingDividend.Should().Be(475_000);
        pos.DividendNet.Should().Be(0);
    }

    [Fact]
    public void ChiaTach1An2_NhanDoiSoLuong_ChiaDoiGiaVon()
    {
        var trades = new[] { Buy("VNM", 500, 60_000, new DateTime(2026, 1, 5)) };
        var action = CorporateAction.StockSplit("p1", "u1", "VNM", 1, 2, Ex, Settled);
        action.MarkSettled(Settled);

        var pos = PositionBuilder.Build(trades, new[] { action }, asOf: Far).Single();

        pos.TotalQuantity.Should().Be(1000);
        pos.AverageCost.Should().Be(30_000);
        pos.TotalCost.Should().Be(30_000_000);
    }

    [Fact]
    public void CungNgayGDKHQ_TienMatTinhTrenSoLuongCu_RoiMoiNhanHeSo()
    {
        var trades = new[] { Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)) };
        var actions = new[]
        {
            CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled),
            CorporateAction.CashDividend("p1", "u1", "HPG", 5m, Ex, Settled, 5m)
        };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.PendingDividend.Should().Be(475_000); // 1000 CP, không phải 1300
        pos.TotalQuantity.Should().Be(1300);
    }

    [Fact]
    public void CoPhieuLe_LamTronXuong()
    {
        var trades = new[] { Buy("HPG", 137, 25_000, new DateTime(2026, 1, 5)) };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.TotalQuantity.Should().Be(178); // 137 × 1,3 = 178,1
    }

    [Fact]
    public void BanBotTruocNgayGDKHQ_ChiPhanConGiuDuocHuongQuyen()
    {
        var trades = new[]
        {
            Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)),
            Sell("HPG", 500, 30_000, new DateTime(2026, 3, 1))
        };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: new DateTime(2026, 6, 15)).Single();

        pos.TotalQuantity.Should().Be(650);
        pos.RealizedPnL.Should().Be(2_500_000); // 500 × (30.000 − 25.000)
    }

    [Fact]
    public void SuKienTruocGiaoDichDauTien_ThiBoQua()
    {
        var trades = new[] { Buy("HPG", 1000, 25_000, new DateTime(2026, 7, 1)) };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: Far).Single();

        pos.TotalQuantity.Should().Be(1000);
    }

    [Fact]
    public void KhongCoSuKien_ThiKetQuaGiongHetTinhTuTradeTho()
    {
        var trades = new[]
        {
            Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)),
            Sell("HPG", 400, 28_000, new DateTime(2026, 3, 1))
        };

        var pos = PositionBuilder.Build(trades, Array.Empty<CorporateAction>(), asOf: Far).Single();

        pos.TotalQuantity.Should().Be(600);
        pos.AverageCost.Should().Be(25_000);
        pos.RealizedPnL.Should().Be(1_200_000);
    }

    [Fact]
    public void BanHetTruocNgayGDKHQ_ThiGiaVonBangKhong_KhongChiaChoKhong()
    {
        var trades = new[]
        {
            Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)),
            Sell("HPG", 1000, 30_000, new DateTime(2026, 3, 1))
        };
        var actions = new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, Settled) };

        var pos = PositionBuilder.Build(trades, actions, asOf: Far).Single();

        pos.TotalQuantity.Should().Be(0);
        pos.AverageCost.Should().Be(0);
    }

    [Fact]
    public void PhiVaThue_TinhVaoGiaVonKhiMua()
    {
        var trades = new[]
        {
            new Trade("p1", "HPG", TradeType.BUY, 1000, 25_000, fee: 50_000, tax: 0,
                tradeDate: new DateTime(2026, 1, 5))
        };

        var pos = PositionBuilder.Build(trades, Array.Empty<CorporateAction>(), asOf: Far).Single();

        pos.TotalCost.Should().Be(25_050_000);
        pos.AverageCost.Should().Be(25_050m);
    }

    [Fact]
    public void GiaoDichSauAsOf_ThiKhongTinh()
    {
        var trades = new[]
        {
            Buy("HPG", 1000, 25_000, new DateTime(2026, 1, 5)),
            Buy("HPG", 500, 30_000, new DateTime(2026, 8, 1))
        };

        var pos = PositionBuilder.Build(trades, Array.Empty<CorporateAction>(), asOf: new DateTime(2026, 6, 1)).Single();

        pos.TotalQuantity.Should().Be(1000);
    }
}
