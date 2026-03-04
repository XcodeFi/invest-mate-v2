using System.Text.Json.Serialization;
using MediatR;

namespace InvestmentApp.Application.CapitalFlows.Commands.RecordCapitalFlow;

public class RecordCapitalFlowCommand : IRequest<string>
{
    [JsonIgnore]
    public string? UserId { get; set; }
    public string PortfolioId { get; set; } = null!;
    public string Type { get; set; } = null!;  // Deposit, Withdraw, Dividend, Interest, Fee
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Note { get; set; }
    public DateTime? FlowDate { get; set; }
}
