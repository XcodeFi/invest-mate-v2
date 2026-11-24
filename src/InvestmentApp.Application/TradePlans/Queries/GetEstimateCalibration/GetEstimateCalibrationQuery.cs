using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetEstimateCalibration;

/// <summary>
/// Độ khớp giữa % thắng đã ước lượng (ảnh chụp Estimate lúc khoá, khi vào lệnh) và kết quả thật của các chiến dịch đã review.
/// Chỉ kế hoạch mua của người gọi, chưa xoá, đã review, có ước lượng ĐÃ KHOÁ có tổng xác suất hợp lệ, và tính được R.
/// Ước lượng chưa khoá (kế hoạch chưa khớp) không vào: chưa có kết quả thật để so. <c>StrategyId</c> lạ hoặc của người khác
/// không khớp kế hoạch nào của người gọi nên cho đúng kết quả của một mã không tồn tại.
/// </summary>
public class GetEstimateCalibrationQuery : IRequest<CalibrationResult>
{
    public string UserId { get; set; } = null!;
    public string? StrategyId { get; set; }
}

public class GetEstimateCalibrationQueryHandler : IRequestHandler<GetEstimateCalibrationQuery, CalibrationResult>
{
    private readonly ITradePlanRepository _plans;

    public GetEstimateCalibrationQueryHandler(ITradePlanRepository plans) => _plans = plans;

    public async Task<CalibrationResult> Handle(GetEstimateCalibrationQuery request, CancellationToken ct)
    {
        var plans = await _plans.GetByUserIdAsync(request.UserId, ct);

        var samples = plans
            .Where(p => p.UserId == request.UserId
                        && !p.IsDeleted
                        && p.Status == TradePlanStatus.Reviewed
                        && string.Equals(p.Direction, "Buy", StringComparison.OrdinalIgnoreCase)
                        && (string.IsNullOrEmpty(request.StrategyId) || p.StrategyId == request.StrategyId)
                        && p.Estimate is { LockedAt: not null, ImpliedWinProbability: not null })
            .Select(p => (Probability: p.Estimate!.ImpliedWinProbability!.Value / 100m, R: RMath.ResolveRealizedR(p).R))
            .Where(x => x.R.HasValue)
            .Select(x => (x.Probability, x.R!.Value))
            .ToList();

        return Calibration.Compute(samples);
    }
}
