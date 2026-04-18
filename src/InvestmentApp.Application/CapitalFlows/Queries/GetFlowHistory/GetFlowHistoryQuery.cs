using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.CapitalFlows.Queries.GetFlowHistory;

public class GetFlowHistoryQuery : IRequest<CapitalFlowHistoryDto>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class CapitalFlowHistoryDto
{
    public string PortfolioId { get; set; } = null!;
    public List<CapitalFlowItemDto> Flows { get; set; } = new();
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal TotalDividends { get; set; }
    public decimal NetCashFlow { get; set; }
}

public class CapitalFlowItemDto
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime FlowDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsSeedDeposit { get; set; }
}

public class GetFlowHistoryQueryHandler : IRequestHandler<GetFlowHistoryQuery, CapitalFlowHistoryDto>
{
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetFlowHistoryQueryHandler(
        ICapitalFlowRepository capitalFlowRepository,
        IPortfolioRepository portfolioRepository)
    {
        _capitalFlowRepository = capitalFlowRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<CapitalFlowHistoryDto> Handle(GetFlowHistoryQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        IEnumerable<Domain.Entities.CapitalFlow> flows;
        if (request.From.HasValue && request.To.HasValue)
        {
            flows = await _capitalFlowRepository.GetByPortfolioIdAsync(request.PortfolioId, request.From.Value, request.To.Value, cancellationToken);
        }
        else
        {
            flows = await _capitalFlowRepository.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken);
        }

        var flowList = flows.ToList();

        // Aggregates exclude seed Deposit so summaries reflect "user activity
        // after portfolio creation". The Flows list keeps the seed record for
        // full audit trail.
        var userFlows = flowList.Where(f => !f.IsSeedDeposit).ToList();

        return new CapitalFlowHistoryDto
        {
            PortfolioId = request.PortfolioId,
            Flows = flowList.Select(f => new CapitalFlowItemDto
            {
                Id = f.Id,
                Type = f.Type.ToString(),
                Amount = f.Amount,
                Currency = f.Currency,
                Note = f.Note,
                FlowDate = f.FlowDate,
                CreatedAt = f.CreatedAt,
                IsSeedDeposit = f.IsSeedDeposit
            }).ToList(),
            TotalDeposits = userFlows.Where(f => f.Type == Domain.Entities.CapitalFlowType.Deposit).Sum(f => f.Amount),
            TotalWithdrawals = userFlows.Where(f => f.Type == Domain.Entities.CapitalFlowType.Withdraw).Sum(f => f.Amount),
            TotalDividends = userFlows.Where(f => f.Type == Domain.Entities.CapitalFlowType.Dividend).Sum(f => f.Amount),
            NetCashFlow = userFlows.Sum(f => f.SignedAmount)
        };
    }
}
