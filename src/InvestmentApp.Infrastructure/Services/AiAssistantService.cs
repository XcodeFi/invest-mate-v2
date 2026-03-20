using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Services;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

public class AiAssistantService : IAiAssistantService
{
    private readonly IAiSettingsRepository _settingsRepo;
    private readonly IAiKeyEncryptionService _encryption;
    private readonly IAiChatService _chatService;
    private readonly ITradeJournalRepository _journalRepo;
    private readonly ITradeRepository _tradeRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IPnLService _pnlService;
    private readonly ITradePlanRepository _tradePlanRepo;

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
        IAiChatService chatService,
        ITradeJournalRepository journalRepo,
        ITradeRepository tradeRepo,
        IPortfolioRepository portfolioRepo,
        IPnLService pnlService,
        ITradePlanRepository tradePlanRepo)
    {
        _settingsRepo = settingsRepo;
        _encryption = encryption;
        _chatService = chatService;
        _journalRepo = journalRepo;
        _tradeRepo = tradeRepo;
        _portfolioRepo = portfolioRepo;
        _pnlService = pnlService;
        _tradePlanRepo = tradePlanRepo;
    }

    public async IAsyncEnumerable<AiStreamChunk> ReviewJournalAsync(
        string userId, string? portfolioId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null)
        {
            yield return NoApiKeyError();
            yield break;
        }

        // Gather journal context
        IEnumerable<TradeJournal> journals;
        if (!string.IsNullOrEmpty(portfolioId))
            journals = await _journalRepo.GetByPortfolioIdAsync(portfolioId, ct);
        else
            journals = await _journalRepo.GetByUserIdAsync(userId, ct);

        var journalList = journals.OrderByDescending(j => j.CreatedAt).Take(20).ToList();
        if (journalList.Count == 0)
        {
            yield return new AiStreamChunk { Type = "error", ErrorMessage = "Chưa có nhật ký giao dịch nào để phân tích." };
            yield break;
        }

        var contextSb = new StringBuilder();
        foreach (var j in journalList)
        {
            contextSb.AppendLine($"---\nNgày: {j.CreatedAt:dd/MM/yyyy}");
            contextSb.AppendLine($"Lý do vào lệnh: {j.EntryReason}");
            contextSb.AppendLine($"Bối cảnh thị trường: {j.MarketContext}");
            contextSb.AppendLine($"Setup kỹ thuật: {j.TechnicalSetup}");
            contextSb.AppendLine($"Cảm xúc: {j.EmotionalState}");
            contextSb.AppendLine($"Độ tự tin: {j.ConfidenceLevel}/10");
            if (!string.IsNullOrEmpty(j.PostTradeReview))
                contextSb.AppendLine($"Đánh giá sau GD: {j.PostTradeReview}");
            if (!string.IsNullOrEmpty(j.LessonsLearned))
                contextSb.AppendLine($"Bài học: {j.LessonsLearned}");
            contextSb.AppendLine($"Đánh giá: {j.Rating}/5 sao");
            if (j.Tags.Count > 0)
                contextSb.AppendLine($"Tags: {string.Join(", ", j.Tags)}");
        }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Phân tích nhật ký giao dịch của nhà đầu tư.
1. **Tâm lý giao dịch**: Nhận diện cảm xúc (FOMO, tham lam, sợ hãi, revenge trading, tự tin thái quá)
2. **Kỷ luật**: Đánh giá mức độ tuân thủ kế hoạch giao dịch
3. **Điểm mạnh & yếu**: Quyết định tốt vs sai lầm lặp lại
4. **Gợi ý cải thiện**: Hành động cụ thể để cải thiện";

        var userMessage = question ?? $"Phân tích {journalList.Count} nhật ký giao dịch gần nhất:\n\n{contextSb}";

        var messages = new List<AiChatMessage> { new() { Role = "user", Content = userMessage } };

        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, systemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> ReviewPortfolioAsync(
        string userId, string portfolioId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null)
        {
            yield return NoApiKeyError();
            yield break;
        }

        var portfolio = await _portfolioRepo.GetByIdAsync(portfolioId, ct);
        if (portfolio == null)
        {
            yield return new AiStreamChunk { Type = "error", ErrorMessage = "Không tìm thấy danh mục." };
            yield break;
        }

        var trades = await _tradeRepo.GetByPortfolioIdAsync(portfolioId, ct);
        var tradeList = trades.ToList();

        // Build positions summary from trades
        var contextSb = new StringBuilder();
        contextSb.AppendLine($"Danh mục: {portfolio.Name}");
        contextSb.AppendLine($"Vốn ban đầu: {portfolio.InitialCapital:N0} VND");
        contextSb.AppendLine($"Tổng giao dịch: {tradeList.Count}");
        contextSb.AppendLine();

        // Group trades by symbol to show positions
        var symbols = tradeList.GroupBy(t => t.Symbol).ToList();
        contextSb.AppendLine("Các mã đang giao dịch:");
        foreach (var group in symbols)
        {
            var buys = group.Where(t => t.TradeType.ToString() == "Buy").Sum(t => t.Quantity);
            var sells = group.Where(t => t.TradeType.ToString() == "Sell").Sum(t => t.Quantity);
            var net = buys - sells;
            var avgBuy = group.Where(t => t.TradeType.ToString() == "Buy").Select(t => t.Price).DefaultIfEmpty(0).Average();
            contextSb.AppendLine($"- {group.Key}: Mua {buys}, Bán {sells}, Còn {net} CP, Giá TB mua: {avgBuy:N0} VND");
        }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Đánh giá danh mục đầu tư.
1. **Đa dạng hóa**: Cảnh báo nếu 1 mã chiếm > 30% danh mục
2. **Hiệu suất**: P&L, vị thế đang lỗ
3. **Rủi ro**: Drawdown, vị thế gần stop-loss, tập trung ngành
4. **Gợi ý**: Cân bằng lại, chốt lời/cắt lỗ, đa dạng hóa";

        var userMessage = question ?? $"Đánh giá danh mục đầu tư:\n\n{contextSb}";
        var messages = new List<AiChatMessage> { new() { Role = "user", Content = userMessage } };

        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, systemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> AdviseTradePlanAsync(
        string userId, string tradePlanId, string? question,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null)
        {
            yield return NoApiKeyError();
            yield break;
        }

        var plan = await _tradePlanRepo.GetByIdAsync(tradePlanId, ct);
        if (plan == null)
        {
            yield return new AiStreamChunk { Type = "error", ErrorMessage = "Không tìm thấy kế hoạch giao dịch." };
            yield break;
        }

        var contextSb = new StringBuilder();
        contextSb.AppendLine($"Mã: {plan.Symbol}");
        contextSb.AppendLine($"Hướng: {plan.Direction}");
        contextSb.AppendLine($"Giá entry: {plan.EntryPrice:N0} VND");
        contextSb.AppendLine($"Stop-loss: {plan.StopLoss:N0} VND");
        contextSb.AppendLine($"Take-profit: {plan.Target:N0} VND");
        contextSb.AppendLine($"Số lượng: {plan.Quantity}");
        contextSb.AppendLine($"Chế độ vào lệnh: {plan.EntryMode}");
        contextSb.AppendLine($"Trạng thái: {plan.Status}");
        if (!string.IsNullOrEmpty(plan.Reason))
            contextSb.AppendLine($"Lý do vào lệnh: {plan.Reason}");
        if (!string.IsNullOrEmpty(plan.MarketCondition))
            contextSb.AppendLine($"Điều kiện thị trường: {plan.MarketCondition}");
        contextSb.AppendLine($"Độ tự tin: {plan.ConfidenceLevel}/10");

        // Calculate R:R
        if (plan.EntryPrice > 0 && plan.StopLoss > 0 && plan.Target > 0)
        {
            var risk = Math.Abs(plan.EntryPrice - plan.StopLoss);
            var reward = Math.Abs(plan.Target - plan.EntryPrice);
            var rr = risk > 0 ? reward / risk : 0;
            contextSb.AppendLine($"Risk:Reward = 1:{rr:F1}");
        }

        // Exit targets
        if (plan.ExitTargets.Count > 0)
        {
            contextSb.AppendLine("Mục tiêu thoát:");
            foreach (var t in plan.ExitTargets)
                contextSb.AppendLine($"  - {t.ActionType}: Giá {t.Price:N0}, {t.PercentOfPosition}% vị thế");
        }

        var systemPrompt = BasePrompt + @"

Nhiệm vụ: Tư vấn kế hoạch giao dịch.
1. **Entry**: Điểm vào có hợp lý không? So với hỗ trợ/kháng cự
2. **Stop-loss**: SL quá gần/xa? Risk per trade
3. **Take-profit**: TP realistic? Risk:Reward ratio
4. **Position sizing**: Kích thước vị thế có phù hợp?
5. **Chấm điểm** (1-10) và gợi ý điều chỉnh";

        var userMessage = question ?? $"Đánh giá kế hoạch giao dịch:\n\n{contextSb}";
        var messages = new List<AiChatMessage> { new() { Role = "user", Content = userMessage } };

        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, systemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> ChatAsync(
        string userId, string message, List<AiChatMessage>? history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null)
        {
            yield return NoApiKeyError();
            yield break;
        }

        // Brief portfolio context
        var portfolios = await _portfolioRepo.GetByUserIdAsync(userId, ct);
        var portfolioList = portfolios.ToList();

        var contextSb = new StringBuilder();
        if (portfolioList.Count > 0)
        {
            contextSb.AppendLine($"Người dùng có {portfolioList.Count} danh mục:");
            foreach (var p in portfolioList)
                contextSb.AppendLine($"- {p.Name} (vốn: {p.InitialCapital:N0} VND)");
        }

        var systemPrompt = BasePrompt + @"

Bạn có thể trả lời về: chiến lược đầu tư, phân tích kỹ thuật, quản lý rủi ro, cách sử dụng app Investment Mate, thị trường chứng khoán Việt Nam.
" + (contextSb.Length > 0 ? $"\nThông tin danh mục:\n{contextSb}" : "");

        var messages = history?.ToList() ?? new List<AiChatMessage>();
        messages.Add(new AiChatMessage { Role = "user", Content = message });

        // Keep only last 20 messages to avoid context overflow
        if (messages.Count > 20)
            messages = messages.Skip(messages.Count - 20).ToList();

        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, systemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    public async IAsyncEnumerable<AiStreamChunk> MonthlySummaryAsync(
        string userId, string portfolioId, int year, int month,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (apiKey, model, settings) = await GetUserSettings(userId, ct);
        if (apiKey == null)
        {
            yield return NoApiKeyError();
            yield break;
        }

        var portfolio = await _portfolioRepo.GetByIdAsync(portfolioId, ct);
        if (portfolio == null)
        {
            yield return new AiStreamChunk { Type = "error", ErrorMessage = "Không tìm thấy danh mục." };
            yield break;
        }

        var trades = await _tradeRepo.GetByPortfolioIdAsync(portfolioId, ct);
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var monthTrades = trades.Where(t => t.CreatedAt >= monthStart && t.CreatedAt < monthEnd).ToList();

        var contextSb = new StringBuilder();
        contextSb.AppendLine($"Danh mục: {portfolio.Name}");
        contextSb.AppendLine($"Tháng: {month:00}/{year}");
        contextSb.AppendLine($"Số giao dịch trong tháng: {monthTrades.Count}");

        if (monthTrades.Count > 0)
        {
            var buys = monthTrades.Where(t => t.TradeType.ToString() == "Buy").ToList();
            var sells = monthTrades.Where(t => t.TradeType.ToString() == "Sell").ToList();
            contextSb.AppendLine($"Lệnh mua: {buys.Count}, Lệnh bán: {sells.Count}");
            contextSb.AppendLine("Chi tiết giao dịch:");
            foreach (var t in monthTrades.OrderBy(t => t.CreatedAt))
            {
                contextSb.AppendLine($"  - {t.CreatedAt:dd/MM} {t.TradeType} {t.Symbol} x{t.Quantity} @ {t.Price:N0} VND");
            }
        }
        else
        {
            contextSb.AppendLine("Không có giao dịch nào trong tháng này.");
        }

        var systemPrompt = BasePrompt + $@"

Nhiệm vụ: Tổng kết hiệu suất đầu tư tháng {month:00}/{year}.
1. **Tổng quan hiệu suất**: Lãi/lỗ, return %, win rate
2. **Giao dịch nổi bật**: Top winning/losing trades
3. **Pattern**: Xu hướng hành vi, sai lầm lặp lại
4. **Gợi ý tháng tới**: Mục tiêu cụ thể để cải thiện";

        var userMessage = $"Tổng kết tháng {month:00}/{year}:\n\n{contextSb}";
        var messages = new List<AiChatMessage> { new() { Role = "user", Content = userMessage } };

        await foreach (var chunk in StreamAndTrackUsage(apiKey, model, systemPrompt, messages, settings!, ct))
            yield return chunk;
    }

    // --- Helpers ---

    private async Task<(string? apiKey, string model, Domain.Entities.AiSettings? settings)> GetUserSettings(
        string userId, CancellationToken ct)
    {
        var settings = await _settingsRepo.GetByUserIdAsync(userId, ct);
        if (settings == null || string.IsNullOrEmpty(settings.EncryptedApiKey))
            return (null, "", null);

        var apiKey = _encryption.Decrypt(settings.EncryptedApiKey);
        return (apiKey, settings.Model, settings);
    }

    private static AiStreamChunk NoApiKeyError() => new()
    {
        Type = "error",
        ErrorMessage = "Chưa cấu hình API key. Vui lòng vào Cài đặt AI để nhập Anthropic API key."
    };

    private async IAsyncEnumerable<AiStreamChunk> StreamAndTrackUsage(
        string apiKey, string model, string systemPrompt,
        List<AiChatMessage> messages, Domain.Entities.AiSettings settings,
        [EnumeratorCancellation] CancellationToken ct)
    {
        int totalInput = 0, totalOutput = 0;

        await foreach (var chunk in _chatService.StreamChatAsync(apiKey, model, systemPrompt, messages, ct))
        {
            if (chunk.InputTokens.HasValue) totalInput += chunk.InputTokens.Value;
            if (chunk.OutputTokens.HasValue) totalOutput += chunk.OutputTokens.Value;
            yield return chunk;
        }

        // Track usage
        if (totalInput > 0 || totalOutput > 0)
        {
            var cost = CalculateCost(model, totalInput, totalOutput);
            settings.AddTokenUsage(totalInput, totalOutput, cost);
            await _settingsRepo.UpdateAsync(settings, ct);
        }
    }

    private static decimal CalculateCost(string model, int inputTokens, int outputTokens)
    {
        decimal inputPricePerMTok, outputPricePerMTok;

        if (model.Contains("opus", StringComparison.OrdinalIgnoreCase))
        {
            inputPricePerMTok = 15m;
            outputPricePerMTok = 75m;
        }
        else
        {
            // Sonnet (default)
            inputPricePerMTok = 3m;
            outputPricePerMTok = 15m;
        }

        return (inputTokens / 1_000_000m * inputPricePerMTok) +
               (outputTokens / 1_000_000m * outputPricePerMTok);
    }
}
