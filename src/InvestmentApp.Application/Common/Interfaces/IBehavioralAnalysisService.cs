using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

public interface IBehavioralAnalysisService
{
    List<BehavioralPatternDto> DetectPatterns(
        IEnumerable<JournalEntry> journalEntries,
        IEnumerable<Trade> trades);
}

public class BehavioralPatternDto
{
    public string PatternType { get; set; } = null!;   // "FOMO", "PanicSell", "RevengeTrading", "Overtrading"
    public string Severity { get; set; } = null!;       // "Warning", "Critical", "Info"
    public string Description { get; set; } = null!;
    public DateTime OccurredAt { get; set; }
    public string? RelatedTradeId { get; set; }
    public string? RelatedJournalId { get; set; }
}
