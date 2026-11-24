using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Configuration;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetKellyFromHistory;
using InvestmentApp.Domain.Services;
using InvestmentApp.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>
/// Định cỡ vị thế có thêm nguồn Kelly "từ lịch sử" nhưng KHÔNG đổi gì khi không bật: cùng JSON với dịch vụ gọi thẳng
/// (không có khoá kellyFromHistory), và không đụng tới dữ liệu lịch sử.
/// </summary>
public class RiskControllerSizingHistoryTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly PositionSizingService _service = new();

    private static readonly JsonSerializerOptions Json = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonConfig.Configure(o);
        return o;
    }

    private RiskController Sut()
    {
        var controller = new RiskController(_mediator.Object, _service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "test"))
            }
        };
        return controller;
    }

    private static PositionSizingRequest Request(decimal? winRate = null, decimal? avgWin = null, decimal? avgLoss = null) => new()
    {
        AccountBalance = 1_000_000_000m, EntryPrice = 100_000m, StopLoss = 95_000m, RiskPercent = 1m,
        WinRate = winRate, AverageWin = avgWin, AverageLoss = avgLoss
    };

    private void KellyReturns(ConservativeKelly kelly)
        => _mediator.Setup(m => m.Send(It.IsAny<GetKellyFromHistoryQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(kelly);

    [Fact]
    public async Task sizing_without_flag_unchanged()
    {
        // Người gọi tự truyền winRate/avgWin/avgLoss: hành vi cũ, Kelly theo số họ truyền
        var expected = await _service.CalculateAsync(Request(0.55m, 2_000_000m, 1_000_000m));

        var ok = (await Sut().CalculatePositionSizing(Request(0.55m, 2_000_000m, 1_000_000m)))
            .Should().BeOfType<OkObjectResult>().Subject;

        JsonSerializer.Serialize(ok.Value, Json).Should().Be(JsonSerializer.Serialize(expected, Json));
        JsonSerializer.Serialize(ok.Value, Json).Should().NotContain("kellyFromHistory");
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task sizing_with_history_adds_block_and_a_kelly_model_from_the_lower_bound()
    {
        var kelly = KellyFromHistory.Compute(14, 10, 2m, -1m);
        KellyReturns(kelly);

        var ok = (await Sut().CalculatePositionSizing(Request(), useHistory: true))
            .Should().BeOfType<OkObjectResult>().Subject;

        var result = ok.Value.Should().BeOfType<PositionSizingResult>().Subject;
        result.KellyFromHistory.Should().Be(kelly);
        var model = result.Models.Single(m => m.Model == "kelly");
        // half-Kelly 4,126% của 1 tỷ / 100.000 = 412 cp → làm tròn xuống lô 100 = 400
        model.Shares.Should().Be(400);
        model.Note.Should().Contain("Half-Kelly: 4.1%");
        JsonSerializer.Serialize(ok.Value, Json).Should().Contain("\"kellyFromHistory\":{\"n\":24");
    }

    [Fact]
    public async Task sizing_with_history_ignores_the_numbers_the_caller_passed()
    {
        KellyReturns(KellyFromHistory.Compute(14, 10, 2m, -1m));

        var withOwn = (PositionSizingResult)((OkObjectResult)await Sut()
            .CalculatePositionSizing(Request(0.9m, 9_000_000m, 1_000_000m), useHistory: true)).Value!;
        var without = (PositionSizingResult)((OkObjectResult)await Sut()
            .CalculatePositionSizing(Request(), useHistory: true)).Value!;

        withOwn.Models.Single(m => m.Model == "kelly").Shares
            .Should().Be(without.Models.Single(m => m.Model == "kelly").Shares);
    }

    [Theory]
    [InlineData(5, 5)]    // chưa đủ 20 lệnh
    [InlineData(5, 20)]   // không có lợi thế
    [InlineData(0, 25)]   // toàn thua
    public async Task sizing_with_history_without_an_edge_has_no_kelly_model_only_the_block_with_a_reason(int wins, int losses)
    {
        KellyReturns(KellyFromHistory.Compute(wins, losses, wins == 0 ? null : 1m, -1m));

        // Người gọi tự truyền số rất đẹp, nhưng đã bật lịch sử thì không được lọt qua
        var ok = (OkObjectResult)await Sut().CalculatePositionSizing(Request(0.9m, 9_000_000m, 1_000_000m), useHistory: true);

        var result = (PositionSizingResult)ok.Value!;
        result.Models.Should().NotContain(m => m.Model == "kelly");
        result.KellyFromHistory!.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task sizing_with_history_passes_the_caller_id_and_strategy_to_the_query()
    {
        GetKellyFromHistoryQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetKellyFromHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ConservativeKelly>, CancellationToken>((q, _) => sent = (GetKellyFromHistoryQuery)q)
            .ReturnsAsync(KellyFromHistory.Compute(0, 0, null, null));

        await Sut().CalculatePositionSizing(Request(), useHistory: true, strategyId: "s1");

        sent!.UserId.Should().Be("user-1");
        sent.StrategyId.Should().Be("s1");
    }
}
