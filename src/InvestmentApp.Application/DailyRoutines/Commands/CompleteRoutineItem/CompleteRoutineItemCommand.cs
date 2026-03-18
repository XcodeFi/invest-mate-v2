using System.Text.Json.Serialization;
using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Commands.CompleteRoutineItem;

public class CompleteRoutineItemCommand : IRequest<DailyRoutineDto>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    [JsonIgnore]
    public int ItemIndex { get; set; }
    public bool IsCompleted { get; set; }
}

public class CompleteRoutineItemCommandHandler
    : IRequestHandler<CompleteRoutineItemCommand, DailyRoutineDto>
{
    private readonly IDailyRoutineRepository _repo;

    public CompleteRoutineItemCommandHandler(IDailyRoutineRepository repo)
    {
        _repo = repo;
    }

    public async Task<DailyRoutineDto> Handle(CompleteRoutineItemCommand request, CancellationToken ct)
    {
        var routine = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Daily routine '{request.Id}' not found.");

        if (routine.UserId != request.UserId)
            throw new UnauthorizedAccessException();

        if (request.IsCompleted)
            routine.CompleteItem(request.ItemIndex);
        else
            routine.UncompleteItem(request.ItemIndex);

        await _repo.UpdateAsync(routine, ct);
        return GetOrCreateTodayRoutine.GetOrCreateTodayRoutineCommandHandler.MapToDto(routine);
    }
}
