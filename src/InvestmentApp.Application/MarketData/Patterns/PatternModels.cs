namespace InvestmentApp.Application.MarketData.Patterns;

/// <summary>
/// Ngưỡng của bộ dò Cốc tay cầm. Mọi giá trị là MẶC ĐỊNH CÓ THỂ CHỈNH, không phải luật của một tác giả nào:
/// độ sâu cốc 12–33% và độ dài cốc 7–65 tuần theo bảng so sánh O'Neil qua Bulkowski (có kiểm chứng một phần);
/// độ sâu tay cầm 8–15%, ngưỡng khối lượng +40%, ngưỡng "sát pivot" 3% và ngưỡng "đã vượt xa" 5% CHƯA kiểm chứng.
/// </summary>
public sealed class CupWithHandleOptions
{
    public int MinBars { get; init; } = 60;
    public decimal MinCupWeeks { get; init; } = 7m;
    public decimal MaxCupWeeks { get; init; } = 65m;
    public decimal MinCupDepthPct { get; init; } = 12m;
    public decimal MaxCupDepthPct { get; init; } = 33m;
    /// <summary>Vành phải phải hồi tới ít nhất bấy nhiêu phần của đỉnh trái.</summary>
    public decimal MinRimRecovery { get; init; } = 0.90m;
    public int MinHandleSessions { get; init; } = 5;
    public int MaxHandleSessions { get; init; } = 25;
    public decimal MinHandleDepthPct { get; init; } = 8m;
    public decimal MaxHandleDepthPct { get; init; } = 15m;
    /// <summary>Đỉnh trái là đỉnh cao nhất trong ± bấy nhiêu phiên.</summary>
    public int SwingWindow { get; init; } = 5;
    public decimal NearPivotPct { get; init; } = 3m;
    /// <summary>Khối lượng nến cuối so với trung bình 50 phiên trước đó, từ ngưỡng này trở lên coi là có khối lượng xác nhận.</summary>
    public decimal BreakoutVolumeMultiple { get; init; } = 1.4m;
    /// <summary>Giá đóng cửa vượt pivot quá bấy nhiêu % thì không còn là ứng viên (đã chạy xa).</summary>
    public decimal MaxExtendedPct { get; init; } = 5m;
    /// <summary>Cắt lỗ đặt dưới đáy tay cầm bấy nhiêu % (0,99 = thấp hơn 1%).</summary>
    public decimal StopBelowHandleFactor { get; init; } = 0.99m;
}

/// <summary>Một điều kiện của mô hình: luật, giá trị đo được, đạt hay không. <c>Required</c> = bắt buộc để mô hình được báo.</summary>
public sealed class PatternCriterion
{
    public string Key { get; init; } = "";
    public string Rule { get; init; } = "";
    public decimal? Value { get; init; }
    public string Unit { get; init; } = "";
    public bool Passed { get; init; }
    public bool Required { get; init; } = true;
}

/// <summary>
/// Một ứng viên mô hình giá. Cố ý KHÔNG có điểm số, độ tin cậy hay xác suất: không có con số nào được hiệu chỉnh.
/// Phần "nếu sai" (cắt lỗ, mục tiêu đo theo độ sâu cốc, lãi/rủi ro theo R) là hình học xác định từ nến, không phải dự báo.
/// </summary>
public sealed class PatternCandidate
{
    public const string DisclaimerText = "Ứng viên theo ngưỡng mặc định có thể chỉnh, không phải tín hiệu mua. Chưa vượt pivot kèm khối lượng thì chưa xác nhận. Mục tiêu đo theo độ sâu cốc là quy ước hình học, không phải dự báo.";

    public string Type { get; init; } = "cup-with-handle";
    public string TypeVi { get; init; } = "Cốc tay cầm";

    /// <summary>forming | near-pivot | broke-out-unconfirmed | broke-out-with-volume.</summary>
    public string State { get; init; } = "forming";
    public string StateVi { get; init; } = "";

    public DateTime StartDate { get; init; }
    public DateTime BottomDate { get; init; }
    public DateTime RightLipDate { get; init; }
    public decimal CupLengthWeeks { get; init; }
    public decimal CupDepthPct { get; init; }
    public int HandleLengthSessions { get; init; }
    public decimal HandleDepthPct { get; init; }

    public decimal PivotPrice { get; init; }
    /// <summary>(pivot − giá đóng cửa) / pivot, theo %: dương = còn dưới pivot, âm = đã trên pivot.</summary>
    public decimal DistanceToPivotPct { get; init; }
    /// <summary>Khối lượng nến cuối / trung bình 50 phiên trước đó (gấp bao nhiêu lần); null khi chưa đủ 51 phiên.</summary>
    public decimal? VolumeVsAvg50 { get; init; }

    /// <summary>Cắt lỗ ngay dưới đáy tay cầm.</summary>
    public decimal StopPrice { get; init; }
    /// <summary>Nếu sai: % mất từ pivot tới điểm cắt lỗ (số dương, hiển thị "−X%").</summary>
    public decimal RiskPct { get; init; }
    /// <summary>Mục tiêu đo: pivot + độ sâu tuyệt đối của cốc.</summary>
    public decimal TargetPrice { get; init; }
    public decimal RewardPct { get; init; }
    /// <summary>Lãi/rủi ro theo R: (mục tiêu − pivot) / (pivot − cắt lỗ).</summary>
    public decimal RewardRisk { get; init; }

    public List<PatternCriterion> Criteria { get; init; } = new();
    public string Disclaimer { get; init; } = DisclaimerText;
}
