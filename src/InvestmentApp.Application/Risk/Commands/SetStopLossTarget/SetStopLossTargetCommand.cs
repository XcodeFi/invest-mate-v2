using System.Text.Json.Serialization;
using MediatR;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Risk.Commands.SetStopLossTarget;

public class SetStopLossTargetCommand : IRequest<string>
{
    [JsonIgnore]
    public string? UserId { get; set; }
    public string TradeId { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public decimal EntryPrice { get; set; }
    public decimal StopLossPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public decimal? TrailingStopPercent { get; set; }
}

public class SetStopLossTargetCommandHandler : IRequestHandler<SetStopLossTargetCommand, string>
{
    private readonly IStopLossTargetRepository _stopLossTargetRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ITradeRepository _tradeRepository;

    public SetStopLossTargetCommandHandler(
        IStopLossTargetRepository stopLossTargetRepository,
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository)
    {
        _stopLossTargetRepository = stopLossTargetRepository;
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<string> Handle(SetStopLossTargetCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            throw new ArgumentException("Portfolio not found or access denied");

        var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null || trade.PortfolioId != request.PortfolioId)
            throw new ArgumentException("Trade not found or does not belong to portfolio");

        var existing = await _stopLossTargetRepository.GetByTradeIdAsync(request.TradeId, cancellationToken);

        if (existing != null)
        {
            existing.UpdateStopLoss(request.StopLossPrice);
            existing.UpdateTarget(request.TargetPrice);
            if (request.TrailingStopPercent.HasValue)
                existing.UpdateTrailingStop(request.TrailingStopPercent.Value, request.EntryPrice);
            await _stopLossTargetRepository.UpdateAsync(existing, cancellationToken);
            return existing.Id;
        }
        else
        {
            var slTarget = new StopLossTarget(
                request.TradeId,
                request.PortfolioId,
                request.UserId!,
                request.Symbol,
                request.EntryPrice,
                request.StopLossPrice,
                request.TargetPrice,
                request.TrailingStopPercent);
            await _stopLossTargetRepository.AddAsync(slTarget, cancellationToken);
            return slTarget.Id;
        }
    }
}
