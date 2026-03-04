using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.CapitalFlows.Queries.GetAdjustedReturn;

public class GetAdjustedReturnQuery : IRequest<AdjustedReturnDto>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class AdjustedReturnDto
{
    public string PortfolioId { get; set; } = null!;
    public decimal TimeWeightedReturn { get; set; }
    public decimal MoneyWeightedReturn { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal NetCashFlow { get; set; }
    public decimal CurrentValue { get; set; }
    public int FlowCount { get; set; }
}

public class GetAdjustedReturnQueryHandler : IRequestHandler<GetAdjustedReturnQuery, AdjustedReturnDto>
{
    private readonly ICashFlowAdjustedReturnService _returnService;
    private readonly IPortfolioRepository _portfolioRepository;

    public GetAdjustedReturnQueryHandler(
        ICashFlowAdjustedReturnService returnService,
        IPortfolioRepository portfolioRepository)
    {
        _returnService = returnService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<AdjustedReturnDto> Handle(GetAdjustedReturnQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        var summary = await _returnService.GetAdjustedReturnSummaryAsync(request.PortfolioId, cancellationToken);

        return new AdjustedReturnDto
        {
            PortfolioId = summary.PortfolioId,
            TimeWeightedReturn = summary.TimeWeightedReturn,
            MoneyWeightedReturn = summary.MoneyWeightedReturn,
            TotalDeposits = summary.TotalDeposits,
            TotalWithdrawals = summary.TotalWithdrawals,
            NetCashFlow = summary.NetCashFlow,
            CurrentValue = summary.CurrentValue,
            FlowCount = summary.FlowCount
        };
    }
}
