using System.Text.Json.Serialization;
using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Commands.SwitchTemplate;

public class SwitchTemplateCommand : IRequest<DailyRoutineDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string TemplateId { get; set; } = null!;
    public string? LocalDate { get; set; }
}

public class SwitchTemplateCommandHandler
    : IRequestHandler<SwitchTemplateCommand, DailyRoutineDto>
{
    private readonly IDailyRoutineRepository _routineRepo;
    private readonly IRoutineTemplateRepository _templateRepo;

    public SwitchTemplateCommandHandler(
        IDailyRoutineRepository routineRepo,
        IRoutineTemplateRepository templateRepo)
    {
        _routineRepo = routineRepo;
        _templateRepo = templateRepo;
    }

    public async Task<DailyRoutineDto> Handle(SwitchTemplateCommand request, CancellationToken ct)
    {
        var today = string.IsNullOrEmpty(request.LocalDate)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(request.LocalDate).Date;

        var template = await _templateRepo.GetByIdAsync(request.TemplateId, ct)
            ?? throw new KeyNotFoundException($"Template '{request.TemplateId}' not found.");

        // Find active routine for today
        var existing = await _routineRepo.GetByUserIdAndDateAsync(request.UserId, today, ct);

        if (existing != null)
        {
            // Update in-place — preserves the document (no duplicate key issue)
            existing.ResetFromTemplate(template);
            await _routineRepo.UpdateAsync(existing, ct);
            return GetOrCreateTodayRoutine.GetOrCreateTodayRoutineCommandHandler.MapToDto(existing);
        }

        // Clean up any soft-deleted routine for today
        var deleted = await _routineRepo.GetAnyByUserIdAndDateAsync(request.UserId, today, ct);
        if (deleted != null)
            await _routineRepo.HardDeleteAsync(deleted.Id, ct);

        // Create new
        var routine = DailyRoutine.CreateFromTemplate(request.UserId, today, template, 0, 0);
        await _routineRepo.AddAsync(routine, ct);
        return GetOrCreateTodayRoutine.GetOrCreateTodayRoutineCommandHandler.MapToDto(routine);
    }
}
