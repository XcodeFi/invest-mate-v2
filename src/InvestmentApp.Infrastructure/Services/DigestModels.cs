namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Một dòng danh mục trong &lt;portfolio_overview&gt;. Cash null = chưa lấy được (n/a).
/// PendingCash là phần tiền bán chưa về theo T+2 — đã nằm TRONG Cash, null khi Cash null.
/// </summary>
public sealed record PortfolioDigestRow(
    string Name,
    decimal MarketValue,
    decimal? Cash,
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    decimal? PendingCash = null);

/// <summary>
/// Một dòng vị thế trong &lt;positions&gt;. Ghép <c>PositionPnL</c> với <c>PositionRiskItem</c>.
/// Các trường nullable = risk service không trả được (n/a), TRỪ StopLossPrice:
/// null ở đó nghĩa là user CHƯA ĐẶT stop-loss — một tín hiệu rủi ro thật.
/// </summary>
public sealed record PositionDigestRow(
    string Symbol,
    string PortfolioName,
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal MarketValue,
    decimal UnrealizedPnL,
    decimal UnrealizedPnLPercent,
    decimal? PositionSizePercent,
    decimal? StopLossPrice,
    decimal? DistanceToStopLossPercent,
    bool RiskDataAvailable);

/// <summary>Một lệnh trong &lt;recent_trades&gt;.</summary>
public sealed record TradeDigestRow(
    DateTime TradeDate,
    string PortfolioName,
    string Symbol,
    bool IsBuy,
    decimal Quantity,
    decimal Price,
    decimal GrossValue);
