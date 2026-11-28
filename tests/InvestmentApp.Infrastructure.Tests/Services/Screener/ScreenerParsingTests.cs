using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Screener;

namespace InvestmentApp.Infrastructure.Tests.Services.Screener;

/// <summary>
/// Ghim đơn vị và hình dạng của hai nguồn công khai bằng mẫu thật (đo 2026-11-28, rút gọn).
/// technical-filter: giá tính bằng NGHÌN đồng, vốn hoá và lợi nhuận bằng TỶ đồng, khối lượng bằng cổ phiếu,
/// rs1m/rs3m/rs52w là XẾP HẠNG PHÂN VỊ 1–99 trên toàn thị trường (không phải tỉ suất sinh lời), có thể null.
/// </summary>
public class ScreenerParsingTests
{
    private const string TechnicalFilterSample = """
    {"message":"success","status":200,"data":{"total_page":4,"data":[
      {"id":1,"symbol":"AAA","company_name":"Công ty cổ phần Nhựa An Phát Xanh","floor":"HOSE","pb4Q":0.429359,"pe4Q":4.2686,"roe":10.35,"roa":4.33,"market_cap":2480.58,"the_beta":0.84,"profit_after_tax":372.91,"icb_group":"1000","avg_trading_vol_10":869694,"match_price_10":6.82,"rs1m":93,"rs3m":89,"rs52w":60,"match_price":6.3},
      {"id":2123403,"symbol":"AAN","floor":"HOSE","pb4Q":1.3353,"pe4Q":20.3447,"roe":6.59,"roa":3.55,"market_cap":1301.43,"the_beta":0.0,"profit_after_tax":0.0,"icb_group":"3000","avg_trading_vol_10":142111,"match_price_10":15.2,"rs1m":null,"rs3m":null,"rs52w":null,"match_price":14.2},
      {"id":3,"symbol":"HHH","floor":"HNX","pe4Q":9.5,"roe":15.0,"market_cap":1500.0,"profit_after_tax":100.0,"icb_group":"8000","avg_trading_vol_10":500000,"match_price_10":20.0,"rs1m":50,"rs3m":51,"rs52w":52,"match_price":21.0}
    ]},"execute_time_ms":76}
    """;

    [Fact]
    public void technical_filter_parses_units()
    {
        var page = TechnicalFilterParser.Parse(TechnicalFilterSample);

        page.TotalPages.Should().Be(4);
        page.RawCount.Should().Be(3);
        page.InvalidCount.Should().Be(0);
        var aaa = page.Rows.Single(r => r.Symbol == "AAA");
        aaa.Floor.Should().Be("HOSE");
        aaa.PriceVnd.Should().Be(6_300m, "match_price tính bằng nghìn đồng: 6,3 → 6.300 đ");
        aaa.Price10Vnd.Should().Be(6_820m);
        aaa.MarketCapBn.Should().Be(2480.58m, "market_cap đã là tỷ đồng");
        aaa.ProfitAfterTaxBn.Should().Be(372.91m, "profit_after_tax đã là tỷ đồng");
        aaa.AvgVolume10.Should().Be(869_694m, "khối lượng tính bằng cổ phiếu");
        aaa.Roe.Should().Be(10.35m);
        aaa.Pe4Q.Should().Be(4.2686m);
        aaa.Beta.Should().Be(0.84m);
        aaa.IcbGroup.Should().Be("1000");
        aaa.Rs1m.Should().Be(93);
        aaa.Rs3m.Should().Be(89);
        aaa.Rs52w.Should().Be(60);
        // 869.694 cp × 6.820 đ = 5,93 tỷ đồng/phiên
        aaa.AdtvBn.Should().BeApproximately(5.9314m, 0.001m);
    }

    [Fact]
    public void rs_fields_are_percentile_ranks_and_a_null_rs_row_is_kept_with_null_ranks()
    {
        var page = TechnicalFilterParser.Parse(TechnicalFilterSample);

        var aan = page.Rows.Single(r => r.Symbol == "AAN");
        aan.Rs1m.Should().BeNull();
        aan.Rs3m.Should().BeNull("rs null nghĩa là thiếu dữ liệu xếp hạng, không phải 0");
        aan.Rs52w.Should().BeNull();
        aan.Beta.Should().BeNull("the_beta = 0.0 là thiếu dữ liệu của nguồn (223/1827 dòng), không phải beta bằng 0");
        page.Rows.Where(r => r.Rs3m.HasValue).Select(r => r.Rs3m!.Value).Should().OnlyContain(v => v >= 1 && v <= 99,
            "đây là xếp hạng phân vị 1–99, không có giá trị âm hay phần trăm");
    }

    [Fact]
    public void rows_with_unusable_core_fields_are_counted_invalid_and_dropped()
    {
        const string json = """
        {"status":200,"data":{"total_page":1,"data":[
          {"symbol":"OK1","floor":"HOSE","market_cap":2000.0,"match_price":10.0,"avg_trading_vol_10":1,"match_price_10":10.0},
          {"symbol":"NOP","floor":"HOSE","market_cap":2000.0,"match_price":0.0},
          {"symbol":"NOC","floor":"HOSE","market_cap":0.0,"match_price":10.0},
          {"symbol":"HUGE","floor":"HOSE","market_cap":99999999.0,"match_price":10.0},
          {"symbol":"NOFL","floor":"???","market_cap":2000.0,"match_price":10.0},
          {"symbol":"","floor":"HNX","market_cap":2000.0,"match_price":10.0}
        ]}}
        """;

        var page = TechnicalFilterParser.Parse(json);

        page.Rows.Select(r => r.Symbol).Should().Equal("OK1");
        page.RawCount.Should().Be(6);
        page.InvalidCount.Should().Be(5);
    }

    [Theory]
    [InlineData("""{"status":500,"message":"Don't panic, we're on it"}""")]
    [InlineData("""{"message":"success","status":200,"data":[]}""")]
    [InlineData("<html>blocked</html>")]
    [InlineData("")]
    public void technical_filter_refuses_error_or_unexpected_bodies(string body)
    {
        var act = () => TechnicalFilterParser.Parse(body);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void the_whole_market_fetch_uses_the_permissive_market_cap_param_and_a_fixed_page_size()
    {
        // Tham số rỗng cho ra data rỗng; bộ lọc market_cap:0:999999999| trả toàn thị trường (đo 2026-11-28: 1827 mã, 4 trang × 500).
        TechnicalFilterParser.FullUniverseParam.Should().Be("market_cap:0:999999999|");
        TechnicalFilterParser.PageSize.Should().Be(500);
    }

    // ---------------- HOSE

    [Fact]
    public void hose_status_list_returns_only_the_wanted_ids()
    {
        const string json = """
        {"data":[{"id":33,"name":"CK thuộc diện bị cảnh báo"},{"id":34,"name":"CK thuộc diện bị kiểm soát"},{"id":99,"name":"Danh sách khác"},{"id":53,"name":"CK thuộc diện tạm ngừng/đình chỉ"}],"success":true}
        """;

        var lists = HoseStatusParser.ParseStatusLists(json);

        lists.Select(l => l.Id).Should().Equal(33, 34, 53);
        lists[0].Name.Should().Be("CK thuộc diện bị cảnh báo");
    }

    [Fact]
    public void hose_status_rows_parsed_reason_stripped()
    {
        const string json = """
        {"data":{"list":[
          {"securitiesCode":"TTF","datePublish":1794385928,"appliedDate":1794355200,"reason":"<p>Vốn chủ sở hữu &lt; 0 tại ngày <b>30/09/2026</b>.</p>\n<p>BCTC quý 3.</p>"},
          {"securitiesCode":"vph","appliedDate":1790121600,"reason":"<p>Chậm nộp báo cáo.</p>"}
        ],"paging":{"pageIndex":1,"pageSize":2,"totalCount":35,"totalPages":18}},"success":true}
        """;

        var page = HoseStatusParser.ParseStockStatus(json, "CK thuộc diện bị cảnh báo");

        page.TotalPages.Should().Be(18);
        page.Entries.Select(e => e.Symbol).Should().Equal("TTF", "VPH");
        page.Entries[0].Reason.Should().Be("Vốn chủ sở hữu < 0 tại ngày 30/09/2026. BCTC quý 3.");
        page.Entries[0].Reason.Should().NotContain("<p>").And.NotContain("&lt;");
        page.Entries[0].ListName.Should().Be("CK thuộc diện bị cảnh báo");
        page.Entries[0].AppliedDate.Should().Be(new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc).AddHours(0), "appliedDate là giây Unix");
    }

    [Fact]
    public void hose_reason_html_is_stripped_to_plain_text_so_markup_never_reaches_the_ui()
    {
        HoseStatusParser.StripHtml("<script>alert(1)</script><p>Lý <i>do</i>&nbsp;A</p>").Should().Be("Lý do A");
        HoseStatusParser.StripHtml("&lt;script&gt;alert(1)&lt;/script&gt;Lý do").Should().NotContain("<script").And.NotContain("</script");
        HoseStatusParser.StripHtml("<p>a</p><p>b</p>").Should().Be("a b");
        HoseStatusParser.StripHtml(null).Should().Be("");
    }

    [Fact]
    public void delist_list_keeps_stock_codes_only()
    {
        const string json = """
        {"data":{"list":[
          {"code":"CACB2608","cancelDate":1794268800},
          {"code":"ABC","cancelDate":1794268800},
          {"code":"A32","cancelDate":1794268800},
          {"code":"CFPT2613","cancelDate":1794268800},
          {"code":"xyz","cancelDate":null}
        ]}}
        """;

        var delistings = HoseStatusParser.ParseDelistings(json);

        delistings.Select(d => d.Symbol).Should().Equal("ABC", "A32", "XYZ");
    }

    [Theory]
    [InlineData("<html>x</html>")]
    [InlineData("""{"data":null}""")]
    public void hose_parsers_refuse_unexpected_bodies(string body)
    {
        ((Action)(() => HoseStatusParser.ParseDelistings(body))).Should().Throw<FormatException>();
        ((Action)(() => HoseStatusParser.ParseStockStatus(body, "x"))).Should().Throw<FormatException>();
    }

    [Fact]
    public void an_rs_value_outside_1_to_99_is_reported_for_the_sanity_gate()
    {
        const string json = """
        {"status":200,"data":{"total_page":1,"data":[
          {"symbol":"AAA","floor":"HOSE","market_cap":2000.0,"match_price":10.0,"rs1m":50,"rs3m":50,"rs52w":50},
          {"symbol":"BBB","floor":"HOSE","market_cap":2000.0,"match_price":10.0,"rs1m":50,"rs3m":120,"rs52w":50},
          {"symbol":"CCC","floor":"HOSE","market_cap":2000.0,"match_price":10.0,"rs1m":-4,"rs3m":50,"rs52w":0}
        ]}}
        """;

        var page = TechnicalFilterParser.Parse(json);

        page.RsOutOfRangeCount.Should().Be(3, "rs là xếp hạng 1–99: giá trị ngoài khoảng nghĩa là nguồn đổi ý nghĩa của trường này");
    }
}
