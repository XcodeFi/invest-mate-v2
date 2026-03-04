using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Constructor_ValidParameters_ShouldCreateUser()
    {
        // Arrange
        var email = "test@example.com";
        var name = "Test User";
        var provider = "google";

        // Act
        var user = new User(email, name, null, provider);

        // Assert
        user.Email.Should().Be(email);
        user.Name.Should().Be(name);
        user.Provider.Should().Be(provider);
        user.Id.Should().NotBeNullOrEmpty();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        user.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullEmail_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new User(null!, "Test User", null, "google");

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("email");
    }

    [Fact]
    public void UpdateProfile_ValidParameters_ShouldUpdateUser()
    {
        // Arrange
        var user = new User("test@example.com", "Old Name", null, "google");

        // Act
        user.UpdateProfile("New Name", "avatar.jpg");

        // Assert
        user.Name.Should().Be("New Name");
        user.Avatar.Should().Be("avatar.jpg");
    }
}