using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Journals.Commands.CreateJournal;

public class CreateJournalCommand : IRequest<string>
{
    public string TradeId { get; set; } = null!;
    [JsonIgnore]
    public string UserId { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public string EntryReason { get; set; } = string.Empty;
    public string MarketContext { get; set; } = string.Empty;
    public string TechnicalSetup { get; set; } = string.Empty;
    public string EmotionalState { get; set; } = string.Empty;
    public int ConfidenceLevel { get; set; } = 5;
    public string? TradePlanId { get; set; }
}

public class CreateJournalCommandHandler : IRequestHandler<CreateJournalCommand, string>
{
    private readonly ITradeJournalRepository _journalRepository;
    private readonly ITradeRepository _tradeRepository;

    public CreateJournalCommandHandler(ITradeJournalRepository journalRepository, ITradeRepository tradeRepository)
    {
        _journalRepository = journalRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<string> Handle(CreateJournalCommand request, CancellationToken cancellationToken)
    {
        // Verify trade exists
        var trade = await _tradeRepository.GetByIdAsync(request.TradeId, cancellationToken)
            ?? throw new Exception($"Trade {request.TradeId} not found");

        // Check if journal already exists for this trade
        var existing = await _journalRepository.GetByTradeIdAsync(request.TradeId, cancellationToken);
        if (existing != null)
            throw new Exception($"Journal already exists for trade {request.TradeId}");

        var journal = new TradeJournal(
            request.TradeId, request.UserId, request.PortfolioId,
            request.EntryReason, request.MarketContext, request.TechnicalSetup,
            request.EmotionalState, request.ConfidenceLevel
        );

        // Link trade plan if provided (or inherit from trade)
        var planId = request.TradePlanId ?? trade.TradePlanId;
        if (!string.IsNullOrEmpty(planId))
            journal.LinkTradePlan(planId);

        await _journalRepository.AddAsync(journal, cancellationToken);
        return journal.Id;
    }
}
