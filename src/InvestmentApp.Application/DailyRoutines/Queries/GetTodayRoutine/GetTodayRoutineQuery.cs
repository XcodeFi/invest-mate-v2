using InvestmentApp.Application.DailyRoutines.Commands.GetOrCreateTodayRoutine;
using InvestmentApp.Application.DailyRoutines.Dtos;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.DailyRoutines.Queries.GetTodayRoutine;

public class GetTodayRoutineQuery : IRequest<DailyRoutineDto?>
{
    public string UserId { get; set; } = null!;
    public string? LocalDate { get; set; }
}

public class GetTodayRoutineQueryHandler
    : IRequestHandler<GetTodayRoutineQuery, DailyRoutineDto?>
{
    private readonly IDailyRoutineRepository _repo;

    public GetTodayRoutineQueryHandler(IDailyRoutineRepository repo)
    {
        _repo = repo;
    }

    public async Task<DailyRoutineDto?> Handle(GetTodayRoutineQuery request, CancellationToken ct)
    {
        var today = string.IsNullOrEmpty(request.LocalDate)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(request.LocalDate).Date;

        var routine = await _repo.GetByUserIdAndDateAsync(request.UserId, today, ct);
        return routine == null ? null : GetOrCreateTodayRoutineCommandHandler.MapToDto(routine);
    }
}
