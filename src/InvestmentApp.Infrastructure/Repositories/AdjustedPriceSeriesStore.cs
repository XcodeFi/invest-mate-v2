using InvestmentApp.Application.Interfaces;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

/// <summary>Bản ghi một mã: <c>_id</c> chính là mã nên không cần chỉ mục thêm, ghi đè bằng một lệnh upsert.</summary>
public class AdjustedPriceSeriesDocument
{
    [BsonId]
    public string Id { get; set; } = null!;

    public string Symbol { get; set; } = null!;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime FetchedAt { get; set; }

    public List<AdjustedPriceBarDocument> Bars { get; set; } = new();
}

public class AdjustedPriceBarDocument
{
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

public class AdjustedPriceSeriesStore : IAdjustedPriceSeriesStore
{
    public const string CollectionName = "adjusted_price_series";

    private readonly IMongoCollection<AdjustedPriceSeriesDocument> _collection;

    public AdjustedPriceSeriesStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<AdjustedPriceSeriesDocument>(CollectionName);
    }

    public async Task<AdjustedPriceSeriesSnapshot?> GetAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var id = Normalize(symbol);
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync(cancellationToken);
        return doc == null ? null : ToSnapshot(doc);
    }

    public async Task SaveAsync(string symbol, DateTime fetchedAtUtc, IReadOnlyList<StockPriceData> bars,
        CancellationToken cancellationToken = default)
    {
        var doc = ToDocument(Normalize(symbol), fetchedAtUtc, bars);
        await _collection.ReplaceOneAsync(d => d.Id == doc.Id, doc,
            new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    private static string Normalize(string symbol) => symbol.ToUpperInvariant().Trim();

    public static AdjustedPriceSeriesDocument ToDocument(string symbol, DateTime fetchedAtUtc, IReadOnlyList<StockPriceData> bars) => new()
    {
        Id = symbol,
        Symbol = symbol,
        FetchedAt = DateTime.SpecifyKind(fetchedAtUtc, DateTimeKind.Utc),
        Bars = bars.Select(b => new AdjustedPriceBarDocument
        {
            Date = DateTime.SpecifyKind(b.Date, DateTimeKind.Utc),
            Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Volume = b.Volume
        }).ToList()
    };

    public static AdjustedPriceSeriesSnapshot ToSnapshot(AdjustedPriceSeriesDocument doc) => new(
        doc.Symbol,
        DateTime.SpecifyKind(doc.FetchedAt, DateTimeKind.Utc),
        doc.Bars.OrderBy(b => b.Date).Select(b => new StockPriceData
        {
            Symbol = doc.Symbol,
            Date = b.Date.Date,
            Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Volume = b.Volume,
            IsAdjusted = true   // chỉ lưu chuỗi đã điều chỉnh từ UDF
        }).ToList());
}
