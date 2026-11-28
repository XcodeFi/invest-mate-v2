using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.CalculatePositionSizing;
using InvestmentApp.Application.TradePlans.Queries.GetKellyFromHistory;
using InvestmentApp.Domain.Services;
using InvestmentApp.Infrastructure.Services;
using MediatR;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Định cỡ vị thế có thêm nguồn Kelly "từ lịch sử" nhưng KHÔNG đổi gì khi không bật: cùng JSON với dịch vụ gọi thẳng
/// (không có khoá kellyFromHistory), và không đụng tới dữ liệu lịch sử. Một đường tính chung cho REST (RiskController) và MCP (calculate_position_size).
/// </summary>
public class CalculatePositionSizingQueryHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly PositionSizingService _service = new();

    private static readonly JsonSerializerOptions Json = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        return o;
    }

    private CalculatePositionSizingQueryHandler Sut() => new(_service, _mediator.Object);

    private Task<PositionSizingResult> Run(PositionSizingRequest request, bool useHistory = false, string? strategyId = null)
        => Sut().Handle(new CalculatePositionSizingQuery
        {
            UserId = "user-1", Request = request, UseHistory = useHistory, StrategyId = strategyId
        }, CancellationToken.None);

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

        var result = await Run(Request(0.55m, 2_000_000m, 1_000_000m));

        JsonSerializer.Serialize(result, Json).Should().Be(JsonSerializer.Serialize(expected, Json));
        JsonSerializer.Serialize(result, Json).Should().NotContain("kellyFromHistory");
        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task sizing_with_history_adds_block_and_a_kelly_model_from_the_lower_bound()
    {
        var kelly = KellyFromHistory.Compute(14, 10, 2m, -1m);
        KellyReturns(kelly);

        var result = await Run(Request(), useHistory: true);

        result.KellyFromHistory.Should().Be(kelly);
        var model = result.Models.Single(m => m.Model == "kelly");
        // half-Kelly 4,126% của 1 tỷ / 100.000 = 412 cp → làm tròn xuống lô 100 = 400
        model.Shares.Should().Be(400);
        model.Note.Should().Contain("Half-Kelly: 4.1%");
        JsonSerializer.Serialize(result, Json).Should().Contain("\"kellyFromHistory\":{\"n\":24");
    }

    [Fact]
    public async Task sizing_with_history_ignores_the_numbers_the_caller_passed()
    {
        KellyReturns(KellyFromHistory.Compute(14, 10, 2m, -1m));

        var withOwn = await Run(Request(0.9m, 9_000_000m, 1_000_000m), useHistory: true);
        var without = await Run(Request(), useHistory: true);

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
        var result = await Run(Request(0.9m, 9_000_000m, 1_000_000m), useHistory: true);

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

        await Run(Request(), useHistory: true, strategyId: "s1");

        sent!.UserId.Should().Be("user-1");
        sent.StrategyId.Should().Be("s1");
    }
}
