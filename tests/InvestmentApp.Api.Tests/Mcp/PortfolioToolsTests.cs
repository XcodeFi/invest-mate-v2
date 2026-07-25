using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.TradePlans.Queries.GetActivePositions;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class PortfolioToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task ListPortfolios_SetsUserId()
    {
        McpTestContext.Capture<List<PortfolioSummaryDto>, GetAllPortfoliosQuery>(
            _mediator, out var sent, new List<PortfolioSummaryDto>());
        await PortfolioTools.ListPortfolios(_mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task ListPositions_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<List<ActivePositionDto>, GetActivePositionsQuery>(
            _mediator, out var sent, new List<ActivePositionDto>());
        await PortfolioTools.ListPositions("p1", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-2");
        sent()!.PortfolioId.Should().Be("p1");
    }
}
