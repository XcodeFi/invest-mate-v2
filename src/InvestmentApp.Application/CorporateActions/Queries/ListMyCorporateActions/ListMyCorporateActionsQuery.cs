using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Queries.ListMyCorporateActions;

public record MyCorporateActionDto(
    string Id,
    string PortfolioId,
    string Symbol,
    CorporateActionType Type,
    DateTime ExDate,
    DateTime? SettlementDate,
    DateTime? SettledAt,
    decimal? AmountPerShare,
    decimal Multiplier,
    string DeclaredText,
    string? Note);

/// <summary>
/// Sự kiện quyền (cổ tức, chia tách) trên các danh mục CỦA NGƯỜI GỌI, tuỳ chọn lọc theo danh mục và mã. Dùng cho MCP
/// <c>list_corporate_actions</c>. Danh mục không có và danh mục của người khác cho cùng một phản hồi (không tìm thấy).
/// </summary>
public record ListMyCorporateActionsQuery(string UserId, string? PortfolioId, string? Symbol)
    : IRequest<List<MyCorporateActionDto>>;

public class ListMyCorporateActionsQueryHandler
    : IRequestHandler<ListMyCorporateActionsQuery, List<MyCorporateActionDto>>
{
    private readonly ICorporateActionRepository _actions;
    private readonly IPortfolioRepository _portfolios;

    public ListMyCorporateActionsQueryHandler(ICorporateActionRepository actions, IPortfolioRepository portfolios)
    {
        _actions = actions;
        _portfolios = portfolios;
    }

    public async Task<List<MyCorporateActionDto>> Handle(ListMyCorporateActionsQuery request, CancellationToken ct)
    {
        var mine = (await _portfolios.GetByUserIdAsync(request.UserId, ct)).Select(p => p.Id).ToList();

        if (!string.IsNullOrWhiteSpace(request.PortfolioId))
        {
            if (!mine.Contains(request.PortfolioId))
                throw new KeyNotFoundException($"Portfolio {request.PortfolioId} not found");
            mine = new List<string> { request.PortfolioId };
        }

        var items = await _actions.GetByPortfolioIdsAsync(mine, ct);

        if (!string.IsNullOrWhiteSpace(request.Symbol))
        {
            var symbol = request.Symbol.Trim().ToUpperInvariant();
            items = items.Where(a => string.Equals(a.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        }

        return items.Select(a => new MyCorporateActionDto(
            a.Id, a.PortfolioId, a.Symbol, a.Type, a.ExDate, a.SettlementDate, a.SettledAt,
            a.AmountPerShare, a.Multiplier, a.DeclaredText, a.Note)).ToList();
    }
}
