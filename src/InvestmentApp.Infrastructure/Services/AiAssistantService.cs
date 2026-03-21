using System.Runtime.CompilerServices;
using System.Text;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Portfolios.Queries;
using InvestmentApp.Application.Services;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

public class AiAssistantService : IAiAssistantService
{
    private readonly IAiSettingsRepository _settingsRepo;
    private readonly IAiKeyEncryptionService _encryption;
    private readonly IAiChatServiceFactory _chatServiceFactory;
    private readonly ITradeJournalRepository _journalRepo;
    private readonly ITradeRepository _tradeRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IPnLService _pnlService;
    private readonly ITradePlanRepository _tradePlanRepo;
    private readonly IFundamentalDataProvider _fundamentalProvider;
    private readonly IStockInfoProvider _stockInfoProvider;
    private readonly ITechnicalIndicatorService _technicalService;
    private readonly IRiskCalculationService _riskService;
    private readonly IRiskProfileRepository _riskProfileRepo;
    private readonly IWatchlistRepository _watchlistRepo;

    private const string BasePrompt = @"Bạn là trợ lý AI tích hợp trong Investment Mate — ứng dụng quản lý danh mục đầu tư chứng khoán Việt Nam.
Quy tắc:
- Trả lời bằng tiếng Việt có dấu đầy đủ
- Sử dụng markdown formatting
- Ngắn gọn, đi thẳng vào vấn đề
- Đưa ra nhận xét khách quan, dựa trên dữ liệu
- Khi gợi ý, luôn kèm lý do cụ thể";

    public AiAssistantService(
        IAiSettingsRepository settingsRepo,
        IAiKeyEncryptionService encryption,
        IAiChatServiceFactory chatServiceFactory,
        ITradeJournalRepository journalRepo,
        ITradeRepository tradeRepo,
        IPortfolioRepository portfolioRepo,
        IPnLService pnlService,
        ITradePlanRepository tradePlanRepo,
        IFundamentalDataProvider fundamentalProvider,
        IStockInfoProvider stockInfoProvider,
        ITechnicalIndicatorService technicalService,
        IRiskCalculationService riskService,
        IRiskProfileRepository riskProfileRepo,
        IWatchlistRepository watchlistRepo)
    {
        _settingsRepo = settingsRepo;
        _encryption = encryption;
        _chatServiceFactory = chatServiceFactory;
        _journalRepo = journalRepo;
        _tradeRepo = tradeRepo;
        _portfolioRepo = portfolioRepo;
        _pnlService = pnlService;
        _tradePlanRepo = tradePlanRepo;
        _fundamentalProvider = fundamentalProvider;
        _stockInfoProvider = stockInfoProvider;
        _technicalService = technicalService;
        _riskService = riskService;
        _riskProfileRepo = riskProfileRepo;
        _watchlistRepo = watchlistRepo;
    }

    // =============================================
    // Streaming methods (API integration)
    // =============================================

    public async IAsyncEnumerable<AiStreamChunk> ReviewJournalAsync(
        string userId, string? portfolioId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildJournalReviewContext(userId, portfolioId, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> ReviewPortfolioAsync(
        string userId, string portfolioId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildPortfolioReviewContext(userId, portfolioId, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> AdviseTradePlanAsync(
        string userId, string tradePlanId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildTradePlanAdvisorContext(userId, tradePlanId, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> ChatAsync(
        string userId, string message, List<AiChatMessage>? history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildChatContext(userId, message, history, ct);

        var messages = history?.ToList() ?? new List<AiChatMessage>();
        messages.Add(new AiChatMessage { Role = "user", Content = message });
        if (messages.Count > 20)
            messages = messages.Skip(messages.Count - 20).ToList();

        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> MonthlySummaryAsync(
        string userId, string portfolioId, int year, int month,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildMonthlySummaryContext(userId, portfolioId, year, month, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> EvaluateStockAsync(
        string userId, string symbol, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildStockEvaluationContext(userId, symbol, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> AssessRiskAsync(
        string userId, string portfolioId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildRiskAssessmentContext(userId, portfolioId, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> AdvisePositionsAsync(
        string userId, string? portfolioId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildPositionAdvisorContext(userId, portfolioId, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> AnalyzeTradesAsync(
        string userId, string? portfolioId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildTradeAnalysisContext(userId, portfolioId, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> ScanWatchlistAsync(
        string userId, string watchlistId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildWatchlistScannerContext(userId, watchlistId, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> DailyBriefingAsync(
        string userId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, provider, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null) { yield return NoApiKeyError(); yield break; }

        var context = await BuildDailyBriefingContext(userId, question, ct);
        if (context.ErrorMessage != null) { yield return new AiStreamChunk { Type = "error", ErrorMessage = context.ErrorMessage }; yield break; }

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = context.UserMessage } };
        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, provider, context.SystemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    // =============================================
    // BuildContextAsync — public, no API key needed
    // =============================================

    public async Task<AiContextResult> BuildContextAsync(string useCase, string userId,
        string? portfolioId, string? tradePlanId, string? symbol, string? question,
        int? year, int? month, string? message, List<AiChatMessage>? history,
        string? watchlistId = null,
        CancellationToken ct = default)
    {
        try
        {
            return useCase switch
            {
                "journal-review" => await BuildJournalReviewContext(userId, portfolioId, question, ct),
                "portfolio-review" when !string.IsNullOrEmpty(portfolioId) =>
                    await BuildPortfolioReviewContext(userId, portfolioId, question, ct),
                "trade-plan-advisor" when !string.IsNullOrEmpty(tradePlanId) =>
                    await BuildTradePlanAdvisorContext(userId, tradePlanId, question, ct),
                "chat" => await BuildChatContext(userId, message ?? "", history, ct),
                "monthly-summary" when !string.IsNullOrEmpty(portfolioId) =>
                    await BuildMonthlySummaryContext(userId, portfolioId, year ?? DateTime.UtcNow.Year, month ?? DateTime.UtcNow.Month, ct),
                "stock-evaluation" when !string.IsNullOrEmpty(symbol) =>
                    await BuildStockEvaluationContext(userId, symbol, question, ct),
                "risk-assessment" when !string.IsNullOrEmpty(portfolioId) =>
                    await BuildRiskAssessmentContext(userId, portfolioId, question, ct),
                "position-advisor" => await BuildPositionAdvisorContext(userId, portfolioId, question, ct),
                "trade-analysis" => await BuildTradeAnalysisContext(userId, portfolioId, question, ct),
                "watchlist-scanner" when !string.IsNullOrEmpty(watchlistId) =>
                    await BuildWatchlistScannerContext(userId, watchlistId, question, ct),
                "daily-briefing" => await BuildDailyBriefingContext(userId, question, ct),
                "portfolio-review" or "monthly-summary" or "risk-assessment" =>
                    new AiContextResult { ErrorMessage = "Thiếu portfolioId." },
                "trade-plan-advisor" =>
                    new AiContextResult { ErrorMessage = "Thiếu tradePlanId." },
                "stock-evaluation" =>
                    new AiContextResult { ErrorMessage = "Thiếu mã cổ phiếu (symbol)." },
                "watchlist-scanner" =>
                    new AiContextResult { ErrorMessage = "Thiếu watchlistId." },
                _ => new AiContextResult { ErrorMessage = $"Use case không hợp lệ: {useCase}" }
            };
        }
        catch (Exception ex)
        {
            return new AiContextResult { ErrorMessage = $"Lỗi khi tạo context: {ex.Message}" };
        }
    }

    // =============================================
    // Private context builders (XML-tagged)
    // =============================================

    private async Task<AiContextResult> BuildJournalReviewContext(
        string userId, string? portfolioId, string? question, CancellationToken ct)
    {
        IEnumerable<TradeJournal> journals;
        if (!string.IsNullOrEmpty(portfolioId))
            journals = await _journalRepo.GetByPortfolioIdAsync(portfolioId, ct);
        else
            journals = await _journalRepo.GetByUserIdAsync(userId, ct);

        var journalList = journals.OrderByDescending(j => j.CreatedAt).Take(20).ToList();
        if (journalList.Count == 0)
            return new AiContextResult { ErrorMessage = "Chưa có nhật ký giao dịch nào để phân tích." };

        var sb = new StringBuilder();

        // Journal statistics
        var avgConfidence = journalList.Average(j => j.ConfidenceLevel);
        var avgRating = journalList.Average(j => j.Rating);
        var emotionGroups = journalList.GroupBy(j => j.EmotionalState).OrderByDescending(g => g.Count()).ToList();

        sb.AppendLine("<journal_statistics>");
        sb.AppendLine($"  <count>{journalList.Count}</count>");
        sb.AppendLine($"  <avg_confidence>{avgConfidence:F1}/10</avg_confidence>");
        sb.AppendLine($"  <avg_rating>{avgRating:F1}/5</avg_rating>");
        sb.AppendLine($"  <emotions>");
        foreach (var g in emotionGroups)
            sb.AppendLine($"    {g.Key}: {g.Count()} lần ({g.Count() * 100 / journalList.Count}%)");
        sb.AppendLine($"  </emotions>");
        if (journalList.Any(j => j.Tags?.Count > 0))
        {
            var topTags = journalList.SelectMany(j => j.Tags ?? new()).GroupBy(t => t).OrderByDescending(g => g.Count()).Take(5);
            sb.AppendLine($"  <top_tags>{string.Join(", ", topTags.Select(g => $"{g.Key}({g.Count()})"))}</top_tags>");
        }
        sb.AppendLine("</journal_statistics>");

        sb.AppendLine();
        sb.AppendLine("<trade_journals>");
        sb.AppendLine("| Ngày | Lý do vào lệnh | Cảm xúc | Tự tin | Đánh giá | Bài học |");
        sb.AppendLine("|------|----------------|---------|--------|----------|--------|");
        foreach (var j in journalList)
        {
            var lessons = !string.IsNullOrEmpty(j.LessonsLearned) ? j.LessonsLearned.Replace("|", "/") : "—";
            var reason = (j.EntryReason ?? "—").Replace("|", "/");
            sb.AppendLine($"| {j.CreatedAt:dd/MM/yyyy} | {reason} | {j.EmotionalState} | {j.ConfidenceLevel}/10 | {j.Rating}/5 | {lessons} |");
        }
        sb.AppendLine("</trade_journals>");

        sb.AppendLine();
        sb.AppendLine("<journal_details>");
        foreach (var j in journalList.Take(10))
        {
            sb.AppendLine($"--- Ngày: {j.CreatedAt:dd/MM/yyyy} ---");
            if (!string.IsNullOrEmpty(j.MarketContext)) sb.AppendLine($"Bối cảnh thị trường: {j.MarketContext}");
            if (!string.IsNullOrEmpty(j.TechnicalSetup)) sb.AppendLine($"Setup kỹ thuật: {j.TechnicalSetup}");
            if (!string.IsNullOrEmpty(j.PostTradeReview)) sb.AppendLine($"Đánh giá sau GD: {j.PostTradeReview}");
            if (j.Tags?.Count > 0) sb.AppendLine($"Tags: {string.Join(", ", j.Tags)}");
        }
        sb.AppendLine("</journal_details>");

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Phân tích nhật ký giao dịch của nhà đầu tư.
1. **Tâm lý giao dịch**: Nhận diện cảm xúc (FOMO, tham lam, sợ hãi, revenge trading, tự tin thái quá)
2. **Kỷ luật**: Đánh giá mức độ tuân thủ kế hoạch giao dịch
3. **Điểm mạnh & yếu**: Quyết định tốt vs sai lầm lặp lại
4. **Tương quan cảm xúc - kết quả**: Khi tự tin cao (≥7) thì đánh giá có tốt không? Khi sợ hãi thì sao?
5. **Nhận diện trigger**: Tình huống thị trường nào thường dẫn đến quyết định tệ
6. **Tiến bộ theo thời gian**: 10 entry gần nhất vs 10 entry cũ hơn, có cải thiện không
7. **Gợi ý cải thiện**: 3 hành động cụ thể nhất để cải thiện tâm lý và kỷ luật";

        var userMessage = question ?? $"Phân tích {journalList.Count} nhật ký giao dịch gần nhất:\n\n{sb}";

        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildPortfolioReviewContext(
        string userId, string portfolioId, string? question, CancellationToken ct)
    {
        var portfolio = await _portfolioRepo.GetByIdAsync(portfolioId, ct);
        if (portfolio == null)
            return new AiContextResult { ErrorMessage = "Không tìm thấy danh mục." };

        var trades = await _tradeRepo.GetByPortfolioIdAsync(portfolioId, ct);
        var tradeList = trades.ToList();

        // Fetch PnL, risk metrics, risk profile, trade plans in parallel
        PortfolioPnLSummary? pnl = null;
        PortfolioRiskSummary? riskSummary = null;
        RiskProfile? riskProfile = null;
        List<TradePlan>? activePlans = null;

        var pnlTask = _pnlService.CalculatePortfolioPnLAsync(portfolioId, ct);
        var riskTask = _riskService.GetPortfolioRiskSummaryAsync(portfolioId, ct);
        var riskProfileTask = _riskProfileRepo.GetByPortfolioIdAsync(portfolioId, ct);
        var plansTask = _tradePlanRepo.GetActiveByUserIdAsync(userId, ct);

        await Task.WhenAll(
            pnlTask.ContinueWith(_ => { }),
            riskTask.ContinueWith(_ => { }),
            riskProfileTask.ContinueWith(_ => { }),
            plansTask.ContinueWith(_ => { }));

        if (pnlTask.IsCompletedSuccessfully) pnl = pnlTask.Result;
        if (riskTask.IsCompletedSuccessfully) riskSummary = riskTask.Result;
        if (riskProfileTask.IsCompletedSuccessfully) riskProfile = riskProfileTask.Result;
        if (plansTask.IsCompletedSuccessfully) activePlans = plansTask.Result?.Where(p => p.PortfolioId == portfolioId).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<portfolio>");
        sb.AppendLine($"  <name>{portfolio.Name}</name>");
        sb.AppendLine($"  <initial_capital>{portfolio.InitialCapital:N0} VND</initial_capital>");
        sb.AppendLine($"  <total_trades>{tradeList.Count}</total_trades>");
        if (pnl != null)
        {
            sb.AppendLine($"  <total_invested>{pnl.TotalInvested:N0} VND</total_invested>");
            sb.AppendLine($"  <market_value>{pnl.TotalMarketValue:N0} VND</market_value>");
            sb.AppendLine($"  <realized_pnl>{pnl.TotalRealizedPnL:+#,0;-#,0} VND</realized_pnl>");
            sb.AppendLine($"  <unrealized_pnl>{pnl.TotalUnrealizedPnL:+#,0;-#,0} VND</unrealized_pnl>");
            sb.AppendLine($"  <total_return>{pnl.TotalReturnPercentage:+0.0;-0.0}%</total_return>");
        }
        sb.AppendLine("</portfolio>");

        // Positions with PnL detail
        if (pnl?.Positions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<positions>");
            sb.AppendLine("| Mã | SL | Giá TB | Giá hiện tại | Giá trị | Lãi/Lỗ | % |");
            sb.AppendLine("|-----|-----|--------|-------------|---------|--------|-----|");
            foreach (var pos in pnl.Positions.OrderByDescending(p => Math.Abs(p.TotalPnL)))
            {
                sb.AppendLine($"| {pos.Symbol} | {pos.Quantity:N0} | {pos.AverageCost:N0} | {pos.CurrentPrice:N0} | {pos.MarketValue:N0} | {pos.TotalPnL:+#,0;-#,0} | {pos.TotalPnLPercent:+0.0;-0.0}% |");
            }
            sb.AppendLine("</positions>");
        }
        else
        {
            // Fallback: positions from trade data only
            var symbols = tradeList.GroupBy(t => t.Symbol).ToList();
            sb.AppendLine();
            sb.AppendLine("<positions>");
            sb.AppendLine("| Mã | Mua | Bán | Còn | Giá TB mua |");
            sb.AppendLine("|-----|-----|-----|-----|------------|");
            foreach (var group in symbols)
            {
                var buys = group.Where(t => t.TradeType == TradeType.BUY).Sum(t => t.Quantity);
                var sells = group.Where(t => t.TradeType == TradeType.SELL).Sum(t => t.Quantity);
                var net = buys - sells;
                var avgBuy = group.Where(t => t.TradeType == TradeType.BUY).Select(t => t.Price).DefaultIfEmpty(0).Average();
                sb.AppendLine($"| {group.Key} | {buys} | {sells} | {net} | {avgBuy:N0} VND |");
            }
            sb.AppendLine("</positions>");
        }

        // Recent trades
        var recentTrades = tradeList.OrderByDescending(t => t.CreatedAt).Take(10).ToList();
        if (recentTrades.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<recent_trades>");
            sb.AppendLine("| Ngày | Loại | Mã | SL | Giá | Phí |");
            sb.AppendLine("|------|------|-----|-----|------|-----|");
            foreach (var t in recentTrades)
                sb.AppendLine($"| {t.CreatedAt:dd/MM/yyyy} | {t.TradeType} | {t.Symbol} | {t.Quantity} | {t.Price:N0} | {t.Fee:N0} |");
            sb.AppendLine("</recent_trades>");
        }

        // Risk metrics
        if (riskSummary != null)
        {
            sb.AppendLine();
            sb.AppendLine("<risk_metrics>");
            sb.AppendLine($"  <value_at_risk_95>{riskSummary.ValueAtRisk95:N0} VND</value_at_risk_95>");
            sb.AppendLine($"  <max_drawdown>{riskSummary.MaxDrawdown:F1}%</max_drawdown>");
            sb.AppendLine($"  <largest_position>{riskSummary.LargestPositionPercent:F1}%</largest_position>");
            sb.AppendLine($"  <position_count>{riskSummary.PositionCount}</position_count>");
            sb.AppendLine("</risk_metrics>");
        }

        // Risk profile compliance
        if (riskProfile != null)
        {
            sb.AppendLine();
            sb.AppendLine("<risk_profile>");
            sb.AppendLine($"  <max_position_size>{riskProfile.MaxPositionSizePercent}%</max_position_size>");
            sb.AppendLine($"  <max_sector_exposure>{riskProfile.MaxSectorExposurePercent}%</max_sector_exposure>");
            sb.AppendLine($"  <max_drawdown_alert>{riskProfile.MaxDrawdownAlertPercent}%</max_drawdown_alert>");
            sb.AppendLine($"  <min_risk_reward>{riskProfile.DefaultRiskRewardRatio}</min_risk_reward>");
            // Check violations
            if (riskSummary != null)
            {
                if (riskSummary.LargestPositionPercent > riskProfile.MaxPositionSizePercent)
                    sb.AppendLine($"  <violation>Vị thế lớn nhất ({riskSummary.LargestPositionPercent:F1}%) vượt giới hạn ({riskProfile.MaxPositionSizePercent}%)</violation>");
                if (riskSummary.MaxDrawdown > riskProfile.MaxDrawdownAlertPercent)
                    sb.AppendLine($"  <violation>Drawdown ({riskSummary.MaxDrawdown:F1}%) vượt ngưỡng cảnh báo ({riskProfile.MaxDrawdownAlertPercent}%)</violation>");
            }
            sb.AppendLine("</risk_profile>");
        }

        // Active trade plans
        if (activePlans?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"<active_plans count=\"{activePlans.Count}\">");
            foreach (var p in activePlans.Take(5))
                sb.AppendLine($"  <plan symbol=\"{p.Symbol}\" direction=\"{p.Direction}\" status=\"{p.Status}\" confidence=\"{p.ConfidenceLevel}/10\" />");
            sb.AppendLine("</active_plans>");
        }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Đánh giá danh mục đầu tư toàn diện.
1. **Tổng quan hiệu suất**: Tổng P&L, return %, so sánh với vốn ban đầu
2. **Phân tích vị thế**: Mã nào đang lãi/lỗ nhiều nhất, tỷ trọng từng mã
3. **Đa dạng hóa**: Cảnh báo nếu 1 mã chiếm > 30% danh mục, tập trung ngành
4. **Rủi ro**: Vị thế đang lỗ nặng, drawdown, gợi ý cắt lỗ nếu cần
5. **Tuân thủ risk profile**: So sánh metrics hiện tại vs giới hạn đã đặt, liệt kê vi phạm
6. **Kế hoạch giao dịch**: Đánh giá các trade plan đang hoạt động, có phù hợp danh mục
7. **Kế hoạch hành động**: 3 việc cụ thể nhất cần làm ngay (cắt lỗ, chốt lời, cân bằng lại)";

        var userMessage = question ?? $"Đánh giá danh mục đầu tư:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildTradePlanAdvisorContext(
        string userId, string tradePlanId, string? question, CancellationToken ct)
    {
        var plan = await _tradePlanRepo.GetByIdAsync(tradePlanId, ct);
        if (plan == null)
            return new AiContextResult { ErrorMessage = "Không tìm thấy kế hoạch giao dịch." };

        // Fetch market data, technical signals, risk profile, historical trades in parallel
        var stockDetailTask = _stockInfoProvider.GetStockDetailAsync(plan.Symbol, ct);
        var technicalTask = _technicalService.AnalyzeAsync(plan.Symbol, ct);
        var riskProfileTask = !string.IsNullOrEmpty(plan.PortfolioId)
            ? _riskProfileRepo.GetByPortfolioIdAsync(plan.PortfolioId, ct)
            : Task.FromResult<RiskProfile?>(null);
        var historyTask = !string.IsNullOrEmpty(plan.PortfolioId)
            ? _tradeRepo.GetByPortfolioIdAndSymbolAsync(plan.PortfolioId, plan.Symbol, ct)
            : Task.FromResult<IEnumerable<Trade>>(Enumerable.Empty<Trade>());

        await Task.WhenAll(
            stockDetailTask.ContinueWith(_ => { }),
            technicalTask.ContinueWith(_ => { }),
            riskProfileTask.ContinueWith(_ => { }),
            historyTask.ContinueWith(_ => { }));

        var stockDetail = stockDetailTask.IsCompletedSuccessfully ? stockDetailTask.Result : null;
        var technical = technicalTask.IsCompletedSuccessfully ? technicalTask.Result : null;
        var riskProfile = riskProfileTask.IsCompletedSuccessfully ? riskProfileTask.Result : null;
        var historicalTrades = historyTask.IsCompletedSuccessfully ? historyTask.Result?.ToList() : null;

        var sb = new StringBuilder();
        sb.AppendLine("<trade_plan>");
        sb.AppendLine($"  <symbol>{plan.Symbol}</symbol>");
        sb.AppendLine($"  <direction>{plan.Direction}</direction>");
        sb.AppendLine($"  <entry_price>{plan.EntryPrice:N0} VND</entry_price>");
        sb.AppendLine($"  <stop_loss>{plan.StopLoss:N0} VND</stop_loss>");
        sb.AppendLine($"  <take_profit>{plan.Target:N0} VND</take_profit>");
        sb.AppendLine($"  <quantity>{plan.Quantity}</quantity>");
        sb.AppendLine($"  <entry_mode>{plan.EntryMode}</entry_mode>");
        sb.AppendLine($"  <status>{plan.Status}</status>");
        sb.AppendLine($"  <confidence>{plan.ConfidenceLevel}/10</confidence>");
        if (!string.IsNullOrEmpty(plan.Reason))
            sb.AppendLine($"  <reason>{plan.Reason}</reason>");
        if (!string.IsNullOrEmpty(plan.MarketCondition))
            sb.AppendLine($"  <market_condition>{plan.MarketCondition}</market_condition>");
        if (plan.RiskPercent.HasValue)
            sb.AppendLine($"  <risk_percent>{plan.RiskPercent:F1}%</risk_percent>");
        if (plan.AccountBalance.HasValue)
            sb.AppendLine($"  <account_balance>{plan.AccountBalance:N0} VND</account_balance>");

        if (plan.EntryPrice > 0 && plan.StopLoss > 0 && plan.Target > 0)
        {
            var risk = Math.Abs(plan.EntryPrice - plan.StopLoss);
            var reward = Math.Abs(plan.Target - plan.EntryPrice);
            var rr = risk > 0 ? reward / risk : 0;
            sb.AppendLine($"  <risk_reward>1:{rr:F1}</risk_reward>");
            if (plan.Quantity > 0)
                sb.AppendLine($"  <risk_amount>{risk * plan.Quantity:N0} VND</risk_amount>");
        }
        sb.AppendLine("</trade_plan>");

        if (plan.ExitTargets?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<exit_targets>");
            sb.AppendLine("| Hành động | Giá | % vị thế |");
            sb.AppendLine("|-----------|-----|----------|");
            foreach (var t in plan.ExitTargets)
                sb.AppendLine($"| {t.ActionType} | {t.Price:N0} VND | {t.PercentOfPosition}% |");
            sb.AppendLine("</exit_targets>");
        }

        // Current market data
        if (stockDetail != null)
        {
            sb.AppendLine();
            sb.AppendLine("<current_market_data>");
            sb.AppendLine($"  <price>{stockDetail.Price:N0} VND</price>");
            sb.AppendLine($"  <change>{stockDetail.ChangePercent:+0.00;-0.00}%</change>");
            sb.AppendLine($"  <volume>{stockDetail.Volume:N0}</volume>");
            if (plan.EntryPrice > 0)
            {
                var distEntry = (stockDetail.Price - plan.EntryPrice) / plan.EntryPrice * 100;
                sb.AppendLine($"  <distance_to_entry>{distEntry:+0.0;-0.0}%</distance_to_entry>");
            }
            sb.AppendLine("</current_market_data>");
        }

        // Technical signals
        if (technical != null && technical.DataPoints >= 20)
        {
            sb.AppendLine();
            sb.AppendLine("<technical_context>");
            sb.AppendLine("| Chỉ báo | Giá trị | Tín hiệu |");
            sb.AppendLine("|---------|---------|-----------|");
            if (technical.Ema20.HasValue)
                sb.AppendLine($"| EMA20 | {technical.Ema20:N0} | {technical.EmaTrend} |");
            if (technical.Ema50.HasValue)
                sb.AppendLine($"| EMA50 | {technical.Ema50:N0} | — |");
            if (technical.Rsi14.HasValue)
                sb.AppendLine($"| RSI(14) | {technical.Rsi14:F1} | {technical.RsiSignal} |");
            sb.AppendLine($"| MACD | {technical.MacdLine:F2} | {technical.MacdSignal} |");
            sb.AppendLine($"| Tổng hợp | {technical.OverallSignalVi} | {technical.BullishCount}↑ {technical.BearishCount}↓ |");
            if (technical.SupportLevels.Count > 0)
                sb.AppendLine($"  Hỗ trợ: {string.Join(" | ", technical.SupportLevels.Select(s => $"{s:N0}"))}");
            if (technical.ResistanceLevels.Count > 0)
                sb.AppendLine($"  Kháng cự: {string.Join(" | ", technical.ResistanceLevels.Select(r => $"{r:N0}"))}");
            sb.AppendLine("</technical_context>");
        }

        // Risk profile compliance
        if (riskProfile != null)
        {
            sb.AppendLine();
            sb.AppendLine("<risk_compliance>");
            sb.AppendLine($"  <max_position_size>{riskProfile.MaxPositionSizePercent}%</max_position_size>");
            sb.AppendLine($"  <min_risk_reward>{riskProfile.DefaultRiskRewardRatio}</min_risk_reward>");
            sb.AppendLine($"  <max_portfolio_risk>{riskProfile.MaxPortfolioRiskPercent}%</max_portfolio_risk>");
            if (plan.EntryPrice > 0 && plan.StopLoss > 0 && plan.Target > 0)
            {
                var risk = Math.Abs(plan.EntryPrice - plan.StopLoss);
                var reward = Math.Abs(plan.Target - plan.EntryPrice);
                var rr = risk > 0 ? reward / risk : 0;
                var rrCompliant = rr >= riskProfile.DefaultRiskRewardRatio;
                sb.AppendLine($"  <rr_status>{(rrCompliant ? "✅ Đạt" : "⚠️ Chưa đạt")} (plan: 1:{rr:F1}, yêu cầu: 1:{riskProfile.DefaultRiskRewardRatio:F1})</rr_status>");
            }
            sb.AppendLine("</risk_compliance>");
        }

        // Historical trades on this symbol
        if (historicalTrades?.Count > 0)
        {
            var buys = historicalTrades.Where(t => t.TradeType == TradeType.BUY).ToList();
            var sells = historicalTrades.Where(t => t.TradeType == TradeType.SELL).ToList();
            sb.AppendLine();
            sb.AppendLine("<historical_trades>");
            sb.AppendLine($"  <total_trades>{historicalTrades.Count}</total_trades>");
            sb.AppendLine($"  <buy_count>{buys.Count}</buy_count>");
            sb.AppendLine($"  <sell_count>{sells.Count}</sell_count>");
            if (buys.Count > 0)
                sb.AppendLine($"  <avg_buy_price>{buys.Average(t => t.Price):N0} VND</avg_buy_price>");
            if (sells.Count > 0)
                sb.AppendLine($"  <avg_sell_price>{sells.Average(t => t.Price):N0} VND</avg_sell_price>");
            sb.AppendLine("</historical_trades>");
        }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Tư vấn kế hoạch giao dịch toàn diện.
1. **Entry**: Điểm vào có hợp lý không? So sánh với giá hiện tại, hỗ trợ/kháng cự, tín hiệu kỹ thuật
2. **Stop-loss**: SL quá gần/xa? Risk per trade có hợp lý?
3. **Take-profit**: TP realistic? Risk:Reward ratio đạt yêu cầu?
4. **Position sizing**: Kích thước vị thế có phù hợp với risk profile?
5. **Phân tích kỹ thuật**: Tín hiệu kỹ thuật hiện tại ủng hộ hay phản đối kế hoạch
6. **Tuân thủ risk profile**: Position size, R:R vs giới hạn đã đặt
7. **Lịch sử mã này**: Dựa trên giao dịch trước đó, nhà đầu tư trade mã này hiệu quả không
8. **Chấm điểm** (1-10) và gợi ý điều chỉnh cụ thể";

        var userMessage = question ?? $"Đánh giá kế hoạch giao dịch:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildChatContext(
        string userId, string message, List<AiChatMessage>? history, CancellationToken ct)
    {
        var portfoliosTask = _portfolioRepo.GetByUserIdAsync(userId, ct);
        var watchlistsTask = _watchlistRepo.GetByUserIdAsync(userId, ct);
        await Task.WhenAll(
            portfoliosTask.ContinueWith(_ => { }),
            watchlistsTask.ContinueWith(_ => { }));

        var portfolioList = portfoliosTask.IsCompletedSuccessfully ? portfoliosTask.Result?.ToList() : null;
        var watchlists = watchlistsTask.IsCompletedSuccessfully ? watchlistsTask.Result?.ToList() : null;

        var sb = new StringBuilder();
        sb.AppendLine($"<context date=\"{DateTime.UtcNow:dd/MM/yyyy}\">");

        if (portfolioList?.Count > 0)
        {
            sb.AppendLine("  <portfolios>");
            foreach (var p in portfolioList)
                sb.AppendLine($"    <portfolio name=\"{p.Name}\" capital=\"{p.InitialCapital:N0} VND\" />");
            sb.AppendLine("  </portfolios>");

            // Try to get top positions from first portfolio
            try
            {
                var pnl = await _pnlService.CalculatePortfolioPnLAsync(portfolioList[0].Id, ct);
                if (pnl?.Positions.Count > 0)
                {
                    sb.AppendLine("  <active_positions>");
                    foreach (var pos in pnl.Positions.OrderByDescending(p => p.MarketValue).Take(5))
                        sb.AppendLine($"    <position symbol=\"{pos.Symbol}\" qty=\"{pos.Quantity:N0}\" pnl=\"{pos.TotalPnLPercent:+0.0;-0.0}%\" value=\"{pos.MarketValue:N0}\" />");
                    sb.AppendLine("  </active_positions>");
                }
            }
            catch { /* Silently skip if PnL unavailable */ }
        }

        if (watchlists?.Count > 0)
        {
            var allSymbols = watchlists.SelectMany(w => w.Items.Select(i => i.Symbol)).Distinct().Take(15);
            sb.AppendLine($"  <watchlist_symbols>{string.Join(", ", allSymbols)}</watchlist_symbols>");
        }

        sb.AppendLine("</context>");

        var systemPrompt = BasePrompt + @"

Bạn có thể trả lời về: chiến lược đầu tư, phân tích kỹ thuật, quản lý rủi ro, cách sử dụng app Investment Mate, thị trường chứng khoán Việt Nam.
Dưới đây là context về danh mục và vị thế hiện tại của nhà đầu tư để bạn có thể đưa ra tư vấn phù hợp.
" + sb;

        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = message };
    }

    private async Task<AiContextResult> BuildMonthlySummaryContext(
        string userId, string portfolioId, int year, int month, CancellationToken ct)
    {
        var portfolio = await _portfolioRepo.GetByIdAsync(portfolioId, ct);
        if (portfolio == null)
            return new AiContextResult { ErrorMessage = "Không tìm thấy danh mục." };

        var trades = await _tradeRepo.GetByPortfolioIdAsync(portfolioId, ct);
        var allTrades = trades.ToList();
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var monthTrades = allTrades.Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd).ToList();

        // Previous month for comparison
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevMonthTrades = allTrades.Where(t => t.CreatedAt >= prevMonthStart && t.CreatedAt < monthStart).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<monthly_report>");
        sb.AppendLine($"  <portfolio>{portfolio.Name}</portfolio>");
        sb.AppendLine($"  <period>{month:00}/{year}</period>");
        sb.AppendLine($"  <total_trades>{monthTrades.Count}</total_trades>");

        if (monthTrades.Count > 0)
        {
            var buys = monthTrades.Where(t => t.TradeType == TradeType.BUY).ToList();
            var sells = monthTrades.Where(t => t.TradeType == TradeType.SELL).ToList();
            var totalBuyAmount = buys.Sum(t => t.Price * t.Quantity);
            var totalSellAmount = sells.Sum(t => t.Price * t.Quantity);
            var totalFees = monthTrades.Sum(t => t.Fee);
            var totalTax = monthTrades.Sum(t => t.Tax);
            sb.AppendLine($"  <buys>{buys.Count} lệnh — {totalBuyAmount:N0} VND</buys>");
            sb.AppendLine($"  <sells>{sells.Count} lệnh — {totalSellAmount:N0} VND</sells>");
            sb.AppendLine($"  <fees>{totalFees:N0} VND</fees>");
            sb.AppendLine($"  <tax>{totalTax:N0} VND</tax>");
            sb.AppendLine($"  <net_cost>{totalFees + totalTax:N0} VND</net_cost>");
            sb.AppendLine($"  <symbols_traded>{string.Join(", ", monthTrades.Select(t => t.Symbol).Distinct())}</symbols_traded>");

            // Calculate win/loss from sell trades
            if (sells.Count > 0)
            {
                int wins = 0, losses = 0;
                decimal totalRealizedPnL = 0;
                foreach (var sell in sells)
                {
                    var avgBuy = allTrades
                        .Where(t => t.TradeType == TradeType.BUY && t.Symbol == sell.Symbol && t.CreatedAt <= sell.CreatedAt)
                        .Select(t => t.Price).DefaultIfEmpty(sell.Price).Average();
                    var pnl = (sell.Price - avgBuy) * sell.Quantity - sell.Fee - sell.Tax;
                    totalRealizedPnL += pnl;
                    if (pnl >= 0) wins++; else losses++;
                }
                var winRate = sells.Count > 0 ? wins * 100.0 / sells.Count : 0;
                sb.AppendLine($"  <win_count>{wins}</win_count>");
                sb.AppendLine($"  <loss_count>{losses}</loss_count>");
                sb.AppendLine($"  <win_rate>{winRate:F0}%</win_rate>");
                sb.AppendLine($"  <realized_pnl>{totalRealizedPnL:+#,0;-#,0} VND</realized_pnl>");
            }
            sb.AppendLine("</monthly_report>");

            // Per-symbol P&L breakdown
            var symbolGroups = sells.GroupBy(t => t.Symbol).ToList();
            if (symbolGroups.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<symbol_performance>");
                sb.AppendLine("| Mã | Lệnh bán | Tổng bán | Giá TB mua | Ước tính Lãi/Lỗ |");
                sb.AppendLine("|-----|----------|----------|------------|------------------|");
                foreach (var sg in symbolGroups)
                {
                    var sellAmt = sg.Sum(t => t.Price * t.Quantity);
                    var avgBuyP = allTrades.Where(t => t.TradeType == TradeType.BUY && t.Symbol == sg.Key)
                        .Select(t => t.Price).DefaultIfEmpty(0).Average();
                    var estPnl = sg.Sum(t => (t.Price - avgBuyP) * t.Quantity);
                    sb.AppendLine($"| {sg.Key} | {sg.Count()} | {sellAmt:N0} | {avgBuyP:N0} | {estPnl:+#,0;-#,0} |");
                }
                sb.AppendLine("</symbol_performance>");
            }

            sb.AppendLine();
            sb.AppendLine("<transactions>");
            sb.AppendLine("| Ngày | Loại | Mã | SL | Giá | Phí | Thuế |");
            sb.AppendLine("|------|------|-----|-----|------|-----|------|");
            foreach (var t in monthTrades.OrderBy(t => t.CreatedAt))
                sb.AppendLine($"| {t.CreatedAt:dd/MM} | {t.TradeType} | {t.Symbol} | {t.Quantity} | {t.Price:N0} | {t.Fee:N0} | {t.Tax:N0} |");
            sb.AppendLine("</transactions>");
        }
        else
        {
            sb.AppendLine("  <note>Không có giao dịch nào trong tháng này.</note>");
            sb.AppendLine("</monthly_report>");
        }

        // Previous month comparison
        if (prevMonthTrades.Count > 0)
        {
            var prevSells = prevMonthTrades.Where(t => t.TradeType == TradeType.SELL).ToList();
            var prevBuyAmt = prevMonthTrades.Where(t => t.TradeType == TradeType.BUY).Sum(t => t.Price * t.Quantity);
            var prevSellAmt = prevSells.Sum(t => t.Price * t.Quantity);
            sb.AppendLine();
            sb.AppendLine("<previous_month>");
            sb.AppendLine($"  <period>{prevMonthStart:MM/yyyy}</period>");
            sb.AppendLine($"  <total_trades>{prevMonthTrades.Count}</total_trades>");
            sb.AppendLine($"  <buy_amount>{prevBuyAmt:N0} VND</buy_amount>");
            sb.AppendLine($"  <sell_amount>{prevSellAmt:N0} VND</sell_amount>");
            sb.AppendLine($"  <sell_count>{prevSells.Count}</sell_count>");
            sb.AppendLine("</previous_month>");
        }

        var systemPrompt = BasePrompt + $@"

Nhiệm vụ: Tổng kết hiệu suất đầu tư tháng {month:00}/{year}.
1. **Tổng quan hiệu suất**: Lãi/lỗ, return %, win rate, profit factor
2. **Giao dịch nổi bật**: Top winning/losing trades theo mã
3. **So sánh tháng trước**: Cải thiện hay xấu đi (win rate, P&L, số lệnh)
4. **Chi phí giao dịch**: Phí + thuế chiếm bao nhiêu % so với lợi nhuận
5. **Pattern hành vi**: Xu hướng, sai lầm lặp lại, trade quá nhiều?
6. **Điểm số tháng** (A-F): Chấm điểm dựa trên kỷ luật, lợi nhuận, win rate
7. **Gợi ý tháng tới**: 3 mục tiêu cụ thể nhất";

        var userMessage = $"Tổng kết tháng {month:00}/{year}:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildStockEvaluationContext(
        string userId, string symbol, string? question, CancellationToken ct)
    {
        symbol = symbol.ToUpper().Trim();

        // Fetch all data in parallel — individual failures should not crash the whole request
        var fundamentalTask = _fundamentalProvider.GetFundamentalsAsync(symbol, ct);
        var stockDetailTask = _stockInfoProvider.GetStockDetailAsync(symbol, ct);
        var technicalTask = _technicalService.AnalyzeAsync(symbol, ct);

        // Wait for all, even if some fail
        await Task.WhenAll(
            fundamentalTask.ContinueWith(_ => { }),
            stockDetailTask.ContinueWith(_ => { }),
            technicalTask.ContinueWith(_ => { })
        );

        var fundamental = fundamentalTask.IsCompletedSuccessfully ? fundamentalTask.Result : null;
        var stockDetail = stockDetailTask.IsCompletedSuccessfully ? stockDetailTask.Result : null;
        var technical = technicalTask.IsCompletedSuccessfully ? technicalTask.Result : null;

        if (stockDetail == null && fundamental == null)
            return new AiContextResult { ErrorMessage = $"Không tìm thấy dữ liệu cho mã {symbol}." };

        var sb = new StringBuilder();

        // Stock info
        sb.AppendLine("<stock_info>");
        sb.AppendLine($"  <symbol>{symbol}</symbol>");
        if (stockDetail != null)
        {
            sb.AppendLine($"  <company>{stockDetail.CompanyName}</company>");
            sb.AppendLine($"  <exchange>{stockDetail.Exchange}</exchange>");
            sb.AppendLine($"  <price>{stockDetail.Price:N0} VND</price>");
            sb.AppendLine($"  <change>{stockDetail.ChangePercent:+0.00;-0.00}%</change>");
            sb.AppendLine($"  <volume>{stockDetail.Volume:N0}</volume>");
        }
        if (fundamental?.Industry != null)
            sb.AppendLine($"  <industry>{fundamental.Industry}</industry>");
        sb.AppendLine("</stock_info>");

        // Fundamental metrics
        if (fundamental != null)
        {
            sb.AppendLine();
            sb.AppendLine("<fundamental_metrics>");
            sb.AppendLine("| Chỉ số | Giá trị |");
            sb.AppendLine("|--------|---------|");
            if (fundamental.PE.HasValue) sb.AppendLine($"| P/E | {fundamental.PE:F1} |");
            if (fundamental.PB.HasValue) sb.AppendLine($"| P/B | {fundamental.PB:F1} |");
            if (fundamental.EPS.HasValue) sb.AppendLine($"| EPS | {fundamental.EPS:N0} VND |");
            if (fundamental.ROE.HasValue) sb.AppendLine($"| ROE | {fundamental.ROE:F1}% |");
            if (fundamental.ROA.HasValue) sb.AppendLine($"| ROA | {fundamental.ROA:F1}% |");
            if (fundamental.DebtToEquity.HasValue) sb.AppendLine($"| Nợ/Vốn | {fundamental.DebtToEquity:F2} |");
            if (fundamental.MarketCap.HasValue) sb.AppendLine($"| Vốn hóa | {fundamental.MarketCap:N0} tỷ VND |");
            if (fundamental.DividendYield.HasValue) sb.AppendLine($"| Cổ tức | {fundamental.DividendYield:F1}% |");
            if (fundamental.RevenueGrowth.HasValue) sb.AppendLine($"| Tăng trưởng DT | {fundamental.RevenueGrowth:+0.0;-0.0}% |");
            if (fundamental.NetProfitGrowth.HasValue) sb.AppendLine($"| Tăng trưởng LN | {fundamental.NetProfitGrowth:+0.0;-0.0}% |");
            if (fundamental.ForeignPercent.HasValue) sb.AppendLine($"| SHNN | {fundamental.ForeignPercent:F1}% |");
            sb.AppendLine("</fundamental_metrics>");
        }

        // Technical signals
        if (technical != null && technical.DataPoints >= 20)
        {
            sb.AppendLine();
            sb.AppendLine("<technical_signals>");
            sb.AppendLine("| Chỉ báo | Giá trị | Tín hiệu |");
            sb.AppendLine("|---------|---------|-----------|");
            if (technical.Ema20.HasValue)
                sb.AppendLine($"| EMA20 | {technical.Ema20:N0} | {technical.EmaTrend} |");
            if (technical.Ema50.HasValue)
                sb.AppendLine($"| EMA50 | {technical.Ema50:N0} | — |");
            if (technical.Rsi14.HasValue)
                sb.AppendLine($"| RSI(14) | {technical.Rsi14:F1} | {technical.RsiSignal} |");
            sb.AppendLine($"| MACD | {technical.MacdLine:F2} | {technical.MacdSignal} |");
            sb.AppendLine($"| Khối lượng | {technical.VolumeRatio:F1}x avg | {technical.VolumeSignal} |");
            sb.AppendLine($"| **Tổng hợp** | **{technical.OverallSignalVi}** | {technical.BullishCount}↑ {technical.BearishCount}↓ {technical.NeutralCount}— |");
            sb.AppendLine("</technical_signals>");

            if (technical.SupportLevels.Count > 0 || technical.ResistanceLevels.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<support_resistance>");
                if (technical.SupportLevels.Count > 0)
                    sb.AppendLine($"  <supports>{string.Join(" | ", technical.SupportLevels.Select(s => $"{s:N0}"))}</supports>");
                if (technical.ResistanceLevels.Count > 0)
                    sb.AppendLine($"  <resistances>{string.Join(" | ", technical.ResistanceLevels.Select(r => $"{r:N0}"))}</resistances>");
                sb.AppendLine("</support_resistance>");
            }

            if (technical.SuggestedEntry.HasValue)
            {
                sb.AppendLine();
                sb.AppendLine("<trade_suggestion>");
                sb.AppendLine($"  <entry>{technical.SuggestedEntry:N0} VND</entry>");
                sb.AppendLine($"  <stop_loss>{technical.SuggestedStopLoss:N0} VND</stop_loss>");
                sb.AppendLine($"  <target>{technical.SuggestedTarget:N0} VND</target>");
                sb.AppendLine($"  <risk_reward>1:{technical.RiskRewardRatio:F1}</risk_reward>");
                sb.AppendLine("</trade_suggestion>");
            }
        }

        // Check if user already holds this symbol
        try
        {
            var portfolios = await _portfolioRepo.GetByUserIdAsync(userId, ct);
            foreach (var portfolio in portfolios)
            {
                var pnl = await _pnlService.CalculatePortfolioPnLAsync(portfolio.Id, ct);
                var pos = pnl?.Positions.FirstOrDefault(p => p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                if (pos != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("<user_position>");
                    sb.AppendLine($"  <portfolio>{portfolio.Name}</portfolio>");
                    sb.AppendLine($"  <quantity>{pos.Quantity:N0}</quantity>");
                    sb.AppendLine($"  <avg_cost>{pos.AverageCost:N0} VND</avg_cost>");
                    sb.AppendLine($"  <current_value>{pos.MarketValue:N0} VND</current_value>");
                    sb.AppendLine($"  <unrealized_pnl>{pos.TotalPnL:+#,0;-#,0} VND ({pos.TotalPnLPercent:+0.0;-0.0}%)</unrealized_pnl>");
                    sb.AppendLine("</user_position>");
                    break;
                }
            }
        }
        catch { /* Skip if PnL unavailable */ }

        // Check watchlist context
        try
        {
            var watchlists = await _watchlistRepo.GetByUserIdAsync(userId, ct);
            var wlItem = watchlists.SelectMany(w => w.Items).FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (wlItem != null)
            {
                sb.AppendLine();
                sb.AppendLine("<watchlist_context>");
                if (wlItem.TargetBuyPrice.HasValue) sb.AppendLine($"  <target_buy>{wlItem.TargetBuyPrice:N0} VND</target_buy>");
                if (wlItem.TargetSellPrice.HasValue) sb.AppendLine($"  <target_sell>{wlItem.TargetSellPrice:N0} VND</target_sell>");
                if (!string.IsNullOrEmpty(wlItem.Note)) sb.AppendLine($"  <note>{wlItem.Note}</note>");
                sb.AppendLine("</watchlist_context>");
            }
        }
        catch { /* Skip */ }

        // Check active trade plan
        try
        {
            var plans = await _tradePlanRepo.GetActiveByUserIdAsync(userId, ct);
            var activePlan = plans.FirstOrDefault(p => p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (activePlan != null)
            {
                sb.AppendLine();
                sb.AppendLine("<active_trade_plan>");
                sb.AppendLine($"  <direction>{activePlan.Direction}</direction>");
                sb.AppendLine($"  <entry>{activePlan.EntryPrice:N0} VND</entry>");
                sb.AppendLine($"  <stop_loss>{activePlan.StopLoss:N0} VND</stop_loss>");
                sb.AppendLine($"  <target>{activePlan.Target:N0} VND</target>");
                sb.AppendLine($"  <status>{activePlan.Status}</status>");
                sb.AppendLine("</active_trade_plan>");
            }
        }
        catch { /* Skip */ }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Đánh giá toàn diện mã cổ phiếu dựa trên dữ liệu cơ bản và kỹ thuật.
1. **Sức khỏe tài chính**: Đánh giá P/E, ROE, EPS, Nợ/Vốn — đang đắt hay rẻ so với ngành
2. **Tăng trưởng**: Doanh thu, lợi nhuận có bền vững không
3. **Phân tích kỹ thuật**: Xu hướng, tín hiệu mua/bán, hỗ trợ/kháng cự
4. **Rủi ro**: Nêu 2-3 rủi ro lớn nhất khi đầu tư vào mã này
5. **Vị thế hiện tại**: Nếu nhà đầu tư đang nắm giữ, nên giữ/mua thêm/bán bớt?
6. **Mức giá hành động**: Gợi ý entry cụ thể, SL, TP dựa trên phân tích
7. **Kết luận**: Đánh giá tổng thể (Hấp dẫn / Trung bình / Rủi ro cao) và gợi ý hành động";

        var userMessage = question ?? $"Đánh giá toàn diện mã cổ phiếu {symbol}:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    // =============================================
    // New context builders
    // =============================================

    private async Task<AiContextResult> BuildRiskAssessmentContext(
        string userId, string portfolioId, string? question, CancellationToken ct)
    {
        var portfolio = await _portfolioRepo.GetByIdAsync(portfolioId, ct);
        if (portfolio == null)
            return new AiContextResult { ErrorMessage = "Không tìm thấy danh mục." };

        // Fetch risk data in parallel
        var riskSummaryTask = _riskService.GetPortfolioRiskSummaryAsync(portfolioId, ct);
        var drawdownTask = _riskService.CalculateMaxDrawdownAsync(portfolioId, ct);
        var correlationTask = _riskService.CalculateCorrelationMatrixAsync(portfolioId, ct);
        var riskProfileTask = _riskProfileRepo.GetByPortfolioIdAsync(portfolioId, ct);

        await Task.WhenAll(
            riskSummaryTask.ContinueWith(_ => { }),
            drawdownTask.ContinueWith(_ => { }),
            correlationTask.ContinueWith(_ => { }),
            riskProfileTask.ContinueWith(_ => { }));

        var riskSummary = riskSummaryTask.IsCompletedSuccessfully ? riskSummaryTask.Result : null;
        var drawdown = drawdownTask.IsCompletedSuccessfully ? drawdownTask.Result : null;
        var correlation = correlationTask.IsCompletedSuccessfully ? correlationTask.Result : null;
        var riskProfile = riskProfileTask.IsCompletedSuccessfully ? riskProfileTask.Result : null;

        var sb = new StringBuilder();
        sb.AppendLine("<portfolio>");
        sb.AppendLine($"  <name>{portfolio.Name}</name>");
        sb.AppendLine($"  <initial_capital>{portfolio.InitialCapital:N0} VND</initial_capital>");
        sb.AppendLine("</portfolio>");

        if (riskSummary != null)
        {
            sb.AppendLine();
            sb.AppendLine("<risk_overview>");
            sb.AppendLine($"  <total_value>{riskSummary.TotalValue:N0} VND</total_value>");
            sb.AppendLine($"  <position_count>{riskSummary.PositionCount}</position_count>");
            sb.AppendLine($"  <var_95>{riskSummary.ValueAtRisk95:N0} VND</var_95>");
            sb.AppendLine($"  <max_drawdown>{riskSummary.MaxDrawdown:F1}%</max_drawdown>");
            sb.AppendLine($"  <largest_position>{riskSummary.LargestPositionPercent:F1}%</largest_position>");
            sb.AppendLine("</risk_overview>");

            if (riskSummary.Positions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<position_risks>");
                sb.AppendLine("| Mã | Giá trị | Tỷ trọng | SL | Khoảng cách SL | R:R |");
                sb.AppendLine("|-----|---------|----------|-----|---------------|------|");
                foreach (var pos in riskSummary.Positions.OrderByDescending(p => p.PositionSizePercent))
                {
                    var sl = pos.StopLossPrice.HasValue ? $"{pos.StopLossPrice:N0}" : "Chưa đặt";
                    var distSl = pos.DistanceToStopLossPercent != 0 ? $"{pos.DistanceToStopLossPercent:F1}%" : "—";
                    var rr = pos.RiskRewardRatio.HasValue ? $"1:{pos.RiskRewardRatio:F1}" : "—";
                    sb.AppendLine($"| {pos.Symbol} | {pos.MarketValue:N0} | {pos.PositionSizePercent:F1}% | {sl} | {distSl} | {rr} |");
                }
                sb.AppendLine("</position_risks>");
            }
        }

        if (drawdown != null)
        {
            sb.AppendLine();
            sb.AppendLine("<drawdown>");
            sb.AppendLine($"  <max_drawdown>{drawdown.MaxDrawdownPercent:F1}%</max_drawdown>");
            sb.AppendLine($"  <current_drawdown>{drawdown.CurrentDrawdownPercent:F1}%</current_drawdown>");
            if (drawdown.PeakDate.HasValue)
                sb.AppendLine($"  <peak>{drawdown.PeakValue:N0} VND ({drawdown.PeakDate:dd/MM/yyyy})</peak>");
            if (drawdown.TroughDate.HasValue)
                sb.AppendLine($"  <trough>{drawdown.TroughValue:N0} VND ({drawdown.TroughDate:dd/MM/yyyy})</trough>");
            sb.AppendLine("</drawdown>");
        }

        if (riskProfile != null)
        {
            sb.AppendLine();
            sb.AppendLine("<risk_profile>");
            sb.AppendLine($"  <max_position_size>{riskProfile.MaxPositionSizePercent}%</max_position_size>");
            sb.AppendLine($"  <max_sector_exposure>{riskProfile.MaxSectorExposurePercent}%</max_sector_exposure>");
            sb.AppendLine($"  <max_drawdown_alert>{riskProfile.MaxDrawdownAlertPercent}%</max_drawdown_alert>");
            sb.AppendLine($"  <min_risk_reward>{riskProfile.DefaultRiskRewardRatio}</min_risk_reward>");
            sb.AppendLine($"  <max_portfolio_risk>{riskProfile.MaxPortfolioRiskPercent}%</max_portfolio_risk>");

            // Compliance violations
            var violations = new List<string>();
            if (riskSummary != null)
            {
                if (riskSummary.LargestPositionPercent > riskProfile.MaxPositionSizePercent)
                    violations.Add($"Vị thế lớn nhất ({riskSummary.LargestPositionPercent:F1}%) > giới hạn ({riskProfile.MaxPositionSizePercent}%)");
                if (drawdown != null && drawdown.CurrentDrawdownPercent > riskProfile.MaxDrawdownAlertPercent)
                    violations.Add($"Drawdown hiện tại ({drawdown.CurrentDrawdownPercent:F1}%) > ngưỡng ({riskProfile.MaxDrawdownAlertPercent}%)");
                var noSlPositions = riskSummary.Positions.Where(p => !p.StopLossPrice.HasValue).ToList();
                if (noSlPositions.Count > 0)
                    violations.Add($"{noSlPositions.Count} vị thế chưa đặt stop-loss: {string.Join(", ", noSlPositions.Select(p => p.Symbol))}");
            }
            if (violations.Count > 0)
            {
                sb.AppendLine("  <violations>");
                foreach (var v in violations)
                    sb.AppendLine($"    - {v}");
                sb.AppendLine("  </violations>");
            }
            sb.AppendLine("</risk_profile>");
        }

        // High correlation pairs
        if (correlation?.Pairs.Count > 0)
        {
            var highCorr = correlation.Pairs.Where(p => Math.Abs(p.Correlation) > 0.7m).ToList();
            if (highCorr.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<correlation_warnings>");
                foreach (var pair in highCorr.OrderByDescending(p => Math.Abs(p.Correlation)))
                    sb.AppendLine($"  {pair.Symbol1} - {pair.Symbol2}: {pair.Correlation:F2}");
                sb.AppendLine("</correlation_warnings>");
            }
        }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Đánh giá rủi ro danh mục đầu tư toàn diện.
1. **Sức khỏe rủi ro**: Chấm điểm tổng thể 0-100, giải thích từng yếu tố
2. **Vi phạm giới hạn**: Liệt kê cụ thể giới hạn nào đang bị vi phạm, mức nghiêm trọng
3. **Tương quan**: Phân tích rủi ro tập trung do correlation cao giữa các mã
4. **Drawdown**: Đánh giá mức drawdown hiện tại, cảnh báo nếu gần ngưỡng
5. **Stop-loss**: Vị thế nào thiếu SL, vị thế nào gần SL
6. **Kế hoạch giảm rủi ro**: 3 hành động cụ thể để cải thiện risk profile ngay lập tức";

        var userMessage = question ?? $"Đánh giá rủi ro danh mục:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildPositionAdvisorContext(
        string userId, string? portfolioId, string? question, CancellationToken ct)
    {
        var portfolios = await _portfolioRepo.GetByUserIdAsync(userId, ct);
        var portfolioList = portfolios.ToList();
        if (portfolioList.Count == 0)
            return new AiContextResult { ErrorMessage = "Chưa có danh mục nào." };

        var sb = new StringBuilder();
        var allPositions = new List<(string PortfolioName, PositionPnL Pos)>();

        // Get positions from all portfolios (or filtered)
        var targetPortfolios = !string.IsNullOrEmpty(portfolioId)
            ? portfolioList.Where(p => p.Id == portfolioId).ToList()
            : portfolioList;

        foreach (var p in targetPortfolios)
        {
            try
            {
                var pnl = await _pnlService.CalculatePortfolioPnLAsync(p.Id, ct);
                if (pnl?.Positions.Count > 0)
                    allPositions.AddRange(pnl.Positions.Select(pos => (p.Name, pos)));
            }
            catch { /* Skip */ }
        }

        if (allPositions.Count == 0)
            return new AiContextResult { ErrorMessage = "Không có vị thế đang mở." };

        sb.AppendLine($"<positions_summary>");
        sb.AppendLine($"  <total_positions>{allPositions.Count}</total_positions>");
        sb.AppendLine($"  <total_value>{allPositions.Sum(p => p.Pos.MarketValue):N0} VND</total_value>");
        sb.AppendLine($"  <total_unrealized_pnl>{allPositions.Sum(p => p.Pos.TotalPnL):+#,0;-#,0} VND</total_unrealized_pnl>");
        sb.AppendLine($"</positions_summary>");

        sb.AppendLine();
        sb.AppendLine("<positions>");
        sb.AppendLine("| Danh mục | Mã | SL | Giá TB | Giá hiện tại | Lãi/Lỗ | % |");
        sb.AppendLine("|----------|-----|-----|--------|-------------|--------|------|");
        foreach (var (name, pos) in allPositions.OrderByDescending(p => Math.Abs(p.Pos.TotalPnL)))
        {
            sb.AppendLine($"| {name} | {pos.Symbol} | {pos.Quantity:N0} | {pos.AverageCost:N0} | {pos.CurrentPrice:N0} | {pos.TotalPnL:+#,0;-#,0} | {pos.TotalPnLPercent:+0.0;-0.0}% |");
        }
        sb.AppendLine("</positions>");

        // Check linked trade plans
        var plans = await _tradePlanRepo.GetActiveByUserIdAsync(userId, ct);
        var plansList = plans.ToList();
        var posSymbols = allPositions.Select(p => p.Pos.Symbol).Distinct().ToList();
        var withPlan = posSymbols.Where(s => plansList.Any(p => p.Symbol.Equals(s, StringComparison.OrdinalIgnoreCase))).ToList();
        var withoutPlan = posSymbols.Except(withPlan).ToList();

        if (withoutPlan.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"<missing_plans>{string.Join(", ", withoutPlan)}</missing_plans>");
        }

        // Technical signals for top 5 by value
        var top5Symbols = allPositions.OrderByDescending(p => p.Pos.MarketValue).Select(p => p.Pos.Symbol).Distinct().Take(5).ToList();
        var technicals = new List<(string Symbol, dynamic? Tech)>();
        foreach (var sym in top5Symbols)
        {
            try { var t = await _technicalService.AnalyzeAsync(sym, ct); technicals.Add((sym, t)); }
            catch { /* Skip */ }
        }

        if (technicals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<technical_signals>");
            sb.AppendLine("| Mã | RSI | MACD | Xu hướng | Tổng hợp |");
            sb.AppendLine("|-----|------|------|----------|----------|");
            foreach (var (sym, tech) in technicals)
            {
                if (tech != null && tech.DataPoints >= 20)
                    sb.AppendLine($"| {sym} | {tech.Rsi14:F1} | {tech.MacdSignal} | {tech.EmaTrend} | {tech.OverallSignalVi} |");
            }
            sb.AppendLine("</technical_signals>");
        }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Tư vấn quản lý vị thế đang mở.
1. **Tổng quan**: Đánh giá sức khỏe tổng thể các vị thế
2. **Vị thế nguy hiểm**: Vị thế nào đang lỗ nặng (> -5%) hoặc gần stop-loss
3. **Cơ hội chốt lời**: Vị thế nào đã đạt/gần target, nên chốt lời
4. **Thiếu kế hoạch**: Vị thế nào chưa có linked plan, cần tạo kế hoạch
5. **Tín hiệu kỹ thuật**: Tín hiệu hiện tại ủng hộ tiếp tục giữ hay nên thoát
6. **Hành động cụ thể**: Sắp xếp theo ưu tiên, vị thế nào cần xử lý trước nhất";

        var userMessage = question ?? $"Tư vấn vị thế đang mở:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildTradeAnalysisContext(
        string userId, string? portfolioId, string? question, CancellationToken ct)
    {
        List<Trade> allTrades;
        if (!string.IsNullOrEmpty(portfolioId))
        {
            var trades = await _tradeRepo.GetByPortfolioIdAsync(portfolioId, ct);
            allTrades = trades.ToList();
        }
        else
        {
            var portfolios = await _portfolioRepo.GetByUserIdAsync(userId, ct);
            allTrades = new List<Trade>();
            foreach (var p in portfolios)
            {
                var trades = await _tradeRepo.GetByPortfolioIdAsync(p.Id, ct);
                allTrades.AddRange(trades);
            }
        }

        if (allTrades.Count == 0)
            return new AiContextResult { ErrorMessage = "Chưa có giao dịch nào để phân tích." };

        var sb = new StringBuilder();

        // Overall statistics
        var buys = allTrades.Where(t => t.TradeType == TradeType.BUY).ToList();
        var sells = allTrades.Where(t => t.TradeType == TradeType.SELL).ToList();
        var totalFees = allTrades.Sum(t => t.Fee);
        var totalTax = allTrades.Sum(t => t.Tax);

        // Win/loss calculation from sells
        int wins = 0, losses = 0;
        decimal totalWinAmt = 0, totalLossAmt = 0;
        foreach (var sell in sells)
        {
            var avgBuy = allTrades
                .Where(t => t.TradeType == TradeType.BUY && t.Symbol == sell.Symbol && t.CreatedAt <= sell.CreatedAt)
                .Select(t => t.Price).DefaultIfEmpty(sell.Price).Average();
            var pnl = (sell.Price - avgBuy) * sell.Quantity;
            if (pnl >= 0) { wins++; totalWinAmt += pnl; }
            else { losses++; totalLossAmt += Math.Abs(pnl); }
        }
        var winRate = sells.Count > 0 ? wins * 100.0 / sells.Count : 0;
        var avgWin = wins > 0 ? totalWinAmt / wins : 0;
        var avgLoss = losses > 0 ? totalLossAmt / losses : 0;
        var profitFactor = totalLossAmt > 0 ? totalWinAmt / totalLossAmt : 0;

        sb.AppendLine("<trade_statistics>");
        sb.AppendLine($"  <total_trades>{allTrades.Count}</total_trades>");
        sb.AppendLine($"  <buy_count>{buys.Count}</buy_count>");
        sb.AppendLine($"  <sell_count>{sells.Count}</sell_count>");
        sb.AppendLine($"  <win_count>{wins}</win_count>");
        sb.AppendLine($"  <loss_count>{losses}</loss_count>");
        sb.AppendLine($"  <win_rate>{winRate:F0}%</win_rate>");
        sb.AppendLine($"  <avg_win>{avgWin:N0} VND</avg_win>");
        sb.AppendLine($"  <avg_loss>{avgLoss:N0} VND</avg_loss>");
        sb.AppendLine($"  <profit_factor>{profitFactor:F2}</profit_factor>");
        sb.AppendLine($"  <total_fees>{totalFees:N0} VND</total_fees>");
        sb.AppendLine($"  <total_tax>{totalTax:N0} VND</total_tax>");
        sb.AppendLine("</trade_statistics>");

        // Per-symbol breakdown
        var symbolGroups = allTrades.GroupBy(t => t.Symbol).OrderByDescending(g => g.Count()).Take(15).ToList();
        sb.AppendLine();
        sb.AppendLine("<symbol_breakdown>");
        sb.AppendLine("| Mã | Tổng GD | Mua | Bán | Tổng mua | Tổng bán |");
        sb.AppendLine("|-----|---------|-----|-----|----------|----------|");
        foreach (var g in symbolGroups)
        {
            var b = g.Where(t => t.TradeType == TradeType.BUY);
            var s = g.Where(t => t.TradeType == TradeType.SELL);
            sb.AppendLine($"| {g.Key} | {g.Count()} | {b.Count()} | {s.Count()} | {b.Sum(t => t.Price * t.Quantity):N0} | {s.Sum(t => t.Price * t.Quantity):N0} |");
        }
        sb.AppendLine("</symbol_breakdown>");

        // Plan adherence
        var plans = await _tradePlanRepo.GetByUserIdAsync(userId, ct);
        var planSymbols = plans.Select(p => p.Symbol).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tradeSymbols = allTrades.Select(t => t.Symbol).Distinct().ToList();
        var planned = tradeSymbols.Where(s => planSymbols.Contains(s)).Count();
        var unplanned = tradeSymbols.Count - planned;

        sb.AppendLine();
        sb.AppendLine("<plan_adherence>");
        sb.AppendLine($"  <total_symbols_traded>{tradeSymbols.Count}</total_symbols_traded>");
        sb.AppendLine($"  <with_plan>{planned}</with_plan>");
        sb.AppendLine($"  <without_plan>{unplanned}</without_plan>");
        sb.AppendLine($"  <adherence_rate>{(tradeSymbols.Count > 0 ? planned * 100 / tradeSymbols.Count : 0)}%</adherence_rate>");
        sb.AppendLine("</plan_adherence>");

        // Recent 20 trades
        var recentTrades = allTrades.OrderByDescending(t => t.CreatedAt).Take(20).ToList();
        sb.AppendLine();
        sb.AppendLine("<recent_trades>");
        sb.AppendLine("| Ngày | Loại | Mã | SL | Giá | Phí |");
        sb.AppendLine("|------|------|-----|-----|------|-----|");
        foreach (var t in recentTrades)
            sb.AppendLine($"| {t.CreatedAt:dd/MM/yyyy} | {t.TradeType} | {t.Symbol} | {t.Quantity} | {t.Price:N0} | {t.Fee:N0} |");
        sb.AppendLine("</recent_trades>");

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Phân tích giao dịch toàn diện.
1. **Hiệu suất tổng quan**: Win rate, profit factor, expectancy (avg win × win rate - avg loss × loss rate)
2. **Phân tích theo mã**: Mã nào trade tốt nhất/tệ nhất, mã nào nên tiếp tục/bỏ
3. **Kỷ luật kế hoạch**: Bao nhiêu % giao dịch tuân theo kế hoạch, không có plan có tệ hơn?
4. **Chi phí**: Phí + thuế chiếm bao nhiêu % lợi nhuận, có trade quá nhiều không
5. **Pattern hành vi**: Cắt lỗ chậm? Chốt lời sớm? Overtrading?
6. **Gợi ý tối ưu**: 3 điều chỉnh cụ thể nhất để cải thiện hiệu suất";

        var userMessage = question ?? $"Phân tích giao dịch:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildWatchlistScannerContext(
        string userId, string watchlistId, string? question, CancellationToken ct)
    {
        var watchlists = await _watchlistRepo.GetByUserIdAsync(userId, ct);
        var watchlist = watchlists.FirstOrDefault(w => w.Id == watchlistId);
        if (watchlist == null)
            return new AiContextResult { ErrorMessage = "Không tìm thấy watchlist." };

        if (watchlist.Items.Count == 0)
            return new AiContextResult { ErrorMessage = "Watchlist trống, chưa có mã nào." };

        var sb = new StringBuilder();
        sb.AppendLine($"<watchlist name=\"{watchlist.Name}\" count=\"{watchlist.Items.Count}\">");

        // Fetch current prices and technical signals
        var items = watchlist.Items.Take(15).ToList();
        sb.AppendLine();
        sb.AppendLine("| Mã | Ghi chú | Giá mua mục tiêu | Giá bán mục tiêu | Giá hiện tại | Thay đổi | Tín hiệu |");
        sb.AppendLine("|-----|---------|------------------|------------------|-------------|----------|----------|");

        foreach (var item in items)
        {
            var price = "—";
            var change = "—";
            var signal = "—";

            try
            {
                var detail = await _stockInfoProvider.GetStockDetailAsync(item.Symbol, ct);
                if (detail != null)
                {
                    price = $"{detail.Price:N0}";
                    change = $"{detail.ChangePercent:+0.0;-0.0}%";
                }
            }
            catch { /* Skip */ }

            try
            {
                var tech = await _technicalService.AnalyzeAsync(item.Symbol, ct);
                if (tech != null && tech.DataPoints >= 20)
                    signal = tech.OverallSignalVi;
            }
            catch { /* Skip */ }

            var note = item.Note?.Replace("|", "/") ?? "—";
            var targetBuy = item.TargetBuyPrice.HasValue ? $"{item.TargetBuyPrice:N0}" : "—";
            var targetSell = item.TargetSellPrice.HasValue ? $"{item.TargetSellPrice:N0}" : "—";
            sb.AppendLine($"| {item.Symbol} | {note} | {targetBuy} | {targetSell} | {price} | {change} | {signal} |");
        }
        sb.AppendLine("</watchlist>");

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Quét và đánh giá watchlist cổ phiếu.
1. **Cơ hội mua**: Mã nào đang gần giá mua mục tiêu hoặc tại vùng hỗ trợ
2. **Tín hiệu kỹ thuật**: Mã nào có tín hiệu tích cực (RSI oversold, tín hiệu Mua)
3. **Cảnh báo**: Mã nào nên xoá khỏi watchlist (xu hướng giảm, tín hiệu tiêu cực)
4. **Xếp hạng ưu tiên**: Top 3 mã hấp dẫn nhất để mua, Top 3 nên bỏ
5. **Kế hoạch hành động**: Gợi ý entry, SL, TP cho top 3 mã hấp dẫn nhất";

        var userMessage = question ?? $"Quét watchlist \"{watchlist.Name}\":\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildDailyBriefingContext(
        string userId, string? question, CancellationToken ct)
    {
        var portfolios = await _portfolioRepo.GetByUserIdAsync(userId, ct);
        var portfolioList = portfolios.ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"<date>{DateTime.UtcNow:dd/MM/yyyy (dddd)}</date>");

        // Portfolio overview
        decimal totalInvested = 0, totalValue = 0, totalPnL = 0;
        var allPositions = new List<PositionPnL>();

        foreach (var p in portfolioList)
        {
            try
            {
                var pnl = await _pnlService.CalculatePortfolioPnLAsync(p.Id, ct);
                if (pnl != null)
                {
                    totalInvested += pnl.TotalInvested;
                    totalValue += pnl.TotalMarketValue;
                    totalPnL += pnl.TotalUnrealizedPnL;
                    allPositions.AddRange(pnl.Positions);
                }
            }
            catch { /* Skip */ }
        }

        sb.AppendLine();
        sb.AppendLine("<portfolio_overview>");
        sb.AppendLine($"  <portfolios>{portfolioList.Count}</portfolios>");
        sb.AppendLine($"  <total_invested>{totalInvested:N0} VND</total_invested>");
        sb.AppendLine($"  <total_value>{totalValue:N0} VND</total_value>");
        sb.AppendLine($"  <unrealized_pnl>{totalPnL:+#,0;-#,0} VND</unrealized_pnl>");
        if (totalInvested > 0)
            sb.AppendLine($"  <return>{totalPnL / totalInvested * 100:+0.0;-0.0}%</return>");
        sb.AppendLine("</portfolio_overview>");

        // Top positions
        if (allPositions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<top_positions>");
            sb.AppendLine("| Mã | Giá trị | Lãi/Lỗ % |");
            sb.AppendLine("|-----|---------|----------|");
            foreach (var pos in allPositions.OrderByDescending(p => p.MarketValue).Take(10))
                sb.AppendLine($"| {pos.Symbol} | {pos.MarketValue:N0} | {pos.TotalPnLPercent:+0.0;-0.0}% |");
            sb.AppendLine("</top_positions>");

            // Risk alerts: positions with heavy loss or near SL
            var riskyPositions = allPositions.Where(p => p.TotalPnLPercent < -5).ToList();
            if (riskyPositions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<risk_alerts>");
                foreach (var rp in riskyPositions.OrderBy(p => p.TotalPnLPercent))
                    sb.AppendLine($"  ⚠️ {rp.Symbol}: {rp.TotalPnLPercent:+0.0;-0.0}% (lỗ {rp.TotalPnL:N0} VND)");
                sb.AppendLine("</risk_alerts>");
            }
        }

        // Pending trade plans
        try
        {
            var plans = await _tradePlanRepo.GetActiveByUserIdAsync(userId, ct);
            var pendingPlans = plans.Where(p => p.Status.ToString() == "Draft" || p.Status.ToString() == "Ready").ToList();
            if (pendingPlans.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<pending_plans>");
                foreach (var p in pendingPlans.Take(5))
                    sb.AppendLine($"  {p.Symbol} ({p.Direction}) — Entry: {p.EntryPrice:N0}, Status: {p.Status}");
                sb.AppendLine("</pending_plans>");
            }
        }
        catch { /* Skip */ }

        // Watchlist alerts
        try
        {
            var watchlists = await _watchlistRepo.GetByUserIdAsync(userId, ct);
            var alertItems = new List<string>();
            foreach (var wl in watchlists)
            {
                foreach (var item in wl.Items.Where(i => i.TargetBuyPrice.HasValue || i.TargetSellPrice.HasValue).Take(10))
                {
                    try
                    {
                        var detail = await _stockInfoProvider.GetStockDetailAsync(item.Symbol, ct);
                        if (detail != null)
                        {
                            if (item.TargetBuyPrice.HasValue && detail.Price <= item.TargetBuyPrice.Value)
                                alertItems.Add($"📉 {item.Symbol}: giá {detail.Price:N0} ≤ mục tiêu mua {item.TargetBuyPrice:N0}");
                            if (item.TargetSellPrice.HasValue && detail.Price >= item.TargetSellPrice.Value)
                                alertItems.Add($"📈 {item.Symbol}: giá {detail.Price:N0} ≥ mục tiêu bán {item.TargetSellPrice:N0}");
                        }
                    }
                    catch { /* Skip */ }
                }
            }
            if (alertItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<watchlist_alerts>");
                foreach (var a in alertItems)
                    sb.AppendLine($"  {a}");
                sb.AppendLine("</watchlist_alerts>");
            }
        }
        catch { /* Skip */ }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Tạo bản tin đầu tư hàng ngày cho nhà đầu tư.
1. **Tóm tắt buổi sáng**: Tổng quan nhanh tình hình danh mục trong 3-5 dòng
2. **Cần hành động ngay**: Vị thế gần SL hoặc lỗ nặng, kế hoạch cần thực thi
3. **Cơ hội hôm nay**: Watchlist items gần giá mục tiêu, kế hoạch sẵn sàng thực thi
4. **Cảnh báo rủi ro**: Tập trung quá mức, thiếu SL, drawdown
5. **Checklist hôm nay**: 3-5 việc cụ thể nhà đầu tư nên làm hôm nay";

        var userMessage = question ?? $"Bản tin đầu tư hôm nay:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    // =============================================
    // Helpers
    // =============================================

    private async Task<(string? apiKey, string model, string provider, AiSettings? settings)> GetUserSettings(
        string userId, CancellationToken ct)
    {
        var settings = await _settingsRepo.GetByUserIdAsync(userId, ct);
        if (settings == null)
            return (null, "", "claude", null);

        var encryptedKey = settings.GetActiveEncryptedApiKey();
        if (string.IsNullOrEmpty(encryptedKey))
            return (null, "", settings.Provider ?? "claude", null);

        var apiKey = _encryption.Decrypt(encryptedKey);
        return (apiKey, settings.Model, settings.Provider ?? "claude", settings);
    }

    private static AiStreamChunk NoApiKeyError() => new()
    {
        Type = "error",
        ErrorMessage = "Chưa cấu hình API key cho nhà cung cấp AI đang chọn. Vui lòng vào Cài đặt AI để nhập API key."
    };

    private async IAsyncEnumerable<AiStreamChunk> StreamAndTrackUsage(
        string apiKey, string model, string provider, string systemPrompt,
        List<AiChatMessage> messages, AiSettings settings,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var chatService = _chatServiceFactory.GetService(provider);
        int totalInput = 0, totalOutput = 0;

        await foreach (var chunk in chatService.StreamChatAsync(apiKey, model, systemPrompt, messages, ct))
        {
            if (chunk.InputTokens.HasValue) totalInput += chunk.InputTokens.Value;
            if (chunk.OutputTokens.HasValue) totalOutput += chunk.OutputTokens.Value;
            yield return chunk;
        }

        if (totalInput > 0 || totalOutput > 0)
        {
            var cost = CalculateCost(provider, model, totalInput, totalOutput);
            settings.AddTokenUsage(totalInput, totalOutput, cost);
            await _settingsRepo.UpdateAsync(settings, ct);
        }
    }

    private static decimal CalculateCost(string provider, string model, int inputTokens, int outputTokens)
    {
        decimal inputPricePerMTok, outputPricePerMTok;

        if (provider == "gemini")
        {
            if (model.Contains("2.5-pro", StringComparison.OrdinalIgnoreCase))
            { inputPricePerMTok = 1.25m; outputPricePerMTok = 10m; }
            else if (model.Contains("2.5-flash", StringComparison.OrdinalIgnoreCase))
            { inputPricePerMTok = 0.15m; outputPricePerMTok = 0.60m; }
            else
            { inputPricePerMTok = 0.10m; outputPricePerMTok = 0.40m; }
        }
        else
        {
            if (model.Contains("opus", StringComparison.OrdinalIgnoreCase))
            { inputPricePerMTok = 15m; outputPricePerMTok = 75m; }
            else
            { inputPricePerMTok = 3m; outputPricePerMTok = 15m; }
        }

        return (inputTokens / 1_000_000m * inputPricePerMTok) +
               (outputTokens / 1_000_000m * outputPricePerMTok);
    }
}
