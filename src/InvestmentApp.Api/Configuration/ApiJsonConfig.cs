using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvestmentApp.Api.Configuration;

/// <summary>
/// Single source of truth for the API's JSON serializer options.
///
/// <para>
/// Keeps Program.cs and DecisionsControllerJsonTests in sync — when this changes, both
/// runtime and tests pick it up. Drift between them caused PR-3 NRE bug
/// (FE sent <c>"Action":"HoldWithJournal"</c>, BE deserialized to null because
/// <see cref="JsonStringEnumConverter"/> wasn't registered).
/// </para>
/// </summary>
public static class ApiJsonConfig
{
    /// <summary>
    /// Apply API JSON conventions to the given options. Call from
    /// <c>AddControllers().AddJsonOptions(o => ApiJsonConfig.Configure(o.JsonSerializerOptions))</c>.
    /// </summary>
    public static void Configure(JsonSerializerOptions options)
    {
        // Enums round-trip as strings (e.g. "HoldWithJournal" not 1). FE typescript declares
        // enums as string literal unions ('HoldWithJournal' | 'ExecuteSell'), so int form would
        // silently break enum comparisons. Default allowIntegerValues=true preserves backwards-
        // compat for any existing FE caller that sends integers.
        options.Converters.Add(new JsonStringEnumConverter());
    }
}
