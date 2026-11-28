namespace InvestmentApp.Application.Interfaces;

/// <summary>Kết quả một lần lấy trước chuỗi giá đã điều chỉnh của một mã (không bao giờ dùng nguồn graph dự phòng).</summary>
public enum WarmFetchResult
{
    /// <summary>Đã gọi nguồn thành công và lưu bền chuỗi mới.</summary>
    Fetched,
    /// <summary>Không gọi nguồn: mã đang nghỉ riêng, đang có người lấy cùng mã, hoặc chuỗi đã mới. Không tốn lượt.</summary>
    Skipped,
    /// <summary>Không gọi nguồn: nguồn đang chặn hoặc đang trục trặc (cooldown chung, chuỗi lỗi của biểu đồ, hay warm-up đang nghỉ). Dừng lượt này.</summary>
    Degraded,
    /// <summary>Không gọi nguồn: đã hết ngân sách số lần gọi trong ngày. Dừng lượt này.</summary>
    BudgetExhausted,
    /// <summary>Đã gọi nhưng nguồn lỗi hoặc không có dữ liệu. Dừng lượt này.</summary>
    Failed
}

/// <summary>Phía nguồn giá của việc lấy trước: tự giữ mọi giới hạn (cooldown, ngân sách gọi/ngày), không ảnh hưởng chuỗi lỗi dùng chung của biểu đồ.</summary>
public interface IAdjustedSeriesWarmer
{
    /// <summary>Chuỗi lấy lúc <paramref name="fetchedAtUtc"/> còn dùng được theo đúng luật của nguồn (người chọn mã không tốn lượt cho thứ bộ nhớ đệm sẽ trả lời).</summary>
    bool IsFresh(DateTime fetchedAtUtc);

    Task<WarmFetchResult> WarmAsync(string symbol, CancellationToken cancellationToken = default);
}

/// <summary>Ngân sách số lần gọi nguồn của việc lấy trước, tính theo ngày Việt Nam và lưu bền (instance khởi động lại không được làm tròn ngân sách về 0).</summary>
public interface IWarmupCallBudget
{
    /// <summary>Trừ một lượt nếu ngày <paramref name="vnDay"/> còn dưới <paramref name="dailyCap"/> lượt. false = đã hết.</summary>
    Task<bool> TryConsumeAsync(DateTime vnDay, int dailyCap, CancellationToken cancellationToken = default);
}

/// <summary>Kết quả một lượt lấy trước, chỉ để ghi log (kết quả của job chụp giá không đổi).</summary>
public sealed record SignalWarmupOutcome(int Fetched, string Note);

/// <summary>
/// Lấy trước chuỗi giá đã điều chỉnh của rổ VN30 để tỉ lệ nền có dữ liệu, dựa vào job <c>prices</c> sẵn có: mỗi lượt tối đa vài mã,
/// chọn mã chưa có chuỗi trước rồi tới mã cũ nhất. Mọi lỗi đều không được làm hỏng job gọi nó.
/// </summary>
public interface ISignalSeriesWarmup
{
    Task<SignalWarmupOutcome> WarmNextAsync(CancellationToken cancellationToken = default);
}
