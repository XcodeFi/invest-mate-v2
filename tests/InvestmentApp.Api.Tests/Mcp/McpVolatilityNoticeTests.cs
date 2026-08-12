using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Trần biến động là "cảnh báo, không chặn" (ADR-0014, Đ4). Trên form web panel tự hiện nên không
/// tránh được; qua agent thì phải có thứ gì đó chủ động nói ra, nếu không lan can chỉ tồn tại trên
/// đường người dùng tự bấm.
/// </summary>
public class McpVolatilityNoticeTests
{
    private static VolatilitySizingResult Result(
        int? ceiling = 198,
        bool over = false,
        bool unconstrained = false,
        VolatilityDataQuality quality = VolatilityDataQuality.Full,
        decimal? current = 24.8m,
        decimal? projected = 29.6m) => new()
        {
            Symbol = "FPT",
            CurrentVolatilityPercent = current,
            ProjectedVolatilityPercent = projected,
            BudgetVolatilityPercent = 27.4m,
            MaxQuantityWithinBudget = ceiling,
            PortfolioAlreadyOverBudget = over,
            IsUnconstrainedByVolatility = unconstrained,
            DataQuality = quality,
            ObservationCount = 51
        };

    [Fact]
    public void WithinCeiling_SaysNothing()
    {
        // Im lặng đúng ở đây: lệnh đã tạo xong và không có gì để hành động. Nối thêm một dòng vào
        // MỌI lời gọi là biến cảnh báo thành tiếng ồn, rồi agent học cách bỏ qua nó.
        McpVolatilityNotice.Describe(Result(ceiling: 198), quantity: 100).Should().BeNull();
    }

    [Fact]
    public void ExactlyAtCeiling_SaysNothing()
    {
        McpVolatilityNotice.Describe(Result(ceiling: 198), quantity: 198).Should().BeNull();
    }

    [Fact]
    public void OverCeiling_NamesBothNumbers()
    {
        var notice = McpVolatilityNotice.Describe(Result(ceiling: 198), quantity: 500);

        notice.Should().NotBeNull();
        notice.Should().Contain("500").And.Contain("198",
            "agent phải thấy CẢ khối lượng đã đặt lẫn trần, không thì không biết lệch bao nhiêu");
        notice.Should().Contain("27,4").And.Contain("29,6",
            "nêu ngân sách và biến động dự phóng để con số trần có căn cứ");
    }

    [Fact]
    public void PortfolioAlreadyOverBudget_SaysSo_EvenWhenQuantityIsTiny()
    {
        // Trần = 0 nên mọi khối lượng đều vượt, nhưng lý do khác hẳn: vấn đề nằm ở danh mục sẵn có
        // chứ không ở lệnh này. Nói nhầm thành "giảm khối lượng đi" là chỉ sai việc cần làm.
        var notice = McpVolatilityNotice.Describe(Result(ceiling: 0, over: true), quantity: 1);

        notice.Should().NotBeNull();
        notice.Should().Contain("đã vượt ngân sách");
        notice.Should().NotContain("Giảm còn",
            "không đề nghị giảm khối lượng khi giảm bao nhiêu cũng không cứu được");
    }

    [Fact]
    public void Unconstrained_SaysNothing()
    {
        McpVolatilityNotice.Describe(
            Result(ceiling: null, unconstrained: true), quantity: 100_000).Should().BeNull();
    }

    [Fact]
    public void Insufficient_SaysItCouldNotCheck()
    {
        // KHÔNG được im. Im lặng ở đây đọc thành "đã kiểm và ổn", ngược hẳn sự thật là "chưa kiểm
        // được" — cùng lỗi mà panel web tránh bằng cách luôn hiện khối thiếu-dữ-liệu.
        var notice = McpVolatilityNotice.Describe(
            Result(ceiling: null, quality: VolatilityDataQuality.Insufficient,
                   current: null, projected: null),
            quantity: 500);

        notice.Should().NotBeNull();
        notice.Should().Contain("chưa kiểm được");
    }

    [Fact]
    public void NullResult_SaysItCouldNotCheck()
    {
        // Truy vấn hỏng (không danh mục, lỗi mạng) cũng không được im lặng biến mất.
        McpVolatilityNotice.Describe(null, quantity: 500)
            .Should().NotBeNull().And.Contain("chưa kiểm được");
    }

    [Fact]
    public void UsesVietnameseDecimalComma()
    {
        // Toàn bộ text hiển thị của dự án dùng dấu phẩy thập phân.
        var notice = McpVolatilityNotice.Describe(Result(ceiling: 198), quantity: 500);

        notice.Should().Contain("29,6").And.NotContain("29.6");
    }
}

/// <summary>
/// Đấu nối: <c>create_trade_plan</c> phải TỰ gọi truy vấn trần. Không có lớp test này thì
/// <see cref="McpVolatilityNotice"/> vẫn xanh trong khi chẳng ai gọi tới nó — luật chết sau UI sống.
/// </summary>
public class CreateTradePlanVolatilityWiringTests
{
    private readonly Mock<IMediator> _mediator = new();

    private void StubCreate() =>
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("plan-1");

    private void StubSizing(VolatilitySizingResult result) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetVolatilitySizingForPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private Task<string> Create(string? portfolioId = "p-1", string? direction = null, int quantity = 500) =>
        TradePlanTools.CreateTradePlan("FPT", 71200m, 65000m, 85000m, quantity,
            _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None,
            portfolioId: portfolioId, direction: direction);

    [Fact]
    public async Task OverCeiling_AppendsWarningToTheReturnedId()
    {
        StubCreate();
        StubSizing(new VolatilitySizingResult
        {
            Symbol = "FPT", MaxQuantityWithinBudget = 198, BudgetVolatilityPercent = 27.4m,
            ProjectedVolatilityPercent = 29.6m, DataQuality = VolatilityDataQuality.Full
        });

        var result = await Create();

        result.Should().StartWith("plan-1", "id phải còn nguyên ở đầu để agent vẫn đọc được");
        result.Should().Contain("198").And.Contain("vượt trần");
    }

    [Fact]
    public async Task WithinCeiling_ReturnsBareId()
    {
        StubCreate();
        StubSizing(new VolatilitySizingResult
        {
            Symbol = "FPT", MaxQuantityWithinBudget = 198, DataQuality = VolatilityDataQuality.Full
        });

        (await Create(quantity: 100)).Should().Be("plan-1");
    }

    [Fact]
    public async Task NoPortfolio_DoesNotEvenQuery()
    {
        StubCreate();

        (await Create(portfolioId: null)).Should().Be("plan-1");
        _mediator.Verify(m => m.Send(It.IsAny<GetVolatilitySizingForPlanQuery>(), It.IsAny<CancellationToken>()),
            Times.Never, "không gắn danh mục thì không có gì để chiếu lên");
    }

    [Fact]
    public async Task SellDirection_DoesNotEvenQuery()
    {
        StubCreate();

        (await Create(direction: "Sell")).Should().Be("plan-1");
        _mediator.Verify(m => m.Send(It.IsAny<GetVolatilitySizingForPlanQuery>(), It.IsAny<CancellationToken>()),
            Times.Never, "phép chiếu giả định lệnh MUA — lệnh bán sẽ báo rủi ro tăng đúng lúc nó giảm");
    }

    [Fact]
    public async Task SizingQueryThrows_StillReturnsThePlanId()
    {
        // Kế hoạch ĐÃ tạo. Ném ở đây khiến agent tưởng thất bại rồi tạo lại — sinh kế hoạch trùng.
        StubCreate();
        _mediator.Setup(m => m.Send(It.IsAny<GetVolatilitySizingForPlanQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await Create();

        result.Should().StartWith("plan-1");
        result.Should().Contain("chưa kiểm được");
    }
}
