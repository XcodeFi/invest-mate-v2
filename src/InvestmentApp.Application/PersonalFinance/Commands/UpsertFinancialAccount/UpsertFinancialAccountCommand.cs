using System.Text.Json.Serialization;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.PersonalFinance.Commands.UpsertFinancialAccount;

public class UpsertFinancialAccountCommand : IRequest<FinancialAccountDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;

    /// <summary>Null = tạo account mới. Set = update account với id đó.</summary>
    public string? AccountId { get; set; }

    public FinancialAccountType Type { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Số dư (VND). Bắt buộc trừ khi dùng Gold auto-calc (3 field Gold đủ set).</summary>
    public decimal? Balance { get; set; }

    public decimal? InterestRate { get; set; }
    public string? Note { get; set; }

    /// <summary>Gold auto-calc mode: nếu 3 field này đều set + Type=Gold, handler sẽ fetch giá qua IGoldPriceProvider.</summary>
    public GoldBrand? GoldBrand { get; set; }
    public GoldType? GoldType { get; set; }
    public decimal? GoldQuantity { get; set; }
}

public class UpsertFinancialAccountCommandHandler : IRequestHandler<UpsertFinancialAccountCommand, FinancialAccountDto>
{
    private readonly IFinancialProfileRepository _repository;
    private readonly IGoldPriceProvider _goldPriceProvider;

    public UpsertFinancialAccountCommandHandler(IFinancialProfileRepository repository, IGoldPriceProvider goldPriceProvider)
    {
        _repository = repository;
        _goldPriceProvider = goldPriceProvider;
    }

    public async Task<FinancialAccountDto> Handle(UpsertFinancialAccountCommand request, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Chưa có profile cho user {request.UserId}. Hãy tạo profile trước khi thêm account.");

        var balance = await ResolveBalanceAsync(request, cancellationToken);

        var account = profile.UpsertAccount(
            accountId: request.AccountId,
            type: request.Type,
            name: request.Name,
            balance: balance,
            interestRate: request.InterestRate,
            note: request.Note,
            goldBrand: request.GoldBrand,
            goldType: request.GoldType,
            goldQuantity: request.GoldQuantity);

        await _repository.UpdateAsync(profile, cancellationToken);

        return PersonalFinanceMapper.ToDto(account);
    }

    /// <summary>
    /// Gold auto-calc: nếu Type=Gold + 3 Gold field đủ → Balance = quantity × BuyPrice từ provider.
    /// BuyPrice = giá tiệm mua vào = giá user bán được → định giá đúng tài sản đang giữ nếu thanh khoản ngay.
    /// (SellPrice = giá tiệm bán ra, chỉ liên quan khi user đi mua thêm.)
    /// Nếu provider trả null → throw (không silent fallback).
    /// Securities: Balance dùng 0 mặc định (live value tính qua PnLService).
    /// Các type khác (Savings/Emergency/IdleCash + Gold manual): Balance bắt buộc — null → throw để không silent tạo account ₫0.
    /// </summary>
    private async Task<decimal> ResolveBalanceAsync(UpsertFinancialAccountCommand request, CancellationToken cancellationToken)
    {
        var isGoldAutoCalc = request.Type == FinancialAccountType.Gold
            && request.GoldBrand.HasValue
            && request.GoldType.HasValue
            && request.GoldQuantity.HasValue;

        if (isGoldAutoCalc)
        {
            var price = await _goldPriceProvider.GetPriceAsync(
                request.GoldBrand!.Value,
                request.GoldType!.Value,
                cancellationToken);

            if (price is null)
                throw new InvalidOperationException(
                    $"Không lấy được giá vàng {request.GoldBrand} {request.GoldType} từ provider. Thử lại sau hoặc nhập Balance tay.");

            return request.GoldQuantity!.Value * price.BuyPrice;
        }

        // Securities auto-sync from PnLService → default 0 if not provided (stored value is overridden anyway)
        if (request.Type == FinancialAccountType.Securities)
            return request.Balance ?? 0m;

        // Non-Gold-autocalc + non-Securities → Balance bắt buộc
        if (!request.Balance.HasValue)
            throw new InvalidOperationException("Balance là bắt buộc khi không dùng Gold auto-calc.");

        return request.Balance.Value;
    }
}
