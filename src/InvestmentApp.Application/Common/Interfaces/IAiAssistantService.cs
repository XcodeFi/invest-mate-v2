namespace InvestmentApp.Application.Common.Interfaces;

public interface IAiAssistantService
{
    // Streaming (API integration)
    IAsyncEnumerable<AiStreamChunk> ReviewJournalAsync(string userId, string? portfolioId, string? question, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> ReviewPortfolioAsync(string userId, string portfolioId, string? question, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> AdviseTradePlanAsync(string userId, string tradePlanId, string? question, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> ChatAsync(string userId, string message, List<AiChatMessage>? history, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> MonthlySummaryAsync(string userId, string portfolioId, int year, int month, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> EvaluateStockAsync(string userId, string symbol, string? question, CancellationToken ct = default);

    // Build context only (for copy-to-clipboard, no API key needed)
    Task<AiContextResult> BuildContextAsync(string useCase, string userId,
        string? portfolioId, string? tradePlanId, string? symbol, string? question,
        int? year, int? month, string? message, List<AiChatMessage>? history,
        CancellationToken ct = default);
}
