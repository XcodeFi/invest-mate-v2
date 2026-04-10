using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

public interface ICampaignReviewService
{
    CampaignReviewData CalculateMetrics(TradePlan plan, IEnumerable<Trade> linkedTrades);
}
