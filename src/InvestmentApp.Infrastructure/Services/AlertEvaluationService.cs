using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Infrastructure.Services;

public class AlertEvaluationService : IAlertEvaluationService
{
    private readonly IAlertRuleRepository _alertRuleRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IPortfolioSnapshotRepository _snapshotRepository;
    private readonly IStockPriceRepository _stockPriceRepository;
    private readonly ITradeRepository _tradeRepository;

    public AlertEvaluationService(
        IAlertRuleRepository alertRuleRepository,
        IPortfolioRepository portfolioRepository,
        IPortfolioSnapshotRepository snapshotRepository,
        IStockPriceRepository stockPriceRepository,
        ITradeRepository tradeRepository)
    {
        _alertRuleRepository = alertRuleRepository;
        _portfolioRepository = portfolioRepository;
        _snapshotRepository = snapshotRepository;
        _stockPriceRepository = stockPriceRepository;
        _tradeRepository = tradeRepository;
    }

    public async Task<IEnumerable<AlertEvaluationResult>> EvaluateRulesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rules = await _alertRuleRepository.GetActiveByUserIdAsync(userId, cancellationToken);
        var results = new List<AlertEvaluationResult>();

        foreach (var rule in rules)
        {
            var result = rule.AlertType switch
            {
                "PriceAlert" => await EvaluatePriceAlert(rule, cancellationToken),
                "DrawdownAlert" => await EvaluateDrawdownAlert(rule, cancellationToken),
                "PortfolioValue" => await EvaluatePortfolioValueAlert(rule, cancellationToken),
                _ => new AlertEvaluationResult
                {
                    AlertRuleId = rule.Id,
                    AlertType = rule.AlertType,
                    IsTriggered = false
                }
            };

            if (result.IsTriggered)
                results.Add(result);
        }

        return results;
    }

    private async Task<AlertEvaluationResult> EvaluatePriceAlert(AlertRule rule, CancellationToken cancellationToken)
    {
        var result = new AlertEvaluationResult
        {
            AlertRuleId = rule.Id,
            AlertType = rule.AlertType,
            Symbol = rule.Symbol,
            ThresholdValue = rule.Threshold,
            IsTriggered = false
        };

        if (string.IsNullOrEmpty(rule.Symbol)) return result;

        var prices = await _stockPriceRepository.GetLatestPricesAsync(new[] { rule.Symbol }, cancellationToken);
        var latestPrice = prices.FirstOrDefault();
        if (latestPrice == null) return result;

        result.CurrentValue = latestPrice.Close;

        bool triggered = rule.Condition switch
        {
            "Above" => latestPrice.Close >= rule.Threshold,
            "Below" => latestPrice.Close <= rule.Threshold,
            _ => false
        };

        if (triggered)
        {
            result.IsTriggered = true;
            result.Title = $"Giá {rule.Symbol} {(rule.Condition == "Above" ? "vượt" : "dưới")} ngưỡng";
            result.Message = $"{rule.Symbol}: {latestPrice.Close:N0} VNĐ ({rule.Condition} {rule.Threshold:N0} VNĐ)";
        }

        return result;
    }

    private async Task<AlertEvaluationResult> EvaluateDrawdownAlert(AlertRule rule, CancellationToken cancellationToken)
    {
        var result = new AlertEvaluationResult
        {
            AlertRuleId = rule.Id,
            AlertType = rule.AlertType,
            PortfolioId = rule.PortfolioId,
            ThresholdValue = rule.Threshold,
            IsTriggered = false
        };

        if (string.IsNullOrEmpty(rule.PortfolioId)) return result;

        var snapshots = await _snapshotRepository.GetByPortfolioIdAsync(
            rule.PortfolioId, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow, cancellationToken);

        var snapshotList = snapshots.ToList();
        if (snapshotList.Count < 2) return result;

        decimal peak = snapshotList.Max(s => s.TotalValue);
        decimal current = snapshotList.Last().TotalValue;
        decimal drawdown = peak > 0 ? (peak - current) / peak * 100 : 0;

        result.CurrentValue = drawdown;

        if (drawdown >= rule.Threshold)
        {
            result.IsTriggered = true;
            result.Title = $"Drawdown vượt ngưỡng {rule.Threshold}%";
            result.Message = $"Drawdown hiện tại: {drawdown:F2}% (Ngưỡng: {rule.Threshold}%)";
        }

        return result;
    }

    private async Task<AlertEvaluationResult> EvaluatePortfolioValueAlert(AlertRule rule, CancellationToken cancellationToken)
    {
        var result = new AlertEvaluationResult
        {
            AlertRuleId = rule.Id,
            AlertType = rule.AlertType,
            PortfolioId = rule.PortfolioId,
            ThresholdValue = rule.Threshold,
            IsTriggered = false
        };

        if (string.IsNullOrEmpty(rule.PortfolioId)) return result;

        var latest = await _snapshotRepository.GetLatestByPortfolioIdAsync(rule.PortfolioId, cancellationToken);
        if (latest == null) return result;

        result.CurrentValue = latest.TotalValue;

        bool triggered = rule.Condition switch
        {
            "Above" => latest.TotalValue >= rule.Threshold,
            "Below" => latest.TotalValue <= rule.Threshold,
            "Exceeds" => latest.TotalValue >= rule.Threshold,
            _ => false
        };

        if (triggered)
        {
            result.IsTriggered = true;
            result.Title = $"Giá trị danh mục {(rule.Condition == "Below" ? "dưới" : "vượt")} ngưỡng";
            result.Message = $"Giá trị hiện tại: {latest.TotalValue:N0} VNĐ (Ngưỡng: {rule.Threshold:N0} VNĐ)";
        }

        return result;
    }
}
