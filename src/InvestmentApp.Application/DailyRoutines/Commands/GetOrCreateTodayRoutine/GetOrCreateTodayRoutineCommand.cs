using System.Text.Json.Serialization;
using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Commands.GetOrCreateTodayRoutine;

public class GetOrCreateTodayRoutineCommand : IRequest<DailyRoutineDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? TemplateId { get; set; }
    public string? LocalDate { get; set; } // ISO date string from frontend (user's local date)
}

public class GetOrCreateTodayRoutineCommandHandler
    : IRequestHandler<GetOrCreateTodayRoutineCommand, DailyRoutineDto>
{
    private readonly IDailyRoutineRepository _routineRepo;
    private readonly IRoutineTemplateRepository _templateRepo;

    public GetOrCreateTodayRoutineCommandHandler(
        IDailyRoutineRepository routineRepo,
        IRoutineTemplateRepository templateRepo)
    {
        _routineRepo = routineRepo;
        _templateRepo = templateRepo;
    }

    public async Task<DailyRoutineDto> Handle(GetOrCreateTodayRoutineCommand request, CancellationToken ct)
    {
        var today = string.IsNullOrEmpty(request.LocalDate)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(request.LocalDate).Date;

        // Check if today's routine already exists (active)
        var existing = await _routineRepo.GetByUserIdAndDateAsync(request.UserId, today, ct);
        if (existing != null)
            return MapToDto(existing);

        // Clean up any soft-deleted routine for today (avoids duplicate key on compound index)
        var deleted = await _routineRepo.GetAnyByUserIdAndDateAsync(request.UserId, today, ct);
        if (deleted != null)
            await _routineRepo.HardDeleteAsync(deleted.Id, ct);

        // Determine which template to use
        RoutineTemplate? template = null;
        if (!string.IsNullOrEmpty(request.TemplateId))
        {
            template = await _templateRepo.GetByIdAsync(request.TemplateId, ct);
        }

        if (template == null)
        {
            // Auto-suggest: use default swing-trading
            template = await _templateRepo.GetByIdAsync("swing-trading", ct);
        }

        if (template == null)
        {
            // Fallback: get first available
            var all = await _templateRepo.GetBuiltInAsync(ct);
            template = all.FirstOrDefault()
                ?? throw new InvalidOperationException("No routine templates available.");
        }

        // Calculate streak
        var yesterday = today.AddDays(-1);
        var yesterdayRoutine = await _routineRepo.GetByUserIdAndDateAsync(request.UserId, yesterday, ct);
        int currentStreak = 0;
        int longestStreak = 0;

        if (yesterdayRoutine != null)
        {
            longestStreak = yesterdayRoutine.LongestStreak;
            if (yesterdayRoutine.IsFullyCompleted)
            {
                currentStreak = yesterdayRoutine.CurrentStreak;
            }
        }
        else
        {
            // Check latest routine for longest streak
            var latest = await _routineRepo.GetLatestByUserIdAsync(request.UserId, ct);
            if (latest != null)
                longestStreak = latest.LongestStreak;
        }

        var routine = DailyRoutine.CreateFromTemplate(request.UserId, today, template, currentStreak, longestStreak);
        await _routineRepo.AddAsync(routine, ct);

        return MapToDto(routine);
    }

    internal static DailyRoutineDto MapToDto(DailyRoutine r) => new()
    {
        Id = r.Id,
        Date = r.Date,
        TemplateId = r.TemplateId,
        TemplateName = r.TemplateName,
        Items = r.Items.Select(i => new RoutineItemDto
        {
            Index = i.Index,
            Label = i.Label,
            Group = i.Group,
            Link = i.Link,
            IsRequired = i.IsRequired,
            IsCompleted = i.IsCompleted,
            CompletedAt = i.CompletedAt,
            Note = i.Note,
            Emoji = i.Emoji
        }).ToList(),
        CompletedCount = r.CompletedCount,
        TotalCount = r.TotalCount,
        ProgressPercent = r.TotalCount > 0 ? Math.Round((decimal)r.CompletedCount / r.TotalCount * 100, 1) : 0,
        IsFullyCompleted = r.IsFullyCompleted,
        CurrentStreak = r.CurrentStreak,
        LongestStreak = r.LongestStreak,
        CompletedAt = r.CompletedAt
    };
}
