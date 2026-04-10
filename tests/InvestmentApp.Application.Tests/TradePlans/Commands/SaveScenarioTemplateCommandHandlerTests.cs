using FluentAssertions;
using Moq;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.SaveScenarioTemplate;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.TradePlans.Commands;

public class SaveScenarioTemplateCommandHandlerTests
{
    private readonly Mock<IScenarioTemplateRepository> _repo;
    private readonly SaveScenarioTemplateCommandHandler _handler;

    public SaveScenarioTemplateCommandHandlerTests()
    {
        _repo = new Mock<IScenarioTemplateRepository>();
        _handler = new SaveScenarioTemplateCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_SaveTemplate_ShouldRoundTripFieldsCorrectly()
    {
        // Arrange
        ScenarioTemplate? captured = null;
        _repo.Setup(r => r.CreateAsync(It.IsAny<ScenarioTemplate>()))
            .Callback<ScenarioTemplate>(t => captured = t)
            .Returns(Task.CompletedTask);

        var command = new SaveScenarioTemplateCommand
        {
            UserId = "user-1",
            Name = "My Custom Template",
            Description = "Chiến lược chốt lời nhanh",
            Nodes = new List<SaveScenarioNodeDto>
            {
                new()
                {
                    NodeId = "n1",
                    ParentId = null,
                    Order = 0,
                    Label = "Chốt lời 50%",
                    ConditionType = "PricePercentChange",
                    ConditionValue = 50,
                    ActionType = "SellPercent",
                    ActionValue = 50
                },
                new()
                {
                    NodeId = "n2",
                    ParentId = "n1",
                    Order = 0,
                    Label = "Chốt hết",
                    ConditionType = "PricePercentChange",
                    ConditionValue = 100,
                    ActionType = "SellAll"
                }
            }
        };

        // Act
        var id = await _handler.Handle(command, CancellationToken.None);

        // Assert
        id.Should().NotBeNullOrEmpty();
        captured.Should().NotBeNull();
        captured!.UserId.Should().Be("user-1");
        captured.Name.Should().Be("My Custom Template");
        captured.Description.Should().Be("Chiến lược chốt lời nhanh");
        captured.Nodes.Should().HaveCount(2);
        captured.Nodes[0].Label.Should().Be("Chốt lời 50%");
        captured.Nodes[0].ActionType.Should().Be(ScenarioActionType.SellPercent);
        captured.Nodes[0].ActionValue.Should().Be(50);
        captured.Nodes[1].ParentId.Should().Be("n1");
        captured.Nodes[1].ActionType.Should().Be(ScenarioActionType.SellAll);
        captured.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _repo.Verify(r => r.CreateAsync(It.IsAny<ScenarioTemplate>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SaveTemplate_ShouldStorePlaceholderValues()
    {
        // Arrange — nodes use relative/placeholder values, not specific prices
        ScenarioTemplate? captured = null;
        _repo.Setup(r => r.CreateAsync(It.IsAny<ScenarioTemplate>()))
            .Callback<ScenarioTemplate>(t => captured = t)
            .Returns(Task.CompletedTask);

        var command = new SaveScenarioTemplateCommand
        {
            UserId = "user-1",
            Name = "Placeholder Test",
            Description = "Test placeholder values",
            Nodes = new List<SaveScenarioNodeDto>
            {
                new()
                {
                    NodeId = "p1",
                    ParentId = null,
                    Order = 0,
                    Label = "Stop loss",
                    ConditionType = "PriceBelow",
                    ConditionValue = 0, // placeholder: 0 means "use plan stopLoss"
                    ActionType = "SellAll"
                },
                new()
                {
                    NodeId = "p2",
                    ParentId = null,
                    Order = 1,
                    Label = "Take profit at % change",
                    ConditionType = "PricePercentChange",
                    ConditionValue = 60, // relative percentage, not a specific price
                    ActionType = "SellPercent",
                    ActionValue = 30
                }
            }
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — values should be stored as-is (placeholder/relative)
        captured.Should().NotBeNull();
        captured!.Nodes[0].ConditionValue.Should().Be(0); // placeholder preserved
        captured.Nodes[0].ConditionType.Should().Be(ScenarioConditionType.PriceBelow);
        captured.Nodes[1].ConditionValue.Should().Be(60); // relative % preserved
        captured.Nodes[1].ActionValue.Should().Be(30);
    }
}
