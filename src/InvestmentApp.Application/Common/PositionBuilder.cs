using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Vị thế một mã sau khi áp dụng sự kiện quyền.
/// <c>TotalQuantity</c> dùng cho mọi phép tính P&amp;L và rủi ro;
/// <c>SettledQuantity</c> là con số khớp sổ công ty chứng khoán.
/// </summary>
public sealed record AdjustedPosition(
    string Symbol,
    decimal SettledQuantity,
    decimal PendingQuantity,
    decimal TotalQuantity,
    decimal AverageCost,
    decimal TotalCost,
    decimal RealizedPnL,
    decimal DividendNet,
    decimal PendingDividend);

/// <summary>
/// Nguồn duy nhất dựng vị thế từ giao dịch + sự kiện quyền. Hàm thuần, không I/O.
/// Mọi service cần giá vốn / số lượng phải gọi vào đây thay vì tự gộp <c>Trade</c> thô.
/// </summary>
public static class PositionBuilder
{
    private sealed class State
    {
        public decimal Settled;
        public decimal Pending;
        public decimal TotalCost;
        public decimal RealizedPnL;
        public decimal DividendNet;
        public decimal PendingDividend;
        public decimal Total => Settled + Pending;
        public decimal AvgCost => Total > 0 ? TotalCost / Total : 0m;
    }

    public static IReadOnlyList<AdjustedPosition> Build(
        IEnumerable<Trade> trades,
        IEnumerable<CorporateAction> actions,
        DateTime asOf)
    {
        var asOfDate = asOf.Date;
        var states = new Dictionary<string, State>(StringComparer.OrdinalIgnoreCase);

        // Trộn hai nguồn thành một chuỗi sự kiện theo thời gian.
        // Trade trước sự kiện quyền cùng ngày; trong sự kiện quyền, tiền mặt trước cổ phiếu.
        var timeline = trades
            .Where(t => t.TradeDate.Date <= asOfDate)
            .Select(t => (Date: t.TradeDate.Date, Order: 0, Trade: (Trade?)t, Action: (CorporateAction?)null))
            .Concat(actions
                // .Date cả hai vế: bản ghi cũ trong Mongo có thể không còn là nửa đêm
                .Where(a => a.ExDate.Date <= asOfDate)
                .Select(a => (Date: a.ExDate.Date, Order: a.Type == CorporateActionType.CashDividend ? 1 : 2,
                              Trade: (Trade?)null, Action: (CorporateAction?)a)))
            .OrderBy(e => e.Date).ThenBy(e => e.Order)
            .ToList();

        foreach (var e in timeline)
        {
            if (e.Trade is { } trade)
            {
                var s = GetState(states, trade.Symbol);
                if (trade.TradeType == TradeType.BUY)
                {
                    s.TotalCost += trade.Quantity * trade.Price + trade.Fee + trade.Tax;
                    s.Settled += trade.Quantity;
                }
                else
                {
                    // Không bán được nhiều hơn đang giữ. Dữ liệu lệch (nhập thiếu lệnh mua cũ)
                    // sẽ bị chặn ở đây, thay vì đẩy số lượng và giá vốn xuống âm.
                    var sellable = Math.Min(trade.Quantity, s.Total);
                    if (sellable <= 0) continue;

                    var avg = s.AvgCost;
                    s.RealizedPnL += sellable * (trade.Price - avg) - trade.Fee - trade.Tax;
                    s.TotalCost -= sellable * avg;
                    s.Settled -= sellable;
                }
                continue;
            }

            var action = e.Action!;
            if (!states.TryGetValue(action.Symbol, out var st) || st.Total <= 0)
                continue; // chưa sở hữu tại ngày GDKHQ thì không hưởng quyền

            if (action.Type == CorporateActionType.CashDividend)
            {
                var amount = st.Total * action.NetPerShare;
                if (IsSettled(action, asOfDate)) st.DividendNet += amount;
                else st.PendingDividend += amount;
            }
            else
            {
                var before = st.Total;
                var after = Math.Floor(before * action.Multiplier);
                var added = after - before;
                if (added <= 0) continue;

                if (IsSettled(action, asOfDate)) st.Settled += added;
                else st.Pending += added;
                // TotalCost giữ nguyên → AvgCost tự động giảm
            }
        }

        return states
            .Select(kv => new AdjustedPosition(
                Symbol: kv.Key,
                SettledQuantity: kv.Value.Settled,
                PendingQuantity: kv.Value.Pending,
                TotalQuantity: kv.Value.Total,
                AverageCost: kv.Value.AvgCost,
                TotalCost: kv.Value.TotalCost,
                RealizedPnL: kv.Value.RealizedPnL,
                DividendNet: kv.Value.DividendNet,
                PendingDividend: kv.Value.PendingDividend))
            .OrderBy(p => p.Symbol, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsSettled(CorporateAction action, DateTime asOfDate)
        => action.SettledAt.HasValue && action.SettledAt.Value.Date <= asOfDate;

    private static State GetState(Dictionary<string, State> states, string symbol)
    {
        if (!states.TryGetValue(symbol, out var s))
        {
            s = new State();
            states[symbol] = s;
        }
        return s;
    }
}
