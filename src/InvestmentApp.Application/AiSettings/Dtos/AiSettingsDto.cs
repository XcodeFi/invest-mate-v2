namespace InvestmentApp.Application.AiSettings.Dtos;

public class AiSettingsDto
{
    public string Provider { get; set; } = "claude";
    public bool HasClaudeApiKey { get; set; }
    public string? MaskedClaudeApiKey { get; set; }
    public bool HasGeminiApiKey { get; set; }
    public string? MaskedGeminiApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-6-20250514";
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}
