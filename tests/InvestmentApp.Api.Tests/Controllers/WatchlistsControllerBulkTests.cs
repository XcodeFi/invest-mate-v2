using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItems;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class WatchlistsControllerBulkTests
{
    [Fact]
    public async Task bulk_add_binds_the_id_from_the_route_and_the_user_from_the_jwt_never_from_the_body()
    {
        var mediator = new Mock<IMediator>();
        AddWatchlistItemsCommand? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<AddWatchlistItemsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AddWatchlistItemsResult>, CancellationToken>((c, _) => sent = (AddWatchlistItemsCommand)c)
            .ReturnsAsync(new AddWatchlistItemsResult { WatchlistId = "wl-1", Added = new() { "VNM" } });
        var controller = new WatchlistsController(mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "test")) }
            }
        };

        var result = await controller.AddItemsBulk("wl-1", new AddWatchlistItemsCommand { WatchlistId = "other", UserId = "intruder", Symbols = new() { "VNM" }, Note = "n" });

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<AddWatchlistItemsResult>();
        sent!.WatchlistId.Should().Be("wl-1");
        sent.UserId.Should().Be("user-1");
        sent.Symbols.Should().Equal("VNM");
        sent.Note.Should().Be("n");
    }

    [Fact]
    public async Task bulk_add_without_a_user_claim_is_unauthorised_and_sends_nothing()
    {
        var mediator = new Mock<IMediator>();
        var controller = new WatchlistsController(mediator.Object) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var act = () => controller.AddItemsBulk("wl-1", new AddWatchlistItemsCommand { Symbols = new() { "VNM" } });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        mediator.Verify(m => m.Send(It.IsAny<AddWatchlistItemsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
