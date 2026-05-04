using System.Text.Json.Serialization;
using FluentValidation;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Decisions.Commands.ResolveDecision;

/// <summary>
/// Action mà user chọn trên 1 DecisionItem trong Decision Queue.
/// (P4 — Decision Engine v1.1 — xem `docs/plans/dashboard-decision-engine.md` §6).
/// </summary>
public enum DecisionAction
{
    /// <summary>BÁN theo plan: tạo Trade SELL với quantity tính từ plan + giá hiện tại.</summary>
    ExecuteSell,

    /// <summary>GIỮ + ghi lý do: tạo JournalEntry kiểu Decision với note ≥ 20 ký tự.</summary>
    HoldWithJournal
}

/// <summary>
/// Resolve 1 DecisionItem inline từ Decision Queue widget. Hai action:
///   - <see cref="DecisionAction.ExecuteSell"/>: yêu cầu <see cref="TradePlanId"/> để tính quantity.
///     Single-lot dùng `plan.Quantity`. Multi-lot sums `lot.PlannedQuantity` của Executed lots.
///     Tạo Trade SELL với giá hiện tại + link plan + cập nhật portfolio.
///   - <see cref="DecisionAction.HoldWithJournal"/>: tạo JournalEntry với EntryType=Decision,
///     Note ≥ 20 ký tự (force user suy nghĩ thật sự thay vì click cho qua).
/// </summary>
public class ResolveDecisionCommand : IRequest<ResolveDecisionResult>
{
    /// <summary>Composite id từ DecisionItemDto (dùng cho audit trail; handler không parse).</summary>
    public string DecisionId { get; set; } = null!;

    public DecisionAction Action { get; set; }

    /// <summary>TradePlan để load — bắt buộc cho ExecuteSell, optional cho HoldWithJournal (link nếu có).</summary>
    public string? TradePlanId { get; set; }

    /// <summary>Symbol (cho HoldWithJournal khi không có TradePlanId — e.g. StopLossHit không link plan).</summary>
    public string? Symbol { get; set; }

    /// <summary>Lý do giữ — bắt buộc cho HoldWithJournal, ≥ 20 ký tự.</summary>
    public string? Note { get; set; }

    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class ResolveDecisionResult
{
    public string ResultId { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string ResultType { get; set; } = null!;  // "Trade" | "JournalEntry"
}

public class ResolveDecisionCommandValidator : AbstractValidator<ResolveDecisionCommand>
{
    public const int MinNoteLength = 20;

    public ResolveDecisionCommandValidator()
    {
        RuleFor(x => x.DecisionId).NotEmpty().WithMessage("DecisionId không được rỗng");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId không được rỗng");

        When(x => x.Action == DecisionAction.ExecuteSell, () =>
        {
            RuleFor(x => x.TradePlanId)
                .NotEmpty()
                .WithMessage("ExecuteSell yêu cầu TradePlanId — không thể bán không có plan");
        });

        When(x => x.Action == DecisionAction.HoldWithJournal, () =>
        {
            RuleFor(x => x.Note)
                .NotEmpty()
                .WithMessage("Note không được rỗng khi GIỮ + ghi lý do")
                .Must(n => n != null && n.Trim().Length >= MinNoteLength)
                .WithMessage($"Note phải có ít nhất {MinNoteLength} ký tự — buộc bạn nghĩ kỹ trước khi giữ");
        });
    }
}

public class ResolveDecisionCommandHandler : IRequestHandler<ResolveDecisionCommand, ResolveDecisionResult>
{
    private readonly ITradePlanRepository _planRepo;
    private readonly ITradeRepository _tradeRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IStockPriceService _priceService;
    private readonly IAuditService _auditService;

    public ResolveDecisionCommandHandler(
        ITradePlanRepository planRepo,
        ITradeRepository tradeRepo,
        IPortfolioRepository portfolioRepo,
        IJournalEntryRepository journalRepo,
        IStockPriceService priceService,
        IAuditService auditService)
    {
        _planRepo = planRepo;
        _tradeRepo = tradeRepo;
        _portfolioRepo = portfolioRepo;
        _journalRepo = journalRepo;
        _priceService = priceService;
        _auditService = auditService;
    }

    public async Task<ResolveDecisionResult> Handle(ResolveDecisionCommand request, CancellationToken ct)
    {
        // Validator chạy auto qua FluentValidation pipeline / attribute-based validation. Re-validate
        // ở đây làm safety net cho test path không qua pipeline + clear error message khi gọi trực tiếp.
        new ResolveDecisionCommandValidator().ValidateAndThrow(request);

        return request.Action switch
        {
            DecisionAction.ExecuteSell => await HandleExecuteSellAsync(request, ct),
            DecisionAction.HoldWithJournal => await HandleHoldWithJournalAsync(request, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Action), $"Action {request.Action} chưa hỗ trợ")
        };
    }

    private async Task<ResolveDecisionResult> HandleExecuteSellAsync(ResolveDecisionCommand request, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(request.TradePlanId!, ct)
            ?? throw new InvalidOperationException($"TradePlan {request.TradePlanId} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Plan thuộc user khác");

        if (string.IsNullOrEmpty(plan.PortfolioId))
            throw new InvalidOperationException("Plan không gắn portfolio — không thể tạo Trade");

        var portfolio = await _portfolioRepo.GetByIdAsync(plan.PortfolioId, ct)
            ?? throw new InvalidOperationException($"Portfolio {plan.PortfolioId} not found");

        // Defense in depth: even though plan.UserId was already checked, a plan owned by user-A
        // might point to user-B's portfolio if data was tampered. Verify portfolio ownership.
        if (portfolio.UserId != request.UserId)
            throw new UnauthorizedAccessException("Portfolio thuộc user khác");

        // Tính quantity:
        //   - Single-lot (Lots null/empty): dùng plan.Quantity (entry-side; sell toàn bộ position theo plan).
        //   - Multi-lot: sum PlannedQuantity của các lot Status=Executed (= position đã mở thật theo plan).
        decimal quantity = plan.Lots != null && plan.Lots.Count > 0
            ? plan.Lots.Where(l => l.Status == PlanLotStatus.Executed).Sum(l => (decimal)l.PlannedQuantity)
            : plan.Quantity;

        if (quantity <= 0)
            throw new InvalidOperationException("Position theo plan đã đóng (quantity ≤ 0) — refresh queue");

        var price = await _priceService.GetCurrentPriceAsync(new Domain.ValueObjects.StockSymbol(plan.Symbol));
        if (price.Amount <= 0)
            throw new InvalidOperationException($"Không lấy được giá hiện tại của {plan.Symbol}");

        var trade = new Trade(plan.PortfolioId, plan.Symbol, TradeType.SELL, quantity, price.Amount);
        trade.LinkTradePlan(plan.Id);

        await _tradeRepo.AddAsync(trade, ct);
        portfolio.AddTrade(trade);
        await _portfolioRepo.UpdateAsync(portfolio, ct);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = request.UserId,
            Action = "RESOLVE_DECISION_EXECUTE_SELL",
            EntityId = trade.Id,
            Metadata = new
            {
                request.DecisionId,
                TradePlanId = plan.Id,
                plan.Symbol,
                Quantity = quantity,
                Price = price.Amount
            }
        }, ct);

        return new ResolveDecisionResult
        {
            ResultId = trade.Id,
            ResultType = "Trade",
            Message = $"Đã tạo lệnh BÁN {quantity:N0} {plan.Symbol} @ {price.Amount:N0}"
        };
    }

    private async Task<ResolveDecisionResult> HandleHoldWithJournalAsync(ResolveDecisionCommand request, CancellationToken ct)
    {
        string symbol;
        string? portfolioId = null;
        string? tradePlanId = null;

        if (!string.IsNullOrEmpty(request.TradePlanId))
        {
            var plan = await _planRepo.GetByIdAsync(request.TradePlanId, ct)
                ?? throw new InvalidOperationException($"TradePlan {request.TradePlanId} not found");
            if (plan.UserId != request.UserId)
                throw new UnauthorizedAccessException("Plan thuộc user khác");
            symbol = plan.Symbol;
            portfolioId = plan.PortfolioId;
            tradePlanId = plan.Id;
        }
        else if (!string.IsNullOrEmpty(request.Symbol))
        {
            symbol = request.Symbol;
        }
        else
        {
            throw new InvalidOperationException("HoldWithJournal cần TradePlanId hoặc Symbol");
        }

        var entry = new JournalEntry(
            userId: request.UserId,
            symbol: symbol,
            entryType: JournalEntryType.Decision,
            title: $"Quyết định giữ — {symbol}",
            content: request.Note!.Trim(),
            portfolioId: portfolioId,
            tradePlanId: tradePlanId,
            tags: new List<string> { "decision-hold", $"trigger:{request.DecisionId.Split(':')[0]}" });

        await _journalRepo.AddAsync(entry, ct);

        await _auditService.LogAsync(new AuditEntry
        {
            UserId = request.UserId,
            Action = "RESOLVE_DECISION_HOLD_WITH_JOURNAL",
            EntityId = entry.Id,
            Metadata = new { request.DecisionId, Symbol = symbol, TradePlanId = tradePlanId }
        }, ct);

        return new ResolveDecisionResult
        {
            ResultId = entry.Id,
            ResultType = "JournalEntry",
            Message = $"Đã ghi lý do giữ {symbol} vào nhật ký"
        };
    }
}
