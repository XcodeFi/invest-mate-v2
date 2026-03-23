using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Commands.CreatePortfolio;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Portfolios.Commands;

public class CreatePortfolioCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<IAuditService> _auditService;
    private readonly CreatePortfolioCommandHandler _handler;

    public CreatePortfolioCommandHandlerTests()
    {
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _auditService = new Mock<IAuditService>();
        _handler = new CreatePortfolioCommandHandler(_portfolioRepo.Object, _auditService.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesPortfolioAndReturnsId()
    {
        // Arrange
        var command = new CreatePortfolioCommand
        {
            UserId = "user1",
            Name = "My Portfolio",
            InitialCapital = 10_000_000m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();

        _portfolioRepo.Verify(
            r => r.AddAsync(
                It.Is<Portfolio>(p =>
                    p.UserId == "user1" &&
                    p.Name == "My Portfolio" &&
                    p.InitialCapital == 10_000_000m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_AuditEntryHasCorrectDetails()
    {
        // Arrange
        var command = new CreatePortfolioCommand
        {
            UserId = "user1",
            Name = "Growth Portfolio",
            InitialCapital = 50_000_000m
        };

        AuditEntry? capturedAudit = null;
        _auditService
            .Setup(a => a.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => capturedAudit = entry)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedAudit.Should().NotBeNull();
        capturedAudit!.UserId.Should().Be("user1");
        capturedAudit.Action.Should().Be("Created");
        capturedAudit.EntityType.Should().Be("Portfolio");
        capturedAudit.EntityId.Should().Be(result);
        capturedAudit.Description.Should().Contain("Growth Portfolio");
    }

    [Fact]
    public async Task Handle_NullUserId_ThrowsArgumentNullException()
    {
        // Arrange
        var command = new CreatePortfolioCommand
        {
            UserId = null,
            Name = "My Portfolio",
            InitialCapital = 10_000_000m
        };

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("userId");
    }
}
