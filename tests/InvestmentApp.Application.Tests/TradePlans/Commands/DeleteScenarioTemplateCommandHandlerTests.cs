using FluentAssertions;
using Moq;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.DeleteScenarioTemplate;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

public class DeleteScenarioTemplateCommandHandlerTests
{
    private readonly Mock<IScenarioTemplateRepository> _repo;
    private readonly DeleteScenarioTemplateCommandHandler _handler;

    public DeleteScenarioTemplateCommandHandlerTests()
    {
        _repo = new Mock<IScenarioTemplateRepository>();
        _handler = new DeleteScenarioTemplateCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_DeleteExistingTemplate_ShouldCallDeleteAndSucceed()
    {
        // Arrange
        var template = new ScenarioTemplate
        {
            Id = "tmpl-1",
            UserId = "user-1",
            Name = "Test Template",
            Description = "Desc",
            Nodes = new List<ScenarioNode>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _repo.Setup(r => r.GetByIdAsync("tmpl-1")).ReturnsAsync(template);
        _repo.Setup(r => r.DeleteAsync("tmpl-1")).Returns(Task.CompletedTask);

        var command = new DeleteScenarioTemplateCommand
        {
            Id = "tmpl-1",
            UserId = "user-1"
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repo.Verify(r => r.DeleteAsync("tmpl-1"), Times.Once);
    }

    [Fact]
    public async Task Handle_DeleteNonExistentTemplate_ShouldThrow()
    {
        // Arrange
        _repo.Setup(r => r.GetByIdAsync("invalid")).ReturnsAsync((ScenarioTemplate?)null);

        var command = new DeleteScenarioTemplateCommand
        {
            Id = "invalid",
            UserId = "user-1"
        };

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_DeleteOtherUsersTemplate_ShouldThrow()
    {
        // Arrange
        var template = new ScenarioTemplate
        {
            Id = "tmpl-1",
            UserId = "user-1",
            Name = "Test Template",
            Description = "Desc",
            Nodes = new List<ScenarioNode>(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _repo.Setup(r => r.GetByIdAsync("tmpl-1")).ReturnsAsync(template);

        var command = new DeleteScenarioTemplateCommand
        {
            Id = "tmpl-1",
            UserId = "wrong-user"
        };

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
