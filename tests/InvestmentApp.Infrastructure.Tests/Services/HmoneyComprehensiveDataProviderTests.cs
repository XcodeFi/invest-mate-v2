using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Fixture là response THẬT bắt được từ api-finance-t19.24hmoney.vn ngày 2026-08-11 (mã HAH).
/// Fixture tự bịa từng ghim lại cấu trúc mà upstream đã bỏ, nên test xanh trong khi production
/// hỏng 6/8 section. Khi upstream đổi tiếp, bắt lại bằng curl và ghi đè file trong Fixtures/Hmoney/.
/// </summary>
public class HmoneyComprehensiveDataProviderTests
{
    private readonly Mock<ILogger<HmoneyComprehensiveDataProvider>> _loggerMock = new();
    private readonly IOptions<MarketDataProviderOptions> _options =
        Options.Create(new MarketDataProviderOptions { BaseUrl = "https://api-test.example.com" });

    private static string Fixture(string name) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hmoney", name + ".json"));

    /// <summary>Khoá là đoạn URL phân biệt được endpoint; giá trị là response thật.</summary>
    private static Dictionary<string, string> RealResponses() => new()
    {
        ["companies/index"] = Fixture("finance_indicators"),
        ["company/detail"] = Fixture("company_detail"),
        ["company/plan"] = Fixture("company_plan"),
        ["financial-report"] = Fixture("financial_report"),
        ["get_stock_related_bussiness"] = Fixture("peers"),
        ["dividend-events"] = Fixture("dividend_events"),
        ["report-analytics"] = Fixture("analyst_reports"),
        ["foreign-trading-series"] = Fixture("foreign_trading"),
        ["stock/detail"] = Fixture("stock_detail"),
        ["indices/detail"] = Fixture("index_detail")
    };

    private HmoneyComprehensiveDataProvider CreateProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new HmoneyComprehensiveDataProvider(httpClient, _loggerMock.Object, _options);
    }

    private Task<ComprehensiveStockData?> GetRealAsync(string symbol = "HAH")
        => CreateProvider(new FakeHttpHandler(RealResponses())).GetComprehensiveDataAsync(symbol);

    // =============================================
    // Không section nào được rơi khỏi response thật
    // =============================================

    [Fact]
    public async Task GetComprehensiveDataAsync_WithRealResponses_FillsEverySection()
    {
        var result = await GetRealAsync();

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("HAH");

        result.Company.Should().NotBeNull();
        result.Indicators.Should().NotBeNull();
        result.IncomeStatements.Should().NotBeEmpty();
        result.Peers.Should().NotBeEmpty();
        result.DividendEvents.Should().NotBeEmpty();
        result.BusinessPlan.Should().NotBeNull();
        result.AnalystReports.Should().NotBeEmpty();
        result.ForeignTrading.Should().NotBeNull();
        result.MarketIndex.Should().NotBeNull();
    }

    // =============================================
    // Vỏ rỗng: có hàng nhưng field null còn nguy hiểm hơn không có hàng,
    // vì UI hiện bảng trống và người đọc tưởng công ty không có dữ liệu đó.
    // Mọi khẳng định dưới đây soi GIÁ TRỊ, không chỉ soi số lượng.
    // =============================================

    [Fact]
    public async Task Company_FromRealResponse_HasNameExchangeShareholdersAndLeaders()
    {
        var result = (await GetRealAsync())!;

        var company = result.Company!;
        company.CompanyName.Should().Be("Công ty Cổ phần Vận tải và xếp dỡ Hải An");
        company.ShortName.Should().Be("Vận tải Hải An");
        company.Exchange.Should().Be("HOSE");
        company.Industry.Should().Be("Kho bãi, hậu cần và bảo dưỡng");
        company.FreeFloatRate.Should().Be(60m);

        company.MajorShareholders.Should().NotBeEmpty();
        var top = company.MajorShareholders[0];
        top.Name.Should().Be("Công ty cổ phần Container Việt Nam");
        top.Percentage.Should().BeApproximately(16.07m, 0.001m);
        top.Quantity.Should().Be(29863050m);

        company.Leaders.Should().NotBeEmpty();
        company.Leaders[0].Name.Should().Be("Trần Thị Hải Yến");
        company.Leaders[0].Position.Should().Be("Thành viên HĐQT");
    }

    [Fact]
    public async Task Shareholder_WithCommaDecimal_IsDroppedRatherThanRead100xTooBig()
    {
        // Nguồn đang trả "16.07". Nếu đổi sang cách viết Việt Nam thì `NumberStyles.Any` đọc
        // "16,07" thành 1607 — một cổ đông 16% hiện thành 1607%. Thà mất số còn hơn sai 100 lần.
        var responses = RealResponses();
        responses["company/detail"] = JsonSerializer.Serialize(new
        {
            message = "success", status = 200,
            data = new
            {
                ownership = new[] { new { name = "Container Việt Nam", value = "16,07", stock = "29863050" } },
                leadership = Array.Empty<object>()
            }
        });

        var result = await CreateProvider(new FakeHttpHandler(responses)).GetComprehensiveDataAsync("HAH");

        result!.Company!.MajorShareholders[0].Percentage.Should().Be(0m);
    }

    [Fact]
    public async Task DividendEvents_FromRealResponse_HaveTypeDescriptionAndVietnamDates()
    {
        var result = (await GetRealAsync())!;

        var first = result.DividendEvents[0];
        first.EventType.Should().Be("cash");
        first.Description.Should().Contain("chia cổ tức bằng tiền");

        // epoch 1783962000 = 2026-07-13T17:00Z = đúng nửa đêm 14/07 giờ VN.
        // Quy đổi theo UTC sẽ ra 13/07 — lùi đúng một ngày.
        first.ExDate.Should().Be("14/07/2026");
        first.PayDate.Should().Be("05/08/2026");

        result.DividendEvents.Should().OnlyContain(e => e.Description != null && e.Description != "");
    }

    // =============================================
    // Các section đổi hẳn cấu trúc
    // =============================================

    [Fact]
    public async Task BusinessPlan_FromRealResponse_HasLabelledTargetsWithProgress()
    {
        var result = (await GetRealAsync())!;

        var plan = result.BusinessPlan!;
        plan.Year.Should().Be(2026);
        plan.Quarter.Should().Be(2);
        plan.Targets.Should().HaveCount(3);

        var revenue = plan.Targets[0];
        revenue.Label.Should().Be("Doanh thu");
        revenue.Planned.Should().Be(5140m);
        revenue.Actual.Should().Be(2798.51m);
        revenue.PercentComplete.Should().Be(54.45m);

        plan.Targets.Select(t => t.Label)
            .Should().Contain(new[] { "Lợi nhuận trước thuế", "Lợi nhuận sau thuế" });
    }

    [Fact]
    public async Task ForeignTrading_FromRealResponse_SummarisesTodayWeekMonth()
    {
        var result = (await GetRealAsync())!;

        var ft = result.ForeignTrading!;
        ft.TodayBuyValue.Should().Be(1.08m);
        ft.TodaySellValue.Should().Be(7.01m);
        ft.WeekBuyValue.Should().Be(14.1m);
        ft.WeekSellValue.Should().Be(48.11m);
        ft.MonthBuyValue.Should().Be(48.13m);
        ft.MonthSellValue.Should().Be(101.98m);
        ft.TodayNetValue.Should().BeApproximately(-5.93m, 0.001m);
    }

    [Fact]
    public async Task IncomeStatements_FromRealResponse_AreQuarterlyWithNetProfit()
    {
        var result = (await GetRealAsync())!;

        result.IncomeStatements.Should().HaveCount(8);

        var latest = result.IncomeStatements[0];
        latest.Period.Should().Be("Q2/2026");
        latest.Revenue.Should().Be(1533.5525m);
        latest.GrossProfit.Should().Be(635.6182m);

        // Dòng này upstream đặt tên VIẾT HOA ("LỢI NHUẬN SAU THUẾ TNDN"),
        // nên so khớp phân biệt hoa thường sẽ trả null mà không báo lỗi.
        latest.NetProfit.Should().Be(446.2782m);
    }

    [Fact]
    public async Task Peers_FromRealResponse_AreReadFromAllBucket()
    {
        var result = (await GetRealAsync())!;

        result.Peers.Should().HaveCount(5);
        result.Peers[0].Symbol.Should().Be("ACV");
        result.Peers[0].CompanyName.Should().Contain("cảng hàng không");
        result.Peers[0].Price.Should().Be(41400m); // 41.4 × 1000
        result.Peers[0].PE.Should().Be(13.57m);
    }

    [Fact]
    public async Task Indicators_FromRealResponse_StillParse()
    {
        var result = (await GetRealAsync())!;

        var ind = result.Indicators!;
        ind.PE.Should().BeApproximately(7.73m, 0.01m);
        ind.PB.Should().BeApproximately(2.29m, 0.01m);
        ind.ROE.Should().BeApproximately(26.70m, 0.01m);
        ind.ROA.Should().BeApproximately(13.97m, 0.01m);
        ind.EPS.Should().BeApproximately(7125.41m, 0.01m);
        ind.Beta.Should().BeApproximately(1.12m, 0.01m);
        ind.AuditIsBig4.Should().BeTrue();
    }

    [Fact]
    public async Task AnalystReports_FromRealResponse_StillParse()
    {
        var result = (await GetRealAsync())!;

        result.AnalystReports[0].Source.Should().Be("KBSV");
        result.AnalystReports[0].Title.Should().Contain("Khuyến nghị MUA");
        result.AnalystReports[0].PublishDate.Should().Be("2026-06-19");
    }

    // =============================================
    // Edge cases
    // =============================================

    [Fact]
    public async Task GetComprehensiveDataAsync_WithNoData_ReturnsNull()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["companies/index"] = JsonSerializer.Serialize(new { message = "not found", status = 404, data = (object?)null }),
            ["company/detail"] = JsonSerializer.Serialize(new { message = "not found", status = 404, data = (object?)null })
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetComprehensiveDataAsync("INVALID");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetComprehensiveDataAsync_WithPartialFailures_ReturnsAvailableData()
    {
        // Chỉ indicators trả được, tất cả phần còn lại 404
        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["companies/index"] = Fixture("finance_indicators")
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetComprehensiveDataAsync("HAH");

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("HAH");
        result.Indicators.Should().NotBeNull();
        result.Company.Should().NotBeNull(); // dựng từ indicators
        result.Company!.Industry.Should().Be("Kho bãi, hậu cần và bảo dưỡng");

        result.IncomeStatements.Should().BeEmpty();
        result.Peers.Should().BeEmpty();
        result.DividendEvents.Should().BeEmpty();
        result.AnalystReports.Should().BeEmpty();
        result.BusinessPlan.Should().BeNull();
        result.ForeignTrading.Should().BeNull();
        result.MarketIndex.Should().BeNull();
    }

    [Fact]
    public async Task GetComprehensiveDataAsync_NormalizesSymbolToUpperCase()
    {
        var result = await GetRealAsync("  hah  ");

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("HAH");
    }

    [Fact]
    public async Task GetComprehensiveDataAsync_PeersExcludeSameSymbol()
    {
        var responses = RealResponses();
        responses["get_stock_related_bussiness"] = JsonSerializer.Serialize(new
        {
            message = "success", status = 200,
            data = new
            {
                all = new
                {
                    data = new[]
                    {
                        new { symbol = "HAH", company_name = "HAH itself", price = 47.0m, pe = 7.7m, pb = 2.3m, market_cap = 8901m, change_percent = 0m },
                        new { symbol = "ACV", company_name = "ACV", price = 41.4m, pe = 13.57m, pb = 2.04m, market_cap = 151568m, change_percent = -2.13m }
                    }
                }
            }
        });

        var result = await CreateProvider(new FakeHttpHandler(responses)).GetComprehensiveDataAsync("HAH");

        result!.Peers.Should().HaveCount(1);
        result.Peers[0].Symbol.Should().Be("ACV");
    }

    // =============================================
    // Fake HTTP handler
    // =============================================

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses;

        public FakeHttpHandler(Dictionary<string, string> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";

            foreach (var kvp in _responses)
            {
                if (url.Contains(kvp.Key))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(kvp.Value, Encoding.UTF8, "application/json")
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"message\":\"not found\",\"status\":404}", Encoding.UTF8, "application/json")
            });
        }
    }
}
