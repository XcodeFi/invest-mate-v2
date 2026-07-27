using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Tiền mặt còn lại trong một danh mục. Công thức thống nhất với capital-flows hero card
/// và <c>CashFlowAdjustedReturnService</c> — có tính lãi/lỗ đã thực hiện.
/// </summary>
public static class PortfolioCashCalculator
{
    /// <param name="netFlowExcludingSeed">
    /// Tổng nạp/rút SAU khi tạo danh mục. Truyền từ
    /// <c>ICapitalFlowRepository.GetTotalFlowByPortfolioIdAsync</c> — hàm đó đã lọc seed deposit,
    /// nên vốn ban đầu không bị đếm hai lần.
    /// </param>
    public static decimal Compute(decimal initialCapital, decimal netFlowExcludingSeed, IEnumerable<Trade> trades)
    {
        decimal grossBuys = 0m, grossSells = 0m;

        foreach (var t in trades)
        {
            if (t.TradeType == TradeType.BUY)
                grossBuys += t.Quantity * t.Price + t.Fee + t.Tax;
            else
                grossSells += t.Quantity * t.Price - t.Fee - t.Tax;
        }

        return initialCapital + netFlowExcludingSeed - grossBuys + grossSells;
    }
}
