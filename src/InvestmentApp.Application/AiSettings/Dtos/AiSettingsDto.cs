namespace InvestmentApp.Application.AiSettings.Dtos;

public class AiSettingsDto
{
    public bool HasApiKey { get; set; }
    public string? MaskedApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-6-20250514";
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}
