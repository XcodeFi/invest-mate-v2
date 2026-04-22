using System.Globalization;
using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.PersonalFinance.Queries.GetNetWorthSummary;

public class GetNetWorthSummaryQuery : IRequest<NetWorthSummaryDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class GetNetWorthSummaryQueryHandler : IRequestHandler<GetNetWorthSummaryQuery, NetWorthSummaryDto>
{
    private readonly IFinancialProfileRepository _profileRepo;
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IPnLService _pnlService;

    public GetNetWorthSummaryQueryHandler(
        IFinancialProfileRepository profileRepo,
        IPortfolioRepository portfolioRepo,
        IPnLService pnlService)
    {
        _profileRepo = profileRepo;
        _portfolioRepo = portfolioRepo;
        _pnlService = pnlService;
    }

    public async Task<NetWorthSummaryDto> Handle(GetNetWorthSummaryQuery request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        if (profile is null)
            return new NetWorthSummaryDto(); // Empty — FE sẽ show prompt "Thiết lập profile"

        var securitiesValue = await SumSecuritiesValueAsync(request.UserId, cancellationToken);

        var totalAssets = profile.GetTotalAssets(securitiesValue);
        var healthScore = profile.CalculateHealthScore(securitiesValue);

        var goldTotal = SumByType(profile, FinancialAccountType.Gold);
        var savingsTotal = SumByType(profile, FinancialAccountType.Savings);
        var emergencyTotal = SumByType(profile, FinancialAccountType.Emergency);
        var idleCashTotal = SumByType(profile, FinancialAccountType.IdleCash);

        return new NetWorthSummaryDto
        {
            TotalAssets = totalAssets,
            SecuritiesValue = securitiesValue,
            GoldTotal = goldTotal,
            SavingsTotal = savingsTotal,
            EmergencyTotal = emergencyTotal,
            IdleCashTotal = idleCashTotal,
            MonthlyExpense = profile.MonthlyExpense,
            HealthScore = healthScore,
            RuleChecks = BuildRuleChecks(profile, totalAssets, securitiesValue, goldTotal, savingsTotal, emergencyTotal),
            Accounts = profile.Accounts.Select(PersonalFinanceMapper.ToDto).ToList(),
        };
    }

    // N+1 intentional — solo-user app, typically ≤5 portfolios. Batch if multi-user added.
    private async Task<decimal> SumSecuritiesValueAsync(string userId, CancellationToken ct)
    {
        var portfolios = await _portfolioRepo.GetByUserIdAsync(userId, ct);
        decimal total = 0m;
        foreach (var p in portfolios)
        {
            var pnl = await _pnlService.CalculatePortfolioPnLAsync(p.Id, ct);
            total += pnl.TotalMarketValue;
        }
        return total;
    }

    private static decimal SumByType(FinancialProfile profile, FinancialAccountType type) =>
        profile.Accounts.Where(a => a.Type == type).Sum(a => a.Balance);

    private static List<RuleCheckResultDto> BuildRuleChecks(
        FinancialProfile profile,
        decimal totalAssets,
        decimal securitiesValue,
        decimal goldTotal,
        decimal savingsTotal,
        decimal emergencyTotal)
    {
        var requiredEmergency = profile.MonthlyExpense * profile.Rules.EmergencyFundMonths;
        var maxInvestment = totalAssets * (profile.Rules.MaxInvestmentPercent / 100m);
        var requiredSavings = totalAssets * (profile.Rules.MinSavingsPercent / 100m);
        var investmentTotal = securitiesValue + goldTotal;

        // Invariant culture để tránh "50,5%" trên server VN — FE sẽ format lại theo locale nếu cần.
        var inv = CultureInfo.InvariantCulture;

        return new List<RuleCheckResultDto>
        {
            new()
            {
                RuleName = "EmergencyFund",
                IsPassing = emergencyTotal >= requiredEmergency,
                Description = $"Quỹ dự phòng ≥ {profile.Rules.EmergencyFundMonths.ToString(inv)} tháng chi tiêu",
                CurrentValue = emergencyTotal,
                RequiredValue = requiredEmergency,
            },
            new()
            {
                RuleName = "InvestmentCap",
                IsPassing = totalAssets <= 0m || investmentTotal <= maxInvestment,
                Description = $"Đầu tư (CK + Vàng) ≤ {profile.Rules.MaxInvestmentPercent.ToString("G", inv)}% tổng tài sản",
                CurrentValue = investmentTotal,
                RequiredValue = maxInvestment,
            },
            new()
            {
                RuleName = "SavingsFloor",
                IsPassing = totalAssets <= 0m || savingsTotal >= requiredSavings,
                Description = $"Tiết kiệm ≥ {profile.Rules.MinSavingsPercent.ToString("G", inv)}% tổng tài sản",
                CurrentValue = savingsTotal,
                RequiredValue = requiredSavings,
            },
        };
    }
}
