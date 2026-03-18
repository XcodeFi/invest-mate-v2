using System.Text.Json.Serialization;
using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Commands.CreateCustomTemplate;

public class CreateCustomTemplateCommand : IRequest<string>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Emoji { get; set; } = "📋";
    public int EstimatedMinutes { get; set; }
    public List<RoutineItemTemplateDto> Items { get; set; } = new();
}

public class CreateCustomTemplateCommandHandler
    : IRequestHandler<CreateCustomTemplateCommand, string>
{
    private readonly IRoutineTemplateRepository _repo;

    public CreateCustomTemplateCommandHandler(IRoutineTemplateRepository repo)
    {
        _repo = repo;
    }

    public async Task<string> Handle(CreateCustomTemplateCommand request, CancellationToken ct)
    {
        var items = request.Items.Select(i => new RoutineItemTemplate
        {
            Index = i.Index,
            Label = i.Label,
            Group = i.Group,
            Link = i.Link,
            IsRequired = i.IsRequired,
            Emoji = i.Emoji
        }).ToList();

        var template = new RoutineTemplate(
            request.UserId, request.Name, request.Emoji, "Custom",
            request.EstimatedMinutes, items)
        {
            Description = request.Description,
            SortOrder = 100 // custom templates sort after built-in
        };

        await _repo.AddAsync(template, ct);
        return template.Id;
    }
}
