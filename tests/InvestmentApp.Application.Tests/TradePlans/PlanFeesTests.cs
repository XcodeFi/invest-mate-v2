using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Moq;

namespace InvestmentApp.Application.Tests.TradePlans;

public class PlanFeesTests
{
    [Fact]
    public void no_fee_service_means_no_fee_function_so_ev_is_gross_and_flagged()
        => PlanFees.Create(null).Should().BeNull();

    [Fact]
    public void round_trip_fee_is_buy_fee_plus_sell_fee_plus_sell_tax_for_the_whole_position()
    {
        var svc = new Mock<IFeeCalculationService>();
        svc.Setup(s => s.CalculateTransactionFee(It.Is<Money>(m => m.Amount == 10_000m), true, It.IsAny<bool>(), It.IsAny<SecurityType>()))
            .Returns(new Money(15m));                                                    // mua 100 × 100 = 10.000
        svc.Setup(s => s.CalculateTransactionFee(It.Is<Money>(m => m.Amount == 12_000m), false, It.IsAny<bool>(), It.IsAny<SecurityType>()))
            .Returns(new Money(18m));                                                    // bán 100 × 120 = 12.000
        svc.Setup(s => s.CalculateSecuritiesTax(It.Is<Money>(m => m.Amount == 12_000m), SecurityType.Stock, false))
            .Returns(new Money(12m));

        var fee = PlanFees.Create(svc.Object)!;

        fee(100m, 120m, 100m).Should().Be(15m + 18m + 12m);
    }

    [Fact]
    public void a_failing_fee_service_degrades_to_zero_instead_of_failing_the_request()
    {
        var svc = new Mock<IFeeCalculationService>();
        svc.Setup(s => s.CalculateTransactionFee(It.IsAny<Money>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<SecurityType>()))
            .Throws(new InvalidOperationException("cấu hình phí lỗi"));

        PlanFees.Create(svc.Object)!(100m, 120m, 100m).Should().Be(0m);
    }
}
