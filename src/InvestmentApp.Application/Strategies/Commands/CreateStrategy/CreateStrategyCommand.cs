using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Strategies.Commands.CreateStrategy;

public class CreateStrategyCommand : IRequest<string>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public string EntryRules { get; set; } = string.Empty;
    public string ExitRules { get; set; } = string.Empty;
    public string RiskRules { get; set; } = string.Empty;
    public string TimeFrame { get; set; } = "Swing";
    public string MarketCondition { get; set; } = "Trending";
    public decimal? SuggestedSlPercent { get; set; }
    public decimal? SuggestedRrRatio { get; set; }
    public string? SuggestedSlMethod { get; set; }
}

public class CreateStrategyCommandHandler : IRequestHandler<CreateStrategyCommand, string>
{
    private readonly IStrategyRepository _strategyRepository;

    public CreateStrategyCommandHandler(IStrategyRepository strategyRepository)
    {
        _strategyRepository = strategyRepository;
    }

    public async Task<string> Handle(CreateStrategyCommand request, CancellationToken cancellationToken)
    {
        var strategy = new Strategy(
            request.UserId, request.Name, request.Description,
            request.EntryRules, request.ExitRules, request.RiskRules,
            request.TimeFrame, request.MarketCondition,
            request.SuggestedSlPercent, request.SuggestedRrRatio,
            request.SuggestedSlMethod
        );

        await _strategyRepository.AddAsync(strategy, cancellationToken);
        return strategy.Id;
    }
}
