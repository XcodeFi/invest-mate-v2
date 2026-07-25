using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.DeleteJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Application.JournalEntries.Queries.GetJournalEntriesBySymbol;
using InvestmentApp.Application.Journals.Queries.GetTradesPendingReview;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class JournalEntryToolsTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly IHttpContextAccessor _http = McpTestContext.WithUser("u-1");

    [Fact]
    public async Task CreateJournalEntry_SetsUserId()
    {
        McpTestContext.Capture<string, CreateJournalEntryCommand>(_mediator, out var sent, "e1");
        var id = await JournalEntryTools.CreateJournalEntry(new CreateJournalEntryCommand { Symbol = "VNM" }, _mediator.Object, _http, CancellationToken.None);
        id.Should().Be("e1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task UpdateJournalEntry_SetsIdAndUserId()
    {
        McpTestContext.Capture<bool, UpdateJournalEntryCommand>(_mediator, out var sent, true);
        await JournalEntryTools.UpdateJournalEntry("e1", new UpdateJournalEntryCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("e1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task DeleteJournalEntry_SetsIdAndUserId()
    {
        McpTestContext.Capture<bool, DeleteJournalEntryCommand>(_mediator, out var sent, true);
        await JournalEntryTools.DeleteJournalEntry("e1", _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("e1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task ListTradesPendingReview_SetsUserId_AndPortfolioId()
    {
        McpTestContext.Capture<List<PendingReviewTradeDto>, GetTradesPendingReviewQuery>(_mediator, out var sent, new List<PendingReviewTradeDto>());
        await JournalEntryTools.ListTradesPendingReview("p1", _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.PortfolioId.Should().Be("p1");
    }

    [Fact]
    public async Task ListJournalEntriesBySymbol_EmptySymbol_Throws()
    {
        var act = async () => await JournalEntryTools.ListJournalEntriesBySymbol("  ", null, null, _mediator.Object, _http, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ListJournalEntriesBySymbol_SetsUserId_Symbol()
    {
        McpTestContext.Capture<List<JournalEntryDto>, GetJournalEntriesBySymbolQuery>(_mediator, out var sent, new List<JournalEntryDto>());
        await JournalEntryTools.ListJournalEntriesBySymbol("VNM", null, null, _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
        sent()!.Symbol.Should().Be("VNM");
    }
}
