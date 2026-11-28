namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Chuỗi giá ngày ĐÃ điều chỉnh gần nhất mà nguồn trả về thành công, lưu bền theo mã.
/// </summary>
/// <param name="FetchedAtUtc">Thời điểm lấy thành công chuỗi này (UTC).</param>
public sealed record AdjustedPriceSeriesSnapshot(
    string Symbol, DateTime FetchedAtUtc, IReadOnlyList<StockPriceData> Bars);

/// <summary>
/// Kho bền cho chuỗi giá đã điều chỉnh. Tách khỏi <c>stock_prices</c> có chủ đích: kho đó chứa giá thô
/// do job chụp giá ghi hằng ngày, trộn chuỗi đã điều chỉnh vào sẽ làm sai cả hai. Mỗi mã một bản ghi,
/// ghi đè khi lấy thành công. Chỉ là bản sao dự phòng khi nguồn đang chặn, không phải nguồn sự thật.
/// </summary>
public interface IAdjustedPriceSeriesStore
{
    Task<AdjustedPriceSeriesSnapshot?> GetAsync(string symbol, CancellationToken cancellationToken = default);

    Task SaveAsync(string symbol, DateTime fetchedAtUtc, IReadOnlyList<StockPriceData> bars,
        CancellationToken cancellationToken = default);
}
