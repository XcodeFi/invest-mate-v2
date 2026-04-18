using FluentAssertions;
using Moq;
using InvestmentApp.Application.CapitalFlows.Commands.DeleteCapitalFlow;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.CapitalFlows.Commands;

public class DeleteCapitalFlowCommandHandlerTests
{
    private readonly Mock<ICapitalFlowRepository> _flowRepo;
    private readonly Mock<IAuditService> _auditService;
    private readonly DeleteCapitalFlowCommandHandler _handler;

    public DeleteCapitalFlowCommandHandlerTests()
    {
        _flowRepo = new Mock<ICapitalFlowRepository>();
        _auditService = new Mock<IAuditService>();
        _handler = new DeleteCapitalFlowCommandHandler(_flowRepo.Object, _auditService.Object);
    }

    [Fact]
    public async Task Handle_UserFlow_DeletesAndReturnsTrue()
    {
        // Arrange
        var flow = new CapitalFlow("p1", "user1", CapitalFlowType.Deposit, 1_000_000m);
        _flowRepo.Setup(r => r.GetByIdAsync(flow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flow);

        var command = new DeleteCapitalFlowCommand { Id = flow.Id, UserId = "user1" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _flowRepo.Verify(r => r.DeleteAsync(flow.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SeedDeposit_ReturnsFalseAndDoesNotDelete()
    {
        // Arrange
        var seedFlow = new CapitalFlow("p1", "user1", CapitalFlowType.Deposit, 100_000_000m, isSeedDeposit: true);
        _flowRepo.Setup(r => r.GetByIdAsync(seedFlow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seedFlow);

        var command = new DeleteCapitalFlowCommand { Id = seedFlow.Id, UserId = "user1" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _flowRepo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WrongUser_ReturnsFalse()
    {
        // Arrange
        var flow = new CapitalFlow("p1", "user1", CapitalFlowType.Deposit, 1_000_000m);
        _flowRepo.Setup(r => r.GetByIdAsync(flow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(flow);

        var command = new DeleteCapitalFlowCommand { Id = flow.Id, UserId = "otherUser" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _flowRepo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
