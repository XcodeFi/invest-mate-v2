namespace InvestmentApp.Infrastructure.Services.Signals;

/// <summary>Cấu hình mục <c>SignalWarmup</c>. Công tắc tắt: biến môi trường <c>SignalWarmup__Enabled=false</c>.</summary>
public class SignalWarmupOptions
{
    public const string SectionName = "SignalWarmup";

    public bool Enabled { get; set; } = true;

    /// <summary>Số mã lấy trước tối đa mỗi lượt job giá (mặc định 1: 24 lượt mỗi ngày làm việc, thưa hơn trần 30 lần gọi/ngày).</summary>
    public int SymbolsPerRun { get; set; } = 1;
}
