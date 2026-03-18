namespace InvestmentApp.Application.DailyRoutines.Dtos;

public class DailyRoutineDto
{
    public string Id { get; set; } = null!;
    public DateTime Date { get; set; }
    public string TemplateId { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public List<RoutineItemDto> Items { get; set; } = new();
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public decimal ProgressPercent { get; set; }
    public bool IsFullyCompleted { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class RoutineItemDto
{
    public int Index { get; set; }
    public string Label { get; set; } = null!;
    public string Group { get; set; } = null!;
    public string? Link { get; set; }
    public bool IsRequired { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }
    public string? Emoji { get; set; }
}

public class RoutineTemplateDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Emoji { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int EstimatedMinutes { get; set; }
    public bool IsOneTime { get; set; }
    public bool IsUrgent { get; set; }
    public List<RoutineItemTemplateDto> Items { get; set; } = new();
    public bool IsBuiltIn { get; set; }
    public bool IsSuggested { get; set; }
}

public class RoutineItemTemplateDto
{
    public int Index { get; set; }
    public string Label { get; set; } = null!;
    public string Group { get; set; } = null!;
    public string? Link { get; set; }
    public bool IsRequired { get; set; }
    public string? Emoji { get; set; }
}

public class RoutineHistoryDto
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public List<RoutineHistoryDayDto> Days { get; set; } = new();
}

public class RoutineHistoryDayDto
{
    public DateTime Date { get; set; }
    public string TemplateName { get; set; } = null!;
    public bool IsCompleted { get; set; }
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
}
