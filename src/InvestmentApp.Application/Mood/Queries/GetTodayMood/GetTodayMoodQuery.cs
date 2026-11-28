using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Mood.Queries.GetTodayMood;

public class GetTodayMoodQuery : IRequest<TodayMoodDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class TodayMoodDto
{
    /// <summary>Null khi hôm nay chưa chấm — trang chủ hỏi lại và dùng nhóm châm ngôn Bình tĩnh.</summary>
    public MoodState? Mood { get; set; }

    /// <summary>Đã bấm "Vẫn xem bây giờ" hôm nay chưa.</summary>
    public bool Overrode { get; set; }

    /// <summary>
    /// Lần đổi gần nhất do agent (MCP) ghi. Bình tĩnh do agent ghi KHÔNG tính là đã tự chấm (<see cref="Mood"/> = null để trang chủ hỏi lại và
    /// ghi chú "(từ agent)"); trạng thái cảm xúc do agent ghi vẫn có hiệu lực vì đó là chiều an toàn.
    /// </summary>
    public bool FromAgent { get; set; }
}

public class GetTodayMoodQueryHandler : IRequestHandler<GetTodayMoodQuery, TodayMoodDto>
{
    private readonly IMoodCheckInRepository _repo;

    public GetTodayMoodQueryHandler(IMoodCheckInRepository repo)
    {
        _repo = repo;
    }

    public async Task<TodayMoodDto> Handle(GetTodayMoodQuery request, CancellationToken cancellationToken)
    {
        var dateKey = VietnamDate.ToDateKey(DateTime.UtcNow);
        var checkIn = await _repo.GetByUserAndDateAsync(request.UserId, dateKey, cancellationToken);

        if (checkIn is null) return new TodayMoodDto();

        var fromAgent = checkIn.LatestSource == MoodChange.SourceMcp;
        // Tự đánh giá: agent không được "mở khoá" lớp phủ bằng cách ghi Bình tĩnh thay người dùng.
        var mood = fromAgent && checkIn.Mood == MoodState.Calm ? (MoodState?)null : checkIn.Mood;
        return new TodayMoodDto { Mood = mood, Overrode = checkIn.OverrodeAt is not null, FromAgent = fromAgent };
    }
}
