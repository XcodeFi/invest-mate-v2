namespace InvestmentApp.Application.Interfaces;

/// <summary>
/// Lịch sử của các setup tín hiệu trên MỘT mã: setup xuất hiện bao nhiêu lần trong 3 năm qua và các lệnh mô phỏng theo luật thị trường
/// Việt Nam kết thúc ra sao. Không xếp hạng, không dự báo. Dưới 30 lệnh đã đóng thì chỉ có số lượng.
/// </summary>
public interface ISignalSetupService
{
    Task<SignalSetupsDto> GetSignalSetupsAsync(string symbol, CancellationToken cancellationToken = default);
}

public class SignalSetupsDto
{
    public const string DisclaimerText = "Kết quả quá khứ, không đảm bảo tương lai";

    public string Symbol { get; set; } = null!;

    /// <summary>Mọi nến đều là giá đã điều chỉnh theo sự kiện quyền.</summary>
    public bool Adjusted { get; set; }
    public DateTime? AdjustedAsOf { get; set; }

    /// <summary>Có giá trị thì KHÔNG có thống kê (và <see cref="Setups"/> rỗng): chuỗi giá chưa đủ tốt để đếm.</summary>
    public string? ReasonUnavailable { get; set; }

    public List<SignalSetupDto> Setups { get; set; } = new();
    public string Disclaimer { get; set; } = DisclaimerText;

    /// <summary>Các lưu ý bắt buộc đi kèm số liệu (giá đã điều chỉnh, thiên lệch người sống sót, lệnh dừng, T+2,5).</summary>
    public List<string> Notes { get; set; } = new();
}

public class SignalSetupDto
{
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    /// <summary>Số lần tín hiệu xuất hiện (phiên đầu tiên điều kiện đúng) trong chuỗi giá.</summary>
    public int Signals { get; set; }
    /// <summary>Lệnh mô phỏng đã đóng. Lệnh còn mở và lệnh bị bỏ qua KHÔNG tính vào thống kê.</summary>
    public int Completed { get; set; }
    public int Open { get; set; }
    public int Skipped { get; set; }

    /// <summary>Số lệnh đã đóng dùng cho thống kê (= <see cref="Completed"/>).</summary>
    public int N { get; set; }
    public int MinSample { get; set; } = 30;
    public bool SampleSufficient { get; set; }

    // Chỉ có khi N ≥ MinSample; dưới ngưỡng đều null (không phải 0).
    public decimal? WinRate { get; set; }
    public decimal? WinRateLow { get; set; }
    public decimal? WinRateHigh { get; set; }
    public decimal? EvR { get; set; }
    public decimal? EvRLow { get; set; }
    public decimal? EvRHigh { get; set; }
    public decimal? AvgWinR { get; set; }
    public decimal? AvgLossR { get; set; }

    /// <summary>Tỉ lệ nền của setup này trên rổ VN30 (chỉ cho mã thuộc VN30, và chỉ khi đủ dữ liệu). KHÔNG phải bằng chứng riêng của mã.</summary>
    public SignalBaseRateDto? BaseRate { get; set; }

    /// <summary>Có giá trị khi <see cref="BaseRate"/> vắng: mã không thuộc VN30, hoặc chưa đủ mã/lệnh/tháng trong rổ.</summary>
    public string? BaseRateReason { get; set; }

    /// <summary>Ngày tín hiệu đang kích hoạt (3 phiên cuối); null nếu không có.</summary>
    public DateTime? ActiveSince { get; set; }
    public SignalActivePlanDto? ActivePlan { get; set; }
}

public class SignalActivePlanDto
{
    public DateTime SignalDate { get; set; }
    public decimal Entry { get; set; }
    /// <summary>true: chưa tới phiên vào lệnh, vào lệnh ước tính = đóng cửa phiên tín hiệu.</summary>
    public bool EntryIsEstimate { get; set; }
    public decimal Stop { get; set; }
    public decimal Target { get; set; }
    /// <summary>"moi" (chưa vào lệnh) | "dang-mo" (đã vào, chưa thoát).</summary>
    public string Status { get; set; } = null!;
    /// <summary>Khi "dang-mo": ngày thứ mấy kể từ phiên vào lệnh (d0 là ngày 1).</summary>
    public int? DayInTrade { get; set; }
}

/// <summary>
/// Tỉ lệ nền gộp của setup trên rổ VN30 hiện tại. Khoảng 95% là XẤP XỈ (bootstrap theo khối tháng, tỉ lệ thắng và R trung bình tính trên mọi lệnh
/// của các tháng được chọn), không phải khoảng Wilson trên các lệnh coi như độc lập.
/// </summary>
public class SignalBaseRateDto
{
    public const string DefaultLabel = "Tỉ lệ nền VN30";

    public string Label { get; set; } = DefaultLabel;

    /// <summary>Số mã dùng được (đủ nến đã điều chỉnh, còn mới).</summary>
    public int K { get; set; }
    /// <summary>Số lệnh đã đóng được gộp.</summary>
    public int N { get; set; }
    /// <summary>Số tháng có ít nhất một lệnh.</summary>
    public int EffectiveMonths { get; set; }

    public decimal WinRate { get; set; }
    public decimal WinRateLow { get; set; }
    public decimal WinRateHigh { get; set; }
    public decimal EvR { get; set; }
    public decimal EvRLow { get; set; }
    public decimal EvRHigh { get; set; }

    /// <summary>Ngày của nến mới nhất trong số liệu.</summary>
    public DateTime AsOf { get; set; }

    /// <summary>Lưu ý bắt buộc: khoảng xấp xỉ, đây là tỉ lệ của rổ chứ không phải của mã, thiên lệch người sống sót.</summary>
    public List<string> Notes { get; set; } = new();
}

public sealed record SignalBaseRateResult(SignalBaseRateDto? BaseRate, string? Reason);

/// <summary>Tỉ lệ nền của setup Pullback trên rổ VN30, gộp từ chuỗi giá đã điều chỉnh đã lưu. Không gọi nguồn giá.</summary>
public interface ISignalBaseRateService
{
    Task<SignalBaseRateResult> GetPullbackBaseRateAsync(CancellationToken cancellationToken = default);
}
