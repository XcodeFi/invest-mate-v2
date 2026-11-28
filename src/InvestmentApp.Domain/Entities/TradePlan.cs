using InvestmentApp.Domain.Events;
using InvestmentApp.Domain.Services;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class TradePlan : AggregateRoot
{
    /// <summary>Ngưỡng size/tài khoản để vào tầng kỷ luật lớn (§D3 plan Vin-discipline).
    /// Domain gate và Application dossier gate BUỘC PHẢI đồng ý với nhau — tham chiếu
    /// hằng số này thay vì tự sao công thức 0.05m.</summary>
    public const decimal LargeTierThreshold = 0.05m;

    public string UserId { get; private set; }
    public string? PortfolioId { get; private set; }

    // Core plan data
    public string Symbol { get; private set; }
    public string Direction { get; private set; }       // "Buy" | "Sell"
    public decimal EntryPrice { get; private set; }
    public decimal StopLoss { get; private set; }
    public decimal Target { get; private set; }
    public int Quantity { get; private set; }
    public string? StrategyId { get; private set; }
    public string MarketCondition { get; private set; }
    public string? Thesis { get; private set; }
    public string? Notes { get; private set; }

    // Thesis discipline (§D1 plan Vin-discipline)
    public List<InvalidationRule>? InvalidationCriteria { get; private set; }
    public DateTime? ExpectedReviewDate { get; private set; }
    public bool LegacyExempt { get; private set; } = false;

    // Risk calculations (snapshot at plan creation)
    public decimal? RiskPercent { get; private set; }
    public decimal? AccountBalance { get; private set; }
    public decimal? RiskRewardRatio { get; private set; }
    public int ConfidenceLevel { get; private set; }    // 1-10

    // Checklist
    public List<ChecklistItem> Checklist { get; private set; }

    // Multi-lot entry support (nullable for backward compat)
    public EntryMode? EntryMode { get; private set; }
    public List<PlanLot>? Lots { get; private set; }

    // Exit planning
    public List<ExitTarget>? ExitTargets { get; private set; }
    public List<StopLossHistoryEntry>? StopLossHistory { get; private set; }

    // Scenario Playbook (Advanced exit strategy)
    public ExitStrategyMode ExitStrategyMode { get; private set; } = ExitStrategyMode.Simple;
    public List<ScenarioNode>? ScenarioNodes { get; private set; }

    // Time horizon (for campaign comparison)
    public TimeHorizon? TimeHorizon { get; private set; }

    /// <summary>
    /// 1R cố định của kế hoạch. Chụp lần đầu rời Draft (sang Ready hoặc sang InProgress qua ExecuteLot), chụp lại
    /// mỗi lần sửa khi còn Ready, KHOÁ ở lần khớp đầu tiên, xoá khi Restore. SL dời sau đó không đổi 1R.
    /// Null với lệnh bán, SL không thấp hơn giá vào, và kế hoạch cũ (dùng <see cref="RMath.EstimateLegacy"/> khi đọc).
    /// </summary>
    public InitialRiskSnapshot? InitialRisk { get; private set; }

    /// <summary>
    /// Bảng kịch bản kết quả của CẢ vị thế (nhãn, xác suất %, giá thoát) do người dùng nhập: bản hiện hành, sửa được tới khi
    /// review. Chỉ lệnh mua. Null với kế hoạch cũ.
    /// </summary>
    public List<OutcomeScenario>? OutcomeScenarios { get; private set; }

    /// <summary>
    /// Ảnh chụp ước lượng (EV, % thắng ngầm định) lúc chốt kế hoạch: chụp khi rời Draft, chụp lại mỗi lần sửa khi còn Ready,
    /// KHOÁ ở lần khớp đầu cùng chỗ khoá <see cref="InitialRisk"/>, xoá khi Restore. Là mốc để đối chiếu ước lượng với thực tế.
    /// </summary>
    public EstimateSnapshot? Estimate { get; private set; }

    /// <summary>Lý do người dùng vượt cổng kỳ vọng (EV ≤ 0 hoặc chưa ước lượng) khi chuyển sang Sẵn sàng.</summary>
    public string? EvGateReason { get; private set; }

    /// <summary>
    /// Vào lệnh mà không qua cổng kỳ vọng: lúc khớp đầu tiên kỳ vọng ≤ 0 hoặc chưa ước lượng và không có lý do. Không chặn
    /// (lệnh đã xảy ra) nhưng để lại dấu vết cho review.
    /// </summary>
    public bool EvGateSkipped { get; private set; }

    // Campaign review data (set when plan is reviewed/closed)
    public CampaignReviewData? ReviewData { get; private set; }

    // Lifecycle
    public TradePlanStatus Status { get; private set; }
    public string? TradeId { get; private set; }
    public List<string>? TradeIds { get; private set; }
    public DateTime? ExecutedAt { get; private set; }

    // Audit
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Lần cuối người dùng đặt lại các mức giá của kế hoạch. Là mốc để điều chỉnh
    /// giá theo sự kiện quyền — <c>UpdatedAt</c> không dùng được vì còn nhảy mỗi lần
    /// một nhánh kịch bản kích hoạt. Null với kế hoạch cũ → lùi về <c>CreatedAt</c>.
    /// </summary>
    public DateTime? PricesSetAt { get; private set; }

    /// <summary>
    /// Mốc riêng cho ngưỡng giá nằm trong cây kịch bản. Tách khỏi <see cref="PricesSetAt"/>
    /// vì sửa nhánh kịch bản không đụng gì tới giá nhập — dùng chung một mốc thì thao tác đó
    /// sẽ vô hiệu hoá việc điều chỉnh giá nhập. Null → lùi về <see cref="PricesSetAt"/>.
    /// </summary>
    public DateTime? ScenarioPricesSetAt { get; private set; }

    [BsonConstructor]
    public TradePlan() { } // MongoDB

    public TradePlan(string userId, string symbol, string direction,
        decimal entryPrice, decimal stopLoss, decimal target, int quantity,
        string? portfolioId = null, string? strategyId = null,
        string? marketCondition = null, string? thesis = null, string? notes = null,
        decimal? riskPercent = null, decimal? accountBalance = null,
        decimal? riskRewardRatio = null, int confidenceLevel = 5,
        List<ChecklistItem>? checklist = null,
        TimeHorizon? timeHorizon = null,
        List<InvalidationRule>? invalidationCriteria = null,
        DateTime? expectedReviewDate = null,
        bool legacyExempt = false)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        PortfolioId = portfolioId;
        Symbol = symbol?.ToUpper().Trim() ?? throw new ArgumentNullException(nameof(symbol));
        Direction = direction ?? "Buy";
        EntryPrice = entryPrice;
        StopLoss = stopLoss;
        Target = target;
        Quantity = quantity;
        StrategyId = strategyId;
        MarketCondition = marketCondition ?? "Trending";
        Thesis = thesis;
        Notes = notes;
        InvalidationCriteria = invalidationCriteria;
        ExpectedReviewDate = expectedReviewDate;
        LegacyExempt = legacyExempt;
        RiskPercent = riskPercent;
        AccountBalance = accountBalance;
        RiskRewardRatio = riskRewardRatio;
        ConfidenceLevel = Math.Clamp(confidenceLevel, 1, 10);
        Checklist = checklist ?? new List<ChecklistItem>();
        TimeHorizon = timeHorizon;
        Status = TradePlanStatus.Draft;
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        PricesSetAt = CreatedAt;
    }

    public void Update(string? symbol = null, string? direction = null,
        decimal? entryPrice = null, decimal? stopLoss = null, decimal? target = null,
        int? quantity = null, string? portfolioId = null, string? strategyId = null,
        string? marketCondition = null, string? thesis = null, string? notes = null,
        decimal? riskPercent = null, decimal? accountBalance = null,
        decimal? riskRewardRatio = null, int? confidenceLevel = null,
        List<ChecklistItem>? checklist = null,
        TimeHorizon? timeHorizon = null,
        List<InvalidationRule>? invalidationCriteria = null,
        DateTime? expectedReviewDate = null)
    {
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot update an executed or reviewed plan");

        // So sánh TRƯỚC khi gán. Form sửa kế hoạch gửi lại toàn bộ trường mỗi lần lưu, nên
        // "có gửi giá" không có nghĩa là giá đổi — dời mốc theo đó thì sửa mỗi ghi chú cũng
        // huỷ việc điều chỉnh theo sự kiện quyền.
        var priceChanged =
            (entryPrice.HasValue && entryPrice.Value != EntryPrice) ||
            (stopLoss.HasValue && stopLoss.Value != StopLoss) ||
            (target.HasValue && target.Value != Target);

        if (symbol != null) Symbol = symbol.ToUpper().Trim();
        if (direction != null) Direction = direction;
        if (entryPrice.HasValue) EntryPrice = entryPrice.Value;
        if (stopLoss.HasValue) StopLoss = stopLoss.Value;
        if (target.HasValue) Target = target.Value;
        if (quantity.HasValue) Quantity = quantity.Value;
        if (portfolioId != null) PortfolioId = portfolioId;
        if (strategyId != null) StrategyId = strategyId;
        if (marketCondition != null) MarketCondition = marketCondition;
        if (thesis != null) Thesis = thesis;
        if (notes != null) Notes = notes;
        if (riskPercent.HasValue) RiskPercent = riskPercent;
        if (accountBalance.HasValue) AccountBalance = accountBalance;
        if (riskRewardRatio.HasValue) RiskRewardRatio = riskRewardRatio;
        if (confidenceLevel.HasValue) ConfidenceLevel = Math.Clamp(confidenceLevel.Value, 1, 10);
        if (checklist != null) Checklist = checklist;
        if (timeHorizon.HasValue) TimeHorizon = timeHorizon.Value;
        if (invalidationCriteria != null) InvalidationCriteria = invalidationCriteria;
        if (expectedReviewDate.HasValue) ExpectedReviewDate = expectedReviewDate.Value;
        if (priceChanged) PricesSetAt = DateTime.UtcNow;
        if (Status == TradePlanStatus.Ready) { RefreshInitialRisk(); RefreshEstimate(); }
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkReady()
    {
        if (Status == TradePlanStatus.Ready) return;
        if (Status != TradePlanStatus.Draft)
            throw new InvalidOperationException("Only draft plans can be marked ready");
        EnsureDisciplineGate();
        Status = TradePlanStatus.Ready;
        RefreshInitialRisk();
        RefreshEstimate();
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>
    /// Chuyển sang Sẵn sàng qua cổng kỳ vọng: lệnh mua có EV ≤ 0 hoặc chưa ước lượng thì BẮT BUỘC có lý do (cảnh báo + lý do,
    /// không chặn cứng). Lý do được lưu và hiện ở review. Lệnh bán không có cổng. <see cref="MarkReady"/> không tham số giữ
    /// nguyên, không cổng: các đường ghi lại sau sự kiện đi qua đó và được đánh dấu <see cref="EvGateSkipped"/> lúc khớp.
    /// </summary>
    public void MarkReadyWithEvGate(string? evGateReason, RoundTripFee? feeAtExit = null)
    {
        if (Status == TradePlanStatus.Ready) return;

        string? storedReason = null;
        if (!IsSellDirection())
        {
            var ev = ExpectedValue.ForPlan(this, feeAtExit).EvR;
            if (ev is null or <= 0m)
            {
                var reason = evGateReason?.Trim();
                if (string.IsNullOrEmpty(reason) || reason.Length < MinEvGateReasonLength)
                    throw new EvGateException(MinEvGateReasonLength);
                storedReason = reason;
            }
        }

        MarkReady();                 // có thể ném (kỷ luật thesis): chưa đổi gì
        EvGateReason = storedReason;
        RefreshEstimate(feeAtExit);  // chụp lại, lần này có phí
    }

    public const int MinEvGateReasonLength = 10;

    /// <summary>
    /// Đặt bảng kịch bản kết quả (bản hiện hành). Còn ở Ready thì chụp lại ước lượng; đã khoá thì ảnh chụp giữ nguyên.
    /// Truyền null hoặc rỗng để xoá bảng. Không đổi được kế hoạch đã review.
    /// </summary>
    public void SetOutcomeScenarios(IReadOnlyList<OutcomeScenario>? scenarios, RoundTripFee? feeAtExit = null)
    {
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Không đổi được ước lượng của kế hoạch đã review");

        OutcomeScenarios = scenarios is { Count: > 0 } ? scenarios.Select(s => s.Clone()).ToList() : null;
        if (Status == TradePlanStatus.Ready) RefreshEstimate(feeAtExit);
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>Chụp lại ảnh chụp ước lượng theo bảng hiện hành, trừ khi đã khoá. Lệnh bán không có ước lượng.</summary>
    public void RefreshEstimate(RoundTripFee? feeAtExit = null)
    {
        if (Estimate?.LockedAt != null) return;
        if (IsSellDirection() || OutcomeScenarios is not { Count: > 0 })
        {
            Estimate = null;
            return;
        }

        var e = ExpectedValue.ForPlan(this, feeAtExit);
        Estimate = new EstimateSnapshot
        {
            Scenarios = OutcomeScenarios.Select(s => s.Clone()).ToList(),
            EvR = e.EvR,
            ImpliedWinProbability = e.ImpliedWinPercent,
            FeesIncluded = feeAtExit != null,
            CapturedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Lần khớp đầu tiên: chụp nếu chưa có, khoá, và ghi lại việc vào lệnh không qua cổng kỳ vọng (EV ≤ 0 hoặc chưa ước lượng,
    /// không có lý do). Chỉ gọi từ chỗ khoá <see cref="InitialRisk"/>.
    /// </summary>
    private void LockEstimateAndRecordGate()
    {
        if (Estimate == null) RefreshEstimate();
        if (Estimate is { LockedAt: null }) Estimate.LockedAt = DateTime.UtcNow;

        EvGateSkipped = !IsSellDirection()
            && Estimate?.EvR is null or <= 0m
            && string.IsNullOrWhiteSpace(EvGateReason);
    }

    public void MarkInProgress()
    {
        if (Status != TradePlanStatus.Ready)
            throw new InvalidOperationException("Only ready plans can be marked in progress");
        EnsureDisciplineGate();
        Status = TradePlanStatus.InProgress;
        RefreshInitialRisk();
        LockInitialRisk();
        LockEstimateAndRecordGate();
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>Chụp lại 1R theo giá/khối lượng hiện tại, trừ khi đã khoá ở lần khớp đầu.</summary>
    private void RefreshInitialRisk()
    {
        if (InitialRisk?.LockedAt != null) return;
        InitialRisk = RMath.Capture(this);
    }

    private void LockInitialRisk()
    {
        if (InitialRisk != null && InitialRisk.LockedAt == null) InitialRisk.LockedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gate kỷ luật size-based (§D3 plan Vin-discipline).
    /// Throw <see cref="InvalidOperationException"/> nếu plan vi phạm yêu cầu thesis / invalidation rule.
    /// </summary>
    private void EnsureDisciplineGate()
    {
        // Legacy exempt ở Draft → skip (tới T+3 hard gate release 2 sẽ bỏ nhánh này).
        if (LegacyExempt && Status == TradePlanStatus.Draft) return;

        decimal planSize = Quantity * EntryPrice;
        bool requireFullDiscipline =
            AccountBalance.HasValue
            && AccountBalance.Value > 0m
            && planSize >= AccountBalance.Value * LargeTierThreshold;

        if (requireFullDiscipline)
        {
            if (string.IsNullOrWhiteSpace(Thesis) || Thesis.Length < 30)
                throw new InvalidOperationException(
                    "Thesis ≥ 30 ký tự bắt buộc với plan size ≥ 5% tài khoản");
            if (InvalidationCriteria == null || InvalidationCriteria.Count == 0)
                throw new InvalidOperationException(
                    "Phải có ≥ 1 invalidation rule với plan size ≥ 5% tài khoản");
            if (InvalidationCriteria.Any(r => string.IsNullOrWhiteSpace(r.Detail) || r.Detail.Length < 20))
                throw new InvalidOperationException(
                    "Mỗi invalidation rule phải có detail ≥ 20 ký tự");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Thesis) || Thesis.Length < 15)
                throw new InvalidOperationException(
                    "Thesis ≥ 15 ký tự bắt buộc (dù plan size nhỏ)");
        }
    }

    public void SetThesis(string thesis)
    {
        if (string.IsNullOrWhiteSpace(thesis))
            throw new ArgumentException("Thesis không được rỗng", nameof(thesis));
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Không thể thay đổi thesis trên plan đã review");
        Thesis = thesis;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetInvalidationCriteria(List<InvalidationRule> rules)
    {
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Không thể thay đổi invalidation criteria trên plan đã review");
        InvalidationCriteria = rules;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetExpectedReviewDate(DateTime? date)
    {
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Không thể thay đổi expected review date trên plan đã review");
        ExpectedReviewDate = date;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Execute(string tradeId)
    {
        if (Status != TradePlanStatus.InProgress)
            throw new InvalidOperationException("Only in-progress plans can be executed");
        TradeId = tradeId ?? throw new ArgumentNullException(nameof(tradeId));
        Status = TradePlanStatus.Executed;
        ExecutedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkReviewed(CampaignReviewData reviewData)
    {
        if (Status != TradePlanStatus.Executed)
            throw new InvalidOperationException("Only executed plans can be reviewed");
        ReviewData = reviewData ?? throw new ArgumentNullException(nameof(reviewData));
        Status = TradePlanStatus.Reviewed;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
        AddDomainEvent(new PlanReviewedEvent(Id, UserId, reviewData.PnLPercent));
    }

    public void UpdateReviewLessons(string lessonsLearned)
    {
        if (Status != TradePlanStatus.Reviewed || ReviewData == null)
            throw new InvalidOperationException("Can only update lessons on a reviewed plan");
        ReviewData.LessonsLearned = lessonsLearned;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetTimeHorizon(TimeHorizon horizon)
    {
        if (Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot change time horizon on a reviewed plan");
        TimeHorizon = horizon;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Cancel()
    {
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot cancel an executed or reviewed plan");
        Status = TradePlanStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Restore()
    {
        if (Status != TradePlanStatus.Cancelled)
            throw new InvalidOperationException("Only cancelled plans can be restored");
        Status = TradePlanStatus.Draft;
        TradeId = null;
        TradeIds?.Clear();
        ExecutedAt = null;
        InitialRisk = null;   // về Draft thì 1R chụp lại khi chốt lần sau
        Estimate = null;      // ước lượng cũng chụp lại; bảng kịch bản người dùng nhập giữ nguyên
        EvGateReason = null;  // cổng chạy lại ở lần chốt kế tiếp
        EvGateSkipped = false;

        // Clear IsTriggered flags on invalidation rules in case the cancellation came from AbortWithThesisInvalidation.
        // Rule text is preserved so user doesn't have to rewrite.
        if (InvalidationCriteria != null)
        {
            foreach (var rule in InvalidationCriteria)
            {
                rule.IsTriggered = false;
                rule.TriggeredAt = null;
            }
        }

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>
    /// Abort plan vì thesis đã sai (§D4 plan Vin-discipline).
    /// Áp cho state Ready | InProgress | Executed (B3 fix — multi-lot partial-executed).
    /// Throw khi Draft (dùng Cancel) hoặc Reviewed/Cancelled (terminal).
    /// </summary>
    public void AbortWithThesisInvalidation(InvalidationTrigger trigger, string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            throw new ArgumentException("Detail không được rỗng khi abort thesis", nameof(detail));
        if (detail.Length < 20)
            throw new ArgumentException(
                "Detail phải ≥ 20 ký tự để đảm bảo falsifiable (§D4 plan Vin-discipline)",
                nameof(detail));

        if (Status != TradePlanStatus.Ready
            && Status != TradePlanStatus.InProgress
            && Status != TradePlanStatus.Executed)
        {
            throw new InvalidOperationException(
                $"Không thể abort plan ở trạng thái {Status}. Chỉ áp dụng cho Ready/InProgress/Executed.");
        }

        InvalidationCriteria ??= new List<InvalidationRule>();
        InvalidationCriteria.Add(new InvalidationRule
        {
            Trigger = trigger,
            Detail = detail,
            IsTriggered = true,
            TriggeredAt = DateTime.UtcNow
        });

        // Ready: không có trade → set Cancelled luôn.
        // InProgress: set Cancelled (service layer chịu trách nhiệm exit trades nếu cần).
        // Executed: giữ nguyên status — position đã mở, exit flow do application/service layer lo.
        if (Status == TradePlanStatus.Ready || Status == TradePlanStatus.InProgress)
        {
            Status = TradePlanStatus.Cancelled;
        }

        AddDomainEvent(new TradePlanThesisInvalidatedEvent(
            Id, UserId, trigger, detail,
            TradeIds?.ToList() ?? (TradeId != null ? new List<string> { TradeId } : new List<string>())));

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    // --- Multi-lot methods ---

    public void SetLots(EntryMode mode, List<PlanLot> lots)
    {
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot modify lots on executed/reviewed plan");
        EntryMode = mode;
        Lots = lots;
        Quantity = lots.Sum(l => l.PlannedQuantity);
        if (Status == TradePlanStatus.Ready) { RefreshInitialRisk(); RefreshEstimate(); }
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void ExecuteLot(int lotNumber, string tradeId, decimal actualPrice)
    {
        if (Lots == null || Lots.Count == 0)
            throw new InvalidOperationException("Plan has no lots");
        var lot = Lots.FirstOrDefault(l => l.LotNumber == lotNumber)
            ?? throw new ArgumentException($"Lot {lotNumber} not found");
        if (lot.Status != PlanLotStatus.Pending)
            throw new InvalidOperationException($"Lot {lotNumber} is not pending");
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed || Status == TradePlanStatus.Cancelled)
            throw new InvalidOperationException($"Cannot execute lot on a {Status} plan");

        var statusBeforeFill = Status;   // chụp TRƯỚC khi chuyển trạng thái: chỉ lần khớp đầu (Draft/Ready) mới được chụp 1R

        lot.Status = PlanLotStatus.Executed;
        lot.ActualPrice = actualPrice;
        lot.ExecutedAt = DateTime.UtcNow;
        lot.TradeId = tradeId;

        TradeIds ??= new List<string>();
        TradeIds.Add(tradeId);

        // Auto-transition status: Draft/Ready → InProgress; all-lots-executed → Executed
        if (Status == TradePlanStatus.Ready || Status == TradePlanStatus.Draft)
            Status = TradePlanStatus.InProgress;

        // Lần khớp đầu (trước đó là Draft/Ready): chụp (nếu đi thẳng từ Draft) rồi khoá 1R, theo khối lượng DỰ TÍNH.
        // Đã InProgress thì KHÔNG chụp: ảnh chụp null (kế hoạch cũ hoặc ảnh đầu không hợp lệ) mà chụp bây giờ sẽ lấy SL đã dời
        // và gắn cờ chốt-kế-hoạch, 1R sai nhưng trông như thật. Để null thì khi đọc dùng ước tính ("uoc-tinh").
        if (statusBeforeFill == TradePlanStatus.Draft || statusBeforeFill == TradePlanStatus.Ready)
        {
            RefreshInitialRisk();
            LockInitialRisk();
            LockEstimateAndRecordGate();
        }

        if (Lots.All(l => l.Status != PlanLotStatus.Pending))
        {
            Status = TradePlanStatus.Executed;
            ExecutedAt = DateTime.UtcNow;
        }

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    // --- Exit management methods ---

    public void SetExitTargets(List<ExitTarget> targets)
    {
        ExitTargets = targets;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void TriggerExitTarget(int level, string tradeId)
    {
        var target = ExitTargets?.FirstOrDefault(t => t.Level == level)
            ?? throw new ArgumentException($"Exit target level {level} not found");
        target.IsTriggered = true;
        target.TriggeredAt = DateTime.UtcNow;
        target.TradeId = tradeId;

        TradeIds ??= new List<string>();
        TradeIds.Add(tradeId);

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateStopLossWithHistory(decimal newStopLoss, string? reason = null)
    {
        if (Status == TradePlanStatus.Reviewed || Status == TradePlanStatus.Cancelled)
            throw new InvalidOperationException("Không dời được stop-loss của kế hoạch đã đóng");

        if (newStopLoss <= 0)
            throw new ArgumentException("Giá stop-loss phải lớn hơn 0", nameof(newStopLoss));

        // Nới = dời ngưỡng ra xa giá vào hơn (Buy: xuống, Sell: lên). Guard nằm ở entity vì
        // REST, MCP và mọi caller sau này đều đi qua đây — để riêng ở modal là để trống cửa.
        // StopLoss = 0 nghĩa là chưa đặt ngưỡng, lần đặt đầu không tính là nới.
        var widening = StopLoss > 0 && (IsSellDirection()
            ? newStopLoss > StopLoss
            : newStopLoss < StopLoss);
        if (widening && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Nới stop-loss bắt buộc ghi lý do");

        StopLossHistory ??= new List<StopLossHistoryEntry>();
        StopLossHistory.Add(new StopLossHistoryEntry
        {
            OldPrice = StopLoss,
            NewPrice = newStopLoss,
            Reason = reason,
            ChangedAt = DateTime.UtcNow
        });
        StopLoss = newStopLoss;
        PricesSetAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private bool IsSellDirection()
        => string.Equals(Direction, "Sell", StringComparison.OrdinalIgnoreCase);

    // --- Scenario Playbook methods ---

    public void SetExitStrategyMode(ExitStrategyMode mode)
    {
        if (Status == TradePlanStatus.Executed || Status == TradePlanStatus.Reviewed)
            throw new InvalidOperationException("Cannot change exit strategy mode on executed/reviewed plan");
        ExitStrategyMode = mode;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void SetScenarioNodes(List<ScenarioNode> nodes)
    {
        if (ExitStrategyMode != ExitStrategyMode.Advanced)
            throw new InvalidOperationException("Cannot set scenario nodes in Simple mode");
        if (!nodes.Any(n => n.ParentId == null))
            throw new ArgumentException("Scenario tree must have at least one root node");
        var nodeIds = nodes.Select(n => n.NodeId).ToHashSet();
        if (nodes.Any(n => n.ParentId != null && !nodeIds.Contains(n.ParentId)))
            throw new ArgumentException("ScenarioNode references a non-existent parent");
        // Chỉ dời mốc của riêng cây kịch bản (giá nhập của kế hoạch không hề được đặt lại),
        // và chỉ khi ngưỡng thực sự đổi — form gửi lại toàn bộ node mỗi lần lưu.
        var thresholdsChanged = PriceSignature(nodes) != PriceSignature(ScenarioNodes);

        ScenarioNodes = nodes;
        if (thresholdsChanged) ScenarioPricesSetAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>Chữ ký của mọi con số mang giá trong cây kịch bản.</summary>
    private static string PriceSignature(IEnumerable<ScenarioNode>? nodes)
        => nodes == null
            ? string.Empty
            : string.Join("|", nodes.Select(n =>
                $"{n.NodeId}:{n.ConditionValue}:{n.ActionValue}:{n.TrailingStopConfig?.ActivationPrice}" +
                $":{n.TrailingStopConfig?.TrailValue}:{n.TrailingStopConfig?.StepSize}"));

    public void TriggerScenarioNode(string nodeId, string? tradeId = null)
    {
        var node = ScenarioNodes?.FirstOrDefault(n => n.NodeId == nodeId)
            ?? throw new ArgumentException($"Scenario node {nodeId} not found");
        if (node.Status != ScenarioNodeStatus.Pending)
            throw new InvalidOperationException($"Scenario node {nodeId} is not pending");
        if (node.ParentId != null)
        {
            var parent = ScenarioNodes!.First(n => n.NodeId == node.ParentId);
            if (parent.Status != ScenarioNodeStatus.Triggered)
                throw new InvalidOperationException("Parent scenario must be triggered first");
        }
        node.Status = ScenarioNodeStatus.Triggered;
        node.TriggeredAt = DateTime.UtcNow;
        node.TradeId = tradeId;
        if (tradeId != null)
        {
            TradeIds ??= new List<string>();
            TradeIds.Add(tradeId);
        }
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
        AddDomainEvent(new ScenarioNodeTriggeredEvent(Id, nodeId, node.ActionType.ToString(), UserId));
    }
}

public class ChecklistItem
{
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Checked { get; set; }
    public bool Critical { get; set; }
    public string Hint { get; set; } = string.Empty;
}

public enum TradePlanStatus
{
    Draft,
    Ready,
    InProgress,
    Executed,
    Reviewed,
    Cancelled
}

public enum EntryMode
{
    Single,
    ScalingIn,
    DCA
}

public enum PlanLotStatus
{
    Pending,
    Executed,
    Cancelled
}

public class PlanLot
{
    public int LotNumber { get; set; }
    public decimal PlannedPrice { get; set; }
    public int PlannedQuantity { get; set; }
    public decimal? AllocationPercent { get; set; }
    public string? Label { get; set; }
    public PlanLotStatus Status { get; set; } = PlanLotStatus.Pending;
    public decimal? ActualPrice { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string? TradeId { get; set; }
}

public enum ExitActionType
{
    TakeProfit,
    CutLoss,
    TrailingStop,
    PartialExit
}

public class ExitTarget
{
    public int Level { get; set; }
    public ExitActionType ActionType { get; set; }
    public decimal Price { get; set; }
    public int? Quantity { get; set; }
    public decimal? PercentOfPosition { get; set; }
    public string? Label { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public string? TradeId { get; set; }
}

/// <summary>Một kịch bản kết quả của CẢ vị thế: xác suất (%) và giá thoát bình quân. Lưu nhúng trong TradePlan.</summary>
public class OutcomeScenario
{
    public string Label { get; set; } = string.Empty;
    public decimal ProbabilityPercent { get; set; }
    public decimal ExitPrice { get; set; }

    public OutcomeScenario Clone() => new() { Label = Label, ProbabilityPercent = ProbabilityPercent, ExitPrice = ExitPrice };
}

/// <summary>Ảnh chụp ước lượng lúc chốt kế hoạch (khoá ở lần khớp đầu).</summary>
public class EstimateSnapshot
{
    public List<OutcomeScenario> Scenarios { get; set; } = new();
    /// <summary>EV theo R ròng; null nếu tổng xác suất ≠ 100% hoặc thiếu 1R.</summary>
    public decimal? EvR { get; set; }
    /// <summary>Tổng xác suất (%) các kịch bản có R ròng &gt; +0,1.</summary>
    public decimal? ImpliedWinProbability { get; set; }
    /// <summary>False nếu chụp mà không có phí (đường ghi lại sau sự kiện): EV chưa trừ phí.</summary>
    public bool FeesIncluded { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LockedAt { get; set; }
}

/// <summary>Chuyển sang Sẵn sàng khi kỳ vọng ≤ 0 hoặc chưa ước lượng mà không có lý do. Map sang 400 EV_GATE_REASON_REQUIRED.</summary>
public class EvGateException : InvalidOperationException
{
    public EvGateException(int minReasonLength)
        : base($"Kỳ vọng âm hoặc chưa ước lượng, chưa thể chuyển sang Sẵn sàng. Nhập kịch bản có tổng xác suất 100% và EV dương, hoặc ghi lý do (evGateReason, tối thiểu {minReasonLength} ký tự).")
    {
    }
}

/// <summary>Ảnh chụp rủi ro ban đầu (1R) của kế hoạch. Lưu nhúng trong TradePlan.</summary>
public class InitialRiskSnapshot
{
    public decimal Entry { get; set; }
    public decimal StopLoss { get; set; }
    /// <summary>Tổng khối lượng dự tính (tổng các lot nếu có).</summary>
    public decimal Quantity { get; set; }
    /// <summary>|Entry − SL| × Quantity: số tiền rủi ro ban đầu = 1R.</summary>
    public decimal RiskAmount { get; set; }
    /// <summary><see cref="RMath.RiskBasisPlanned"/> | <see cref="RMath.RiskBasisEstimated"/> | <see cref="RMath.RiskBasisPartialFill"/>.</summary>
    public string RiskBasis { get; set; } = RMath.RiskBasisPlanned;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Có giá trị từ lần khớp đầu tiên: từ đó trở đi không chụp lại.</summary>
    public DateTime? LockedAt { get; set; }
}

public class StopLossHistoryEntry
{
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

// --- Time Horizon ---

public enum TimeHorizon
{
    ShortTerm,    // < 3 months
    MediumTerm,   // 3-12 months
    LongTerm      // > 1 year
}

// --- Campaign Review ---

public class CampaignReviewData
{
    public decimal PnLAmount { get; set; }
    public decimal PnLPercent { get; set; }
    public int HoldingDays { get; set; }
    public decimal PnLPerDay { get; set; }
    public decimal AnnualizedReturnPercent { get; set; }
    public decimal TargetAchievementPercent { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalReturned { get; set; }
    public decimal TotalFees { get; set; }
    public string? LessonsLearned { get; set; }
    public DateTime ReviewedAt { get; set; }

    /// <summary>PnL ròng / 1R ban đầu. Null nếu không tính được. Review cũ không có giá trị này: tính khi đọc, không ghi lại.</summary>
    public decimal? RealizedR { get; set; }
    /// <summary>"chot-ke-hoach" | "uoc-tinh" | "khop-mot-phan" (xem <see cref="InvestmentApp.Domain.Services.RMath"/>).</summary>
    public string? RiskBasis { get; set; }

    /// <summary>
    /// Số cổ phiếu CÒN GIỮ lúc review (đã điều chỉnh theo sự kiện quyền). Null = review cũ, chưa tính theo phần đã chốt.
    /// Lớn hơn 0 nghĩa là PnL/R chỉ tính phần đã bán; review loại khỏi kỳ vọng R, độ khớp ước lượng và Kelly.
    /// </summary>
    public decimal? OpenQuantity { get; set; }

    /// <summary>Cổ tức tiền mặt ròng theo quyền hưởng (số lượng của riêng kế hoạch tại ngày GDKHQ × ròng/CP), đã nằm trong <see cref="PnLAmount"/>.</summary>
    public decimal? DividendIncome { get; set; }

    /// <summary>Giá vốn (gồm phí, thuế mua) của phần ĐÃ BÁN; mẫu số của <see cref="PnLPercent"/>.</summary>
    public decimal? RealizedCost { get; set; }
}

// --- Scenario Playbook types ---

public enum ExitStrategyMode
{
    Simple,
    Advanced
}

public enum ScenarioConditionType
{
    PriceAbove,
    PriceBelow,
    PricePercentChange,
    TrailingStopHit,
    TimeElapsed
}

public enum ScenarioActionType
{
    SellPercent,
    SellAll,
    MoveStopLoss,
    MoveStopToBreakeven,
    ActivateTrailingStop,
    AddPosition,
    SendNotification
}

public enum ScenarioNodeStatus
{
    Pending,
    Triggered,
    Skipped,
    Expired
}

public enum TrailingStopMethod
{
    Percentage,
    ATR,
    FixedAmount
}

public class TrailingStopConfig
{
    public TrailingStopMethod Method { get; set; } = TrailingStopMethod.Percentage;
    public decimal TrailValue { get; set; }
    public decimal? ActivationPrice { get; set; }
    public decimal? StepSize { get; set; }
    public decimal? CurrentTrailingStop { get; set; }
    public decimal? HighestPrice { get; set; }

    /// <summary>
    /// Mặt bằng giá của <see cref="HighestPrice"/> / <see cref="CurrentTrailingStop"/>.
    /// Hai giá trị này được ghi nhận từ thị trường rồi ghi đè trở lại, nên phải quy về
    /// mặt bằng mới đúng một lần sau sự kiện quyền — khác các ngưỡng do người dùng đặt,
    /// vốn điều chỉnh tại thời điểm đọc. Null → lùi về mốc giá của kế hoạch.
    /// </summary>
    public DateTime? PriceBasisAt { get; set; }
}

public class ScenarioNode
{
    public string NodeId { get; set; } = Guid.NewGuid().ToString();
    public string? ParentId { get; set; }
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;

    // Condition
    public ScenarioConditionType ConditionType { get; set; }
    public decimal? ConditionValue { get; set; }
    public string? ConditionNote { get; set; }

    // Action
    public ScenarioActionType ActionType { get; set; }
    public decimal? ActionValue { get; set; }
    public TrailingStopConfig? TrailingStopConfig { get; set; }

    // Status
    public ScenarioNodeStatus Status { get; set; } = ScenarioNodeStatus.Pending;
    public DateTime? TriggeredAt { get; set; }
    public string? TradeId { get; set; }
}
