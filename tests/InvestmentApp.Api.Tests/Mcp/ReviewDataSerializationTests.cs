using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Application.TradePlans.Commands.ReviewTradePlan;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using Moq;
using ModelContextProtocol;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Số liệu review đi qua MCP (get_trade_plan.reviewData, preview_plan_review, recompute_plan_review) phải mang đủ trường mới, và R
/// "không biết" tuyệt đối không được hiện thành 0 (0R nghĩa là hoà vốn, khác hẳn "bán vượt số mua nên không tính được").
/// </summary>
public class ReviewDataSerializationTests
{
    private static JsonElement Serialize<T>(T value)
        => JsonSerializer.SerializeToElement(value, McpJsonUtilities.DefaultOptions);

    private static readonly DateTime At = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private static LegacyReviewDto Legacy() => new()
    {
        PnLAmount = 143_900_000m, PnLPercent = 99m, RealizedR = 20m, RecomputedAt = At
    };

    private static void AssertRIsNotZero(JsonElement review)
    {
        // Vắng mặt hoặc null đều là "không biết"; con số (nhất là 0) thì không được phép
        if (review.TryGetProperty("realizedR", out var r))
            r.ValueKind.Should().Be(JsonValueKind.Null, "R không tính được không được hiện thành số, nhất là 0");
    }

    [Fact]
    public void TradePlanDto_ReviewData_CarriesTheNewFields_AndRealizedRNullIsNotZero()
    {
        var plan = new TradePlanDto
        {
            ReviewData = new CampaignReviewDataDto
            {
                PnLAmount = 1_000_000m,
                RealizedR = null,
                OpenQuantity = 0m,
                OversoldQuantity = 14_500m,
                DividendIncome = 950_000m,
                RealizedCost = 5_000_000m,
                Flags = new List<string> { "thieu-lenh-mua-lien-ket", "lenh-ban-o-ke-hoach-khac" },
                Legacy = Legacy()
            }
        };

        var review = Serialize(plan).GetProperty("reviewData");

        AssertRIsNotZero(review);
        review.GetProperty("oversoldQuantity").GetDecimal().Should().Be(14_500m);
        review.GetProperty("openQuantity").GetDecimal().Should().Be(0m);
        review.GetProperty("dividendIncome").GetDecimal().Should().Be(950_000m);
        review.GetProperty("realizedCost").GetDecimal().Should().Be(5_000_000m);
        review.GetProperty("flags").EnumerateArray().Select(f => f.GetString()).Should()
            .Equal("thieu-lenh-mua-lien-ket", "lenh-ban-o-ke-hoach-khac");
        var legacy = review.GetProperty("legacy");
        legacy.GetProperty("pnLAmount").GetDecimal().Should().Be(143_900_000m);
        legacy.GetProperty("pnLPercent").GetDecimal().Should().Be(99m);
        legacy.GetProperty("realizedR").GetDecimal().Should().Be(20m);
        legacy.GetProperty("recomputedAt").GetDateTime().Should().Be(At);
    }

    [Fact]
    public void CampaignReviewDto_FromPreviewAndRecompute_CarriesTheSameFields_AndRealizedRNullIsNotZero()
    {
        var dto = new CampaignReviewDto
        {
            RealizedR = null,
            OversoldQuantity = 14_500m,
            OpenQuantity = 0m,
            DividendIncome = 950_000m,
            Flags = new List<string> { "thieu-lenh-mua-lien-ket" },
            Legacy = Legacy()
        };

        var review = Serialize(dto);

        AssertRIsNotZero(review);
        review.GetProperty("oversoldQuantity").GetDecimal().Should().Be(14_500m);
        review.GetProperty("dividendIncome").GetDecimal().Should().Be(950_000m);
        review.GetProperty("flags")[0].GetString().Should().Be("thieu-lenh-mua-lien-ket");
        review.GetProperty("legacy").GetProperty("recomputedAt").GetDateTime().Should().Be(At);
    }

    [Fact]
    public async Task AnOpenReview_WithNothingSold_ThroughTheRealMapping_ExposesNoR_NotZero()
    {
        // Kế hoạch 8d9ef87d sau Tính lại: còn 100 CP, chưa bán gì, RealizedR lưu null. Đi qua đúng đường ánh xạ của get_trade_plan
        // (không dựng DTO tay): trước bản sửa RMath, đường này suy ra 0R giả.
        var plan = new TradePlan("u1", "VNM", "Buy", 100m, 90m, 130m, 100, thesis: "Luận điểm mẫu đủ dài cho test kỳ vọng R");
        plan.MarkReady(); plan.MarkInProgress(); plan.Execute("t1");
        plan.MarkReviewed(new CampaignReviewData { PnLAmount = 0m, RealizedR = null, OpenQuantity = 100m });
        var repo = new Mock<ITradePlanRepository>();
        repo.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradePlan> { plan });

        var dto = (await new GetTradePlansQueryHandler(repo.Object)
            .Handle(new GetTradePlansQuery { UserId = "u1" }, CancellationToken.None)).Single();
        var review = Serialize(dto).GetProperty("reviewData");

        AssertRIsNotZero(review);
        review.TryGetProperty("realizedR", out _).Should().BeFalse("MCP bỏ null: R không biết thì vắng mặt");
        review.GetProperty("openQuantity").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public void ARealRValue_IsKept_IncludingAnExactZeroThatMeansBreakEven()
    {
        // Đối chiếu để thấy test trên có răng: R có giá trị (kể cả đúng 0) vẫn hiện, chỉ "không biết" mới vắng/null
        Serialize(new CampaignReviewDto { RealizedR = -0.5m }).GetProperty("realizedR").GetDecimal().Should().Be(-0.5m);
        Serialize(new CampaignReviewDto { RealizedR = 0m }).GetProperty("realizedR").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public void AnOldReview_WithoutTheNewFields_DoesNotInventThem()
    {
        var review = Serialize(new CampaignReviewDto { PnLAmount = 5m });

        foreach (var name in new[] { "openQuantity", "oversoldQuantity", "dividendIncome", "flags", "legacy" })
            if (review.TryGetProperty(name, out var v))
                v.ValueKind.Should().Be(JsonValueKind.Null, $"{name}: review cũ không có thì phải là null/vắng, không phải 0 hay rỗng");
    }
}
