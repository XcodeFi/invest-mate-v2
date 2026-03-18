namespace InvestmentApp.Domain.ValueObjects;

public class RoutineItemTemplate
{
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty; // "Sáng" | "Trong phiên" | "Cuối ngày"
    public string? Link { get; set; }
    public bool IsRequired { get; set; }
    public string? Emoji { get; set; }
}
