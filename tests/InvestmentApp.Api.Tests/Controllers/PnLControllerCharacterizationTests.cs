using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Configuration;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>
/// Đặc tả hành vi REST của PnLController: JSON (tên thuộc tính, thứ tự, kiểu chữ), 404 với thân RỖNG cho danh mục
/// không có hoặc của người khác, lỗi của dịch vụ đi tiếp ra middleware (ArgumentException → 400), và tổng hợp bỏ danh mục
/// tính lãi/lỗ lỗi nhưng vẫn cộng dòng tiền ròng.
///
/// Controller được dựng qua DI (ActivatorUtilities) để các test này giữ nguyên khi controller đổi từ gọi thẳng
/// dịch vụ sang gửi query qua MediatR: mock IPnLService/kho dữ liệu là biên ổn định, MediatR thật chạy handler thật.
/// </summary>
public class PnLControllerCharacterizationTests
{
    private const string Me = "user-1";

    private readonly Mock<IPnLService> _pnl = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<ICapitalFlowRepository> _flows = new();

    // Giống MVC (mặc định Web = camelCase) cộng cấu hình của API
    private static readonly JsonSerializerOptions MvcJson = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonConfig.Configure(o);
        return o;
    }

    private PnLController Sut()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_pnl.Object);
        services.AddSingleton(_portfolios.Object);
        services.AddSingleton(_flows.Object);
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(PortfolioPnLSummary).Assembly));
        var controller = ActivatorUtilities.CreateInstance<PnLController>(services.BuildServiceProvider());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", Me) }, "Bearer"))
            }
        };
        return controller;
    }

    private static Portfolio PortfolioOf(string userId, string id, string name, decimal capital)
    {
        var p = new Portfolio(userId, name, capital);
        typeof(Portfolio).GetProperty("Id")!.SetValue(p, id);
        return p;
    }

    private static PositionPnL Position() => new()
    {
        Symbol = "VNM", Quantity = 100, AverageCost = 80_000m, CurrentPrice = 90_000m, RealizedPnL = 500_000m,
        SettledQuantity = 90, PendingQuantity = 10, DividendNet = 200_000m, PendingDividend = 50_000m
    };

    private static string Json(object? value) => JsonSerializer.Serialize(value, MvcJson);

    // ---------- GET portfolio/{id} ----------

    [Fact]
    public async Task Portfolio_returns_the_exact_json_shape()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortfolioOf(Me, "p1", "Danh mục A", 100_000_000m));
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync("p1", It.IsAny<CancellationToken>())).ReturnsAsync(new PortfolioPnLSummary
        {
            TotalInvested = 8_000_000m, TotalPortfolioValue = 9_000_000m, TotalRealizedPnL = 500_000m, TotalUnrealizedPnL = 1_000_000m,
            Positions = new List<PositionPnL> { Position() }
        });

        var result = await Sut().GetPortfolioPnL("p1");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        Json(ok.Value).Should().Be(GoldenPortfolio);
    }

    [Fact]
    public async Task Portfolio_missing_and_foreign_are_404_with_an_empty_body_and_the_service_is_never_called()
    {
        _portfolios.Setup(r => r.GetByIdAsync("khong-co", It.IsAny<CancellationToken>())).ReturnsAsync((Portfolio?)null);
        _portfolios.Setup(r => r.GetByIdAsync("cua-nguoi-khac", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortfolioOf("user-2", "cua-nguoi-khac", "Người khác", 1m));

        (await Sut().GetPortfolioPnL("khong-co")).Should().BeOfType<NotFoundResult>();
        (await Sut().GetPortfolioPnL("cua-nguoi-khac")).Should().BeOfType<NotFoundResult>();
        _pnl.VerifyNoOtherCalls();
    }

    // ---------- GET portfolio/{id}/position/{symbol} ----------

    [Fact]
    public async Task Position_returns_the_exact_json_shape_and_normalizes_the_symbol()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortfolioOf(Me, "p1", "Danh mục A", 1m));
        _pnl.Setup(s => s.CalculatePositionPnLAsync("p1", It.Is<StockSymbol>(x => x.Value == "VNM"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Position());

        var result = await Sut().GetPositionPnL("p1", " vnm ");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        Json(ok.Value).Should().Be(GoldenPosition);
    }

    [Fact]
    public async Task Position_missing_and_foreign_portfolio_are_404_with_an_empty_body_and_the_service_is_never_called()
    {
        _portfolios.Setup(r => r.GetByIdAsync("khong-co", It.IsAny<CancellationToken>())).ReturnsAsync((Portfolio?)null);
        _portfolios.Setup(r => r.GetByIdAsync("cua-nguoi-khac", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortfolioOf("user-2", "cua-nguoi-khac", "Người khác", 1m));

        (await Sut().GetPositionPnL("khong-co", "VNM")).Should().BeOfType<NotFoundResult>();
        (await Sut().GetPositionPnL("cua-nguoi-khac", "VNM")).Should().BeOfType<NotFoundResult>();
        _pnl.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("No trades found for symbol ZZZ in portfolio p1")]
    [InlineData("Price not available for symbol ZZZ")]
    public async Task Position_without_trades_or_price_surfaces_the_services_ArgumentException_for_the_400_mapping(string message)
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortfolioOf(Me, "p1", "Danh mục A", 1m));
        _pnl.Setup(s => s.CalculatePositionPnLAsync("p1", It.IsAny<StockSymbol>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException(message));

        var act = async () => await Sut().GetPositionPnL("p1", "ZZZ");

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Be(message);
    }

    // ---------- GET summary ----------

    [Fact]
    public async Task Summary_returns_the_exact_json_shape_skips_a_failing_portfolio_and_still_adds_its_net_cash_flow()
    {
        var a = PortfolioOf(Me, "pa", "Danh mục A", 100m);
        var b = PortfolioOf(Me, "pb", "Danh mục B (chưa có giao dịch)", 50m);
        _portfolios.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { a, b });
        _flows.Setup(f => f.GetTotalFlowByPortfolioIdAsync("pa", It.IsAny<CancellationToken>())).ReturnsAsync(10m);
        _flows.Setup(f => f.GetTotalFlowByPortfolioIdAsync("pb", It.IsAny<CancellationToken>())).ReturnsAsync(5m);
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync("pa", It.IsAny<CancellationToken>())).ReturnsAsync(new PortfolioPnLSummary
        {
            TotalInvested = 8_000m, TotalPortfolioValue = 9_000m, TotalRealizedPnL = 500m, TotalUnrealizedPnL = 1_000m,
            Positions = new List<PositionPnL> { Position() }
        });
        _pnl.Setup(s => s.CalculatePortfolioPnLAsync("pb", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("No trades"));

        var result = await Sut().GetOverallPnL();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        Json(ok.Value).Should().Be(GoldenSummary);
    }

    [Fact]
    public async Task Summary_with_no_portfolios_is_all_zero_and_reads_only_the_callers_portfolios()
    {
        _portfolios.Setup(r => r.GetByUserIdAsync(Me, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Portfolio>());

        var result = await Sut().GetOverallPnL();

        Json(result.Should().BeOfType<OkObjectResult>().Subject.Value).Should().Be(GoldenEmptySummary);
        _portfolios.Verify(r => r.GetByUserIdAsync(It.Is<string>(u => u != Me), It.IsAny<CancellationToken>()), Times.Never);
    }

    private const string GoldenPortfolio = """{"portfolioId":"p1","portfolioName":"Danh m\u1EE5c A","totalInvested":8000000,"totalMarketValue":9000000,"totalRealizedPnL":500000,"totalUnrealizedPnL":1000000,"totalPnL":1500000,"totalPnLPercent":18.7500,"positions":[{"symbol":"VNM","quantity":100,"averageCost":80000,"currentPrice":90000,"marketValue":9000000,"totalCost":8000000,"unrealizedPnL":1000000,"unrealizedPnLPercentage":12.500,"realizedPnL":500000,"totalPnL":1500000,"totalPnLPercent":18.7500,"settledQuantity":90,"pendingQuantity":10,"dividendNet":200000,"pendingDividend":50000,"totalPnLWithDividend":1750000}]}""";
    private const string GoldenPosition = """{"symbol":"VNM","quantity":100,"averageCost":80000,"currentPrice":90000,"marketValue":9000000,"totalCost":8000000,"unrealizedPnL":1000000,"unrealizedPnLPercentage":12.500,"realizedPnL":500000,"totalPnL":1500000,"totalPnLPercent":18.7500,"settledQuantity":90,"pendingQuantity":10,"dividendNet":200000,"pendingDividend":50000,"totalPnLWithDividend":1750000}""";
    private const string GoldenSummary = """{"totalPortfolios":2,"totalInitialCapital":150,"totalNetCashFlow":15,"totalCurrentCapital":165,"totalInvested":8000,"totalMarketValue":9000,"totalUnrealizedPnL":1000,"totalRealizedPnL":500,"totalPnL":1500,"totalPnLPercent":18.7500,"portfolios":[{"portfolioId":"pa","portfolioName":"Danh m\u1EE5c A","initialCapital":100,"netCashFlow":10,"currentCapital":110,"totalInvested":8000,"totalMarketValue":9000,"totalUnrealizedPnL":1000,"totalRealizedPnL":500,"totalPnL":1500,"totalPnLPercent":18.7500,"positions":[{"symbol":"VNM","quantity":100,"averageCost":80000,"currentPrice":90000,"marketValue":9000000,"totalCost":8000000,"unrealizedPnL":1000000,"unrealizedPnLPercentage":12.500,"realizedPnL":500000,"totalPnL":1500000,"totalPnLPercent":18.7500,"settledQuantity":90,"pendingQuantity":10,"dividendNet":200000,"pendingDividend":50000,"totalPnLWithDividend":1750000}]}]}""";
    private const string GoldenEmptySummary = """{"totalPortfolios":0,"totalInitialCapital":0,"totalNetCashFlow":0,"totalCurrentCapital":0,"totalInvested":0,"totalMarketValue":0,"totalUnrealizedPnL":0,"totalRealizedPnL":0,"totalPnL":0,"totalPnLPercent":0,"portfolios":[]}""";
}
