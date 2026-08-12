using System.Text.Json.Serialization;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;

public class CreateTradePlanCommand : IRequest<string>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string Symbol { get; set; } = null!;
    public string Direction { get; set; } = "Buy";
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target { get; set; }
    public int Quantity { get; set; }
    public string? StrategyId { get; set; }
    public string MarketCondition { get; set; } = "Trending";
    public string? Thesis { get; set; }
    public string? Notes { get; set; }
    public List<InvalidationRuleDto>? InvalidationCriteria { get; set; }
    public DateTime? ExpectedReviewDate { get; set; }

    // Deprecation shim: accept legacy `reason` key from old clients.
    // Will be removed in the next release after migration.
    [Obsolete("Use Thesis instead. Kept for one release for legacy client compatibility.")]
    [System.Text.Json.Serialization.JsonPropertyName("reason")]
    public string? Reason
    {
        get => Thesis;
        set { if (Thesis == null && value != null) Thesis = value; }
    }
    public decimal? RiskPercent { get; set; }
    public decimal? AccountBalance { get; set; }
    public decimal? RiskRewardRatio { get; set; }
    public int ConfidenceLevel { get; set; } = 5;
    public List<ChecklistItemDto>? Checklist { get; set; }
    public string? EntryMode { get; set; }
    public List<PlanLotDto>? Lots { get; set; }
    public List<ExitTargetDto>? ExitTargets { get; set; }
    public string? ExitStrategyMode { get; set; }
    public List<ScenarioNodeDto>? ScenarioNodes { get; set; }
    public string? TimeHorizon { get; set; }
    public string? Status { get; set; }
    public string? TradeId { get; set; }
}

public class ChecklistItemDto
{
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Checked { get; set; }
    public bool Critical { get; set; }
    public string Hint { get; set; } = string.Empty;
}

public class InvalidationRuleDto
{
    [System.ComponentModel.Description("Sự kiện phủ định luận điểm. Bắt buộc.")]
    public InvalidationTrigger? Trigger { get; set; }

    public string Detail { get; set; } = string.Empty;
    public DateTime? CheckDate { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
}

public class CreateTradePlanCommandHandler : IRequestHandler<CreateTradePlanCommand, string>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly ICompanyDossierGate _dossierGate;

    public CreateTradePlanCommandHandler(ITradePlanRepository tradePlanRepository, ITradeRepository tradeRepository,
        ICompanyDossierGate dossierGate)
    {
        _tradePlanRepository = tradePlanRepository;
        _tradeRepository = tradeRepository;
        _dossierGate = dossierGate;
    }

    public async Task<string> Handle(CreateTradePlanCommand request, CancellationToken cancellationToken)
    {
        // Chấm theo giá trị plan SẼ có sau khi lưu (quantity sau SetLots nếu có lots), không
        // phải giá trị thô trên request — nếu không, header nhỏ + lots to là qua cổng bậc nhỏ
        // rồi Quantity thật lại phình lên sau khi lưu. existingPlan=null: đường tạo không có
        // fallback, mọi field trên request đã bắt buộc.
        // willApplyLots tính MỘT lần ở đây và dùng lại nguyên biến đó ở chỗ gọi SetLots bên dưới:
        // cổng và chỗ ghi buộc phải cùng một điều kiện, không phải hai biểu thức viết giống nhau.
        var willApplyLots = request.EntryMode != null && request.Lots is { Count: > 0 };
        var gateInputs = ResolveEffectiveGateInputs(
            existingPlan: null,
            request.Quantity, request.Lots, willApplyLots,
            request.EntryPrice, request.AccountBalance, request.Symbol);
        await _dossierGate.EnsureAsync(request.UserId, gateInputs.Symbol, gateInputs.PlanSize, gateInputs.AccountBalance, cancellationToken);

        var checklist = request.Checklist?.Select(c => new ChecklistItem
        {
            Label = c.Label,
            Category = c.Category,
            Checked = c.Checked,
            Critical = c.Critical,
            Hint = c.Hint
        }).ToList();

        var invalidationCriteria = request.InvalidationCriteria?.Select(r => new InvalidationRule
        {
            Trigger = r.Trigger ?? InvalidationTrigger.Manual,
            Detail = r.Detail,
            CheckDate = r.CheckDate,
            IsTriggered = r.IsTriggered,
            TriggeredAt = r.TriggeredAt
        }).ToList();

        var plan = new TradePlan(
            request.UserId, request.Symbol, request.Direction,
            request.EntryPrice, request.StopLoss, request.Target, request.Quantity,
            request.PortfolioId, request.StrategyId,
            request.MarketCondition, request.Thesis, request.Notes,
            request.RiskPercent, request.AccountBalance, request.RiskRewardRatio,
            request.ConfidenceLevel, checklist,
            invalidationCriteria: invalidationCriteria,
            expectedReviewDate: request.ExpectedReviewDate
        );

        // Multi-lot support — willApplyLots là biến cổng đã chấm theo, không viết lại điều kiện.
        if (willApplyLots)
        {
            var entryMode = Enum.Parse<EntryMode>(request.EntryMode, ignoreCase: true);
            var lots = request.Lots.Select(l => new PlanLot
            {
                LotNumber = l.LotNumber,
                PlannedPrice = l.PlannedPrice,
                PlannedQuantity = l.PlannedQuantity,
                AllocationPercent = l.AllocationPercent,
                Label = l.Label
            }).ToList();
            plan.SetLots(entryMode, lots);
        }

        // Exit targets
        if (request.ExitTargets != null && request.ExitTargets.Count > 0)
        {
            var targets = request.ExitTargets.Select(e => new ExitTarget
            {
                Level = e.Level,
                ActionType = e.ActionType ?? ExitActionType.TakeProfit,
                Price = e.Price,
                Quantity = e.Quantity,
                PercentOfPosition = e.PercentOfPosition,
                Label = e.Label
            }).ToList();
            plan.SetExitTargets(targets);
        }

        // Time horizon
        if (request.TimeHorizon != null && Enum.TryParse<TimeHorizon>(request.TimeHorizon, ignoreCase: true, out var horizon))
            plan.SetTimeHorizon(horizon);

        // Scenario Playbook. Nodes nằm ngoài nhánh Advanced có chủ đích: lồng vào trong khiến
        // nodes gửi kèm mà thiếu exitStrategyMode bị bỏ đi im lặng. SetScenarioNodes tự từ chối
        // khi còn ở Simple, và đó là câu trả lời người gọi cần nghe.
        if (request.ExitStrategyMode?.Equals("Advanced", StringComparison.OrdinalIgnoreCase) == true)
            plan.SetExitStrategyMode(ExitStrategyMode.Advanced);

        if (request.ScenarioNodes is { Count: > 0 })
            plan.SetScenarioNodes(request.ScenarioNodes.Select(MapToScenarioNode).ToList());

        // Handle initial status if provided (e.g., "Ready" or "Executed" from wizard)
        // Must follow sequential state machine: Draft → Ready → InProgress → Executed
        if (request.Status == "Ready")
            plan.MarkReady();
        else if (request.Status == "Executed" && request.TradeId != null)
        {
            plan.MarkReady();
            plan.MarkInProgress();
            plan.Execute(request.TradeId);
        }

        await _tradePlanRepository.AddAsync(plan, cancellationToken);

        // Link trade to plan if executing
        if (request.TradeId != null)
        {
            var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken);
            if (trade != null)
            {
                trade.LinkTradePlan(plan.Id);
                await _tradeRepository.UpdateAsync(trade, cancellationToken);
            }
        }

        return plan.Id;
    }

    /// <summary>
    /// 4 giá trị mà plan SẼ có sau khi lưu — quantity sau SetLots (nếu có lots),
    /// entryPrice/accountBalance/symbol sau merge partial của <see cref="TradePlan.Update"/>
    /// (nếu là đường sửa). Dùng chung cho cả đường tạo (existingPlan=null, request đã đủ
    /// mọi field bắt buộc, fallback không bao giờ kích hoạt) và đường sửa (existingPlan có,
    /// request là partial). Gộp lại vì đây từng là 3 vế "?? " tách rời trên đường sửa — lý
    /// do bug đọc-sai-thời-điểm lặp lại nhiều lần, một chỗ tính + test còn dễ tin hơn.
    /// </summary>
    /// <param name="willApplyLots">
    /// Phải là CHÍNH biến mà handler dùng để quyết định có gọi SetLots hay không, không phải
    /// một điều kiện suy ra lần thứ hai từ <paramref name="lots"/>. Suy ra lần hai là cách cửa
    /// hậu quay lại: lots có phần tử mà thiếu EntryMode thì SetLots không chạy, Quantity giữ
    /// giá trị header, nhưng cổng lại chấm theo tổng lots — hạ bậc đúng bằng tỷ lệ hai số đó.
    /// </param>
    internal static GateInputs ResolveEffectiveGateInputs(
        TradePlan? existingPlan,
        int? requestedQuantity, List<PlanLotDto>? lots, bool willApplyLots,
        decimal? requestedEntryPrice, decimal? requestedAccountBalance, string? requestedSymbol)
    {
        var quantity = requestedQuantity ?? existingPlan?.Quantity
            ?? throw new InvalidOperationException("Quantity là bắt buộc để chấm cổng dossier");

        var entryPrice = requestedEntryPrice ?? existingPlan?.EntryPrice
            ?? throw new InvalidOperationException("EntryPrice là bắt buộc để chấm cổng dossier");

        var planSize = quantity * entryPrice;
        if (willApplyLots && lots is { Count: > 0 })
        {
            // SetLots ghi Quantity = tổng số lượng lô nhưng KHÔNG chạm EntryPrice, nên plan lưu
            // xuống có size = tổng lô × giá header; còn vốn thật cam kết ở các lô là tổng(lô × giá lô).
            // Lấy mức LỚN HƠN vì mỗi vế đều bỏ trống được: giá lô để 0 thì vế lô bằng 0, giá header
            // để 1đ thì vế header gần 0. Chấm theo mức nhỏ hơn là mở lại đường hạ bậc.
            var lotsQuantity = lots.Sum(l => l.PlannedQuantity);
            planSize = Math.Max(lots.Sum(l => l.PlannedQuantity * l.PlannedPrice), lotsQuantity * entryPrice);
        }

        var accountBalance = requestedAccountBalance ?? existingPlan?.AccountBalance;

        var symbol = requestedSymbol ?? existingPlan?.Symbol
            ?? throw new InvalidOperationException("Symbol là bắt buộc để chấm cổng dossier");

        return new GateInputs(planSize, accountBalance, symbol);
    }

    // Trả thẳng PlanSize thay vì (Quantity, EntryPrice) để không caller nào nhân lại theo cách
    // riêng — công thức size chỉ tồn tại ở một chỗ.
    internal readonly record struct GateInputs(decimal PlanSize, decimal? AccountBalance, string Symbol);

    internal static ScenarioNode MapToScenarioNode(ScenarioNodeDto dto) => new()
    {
        NodeId = dto.NodeId,
        ParentId = dto.ParentId,
        Order = dto.Order,
        Label = dto.Label,
        // Validator chặn null trước khi tới đây. Giá trị lấp chỗ chọn loại vô hại nhất: nếu
        // validator có lỗ thủng thì hậu quả là một thông báo thừa, không phải lệnh bán ngoài ý muốn.
        ConditionType = dto.ConditionType ?? ScenarioConditionType.PriceAbove,
        ConditionValue = dto.ConditionValue,
        ConditionNote = dto.ConditionNote,
        ActionType = dto.ActionType ?? ScenarioActionType.SendNotification,
        ActionValue = dto.ActionValue,
        TrailingStopConfig = dto.TrailingStopConfig != null ? new TrailingStopConfig
        {
            // Đơn vị đo được phép có mặc định; hành động thì không.
            Method = dto.TrailingStopConfig.Method ?? TrailingStopMethod.Percentage,
            TrailValue = dto.TrailingStopConfig.TrailValue,
            ActivationPrice = dto.TrailingStopConfig.ActivationPrice,
            StepSize = dto.TrailingStopConfig.StepSize
        } : null
    };
}
