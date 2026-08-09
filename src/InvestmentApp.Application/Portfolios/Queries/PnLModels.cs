namespace InvestmentApp.Application.Portfolios.Queries;

public class PortfolioPnLSummary
{
    public decimal TotalRealizedPnL { get; set; }
    public decimal TotalUnrealizedPnL { get; set; }
    public decimal TotalPortfolioValue { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalPnL => TotalRealizedPnL + TotalUnrealizedPnL;
    public decimal TotalReturnPercentage => TotalInvested > 0 ? (TotalPnL / TotalInvested) * 100 : 0;
    public decimal TotalPnLPercent => TotalReturnPercentage;
    public decimal TotalMarketValue => TotalPortfolioValue;
    public List<PositionPnL> Positions { get; set; } = new();
}

public class PositionPnL
{
    public string Symbol { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue => Quantity * CurrentPrice;
    public decimal TotalCost => Quantity * AverageCost;
    public decimal UnrealizedPnL => MarketValue - TotalCost;
    public decimal UnrealizedPnLPercentage => TotalCost > 0 ? (UnrealizedPnL / TotalCost) * 100 : 0;
    public decimal RealizedPnL { get; set; }
    public decimal TotalPnL => RealizedPnL + UnrealizedPnL;
    public decimal TotalPnLPercent => TotalCost > 0 ? (TotalPnL / TotalCost) * 100 : 0;

    /// <summary>Số lượng đã về tài khoản — khớp sổ công ty chứng khoán.</summary>
    public decimal SettledQuantity { get; set; }

    /// <summary>Cổ phiếu từ sự kiện quyền đã hưởng nhưng chưa về tài khoản.</summary>
    public decimal PendingQuantity { get; set; }

    /// <summary>Cổ tức tiền mặt đã nhận, sau thuế, luỹ kế.</summary>
    public decimal DividendNet { get; set; }

    /// <summary>Cổ tức tiền mặt đã hưởng quyền nhưng chưa về ví.</summary>
    public decimal PendingDividend { get; set; }

    /// <summary>Lãi/lỗ tổng gồm cả cổ tức — mã trả cổ tức đều nếu bỏ cột này sẽ trông như lỗ dần.</summary>
    public decimal TotalPnLWithDividend => TotalPnL + DividendNet + PendingDividend;
}