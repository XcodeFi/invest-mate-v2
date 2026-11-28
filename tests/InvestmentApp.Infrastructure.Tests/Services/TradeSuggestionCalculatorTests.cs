using FluentAssertions;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Cắt lỗ của gợi ý giao dịch cũ = max(hỗ trợ thứ 2, vào lệnh − 2 × ATR14): lấy mức CAO hơn (chặt hơn), nhưng không bao giờ xa hơn 2 ATR
/// dưới giá vào. Không có ATR thì giữ luật cũ. Cắt lỗ phải > 0 và < giá vào, nếu không thì không gợi ý.
/// </summary>
public class TradeSuggestionCalculatorTests
{
    private static readonly decimal[] Resistance = { 110m };

    [Fact]
    public void stop_uses_second_support_when_tighter_than_2atr()
    {
        // 2 ATR dưới 100 = 90; hỗ trợ 2 = 95 chặt hơn
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 95m }, Resistance, atr14: 5m)!;

        s.StopLoss.Should().Be(95m);
        s.StopLossMethod.Should().Be("support2");
    }

    [Fact]
    public void stop_capped_at_entry_minus_2atr_when_second_support_far()
    {
        // hỗ trợ 2 = 80 xa hơn 2 ATR (90) → bị kéo lại 90
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 80m }, Resistance, atr14: 5m)!;

        s.StopLoss.Should().Be(90m);
        s.StopLossMethod.Should().Be("atr2x");
    }

    [Fact]
    public void when_second_support_equals_the_2atr_level_the_label_is_support2()
    {
        // 2 ATR dưới 100 = 90 và hỗ trợ 2 cũng = 90: cùng mức cắt lỗ, nhãn ghi hỗ trợ (mức có thật trên biểu đồ)
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 90m }, Resistance, atr14: 5m)!;

        s.StopLoss.Should().Be(90m);
        s.StopLossMethod.Should().Be("support2");
    }

    [Fact]
    public void second_support_within_1atr_clamped_to_entry_minus_1atr()
    {
        // Hỗ trợ 2 = 98 chỉ cách vào 2 (< 1 ATR = 5): cắt lỗ sát quá, R:R sẽ ảo. Kéo ra tối thiểu 1 ATR = 95.
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 98m }, Resistance, atr14: 5m)!;

        s.StopLoss.Should().Be(95m);
        s.StopLossMethod.Should().Be("atr1x");
        s.RiskReward.Should().Be(2.0m, "lãi 10 / rủi ro 5, không phải 10 / 2 = 5");
    }

    [Fact]
    public void second_support_exactly_1atr_below_entry_is_kept_and_labelled_support2()
    {
        // Biên: đúng 1 ATR thì không bị kéo, nhãn vẫn là hỗ trợ (chỉ khi bị kéo mới ghi atr1x)
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 95m }, Resistance, atr14: 5m)!;

        s.StopLoss.Should().Be(95m);
        s.StopLossMethod.Should().Be("support2");
    }

    [Fact]
    public void second_support_just_inside_1atr_is_clamped()
    {
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 95.5m }, Resistance, atr14: 5m)!;

        s.StopLoss.Should().Be(95m);
        s.StopLossMethod.Should().Be("atr1x");
    }

    [Fact]
    public void second_support_between_1_and_2atr_is_kept()
    {
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 92m }, Resistance, atr14: 5m)!;

        s.StopLoss.Should().Be(92m);
        s.StopLossMethod.Should().Be("support2");
    }

    [Fact]
    public void the_clamp_does_not_apply_without_atr()
    {
        // Không có ATR: luật cũ giữ nguyên, kể cả hỗ trợ 2 sát giá vào
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 99.9m }, Resistance, atr14: null)!;

        s.StopLoss.Should().Be(99.9m);
        s.StopLossMethod.Should().Be("support2");
    }

    [Fact]
    public void no_second_support_uses_2atr()
    {
        var s = TradeSuggestionCalculator.Build(new[] { 100m }, Resistance, atr14: 5m)!;

        s.StopLoss.Should().Be(90m);
        s.StopLossMethod.Should().Be("atr2x");
    }

    [Fact]
    public void atr_null_falls_back_to_old_rule()
    {
        var withSecond = TradeSuggestionCalculator.Build(new[] { 100m, 95m }, Resistance, atr14: null)!;
        var withoutSecond = TradeSuggestionCalculator.Build(new[] { 100m }, Resistance, atr14: null)!;

        withSecond.StopLoss.Should().Be(95m);
        withSecond.StopLossMethod.Should().Be("support2");
        withoutSecond.StopLoss.Should().Be(95m, "−5% dưới hỗ trợ gần nhất");
        withoutSecond.StopLossMethod.Should().Be("percent5");
    }

    [Fact]
    public void a_zero_atr_counts_as_missing()
    {
        var s = TradeSuggestionCalculator.Build(new[] { 100m }, Resistance, atr14: 0m)!;

        s.StopLossMethod.Should().Be("percent5");
    }

    [Theory]
    [InlineData(60)]    // 100 − 2×60 = −20: không dương
    [InlineData(50)]    // 100 − 2×50 = 0: không dương
    public void invalid_stop_no_suggestion_when_it_would_not_be_positive(int atr)
    {
        TradeSuggestionCalculator.Build(new[] { 100m }, Resistance, atr14: atr).Should().BeNull();
    }

    [Fact]
    public void invalid_stop_no_suggestion_when_second_support_is_not_below_entry()
    {
        // Không ATR: hỗ trợ 2 ≥ giá vào thì cắt lỗ không nằm dưới giá vào: không gợi ý, không đưa ra R:R vô nghĩa
        TradeSuggestionCalculator.Build(new[] { 100m, 105m }, Resistance, atr14: null).Should().BeNull();
        TradeSuggestionCalculator.Build(new[] { 100m, 100m }, Resistance, atr14: null).Should().BeNull();
        // Có ATR: dữ liệu hỏng đó cũng bị kéo về vào − 1 ATR (không bao giờ ≥ giá vào)
        var clamped = TradeSuggestionCalculator.Build(new[] { 100m, 105m }, Resistance, atr14: 5m)!;
        clamped.StopLoss.Should().Be(95m);
        clamped.StopLossMethod.Should().Be("atr1x");
    }

    [Fact]
    public void no_suggestion_without_a_support_or_a_resistance()
    {
        TradeSuggestionCalculator.Build(Array.Empty<decimal>(), Resistance, atr14: 5m).Should().BeNull();
        TradeSuggestionCalculator.Build(new[] { 100m }, Array.Empty<decimal>(), atr14: 5m).Should().BeNull();
    }

    [Fact]
    public void entry_and_target_come_from_the_nearest_levels_and_risk_reward_follows_the_stop()
    {
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 95m }, new[] { 110m, 120m }, atr14: 5m)!;

        s.Entry.Should().Be(100m);
        s.Target.Should().Be(110m);
        s.RiskReward.Should().Be(2.0m, "lãi 10 / rủi ro 5");
    }

    [Fact]
    public void a_tighter_stop_gives_a_higher_risk_reward_than_the_old_far_stop()
    {
        // Hỗ trợ 2 xa (80): luật cũ cho R:R = 10/20 = 0.5; luật mới kéo về 90 → 10/10 = 1.0
        var s = TradeSuggestionCalculator.Build(new[] { 100m, 80m }, Resistance, atr14: 5m)!;

        s.RiskReward.Should().Be(1.0m);
    }
}
