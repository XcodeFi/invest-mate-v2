using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Queries.GetRoutineTemplates;

public class GetRoutineTemplatesQuery : IRequest<IEnumerable<RoutineTemplateDto>>
{
    public string UserId { get; set; } = null!;
}

public class GetRoutineTemplatesQueryHandler
    : IRequestHandler<GetRoutineTemplatesQuery, IEnumerable<RoutineTemplateDto>>
{
    private readonly IRoutineTemplateRepository _repo;

    public GetRoutineTemplatesQueryHandler(IRoutineTemplateRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<RoutineTemplateDto>> Handle(GetRoutineTemplatesQuery request, CancellationToken ct)
    {
        var templates = await _repo.GetAllForUserAsync(request.UserId, ct);

        return templates.Select(t => new RoutineTemplateDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            Emoji = t.Emoji,
            Category = t.Category,
            EstimatedMinutes = t.EstimatedMinutes,
            IsOneTime = t.IsOneTime,
            IsUrgent = t.IsUrgent,
            Items = t.Items.Select(i => new RoutineItemTemplateDto
            {
                Index = i.Index,
                Label = i.Label,
                Group = i.Group,
                Link = i.Link,
                IsRequired = i.IsRequired,
                Emoji = i.Emoji
            }).ToList(),
            IsBuiltIn = t.UserId == null,
            IsSuggested = false
        });
    }
}
