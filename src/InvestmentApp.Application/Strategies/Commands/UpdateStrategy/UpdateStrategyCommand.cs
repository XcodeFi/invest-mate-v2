using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Strategies.Commands.UpdateStrategy;

public class UpdateStrategyCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? EntryRules { get; set; }
    public string? ExitRules { get; set; }
    public string? RiskRules { get; set; }
    public string? TimeFrame { get; set; }
    public string? MarketCondition { get; set; }
    public bool? IsActive { get; set; }
    public decimal? SuggestedSlPercent { get; set; }
    public decimal? SuggestedRrRatio { get; set; }
    public string? SuggestedSlMethod { get; set; }
}

public class UpdateStrategyCommandHandler : IRequestHandler<UpdateStrategyCommand, Unit>
{
    private readonly IStrategyRepository _strategyRepository;

    public UpdateStrategyCommandHandler(IStrategyRepository strategyRepository)
    {
        _strategyRepository = strategyRepository;
    }

    public async Task<Unit> Handle(UpdateStrategyCommand request, CancellationToken cancellationToken)
    {
        var strategy = await _strategyRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Strategy {request.Id} not found");

        if (strategy.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this strategy");

        strategy.Update(request.Name, request.Description, request.EntryRules,
            request.ExitRules, request.RiskRules, request.TimeFrame,
            request.MarketCondition, request.IsActive,
            request.SuggestedSlPercent, request.SuggestedRrRatio,
            request.SuggestedSlMethod);

        await _strategyRepository.UpdateAsync(strategy, cancellationToken);
        return Unit.Value;
    }
}
