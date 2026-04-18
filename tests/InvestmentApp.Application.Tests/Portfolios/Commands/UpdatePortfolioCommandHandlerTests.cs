using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Commands.UpdatePortfolio;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Portfolios.Commands;

public class UpdatePortfolioCommandHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<IAuditService> _auditService;
    private readonly UpdatePortfolioCommandHandler _handler;

    public UpdatePortfolioCommandHandlerTests()
    {
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _auditService = new Mock<IAuditService>();
        _handler = new UpdatePortfolioCommandHandler(_portfolioRepo.Object, _auditService.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesNameOnly()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Old Name", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        var command = new UpdatePortfolioCommand
        {
            Id = portfolio.Id,
            UserId = "user1",
            Name = "New Name"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        portfolio.Name.Should().Be("New Name");
        portfolio.InitialCapital.Should().Be(100_000_000m); // unchanged — not editable
    }

    [Fact]
    public async Task Handle_WrongUserId_ReturnsFalseAndDoesNotUpdate()
    {
        // Arrange
        var portfolio = new Portfolio("user1", "Old Name", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByIdAsync(portfolio.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);

        var command = new UpdatePortfolioCommand
        {
            Id = portfolio.Id,
            UserId = "otherUser",
            Name = "Hijacked"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        portfolio.Name.Should().Be("Old Name");
    }

    [Fact]
    public async Task Handle_PortfolioNotFound_ReturnsFalse()
    {
        // Arrange
        _portfolioRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        var command = new UpdatePortfolioCommand
        {
            Id = "missing",
            UserId = "user1",
            Name = "Anything"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}
