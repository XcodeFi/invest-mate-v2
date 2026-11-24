using InvestmentApp.Application.TradePlans.Queries.GetStrategyExpectancy;
using InvestmentApp.Domain.Services;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetKellyFromHistory;

/// <summary>
/// Kelly thận trọng từ dữ liệu R của chính người gọi (theo chiến lược nếu có, không thì tổng). Dùng lại
/// <see cref="GetStrategyExpectancyQuery"/> nên cùng tập mẫu và cùng kiểm chủ sở hữu. <c>StrategyId</c> lạ hoặc của người khác
/// cho cùng kết quả của chiến lược chưa có lệnh nào.
/// </summary>
public class GetKellyFromHistoryQuery : IRequest<ConservativeKelly>
{
    public string UserId { get; set; } = null!;
    public string? StrategyId { get; set; }
}

public class GetKellyFromHistoryQueryHandler : IRequestHandler<GetKellyFromHistoryQuery, ConservativeKelly>
{
    private readonly IMediator _mediator;

    public GetKellyFromHistoryQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<ConservativeKelly> Handle(GetKellyFromHistoryQuery request, CancellationToken ct)
    {
        var stats = await _mediator.Send(new GetStrategyExpectancyQuery { UserId = request.UserId }, ct);

        var group = string.IsNullOrEmpty(request.StrategyId)
            ? stats.Total
            : stats.Strategies.FirstOrDefault(s => s.StrategyId == request.StrategyId) ?? new ExpectancyStatsDto();

        return KellyFromHistory.Compute(group.Wins, group.Losses, group.AvgWinR, group.AvgLossR);
    }
}
