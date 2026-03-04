using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Alerts.Queries.GetAlertHistory;

public class GetAlertHistoryQuery : IRequest<AlertHistoryResult>
{
    public string UserId { get; set; } = null!;
    public bool UnreadOnly { get; set; } = false;
}

public class AlertHistoryResult
{
    public IEnumerable<AlertHistoryDto> Alerts { get; set; } = new List<AlertHistoryDto>();
    public int UnreadCount { get; set; }
}

public class AlertHistoryDto
{
    public string Id { get; set; } = null!;
    public string AlertRuleId { get; set; } = null!;
    public string AlertType { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string? Symbol { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? ThresholdValue { get; set; }
    public bool IsRead { get; set; }
    public DateTime TriggeredAt { get; set; }
}

public class GetAlertHistoryQueryHandler : IRequestHandler<GetAlertHistoryQuery, AlertHistoryResult>
{
    private readonly IAlertHistoryRepository _alertHistoryRepository;

    public GetAlertHistoryQueryHandler(IAlertHistoryRepository alertHistoryRepository)
    {
        _alertHistoryRepository = alertHistoryRepository;
    }

    public async Task<AlertHistoryResult> Handle(GetAlertHistoryQuery request, CancellationToken cancellationToken)
    {
        var alerts = request.UnreadOnly
            ? await _alertHistoryRepository.GetUnreadByUserIdAsync(request.UserId, cancellationToken)
            : await _alertHistoryRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        var unreadCount = await _alertHistoryRepository.GetUnreadCountAsync(request.UserId, cancellationToken);

        return new AlertHistoryResult
        {
            Alerts = alerts.Select(a => new AlertHistoryDto
            {
                Id = a.Id,
                AlertRuleId = a.AlertRuleId,
                AlertType = a.AlertType,
                Title = a.Title,
                Message = a.Message,
                PortfolioId = a.PortfolioId,
                Symbol = a.Symbol,
                CurrentValue = a.CurrentValue,
                ThresholdValue = a.ThresholdValue,
                IsRead = a.IsRead,
                TriggeredAt = a.TriggeredAt
            }),
            UnreadCount = unreadCount
        };
    }
}
