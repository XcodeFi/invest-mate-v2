using FluentAssertions;
using Moq;
using InvestmentApp.Application.Admin.Queries.SearchUsers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Admin;

public class SearchUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo;
    private readonly SearchUsersQueryHandler _handler;

    public SearchUsersQueryHandlerTests()
    {
        _userRepo = new Mock<IUserRepository>();
        _handler = new SearchUsersQueryHandler(_userRepo.Object);
    }

    private static User NewUser(string id, string email, UserRole role = UserRole.User)
    {
        var user = new User(email, $"Name-{id}", null, "google");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        if (role == UserRole.Admin) user.PromoteToAdmin();
        return user;
    }

    [Fact]
    public async Task Handle_CallerIsAdmin_ReturnsMatches()
    {
        var admin = NewUser("admin-1", "admin@example.com", UserRole.Admin);
        var target1 = NewUser("u-1", "alice@example.com");
        var target2 = NewUser("u-2", "bob@example.com");

        _userRepo.Setup(r => r.GetByIdAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _userRepo.Setup(r => r.SearchByEmailAsync("ali", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { target1, target2 });

        var query = new SearchUsersQuery { CallerUserId = "admin-1", EmailQuery = "ali" };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("u-1");
        result[0].Email.Should().Be("alice@example.com");
        result[0].Role.Should().Be("User");
    }

    [Fact]
    public async Task Handle_ExcludesCallerFromResults()
    {
        var admin = NewUser("admin-1", "admin@example.com", UserRole.Admin);
        var other = NewUser("u-1", "other@example.com");

        _userRepo.Setup(r => r.GetByIdAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _userRepo.Setup(r => r.SearchByEmailAsync("example", 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { admin, other });

        var query = new SearchUsersQuery { CallerUserId = "admin-1", EmailQuery = "example" };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("u-1");
    }

    [Fact]
    public async Task Handle_CallerNotAdmin_ThrowsUnauthorized()
    {
        var caller = NewUser("u-1", "regular@example.com", UserRole.User);
        _userRepo.Setup(r => r.GetByIdAsync("u-1", It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        var query = new SearchUsersQuery { CallerUserId = "u-1", EmailQuery = "anyone" };
        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_EmptyQuery_ReturnsEmpty()
    {
        var admin = NewUser("admin-1", "admin@example.com", UserRole.Admin);
        _userRepo.Setup(r => r.GetByIdAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        var query = new SearchUsersQuery { CallerUserId = "admin-1", EmailQuery = "" };
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
        _userRepo.Verify(r => r.SearchByEmailAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
