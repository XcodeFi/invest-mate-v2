using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CapitalFlows.Commands.RecordCapitalFlow;

public class RecordCapitalFlowCommandHandler : IRequestHandler<RecordCapitalFlowCommand, string>
{
    private readonly ICapitalFlowRepository _capitalFlowRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IAuditService _auditService;

    public RecordCapitalFlowCommandHandler(
        ICapitalFlowRepository capitalFlowRepository,
        IPortfolioRepository portfolioRepository,
        IAuditService auditService)
    {
        _capitalFlowRepository = capitalFlowRepository;
        _portfolioRepository = portfolioRepository;
        _auditService = auditService;
    }

    public async Task<string> Handle(RecordCapitalFlowCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        var flowType = Enum.Parse<CapitalFlowType>(request.Type, ignoreCase: true);

        var capitalFlow = new CapitalFlow(
            portfolioId: request.PortfolioId,
            userId: request.UserId!,
            type: flowType,
            amount: request.Amount,
            currency: request.Currency,
            note: request.Note,
            flowDate: request.FlowDate
        );

        await _capitalFlowRepository.AddAsync(capitalFlow, cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = request.UserId,
            Action = "RecordedCapitalFlow",
            EntityId = capitalFlow.Id,
            EntityType = "CapitalFlow",
            Description = $"{request.Type} of {request.Amount:N0} {request.Currency} for portfolio '{portfolio.Name}'"
        }, cancellationToken);

        return capitalFlow.Id;
    }
}
