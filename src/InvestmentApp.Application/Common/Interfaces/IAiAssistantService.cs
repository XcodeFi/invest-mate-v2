namespace InvestmentApp.Application.Common.Interfaces;

public interface IAiAssistantService
{
    IAsyncEnumerable<AiStreamChunk> ReviewJournalAsync(string userId, string? portfolioId, string? question, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> ReviewPortfolioAsync(string userId, string portfolioId, string? question, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> AdviseTradePlanAsync(string userId, string tradePlanId, string? question, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> ChatAsync(string userId, string message, List<AiChatMessage>? history, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> MonthlySummaryAsync(string userId, string portfolioId, int year, int month, CancellationToken ct = default);
}
