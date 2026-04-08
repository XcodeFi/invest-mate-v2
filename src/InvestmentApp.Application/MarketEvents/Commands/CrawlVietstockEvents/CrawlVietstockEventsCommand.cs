using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.MarketEvents.Commands.CrawlVietstockEvents;

public class CrawlVietstockEventsCommand : IRequest<CrawlResultDto>
{
    public string Symbol { get; set; } = null!;
    public bool CrawlNews { get; set; } = true;
    public bool CrawlEvents { get; set; } = true;
}

public class CrawlResultDto
{
    public int NewsAdded { get; set; }
    public int EventsAdded { get; set; }
    public int DuplicatesSkipped { get; set; }
}

public class CrawlVietstockEventsCommandHandler : IRequestHandler<CrawlVietstockEventsCommand, CrawlResultDto>
{
    private readonly IVietstockEventProvider _vietstockProvider;
    private readonly IMarketEventRepository _marketEventRepo;

    public CrawlVietstockEventsCommandHandler(
        IVietstockEventProvider vietstockProvider,
        IMarketEventRepository marketEventRepo)
    {
        _vietstockProvider = vietstockProvider;
        _marketEventRepo = marketEventRepo;
    }

    public async Task<CrawlResultDto> Handle(CrawlVietstockEventsCommand request, CancellationToken cancellationToken)
    {
        var symbol = request.Symbol.ToUpper().Trim();
        var result = new CrawlResultDto();

        // Load existing events for dedup
        var existing = (await _marketEventRepo.GetBySymbolAsync(symbol, cancellationToken: cancellationToken)).ToList();
        var existingKeys = new HashSet<string>(
            existing.Select(e => $"{e.Symbol}|{e.Title}|{e.EventDate:yyyy-MM-dd}"));

        if (request.CrawlNews)
        {
            var news = await _vietstockProvider.GetNewsAsync(symbol, ct: cancellationToken);
            foreach (var item in news)
            {
                var key = $"{symbol}|{item.Title}|{item.PublishTime:yyyy-MM-dd}";
                if (existingKeys.Contains(key))
                {
                    result.DuplicatesSkipped++;
                    continue;
                }

                var marketEvent = new MarketEvent(
                    symbol,
                    MarketEventType.News,
                    item.Title,
                    item.PublishTime,
                    description: item.Head,
                    source: item.Url);

                await _marketEventRepo.AddAsync(marketEvent, cancellationToken);
                existingKeys.Add(key);
                result.NewsAdded++;
            }
        }

        if (request.CrawlEvents)
        {
            var events = await _vietstockProvider.GetEventsAsync(symbol, ct: cancellationToken);
            foreach (var item in events)
            {
                var title = item.Title ?? item.Name;
                var key = $"{symbol}|{title}|{item.EventDate:yyyy-MM-dd}";
                if (existingKeys.Contains(key))
                {
                    result.DuplicatesSkipped++;
                    continue;
                }

                var eventType = MapChannelToEventType(item.ChannelId);
                var marketEvent = new MarketEvent(
                    symbol,
                    eventType,
                    title,
                    item.EventDate,
                    description: item.Note,
                    source: item.FileUrl);

                await _marketEventRepo.AddAsync(marketEvent, cancellationToken);
                existingKeys.Add(key);
                result.EventsAdded++;
            }
        }

        return result;
    }

    public static MarketEventType MapChannelToEventType(int channelId)
    {
        return channelId switch
        {
            13 => MarketEventType.Dividend,      // Cổ tức tiền mặt
            15 => MarketEventType.Dividend,      // Cổ tức cổ phiếu
            16 => MarketEventType.RightsIssue,   // Phát hành thêm
            _ => MarketEventType.News            // Default
        };
    }
}
