using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Tests.MarketClosures;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Common;

public class SettlementCalculatorTests
{
    private const int T2 = SettlementOptions.DefaultSessions;

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
        SettlementCalculator.SettlementDateOf(DateTime.Parse(tradeDate), Closures2026, T2)
            .Should().Be(DateTime.Parse(expected));
    }

    [Fact]
    public void Ban_thu_Nam_thi_tien_ve_thu_Hai_vi_vat_qua_cuoi_tuan()
    {
        // 2026-06-11 là thứ Năm, không có lễ quanh đó: T+1 = thứ Sáu 12/6, T+2 = thứ Hai 15/6.
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 6, 11), Closures2026, T2)
            .Should().Be(new DateTime(2026, 6, 15));
    }

    [Fact]
    public void Khong_co_ngay_nghi_nao_thi_chi_bo_cuoi_tuan()
    {
        // Cùng ngày 12/02 nhưng tập ngày nghỉ rỗng: T+1 = 13/2 (thứ Sáu), T+2 = 16/2 (thứ Hai).
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 2, 12), NoClosures, T2)
            .Should().Be(new DateTime(2026, 2, 16));
    }

    [Fact]
    public void Ban_ngay_truoc_dot_le_dai_thi_ngay_ve_nhay_qua_ca_dot()
    {
        // Bán 28/4 (thứ Ba). T+1 = 29/4 (thứ Tư). 30/4 + 1/5 nghỉ, 2-3/5 cuối tuần → T+2 = 4/5.
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 4, 28), Closures2026, T2)
            .Should().Be(new DateTime(2026, 5, 4));
    }

    // --- Chu kỳ thanh toán đổi được: T+1 và T+0 ---

    [Fact]
    public void T1_thi_tien_ve_ngay_phien_giao_dich_ke_tiep()
    {
        // Thứ Năm 11/6/2026 → T+1 là thứ Sáu 12/6 (T+2 mới là thứ Hai 15/6).
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 6, 11), Closures2026, sessions: 1)
            .Should().Be(new DateTime(2026, 6, 12));
    }

    [Fact]
    public void T1_van_vat_qua_ca_dot_nghi_Tet()
    {
        // Bán thứ Sáu 13/02: 14-15 cuối tuần, 16-20 nghỉ Tết, 21-22 cuối tuần
        // → phiên kế tiếp là thứ Hai 23/02. Ngày nghỉ vẫn được đếm khi chu kỳ ngắn đi.
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 2, 13), Closures2026, sessions: 1)
            .Should().Be(new DateTime(2026, 2, 23));
    }

    [Fact]
    public void T0_thi_tien_ve_ngay_trong_ngay_nen_khong_bao_gio_cho()
    {
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 6, 11), Closures2026, sessions: 0)
            .Should().Be(new DateTime(2026, 6, 11));

        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m) };

        var (amount, last) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 11), Closures2026, sessions: 0);

        amount.Should().Be(0m);
        last.Should().BeNull();
    }

    [Fact]
    public void So_phien_am_bi_chan_chu_khong_lang_le_thanh_T0()
    {
        // `while (counted < sessions)` không chạy lần nào với số âm, nên không chặn thì
        // một cấu hình sai biến thành T+0 trong im lặng — mất hẳn khái niệm chờ về.
        var act = () => SettlementCalculator.SettlementDateOf(
            new DateTime(2026, 6, 11), Closures2026, sessions: -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("sessions");
    }

    // --- Tiền chờ về ---

    [Fact]
    public void Tien_cho_ve_tru_phi_va_thue()
    {
        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m, fee: 30_000m, tax: 20_000m) };

        var (amount, _) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 12), Closures2026, T2);

        amount.Should().Be(1_000m * 20_000m - 30_000m - 20_000m);
    }

    [Fact]
    public void Lenh_da_ve_thi_khong_con_tinh_la_cho_ve()
    {
        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m) };

        // asOf = đúng ngày về (15/6) → đã về, không còn chờ.
        var (amount, last) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 15), Closures2026, T2);

        amount.Should().Be(0m);
        last.Should().BeNull();
    }

    [Fact]
    public void Lenh_mua_khong_tao_tien_cho_ve()
    {
        var trades = new[] { Buy("2026-06-11", 1_000m, 20_000m) };

        var (amount, _) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 12), Closures2026, T2);

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
            trades, new DateTime(2026, 6, 12), Closures2026, T2);

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

        var (pending, _) = SettlementCalculator.PendingSellProceeds(trades, asOf, Closures2026, T2);
        var settled = totalSold - pending;

        settled.Should().Be(500m * 15_000m - 10_000m);
        (settled + pending).Should().Be(totalSold);
    }

    // `Trade.TradeDate` KHÔNG có [BsonDateTimeOptions(Kind = DateTimeKind.Utc)], nên driver
    // coi DateTime Unspecified là giờ local rồi quy về UTC: người dùng ghi lệnh ngày 13/08 thì
    // Mongo lưu 2026-08-12T17:00:00Z. Đọc `.Date` trần ra 12/08 — lùi một ngày, và tiền bị
    // đánh dấu đã về sớm một ngày. Quy về NGÀY LỊCH VN thì cả hai dạng lưu đều ra đúng ngày.
    [Theory]
    [InlineData("2026-08-12T17:00:00Z")]  // nửa đêm giờ VN của 13/08, dạng thực tế trong DB
    [InlineData("2026-08-13T00:00:00Z")]  // nửa đêm UTC thật, dạng "đúng" nếu ai đó sửa entity
    public void Ngay_phien_lay_theo_lich_VN_nen_hai_dang_luu_cho_cung_ket_qua(string stored)
    {
        var trade = new Trade("p1", "HHV", TradeType.SELL, 100m, 10_000m, 0m, 0m,
            DateTime.Parse(stored, null, System.Globalization.DateTimeStyles.AdjustToUniversal));

        var (amount, last) = SettlementCalculator.PendingSellProceeds(
            new[] { trade }, new DateTime(2026, 8, 13), NoClosures, T2);

        amount.Should().Be(1_000_000m);
        // 13/08/2026 là thứ Năm: T+1 = thứ Sáu 14/08, T+2 = thứ Hai 17/08.
        last.Should().Be(new DateTime(2026, 8, 17));
    }

    [Fact]
    public void Lenh_ghi_hom_qua_van_con_cho_ve_chu_khong_bi_coi_la_da_ve()
    {
        // Ca thật gặp khi verify: bán 12/08 (thứ Tư) → T+2 = thứ Sáu 14/08. Hôm nay 13/08
        // thì vẫn PHẢI đang chờ. Trước khi vá, ngày lưu lùi về 11/08 nên nó bị tính là đã về.
        var trade = new Trade("p1", "HHV", TradeType.SELL, 1_000m, 21_000m, 30_000m, 21_000m,
            new DateTime(2026, 8, 11, 17, 0, 0, DateTimeKind.Utc));  // = 12/08 giờ VN

        var (amount, last) = SettlementCalculator.PendingSellProceeds(
            new[] { trade }, new DateTime(2026, 8, 13), NoClosures, T2);

        amount.Should().Be(1_000m * 21_000m - 30_000m - 21_000m);
        last.Should().Be(new DateTime(2026, 8, 14));
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
            trades, VietnamDate.Today(utc), Closures2026, T2);

        amount.Should().Be(0m, "tiền về ngày 15/6, mà giờ VN đã sang 15/6");
    }
}
