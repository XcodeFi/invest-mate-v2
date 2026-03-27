using MediatR;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Journals.Queries.GetTradesPendingReview;

public class GetTradesPendingReviewQuery : IRequest<List<PendingReviewTradeDto>>
{
    public string UserId { get; set; } = null!;
    public string? PortfolioId { get; set; }
}

public class PendingReviewTradeDto
{
    public string TradeId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string PortfolioId { get; set; } = null!;
    public string PortfolioName { get; set; } = null!;
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public DateTime TradeDate { get; set; }
}

public class GetTradesPendingReviewQueryHandler
    : IRequestHandler<GetTradesPendingReviewQuery, List<PendingReviewTradeDto>>
{
    private readonly ITradeRepository _tradeRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IJournalEntryRepository _journalEntryRepo;

    public GetTradesPendingReviewQueryHandler(
        ITradeRepository tradeRepo,
        IPortfolioRepository portfolioRepo,
        IJournalEntryRepository journalEntryRepo)
    {
        _tradeRepo = tradeRepo;
        _portfolioRepo = portfolioRepo;
        _journalEntryRepo = journalEntryRepo;
    }

    public async Task<List<PendingReviewTradeDto>> Handle(
        GetTradesPendingReviewQuery request, CancellationToken cancellationToken)
    {
        // 1. Get user's portfolios
        var portfolios = (await _portfolioRepo.GetByUserIdAsync(request.UserId, cancellationToken)).ToList();
        if (request.PortfolioId != null)
            portfolios = portfolios.Where(p => p.Id == request.PortfolioId).ToList();

        var portfolioMap = portfolios.ToDictionary(p => p.Id, p => p.Name);

        // 2. Get all SELL trades across portfolios
        var sellTrades = new List<Trade>();
        foreach (var portfolio in portfolios)
        {
            var trades = await _tradeRepo.GetByPortfolioIdAsync(portfolio.Id, cancellationToken);
            sellTrades.AddRange(trades.Where(t => t.TradeType == TradeType.SELL));
        }

        if (sellTrades.Count == 0)
            return new List<PendingReviewTradeDto>();

        // 3. Get all PostTrade journal entries for this user
        var journalEntries = await _journalEntryRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        var reviewedTradeIds = journalEntries
            .Where(j => j.EntryType == JournalEntryType.PostTrade && !string.IsNullOrEmpty(j.TradeId))
            .Select(j => j.TradeId!)
            .ToHashSet();

        // 4. Cross-reference: trades without PostTrade journal
        return sellTrades
            .Where(t => !reviewedTradeIds.Contains(t.Id))
            .OrderByDescending(t => t.TradeDate)
            .Select(t => new PendingReviewTradeDto
            {
                TradeId = t.Id,
                Symbol = t.Symbol,
                PortfolioId = t.PortfolioId,
                PortfolioName = portfolioMap.GetValueOrDefault(t.PortfolioId, ""),
                Price = t.Price,
                Quantity = t.Quantity,
                TradeDate = t.TradeDate
            })
            .ToList();
    }
}
