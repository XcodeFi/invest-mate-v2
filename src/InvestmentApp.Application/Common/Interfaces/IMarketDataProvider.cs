using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Market data provider interface for fetching real-time and historical prices.
/// </summary>
public interface IMarketDataProvider
{
    Task<StockPriceData?> GetCurrentPriceAsync(string symbol, CancellationToken cancellationToken = default);
    Task<List<StockPriceData>> GetHistoricalPricesAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lịch sử giá theo <b>phiên</b>, sâu nhất mà nguồn cho (24hmoney: ~65 phiên).
    /// </summary>
    /// <remarks>
    /// Tách khỏi <see cref="GetHistoricalPricesAsync"/> có chủ đích: hàm đó chọn độ chi tiết theo
    /// độ rộng cửa sổ, và mọi cửa sổ &gt; 35 ngày rơi vào thanh 3 ngày trở lên. Ước lượng hiệp
    /// phương sai cần lợi suất theo phiên, nên nó phải có đường đi riêng — xem ADR-0014.
    /// </remarks>
    Task<List<StockPriceData>> GetDailyHistoryAsync(string symbol, CancellationToken cancellationToken = default);
    Task<Dictionary<string, StockPriceData>> GetBatchPricesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default);
    Task<MarketIndexData?> GetIndexDataAsync(string indexSymbol, CancellationToken cancellationToken = default);
}

public class StockPriceData
{
    public string Symbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }

    /// <summary>
    /// <c>true</c> khi giá đã được nguồn điều chỉnh theo sự kiện quyền (cổ tức, thưởng, chia tách).
    /// Nguồn dự phòng (graph, chỉ có giá đóng cửa) là <c>false</c>.
    /// </summary>
    public bool IsAdjusted { get; set; }

    /// <summary>
    /// Ngày của nến cuối trong chuỗi ĐÃ điều chỉnh mà nến này thuộc về (null khi nến chưa điều chỉnh); mọi nến của một chuỗi mang cùng giá trị. Khi nguồn đang bị chặn và
    /// phải dùng bản lưu cũ, ngày này lùi lại so với phiên mới nhất, nên người dùng và công cụ phía sau biết dữ liệu cũ tới đâu.
    /// </summary>
    public DateTime? AdjustedAsOf { get; set; }
}

public class MarketIndexData
{
    public string IndexSymbol { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Close { get; set; }
    public decimal PriorClose { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Average { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public decimal Value { get; set; }

    // Advance / Decline
    public int Advance { get; set; }
    public int Decline { get; set; }
    public int NoChange { get; set; }
    public int Ceiling { get; set; }
    public int Floor { get; set; }

    // Foreign trading (tỷ VND)
    public decimal ForeignBuyValue { get; set; }
    public decimal ForeignSellValue { get; set; }
    public decimal ForeignWeekBuyValue { get; set; }
    public decimal ForeignWeekSellValue { get; set; }
    public decimal ForeignMonthBuyValue { get; set; }
    public decimal ForeignMonthSellValue { get; set; }
}
