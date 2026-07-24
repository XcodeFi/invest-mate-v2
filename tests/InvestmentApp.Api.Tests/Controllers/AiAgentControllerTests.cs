using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;
using InvestmentApp.Application.Trades.Commands.CreateTrade;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

public class AiAgentControllerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IFeeCalculationService> _fees = new();

    private AiAgentController Sut(string userId = "user-1")
    {
        var controller = new AiAgentController(_mediator.Object, _fees.Object);
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId) }, "ApiKey");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task CreatePlan_NullsStatusAndTradeId_AndSetsUserId()
    {
        CreateTradePlanCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (CreateTradePlanCommand)c)
            .ReturnsAsync("plan-1");

        var cmd = new CreateTradePlanCommand { Symbol = "VNM", Status = "Executed", TradeId = "t-x" };
        await Sut().CreatePlan(cmd);

        sent!.Status.Should().BeNull();
        sent.TradeId.Should().BeNull();
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task UpdateStatus_Restore_ReturnsBadRequest_AndDoesNotDispatch()
    {
        var result = await Sut().UpdateStatus("plan-1",
            new UpdateTradePlanStatusCommand { Status = "restore" });

        result.Should().BeOfType<BadRequestObjectResult>();
        _mediator.Verify(m => m.Send(It.IsAny<UpdateTradePlanStatusCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatus_Executed_Dispatches_WithUserId()
    {
        UpdateTradePlanStatusCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpdateTradePlanStatusCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Unit>, CancellationToken>((c, _) => sent = (UpdateTradePlanStatusCommand)c)
            .ReturnsAsync(Unit.Value);

        var result = await Sut().UpdateStatus("plan-1",
            new UpdateTradePlanStatusCommand { Status = "executed", TradeId = "t-1" });

        result.Should().BeOfType<NoContentResult>();
        sent!.UserId.Should().Be("user-1");
        sent.Id.Should().Be("plan-1");
    }

    private void SetupCreateTradeCapture(out Func<CreateTradeCommand?> get)
    {
        CreateTradeCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateTradeCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (CreateTradeCommand)c)
            .ReturnsAsync("trade-1");
        get = () => sent;
    }

    private void SetupPortfolios(params (string Id, string Name)[] portfolios) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetAllPortfoliosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(portfolios.Select(p => new PortfolioSummaryDto { Id = p.Id, Name = p.Name }).ToList());

    [Fact]
    public async Task CreateTrade_ExplicitValues_SetsOriginUserIdPortfolio_NoResolve()
    {
        SetupCreateTradeCapture(out var sent);

        await Sut().CreateTrade(new AgentCreateTradeRequest
        {
            PortfolioId = "p1", Symbol = "VNM", TradeType = "BUY", Quantity = 100, Price = 50000, Fee = 12, Tax = 0
        });

        sent()!.UserId.Should().Be("user-1");
        sent()!.Origin.Should().Be("AI_AGENT");
        sent()!.PortfolioId.Should().Be("p1");
        sent()!.Fee.Should().Be(12);
        sent()!.Tax.Should().Be(0);
        // portfolioId + fee/tax provided → no lookup, no fee compute
        _mediator.Verify(m => m.Send(It.IsAny<GetAllPortfoliosQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _fees.Verify(f => f.GetFeesSummary(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task CreateTrade_NoPortfolioId_SinglePortfolio_AutoPicks()
    {
        SetupPortfolios(("p9", "Danh mục chính"));
        SetupCreateTradeCapture(out var sent);

        await Sut().CreateTrade(new AgentCreateTradeRequest
        {
            Symbol = "VNM", TradeType = "BUY", Quantity = 100, Price = 50000, Fee = 0, Tax = 0
        });

        sent()!.PortfolioId.Should().Be("p9");
    }

    [Fact]
    public async Task CreateTrade_NoPortfolioId_NoPortfolio_Returns400_NotDispatched()
    {
        SetupPortfolios();

        var result = await Sut().CreateTrade(new AgentCreateTradeRequest
        {
            Symbol = "VNM", TradeType = "BUY", Quantity = 100, Price = 50000, Fee = 0, Tax = 0
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _mediator.Verify(m => m.Send(It.IsAny<CreateTradeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTrade_NoPortfolioId_MultiPortfolio_Returns400_NotDispatched()
    {
        SetupPortfolios(("p1", "A"), ("p2", "B"));

        var result = await Sut().CreateTrade(new AgentCreateTradeRequest
        {
            Symbol = "VNM", TradeType = "BUY", Quantity = 100, Price = 50000, Fee = 0, Tax = 0
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        _mediator.Verify(m => m.Send(It.IsAny<CreateTradeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTrade_NullFeeTax_Sell_AutoComputes()
    {
        // amount = 100 * 1,000,000 = 100,000,000.
        // Fee = transactionFee 150,000 + VAT 15,000 = 165,000 — broker cost only.
        // Tax = TNCN 0.1% = 100,000, stored SEPARATELY. Fee must NOT include Tax, otherwise every
        // consumer (`- Fee - Tax` on sell) double-counts the tax. See ADR-0006.
        _fees.Setup(f => f.GetFeesSummary(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(new TradingFeesSummary { TransactionFee = new Money(150000, "VND") });
        _fees.Setup(f => f.CalculateVAT(It.IsAny<Money>(), It.IsAny<string>()))
            .Returns(new Money(15000, "VND"));
        _fees.Setup(f => f.CalculateSecuritiesTax(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>()))
            .Returns((Money amt, SecurityType _, bool isBuy) => new Money(isBuy ? 0m : amt.Amount * 0.001m, "VND"));
        SetupCreateTradeCapture(out var sent);

        await Sut().CreateTrade(new AgentCreateTradeRequest
        {
            PortfolioId = "p1", Symbol = "HHV", TradeType = "SELL", Quantity = 100, Price = 1000000
        });

        sent()!.Fee.Should().Be(165000);   // broker + VAT, EXCLUDES the separately-stored TNCN tax
        sent()!.Tax.Should().Be(100000);
    }

    [Fact]
    public async Task CreateTrade_ExplicitZeroFeeTax_NotComputed()
    {
        SetupCreateTradeCapture(out var sent);

        await Sut().CreateTrade(new AgentCreateTradeRequest
        {
            PortfolioId = "p1", Symbol = "HHV", TradeType = "SELL", Quantity = 100, Price = 1000000, Fee = 0, Tax = 0
        });

        sent()!.Fee.Should().Be(0);
        sent()!.Tax.Should().Be(0);
        _fees.Verify(f => f.GetFeesSummary(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void GetDoc_Returns200_WithETagAndBody()
    {
        var sut = Sut();
        var result = sut.GetDoc() as ContentResult;

        result!.StatusCode.Should().Be(200);
        result.Content.Should().Contain("Mục lục");
        sut.Response.Headers.ETag.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetDoc_MatchingIfNoneMatch_Returns304()
    {
        var sut = Sut();
        sut.ControllerContext.HttpContext.Request.Headers.IfNoneMatch = $"\"{AiAgentController.DocVersion}\"";

        var result = sut.GetDoc() as StatusCodeResult;

        result!.StatusCode.Should().Be(StatusCodes.Status304NotModified);
    }

    [Fact]
    public void GetDoc_ContainsPortfoliosAndFeesSections()
    {
        var result = Sut().GetDoc() as ContentResult;

        result!.Content.Should().Contain("/portfolios");
        result.Content.Should().Contain("/fees/calculate");
    }
}
