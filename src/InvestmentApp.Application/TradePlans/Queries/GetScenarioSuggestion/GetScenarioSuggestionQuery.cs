using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetScenarioSuggestion;

public class GetScenarioSuggestionQuery : IRequest<ScenarioSuggestionDto>
{
    public string Symbol { get; set; } = null!;
    public decimal EntryPrice { get; set; }
    public TimeHorizon TimeHorizon { get; set; } = TimeHorizon.Medium;
    public string UserId { get; set; } = null!;
}

public class GetScenarioSuggestionQueryHandler : IRequestHandler<GetScenarioSuggestionQuery, ScenarioSuggestionDto>
{
    private readonly IScenarioConsultantService _consultantService;

    public GetScenarioSuggestionQueryHandler(IScenarioConsultantService consultantService)
    {
        _consultantService = consultantService;
    }

    public async Task<ScenarioSuggestionDto> Handle(
        GetScenarioSuggestionQuery request, CancellationToken cancellationToken)
    {
        var suggestion = await _consultantService.SuggestAsync(
            request.Symbol.ToUpper().Trim(),
            request.EntryPrice,
            request.TimeHorizon,
            cancellationToken);

        return MapToDto(suggestion);
    }

    private static ScenarioSuggestionDto MapToDto(ScenarioSuggestion s) => new()
    {
        Symbol = s.Symbol,
        EntryPrice = s.EntryPrice,
        TimeHorizon = s.TimeHorizon.ToString(),
        TechnicalBasis = new TechnicalBasisDto
        {
            Ema20 = s.TechnicalBasis.Ema20,
            Ema50 = s.TechnicalBasis.Ema50,
            Ema200 = s.TechnicalBasis.Ema200,
            Rsi14 = s.TechnicalBasis.Rsi14,
            BollingerUpper = s.TechnicalBasis.BollingerUpper,
            BollingerLower = s.TechnicalBasis.BollingerLower,
            SupportLevels = s.TechnicalBasis.SupportLevels,
            ResistanceLevels = s.TechnicalBasis.ResistanceLevels,
            Fibonacci = s.TechnicalBasis.Fibonacci != null ? new FibonacciLevelsDto
            {
                SwingHigh = s.TechnicalBasis.Fibonacci.SwingHigh,
                SwingLow = s.TechnicalBasis.Fibonacci.SwingLow,
                Retracement236 = s.TechnicalBasis.Fibonacci.Retracement236,
                Retracement382 = s.TechnicalBasis.Fibonacci.Retracement382,
                Retracement500 = s.TechnicalBasis.Fibonacci.Retracement500,
                Retracement618 = s.TechnicalBasis.Fibonacci.Retracement618,
                Retracement786 = s.TechnicalBasis.Fibonacci.Retracement786,
                Extension1272  = s.TechnicalBasis.Fibonacci.Extension1272,
                Extension1618  = s.TechnicalBasis.Fibonacci.Extension1618
            } : null,
            Atr14 = s.TechnicalBasis.Atr14
        },
        Nodes = s.Nodes.Select(n => new SuggestedNodeDto
        {
            NodeId = n.NodeId,
            ParentId = n.ParentId,
            Order = n.Order,
            Label = n.Label,
            ConditionType = n.ConditionType,
            ConditionValue = n.ConditionValue,
            ActionType = n.ActionType,
            ActionValue = n.ActionValue,
            Reasoning = n.Reasoning,
            Category = n.Category
        }).ToList()
    };
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class ScenarioSuggestionDto
{
    public string Symbol { get; set; } = null!;
    public decimal EntryPrice { get; set; }
    public string TimeHorizon { get; set; } = "Medium";
    public TechnicalBasisDto TechnicalBasis { get; set; } = new();
    public List<SuggestedNodeDto> Nodes { get; set; } = new();
}

public class TechnicalBasisDto
{
    public decimal? Ema20 { get; set; }
    public decimal? Ema50 { get; set; }
    public decimal? Ema200 { get; set; }
    public decimal? Rsi14 { get; set; }
    public decimal? BollingerUpper { get; set; }
    public decimal? BollingerLower { get; set; }
    public List<decimal> SupportLevels { get; set; } = new();
    public List<decimal> ResistanceLevels { get; set; } = new();
    public FibonacciLevelsDto? Fibonacci { get; set; }
    public decimal? Atr14 { get; set; }
}

public class FibonacciLevelsDto
{
    public decimal SwingHigh { get; set; }
    public decimal SwingLow { get; set; }
    public decimal Retracement236 { get; set; }
    public decimal Retracement382 { get; set; }
    public decimal Retracement500 { get; set; }
    public decimal Retracement618 { get; set; }
    public decimal Retracement786 { get; set; }
    public decimal Extension1272 { get; set; }
    public decimal Extension1618 { get; set; }
}

public class SuggestedNodeDto
{
    public string NodeId { get; set; } = null!;
    public string? ParentId { get; set; }
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ConditionType { get; set; } = "PriceAbove";
    public decimal? ConditionValue { get; set; }
    public string ActionType { get; set; } = "SellPercent";
    public decimal? ActionValue { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
