using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.DeleteWatchlist;
using InvestmentApp.Application.Watchlists.Commands.ImportVn30;
using InvestmentApp.Application.Watchlists.Commands.RemoveWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlistItem;
using InvestmentApp.Application.Watchlists.Dtos;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlistDetail;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlists;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class WatchlistToolsTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly IHttpContextAccessor _http = McpTestContext.WithUser("u-1");

    [Fact]
    public async Task ListWatchlists_SetsUserId()
    {
        McpTestContext.Capture<List<WatchlistDto>, GetWatchlistsQuery>(_mediator, out var sent, new List<WatchlistDto>());
        await WatchlistTools.ListWatchlists(_mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task GetWatchlist_SetsIdAndUserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, GetWatchlistDetailQuery>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.GetWatchlist("w1", _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("w1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task CreateWatchlist_SetsUserId()
    {
        McpTestContext.Capture<WatchlistDto, CreateWatchlistCommand>(_mediator, out var sent, new WatchlistDto());
        await WatchlistTools.CreateWatchlist(new CreateWatchlistCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task UpdateWatchlist_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, UpdateWatchlistCommand>(_mediator, out var sent, Unit.Value);
        await WatchlistTools.UpdateWatchlist("w1", new UpdateWatchlistCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("w1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task DeleteWatchlist_SetsIdAndUserId()
    {
        McpTestContext.Capture<Unit, DeleteWatchlistCommand>(_mediator, out var sent, Unit.Value);
        await WatchlistTools.DeleteWatchlist("w1", _mediator.Object, _http, CancellationToken.None);
        sent()!.Id.Should().Be("w1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task AddWatchlistItem_SetsWatchlistIdAndUserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, AddWatchlistItemCommand>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.AddWatchlistItem("w1", new AddWatchlistItemCommand { Symbol = "VNM" }, _mediator.Object, _http, CancellationToken.None);
        sent()!.WatchlistId.Should().Be("w1");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task UpdateWatchlistItem_SetsWatchlistId_Symbol_UserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, UpdateWatchlistItemCommand>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.UpdateWatchlistItem("w1", "VNM", new UpdateWatchlistItemCommand(), _mediator.Object, _http, CancellationToken.None);
        sent()!.WatchlistId.Should().Be("w1");
        sent()!.Symbol.Should().Be("VNM");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task RemoveWatchlistItem_SetsWatchlistId_Symbol_UserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, RemoveWatchlistItemCommand>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.RemoveWatchlistItem("w1", "VNM", _mediator.Object, _http, CancellationToken.None);
        sent()!.WatchlistId.Should().Be("w1");
        sent()!.Symbol.Should().Be("VNM");
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task ImportVn30_SetsUserId()
    {
        McpTestContext.Capture<WatchlistDetailDto, ImportVn30Command>(_mediator, out var sent, new WatchlistDetailDto());
        await WatchlistTools.ImportVn30(new ImportVn30Command(), _mediator.Object, _http, CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }
}
