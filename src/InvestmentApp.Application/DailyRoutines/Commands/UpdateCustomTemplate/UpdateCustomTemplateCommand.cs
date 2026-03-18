using System.Text.Json.Serialization;
using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.ValueObjects;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Commands.UpdateCustomTemplate;

public class UpdateCustomTemplateCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Emoji { get; set; }
    public int? EstimatedMinutes { get; set; }
    public List<RoutineItemTemplateDto>? Items { get; set; }
}

public class UpdateCustomTemplateCommandHandler
    : IRequestHandler<UpdateCustomTemplateCommand, Unit>
{
    private readonly IRoutineTemplateRepository _repo;

    public UpdateCustomTemplateCommandHandler(IRoutineTemplateRepository repo)
    {
        _repo = repo;
    }

    public async Task<Unit> Handle(UpdateCustomTemplateCommand request, CancellationToken ct)
    {
        var template = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Template '{request.Id}' not found.");

        if (template.UserId != request.UserId)
            throw new UnauthorizedAccessException("Cannot edit built-in templates.");

        var items = request.Items?.Select(i => new RoutineItemTemplate
        {
            Index = i.Index,
            Label = i.Label,
            Group = i.Group,
            Link = i.Link,
            IsRequired = i.IsRequired,
            Emoji = i.Emoji
        }).ToList();

        template.Update(request.Name, request.Description, request.Emoji,
            request.EstimatedMinutes, items);

        await _repo.UpdateAsync(template, ct);
        return Unit.Value;
    }
}
