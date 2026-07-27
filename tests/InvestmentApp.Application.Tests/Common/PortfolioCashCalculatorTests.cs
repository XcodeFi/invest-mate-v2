using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Common;

/// <summary>
/// Tiền mặt còn lại trong danh mục. Trước đây bản tin hằng ngày chỉ đọc tiền từ hồ sơ
/// tài chính cá nhân nên tiền thu về từ lệnh bán bị bỏ sót hoàn toàn.
/// </summary>
public class PortfolioCashCalculatorTests
{
    private static Trade Buy(decimal qty, decimal price, decimal fee = 0, decimal tax = 0)
        => new("p1", "HHV", TradeType.BUY, qty, price, fee, tax);

    private static Trade Sell(decimal qty, decimal price, decimal fee = 0, decimal tax = 0)
        => new("p1", "HHV", TradeType.SELL, qty, price, fee, tax);

    [Fact]
    public void NoTradesNoFlows_ReturnsInitialCapital()
    {
        PortfolioCashCalculator.Compute(500_000_000m, 0m, Array.Empty<Trade>())
            .Should().Be(500_000_000m);
    }

    [Fact]
    public void BuyOnly_SubtractsGrossIncludingFeeAndTax()
    {
        // 1.000 × 10.000 = 10.000.000 + fee 15.000 + tax 5.000 = 10.020.000
        PortfolioCashCalculator.Compute(50_000_000m, 0m, new[] { Buy(1_000m, 10_000m, 15_000m, 5_000m) })
            .Should().Be(39_980_000m);
    }

    [Fact]
    public void PartialSell_AddsNetProceedsAfterFeeAndTax()
    {
        // Tái hiện kịch bản HHV: mua 29.000 @ 12.426, bán 14.500 @ 9.950.
        // buys  = 29.000 × 12.426 = 360.354.000
        // sells = 14.500 × 9.950  = 144.275.000 − fee 250.000 − tax 110.000 = 143.915.000
        var trades = new[] { Buy(29_000m, 12_426m), Sell(14_500m, 9_950m, 250_000m, 110_000m) };

        PortfolioCashCalculator.Compute(500_000_000m, 0m, trades)
            .Should().Be(500_000_000m - 360_354_000m + 143_915_000m);
    }

    [Fact]
    public void AddsNetFlowExcludingSeed()
    {
        PortfolioCashCalculator.Compute(100_000_000m, 20_000_000m, Array.Empty<Trade>())
            .Should().Be(120_000_000m);
    }

    [Fact]
    public void NegativeNetFlow_Withdrawal_ReducesCash()
    {
        PortfolioCashCalculator.Compute(100_000_000m, -30_000_000m, Array.Empty<Trade>())
            .Should().Be(70_000_000m);
    }

    [Fact]
    public void FullExitAtProfit_CashEqualsInitialPlusRealizedGain()
    {
        // Mua 1.000 @ 10.000 = 10.000.000; bán hết @ 12.000 = 12.000.000 → lãi 2.000.000
        var trades = new[] { Buy(1_000m, 10_000m), Sell(1_000m, 12_000m) };

        PortfolioCashCalculator.Compute(10_000_000m, 0m, trades)
            .Should().Be(12_000_000m);
    }
}
