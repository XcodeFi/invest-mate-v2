using System.Reflection;
using System.Text.Json.Serialization;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.Trades.Commands.CreateTrade;

namespace InvestmentApp.Api.Tests.Docs;

/// <summary>
/// Chống drift: mọi field public (client gửi) của các command mà agent expose phải được nhắc trong
/// tài liệu. Thêm field mới mà quên cập nhật doc → test đỏ.
/// </summary>
public class AgentDocDriftTests
{
    public static IEnumerable<object[]> DocumentedFields()
    {
        foreach (var t in new[] { typeof(CreateTradePlanCommand), typeof(CreateTradeCommand) })
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue; // UserId/Origin/Id... server-set
                if (p.GetCustomAttribute<ObsoleteAttribute>() != null) continue;    // Reason shim (deprecated)
                yield return new object[] { p.Name };
            }
    }

    [Theory]
    [MemberData(nameof(DocumentedFields))]
    public void EveryCommandField_IsMentionedInDoc(string fieldName)
    {
        var doc = AiAgentController.LoadDoc();
        var camel = char.ToLowerInvariant(fieldName[0]) + fieldName[1..];
        doc.Should().MatchRegex($"(?i)\\b{camel}\\b",
            $"field '{camel}' phải xuất hiện trong AI-Agent-TradePlan-API.md (vừa thêm field? cập nhật doc)");
    }
}
