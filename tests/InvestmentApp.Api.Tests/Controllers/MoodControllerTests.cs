using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Mood.Commands.SetMood;
using InvestmentApp.Application.Mood.Queries.GetMoodHistory;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class MoodControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private MoodController Sut(string userId = "user-1")
    {
        var controller = new MoodController(_mediator.Object);
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId) }, "Bearer");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return controller;
    }

    [Fact]
    public void The_controller_requires_authorization()
    {
        typeof(MoodController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
    }

    [Fact]
    public async Task History_takes_the_user_id_from_the_token_only()
    {
        GetMoodHistoryQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetMoodHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<MoodHistoryDto>, CancellationToken>((q, _) => sent = (GetMoodHistoryQuery)q)
            .ReturnsAsync(new MoodHistoryDto());

        var result = await Sut("token-user").GetHistory(new DateTime(2026, 11, 1), new DateTime(2026, 11, 28), default);

        result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be("token-user");
        sent.From.Should().Be(new DateTime(2026, 11, 1));
    }

    [Fact]
    public void The_query_object_cannot_be_bound_with_a_user_id_from_the_request()
    {
        typeof(GetMoodHistoryQuery).GetProperty(nameof(GetMoodHistoryQuery.UserId))!
            .GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), false).Should().NotBeEmpty();
        typeof(MoodController).GetMethod(nameof(MoodController.GetHistory))!.GetParameters()
            .Select(p => p.Name).Should().Equal("from", "to", "cancellationToken");
    }

    [Fact]
    public async Task The_web_endpoint_never_lets_the_body_choose_the_source()
    {
        SetMoodCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<SetMoodCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Unit>, CancellationToken>((c, _) => sent = (SetMoodCommand)c)
            .ReturnsAsync(Unit.Value);

        await Sut().SetMood(new SetMoodCommand { Mood = MoodState.Calm, Source = MoodChange.SourceMcp }, default);

        typeof(SetMoodCommand).GetProperty(nameof(SetMoodCommand.Source))!
            .GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), false).Should().NotBeEmpty("JSON của client không được đặt Source");
        sent!.UserId.Should().Be("user-1");
        sent.Source.Should().Be(MoodChange.SourceWeb, "server đặt nguồn, không tin giá trị client gửi");
    }
}
