using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Tests.MarketClosures;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Common;

public class SettlementCalculatorTests
{
    private static readonly IReadOnlySet<DateOnly> Closures2026 =
        Vn2026Closures.Dates.Select(DateOnly.Parse).ToHashSet();

    private static readonly IReadOnlySet<DateOnly> NoClosures = new HashSet<DateOnly>();

    private static Trade Sell(string date, decimal qty, decimal price, decimal fee = 0m, decimal tax = 0m)
        => new("p1", "HHV", TradeType.SELL, qty, price, fee, tax, DateTime.Parse(date));

    private static Trade Buy(string date, decimal qty, decimal price)
        => new("p1", "HHV", TradeType.BUY, qty, price, 0m, 0m, DateTime.Parse(date));

    // Hai ca dưới lấy thẳng từ thông báo của HOSE: nghỉ Tết 16–20/02/2026 nên giao dịch
    // ngày 12/02 thanh toán 23/02, và giao dịch ngày 13/02 thanh toán 24/02.
    [Theory]
    [InlineData("2026-02-12", "2026-02-23")]
    [InlineData("2026-02-13", "2026-02-24")]
    public void Golden_Tet_2026_khop_thong_bao_HOSE(string tradeDate, string expected)
    {
        SettlementCalculator.SettlementDateOf(DateTime.Parse(tradeDate), Closures2026)
            .Should().Be(DateTime.Parse(expected));
    }

    [Fact]
    public void Ban_thu_Nam_thi_tien_ve_thu_Hai_vi_vat_qua_cuoi_tuan()
    {
        // 2026-06-11 là thứ Năm, không có lễ quanh đó: T+1 = thứ Sáu 12/6, T+2 = thứ Hai 15/6.
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 6, 11), Closures2026)
            .Should().Be(new DateTime(2026, 6, 15));
    }

    [Fact]
    public void Khong_co_ngay_nghi_nao_thi_chi_bo_cuoi_tuan()
    {
        // Cùng ngày 12/02 nhưng tập ngày nghỉ rỗng: T+1 = 13/2 (thứ Sáu), T+2 = 16/2 (thứ Hai).
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 2, 12), NoClosures)
            .Should().Be(new DateTime(2026, 2, 16));
    }

    [Fact]
    public void Ban_ngay_truoc_dot_le_dai_thi_ngay_ve_nhay_qua_ca_dot()
    {
        // Bán 28/4 (thứ Ba). T+1 = 29/4 (thứ Tư). 30/4 + 1/5 nghỉ, 2-3/5 cuối tuần → T+2 = 4/5.
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 4, 28), Closures2026)
            .Should().Be(new DateTime(2026, 5, 4));
    }

    [Fact]
    public void Tien_cho_ve_tru_phi_va_thue()
    {
        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m, fee: 30_000m, tax: 20_000m) };

        var (amount, _) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 12), Closures2026);

        amount.Should().Be(1_000m * 20_000m - 30_000m - 20_000m);
    }

    [Fact]
    public void Lenh_da_ve_thi_khong_con_tinh_la_cho_ve()
    {
        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m) };

        // asOf = đúng ngày về (15/6) → đã về, không còn chờ.
        var (amount, last) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 15), Closures2026);

        amount.Should().Be(0m);
        last.Should().BeNull();
    }

    [Fact]
    public void Lenh_mua_khong_tao_tien_cho_ve()
    {
        var trades = new[] { Buy("2026-06-11", 1_000m, 20_000m) };

        var (amount, _) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 12), Closures2026);

        amount.Should().Be(0m);
    }

    [Fact]
    public void Ngay_ve_hien_thi_la_moc_xa_nhat_trong_cac_lenh_dang_cho()
    {
        var trades = new[]
        {
            Sell("2026-06-11", 100m, 10_000m),  // về 15/6
            Sell("2026-06-12", 100m, 10_000m)   // về 16/6
        };

        var (_, last) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 12), Closures2026);

        last.Should().Be(new DateTime(2026, 6, 16));
    }

    [Fact]
    public void Bat_bien_da_ve_cong_cho_ve_bang_TotalSold()
    {
        var trades = new[]
        {
            Sell("2026-05-04", 500m, 15_000m, fee: 10_000m),   // đã về từ lâu
            Sell("2026-06-11", 300m, 21_000m, fee: 8_000m),    // còn chờ
            Buy("2026-06-01", 1_000m, 12_000m)                 // không liên quan
        };
        var asOf = new DateTime(2026, 6, 12);

        var totalSold = trades
            .Where(t => t.TradeType == TradeType.SELL)
            .Sum(t => t.Quantity * t.Price - t.Fee - t.Tax);

        var (pending, _) = SettlementCalculator.PendingSellProceeds(trades, asOf, Closures2026);
        var settled = totalSold - pending;

        settled.Should().Be(500m * 15_000m - 10_000m);
        (settled + pending).Should().Be(totalSold);
    }

    [Fact]
    public void Ban_ghi_cu_khong_con_la_nua_dem_van_tinh_dung()
    {
        // Mongo có thể trả về 17:00 hôm trước cho ngày lưu sai Kind. Phần .Date phải cắt.
        var trade = new Trade("p1", "HHV", TradeType.SELL, 100m, 10_000m, 0m, 0m,
            new DateTime(2026, 6, 11, 23, 45, 0));

        var (amount, last) = SettlementCalculator.PendingSellProceeds(
            new[] { trade }, new DateTime(2026, 6, 12), Closures2026);

        amount.Should().Be(1_000_000m);
        last.Should().Be(new DateTime(2026, 6, 15));
    }

    [Fact]
    public void Hom_nay_tinh_theo_gio_Viet_Nam_khong_phai_UTC()
    {
        // 01:00 giờ VN ngày 15/6 = 18:00 UTC ngày 14/6. Dùng ngày UTC sẽ ra 14/6 và
        // giữ tiền ở trạng thái chờ về thêm một ngày.
        var utc = new DateTime(2026, 6, 14, 18, 0, 0, DateTimeKind.Utc);

        VietnamDate.Today(utc).Should().Be(new DateTime(2026, 6, 15));

        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m) };
        var (amount, _) = SettlementCalculator.PendingSellProceeds(
            trades, VietnamDate.Today(utc), Closures2026);

        amount.Should().Be(0m, "tiền về ngày 15/6, mà giờ VN đã sang 15/6");
    }
}
