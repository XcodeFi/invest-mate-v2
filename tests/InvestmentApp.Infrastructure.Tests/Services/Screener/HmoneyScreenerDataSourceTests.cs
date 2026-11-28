using System.Net;
using System.Text;
using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Screener;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Infrastructure.Tests.Services.Screener;

public class HmoneyScreenerDataSourceTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = new();
        public Func<HttpRequestMessage, (HttpStatusCode, string)> Respond = _ => (HttpStatusCode.OK, "{}");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            var (code, body) = Respond(request);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 28, 3, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (HmoneyScreenerDataSource Source, Handler Handler) Build()
    {
        var handler = new Handler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api-finance.example") };
        var source = new HmoneyScreenerDataSource(http, new FixedTime(), Options.Create(new ScreenerOptions { HsxBaseUrl = "https://hsx.example" }));
        return (source, handler);
    }

    private static string Page(int totalPages, params string[] symbols)
    {
        var rows = string.Join(",", symbols.Select(x =>
            "{\"symbol\":\"" + x + "\",\"floor\":\"HOSE\",\"market_cap\":2000.0,\"match_price\":10.0,\"avg_trading_vol_10\":1000,\"match_price_10\":10.0,\"rs1m\":50,\"rs3m\":50,\"rs52w\":50}"));
        return "{\"message\":\"success\",\"status\":200,\"data\":{\"total_page\":" + totalPages + ",\"data\":[" + rows + "]}}";
    }

    private static string Query(HttpRequestMessage r, string key)
        => System.Web.HttpUtility.ParseQueryString(r.RequestUri!.Query)[key]!;

    [Fact]
    public async Task universe_is_fetched_page_by_page_with_the_fixed_page_size_and_the_whole_market_param()
    {
        var (source, handler) = Build();
        handler.Respond = r => Query(r, "page") switch
        {
            "1" => (HttpStatusCode.OK, Page(3, "AAA", "BBB")),
            "2" => (HttpStatusCode.OK, Page(3, "CCC")),
            _ => (HttpStatusCode.OK, Page(3, "DDD"))
        };

        var fetch = await source.FetchUniverseAsync();

        fetch.Rows.Select(x => x.Symbol).Should().Equal("AAA", "BBB", "CCC", "DDD");
        handler.Requests.Should().HaveCount(3);
        handler.Requests.Select(r => Query(r, "page")).Should().Equal("1", "2", "3");
        handler.Requests.Should().OnlyContain(r => r.RequestUri!.AbsolutePath == "/v1/ios/company/technical-filter");
        handler.Requests.Should().OnlyContain(r => Query(r, "per_page") == "500" && Query(r, "floor") == "all" && Query(r, "param") == "market_cap:0:999999999|");
    }

    [Fact]
    public async Task universe_requests_carry_no_headers_beyond_the_default_client_ones()
    {
        var (source, handler) = Build();
        handler.Respond = _ => (HttpStatusCode.OK, Page(1, "AAA"));

        await source.FetchUniverseAsync();

        handler.Requests[0].Headers.Select(h => h.Key).Should().BeEmpty("không thêm User-Agent, Referer hay header nào để qua WAF");
    }

    [Fact]
    public async Task universe_dedupes_a_symbol_seen_on_two_pages()
    {
        var (source, handler) = Build();
        handler.Respond = r => Query(r, "page") == "1" ? (HttpStatusCode.OK, Page(2, "AAA", "BBB")) : (HttpStatusCode.OK, Page(2, "BBB", "CCC"));

        var fetch = await source.FetchUniverseAsync();

        fetch.Rows.Select(x => x.Symbol).Should().Equal("AAA", "BBB", "CCC");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.Forbidden, "<html>blocked</html>")]
    public async Task a_failing_page_fails_the_whole_fetch(HttpStatusCode code, string body)
    {
        var (source, handler) = Build();
        handler.Respond = r => Query(r, "page") == "1" ? (HttpStatusCode.OK, Page(3, "AAA")) : (code, body);

        var act = () => source.FetchUniverseAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task an_unexpected_body_on_a_later_page_fails_the_whole_fetch_with_format_error()
    {
        var (source, handler) = Build();
        handler.Respond = r => Query(r, "page") == "1" ? (HttpStatusCode.OK, Page(2, "AAA")) : (HttpStatusCode.OK, "<html>captcha</html>");

        var act = () => source.FetchUniverseAsync();

        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task an_absurd_page_count_is_refused_instead_of_looping()
    {
        var (source, handler) = Build();
        handler.Respond = _ => (HttpStatusCode.OK, Page(100_000, "AAA"));

        var act = () => source.FetchUniverseAsync();

        await act.Should().ThrowAsync<FormatException>();
        handler.Requests.Count.Should().BeLessThan(5);
    }

    // ---------------- HOSE

    private static string StatusListBody => """{"data":[{"id":33,"name":"CK thuộc diện bị cảnh báo"},{"id":34,"name":"CK thuộc diện bị kiểm soát"},{"id":99,"name":"Khác"}],"success":true}""";

    private static string StockStatusBody(int totalPages, params string[] codes)
    {
        var items = string.Join(",", codes.Select(c => "{\"securitiesCode\":\"" + c + "\",\"appliedDate\":1794355200,\"reason\":\"<p>Lý do</p>\"}"));
        return "{\"data\":{\"list\":[" + items + "],\"paging\":{\"pageIndex\":1,\"totalPages\":" + totalPages + "}},\"success\":true}";
    }

    private static string CancelBody(params string[] codes)
        => "{\"data\":{\"list\":[" + string.Join(",", codes.Select(c => "{\"code\":\"" + c + "\",\"cancelDate\":1794268800}")) + "]}}";

    [Fact]
    public async Task hose_lists_are_read_without_any_extra_header_and_only_the_wanted_ids()
    {
        var (source, handler) = Build();
        handler.Respond = r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path.EndsWith("/status-list")) return (HttpStatusCode.OK, StatusListBody);
            if (path.EndsWith("/stock-status")) return (HttpStatusCode.OK, Query(r, "statusListId") == "33" ? StockStatusBody(1, "AAA") : StockStatusBody(1, "BBB"));
            return (HttpStatusCode.OK, CancelBody("CCC", "CACB2608"));
        };

        var lists = await source.FetchHoseListsAsync();

        lists.Statuses.Select(s => (s.Symbol, s.ListName)).Should().BeEquivalentTo(new[]
        {
            ("AAA", "CK thuộc diện bị cảnh báo"), ("BBB", "CK thuộc diện bị kiểm soát")
        });
        lists.Delistings.Select(d => d.Symbol).Should().Equal("CCC");
        handler.Requests.Where(r => r.RequestUri!.AbsolutePath.EndsWith("/stock-status")).Select(r => Query(r, "statusListId"))
            .Should().BeEquivalentTo(new[] { "33", "34" }, "id 99 không nằm trong danh sách cần đọc");
        handler.Requests.Should().OnlyContain(r => r.RequestUri!.Host == "hsx.example");
        handler.Requests.Should().OnlyContain(r => !r.Headers.Any(), "không gửi header `type` hay bất kỳ header nào khác");
    }

    [Fact]
    public async Task hose_delistings_are_read_for_this_year_and_next()
    {
        var (source, handler) = Build();
        handler.Respond = r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path.EndsWith("/status-list")) return (HttpStatusCode.OK, """{"data":[{"id":33,"name":"CK thuộc diện bị cảnh báo"}]}""");
            if (path.EndsWith("/stock-status")) return (HttpStatusCode.OK, StockStatusBody(1, "AAA"));
            return (HttpStatusCode.OK, CancelBody("ABC"));
        };

        await source.FetchHoseListsAsync();

        handler.Requests.Where(r => r.RequestUri!.AbsolutePath.EndsWith("/cancel")).Select(r => Query(r, "year"))
            .Should().Equal("2026", "2027");
    }

    [Fact]
    public async Task hose_paged_lists_are_read_to_the_last_page()
    {
        var (source, handler) = Build();
        handler.Respond = r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path.EndsWith("/status-list")) return (HttpStatusCode.OK, """{"data":[{"id":33,"name":"CK thuộc diện bị cảnh báo"}]}""");
            if (path.EndsWith("/stock-status")) return (HttpStatusCode.OK, StockStatusBody(2, Query(r, "pageIndex") == "1" ? "AAA" : "BBB"));
            return (HttpStatusCode.OK, CancelBody());
        };

        var lists = await source.FetchHoseListsAsync();

        lists.Statuses.Select(s => s.Symbol).Should().Equal("AAA", "BBB");
    }

    [Fact]
    public async Task one_failing_hose_list_fails_the_whole_hose_fetch()
    {
        var (source, handler) = Build();
        handler.Respond = r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path.EndsWith("/status-list")) return (HttpStatusCode.OK, StatusListBody);
            if (path.EndsWith("/stock-status") && Query(r, "statusListId") == "34") return (HttpStatusCode.Forbidden, "{}");
            return (HttpStatusCode.OK, path.EndsWith("/stock-status") ? StockStatusBody(1, "AAA") : CancelBody());
        };

        var act = () => source.FetchHoseListsAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData("""{"data":[]}""")]
    [InlineData("""{"data":[{"id":99,"name":"Danh sách khác"}]}""")]
    public async Task hose_status_list_with_none_of_the_wanted_ids_fails_the_fetch_instead_of_looking_clean(string statusList)
    {
        var (source, handler) = Build();
        handler.Respond = r => r.RequestUri!.AbsolutePath.EndsWith("/status-list")
            ? (HttpStatusCode.OK, statusList)
            : (HttpStatusCode.OK, CancelBody());

        var act = () => source.FetchHoseListsAsync();

        (await act.Should().ThrowAsync<FormatException>()).Which.Message.Should().Contain("HOSE");
    }

    [Fact]
    public async Task hose_fetch_with_wanted_ids_but_zero_status_rows_in_total_fails_the_fetch()
    {
        var (source, handler) = Build();
        handler.Respond = r =>
        {
            var path = r.RequestUri!.AbsolutePath;
            if (path.EndsWith("/status-list")) return (HttpStatusCode.OK, StatusListBody);
            if (path.EndsWith("/stock-status")) return (HttpStatusCode.OK, StockStatusBody(1));
            return (HttpStatusCode.OK, CancelBody());
        };

        var act = () => source.FetchHoseListsAsync();

        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public void the_wanted_hose_list_ids_are_exactly_33_to_38_and_47_to_53()
    {
        HoseStatusParser.WantedListIds.OrderBy(x => x).Should().Equal(33, 34, 35, 36, 37, 38, 47, 48, 49, 50, 51, 52, 53);
    }
}
