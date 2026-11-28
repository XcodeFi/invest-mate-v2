using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Mood.Queries.GetMoodHistory;

/// <summary>
/// Lịch sử tâm trạng theo ngày VN. Mặc định 90 ngày gần nhất tính cả hôm nay, tối đa 366 ngày. userId chỉ lấy từ xác thực.
/// </summary>
public class GetMoodHistoryQuery : IRequest<MoodHistoryDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;

    /// <summary>Ngày VN bắt đầu (chỉ phần ngày được dùng). Bỏ trống thì 90 ngày kết thúc ở <see cref="To"/>.</summary>
    public DateTime? From { get; set; }

    /// <summary>Ngày VN kết thúc (gồm cả ngày này). Bỏ trống thì hôm nay.</summary>
    public DateTime? To { get; set; }
}

public class MoodChangeDto
{
    public DateTime At { get; set; }
    public MoodState Mood { get; set; }
    /// <summary>"web" hoặc "mcp".</summary>
    public string Source { get; set; } = "web";
}

public class MoodDayDto
{
    /// <summary>Ngày lịch VN, "YYYY-MM-DD".</summary>
    public string DateKey { get; set; } = null!;

    /// <summary>Null khi ngày có lệnh nhưng chưa chấm tâm trạng ("chưa chấm").</summary>
    public MoodState? Mood { get; set; }

    public List<MoodChangeDto> Changes { get; set; } = new();
    public bool Overrode { get; set; }
    public int TradeCount { get; set; }

    /// <summary>
    /// Đặt lệnh khi đang cảm xúc: đã bấm qua lớp phủ, HOẶC có lệnh trong ngày mà tâm trạng (ở bất kỳ lần đổi nào trong ngày) khác Bình tĩnh.
    /// Ngày chưa chấm thì không bao giờ là cảm xúc.
    /// </summary>
    public bool EmotionalTrade { get; set; }
}

public class MoodHistoryDto
{
    public string From { get; set; } = null!;
    public string To { get; set; } = null!;

    /// <summary>Chỉ các ngày có chấm tâm trạng hoặc có lệnh, theo ngày tăng dần; ngày trống không có mục.</summary>
    public List<MoodDayDto> Days { get; set; } = new();
}

public class GetMoodHistoryQueryHandler : IRequestHandler<GetMoodHistoryQuery, MoodHistoryDto>
{
    public const int DefaultDays = 90;
    public const int MaxDays = 366;

    private readonly IMoodCheckInRepository _moods;
    private readonly IPortfolioRepository _portfolios;
    private readonly ITradeRepository _trades;
    private readonly TimeProvider _time;

    public GetMoodHistoryQueryHandler(IMoodCheckInRepository moods, IPortfolioRepository portfolios, ITradeRepository trades, TimeProvider? time = null)
    {
        _moods = moods;
        _portfolios = portfolios;
        _trades = trades;
        _time = time ?? TimeProvider.System;
    }

    public async Task<MoodHistoryDto> Handle(GetMoodHistoryQuery request, CancellationToken cancellationToken)
    {
        var today = VietnamDate.Today(_time.GetUtcNow().UtcDateTime);
        var to = request.To?.Date ?? today;
        var from = request.From?.Date ?? to.AddDays(-(DefaultDays - 1));

        if (from > to)
            throw new ArgumentException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if ((to - from).TotalDays + 1 > MaxDays)
            throw new ArgumentException($"Khoảng tối đa {MaxDays} ngày.");

        var fromKey = from.ToString("yyyy-MM-dd");
        var toKey = to.ToString("yyyy-MM-dd");

        var checkIns = (await _moods.GetByUserAndDateRangeAsync(request.UserId, fromKey, toKey, cancellationToken))
            .ToDictionary(m => m.DateKey);

        // MỘT truy vấn lệnh cho cả khoảng (mọi danh mục của người gọi), nhóm theo ngày VN trong bộ nhớ: không N+1.
        // Mốc từ/đến là biên ngày VN đổi sang UTC để không lệch lệnh sát nửa đêm.
        var portfolioIds = (await _portfolios.GetByUserIdAsync(request.UserId, cancellationToken)).Select(p => p.Id).ToList();
        var tradeCounts = new Dictionary<string, int>();
        if (portfolioIds.Count > 0)
        {
            var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc) - VietnamDate.Offset;
            var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc) - VietnamDate.Offset;
            var trades = await _trades.GetByPortfolioIdsAndDateRangeAsync(portfolioIds, fromUtc, toUtc, cancellationToken);
            foreach (var t in trades)
            {
                var key = VietnamDate.ToDateKey(t.TradeDate);
                if (string.CompareOrdinal(key, fromKey) < 0 || string.CompareOrdinal(key, toKey) > 0) continue;
                tradeCounts[key] = tradeCounts.GetValueOrDefault(key) + 1;
            }
        }

        var days = checkIns.Keys.Union(tradeCounts.Keys).OrderBy(k => k, StringComparer.Ordinal).Select(key =>
        {
            checkIns.TryGetValue(key, out var c);
            var count = tradeCounts.GetValueOrDefault(key);
            var everNotCalm = c is not null
                && (c.Changes.Count > 0 ? c.Changes.Any(x => x.Mood != MoodState.Calm) : c.Mood != MoodState.Calm);
            var overrode = c?.OverrodeAt is not null;
            return new MoodDayDto
            {
                DateKey = key,
                Mood = c?.Mood,
                Changes = c?.Changes.Select(x => new MoodChangeDto { At = x.At, Mood = x.Mood, Source = x.Source }).ToList() ?? new(),
                Overrode = overrode,
                TradeCount = count,
                EmotionalTrade = overrode || (count > 0 && everNotCalm)
            };
        }).ToList();

        return new MoodHistoryDto { From = fromKey, To = toKey, Days = days };
    }
}
