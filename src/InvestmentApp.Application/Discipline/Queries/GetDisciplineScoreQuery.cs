using System.Text.Json.Serialization;
using InvestmentApp.Application.Discipline.Services;
using MediatR;

namespace InvestmentApp.Application.Discipline.Queries;

/// <summary>
/// Query Discipline Score cho widget Dashboard (§D6 plan Vin-discipline).
/// </summary>
public class GetDisciplineScoreQuery : IRequest<DisciplineScoreDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;

    public int Days { get; set; } = 90;
}

/// <summary>
/// Response cho widget: composite score + components + primitive + sample size + trend.
/// Xem shape đầy đủ §D6 plan.
/// </summary>
public class DisciplineScoreDto
{
    /// <summary>Composite 0-100 (weighted avg). Null khi chưa đủ dữ liệu.</summary>
    public int? Overall { get; set; }

    /// <summary>"Kỷ luật Vin" / "Cần cải thiện" / "Trôi dạt" / "Chưa đủ dữ liệu".</summary>
    public string Label { get; set; } = string.Empty;

    public DisciplineComponents Components { get; set; } = new();
    public DisciplinePrimitives Primitives { get; set; } = new();
    public DisciplineSampleSize SampleSize { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class DisciplineComponents
{
    /// <summary>max(0, StopHonorRate − SlWidenedRate) × 100. Null khi không có lệnh lỗ đã đóng.</summary>
    public int? SlIntegrity { get; set; }

    /// <summary>% plan trong period pass gate size-based. Loại trừ LegacyExempt.</summary>
    public int? PlanQuality { get; set; }

    /// <summary>% plan Ready/InProgress review thesis trong 3 ngày của CheckDate/ExpectedReviewDate.</summary>
    public int? ReviewTimeliness { get; set; }
}

public class DisciplinePrimitives
{
    public StopHonorRateDto StopHonorRate { get; set; } = new();
}

public class StopHonorRateDto
{
    /// <summary>Giá trị 0..1; -1 khi không có mẫu (denominator=0).</summary>
    public decimal Value { get; set; } = -1m;

    /// <summary>Số lệnh lỗ đã đóng tôn trọng SL (exit ≥ SL cho Buy, ≤ SL cho Sell).</summary>
    public int Hit { get; set; }

    /// <summary>Tổng số lệnh lỗ đã đóng trong period.</summary>
    public int Total { get; set; }
}

public class DisciplineSampleSize
{
    public int TotalPlans { get; set; }
    public int ClosedLossTrades { get; set; }
    public int DaysObserved { get; set; }
}

public class GetDisciplineScoreQueryHandler : IRequestHandler<GetDisciplineScoreQuery, DisciplineScoreDto>
{
    private readonly IDisciplineScoreCalculator _calculator;

    public GetDisciplineScoreQueryHandler(IDisciplineScoreCalculator calculator)
    {
        _calculator = calculator;
    }

    public Task<DisciplineScoreDto> Handle(GetDisciplineScoreQuery request, CancellationToken cancellationToken)
    {
        if (request.Days <= 0)
            throw new ArgumentException("Days must be positive", nameof(request.Days));

        return _calculator.ComputeAsync(request.UserId, request.Days, cancellationToken);
    }
}
