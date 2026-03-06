using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Backtests.Commands.RunBacktest;

public record RunBacktestCommand(
    string UserId,
    string StrategyId,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    decimal InitialCapital
) : IRequest<string>;

public class RunBacktestCommandHandler : IRequestHandler<RunBacktestCommand, string>
{
    private readonly IBacktestRepository _backtestRepository;
    private readonly IStrategyRepository _strategyRepository;

    public RunBacktestCommandHandler(IBacktestRepository backtestRepository, IStrategyRepository strategyRepository)
    {
        _backtestRepository = backtestRepository;
        _strategyRepository = strategyRepository;
    }

    public async Task<string> Handle(RunBacktestCommand request, CancellationToken cancellationToken)
    {
        var strategy = await _strategyRepository.GetByIdAsync(request.StrategyId, cancellationToken);
        if (strategy == null || strategy.UserId != request.UserId)
            throw new ArgumentException("Strategy not found or access denied");

        var backtest = new Backtest(
            request.UserId, request.StrategyId, request.Name,
            request.StartDate, request.EndDate, request.InitialCapital);

        await _backtestRepository.AddAsync(backtest, cancellationToken);
        return backtest.Id;
    }
}
