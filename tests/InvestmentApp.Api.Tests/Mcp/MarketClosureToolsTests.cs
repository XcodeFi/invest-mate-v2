using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using MediatR;
using Moq;
using Xunit;

namespace InvestmentApp.Api.Tests.Mcp;

public class MarketClosureToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task add_market_closures_chuyen_mang_chuoi_thanh_ngay()
    {
        McpTestContext.Capture<AddMarketClosuresResult, AddMarketClosuresCommand>(
            _mediator, out var sent, new AddMarketClosuresResult(2, 0, 0));

        var result = await MarketClosureTools.AddMarketClosures(
            new[] { "2026-04-30", "2026-05-01" },
            _mediator.Object, McpTestContext.WithUser("user1"), CancellationToken.None, "Lễ 30/4");

        result.Added.Should().Be(2);
        sent()!.UserId.Should().Be("user1");
        sent()!.Dates.Should().BeEquivalentTo(new[] { new DateTime(2026, 4, 30), new DateTime(2026, 5, 1) });
        sent()!.Note.Should().Be("Lễ 30/4");
    }

    [Fact]
    public async Task Bo_trong_note_van_goi_duoc()
    {
        McpTestContext.Capture<AddMarketClosuresResult, AddMarketClosuresCommand>(
            _mediator, out var sent, new AddMarketClosuresResult(1, 0, 0));

        await MarketClosureTools.AddMarketClosures(
            new[] { "2026-01-01" }, _mediator.Object, McpTestContext.WithUser(), CancellationToken.None);

        sent()!.Note.Should().BeNull();
    }

    [Theory]
    [InlineData("30/04/2026")]
    [InlineData("2026-4-30")]
    [InlineData("hôm nay")]
    public async Task Ngay_sai_dinh_dang_bao_ro_can_gui_gi(string bad)
    {
        var act = async () => await MarketClosureTools.AddMarketClosures(
            new[] { bad }, _mediator.Object, McpTestContext.WithUser(), CancellationToken.None);

        // Lỗi phải nói phải gửi GÌ cho đúng, không chỉ nói là sai.
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*YYYY-MM-DD*");
    }

    [Fact]
    public async Task list_market_closures_truyen_dung_nam_va_user()
    {
        McpTestContext.Capture<MarketClosureYearDto, GetMarketClosuresQuery>(
            _mediator, out var sent, new MarketClosureYearDto(2026, new List<MarketClosureMonthDto>()));

        await MarketClosureTools.ListMarketClosures(
            2026, _mediator.Object, McpTestContext.WithUser("user1"), CancellationToken.None);

        sent()!.Year.Should().Be(2026);
        sent()!.UserId.Should().Be("user1");
    }

    [Fact]
    public async Task remove_market_closure_truyen_dung_ngay()
    {
        McpTestContext.Capture<bool, RemoveMarketClosureCommand>(_mediator, out var sent, true);

        var removed = await MarketClosureTools.RemoveMarketClosure(
            "2026-04-27", _mediator.Object, McpTestContext.WithUser("user1"), CancellationToken.None);

        removed.Should().BeTrue();
        sent()!.Date.Should().Be(new DateTime(2026, 4, 27));
        sent()!.UserId.Should().Be("user1");
    }
}
