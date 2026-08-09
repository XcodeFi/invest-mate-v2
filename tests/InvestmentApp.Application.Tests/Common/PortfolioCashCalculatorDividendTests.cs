using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Application.Tests.Common;

/// <summary>
/// Cổ tức tiền mặt về ví đi qua CapitalFlow.Dividend, nên tiền mặt danh mục tự tăng.
/// Test này khoá hành vi đó lại để lần refactor sau không làm hỏng.
/// </summary>
public class PortfolioCashCalculatorDividendTests
{
    [Fact]
    public void CoTucTienMatDaVe_LamTangTienMat()
    {
        var trades = new[] { new Trade("p1", "SAB", TradeType.BUY, 1000, 55_000,
            tradeDate: new DateTime(2026, 1, 5)) };

        var withoutDividend = PortfolioCashCalculator.Compute(100_000_000m, 0m, trades);
        var withDividend = PortfolioCashCalculator.Compute(100_000_000m, 475_000m, trades);

        withoutDividend.Should().Be(45_000_000m);
        withDividend.Should().Be(45_475_000m);
    }

    [Fact]
    public void DongTienCoTuc_CoSignedAmountDuong()
    {
        var flow = new CapitalFlow("p1", "u1", CapitalFlowType.Dividend, 475_000m);
        flow.SignedAmount.Should().Be(475_000m);
    }

    [Fact]
    public void DongTienCoTuc_GanDuocVaoMa()
    {
        var flow = new CapitalFlow("p1", "u1", CapitalFlowType.Dividend, 475_000m);
        flow.LinkCorporateAction("ca1", "sab");

        flow.Symbol.Should().Be("SAB");
        flow.CorporateActionId.Should().Be("ca1");
    }
}
