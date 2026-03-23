using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;
using InvestmentApp.Application.Watchlists.Dtos;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Watchlists.Commands;

public class CreateWatchlistCommandHandlerTests
{
    private readonly Mock<IWatchlistRepository> _watchlistRepo;
    private readonly CreateWatchlistCommandHandler _handler;

    public CreateWatchlistCommandHandlerTests()
    {
        _watchlistRepo = new Mock<IWatchlistRepository>();
        _handler = new CreateWatchlistCommandHandler(_watchlistRepo.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesWatchlistAndReturnsDto()
    {
        // Arrange
        var command = new CreateWatchlistCommand
        {
            UserId = "user1",
            Name = "VN30 Stocks",
            Emoji = "\U0001F525",
            IsDefault = false,
            SortOrder = 1
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<WatchlistDto>();
        result.Id.Should().NotBeNullOrEmpty();
        result.Name.Should().Be("VN30 Stocks");
        result.Emoji.Should().Be("\U0001F525");
        result.IsDefault.Should().BeFalse();
        result.SortOrder.Should().Be(1);
        result.ItemCount.Should().Be(0);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsAddAsync()
    {
        // Arrange
        var command = new CreateWatchlistCommand
        {
            UserId = "user1",
            Name = "Watchlist 1",
            Emoji = "\u2B50",
            IsDefault = true,
            SortOrder = 0
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _watchlistRepo.Verify(
            r => r.AddAsync(
                It.Is<Watchlist>(w =>
                    w.Name == "Watchlist 1" &&
                    w.IsDefault == true &&
                    w.SortOrder == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
