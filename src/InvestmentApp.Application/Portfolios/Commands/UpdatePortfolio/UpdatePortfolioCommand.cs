using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Portfolios.Commands.UpdatePortfolio;

public class UpdatePortfolioCommand : IRequest<bool>
{
    [JsonIgnore]
    public string? Id { get; set; }
    [JsonIgnore]
    public string? UserId { get; set; }
    public string Name { get; set; } = null!;
    public decimal InitialCapital { get; set; }
}

public class UpdatePortfolioCommandHandler : IRequestHandler<UpdatePortfolioCommand, bool>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IAuditService _auditService;

    public UpdatePortfolioCommandHandler(
        IPortfolioRepository portfolioRepository,
        IAuditService auditService)
    {
        _portfolioRepository = portfolioRepository;
        _auditService = auditService;
    }

    public async Task<bool> Handle(UpdatePortfolioCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.Id, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            return false;

        portfolio.UpdateName(request.Name);

        await _portfolioRepository.UpdateAsync(portfolio, cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = request.UserId,
            Action = "Updated",
            EntityId = portfolio.Id,
            EntityType = "Portfolio",
            Description = $"Portfolio updated: '{request.Name}'"
        }, cancellationToken);

        return true;
    }
}
