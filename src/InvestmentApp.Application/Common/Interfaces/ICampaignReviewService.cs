using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

public interface ICampaignReviewService
{
    /// <param name="corporateActions">Sự kiện quyền của mã trong các danh mục có giao dịch của kế hoạch.</param>
    CampaignReviewData CalculateMetrics(TradePlan plan, IEnumerable<Trade> linkedTrades, IEnumerable<CorporateAction> corporateActions);
}
