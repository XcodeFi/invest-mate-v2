using FluentAssertions;
using Moq;
using InvestmentApp.Application.Admin.Queries.GetUsersOverview;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Admin;

public class GetUsersOverviewQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPortfolioRepository> _portfolioRepo = new();
    private readonly Mock<ITradeRepository> _tradeRepo = new();
    private readonly Mock<IImpersonationAuditRepository> _auditRepo = new();
    private readonly GetUsersOverviewQueryHandler _handler;

    public GetUsersOverviewQueryHandlerTests()
    {
        _handler = new GetUsersOverviewQueryHandler(
            _userRepo.Object, _portfolioRepo.Object, _tradeRepo.Object, _auditRepo.Object);
    }

    private static User NewUser(string id, string email, UserRole role = UserRole.User, DateTime? lastLogin = null)
    {
        var user = new User(email, $"Name-{id}", null, "google");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        if (role == UserRole.Admin) user.PromoteToAdmin();
        if (lastLogin.HasValue)
        {
            typeof(User).GetProperty(nameof(User.LastLoginAt))!.SetValue(user, lastLogin);
        }
        return user;
    }

    [Fact]
    public async Task Handle_CallerNotAdmin_ThrowsUnauthorized()
    {
        var caller = NewUser("u-1", "u1@example.com", UserRole.User);
        _userRepo.Setup(r => r.GetByIdAsync("u-1", It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        var query = new GetUsersOverviewQuery { CallerUserId = "u-1", Page = 1, PageSize = 20 };
        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_AdminCaller_ReturnsPagedUsersWithStats()
    {
        var admin = NewUser("admin-1", "admin@example.com", UserRole.Admin, lastLogin: DateTime.UtcNow.AddHours(-1));
        var alice = NewUser("u-alice", "alice@example.com", lastLogin: DateTime.UtcNow.AddDays(-2));
        var bob = NewUser("u-bob", "bob@example.com");

        _userRepo.Setup(r => r.GetByIdAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _userRepo.Setup(r => r.GetPagedAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new[] { admin, alice, bob }, (long)3));

        var portfoliosByUser = new Dictionary<string, List<string>>
        {
            ["admin-1"] = new() { "p-admin-1" },
            ["u-alice"] = new() { "p-a1", "p-a2" },
            // bob has no portfolios
        };
        _portfolioRepo.Setup(r => r.GetIdsByUserIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfoliosByUser);

        var lastTradeAlice = DateTime.UtcNow.AddDays(-1);
        var tradeStats = new Dictionary<string, (int Count, DateTime? LastTradeAt)>
        {
            ["p-admin-1"] = (5, DateTime.UtcNow.AddDays(-3)),
            ["p-a1"] = (10, lastTradeAlice),
            ["p-a2"] = (2, DateTime.UtcNow.AddDays(-4)),
        };
        _tradeRepo.Setup(r => r.GetStatsByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tradeStats);

        _auditRepo.Setup(r => r.GetLatestStartedAtByTargetAsync("u-alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow.AddDays(-5));
        _auditRepo.Setup(r => r.GetLatestStartedAtByTargetAsync(It.IsNotIn("u-alice"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var query = new GetUsersOverviewQuery { CallerUserId = "admin-1", Page = 1, PageSize = 20 };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items.Should().HaveCount(3);

        var aliceRow = result.Items.Single(x => x.Id == "u-alice");
        aliceRow.Email.Should().Be("alice@example.com");
        aliceRow.Role.Should().Be("User");
        aliceRow.PortfolioCount.Should().Be(2);
        aliceRow.TradeCount.Should().Be(12);
        aliceRow.LastTradeAt.Should().BeCloseTo(lastTradeAlice, TimeSpan.FromSeconds(1));
        aliceRow.LastLoginAt.Should().NotBeNull();
        aliceRow.LastImpersonatedAt.Should().NotBeNull();

        var bobRow = result.Items.Single(x => x.Id == "u-bob");
        bobRow.PortfolioCount.Should().Be(0);
        bobRow.TradeCount.Should().Be(0);
        bobRow.LastTradeAt.Should().BeNull();
        bobRow.LastLoginAt.Should().BeNull();
        bobRow.LastImpersonatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptyPage_ReturnsEmptyItemsButTotalCount()
    {
        var admin = NewUser("admin-1", "admin@example.com", UserRole.Admin);
        _userRepo.Setup(r => r.GetByIdAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _userRepo.Setup(r => r.GetPagedAsync(5, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)Array.Empty<User>(), (long)42));

        var query = new GetUsersOverviewQuery { CallerUserId = "admin-1", Page = 5, PageSize = 20 };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(42);
        _portfolioRepo.Verify(r => r.GetIdsByUserIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
