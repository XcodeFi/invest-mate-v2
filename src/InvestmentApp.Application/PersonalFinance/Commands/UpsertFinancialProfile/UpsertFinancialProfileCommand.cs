using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.PersonalFinance.Commands.UpsertFinancialProfile;

public class UpsertFinancialProfileCommand : IRequest<FinancialProfileDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;

    /// <summary>Chi tiêu trung bình/tháng. Bắt buộc khi tạo mới; optional khi update (chỉ update nếu set).</summary>
    public decimal? MonthlyExpense { get; set; }

    /// <summary>Partial rule update. Null field = không đổi.</summary>
    public int? EmergencyFundMonths { get; set; }
    public decimal? MaxInvestmentPercent { get; set; }
    public decimal? MinSavingsPercent { get; set; }
}

public class UpsertFinancialProfileCommandHandler : IRequestHandler<UpsertFinancialProfileCommand, FinancialProfileDto>
{
    private readonly IFinancialProfileRepository _repository;

    public UpsertFinancialProfileCommandHandler(IFinancialProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<FinancialProfileDto> Handle(UpsertFinancialProfileCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (existing != null)
        {
            ApplyUpdates(existing, request);
            await _repository.UpdateAsync(existing, cancellationToken);
            return PersonalFinanceMapper.ToDto(existing);
        }

        // Check soft-deleted — restore thay vì tạo mới (unique index trên UserId)
        var deleted = await _repository.GetByUserIdIncludingDeletedAsync(request.UserId, cancellationToken);
        if (deleted != null)
        {
            deleted.Restore();
            ApplyUpdates(deleted, request);
            await _repository.UpdateAsync(deleted, cancellationToken);
            return PersonalFinanceMapper.ToDto(deleted);
        }

        // New profile — MonthlyExpense bắt buộc
        if (!request.MonthlyExpense.HasValue)
            throw new InvalidOperationException("MonthlyExpense là bắt buộc khi tạo profile lần đầu");

        var profile = FinancialProfile.Create(request.UserId, request.MonthlyExpense.Value);
        ApplyRuleUpdates(profile, request);
        await _repository.AddAsync(profile, cancellationToken);
        return PersonalFinanceMapper.ToDto(profile);
    }

    private static void ApplyUpdates(FinancialProfile profile, UpsertFinancialProfileCommand request)
    {
        if (request.MonthlyExpense.HasValue)
            profile.UpdateMonthlyExpense(request.MonthlyExpense.Value);
        ApplyRuleUpdates(profile, request);
    }

    private static void ApplyRuleUpdates(FinancialProfile profile, UpsertFinancialProfileCommand request)
    {
        if (request.EmergencyFundMonths.HasValue || request.MaxInvestmentPercent.HasValue || request.MinSavingsPercent.HasValue)
        {
            profile.UpdateRules(request.EmergencyFundMonths, request.MaxInvestmentPercent, request.MinSavingsPercent);
        }
    }
}
