using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class TradeToolsTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IFeeCalculationService> _fees = new();

    private void SetupFees()
    {
        _fees.Setup(f => f.GetFeesSummary(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(new TradingFeesSummary { TransactionFee = new Money(150000, "VND") });
        _fees.Setup(f => f.CalculateVAT(It.IsAny<Money>(), It.IsAny<string>())).Returns(new Money(15000, "VND"));
        _fees.Setup(f => f.CalculateSecuritiesTax(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>()))
            .Returns((Money amt, SecurityType _, bool isBuy) => new Money(isBuy ? 0m : amt.Amount * 0.001m, "VND"));
    }

    [Fact]
    public void CalculateFees_ReturnsBrokerCostAndTax_Separately()
    {
        SetupFees();
        var r = TradeTools.CalculateFees("SELL", 100, 1000000, _fees.Object);
        r.TransactionFee.Should().Be(150000);
        r.Vat.Should().Be(15000);
        r.Breakdown.Tax.Should().Be(100000);
    }

    [Fact]
    public async Task CreateTrade_SinglePortfolio_AutoResolves_FeeExclTax()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetAllPortfoliosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PortfolioSummaryDto> { new() { Id = "only-p", Name = "Chính" } });
        SetupFees();
        McpTestContext.Capture<string, CreateTradeCommand>(_mediator, out var sent, "trade-1");

        var id = await TradeTools.CreateTrade(null, "HHV", "SELL", 100, 1000000, null, null, null,
            _mediator.Object, _fees.Object, McpTestContext.WithUser("u-9"), CancellationToken.None);

        id.Should().Be("trade-1");
        sent()!.PortfolioId.Should().Be("only-p");
        sent()!.Origin.Should().Be("AI_AGENT");
        sent()!.Fee.Should().Be(165000);   // TransactionFee + Vat (excl tax)
        sent()!.Tax.Should().Be(100000);   // stored separately
        sent()!.UserId.Should().Be("u-9");
    }

    [Fact]
    public async Task CreateTrade_MultiplePortfolios_Throws()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetAllPortfoliosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PortfolioSummaryDto> { new() { Id = "a", Name = "A" }, new() { Id = "b", Name = "B" } });
        var act = async () => await TradeTools.CreateTrade(null, "HHV", "BUY", 100, 1000, null, null, null,
            _mediator.Object, _fees.Object, McpTestContext.WithUser("u-9"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
