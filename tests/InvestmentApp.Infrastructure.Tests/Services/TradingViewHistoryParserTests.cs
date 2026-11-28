using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Hmoney;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Payload mẫu lấy từ https://24hmoney.vn/tradingview/history (UDF của biểu đồ web 24hmoney), đo ngày
/// 2026-11-28 mã FPT. Giá tính bằng nghìn đồng; v là số CỔ PHIẾU (ngày 2026-11-27 v = 12.431.700 trùng
/// accumulated_vol của /v1/ios/stock/trading-history, còn z của endpoint graph chỉ bằng 1/10 số đó).
/// </summary>
public class TradingViewHistoryParserTests
{
    // 2026-09-17 / 09-18 / 09-21 00:00 UTC
    private const string Ok =
        "{\"symbol\":\"FPT\",\"s\":\"ok\"," +
        "\"t\":[1789603200,1789689600,1789948800]," +
        "\"c\":[66.026,63.715,64.906],\"o\":[65.137,66.204,65.003]," +
        "\"h\":[66.026,66.47,65.297],\"l\":[64.515,63.715,63.537]," +
        "\"v\":[6644193,15529012,8488671]}";

    [Fact]
    public void Parse_PayloadOk_TraNenThatDaNhanGiaVoi1000()
    {
        var bars = TradingViewHistoryParser.Parse(Ok, "FPT")!;

        bars.Should().HaveCount(3);
        var b = bars[1];
        b.Symbol.Should().Be("FPT");
        b.Date.Should().Be(new DateTime(2026, 9, 18));
        b.Open.Should().Be(66204m);
        b.High.Should().Be(66470m);
        b.Low.Should().Be(63715m);
        b.Close.Should().Be(63715m);
        b.Volume.Should().Be(15_529_012L);
        b.High.Should().NotBe(b.Low, "nến thật có râu, khác nguồn cũ gán O=H=L=C");
    }

    [Fact]
    public void Parse_NenDuocDanhDauDaDieuChinh()
    {
        TradingViewHistoryParser.Parse(Ok, "FPT")!.Should().OnlyContain(b => b.IsAdjusted);
    }

    [Fact]
    public void Parse_SapTangDanTheoNgay()
    {
        var json = "{\"s\":\"ok\",\"t\":[1789948800,1789603200],\"c\":[2,1],\"o\":[2,1],\"h\":[2,1],\"l\":[2,1],\"v\":[20,10]}";

        var bars = TradingViewHistoryParser.Parse(json, "FPT")!;

        bars.Select(b => b.Close).Should().Equal(1000m, 2000m);
    }

    [Theory]
    [InlineData("{\"s\":\"no_data\"}")]
    [InlineData("{\"s\":\"error\",\"errmsg\":\"x\"}")]
    [InlineData("{\"s\":\"ok\",\"t\":[],\"c\":[],\"o\":[],\"h\":[],\"l\":[],\"v\":[]}")]
    [InlineData("{\"s\":\"ok\"}")]
    [InlineData("không phải json")]
    [InlineData("")]
    public void Parse_KhongDungDuocThiTraNull(string json)
    {
        TradingViewHistoryParser.Parse(json, "FPT").Should().BeNull();
    }

    [Fact]
    public void Parse_MangLechDoDai_BoCaPayload()
    {
        var json = "{\"s\":\"ok\",\"t\":[1789603200,1789689600],\"c\":[1,2],\"o\":[1,2],\"h\":[1,2],\"l\":[1],\"v\":[1,2]}";

        TradingViewHistoryParser.Parse(json, "FPT").Should().BeNull(
            "mảng lệch độ dài nghĩa là không biết nến nào ghép với nến nào; đoán là làm sai chỉ báo");
    }

    [Fact]
    public void Parse_NenCoGiaTriNull_BoNenDoGiuNenConLai()
    {
        var json = "{\"s\":\"ok\",\"t\":[1789603200,1789689600,1789948800]," +
                   "\"c\":[1,null,3],\"o\":[1,2,3],\"h\":[1,2,3],\"l\":[1,2,3],\"v\":[10,20,30]}";

        var bars = TradingViewHistoryParser.Parse(json, "FPT")!;

        bars.Select(b => b.Close).Should().Equal(1000m, 3000m);
    }

    [Fact]
    public void Parse_KhoiLuongNull_ThanhKhong()
    {
        var json = "{\"s\":\"ok\",\"t\":[1789603200],\"c\":[1],\"o\":[1],\"h\":[1],\"l\":[1],\"v\":[null]}";

        TradingViewHistoryParser.Parse(json, "FPT")!.Single().Volume.Should().Be(0);
    }

    [Fact]
    public void Parse_GiaDongCuaKhongDuong_BoNen()
    {
        var json = "{\"s\":\"ok\",\"t\":[1789603200,1789689600],\"c\":[0,2],\"o\":[0,2],\"h\":[0,2],\"l\":[0,2],\"v\":[1,2]}";

        TradingViewHistoryParser.Parse(json, "FPT")!.Should().ContainSingle();
    }

    [Fact]
    public void Parse_TatCaNenDeuHong_TraNullDeNguoiGoiDuPhong()
    {
        var json = "{\"s\":\"ok\",\"t\":[1789603200],\"c\":[null],\"o\":[1],\"h\":[1],\"l\":[1],\"v\":[1]}";

        TradingViewHistoryParser.Parse(json, "FPT").Should().BeNull();
    }

    [Theory]
    [InlineData("{\"s\":\"no_data\"}", true)]
    [InlineData("{\"s\":\"error\"}", false)]
    [InlineData("{\"s\":\"ok\",\"t\":[]}", false)]
    [InlineData("<html>Error</html>", false)]
    [InlineData("", false)]
    public void IsNoData_OnlyTrueForTheNoDataStatus(string json, bool expected)
    {
        TradingViewHistoryParser.IsNoData(json).Should().Be(expected);
    }
}
