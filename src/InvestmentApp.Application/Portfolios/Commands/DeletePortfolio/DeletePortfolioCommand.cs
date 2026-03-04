using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Portfolios.Commands.DeletePortfolio;

public class DeletePortfolioCommand : IRequest<bool>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class DeletePortfolioCommandHandler : IRequestHandler<DeletePortfolioCommand, bool>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IAuditService _auditService;

    public DeletePortfolioCommandHandler(
        IPortfolioRepository portfolioRepository,
        IAuditService auditService)
    {
        _portfolioRepository = portfolioRepository;
        _auditService = auditService;
    }

    public async Task<bool> Handle(DeletePortfolioCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.Id, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            return false;

        portfolio.MarkAsDeleted();
        await _portfolioRepository.DeleteAsync(request.Id, cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = request.UserId,
            Action = "Deleted",
            EntityId = portfolio.Id,
            EntityType = "Portfolio",
            Description = $"Portfolio '{portfolio.Name}' deleted"
        }, cancellationToken);

        return true;
    }
}
