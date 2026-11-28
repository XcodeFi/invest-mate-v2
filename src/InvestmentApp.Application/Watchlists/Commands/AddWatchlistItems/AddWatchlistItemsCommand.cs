using System.Text.RegularExpressions;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Watchlists.Commands.AddWatchlistItems;

/// <summary>
/// Thêm nhiều mã vào một watchlist trong một lần (từ trang Sàng lọc hoặc tác vụ định kỳ của agent). Mã đã có thì bỏ qua, không lỗi.
/// </summary>
public class AddWatchlistItemsCommand : IRequest<AddWatchlistItemsResult>
{
    public string WatchlistId { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public List<string> Symbols { get; set; } = new();
    /// <summary>Ghi chú gắn cho các mã MỚI thêm (ví dụ "Từ sàng lọc 28/11"), tối đa 200 ký tự.</summary>
    public string? Note { get; set; }
}

public class AddWatchlistItemsResult
{
    public string WatchlistId { get; set; } = "";
    public List<string> Added { get; set; } = new();
    /// <summary>Mã đã có sẵn trong watchlist (hoặc lặp trong đầu vào) nên không thêm.</summary>
    public List<string> Skipped { get; set; } = new();
}

public class AddWatchlistItemsCommandHandler : IRequestHandler<AddWatchlistItemsCommand, AddWatchlistItemsResult>
{
    public const int MaxSymbols = 30;
    public const int MaxNoteLength = 200;
    private const int MaxAttempts = 3;

    private static readonly Regex ValidSymbol = new(@"^[A-Z0-9]{1,10}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IWatchlistRepository _repo;

    public AddWatchlistItemsCommandHandler(IWatchlistRepository repo) => _repo = repo;

    public async Task<AddWatchlistItemsResult> Handle(AddWatchlistItemsCommand request, CancellationToken ct)
    {
        // Kiểm đầu vào TRƯỚC khi chạm kho dữ liệu: một mã sai làm hỏng cả lần gọi, không có chuyện thêm một nửa.
        var symbols = Normalise(request.Symbols);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > MaxNoteLength })
            throw new ArgumentException($"Ghi chú tối đa {MaxNoteLength} ký tự.", nameof(request));

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var watchlist = await _repo.GetByIdAsync(request.WatchlistId, ct)
                ?? throw new KeyNotFoundException($"Watchlist {request.WatchlistId} not found");

            // Sở hữu kiểm trên MỖI bản đọc (kể cả lần đọc lại sau xung đột), cùng cách với AddWatchlistItemCommand.
            if (watchlist.UserId != request.UserId)
                throw new UnauthorizedAccessException();

            var readVersion = watchlist.Version;
            var (added, skipped) = watchlist.AddItemsIfMissing(symbols, note);

            // Không có gì mới thì không ghi (không tăng Version, không đụng DB).
            if (added.Count == 0)
                return new AddWatchlistItemsResult { WatchlistId = watchlist.Id, Added = new(), Skipped = skipped.ToList() };

            if (await _repo.TryUpdateAsync(watchlist, readVersion, ct))
                return new AddWatchlistItemsResult { WatchlistId = watchlist.Id, Added = added.ToList(), Skipped = skipped.ToList() };

            // Ai đó ghi xen vào giữa lúc đọc và ghi: đọc lại bản mới và áp dụng lại, không ghi đè thay đổi của họ.
        }

        throw new InvalidOperationException("Watchlist vừa được sửa ở nơi khác nhiều lần liên tiếp, hãy thử lại.");
    }

    private static List<string> Normalise(List<string>? raw)
    {
        if (raw == null || raw.Count == 0)
            throw new ArgumentException("Cần ít nhất một mã.", nameof(raw));

        var result = new List<string>();
        var seen = new HashSet<string>();
        foreach (var item in raw)
        {
            var symbol = (item ?? "").Trim().ToUpperInvariant();
            if (!ValidSymbol.IsMatch(symbol))
                throw new ArgumentException($"Mã không hợp lệ: chỉ gồm chữ và số, tối đa 10 ký tự (nhận được \"{Truncate(item)}\").", nameof(raw));
            if (seen.Add(symbol)) result.Add(symbol);   // lặp ngay trong đầu vào gộp lại, đếm một lần
        }

        if (result.Count > MaxSymbols)
            throw new ArgumentException($"Mỗi lần thêm tối đa {MaxSymbols} mã (nhận {result.Count}).", nameof(raw));
        return result;
    }

    private static string Truncate(string? s) => s is { Length: > 20 } ? s[..20] + "…" : s ?? "";
}
