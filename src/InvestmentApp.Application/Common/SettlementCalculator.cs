using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Chu kỳ thanh toán T+2 của chứng khoán Việt Nam: bán hôm nay thì tiền về sau 2 phiên
/// giao dịch. Hàm thuần, không I/O — tập ngày nghỉ do caller nạp từ
/// <c>IMarketClosureRepository</c> rồi truyền vào, nên test được không cần DB.
/// </summary>
public static class SettlementCalculator
{
    public const int SettlementSessions = 2;

    /// <summary>Phiên giao dịch = không phải T7/CN và không nằm trong danh sách nghỉ lễ.</summary>
    public static bool IsTradingDay(DateTime date, IReadOnlySet<DateOnly> closedDates)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
           && !closedDates.Contains(DateOnly.FromDateTime(date.Date));

    /// <summary>Ngày tiền về ví: <paramref name="tradeDate"/> cộng 2 phiên giao dịch.</summary>
    public static DateTime SettlementDateOf(DateTime tradeDate, IReadOnlySet<DateOnly> closedDates)
    {
        var date = tradeDate.Date;
        var counted = 0;

        while (counted < SettlementSessions)
        {
            date = date.AddDays(1);
            if (IsTradingDay(date, closedDates)) counted++;
        }

        return date;
    }

    /// <summary>
    /// Tiền bán chưa về ví tại ngày <paramref name="asOfVnDate"/> (phải là ngày lịch VN —
    /// dùng <see cref="VietnamDate.Today"/>), kèm mốc về XA NHẤT trong các lệnh còn chờ.
    /// </summary>
    public static (decimal Amount, DateTime? LastArrivalDate) PendingSellProceeds(
        IEnumerable<Trade> trades, DateTime asOfVnDate, IReadOnlySet<DateOnly> closedDates)
    {
        var asOf = asOfVnDate.Date;
        var total = 0m;
        DateTime? last = null;

        foreach (var trade in trades)
        {
            if (trade.TradeType != TradeType.SELL) continue;

            // Quy về NGÀY LỊCH VN, không dùng .Date trần: `Trade.TradeDate` không có
            // [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] nên lệnh ghi ngày 13/08 được Mongo
            // lưu thành 2026-08-12T17:00:00Z. Đọc .Date ra 12/08 là lùi một ngày, và tiền bị
            // đánh dấu đã về sớm một ngày. Phép quy này đúng cho cả bản ghi lưu nửa đêm UTC thật.
            var arrival = SettlementDateOf(VietnamDate.DayOf(trade.TradeDate), closedDates);
            if (arrival <= asOf) continue;

            total += trade.Quantity * trade.Price - trade.Fee - trade.Tax;
            if (last is null || arrival > last.Value) last = arrival;
        }

        return (total, last);
    }
}
