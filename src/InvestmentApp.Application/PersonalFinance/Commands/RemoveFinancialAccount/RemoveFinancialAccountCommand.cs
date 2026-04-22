using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.PersonalFinance.Commands.RemoveFinancialAccount;

public class RemoveFinancialAccountCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;

    public string AccountId { get; set; } = null!;
}

public class RemoveFinancialAccountCommandHandler : IRequestHandler<RemoveFinancialAccountCommand, Unit>
{
    private readonly IFinancialProfileRepository _repository;

    public RemoveFinancialAccountCommandHandler(IFinancialProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(RemoveFinancialAccountCommand request, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Chưa có profile cho user {request.UserId}");

        profile.RemoveAccount(request.AccountId); // throws if not found or last Securities

        await _repository.UpdateAsync(profile, cancellationToken);
        return Unit.Value;
    }
}
