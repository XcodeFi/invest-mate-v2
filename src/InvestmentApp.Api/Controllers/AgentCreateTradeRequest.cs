namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Body cho create-trade trên agent surface. Khác <c>CreateTradeCommand</c>: <see cref="PortfolioId"/> và
/// <see cref="Fee"/>/<see cref="Tax"/> đều optional — agent controller resolve (auto-pick danh mục khi chỉ có 1,
/// tự tính phí/thuế khi bỏ trống) trước khi dựng command đầy đủ để dispatch.
/// </summary>
public class AgentCreateTradeRequest
{
    public string? PortfolioId { get; set; }
    public string Symbol { get; set; } = null!;
    public string TradeType { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal? Fee { get; set; }
    public decimal? Tax { get; set; }
    public DateTime? TradeDate { get; set; }
}
