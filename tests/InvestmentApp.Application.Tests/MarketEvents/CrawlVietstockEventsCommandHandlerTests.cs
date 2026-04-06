using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketEvents.Commands.CrawlVietstockEvents;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.MarketEvents;

public class CrawlVietstockEventsCommandHandlerTests
{
    private readonly Mock<IVietstockEventProvider> _vietstockProvider;
    private readonly Mock<IMarketEventRepository> _marketEventRepo;
    private readonly CrawlVietstockEventsCommandHandler _handler;

    public CrawlVietstockEventsCommandHandlerTests()
    {
        _vietstockProvider = new Mock<IVietstockEventProvider>();
        _marketEventRepo = new Mock<IMarketEventRepository>();
        _handler = new CrawlVietstockEventsCommandHandler(
            _vietstockProvider.Object,
            _marketEventRepo.Object);
    }

    [Fact]
    public async Task Handle_CrawlNews_AddsNewMarketEvents()
    {
        // Arrange
        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VND", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>());

        _vietstockProvider.Setup(p => p.GetNewsAsync("VND", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VietstockNewsDto>
            {
                new() { StockCode = "VND", ArticleId = 1, Title = "VND thông báo cổ tức", PublishTime = new DateTime(2025, 3, 15), Url = "https://example.com" },
                new() { StockCode = "VND", ArticleId = 2, Title = "VND kết quả kinh doanh Q1", PublishTime = new DateTime(2025, 3, 20) }
            });

        var command = new CrawlVietstockEventsCommand { Symbol = "VND", CrawlNews = true, CrawlEvents = false };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.NewsAdded.Should().Be(2);
        result.DuplicatesSkipped.Should().Be(0);
        _marketEventRepo.Verify(r => r.AddAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_DuplicateNews_SkipsDuplicates()
    {
        // Arrange — existing event with same Symbol + Title + Date
        var existing = new MarketEvent("VND", MarketEventType.News, "VND thông báo cổ tức",
            new DateTime(2025, 3, 15));
        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VND", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent> { existing });

        _vietstockProvider.Setup(p => p.GetNewsAsync("VND", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VietstockNewsDto>
            {
                new() { StockCode = "VND", ArticleId = 1, Title = "VND thông báo cổ tức", PublishTime = new DateTime(2025, 3, 15) },
                new() { StockCode = "VND", ArticleId = 2, Title = "VND tin mới", PublishTime = new DateTime(2025, 3, 20) }
            });

        var command = new CrawlVietstockEventsCommand { Symbol = "VND", CrawlNews = true, CrawlEvents = false };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.NewsAdded.Should().Be(1);
        result.DuplicatesSkipped.Should().Be(1);
        _marketEventRepo.Verify(r => r.AddAsync(It.IsAny<MarketEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DifferentTitleSameDate_CreatesNew()
    {
        // Arrange — existing event with same date but different title
        var existing = new MarketEvent("VND", MarketEventType.News, "VND tin cũ",
            new DateTime(2025, 3, 15));
        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VND", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent> { existing });

        _vietstockProvider.Setup(p => p.GetNewsAsync("VND", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VietstockNewsDto>
            {
                new() { StockCode = "VND", ArticleId = 1, Title = "VND tin mới", PublishTime = new DateTime(2025, 3, 15) }
            });

        var command = new CrawlVietstockEventsCommand { Symbol = "VND", CrawlNews = true, CrawlEvents = false };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.NewsAdded.Should().Be(1);
        result.DuplicatesSkipped.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CrawlEvents_MapsChannelIdToEventType()
    {
        // Arrange
        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VND", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>());

        _vietstockProvider.Setup(p => p.GetEventsAsync("VND", 1, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VietstockEventDto>
            {
                new() { EventId = 1, ChannelId = 13, Code = "VND", Name = "Trả cổ tức tiền mặt", Title = "VND cổ tức 2024", EventDate = new DateTime(2025, 6, 1) },
                new() { EventId = 2, ChannelId = 16, Code = "VND", Name = "Phát hành thêm", Title = "VND phát hành", EventDate = new DateTime(2025, 7, 1) }
            });

        var command = new CrawlVietstockEventsCommand { Symbol = "VND", CrawlNews = false, CrawlEvents = true };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.EventsAdded.Should().Be(2);
        _marketEventRepo.Verify(r => r.AddAsync(
            It.Is<MarketEvent>(e => e.EventType == MarketEventType.Dividend),
            It.IsAny<CancellationToken>()), Times.Once);
        _marketEventRepo.Verify(r => r.AddAsync(
            It.Is<MarketEvent>(e => e.EventType == MarketEventType.RightsIssue),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CrawlBothNewsAndEvents_ReturnsCombinedCounts()
    {
        // Arrange
        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VND", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>());

        _vietstockProvider.Setup(p => p.GetNewsAsync("VND", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VietstockNewsDto>
            {
                new() { StockCode = "VND", Title = "Tin 1", PublishTime = new DateTime(2025, 3, 1) }
            });

        _vietstockProvider.Setup(p => p.GetEventsAsync("VND", 1, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VietstockEventDto>
            {
                new() { EventId = 1, ChannelId = 13, Code = "VND", Name = "Cổ tức", Title = "VND cổ tức", EventDate = new DateTime(2025, 6, 1) }
            });

        var command = new CrawlVietstockEventsCommand { Symbol = "VND", CrawlNews = true, CrawlEvents = true };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.NewsAdded.Should().Be(1);
        result.EventsAdded.Should().Be(1);
        result.DuplicatesSkipped.Should().Be(0);
    }

    [Fact]
    public void MapChannelToEventType_KnownChannels_ReturnsCorrectType()
    {
        CrawlVietstockEventsCommandHandler.MapChannelToEventType(13).Should().Be(MarketEventType.Dividend);
        CrawlVietstockEventsCommandHandler.MapChannelToEventType(15).Should().Be(MarketEventType.Dividend);
        CrawlVietstockEventsCommandHandler.MapChannelToEventType(16).Should().Be(MarketEventType.RightsIssue);
        CrawlVietstockEventsCommandHandler.MapChannelToEventType(99).Should().Be(MarketEventType.News);
    }
}
