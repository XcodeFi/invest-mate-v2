using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.PersonalFinance.Commands.RemoveDebt;

public class RemoveDebtCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;

    public string DebtId { get; set; } = null!;
}

public class RemoveDebtCommandHandler : IRequestHandler<RemoveDebtCommand, Unit>
{
    private readonly IFinancialProfileRepository _repository;

    public RemoveDebtCommandHandler(IFinancialProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(RemoveDebtCommand request, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Chưa có profile cho user {request.UserId}");

        profile.RemoveDebt(request.DebtId); // throws if not found or Principal > 0

        await _repository.UpdateAsync(profile, cancellationToken);
        return Unit.Value;
    }
}
