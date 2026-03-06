using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Backtests.Queries.GetBacktest;

public record GetBacktestQuery(string BacktestId, string UserId) : IRequest<Backtest?>;

public class GetBacktestQueryHandler : IRequestHandler<GetBacktestQuery, Backtest?>
{
    private readonly IBacktestRepository _repository;

    public GetBacktestQueryHandler(IBacktestRepository repository) => _repository = repository;

    public async Task<Backtest?> Handle(GetBacktestQuery request, CancellationToken cancellationToken)
    {
        var backtest = await _repository.GetByIdAsync(request.BacktestId, cancellationToken);
        if (backtest == null || backtest.UserId != request.UserId) return null;
        return backtest;
    }
}
