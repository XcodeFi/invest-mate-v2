using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Decisions.DTOs;
using InvestmentApp.Application.Decisions.Queries.GetDecisionQueue;
using InvestmentApp.Application.Discipline.Queries;
using InvestmentApp.Application.TradePlans.Queries.GetPendingThesisReviews;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class DecisionToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task GetDecisionQueue_SetsUserId()
    {
        McpTestContext.Capture<DecisionQueueDto, GetDecisionQueueQuery>(
            _mediator, out var sent, new DecisionQueueDto());
        await DecisionTools.GetDecisionQueue(_mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task GetDisciplineScore_SetsUserId_AndDays()
    {
        McpTestContext.Capture<DisciplineScoreDto, GetDisciplineScoreQuery>(
            _mediator, out var sent, new DisciplineScoreDto());
        await DecisionTools.GetDisciplineScore(30, _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-2");
        sent()!.Days.Should().Be(30);
    }

    [Fact]
    public async Task GetDisciplineScore_DefaultsDaysTo90()
    {
        McpTestContext.Capture<DisciplineScoreDto, GetDisciplineScoreQuery>(
            _mediator, out var sent, new DisciplineScoreDto());
        await DecisionTools.GetDisciplineScore(null, _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);
        sent()!.Days.Should().Be(90);
    }

    [Fact]
    public async Task GetDisciplineStreak_SetsUserId()
    {
        McpTestContext.Capture<DisciplineStreakDto, GetDisciplineStreakQuery>(
            _mediator, out var sent, new DisciplineStreakDto());
        await DecisionTools.GetDisciplineStreak(_mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-4");
    }

    [Fact]
    public async Task GetPendingThesisReviews_SetsUserId()
    {
        McpTestContext.Capture<List<PendingThesisReviewDto>, GetPendingThesisReviewsQuery>(
            _mediator, out var sent, new List<PendingThesisReviewDto>());
        await DecisionTools.GetPendingThesisReviews(_mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);
        sent()!.UserId.Should().Be("u-5");
    }
}
