using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Queries.GetSuggestedTemplate;

public class GetSuggestedTemplateQuery : IRequest<RoutineTemplateDto?>
{
    public string UserId { get; set; } = null!;
    public string? LocalDate { get; set; }
}

public class GetSuggestedTemplateQueryHandler
    : IRequestHandler<GetSuggestedTemplateQuery, RoutineTemplateDto?>
{
    private readonly IDailyRoutineRepository _routineRepo;
    private readonly IRoutineTemplateRepository _templateRepo;
    private readonly IMarketIndexRepository _marketIndexRepo;

    public GetSuggestedTemplateQueryHandler(
        IDailyRoutineRepository routineRepo,
        IRoutineTemplateRepository templateRepo,
        IMarketIndexRepository marketIndexRepo)
    {
        _routineRepo = routineRepo;
        _templateRepo = templateRepo;
        _marketIndexRepo = marketIndexRepo;
    }

    public async Task<RoutineTemplateDto?> Handle(GetSuggestedTemplateQuery request, CancellationToken ct)
    {
        var today = string.IsNullOrEmpty(request.LocalDate)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(request.LocalDate).Date;

        var templates = (await _templateRepo.GetBuiltInAsync(ct)).ToList();
        if (templates.Count == 0) return null;

        string suggestedId = "swing-trading"; // default

        // 1. First-time user → Onboarding
        var latest = await _routineRepo.GetLatestByUserIdAsync(request.UserId, ct);
        if (latest == null)
        {
            suggestedId = "onboarding";
        }
        else
        {
            // 2. Crisis detection: VN-Index drops ≤ -3%
            try
            {
                var marketIndex = await _marketIndexRepo.GetBySymbolAndDateAsync("VNINDEX", today, ct);
                if (marketIndex != null && marketIndex.ChangePercent <= -3.0m)
                {
                    suggestedId = "crisis";
                }
                else
                {
                    // 3. Weekend → Research
                    var dayOfWeek = (int)today.DayOfWeek;
                    if (dayOfWeek == 0 || dayOfWeek == 6)
                    {
                        suggestedId = "research";
                    }
                    else
                    {
                        // 4. DCA day check
                        var dcaTemplate = templates.FirstOrDefault(t => t.Id == "dca");
                        if (dcaTemplate?.AutoSuggestDaysOfWeek?.Contains(dayOfWeek) == true)
                        {
                            suggestedId = "dca";
                        }
                    }
                }
            }
            catch
            {
                // Market data unavailable — use day-based logic only
                var dayOfWeek = (int)today.DayOfWeek;
                if (dayOfWeek == 0 || dayOfWeek == 6) suggestedId = "research";
            }
        }

        var suggested = templates.FirstOrDefault(t => t.Id == suggestedId) ?? templates.First();

        return new RoutineTemplateDto
        {
            Id = suggested.Id,
            Name = suggested.Name,
            Description = suggested.Description,
            Emoji = suggested.Emoji,
            Category = suggested.Category,
            EstimatedMinutes = suggested.EstimatedMinutes,
            IsOneTime = suggested.IsOneTime,
            IsUrgent = suggested.IsUrgent,
            Items = suggested.Items.Select(i => new RoutineItemTemplateDto
            {
                Index = i.Index,
                Label = i.Label,
                Group = i.Group,
                Link = i.Link,
                IsRequired = i.IsRequired,
                Emoji = i.Emoji
            }).ToList(),
            IsBuiltIn = true,
            IsSuggested = true
        };
    }
}
