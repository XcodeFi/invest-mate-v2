using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Queries.GetCorporateActions;

public record CorporateActionDto(
    string Id,
    string Symbol,
    CorporateActionType Type,
    DateTime ExDate,
    DateTime? SettlementDate,
    DateTime? SettledAt,
    decimal? AmountPerShare,
    decimal Multiplier,
    string DeclaredText,
    string? Note);

public record GetCorporateActionsQuery(string UserId, string PortfolioId, string? Symbol)
    : IRequest<List<CorporateActionDto>>;

public class GetCorporateActionsQueryHandler
    : IRequestHandler<GetCorporateActionsQuery, List<CorporateActionDto>>
{
    private readonly ICorporateActionRepository _actions;
    private readonly IPortfolioRepository _portfolios;

    public GetCorporateActionsQueryHandler(
        ICorporateActionRepository actions, IPortfolioRepository portfolios)
    {
        _actions = actions;
        _portfolios = portfolios;
    }

    public async Task<List<CorporateActionDto>> Handle(
        GetCorporateActionsQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolios.GetByIdAsync(request.PortfolioId, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy danh mục", nameof(request.PortfolioId));
        if (portfolio.UserId != request.UserId)
            throw new UnauthorizedAccessException("Danh mục không thuộc về người dùng này");

        var items = string.IsNullOrWhiteSpace(request.Symbol)
            ? await _actions.GetByPortfolioIdAsync(request.PortfolioId, cancellationToken)
            : await _actions.GetByPortfolioIdAndSymbolAsync(request.PortfolioId, request.Symbol, cancellationToken);

        return items.Select(a => new CorporateActionDto(
            a.Id, a.Symbol, a.Type, a.ExDate, a.SettlementDate, a.SettledAt,
            a.AmountPerShare, a.Multiplier, a.DeclaredText, a.Note)).ToList();
    }
}
