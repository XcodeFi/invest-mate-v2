using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Application.TradePlans;

/// <summary>
/// Dựng hàm phí khứ hồi cho EV từ <see cref="IFeeCalculationService"/> sẵn có: phí mua tại giá vào + phí bán và thuế bán
/// tại giá thoát, tính cho cả vị thế. Không có dịch vụ (hoặc dịch vụ lỗi) thì phí 0: EV chưa trừ phí, ghi nhận qua
/// <c>EstimateSnapshot.FeesIncluded</c>.
/// </summary>
public static class PlanFees
{
    public static RoundTripFee? Create(IFeeCalculationService? fees)
    {
        if (fees == null) return null;
        return (entry, exitPrice, quantity) =>
        {
            try
            {
                var buy = fees.CalculateTransactionFee(new Money(entry * quantity), isBuy: true).Amount;
                var exit = new Money(exitPrice * quantity);
                var sell = fees.CalculateTransactionFee(exit, isBuy: false).Amount;
                var tax = fees.CalculateSecuritiesTax(exit, SecurityType.Stock, isBuy: false).Amount;
                return buy + sell + tax;
            }
            catch (Exception)
            {
                return 0m;
            }
        };
    }
}
