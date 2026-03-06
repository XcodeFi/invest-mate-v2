using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Backtests.Queries.GetBacktests;

public record GetBacktestsQuery(string UserId) : IRequest<IEnumerable<Backtest>>;

public class GetBacktestsQueryHandler : IRequestHandler<GetBacktestsQuery, IEnumerable<Backtest>>
{
    private readonly IBacktestRepository _repository;

    public GetBacktestsQueryHandler(IBacktestRepository repository) => _repository = repository;

    public Task<IEnumerable<Backtest>> Handle(GetBacktestsQuery request, CancellationToken cancellationToken)
        => _repository.GetByUserIdAsync(request.UserId, cancellationToken);
}
