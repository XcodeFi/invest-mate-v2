using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Strategies.Queries.GetStrategies;

public class GetStrategiesQuery : IRequest<IEnumerable<StrategyDto>>
{
    public string UserId { get; set; } = null!;
}

public class StrategyDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string EntryRules { get; set; } = string.Empty;
    public string ExitRules { get; set; } = string.Empty;
    public string RiskRules { get; set; } = string.Empty;
    public string TimeFrame { get; set; } = string.Empty;
    public string MarketCondition { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GetStrategiesQueryHandler : IRequestHandler<GetStrategiesQuery, IEnumerable<StrategyDto>>
{
    private readonly IStrategyRepository _strategyRepository;

    public GetStrategiesQueryHandler(IStrategyRepository strategyRepository)
    {
        _strategyRepository = strategyRepository;
    }

    public async Task<IEnumerable<StrategyDto>> Handle(GetStrategiesQuery request, CancellationToken cancellationToken)
    {
        var strategies = await _strategyRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return strategies.Select(s => new StrategyDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            EntryRules = s.EntryRules,
            ExitRules = s.ExitRules,
            RiskRules = s.RiskRules,
            TimeFrame = s.TimeFrame,
            MarketCondition = s.MarketCondition,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        });
    }
}
