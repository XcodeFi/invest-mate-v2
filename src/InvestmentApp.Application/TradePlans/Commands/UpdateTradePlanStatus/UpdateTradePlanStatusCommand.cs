using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.UpdateTradePlanStatus;

public class UpdateTradePlanStatusCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? TradeId { get; set; }
    /// <summary>
    /// Lý do vượt cổng kỳ vọng khi chuyển sang Ready mà EV ≤ 0 hoặc chưa ước lượng (tối thiểu 10 ký tự). Bỏ trống nếu EV dương.
    /// </summary>
    public string? EvGateReason { get; set; }
}

public class UpdateTradePlanStatusCommandHandler : IRequestHandler<UpdateTradePlanStatusCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IFeeCalculationService? _fees;

    public UpdateTradePlanStatusCommandHandler(
        ITradePlanRepository tradePlanRepository,
        ITradeRepository tradeRepository,
        IPortfolioRepository portfolioRepository,
        IFeeCalculationService? fees = null)
    {
        _fees = fees;
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<Unit> Handle(UpdateTradePlanStatusCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Trade plan {request.Id} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        // Kiểm quyền trade TRƯỚC khi đổi trạng thái plan
        var linkedTrade = request.Status.ToLowerInvariant() == "executed"
            ? await TradeOwnership.LoadOwnedAsync(request.TradeId, request.UserId, _tradeRepository, _portfolioRepository, cancellationToken)
            : null;

        switch (request.Status.ToLowerInvariant())
        {
            case "ready":
                // Đường quyết định tường minh: qua cổng kỳ vọng. Các đường dưới (inprogress/executed tự chuỗi từ Draft) là ghi lại
                // lệnh đã khớp: không chặn, domain đánh dấu EvGateSkipped lúc khoá ảnh chụp.
                plan.MarkReadyWithEvGate(request.EvGateReason, PlanFees.Create(_fees));
                break;
            case "inprogress":
            case "in_progress":
                // Auto-chain: Draft → Ready → InProgress
                if (plan.Status == TradePlanStatus.Draft)
                    plan.MarkReady();
                plan.MarkInProgress();
                break;
            case "executed":
                if (string.IsNullOrEmpty(request.TradeId))
                    throw new ArgumentException("TradeId is required when marking as executed");
                // Auto-chain: Draft → Ready → InProgress → Executed
                if (plan.Status == TradePlanStatus.Draft)
                    plan.MarkReady();
                if (plan.Status == TradePlanStatus.Ready)
                    plan.MarkInProgress();
                plan.Execute(request.TradeId);
                // Link trade back to plan
                if (linkedTrade != null)
                {
                    linkedTrade.LinkTradePlan(plan.Id);
                    await _tradeRepository.UpdateAsync(linkedTrade, cancellationToken);
                }
                break;
            case "reviewed":
                throw new InvalidOperationException("Use the dedicated ReviewTradePlan endpoint to review a plan with campaign metrics");
            case "cancelled":
                plan.Cancel();
                break;
            case "restore":
                plan.Restore();
                break;
            default:
                throw new ArgumentException($"Invalid status: {request.Status}");
        }

        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
