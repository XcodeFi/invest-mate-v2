using FluentAssertions;
using InvestmentApp.Domain.Services;

namespace InvestmentApp.Domain.Tests.Services;

public class CalibrationTests
{
    private const decimal Win = 1.5m;
    private const decimal Loss = -1m;
    private const decimal Draw = 0.05m;

    private static List<(decimal, decimal)> Repeat(int times, params (decimal p, decimal r)[] samples)
        => Enumerable.Range(0, times).SelectMany(_ => samples).ToList();

    [Fact]
    public void buckets_assign_by_estimate()
    {
        var result = Calibration.Compute(new List<(decimal, decimal)>
        {
            (0.10m, Loss), (0.399m, Win),      // 0–40
            (0.40m, Win), (0.549m, Loss),      // 40–55 (40% thuộc mức này)
            (0.55m, Win),                      // 55–70
            (0.70m, Win), (1.00m, Win),        // 70–100 (100% thuộc mức cuối)
        });

        result.Buckets.Select(b => b.N).Should().Equal(2, 2, 1, 2);
        result.Buckets.Select(b => b.Label).Should().Equal("0–40%", "40–55%", "55–70%", "70–100%");
        result.Buckets[0].ActualWinRate.Should().Be(0.5m);
        result.Buckets[3].ActualWinRate.Should().Be(1m);
        result.Buckets[2].AvgEstimated.Should().Be(0.55m);
    }

    [Fact]
    public void draws_excluded()
    {
        var result = Calibration.Compute(new List<(decimal, decimal)> { (0.6m, Draw), (0.6m, Win), (0.6m, -0.05m) });

        result.N.Should().Be(1, "hoà (|R| ≤ 0,1) không có đúng/sai để chấm");
        result.Buckets[2].N.Should().Be(1);
    }

    [Fact]
    public void brier_example()
    {
        // (0,6 thắng) (0,6 thua) (0,8 thắng): ((0,4)² + (0,6)² + (0,2)²) / 3 = 0,1867; lặp 5 lần để đủ ngưỡng 15 mẫu
        var result = Calibration.Compute(Repeat(5, (0.6m, Win), (0.6m, Loss), (0.8m, Win)));

        result.N.Should().Be(15);
        result.Sufficient.Should().BeTrue();
        result.Brier!.Value.Should().BeApproximately(0.18667m, 0.00001m);
    }

    [Fact]
    public void optimism_example()
    {
        // p trung bình 0,667 so với thắng thật 2/3 → lệch 0 điểm %
        var result = Calibration.Compute(Repeat(5, (0.6m, Win), (0.6m, Loss), (0.8m, Win)));

        result.OptimismPoints!.Value.Should().BeApproximately(0m, 0.0001m);
    }

    [Fact]
    public void optimism_positive_when_the_user_overestimates()
    {
        // Ước 80% nhưng thắng 50%: lạc quan +30 điểm
        var result = Calibration.Compute(Repeat(8, (0.8m, Win), (0.8m, Loss)));

        result.OptimismPoints!.Value.Should().BeApproximately(30m, 0.0001m);
    }

    [Fact]
    public void bucket_n_lt5_insufficient()
    {
        var result = Calibration.Compute(Repeat(4, (0.6m, Win)).Concat(Repeat(5, (0.8m, Win))).ToList());

        result.Buckets[2].N.Should().Be(4);
        result.Buckets[2].Sufficient.Should().BeFalse("n < 5 thì làm mờ, ghi chưa đủ mẫu");
        result.Buckets[3].N.Should().Be(5);
        result.Buckets[3].Sufficient.Should().BeTrue();
    }

    [Fact]
    public void total_n_lt15_hides_brier()
    {
        var result = Calibration.Compute(Repeat(5, (0.6m, Win), (0.6m, Loss), (0.8m, Win)).Take(14).ToList());

        result.N.Should().Be(14);
        result.Sufficient.Should().BeFalse();
        result.Brier.Should().BeNull();
        result.OptimismPoints.Should().BeNull();
    }

    [Fact]
    public void empty_input_gives_four_empty_buckets_and_no_error()
    {
        var result = Calibration.Compute(Array.Empty<(decimal, decimal)>());

        result.N.Should().Be(0);
        result.Buckets.Should().HaveCount(4).And.OnlyContain(b => b.N == 0 && b.AvgEstimated == null && b.ActualWinRate == null && !b.Sufficient);
        result.Brier.Should().BeNull();
    }

    [Fact]
    public void brier_is_finite_when_every_estimate_is_0_or_1()
    {
        var result = Calibration.Compute(Repeat(8, (1m, Win), (0m, Loss)));

        result.Brier.Should().Be(0m, "ước 100% và thắng, ước 0% và thua: đúng hoàn toàn");
        var worst = Calibration.Compute(Repeat(8, (1m, Loss), (0m, Win)));
        worst.Brier.Should().Be(1m);
    }

    [Fact]
    public void estimates_outside_0_1_are_clamped_not_thrown()
    {
        var act = () => Calibration.Compute(Repeat(15, (1.4m, Win)));

        act.Should().NotThrow();
        act().Buckets[3].N.Should().Be(15);
    }
}

public class KellyFromHistoryTests
{
    [Theory]
    [InlineData(10, 9)]
    [InlineData(0, 0)]
    [InlineData(19, 0)]
    public void kelly_n_lt20_null_with_reason(int wins, int losses)
    {
        var k = KellyFromHistory.Compute(wins, losses, 2m, -1m);

        k.N.Should().Be(wins + losses);
        k.WinRateLower95.Should().BeNull();
        k.HalfKellyCapped.Should().BeNull();
        k.Reason.Should().Contain("20").And.Contain((wins + losses).ToString());
    }

    [Fact]
    public void kelly_example()
    {
        // 14 thắng, 10 thua, avgWin +2R, avgLoss −1R. Số dưới đây tính bằng chính code này, đối chiếu độc lập bằng công thức Wilson:
        // p cận dưới = 0,38835; b = 2; f* = (2·0,38835 − 0,61165) / 2 = 0,08252; half = 0,04126 (kế hoạch ghi 0,383/0,0745/0,037 là ước lượng tay, lệch).
        var k = KellyFromHistory.Compute(14, 10, 2m, -1m);

        k.N.Should().Be(24);
        k.WinRateLower95!.Value.Should().BeApproximately(0.388347m, 0.000001m);
        k.PayoffRatio.Should().Be(2m);
        k.FullKelly!.Value.Should().BeApproximately(0.082520m, 0.000001m);
        k.HalfKellyCapped!.Value.Should().BeApproximately(0.041260m, 0.000001m);
        k.Reason.Should().BeNull();
    }

    [Fact]
    public void kelly_uses_the_wilson_lower_bound_not_the_raw_win_rate()
    {
        // Tỉ lệ thô 14/24 = 58%: dùng nó cho f* lớn hơn hẳn. Cận dưới phải thấp hơn tỉ lệ thô.
        var k = KellyFromHistory.Compute(14, 10, 2m, -1m);

        k.WinRateLower95.Should().BeLessThan(14m / 24m);
    }

    [Fact]
    public void kelly_half_is_capped_at_25_percent()
    {
        var k = KellyFromHistory.Compute(95, 5, 10m, -1m);   // lợi thế rất lớn

        k.FullKelly.Should().BeGreaterThan(0.5m);
        k.HalfKellyCapped.Should().Be(0.25m);
    }

    [Fact]
    public void kelly_no_edge_returns_zero_reason()
    {
        // 5 thắng, 20 thua, thắng nhỏ hơn thua: f* âm
        var k = KellyFromHistory.Compute(5, 20, 1m, -1m);

        k.FullKelly.Should().Be(0m);
        k.HalfKellyCapped.Should().Be(0m);
        k.Reason.Should().Be(KellyFromHistory.NoEdge);
    }

    [Fact]
    public void kelly_all_losses_is_no_edge_without_dividing_by_zero()
    {
        var k = KellyFromHistory.Compute(0, 25, null, -1m);

        k.HalfKellyCapped.Should().Be(0m);
        k.PayoffRatio.Should().BeNull();
        k.Reason.Should().Be(KellyFromHistory.NoEdge);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public void kelly_no_losses_reason(double? avgLossR)
    {
        var k = KellyFromHistory.Compute(25, 0, 2m, avgLossR == null ? null : (decimal)avgLossR);

        k.HalfKellyCapped.Should().BeNull("b không xác định thì không hiện Kelly");
        k.PayoffRatio.Should().BeNull();
        k.Reason.Should().Be("Chưa có lệnh thua để ước lượng.");
    }
}
