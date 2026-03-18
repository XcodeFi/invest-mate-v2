using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Queries.GetRoutineHistory;

public class GetRoutineHistoryQuery : IRequest<RoutineHistoryDto>
{
    public string UserId { get; set; } = null!;
    public int Days { get; set; } = 30;
}

public class GetRoutineHistoryQueryHandler
    : IRequestHandler<GetRoutineHistoryQuery, RoutineHistoryDto>
{
    private readonly IDailyRoutineRepository _repo;

    public GetRoutineHistoryQueryHandler(IDailyRoutineRepository repo)
    {
        _repo = repo;
    }

    public async Task<RoutineHistoryDto> Handle(GetRoutineHistoryQuery request, CancellationToken ct)
    {
        var to = DateTime.UtcNow.Date;
        var from = to.AddDays(-request.Days);

        var routines = await _repo.GetByUserIdRangeAsync(request.UserId, from, to, ct);
        var list = routines.ToList();

        var latest = list.FirstOrDefault(); // sorted desc
        int currentStreak = latest?.CurrentStreak ?? 0;
        int longestStreak = latest?.LongestStreak ?? 0;

        return new RoutineHistoryDto
        {
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak,
            Days = list.Select(r => new RoutineHistoryDayDto
            {
                Date = r.Date,
                TemplateName = r.TemplateName,
                IsCompleted = r.IsFullyCompleted,
                CompletedCount = r.CompletedCount,
                TotalCount = r.TotalCount
            }).ToList()
        };
    }
}
