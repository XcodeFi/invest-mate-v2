using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;
using System.Security.Claims;

namespace InvestmentApp.Application.Portfolios.Commands.CreatePortfolio;

public class CreatePortfolioCommandHandler : IRequestHandler<CreatePortfolioCommand, string>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IAuditService _auditService;

    public CreatePortfolioCommandHandler(
        IPortfolioRepository portfolioRepository,
        IAuditService auditService)
    {
        _portfolioRepository = portfolioRepository;
        _auditService = auditService;
    }

    public async Task<string> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken)
    {
        var userId = request.UserId; // This will come from the controller

        var portfolio = new Portfolio(userId, request.Name, request.InitialCapital);

        await _portfolioRepository.AddAsync(portfolio);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = userId,
            Action = "Created",
            EntityId = portfolio.Id,
            EntityType = "Portfolio",
            Description = $"Portfolio '{request.Name}' created with initial capital ${request.InitialCapital}"
        });

        return portfolio.Id;
    }
}