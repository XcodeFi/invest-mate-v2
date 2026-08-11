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

        return checkIn is null
            ? new TodayMoodDto()
            : new TodayMoodDto { Mood = checkIn.Mood, Overrode = checkIn.OverrodeAt is not null };
    }
}
