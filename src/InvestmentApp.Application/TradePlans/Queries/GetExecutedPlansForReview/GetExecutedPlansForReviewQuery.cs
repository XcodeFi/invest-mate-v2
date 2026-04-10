using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetExecutedPlansForReview;

public class GetExecutedPlansForReviewQuery : IRequest<List<TradePlanDto>>
{
    public string UserId { get; set; } = null!;
}

public class GetExecutedPlansForReviewQueryHandler : IRequestHandler<GetExecutedPlansForReviewQuery, List<TradePlanDto>>
{
    private readonly ITradePlanRepository _tradePlanRepository;

    public GetExecutedPlansForReviewQueryHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<List<TradePlanDto>> Handle(GetExecutedPlansForReviewQuery request, CancellationToken cancellationToken)
    {
        var plans = await _tradePlanRepository.GetExecutedByUserIdAsync(request.UserId, cancellationToken);
        return plans.Select(GetTradePlansQueryHandler.MapToDto).ToList();
    }
}
