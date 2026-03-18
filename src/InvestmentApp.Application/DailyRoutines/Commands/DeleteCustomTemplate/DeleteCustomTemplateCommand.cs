using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Commands.DeleteCustomTemplate;

public class DeleteCustomTemplateCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class DeleteCustomTemplateCommandHandler
    : IRequestHandler<DeleteCustomTemplateCommand, Unit>
{
    private readonly IRoutineTemplateRepository _repo;

    public DeleteCustomTemplateCommandHandler(IRoutineTemplateRepository repo)
    {
        _repo = repo;
    }

    public async Task<Unit> Handle(DeleteCustomTemplateCommand request, CancellationToken ct)
    {
        var template = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Template '{request.Id}' not found.");

        if (template.UserId != request.UserId)
            throw new UnauthorizedAccessException("Cannot delete built-in templates.");

        template.SoftDelete();
        await _repo.UpdateAsync(template, ct);
        return Unit.Value;
    }
}
