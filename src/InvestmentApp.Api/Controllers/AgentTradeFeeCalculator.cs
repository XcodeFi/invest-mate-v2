using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Tính phí/thuế cho một giao dịch cổ phiếu dự kiến — dùng chung cho agent fees endpoint và auto-fill
/// create-trade. Mirror logic <see cref="FeesController.CalculateFees"/>: fee = phí giao dịch + VAT + thuế TNCN,
/// thuế TNCN 0.1% chỉ áp khi SELL.
/// </summary>
public static class AgentTradeFeeCalculator
{
    public static FeeCalculationResponse Calculate(
        IFeeCalculationService feeService, string? tradeType, decimal quantity, decimal price)
    {
        var amount = quantity * price;
        var money = new Money(amount, "VND");
        var isBuy = string.Equals(tradeType?.Trim(), "buy", StringComparison.OrdinalIgnoreCase);

        var summary = feeService.GetFeesSummary(money, SecurityType.Stock, isBuy, true);
        var vat = feeService.CalculateVAT(summary.TransactionFee, "TransactionFee");
        var tax = feeService.CalculateSecuritiesTax(money, SecurityType.Stock, isBuy).Amount;  // TNCN 0.1% SELL, theo config
        var totalFees = summary.TransactionFee.Amount + vat.Amount + tax;

        return new FeeCalculationResponse
        {
            TransactionFee = summary.TransactionFee.Amount,
            Tax = tax,
            Vat = vat.Amount,
            TotalFees = totalFees,
            Breakdown = new FeeBreakdown
            {
                TransactionFee = summary.TransactionFee.Amount,
                Tax = tax,
                Vat = vat.Amount
            }
        };
    }
}
