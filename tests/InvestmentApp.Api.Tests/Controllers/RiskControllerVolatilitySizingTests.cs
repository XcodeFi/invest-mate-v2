using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>
/// Ba tham số của endpoint trần khối lượng đều bắt buộc. Kiểu không-nullable sẽ bind giá trị thiếu
/// thành 0 trong im lặng, mà <c>quantity = 0</c> cho "biến động sau lệnh" bằng đúng biến động hiện
/// tại — một con số trông như thật (ADR-0014).
/// </summary>
public class RiskControllerVolatilitySizingTests
{
    private readonly Mock<IMediator> _mediator = new();

    private RiskController CreateController(bool withUser = true)
    {
        var controller = new RiskController(_mediator.Object, Mock.Of<IPositionSizingService>());
        var identity = withUser
            ? new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "test")
            : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Theory]
    [InlineData(null, 100_000, 100, "SYMBOL_REQUIRED")]
    [InlineData("", 100_000, 100, "SYMBOL_REQUIRED")]
    [InlineData("   ", 100_000, 100, "SYMBOL_REQUIRED")]
    [InlineData("FPT", null, 100, "ENTRY_PRICE_REQUIRED")]
    [InlineData("FPT", 0, 100, "ENTRY_PRICE_REQUIRED")]
    [InlineData("FPT", -1, 100, "ENTRY_PRICE_REQUIRED")]
    [InlineData("FPT", 100_000, null, "QUANTITY_REQUIRED")]
    [InlineData("FPT", 100_000, 0, "QUANTITY_REQUIRED")]
    [InlineData("FPT", 100_000, -5, "QUANTITY_REQUIRED")]
    public async Task MissingOrInvalidParameter_ReturnsBadRequestWithCode(
        string? symbol, int? entryPrice, int? quantity, string expectedCode)
    {
        var result = await CreateController().GetVolatilitySizingForPlan(
            "port-1", symbol!, entryPrice, quantity, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value!.GetType().GetProperty("code")!.GetValue(badRequest.Value)
            .Should().Be(expectedCode);

        _mediator.Verify(m => m.Send(It.IsAny<GetVolatilitySizingForPlanQuery>(), It.IsAny<CancellationToken>()),
            Times.Never, "tham số hỏng thì không được chạm tới tầng dưới");
    }

    [Fact]
    public async Task ValidParameters_SendsQueryWithUserIdFromToken()
    {
        GetVolatilitySizingForPlanQuery? captured = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetVolatilitySizingForPlanQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<VolatilitySizingResult>, CancellationToken>((q, _) =>
                captured = (GetVolatilitySizingForPlanQuery)q)
            .ReturnsAsync(new VolatilitySizingResult { Symbol = "FPT" });

        var result = await CreateController().GetVolatilitySizingForPlan(
            "port-1", "FPT", 100_000m, 100, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.UserId.Should().Be("user-1", "danh tính lấy từ token, không lấy từ query string");
        captured.PortfolioId.Should().Be("port-1");
        captured.Symbol.Should().Be("FPT");
        captured.EntryPrice.Should().Be(100_000m);
        captured.Quantity.Should().Be(100);
    }
}
