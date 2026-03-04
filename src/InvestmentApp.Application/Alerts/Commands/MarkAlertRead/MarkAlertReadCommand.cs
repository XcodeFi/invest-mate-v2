using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Alerts.Commands.MarkAlertRead;

public class MarkAlertReadCommand : IRequest<Unit>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class MarkAlertReadCommandHandler : IRequestHandler<MarkAlertReadCommand, Unit>
{
    private readonly IAlertHistoryRepository _alertHistoryRepository;

    public MarkAlertReadCommandHandler(IAlertHistoryRepository alertHistoryRepository)
    {
        _alertHistoryRepository = alertHistoryRepository;
    }

    public async Task<Unit> Handle(MarkAlertReadCommand request, CancellationToken cancellationToken)
    {
        var alert = await _alertHistoryRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Alert {request.Id} not found");

        if (alert.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized");

        alert.MarkAsRead();
        await _alertHistoryRepository.UpdateAsync(alert, cancellationToken);
        return Unit.Value;
    }
}
