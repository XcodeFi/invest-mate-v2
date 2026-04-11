using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Infrastructure.Services;

public class PositionSizingService : IPositionSizingService
{
    public Task<PositionSizingResult> CalculateAsync(PositionSizingRequest request, CancellationToken ct = default)
    {
        var result = new PositionSizingResult();
        var riskPerShare = Math.Abs(request.EntryPrice - request.StopLoss);

        // 1. Fixed Risk (always included)
        result.Models.Add(CalculateFixedRisk(request, riskPerShare));

        // 2. ATR-Based (needs ATR > 0)
        if (request.Atr.HasValue && request.Atr.Value > 0)
            result.Models.Add(CalculateAtrBased(request));

        // 3. Kelly Criterion (needs trade history)
        var kelly = CalculateKelly(request);
        if (kelly != null)
            result.Models.Add(kelly);

        // 4. Turtle Sizing (needs ATR > 0)
        if (request.Atr.HasValue && request.Atr.Value > 0)
            result.Models.Add(CalculateTurtle(request));

        // 5. Volatility-Adjusted (needs ATR%)
        if (request.AtrPercent.HasValue && request.AtrPercent.Value > 0)
            result.Models.Add(CalculateVolatilityAdjusted(request, riskPerShare));

        // Recommend: ATR-based if available, else fixed_risk
        result.RecommendedModel = result.Models.Any(m => m.Model == "atr_based")
            ? "atr_based"
            : "fixed_risk";

        return Task.FromResult(result);
    }

    // --- Fixed Risk: Size = (Balance × Risk%) / RiskPerShare ---
    private static SizingModelResult CalculateFixedRisk(PositionSizingRequest req, decimal riskPerShare)
    {
        var maxRisk = req.AccountBalance * (req.RiskPercent / 100m);
        var shares = riskPerShare > 0
            ? RoundShares(maxRisk / riskPerShare)
            : 100;

        return BuildResult("fixed_risk", "Cố định % rủi ro", req, shares, riskPerShare, null);
    }

    // --- ATR-Based: Size = (Balance × Risk%) / (N × ATR) ---
    private static SizingModelResult CalculateAtrBased(PositionSizingRequest req)
    {
        var atrStop = req.AtrMultiplier * req.Atr!.Value;
        var maxRisk = req.AccountBalance * (req.RiskPercent / 100m);
        var shares = atrStop > 0
            ? RoundShares(maxRisk / atrStop)
            : 100;

        return BuildResult("atr_based", "Theo ATR", req, shares, atrStop,
            $"Stop = ±{atrStop:N0}đ ({req.AtrMultiplier}×ATR)");
    }

    // --- Kelly Criterion: f* = (bp - q) / b, use Half-Kelly, cap at 25% ---
    private static SizingModelResult? CalculateKelly(PositionSizingRequest req)
    {
        if (!req.WinRate.HasValue || !req.AverageWin.HasValue || !req.AverageLoss.HasValue)
            return null;
        if (req.AverageLoss.Value == 0) return null;

        var p = req.WinRate.Value;         // probability of win
        var q = 1m - p;                     // probability of loss
        var b = req.AverageWin.Value / req.AverageLoss.Value; // payoff ratio

        var fullKelly = (b * p - q) / b;
        if (fullKelly <= 0) return null;   // no edge → don't show

        var halfKelly = fullKelly / 2m;
        // Cap at 25% of account
        halfKelly = Math.Min(halfKelly, 0.25m);

        var positionValue = req.AccountBalance * halfKelly;
        var shares = req.EntryPrice > 0
            ? RoundShares(positionValue / req.EntryPrice)
            : 100;

        var riskPerShare = Math.Abs(req.EntryPrice - req.StopLoss);

        return BuildResult("kelly", "Kelly Criterion", req, shares, riskPerShare,
            $"Half-Kelly: {halfKelly * 100:F1}% (Full: {fullKelly * 100:F1}%)");
    }

    // --- Turtle: 1 Unit = 1% Balance / ATR ---
    // Shows single entry unit. Trader can add up to 4 units manually per Turtle rules.
    private static SizingModelResult CalculateTurtle(PositionSizingRequest req)
    {
        var onePercent = req.AccountBalance * 0.01m;
        var unitShares = RoundShares(onePercent / req.Atr!.Value);

        // Cap to account balance
        var totalValue = unitShares * req.EntryPrice;
        if (totalValue > req.AccountBalance && req.EntryPrice > 0)
            unitShares = RoundShares(req.AccountBalance / req.EntryPrice);

        var riskPerShare = Math.Abs(req.EntryPrice - req.StopLoss);

        return BuildResult("turtle", "Turtle (1 unit)", req, unitShares, riskPerShare,
            $"1 unit = {unitShares:N0} cp, thêm tối đa 3 unit nếu lời");
    }

    // --- Volatility-Adjusted: scale fixed risk by ATR percentile ---
    // Reference ATR%: 2% = normal, <1.5% = low vol, >3% = high vol
    private static SizingModelResult CalculateVolatilityAdjusted(PositionSizingRequest req, decimal riskPerShare)
    {
        // Scale factor: inverse relationship with ATR%
        // Baseline: ATR% = 2% → scale = 1.0
        // Low vol: ATR% = 1% → scale = 1.5 (bigger position)
        // High vol: ATR% = 4% → scale = 0.5 (smaller position)
        var baselineAtrPct = 2m;
        var scaleFactor = req.AtrPercent!.Value > 0
            ? baselineAtrPct / req.AtrPercent.Value
            : 1m;

        // Clamp scale factor between 0.5 and 1.5
        scaleFactor = Math.Clamp(scaleFactor, 0.5m, 1.5m);

        var maxRisk = req.AccountBalance * (req.RiskPercent / 100m) * scaleFactor;
        var shares = riskPerShare > 0
            ? RoundShares(maxRisk / riskPerShare)
            : 100;

        return BuildResult("volatility_adjusted", "Điều chỉnh biến động", req, shares, riskPerShare,
            $"Hệ số: {scaleFactor:F2}x (ATR% = {req.AtrPercent:F1}%)");
    }

    // --- Helpers ---

    private static int RoundShares(decimal raw)
    {
        var rounded = (int)(Math.Floor(raw / 100m) * 100);
        return Math.Max(rounded, 100);
    }

    private static SizingModelResult BuildResult(
        string model, string modelVi, PositionSizingRequest req,
        int shares, decimal riskPerShare, string? note)
    {
        var positionValue = shares * req.EntryPrice;
        var positionPercent = req.AccountBalance > 0
            ? positionValue / req.AccountBalance * 100m
            : 0m;
        var riskAmount = shares * riskPerShare;
        var withinLimit = positionPercent <= req.MaxPositionPercent;

        return new SizingModelResult
        {
            Model = model,
            ModelVi = modelVi,
            Shares = shares,
            PositionValue = positionValue,
            PositionPercent = Math.Round(positionPercent, 1),
            RiskAmount = riskAmount,
            WithinLimit = withinLimit,
            Note = note
        };
    }
}
