using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class HmoneyComprehensiveDataProviderTests
{
    private readonly Mock<ILogger<HmoneyComprehensiveDataProvider>> _loggerMock = new();
    private readonly IOptions<MarketDataProviderOptions> _options =
        Options.Create(new MarketDataProviderOptions { BaseUrl = "https://api-test.example.com" });

    private HmoneyComprehensiveDataProvider CreateProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new HmoneyComprehensiveDataProvider(httpClient, _loggerMock.Object, _options);
    }

    // =============================================
    // Happy path: all APIs return valid data
    // =============================================

    [Fact]
    public async Task GetComprehensiveDataAsync_WithValidSymbol_ReturnsAllSections()
    {
        // Arrange
        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["companies/index"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new
                {
                    symbol = "MWG", pe = 15.5, pe4Q = 23.3, pb = 3.3, pb4Q = 4.3,
                    eps = 4777.0, eps4Q = 3980.0, roe = 23.3, roe4Q = 19.7,
                    roa = 9.1, roa4Q = 7.6, market_cap = 108957000000000.0,
                    book_value = 22203.0, the_beta = 1.02, ev_per_ebitda = 14.8,
                    ev_per_ebit = 18.7, free_float_rate = 0.75, min_52w = 45.59,
                    max_52w = 93.7, listed_share_vol = 1469693177L, circulation_vol = 1468423529L,
                    group_name = "Phân phối hàng chuyên dụng",
                    audit_firm_name = "Ernst & Young", audit_is_big4 = true
                }
            }),
            ["company/detail"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new
                {
                    company_name = "Công ty CP Đầu tư Thế Giới Di Động",
                    short_name = "Thế Giới Di Động", floor = "HOSE",
                    major_share_holder = new[]
                    {
                        new { name = "Nguyễn Đức Tài", position = "Chủ tịch HĐQT", quantity = 50000000m, percentage = 3.4m }
                    },
                    company_leaders = new[]
                    {
                        new { name = "Đoàn Văn Hiểu Em", position = "CEO" }
                    }
                }
            }),
            ["financial-report"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new
                {
                    header = new[] { "Q4/2025", "Q3/2025", "Q2/2025", "Q1/2025" },
                    data = new object[]
                    {
                        new { name = "Doanh thu thuần", values = new decimal?[] { 35000, 32000, 30000, 28000 } },
                        new { name = "Lợi nhuận sau thuế", values = new decimal?[] { 2000, 1800, 1500, 1200 } }
                    }
                }
            }),
            ["get_stock_related_bussiness"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new
                {
                    data = new[]
                    {
                        new { symbol = "FRT", company_name = "FPT Retail", price = 150.0m, pe = 20.0m, pb = 3.0m, market_cap = 10000m, change_percent = 1.5m },
                        new { symbol = "DGW", company_name = "Digiworld", price = 50.0m, pe = 15.0m, pb = 2.5m, market_cap = 5000m, change_percent = -0.5m }
                    }
                }
            }),
            ["dividend-events"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new[]
                {
                    new { event_type = "cash", description = "Trả cổ tức tiền mặt 1,500 VND/CP", ex_right_date = "15/03/2026", pay_date = "30/03/2026", value = 1500m, event_name = (string?)null }
                }
            }),
            ["company/plan"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new[]
                {
                    new { year = 2026, plan_revenue = 150000m, plan_profit = 10000m, plan_dividend = 15m }
                }
            }),
            ["report-analytics"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new[]
                {
                    new { title = "MWG - Khuyến nghị MUA", source = "KBSV", publish_date = "2026-03-17", summary = "Giá mục tiêu 121,600" }
                }
            }),
            ["foreign-trading-series"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new[]
                {
                    new { trading_date = "2026-03-20", buy_foreign_qtty = 500000m, sell_foreign_qtty = 300000m },
                    new { trading_date = "2026-03-21", buy_foreign_qtty = 600000m, sell_foreign_qtty = 700000m }
                }
            }),
            ["indices/detail"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new
                {
                    share_detail = new
                    {
                        symbol = "VNINDEX", floor_code = "10", market_index = 1280.5m,
                        prior_market_index = 1275.0m, change = 5.5m, change_percent = 0.43m,
                        avg_index = 1278m, highest_index = 1285m, lowest_index = 1270m,
                        advance = 200, decline = 150, no_change = 50,
                        ceiling = 20, floor = 10,
                        accumulated_vol = 500000000m, accumulated_val = 15000m,
                        foreign_today_buy_value = 500m, foreign_today_sell_value = 400m,
                        foreign_week_buy_value = 2500m, foreign_week_sell_value = 2000m,
                        foreign_month_buy_value = 10000m, foreign_month_sell_value = 9000m,
                        updated_at = 0m
                    },
                    statistic = (object?)null
                }
            })
        });

        var provider = CreateProvider(handler);

        // Act
        var result = await provider.GetComprehensiveDataAsync("MWG");

        // Assert
        result.Should().NotBeNull();
        result!.Symbol.Should().Be("MWG");

        // Company overview
        result.Company.Should().NotBeNull();
        result.Company!.CompanyName.Should().Be("Công ty CP Đầu tư Thế Giới Di Động");
        result.Company.Industry.Should().Be("Phân phối hàng chuyên dụng");
        result.Company.MajorShareholders.Should().HaveCount(1);
        result.Company.Leaders.Should().HaveCount(1);
        result.Company.FreeFloatRate.Should().Be(75m); // 0.75 * 100

        // Finance indicators
        result.Indicators.Should().NotBeNull();
        result.Indicators!.PE4Q.Should().Be(23.3m);
        result.Indicators.PB4Q.Should().Be(4.3m);
        result.Indicators.ROE.Should().Be(23.3m);
        result.Indicators.ROA.Should().Be(9.1m);
        result.Indicators.EPS.Should().Be(4777m);
        result.Indicators.Beta.Should().Be(1.02m);
        result.Indicators.AuditIsBig4.Should().BeTrue();

        // Income statements
        result.IncomeStatements.Should().HaveCount(4);
        result.IncomeStatements[0].Period.Should().Be("Q4/2025");
        result.IncomeStatements[0].Revenue.Should().Be(35000m);

        // Peers (should exclude MWG itself)
        result.Peers.Should().HaveCount(2);
        result.Peers[0].Symbol.Should().Be("FRT");
        result.Peers[0].Price.Should().Be(150000m); // Scaled ×1000

        // Dividend events
        result.DividendEvents.Should().HaveCount(1);
        result.DividendEvents[0].EventType.Should().Be("cash");

        // Business plan
        result.BusinessPlan.Should().NotBeNull();
        result.BusinessPlan!.Year.Should().Be(2026);
        result.BusinessPlan.RevenuePlan.Should().Be(150000m);

        // Analyst reports
        result.AnalystReports.Should().HaveCount(1);
        result.AnalystReports[0].Source.Should().Be("KBSV");

        // Foreign trading
        result.ForeignTrading.Should().HaveCount(2);
        result.ForeignTrading[0].NetVolume.Should().Be(200000m); // 500k - 300k
        result.ForeignTrading[1].NetVolume.Should().Be(-100000m); // 600k - 700k

        // Market index
        result.MarketIndex.Should().NotBeNull();
        result.MarketIndex!.Value.Should().Be(1280.5m);
        result.MarketIndex.Advances.Should().Be(200);
        result.MarketIndex.ForeignBuyValue.Should().Be(500m);
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
        // Only indicators succeed, all others fail
        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["companies/index"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new
                {
                    symbol = "VNM", pe = 20.0, pe4Q = 18.5, pb = 4.0, pb4Q = 3.8,
                    eps = 3500.0, eps4Q = 3200.0, roe = 30.0, roe4Q = 28.0,
                    roa = 15.0, roa4Q = 14.0, market_cap = 200000000000000.0,
                    book_value = 20000.0, the_beta = 0.8, free_float_rate = 0.6,
                    group_name = "Sữa", listed_share_vol = 2000000000L,
                    circulation_vol = 1800000000L
                }
            })
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetComprehensiveDataAsync("VNM");

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("VNM");
        result.Indicators.Should().NotBeNull();
        result.Indicators!.PE4Q.Should().Be(18.5m);
        result.Company.Should().NotBeNull(); // Created from indicators data
        result.Company!.Industry.Should().Be("Sữa");

        // These should be empty but not crash
        result.IncomeStatements.Should().BeEmpty();
        result.Peers.Should().BeEmpty();
        result.DividendEvents.Should().BeEmpty();
        result.AnalystReports.Should().BeEmpty();
        result.ForeignTrading.Should().BeEmpty();
        result.BusinessPlan.Should().BeNull();
        result.MarketIndex.Should().BeNull();
    }

    [Fact]
    public async Task GetComprehensiveDataAsync_NormalizesSymbolToUpperCase()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["companies/index"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new { symbol = "HPG", pe = 10.0, group_name = "Thép" }
            })
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetComprehensiveDataAsync("  hpg  ");

        result.Should().NotBeNull();
        result!.Symbol.Should().Be("HPG");
    }

    [Fact]
    public async Task GetComprehensiveDataAsync_PeersExcludeSameSymbol()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["companies/index"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new { symbol = "MWG", pe = 15.0, group_name = "Bán lẻ" }
            }),
            ["get_stock_related_bussiness"] = JsonSerializer.Serialize(new
            {
                message = "success", status = 200,
                data = new
                {
                    data = new[]
                    {
                        new { symbol = "MWG", company_name = "MWG itself", price = 75.0m, pe = 15.0m, pb = 3.0m, market_cap = 100000m, change_percent = 0m },
                        new { symbol = "FRT", company_name = "FPT Retail", price = 150.0m, pe = 20.0m, pb = 3.0m, market_cap = 10000m, change_percent = 1.5m }
                    }
                }
            })
        });

        var provider = CreateProvider(handler);
        var result = await provider.GetComprehensiveDataAsync("MWG");

        result!.Peers.Should().HaveCount(1);
        result.Peers[0].Symbol.Should().Be("FRT");
    }

    // =============================================
    // Fake HTTP handler for testing
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

            // Return 404 for unmatched URLs
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"message\":\"not found\",\"status\":404}", Encoding.UTF8, "application/json")
            });
        }
    }
}
