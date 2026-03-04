namespace InvestmentApp.Application.Portfolios.Queries;

public class PortfolioPnLSummary
{
    public decimal TotalRealizedPnL { get; set; }
    public decimal TotalUnrealizedPnL { get; set; }
    public decimal TotalPortfolioValue { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalPnL => TotalRealizedPnL + TotalUnrealizedPnL;
    public decimal TotalReturnPercentage => TotalInvested > 0 ? (TotalPnL / TotalInvested) * 100 : 0;
}

public class PositionPnL
{
    public string Symbol { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue => Quantity * CurrentPrice;
    public decimal UnrealizedPnL => MarketValue - (Quantity * AverageCost);
    public decimal UnrealizedPnLPercentage => AverageCost > 0 ? (UnrealizedPnL / (Quantity * AverageCost)) * 100 : 0;
}