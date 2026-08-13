using System.Text.Json.Serialization;
using FluentValidation;
using InvestmentApp.Application.JournalEntries.Commands;
using MediatR;

namespace InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;

public class UpdateJournalEntryCommand : IRequest<bool>
{
    [JsonIgnore]
    public string? UserId { get; set; }
    [JsonIgnore]
    public string Id { get; set; } = null!;
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? EntryType { get; set; }
    public string? EmotionalState { get; set; }
    public int? ConfidenceLevel { get; set; }
    public string? MarketContext { get; set; }
    public List<string>? Tags { get; set; }
    public int? Rating { get; set; }
}

public class UpdateJournalEntryCommandValidator : AbstractValidator<UpdateJournalEntryCommand>
{
    public UpdateJournalEntryCommandValidator()
    {
        // null = giữ nguyên loại cũ, nên chỉ chấm khi có gửi giá trị.
        RuleFor(x => x.EntryType)
            .Must(JournalEntryTypeRule.IsAllowed)
            .When(x => x.EntryType != null)
            .WithMessage(JournalEntryTypeRule.Message);
    }
}
