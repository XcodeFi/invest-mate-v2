using FluentAssertions;
using FluentValidation.TestHelper;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;

namespace InvestmentApp.Application.Tests.TradePlans;

/// <summary>
/// Validator gates for POST /api/v1/trade-plans (CreateTradePlan command).
///
/// **Bug context:** Before fix, BE accepted plans with InvalidationCriteria having
/// empty/short Detail and stopLoss=0 (returned 201 Created). Persisted useless rules
/// that could never trigger. This test suite locks the validator gates.
/// </summary>
public class CreateTradePlanCommandValidatorTests
{
    private readonly CreateTradePlanCommandValidator _validator = new();

    private static CreateTradePlanCommand ValidBaseCommand() => new()
    {
        UserId = "user-1",
        Symbol = "VIC",
        Direction = "Buy",
        EntryPrice = 100_000m,
        StopLoss = 95_000m,
        Target = 120_000m,
        Quantity = 100,
        Thesis = "Tăng trưởng dài hạn ngành bán lẻ Việt Nam"
    };

    [Fact]
    public void ValidBaseCommand_Passes()
    {
        var result = _validator.TestValidate(ValidBaseCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // -----------------------------------------------------------------
    // StopLoss > 0
    // -----------------------------------------------------------------
    [Fact]
    public void StopLossZero_FailsValidation()
    {
        // Repro from user bug: stopLoss=0 caused "Risk API error: lt" silent fail later.
        var cmd = ValidBaseCommand();
        cmd.StopLoss = 0m;

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(c => c.StopLoss);
    }

    [Fact]
    public void StopLossNegative_FailsValidation()
    {
        var cmd = ValidBaseCommand();
        cmd.StopLoss = -100m;

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(c => c.StopLoss);
    }

    // -----------------------------------------------------------------
    // InvalidationCriteria — Detail ≥ 20 chars after Trim
    // -----------------------------------------------------------------
    [Fact]
    public void InvalidationCriteria_EmptyDetail_FailsValidation()
    {
        // Exact repro: FE sent {trigger:"EarningsMiss", detail:"", checkDate:null}
        var cmd = ValidBaseCommand();
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
        var cmd = ValidBaseCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>
        {
            new() { Trigger = "EarningsMiss", Detail = "EPS giảm" }   // 9 chars
        };

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor("InvalidationCriteria[0].Detail");
    }

    [Fact]
    public void InvalidationCriteria_WhitespaceOnlyPadded_FailsValidation()
    {
        // 25 spaces around 5 chars → 5 chars after Trim → < 20.
        var cmd = ValidBaseCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>
        {
            new() { Trigger = "EarningsMiss", Detail = "                    abcde                    " }
        };

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor("InvalidationCriteria[0].Detail");
    }

    [Fact]
    public void InvalidationCriteria_DetailExactly20Chars_Passes()
    {
        var cmd = ValidBaseCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>
        {
            new() { Trigger = "EarningsMiss", Detail = new string('a', 20) }
        };

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor("InvalidationCriteria[0].Detail");
    }

    [Fact]
    public void InvalidationCriteria_UnknownTrigger_FailsValidation()
    {
        var cmd = ValidBaseCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>
        {
            new() { Trigger = "BogusTrigger", Detail = new string('x', 25) }
        };

        var result = _validator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor("InvalidationCriteria[0].Trigger");
    }

    [Fact]
    public void InvalidationCriteria_NullList_Passes()
    {
        // Plan can have no invalidation rules at all (legacy/exempt). Validator only fires
        // when the list is non-empty.
        var cmd = ValidBaseCommand();
        cmd.InvalidationCriteria = null;

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor(c => c.InvalidationCriteria!);
    }

    [Fact]
    public void InvalidationCriteria_EmptyList_Passes()
    {
        var cmd = ValidBaseCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>();

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void InvalidationCriteria_MultipleRules_OneInvalid_FlagsThatRule()
    {
        var cmd = ValidBaseCommand();
        cmd.InvalidationCriteria = new List<InvalidationRuleDto>
        {
            new() { Trigger = "EarningsMiss", Detail = "BCTC Q1/2026 EPS giảm > 20% YoY" },  // OK
            new() { Trigger = "TrendBreak", Detail = "" }                                       // BAD
        };

        var result = _validator.TestValidate(cmd);

        result.ShouldNotHaveValidationErrorFor("InvalidationCriteria[0].Detail");
        result.ShouldHaveValidationErrorFor("InvalidationCriteria[1].Detail");
    }
}
