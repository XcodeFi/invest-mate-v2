using System.Text.Json.Serialization;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;

public class UpdateTradePlanCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string? PortfolioId { get; set; }
    public string? Symbol { get; set; }
    public string? Direction { get; set; }
    public decimal? EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? Target { get; set; }
    public int? Quantity { get; set; }
    public string? StrategyId { get; set; }
    public string? MarketCondition { get; set; }
    public string? Thesis { get; set; }
    public string? Notes { get; set; }
    public List<InvalidationRuleDto>? InvalidationCriteria { get; set; }
    public DateTime? ExpectedReviewDate { get; set; }

    // Deprecation shim: accept legacy `reason` key from old clients.
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
    public int? ConfidenceLevel { get; set; }
    public List<ChecklistItemDto>? Checklist { get; set; }
    public string? EntryMode { get; set; }
    public List<PlanLotDto>? Lots { get; set; }
    public List<ExitTargetDto>? ExitTargets { get; set; }
    public string? ExitStrategyMode { get; set; }
    public List<ScenarioNodeDto>? ScenarioNodes { get; set; }
    public string? TimeHorizon { get; set; }
}

public class UpdateTradePlanCommandHandler : IRequestHandler<UpdateTradePlanCommand, Unit>
{
    private readonly ITradePlanRepository _tradePlanRepository;
    private readonly ICompanyDossierGate _dossierGate;

    public UpdateTradePlanCommandHandler(ITradePlanRepository tradePlanRepository, ICompanyDossierGate dossierGate)
    {
        _tradePlanRepository = tradePlanRepository;
        _dossierGate = dossierGate;
    }

    public async Task<Unit> Handle(UpdateTradePlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _tradePlanRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new Exception($"Trade plan {request.Id} not found");

        if (plan.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to update this trade plan");

        // UpdateTradePlanCommand.Quantity/EntryPrice/AccountBalance/Symbol đều nullable —
        // update là PARTIAL (TradePlan.Update chỉ gán khi HasValue), và SetLots (nếu có lots)
        // ghi đè Quantity sau đó. Cả 4 vế phải fallback về giá trị plan SẼ có sau khi lưu,
        // không phải giá trị thô trên request — gộp vào ResolveEffectiveGateInputs (dùng
        // chung với đường tạo) thay vì 3-4 vế "?? " tách rời như trước, đó là lý do bug
        // đọc-sai-thời-điểm lặp lại nhiều lần.
        // willApplyLots phải khớp ĐÚNG điều kiện gọi SetLots bên dưới — mỗi handler tự tính
        // rồi truyền vào, và dùng lại chính biến đó ở chỗ ghi.
        // `Count > 0` để `lots: []` không chạy SetLots(mode, []) rồi gán Quantity = 0 sau khi
        // cổng đã chấm theo quantity cũ. Đổi lại: không có cách xoá hết lots qua API — FE hiện
        // gửi `undefined` khi rỗng nên không ai mất đường nào.
        var willApplyLots = request.EntryMode != null && request.Lots is { Count: > 0 };
        var oldSize = plan.Quantity * plan.EntryPrice;
        var gateInputs = CreateTradePlanCommandHandler.ResolveEffectiveGateInputs(
            existingPlan: plan,
            request.Quantity, request.Lots, willApplyLots,
            request.EntryPrice, request.AccountBalance, request.Symbol);
        var newSize = gateInputs.PlanSize;
        var newBalance = gateInputs.AccountBalance;

        var oldThreshold = (plan.AccountBalance ?? 0m) * TradePlan.LargeTierThreshold;
        var newThreshold = (newBalance ?? 0m) * TradePlan.LargeTierThreshold;

        // So TỶ LỆ ở hai thời điểm, mỗi vế dùng số dư của chính thời điểm đó. Nếu ngưỡng
        // mới vẫn tính theo số dư cũ thì một request vừa nâng size vừa hạ số dư sẽ lọt.
        var wasBelow = !plan.AccountBalance.HasValue
            || plan.AccountBalance.Value <= 0m
            || oldSize < oldThreshold;
        var isNowAtOrAbove = newBalance.HasValue
            && newBalance.Value > 0m
            && newSize >= newThreshold;

        // Đổi mã là mở một vị thế mới ở một công ty khác, nên áp đúng cổng mà đường TẠO
        // sẽ áp — chấm theo mã MỚI, với mọi lần đổi mã (không chỉ khi vượt ngưỡng): đường
        // tạo chặn cả lệnh nhỏ (bậc nhỏ đòi BusinessModel), nên đường sửa cũng phải vậy.
        var newSymbol = gateInputs.Symbol.ToUpper().Trim();
        var symbolChanged = newSymbol != plan.Symbol;

        if (symbolChanged || (wasBelow && isNowAtOrAbove))
        {
            await _dossierGate.EnsureAsync(plan.UserId, newSymbol, newSize, newBalance, cancellationToken);
        }

        var checklist = request.Checklist?.Select(c => new ChecklistItem
        {
            Label = c.Label,
            Category = c.Category,
            Checked = c.Checked,
            Critical = c.Critical,
            Hint = c.Hint
        }).ToList();

        TimeHorizon? timeHorizon = request.TimeHorizon != null
            && Enum.TryParse<TimeHorizon>(request.TimeHorizon, ignoreCase: true, out var th) ? th : null;

        var invalidationCriteria = request.InvalidationCriteria?.Select(r => new InvalidationRule
        {
            Trigger = Enum.Parse<InvalidationTrigger>(r.Trigger, ignoreCase: true),
            Detail = r.Detail,
            CheckDate = r.CheckDate,
            IsTriggered = r.IsTriggered,
            TriggeredAt = r.TriggeredAt
        }).ToList();

        plan.Update(
            request.Symbol, request.Direction, request.EntryPrice,
            request.StopLoss, request.Target, request.Quantity,
            request.PortfolioId, request.StrategyId, request.MarketCondition,
            request.Thesis, request.Notes, request.RiskPercent,
            request.AccountBalance, request.RiskRewardRatio,
            request.ConfidenceLevel, checklist,
            timeHorizon,
            invalidationCriteria,
            request.ExpectedReviewDate
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
        if (request.ExitTargets != null)
        {
            var targets = request.ExitTargets.Select(e => new ExitTarget
            {
                Level = e.Level,
                ActionType = Enum.Parse<ExitActionType>(e.ActionType, ignoreCase: true),
                Price = e.Price,
                Quantity = e.Quantity,
                PercentOfPosition = e.PercentOfPosition,
                Label = e.Label
            }).ToList();
            plan.SetExitTargets(targets);
        }

        // Scenario Playbook
        if (request.ExitStrategyMode != null)
        {
            var mode = Enum.Parse<ExitStrategyMode>(request.ExitStrategyMode, ignoreCase: true);
            plan.SetExitStrategyMode(mode);
        }
        if (request.ScenarioNodes != null && plan.ExitStrategyMode == ExitStrategyMode.Advanced)
        {
            var nodes = request.ScenarioNodes.Select(CreateTradePlanCommandHandler.MapToScenarioNode).ToList();
            plan.SetScenarioNodes(nodes);
        }

        await _tradePlanRepository.UpdateAsync(plan, cancellationToken);
        return Unit.Value;
    }
}
