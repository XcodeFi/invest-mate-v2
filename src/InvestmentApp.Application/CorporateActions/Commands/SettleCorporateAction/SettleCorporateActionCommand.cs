using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Commands.SettleCorporateAction;

public record SettleCorporateActionCommand(
    string UserId, string Id, DateTime SettledAt, string? LinkExistingCapitalFlowId) : IRequest<Unit>;

public class SettleCorporateActionCommandHandler
    : IRequestHandler<SettleCorporateActionCommand, Unit>
{
    private readonly ICorporateActionRepository _actions;
    private readonly ICapitalFlowRepository _flows;
    private readonly ITradeRepository _trades;

    public SettleCorporateActionCommandHandler(
        ICorporateActionRepository actions, ICapitalFlowRepository flows, ITradeRepository trades)
    {
        _actions = actions;
        _flows = flows;
        _trades = trades;
    }

    public async Task<Unit> Handle(SettleCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var action = await _actions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy sự kiện quyền", nameof(request.Id));
        if (action.UserId != request.UserId)
            throw new UnauthorizedAccessException("Sự kiện quyền không thuộc về người dùng này");
        if (action.SettledAt.HasValue)
            throw new InvalidOperationException("Sự kiện quyền này đã được xác nhận trước đó");

        action.MarkSettled(request.SettledAt);

        if (action.Type == CorporateActionType.CashDividend)
        {
            if (!string.IsNullOrWhiteSpace(request.LinkExistingCapitalFlowId))
            {
                var existing = await _flows.GetByIdAsync(request.LinkExistingCapitalFlowId, cancellationToken)
                    ?? throw new ArgumentException("Không tìm thấy dòng tiền để liên kết", nameof(request.LinkExistingCapitalFlowId));
                if (existing.UserId != request.UserId || existing.PortfolioId != action.PortfolioId)
                    throw new UnauthorizedAccessException("Dòng tiền không thuộc danh mục của người dùng này");
                existing.LinkCorporateAction(action.Id, action.Symbol);
                await _flows.UpdateAsync(existing, cancellationToken);
                action.LinkCapitalFlow(existing.Id);
            }
            else
            {
                var amount = await ComputeNetDividendAsync(action, cancellationToken);
                if (amount > 0)
                {
                    // Dùng action.SettledAt — MarkSettled đã chuẩn hoá về nửa đêm UTC.
                    // Truyền request.SettledAt thô sẽ bị Mongo đẩy lùi một ngày theo múi giờ.
                    var flow = new CapitalFlow(action.PortfolioId, action.UserId, CapitalFlowType.Dividend,
                        amount, "VND", $"Cổ tức tiền mặt {action.Symbol} ({action.DeclaredText})", action.SettledAt);
                    flow.LinkCorporateAction(action.Id, action.Symbol);
                    await _flows.AddAsync(flow, cancellationToken);
                    action.LinkCapitalFlow(flow.Id);
                }
            }
        }

        await _actions.UpdateAsync(action, cancellationToken);
        return Unit.Value;
    }

    /// <summary>Tiền cổ tức sau thuế, tính trên số lượng nắm giữ tại ngày GDKHQ.</summary>
    private async Task<decimal> ComputeNetDividendAsync(CorporateAction action, CancellationToken cancellationToken)
    {
        var trades = await _trades.GetByPortfolioIdAndSymbolAsync(action.PortfolioId, action.Symbol, cancellationToken);
        var priorActions = (await _actions.GetByPortfolioIdAndSymbolAsync(action.PortfolioId, action.Symbol, cancellationToken))
            .Where(a => a.Id != action.Id && a.ExDate.Date < action.ExDate.Date);

        var position = PositionBuilder
            .Build(trades, priorActions, asOf: action.ExDate.Date.AddDays(-1))
            .FirstOrDefault(p => string.Equals(p.Symbol, action.Symbol, StringComparison.OrdinalIgnoreCase));

        return (position?.TotalQuantity ?? 0m) * action.NetPerShare;
    }
}
