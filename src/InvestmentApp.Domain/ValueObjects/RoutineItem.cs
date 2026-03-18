namespace InvestmentApp.Domain.ValueObjects;

public class RoutineItem
{
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty; // "Sáng" | "Trong phiên" | "Cuối ngày"
    public string? Link { get; set; }
    public bool IsRequired { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }
    public string? Emoji { get; set; }
}
