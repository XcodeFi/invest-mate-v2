using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Application.Journals.Commands.DeleteJournal;
using InvestmentApp.Application.Journals.Commands.UpdateJournal;
using InvestmentApp.Application.Journals.Queries.GetJournalByTrade;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class JournalToolsTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly IHttpContextAccessor _http = McpTestContext.WithUser("u-1");

    [Fact]
    public async Task ListJournals_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<IEnumerable<JournalDto>, GetJournalsQuery>(_mediator, out var sent, Array.Empty<JournalDto>());
        await JournalTools.ListJournals("p1", _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.PortfolioId.Should().Be("p1");
    }

    [Fact]
    public async Task GetJournalByTrade_SetsTradeIdAndUserId()
    {
        McpTestContext.Capture<JournalDto?, GetJournalByTradeQuery>(_mediator, out var sent, null);
        await JournalTools.GetJournalByTrade("t1", _mediator.Object, _http, CancellationToken.None);
        sent()!.TradeId.Should().Be("t1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task CreateJournal_SetsUserId()
    {
        McpTestContext.Capture<string, CreateJournalCommand>(_mediator, out var sent, "j1");
        var id = await JournalTools.CreateJournal(new CreateJournalCommand(), _mediator.Object, _http, CancellationToken.None);
        id.Should().Be("j1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task UpdateJournal_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, UpdateJournalCommand>(_mediator, out var sent, Unit.Value);
        await JournalTools.UpdateJournal("j1", new UpdateJournalCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("j1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task DeleteJournal_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, DeleteJournalCommand>(_mediator, out var sent, Unit.Value);
        await JournalTools.DeleteJournal("j1", _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("j1");
        sent()!.UserId.Should().Be("u-1");
    }
}
