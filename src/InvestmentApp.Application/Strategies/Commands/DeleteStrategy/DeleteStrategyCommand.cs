using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Strategies.Commands.DeleteStrategy;

public class DeleteStrategyCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class DeleteStrategyCommandHandler : IRequestHandler<DeleteStrategyCommand, Unit>
{
    private readonly IStrategyRepository _strategyRepository;

    public DeleteStrategyCommandHandler(IStrategyRepository strategyRepository)
    {
        _strategyRepository = strategyRepository;
    }

    public async Task<Unit> Handle(DeleteStrategyCommand request, CancellationToken cancellationToken)
    {
        var strategy = await _strategyRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Strategy {request.Id} not found");

        if (strategy.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to delete this strategy");

        strategy.SoftDelete();
        await _strategyRepository.UpdateAsync(strategy, cancellationToken);
        return Unit.Value;
    }
}
