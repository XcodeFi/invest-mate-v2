namespace InvestmentApp.Application.Common.Interfaces;

public interface IAiChatService
{
    IAsyncEnumerable<AiStreamChunk> StreamChatAsync(
        string apiKey,
        string model,
        string systemPrompt,
        List<AiChatMessage> messages,
        CancellationToken ct = default);
}

public class AiChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class AiStreamChunk
{
    public string Type { get; set; } = string.Empty;       // "text", "usage", "error"
    public string? Text { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public string? ErrorMessage { get; set; }
}
