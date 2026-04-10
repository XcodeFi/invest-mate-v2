using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.UpdateReviewLessons;

public class UpdateReviewLessonsCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string PlanId { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string LessonsLearned { get; set; } = null!;
}

public class UpdateReviewLessonsCommandHandler : IRequestHandler<UpdateReviewLessonsCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;

    public UpdateReviewLessonsCommandHandler(ITradePlanRepository tradePlanRepository)
    {
        _tradePlanRepository = tradePlanRepository;
    }

    public async Task<Unit> Handle(UpdateReviewLessonsCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new Exception($"Trade plan {request.PlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        plan.UpdateReviewLessons(request.LessonsLearned);
        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);

        return Unit.Value;
    }
}
