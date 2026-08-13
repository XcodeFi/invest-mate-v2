using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Chu kỳ thanh toán của chứng khoán Việt Nam: bán hôm nay thì tiền về sau
/// <c>sessions</c> phiên giao dịch (T+2 hiện hành, xem <see cref="SettlementOptions"/>).
/// Hàm thuần, không I/O — tập ngày nghỉ do caller nạp từ <c>IMarketClosureRepository</c>
/// rồi truyền vào, nên test được không cần DB.
///
/// <c>sessions</c> là tham số BẮT BUỘC, không có giá trị mặc định: mặc định ở đây thì
/// một call site quên nối cấu hình sẽ im lặng chạy T+2 mãi mãi.
/// </summary>
public static class SettlementCalculator
{
    /// <summary>Phiên giao dịch = không phải T7/CN và không nằm trong danh sách nghỉ lễ.</summary>
    public static bool IsTradingDay(DateTime date, IReadOnlySet<DateOnly> closedDates)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
           && !closedDates.Contains(DateOnly.FromDateTime(date.Date));

    /// <summary>
    /// Ngày tiền về ví: <paramref name="tradeDate"/> cộng <paramref name="sessions"/> phiên
    /// giao dịch. Với <c>sessions = 0</c> (T+0) thì chính ngày khớp lệnh.
    /// </summary>
    public static DateTime SettlementDateOf(DateTime tradeDate, IReadOnlySet<DateOnly> closedDates, int sessions)
    {
        // Số âm làm vòng lặp dưới không chạy lần nào, tức biến thành T+0 trong im lặng —
        // mất hẳn khái niệm chờ về mà không có gì báo. Chặn ở đây, ồn ào.
        if (sessions < 0)
            throw new ArgumentOutOfRangeException(nameof(sessions), sessions,
                "Số phiên thanh toán không thể âm");

        var date = tradeDate.Date;
        var counted = 0;

        while (counted < sessions)
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
        IEnumerable<Trade> trades, DateTime asOfVnDate, IReadOnlySet<DateOnly> closedDates, int sessions)
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
            var arrival = SettlementDateOf(VietnamDate.DayOf(trade.TradeDate), closedDates, sessions);
            if (arrival <= asOf) continue;

            total += trade.Quantity * trade.Price - trade.Fee - trade.Tax;
            if (last is null || arrival > last.Value) last = arrival;
        }

        return (total, last);
    }
}
