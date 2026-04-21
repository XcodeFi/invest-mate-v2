using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class UserRoleTests
{
    [Fact]
    public void Constructor_NewUser_ShouldDefaultRoleToUser()
    {
        var user = new User("test@example.com", "Test User", null, "google");

        user.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public void PromoteToAdmin_RegularUser_ShouldSetRoleToAdmin()
    {
        var user = new User("test@example.com", "Test User", null, "google");

        user.PromoteToAdmin();

        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void DemoteToUser_AdminUser_ShouldSetRoleToUser()
    {
        var user = new User("test@example.com", "Test User", null, "google");
        user.PromoteToAdmin();

        user.DemoteToUser();

        user.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public void PromoteToAdmin_AlreadyAdmin_ShouldRemainAdminIdempotent()
    {
        var user = new User("test@example.com", "Test User", null, "google");
        user.PromoteToAdmin();

        user.PromoteToAdmin();

        user.Role.Should().Be(UserRole.Admin);
    }
}
