using FluentAssertions;
using FluentValidation.TestHelper;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using InvestmentApp.Application.TradePlans.Commands.UpdateTradePlan;

namespace InvestmentApp.Application.Tests.TradePlans;

/// <summary>
/// Validator gates for PUT /api/v1/trade-plans/{id} (UpdateTradePlan command).
///
/// **Bug context:** Same root cause as CreateTradePlan — empty/short InvalidationCriteria.Detail
/// + stopLoss=0 sneaked through. PUT was returning 204 No Content while persisting useless rules.
///
/// Update validator differs from Create in nullability: most fields are optional (only update
/// what client sends). InvalidationCriteria semantics: when client sends a list, every rule
/// in it must be valid. When client doesn't send the field (null), no validation fires.
/// </summary>
public class UpdateTradePlanCommandValidatorTests
{
    private readonly UpdateTradePlanCommandValidator _validator = new();

    private static UpdateTradePlanCommand MinimalCommand() => new()
    {
        Id = "plan-1",
        UserId = "user-1"
    };

    [Fact]
    public void EmptyUpdate_Passes()
    {
        // Most update commands only touch a subset of fields.
        var result = _validator.TestValidate(MinimalCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void StopLossZero_FailsValidation()
    {
        var cmd = MinimalCommand();
        cmd.StopLoss = 0m;

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(c => c.StopLoss);
    }

    [Fact]
    public void StopLossNegative_FailsValidation()
    {
        var cmd = MinimalCommand();
        cmd.StopLoss = -100m;

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(c => c.StopLoss);
    }

    [Fact]
    public void StopLossNull_Passes()
    {
        // Update can leave stopLoss alone.
        var cmd = MinimalCommand();
        cmd.StopLoss = null;

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(c => c.StopLoss!);
    }

    [Fact]
    public void InvalidationCriteria_EmptyDetail_FailsValidation()
    {
        var cmd = MinimalCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>
        {
            new() { Trigger = "EarningsMiss", Detail = "", CheckDate = null }
        };

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor("InvalidationCriteria[0].Detail");
    }

    [Fact]
    public void InvalidationCriteria_DetailShorterThan20Chars_FailsValidation()
    {
        var cmd = MinimalCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>
        {
            new() { Trigger = "EarningsMiss", Detail = "ngắn" }
        };

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor("InvalidationCriteria[0].Detail");
    }

    [Fact]
    public void InvalidationCriteria_DetailExactly20Chars_Passes()
    {
        var cmd = MinimalCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>
        {
            new() { Trigger = "EarningsMiss", Detail = new string('a', 20) }
        };

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor("InvalidationCriteria[0].Detail");
    }

    [Fact]
    public void InvalidationCriteria_NullList_Passes()
    {
        var cmd = MinimalCommand();
        cmd.InvalidationCriteria = null;

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
