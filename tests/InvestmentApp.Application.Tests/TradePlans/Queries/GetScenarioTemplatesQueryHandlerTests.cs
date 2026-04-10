using FluentAssertions;
using Moq;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioTemplates;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.TradePlans.Queries;

public class GetScenarioTemplatesQueryHandlerTests
{
    private readonly Mock<IScenarioTemplateRepository> _repo;
    private readonly GetScenarioTemplatesQueryHandler _handler;

    public GetScenarioTemplatesQueryHandlerTests()
    {
        _repo = new Mock<IScenarioTemplateRepository>();
        _handler = new GetScenarioTemplatesQueryHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnBothPresetsAndUserTemplates()
    {
        // Arrange
        var userTemplates = new List<ScenarioTemplate>
        {
            new()
            {
                Id = "user-tmpl-1",
                UserId = "user-1",
                Name = "My Strategy",
                Description = "Custom strategy",
                Nodes = new List<ScenarioNode>
                {
                    new()
                    {
                        NodeId = "n1",
                        ParentId = null,
                        Order = 0,
                        Label = "Chốt lời",
                        ConditionType = ScenarioConditionType.PriceAbove,
                        ConditionValue = 85_000m,
                        ActionType = ScenarioActionType.SellPercent,
                        ActionValue = 50
                    }
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(userTemplates);

        var query = new GetScenarioTemplatesQuery { UserId = "user-1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — should have 3 presets + 1 user template = 4 total
        result.Should().HaveCount(4);
        result.Take(3).Should().AllSatisfy(p => p.IsPreset.Should().BeTrue());
        result.Last().IsPreset.Should().BeFalse();
        result.Last().Id.Should().Be("user-tmpl-1");
        result.Last().Name.Should().Be("My Strategy");
    }

    [Fact]
    public async Task Handle_NoUserTemplates_ShouldReturnOnlyPresets()
    {
        // Arrange
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<ScenarioTemplate>());

        var query = new GetScenarioTemplatesQuery { UserId = "user-1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — should have exactly 3 presets
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(p => p.IsPreset.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_UserTemplatesAppearAfterPresets()
    {
        // Arrange
        var userTemplates = new List<ScenarioTemplate>
        {
            new()
            {
                Id = "ut-1",
                UserId = "user-1",
                Name = "Template A",
                Description = "Desc A",
                Nodes = new List<ScenarioNode>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = "ut-2",
                UserId = "user-1",
                Name = "Template B",
                Description = "Desc B",
                Nodes = new List<ScenarioNode>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(userTemplates);

        var query = new GetScenarioTemplatesQuery { UserId = "user-1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — 3 presets + 2 user = 5, user templates come last
        result.Should().HaveCount(5);
        var presets = result.Where(p => p.IsPreset).ToList();
        var userOnes = result.Where(p => !p.IsPreset).ToList();
        presets.Should().HaveCount(3);
        userOnes.Should().HaveCount(2);
        // Presets should appear before user templates
        var lastPresetIndex = result.ToList().IndexOf(presets.Last());
        var firstUserIndex = result.ToList().IndexOf(userOnes.First());
        lastPresetIndex.Should().BeLessThan(firstUserIndex);
    }
}
