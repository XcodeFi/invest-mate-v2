using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.CapitalFlows.Commands.RecordCapitalFlow;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.CapitalFlows.Commands;

public class RecordCapitalFlowCommandHandlerTests
{
    private readonly Mock<ICapitalFlowRepository> _capitalFlowRepo;
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<IAuditService> _auditService;
    private readonly RecordCapitalFlowCommandHandler _handler;

    public RecordCapitalFlowCommandHandlerTests()
    {
        _capitalFlowRepo = new Mock<ICapitalFlowRepository>();
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _auditService = new Mock<IAuditService>();
        _handler = new RecordCapitalFlowCommandHandler(
            _capitalFlowRepo.Object,
            _portfolioRepo.Object,
            _auditService.Object);
    }

    private static RecordCapitalFlowCommand CreateValidDepositCommand() => new()
    {
        UserId = "user1",
        PortfolioId = "portfolio1",
        Type = "Deposit",
        Amount = 5_000_000m,
        Currency = "VND",
        Note = "Monthly deposit"
    };

    private void SetupPortfolioFound(string portfolioId, string userId = "user1")
    {
        var portfolio = new Portfolio(userId, "My Portfolio", 50_000_000m);
        _portfolioRepo
            .Setup(r => r.GetByIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolio);
    }

    [Fact]
    public async Task Handle_ValidDeposit_CreatesFlowAndReturnsId()
    {
        // Arrange
        var command = CreateValidDepositCommand();
        SetupPortfolioFound(command.PortfolioId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();

        _capitalFlowRepo.Verify(
            r => r.AddAsync(
                It.Is<CapitalFlow>(cf =>
                    cf.PortfolioId == "portfolio1" &&
                    cf.UserId == "user1" &&
                    cf.Type == CapitalFlowType.Deposit &&
                    cf.Amount == 5_000_000m &&
                    cf.Currency == "VND"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PortfolioNotFound_ThrowsArgumentException()
    {
        // Arrange
        var command = CreateValidDepositCommand();
        _portfolioRepo
            .Setup(r => r.GetByIdAsync(command.PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Portfolio not found*");
    }

    [Fact]
    public async Task Handle_WrongUser_ThrowsArgumentException()
    {
        // Arrange
        var command = CreateValidDepositCommand();
        // Portfolio belongs to "other-user", but command.UserId is "user1"
        SetupPortfolioFound(command.PortfolioId, userId: "other-user");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*access denied*");
    }

    [Fact]
    public async Task Handle_ValidCommand_LogsAudit()
    {
        // Arrange
        var command = CreateValidDepositCommand();
        SetupPortfolioFound(command.PortfolioId);

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
        capturedAudit.Action.Should().Be("RecordedCapitalFlow");
        capturedAudit.EntityType.Should().Be("CapitalFlow");
        capturedAudit.EntityId.Should().Be(result);
        capturedAudit.Description.Should().Contain("Deposit");
        capturedAudit.Description.Should().Contain("5,000,000");
        capturedAudit.Description.Should().Contain("VND");
    }
}
