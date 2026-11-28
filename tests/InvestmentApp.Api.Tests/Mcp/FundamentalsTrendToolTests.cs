using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetCompanyFundamentals;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// <c>get_company_fundamentals</c> mang thêm khối <c>trend</c> tính sẵn ở server: agent đọc cùng bức tranh với người dùng (cùng quý năm trước,
/// TTM, cả năm khi đủ 4 quý, biên, vị trí 52 tuần). Giá trị thiếu là vắng mặt/null, KHÔNG bao giờ là 0.
/// </summary>
public class FundamentalsTrendToolTests
{
    private static IncomeStatementItem Q(int year, int quarter, decimal? revenue, decimal? profit)
        => new() { Period = $"Q{quarter}/{year}", Revenue = revenue, NetProfit = profit };

    private static JsonElement Serialize(CompanyFundamentalsDto dto)
        => JsonSerializer.SerializeToElement(dto, McpJsonUtilities.DefaultOptions);

    private static CompanyFundamentalsDto WithTrend(params IncomeStatementItem[] items)
    {
        var dto = new CompanyFundamentalsDto { Symbol = "FPT", IncomeStatements = items.ToList() };
        dto.Trend = FundamentalsTrendCalculator.Compute(dto.IncomeStatements, new FinanceIndicators { Min52W = 43.9m, Max52W = 68.2m }, 44.6m);
        return dto;
    }

    [Fact]
    public void the_trend_block_serializes_quarters_ttm_annual_range_and_the_monthly_note()
    {
        var items = new List<IncomeStatementItem>();
        foreach (var (year, mult) in new[] { (2025, 1m), (2024, 0.8m) })
            foreach (var q in new[] { 4, 3, 2, 1 }) items.Add(Q(year, q, 100m * mult, 10m * mult));

        var json = Serialize(WithTrend(items.ToArray()));

        var trend = json.GetProperty("trend");
        trend.GetProperty("note").GetString().Should().Be("Doanh nghiệp báo cáo theo quý; không có số liệu tháng");
        var q4 = trend.GetProperty("quarters")[0];
        q4.GetProperty("period").GetString().Should().Be("Q4/2025");
        q4.GetProperty("revenue").GetDecimal().Should().Be(100m);
        q4.GetProperty("revenueSameQuarterLastYear").GetDecimal().Should().Be(80m);
        q4.GetProperty("revenueYoyPct").GetDecimal().Should().Be(25.0m);
        q4.GetProperty("revenueDirection").GetString().Should().Be("up");
        q4.GetProperty("netMargin").GetDecimal().Should().Be(10m);
        var ttm = trend.GetProperty("ttm");
        ttm.GetProperty("revenue").GetDecimal().Should().Be(400m);
        ttm.GetProperty("prevRevenue").GetDecimal().Should().Be(320m);
        ttm.GetProperty("revenueYoyPct").GetDecimal().Should().Be(25.0m);
        var annual = trend.GetProperty("annual")[0];
        annual.GetProperty("year").GetInt32().Should().Be(2025);
        annual.GetProperty("complete4Quarters").GetBoolean().Should().BeTrue();
        annual.GetProperty("revenue").GetDecimal().Should().Be(400m);
        var range = trend.GetProperty("range52W");
        range.GetProperty("low").GetDecimal().Should().Be(43.9m);
        range.GetProperty("positionPct").GetDecimal().Should().BeApproximately(2.9m, 0.05m);
        range.GetProperty("unit").GetString().Should().Be("nghìn đồng");
    }

    [Fact]
    public void missing_comparisons_are_absent_or_null_never_zero()
    {
        // Quý duy nhất: không có cùng quý năm trước, không đủ cho TTM trước, năm chưa đủ 4 quý.
        var json = Serialize(WithTrend(Q(2026, 1, 500m, 40m)));

        var q = json.GetProperty("trend").GetProperty("quarters")[0];
        foreach (var name in new[] { "revenueSameQuarterLastYear", "revenueYoyPct", "revenueDirection", "netProfitYoyPct", "netProfitDirection" })
            if (q.TryGetProperty(name, out var v))
                v.ValueKind.Should().Be(JsonValueKind.Null, $"{name}: không có kỳ so sánh thì vắng mặt/null, không phải 0");
        var ttm = json.GetProperty("trend").GetProperty("ttm");
        foreach (var name in new[] { "revenue", "prevRevenue", "revenueYoyPct", "revenueDirection" })
            if (ttm.TryGetProperty(name, out var v))
                v.ValueKind.Should().Be(JsonValueKind.Null, $"ttm.{name}");
        var annual = json.GetProperty("trend").GetProperty("annual")[0];
        annual.GetProperty("complete4Quarters").GetBoolean().Should().BeFalse();
        annual.GetProperty("quartersPresent").GetInt32().Should().Be(1);
        if (annual.TryGetProperty("revenue", out var rev)) rev.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void loss_to_profit_serializes_direction_up_with_the_note_and_no_percentage()
    {
        var json = Serialize(WithTrend(Q(2026, 1, 500m, 80m), Q(2025, 1, 400m, -50m)));

        var q = json.GetProperty("trend").GetProperty("quarters")[0];
        q.GetProperty("netProfitDirection").GetString().Should().Be("up");
        q.GetProperty("netProfitNote").GetString().Should().Be("từ lỗ sang lãi");
        if (q.TryGetProperty("netProfitYoyPct", out var pct)) pct.ValueKind.Should().Be(JsonValueKind.Null);
        q.GetProperty("netProfitSameQuarterLastYear").GetDecimal().Should().Be(-50m);
    }

    [Fact]
    public void a_missing_trend_is_listed_in_unavailable_sections_not_silently_absent()
    {
        var dto = new CompanyFundamentalsDto { Symbol = "FPT", UnavailableSections = { "trend" } };

        var json = Serialize(dto);

        json.GetProperty("unavailableSections").EnumerateArray().Select(e => e.GetString()).Should().Contain("trend");
        if (json.TryGetProperty("trend", out var trend)) trend.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void the_tool_description_tells_agents_how_to_read_the_trend_block()
    {
        var method = typeof(CompanyDossierTools).GetMethod(nameof(CompanyDossierTools.GetCompanyFundamentals))!;
        var description = method.GetCustomAttribute<DescriptionAttribute>()!.Description;

        description.Should().Contain("trend");
        description.Should().Contain("cùng quý năm trước");
        description.Should().Contain("TTM");
        description.Should().Contain("1%");
        description.Should().Contain("từ lỗ sang lãi");
        description.Should().Contain("từ lỗ về hòa vốn");
        description.Should().Contain("không có số liệu tháng");
        description.Should().Contain("null");
        method.GetCustomAttribute<McpServerToolAttribute>()!.ReadOnly.Should().BeTrue();
        method.GetCustomAttribute<McpServerToolAttribute>()!.Name.Should().Be("get_company_fundamentals");
    }
}
