using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Commands.CreateCorporateAction;

public record CreateCorporateActionCommand(
    string UserId,
    string PortfolioId,
    string Symbol,
    CorporateActionType Type,
    DateTime ExDate,
    DateTime? SettlementDate,
    decimal? PercentOfPar,
    decimal? TaxRatePercent,
    decimal? RatioOld,
    decimal? RatioNew,
    string? Note) : IRequest<string>;

public class CreateCorporateActionCommandHandler
    : IRequestHandler<CreateCorporateActionCommand, string>
{
    private readonly ICorporateActionRepository _actions;
    private readonly IPortfolioRepository _portfolios;

    public CreateCorporateActionCommandHandler(
        ICorporateActionRepository actions, IPortfolioRepository portfolios)
    {
        _actions = actions;
        _portfolios = portfolios;
    }

    public async Task<string> Handle(CreateCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolios.GetByIdAsync(request.PortfolioId, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy danh mục", nameof(request.PortfolioId));
        if (portfolio.UserId != request.UserId)
            throw new UnauthorizedAccessException("Danh mục không thuộc về người dùng này");

        var action = request.Type switch
        {
            CorporateActionType.CashDividend => CorporateAction.CashDividend(
                request.PortfolioId, request.UserId, request.Symbol,
                request.PercentOfPar ?? throw new ArgumentException("Thiếu tỷ lệ cổ tức tiền mặt", nameof(request.PercentOfPar)),
                request.ExDate, request.SettlementDate, request.TaxRatePercent ?? 5m, request.Note),

            CorporateActionType.StockDividend => CorporateAction.StockDividend(
                request.PortfolioId, request.UserId, request.Symbol,
                request.RatioOld ?? throw new ArgumentException("Thiếu tỷ lệ cũ", nameof(request.RatioOld)),
                request.RatioNew ?? throw new ArgumentException("Thiếu tỷ lệ mới", nameof(request.RatioNew)),
                request.ExDate, request.SettlementDate, request.Note),

            CorporateActionType.StockSplit => CorporateAction.StockSplit(
                request.PortfolioId, request.UserId, request.Symbol,
                request.RatioOld ?? throw new ArgumentException("Thiếu tỷ lệ cũ", nameof(request.RatioOld)),
                request.RatioNew ?? throw new ArgumentException("Thiếu tỷ lệ mới", nameof(request.RatioNew)),
                request.ExDate, request.SettlementDate, request.Note),

            _ => throw new ArgumentException("Loại sự kiện quyền không hợp lệ", nameof(request.Type))
        };

        await _actions.AddAsync(action, cancellationToken);
        return action.Id;
    }
}
