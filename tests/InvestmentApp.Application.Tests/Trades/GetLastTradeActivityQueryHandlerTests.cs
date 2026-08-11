using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Trades.Queries.GetLastTradeActivity;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Trades;

public class GetLastTradeActivityQueryHandlerTests
{
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ITradeRepository> _trades = new();

    private GetLastTradeActivityQueryHandler Handler() => new(_portfolios.Object, _trades.Object);

    private static GetLastTradeActivityQuery Query() => new() { UserId = "user-1" };

    [Fact]
    public async Task NoPortfolios_ReturnsNulls_AndNeverTouchesTradeRepository()
    {
        _portfolios.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Portfolio>());

        var result = await Handler().Handle(Query(), CancellationToken.None);

        result.LastTradeDate.Should().BeNull();
        result.DaysSince.Should().BeNull();
        _trades.Verify(r => r.GetLastTradeDateByPortfolioIdsAsync(
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PortfoliosButNoTrades_ReturnsNulls()
    {
        _portfolios.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Portfolio("user-1", "Danh mục chính", 100_000_000m) });
        _trades.Setup(r => r.GetLastTradeDateByPortfolioIdsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var result = await Handler().Handle(Query(), CancellationToken.None);

        result.LastTradeDate.Should().BeNull();
        result.DaysSince.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsLastTradeDate_AndDaysSinceInVietnamCalendarDays()
    {
        var lastTrade = DateTime.UtcNow.AddDays(-12);
        _portfolios.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Portfolio("user-1", "Danh mục chính", 100_000_000m) });
        _trades.Setup(r => r.GetLastTradeDateByPortfolioIdsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastTrade);

        var result = await Handler().Handle(Query(), CancellationToken.None);

        result.LastTradeDate.Should().Be(lastTrade);
        result.DaysSince.Should().Be(12);
    }

    [Fact]
    public async Task OnlyQueriesTheCallersOwnPortfolios()
    {
        // Ca quan trọng nhất: Trade không mang UserId, quyền sở hữu đi qua Portfolio.
        // Nếu ai đó sau này truyền danh sách portfolio rộng hơn, test này phải đỏ.
        var mine = new Portfolio("user-1", "Của tôi", 100_000_000m);
        _portfolios.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mine });
        _trades.Setup(r => r.GetLastTradeDateByPortfolioIdsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);

        await Handler().Handle(Query(), CancellationToken.None);

        _trades.Verify(r => r.GetLastTradeDateByPortfolioIdsAsync(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { mine.Id })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExcludesDeletedPortfolios()
    {
        var live = new Portfolio("user-1", "Đang dùng", 100_000_000m);
        var dead = new Portfolio("user-1", "Đã xoá", 50_000_000m);
        dead.MarkAsDeleted();

        _portfolios.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { live, dead });
        _trades.Setup(r => r.GetLastTradeDateByPortfolioIdsAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);

        await Handler().Handle(Query(), CancellationToken.None);

        _trades.Verify(r => r.GetLastTradeDateByPortfolioIdsAsync(
            It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { live.Id })),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
