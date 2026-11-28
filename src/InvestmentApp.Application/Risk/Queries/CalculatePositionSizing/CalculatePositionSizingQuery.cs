using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetKellyFromHistory;
using MediatR;

namespace InvestmentApp.Application.Risk.Queries.CalculatePositionSizing;

/// <summary>
/// Định cỡ vị thế theo nhiều mô hình. Một đường tính chung cho REST (<c>POST /risk/position-sizing</c>) và MCP
/// (<c>calculate_position_size</c>), nên hai nơi không thể lệch nhau.
/// </summary>
public class CalculatePositionSizingQuery : IRequest<PositionSizingResult>
{
    public string UserId { get; set; } = null!;
    public PositionSizingRequest Request { get; set; } = null!;
    public bool UseHistory { get; set; }
    public string? StrategyId { get; set; }
}

public class CalculatePositionSizingQueryHandler : IRequestHandler<CalculatePositionSizingQuery, PositionSizingResult>
{
    private readonly IPositionSizingService _positionSizing;
    private readonly IMediator _mediator;

    public CalculatePositionSizingQueryHandler(IPositionSizingService positionSizing, IMediator mediator)
    {
        _positionSizing = positionSizing;
        _mediator = mediator;
    }

    public async Task<PositionSizingResult> Handle(CalculatePositionSizingQuery query, CancellationToken ct)
    {
        var request = query.Request;

        // Không bật UseHistory: giữ nguyên hành vi cũ (Kelly chỉ khi người gọi tự truyền winRate/averageWin/averageLoss).
        if (!query.UseHistory)
            return await _positionSizing.CalculateAsync(request, ct);

        // Bật: Kelly lấy từ dữ liệu R của chính người gọi (cận dưới Wilson), ghi đè số tự truyền; không đủ dữ liệu hoặc không có
        // lợi thế thì không có mô hình Kelly, chỉ có khối kellyFromHistory kèm lý do.
        var kelly = await _mediator.Send(new GetKellyFromHistoryQuery { UserId = query.UserId, StrategyId = query.StrategyId }, ct);
        request.WinRate = null; request.AverageWin = null; request.AverageLoss = null;
        if (kelly is { HalfKellyCapped: > 0m, WinRateLower95: not null, PayoffRatio: not null })
        {
            // b = trung bình thắng / trung bình thua: đặt thua = 1 thì thắng = b, đúng công thức Kelly hiện có
            request.WinRate = kelly.WinRateLower95;
            request.AverageWin = kelly.PayoffRatio;
            request.AverageLoss = 1m;
        }

        var result = await _positionSizing.CalculateAsync(request, ct);
        result.KellyFromHistory = kelly;
        return result;
    }
}
