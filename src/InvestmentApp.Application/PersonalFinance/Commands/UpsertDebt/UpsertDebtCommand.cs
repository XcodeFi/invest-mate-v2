using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.PersonalFinance.Commands.UpsertDebt;

public class UpsertDebtCommand : IRequest<DebtDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;

    /// <summary>Null = tạo mới. Set = update existing.</summary>
    public string? DebtId { get; set; }

    public DebtType Type { get; set; }
    public string Name { get; set; } = null!;
    public decimal Principal { get; set; }
    public decimal? InterestRate { get; set; }
    public decimal? MonthlyPayment { get; set; }
    public DateTime? MaturityDate { get; set; }
    public string? Note { get; set; }
}

public class UpsertDebtCommandHandler : IRequestHandler<UpsertDebtCommand, DebtDto>
{
    private readonly IFinancialProfileRepository _repository;

    public UpsertDebtCommandHandler(IFinancialProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<DebtDto> Handle(UpsertDebtCommand request, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Chưa có profile cho user {request.UserId}. Hãy tạo profile trước khi thêm khoản nợ.");

        var debt = profile.UpsertDebt(
            debtId: request.DebtId,
            type: request.Type,
            name: request.Name,
            principal: request.Principal,
            interestRate: request.InterestRate,
            monthlyPayment: request.MonthlyPayment,
            maturityDate: request.MaturityDate,
            note: request.Note);

        await _repository.UpdateAsync(profile, cancellationToken);

        return PersonalFinanceMapper.ToDto(debt);
    }
}
