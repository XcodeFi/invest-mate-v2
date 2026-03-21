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
        ITechnicalIndicatorService technicalService)
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

    // =============================================
    // BuildContextAsync — public, no API key needed
    // =============================================

    public async Task<AiContextResult> BuildContextAsync(string useCase, string userId,
        string? portfolioId, string? tradePlanId, string? symbol, string? question,
        int? year, int? month, string? message, List<AiChatMessage>? history,
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
                "portfolio-review" or "monthly-summary" =>
                    new AiContextResult { ErrorMessage = "Thiếu portfolioId." },
                "trade-plan-advisor" =>
                    new AiContextResult { ErrorMessage = "Thiếu tradePlanId." },
                "stock-evaluation" =>
                    new AiContextResult { ErrorMessage = "Thiếu mã cổ phiếu (symbol)." },
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
        foreach (var j in journalList.Take(5))
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
4. **Gợi ý cải thiện**: Hành động cụ thể để cải thiện";

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

        // Get PnL summary with current prices
        PortfolioPnLSummary? pnl = null;
        try { pnl = await _pnlService.CalculatePortfolioPnLAsync(portfolioId, ct); }
        catch { /* PnL may fail if market data unavailable */ }

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

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Đánh giá danh mục đầu tư.
1. **Tổng quan hiệu suất**: Tổng P&L, return %, so sánh với vốn ban đầu
2. **Phân tích vị thế**: Mã nào đang lãi/lỗ nhiều nhất, tỷ trọng từng mã
3. **Đa dạng hóa**: Cảnh báo nếu 1 mã chiếm > 30% danh mục, tập trung ngành
4. **Rủi ro**: Vị thế đang lỗ nặng, drawdown, gợi ý cắt lỗ nếu cần
5. **Gợi ý cụ thể**: Cân bằng lại, chốt lời/cắt lỗ, mã nên thêm/giảm";

        var userMessage = question ?? $"Đánh giá danh mục đầu tư:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildTradePlanAdvisorContext(
        string userId, string tradePlanId, string? question, CancellationToken ct)
    {
        var plan = await _tradePlanRepo.GetByIdAsync(tradePlanId, ct);
        if (plan == null)
            return new AiContextResult { ErrorMessage = "Không tìm thấy kế hoạch giao dịch." };

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

        if (plan.EntryPrice > 0 && plan.StopLoss > 0 && plan.Target > 0)
        {
            var risk = Math.Abs(plan.EntryPrice - plan.StopLoss);
            var reward = Math.Abs(plan.Target - plan.EntryPrice);
            var rr = risk > 0 ? reward / risk : 0;
            sb.AppendLine($"  <risk_reward>1:{rr:F1}</risk_reward>");
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

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Tư vấn kế hoạch giao dịch.
1. **Entry**: Điểm vào có hợp lý không? So với hỗ trợ/kháng cự
2. **Stop-loss**: SL quá gần/xa? Risk per trade
3. **Take-profit**: TP realistic? Risk:Reward ratio
4. **Position sizing**: Kích thước vị thế có phù hợp?
5. **Chấm điểm** (1-10) và gợi ý điều chỉnh";

        var userMessage = question ?? $"Đánh giá kế hoạch giao dịch:\n\n{sb}";
        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = userMessage };
    }

    private async Task<AiContextResult> BuildChatContext(
        string userId, string message, List<AiChatMessage>? history, CancellationToken ct)
    {
        var portfolios = await _portfolioRepo.GetByUserIdAsync(userId, ct);
        var portfolioList = portfolios.ToList();

        var sb = new StringBuilder();
        if (portfolioList.Count > 0)
        {
            sb.AppendLine("<user_portfolios>");
            foreach (var p in portfolioList)
                sb.AppendLine($"  <portfolio name=\"{p.Name}\" capital=\"{p.InitialCapital:N0} VND\" />");
            sb.AppendLine("</user_portfolios>");
        }

        var systemPrompt = BasePrompt + @"

Bạn có thể trả lời về: chiến lược đầu tư, phân tích kỹ thuật, quản lý rủi ro, cách sử dụng app Investment Mate, thị trường chứng khoán Việt Nam.
" + (sb.Length > 0 ? $"\n{sb}" : "");

        return new AiContextResult { SystemPrompt = systemPrompt, UserMessage = message };
    }

    private async Task<AiContextResult> BuildMonthlySummaryContext(
        string userId, string portfolioId, int year, int month, CancellationToken ct)
    {
        var portfolio = await _portfolioRepo.GetByIdAsync(portfolioId, ct);
        if (portfolio == null)
            return new AiContextResult { ErrorMessage = "Không tìm thấy danh mục." };

        var trades = await _tradeRepo.GetByPortfolioIdAsync(portfolioId, ct);
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var monthTrades = trades.Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd).ToList();

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
            sb.AppendLine($"  <symbols_traded>{string.Join(", ", monthTrades.Select(t => t.Symbol).Distinct())}</symbols_traded>");
            sb.AppendLine("</monthly_report>");

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

        var systemPrompt = BasePrompt + $@"

Nhiệm vụ: Tổng kết hiệu suất đầu tư tháng {month:00}/{year}.
1. **Tổng quan hiệu suất**: Lãi/lỗ, return %, win rate
2. **Giao dịch nổi bật**: Top winning/losing trades
3. **Pattern**: Xu hướng hành vi, sai lầm lặp lại
4. **Gợi ý tháng tới**: Mục tiêu cụ thể để cải thiện";

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

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Đánh giá toàn diện mã cổ phiếu dựa trên dữ liệu cơ bản và kỹ thuật.
1. **Sức khỏe tài chính**: Đánh giá P/E, ROE, EPS, Nợ/Vốn — đang đắt hay rẻ so với ngành
2. **Tăng trưởng**: Doanh thu, lợi nhuận có bền vững không
3. **Phân tích kỹ thuật**: Xu hướng, tín hiệu mua/bán, hỗ trợ/kháng cự
4. **Rủi ro**: Nêu 2-3 rủi ro lớn nhất khi đầu tư vào mã này
5. **Kết luận**: Đánh giá tổng thể (Hấp dẫn / Trung bình / Rủi ro cao) và gợi ý hành động";

        var userMessage = question ?? $"Đánh giá toàn diện mã cổ phiếu {symbol}:\n\n{sb}";
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
