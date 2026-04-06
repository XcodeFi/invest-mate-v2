namespace InvestmentApp.Application.Common.Interfaces;

public interface IVietstockEventProvider
{
    Task<IEnumerable<VietstockNewsDto>> GetNewsAsync(string symbol, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<IEnumerable<VietstockEventDto>> GetEventsAsync(string symbol, int eventTypeId = 1, int page = 1, int pageSize = 20, CancellationToken ct = default);
}

public class VietstockNewsDto
{
    public string StockCode { get; set; } = null!;
    public long ArticleId { get; set; }
    public string Title { get; set; } = null!;
    public string? Head { get; set; }
    public DateTime PublishTime { get; set; }
    public string? Url { get; set; }
    public string? Source { get; set; }
}

public class VietstockEventDto
{
    public long EventId { get; set; }
    public int EventTypeId { get; set; }
    public int ChannelId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Note { get; set; }
    public string? Title { get; set; }
    public DateTime EventDate { get; set; }
    public string? FileUrl { get; set; }
}
