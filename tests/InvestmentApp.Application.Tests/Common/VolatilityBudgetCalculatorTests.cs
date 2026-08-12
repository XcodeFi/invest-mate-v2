using FluentAssertions;
using InvestmentApp.Application.Common;
using DatedReturn = InvestmentApp.Application.Common.VolatilityBudgetCalculator.DatedReturn;

namespace InvestmentApp.Application.Tests.Common;

/// <summary>
/// Toán thuần cho trần khối lượng theo ngân sách biến động (ADR-0014). Mọi độ biến động ở biên
/// công khai của calculator là <b>phần trăm mỗi năm</b> (19,4 nghĩa là 19,4%/năm).
/// </summary>
public class VolatilityBudgetCalculatorTests
{
    private static readonly DateTime Day0 = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    // Chuỗi giá VHM thật, 24hmoney type=3, quanh phiên GDKHQ 2026-08-06. Phiên −49,6% là sự kiện
    // quyền chưa điều chỉnh, không phải mất một nửa vốn hóa trong một phiên.
    private static readonly decimal[] VhmCloses =
        { 150.0m, 151.5m, 149.8m, 153.0m, 77.1m, 78.4m, 76.9m, 79.2m, 77.5m, 78.0m };

    /// <summary>Giá đóng cửa theo ngày liên tiếp.</summary>
    private static (DateTime Date, decimal Close)[] Bars(params decimal[] closes) =>
        closes.Select((c, i) => (Day0.AddDays(i), c)).ToArray();

    /// <summary>Lợi suất theo ngày liên tiếp, bắt đầu từ <paramref name="dayOffset"/>.</summary>
    private static DatedReturn[] Dated(decimal[] values, int dayOffset = 0) =>
        values.Select((v, i) => new DatedReturn(Day0.AddDays(dayOffset + i), v)).ToArray();

    private static DatedReturn[] Alternating(int count, decimal magnitude = 0.01m, int phase = 0) =>
        Dated(Enumerable.Range(0, count).Select(i => (i + phase) % 2 == 0 ? magnitude : -magnitude).ToArray());

    private static DatedReturn[] Flat(decimal value, int count) =>
        Dated(Enumerable.Repeat(value, count).ToArray());

    // ---------- ToReturns ----------

    [Fact]
    public void ToReturns_ProducesOneFewerThanBars_AndCarriesTheLaterDate()
    {
        var returns = VolatilityBudgetCalculator.ToReturns(Bars(100m, 110m, 99m));

        returns.Should().HaveCount(2);
        returns[0].Value.Should().BeApproximately(0.10m, 0.0001m);
        returns[0].Date.Should().Be(Day0.AddDays(1), "lợi suất thuộc về phiên SAU, không phải phiên trước");
        returns[1].Value.Should().BeApproximately(-0.10m, 0.0001m);
        returns[1].Date.Should().Be(Day0.AddDays(2));
    }

    [Fact]
    public void ToReturns_SkipsNonPositivePriorClose()
    {
        var returns = VolatilityBudgetCalculator.ToReturns(Bars(0m, 100m, 110m));

        returns.Should().HaveCount(1);
        returns[0].Value.Should().BeApproximately(0.10m, 0.0001m);
    }

    [Fact]
    public void ToReturns_EmptyOrSingle_ReturnsEmpty()
    {
        VolatilityBudgetCalculator.ToReturns(Array.Empty<(DateTime, decimal)>()).Should().BeEmpty();
        VolatilityBudgetCalculator.ToReturns(Bars(100m)).Should().BeEmpty();
    }

    // ---------- Lọc lợi suất bất thường ----------

    [Fact]
    public void FilterAbnormalReturns_RemovesCorporateActionJump_KeepsLegitimateSwings()
    {
        var returns = VolatilityBudgetCalculator.ToReturns(Bars(VhmCloses));

        var (kept, removed) = VolatilityBudgetCalculator.FilterAbnormalReturns(returns);

        removed.Should().Be(1, "chỉ phiên GDKHQ vượt ngưỡng 15%");
        kept.Should().HaveCount(returns.Count - 1);
        kept.Should().NotContain(r => Math.Abs(r.Value) > 0.15m);
    }

    [Fact]
    public void FilterAbnormalReturns_KeepsSevenPointTwoPercent()
    {
        // Ngưỡng 15% cố ý nới hơn biên độ sàn cao nhất (UPCoM ±15%) để không cắt nhầm biến động thật.
        var (kept, removed) = VolatilityBudgetCalculator.FilterAbnormalReturns(
            Dated(new[] { -0.072m, 0.03m }));

        removed.Should().Be(0);
        kept.Should().HaveCount(2);
    }

    [Fact]
    public void FilterAbnormalReturns_CorporateActionJumpDominatesVolatility()
    {
        var raw = VolatilityBudgetCalculator.ToReturns(Bars(VhmCloses));
        var (kept, _) = VolatilityBudgetCalculator.FilterAbnormalReturns(raw);

        var before = VolatilityBudgetCalculator.AnnualizedVolatilityPercent(raw);
        var after = VolatilityBudgetCalculator.AnnualizedVolatilityPercent(kept);

        before.Should().BeGreaterThan(after * 2,
            "một phiên sự kiện quyền chưa lọc thổi σ lên nhiều lần giá trị thật");
        after.Should().BeInRange(20m, 90m);
    }

    [Fact]
    public void FilterAbnormalReturns_LeavesAHoleInTheMiddle_NotATrimmedTail()
    {
        // Chính chỗ này là nguồn gốc lỗi ghép lệch: quan sát bị bỏ nằm GIỮA chuỗi, nên chuỗi còn
        // lại không phải là phần đuôi của chuỗi gốc.
        var raw = VolatilityBudgetCalculator.ToReturns(Bars(VhmCloses));
        var (kept, _) = VolatilityBudgetCalculator.FilterAbnormalReturns(raw);

        kept.Should().HaveCount(raw.Count - 1);
        kept.First().Date.Should().Be(raw.First().Date, "đầu chuỗi không bị cắt");
        kept.Last().Date.Should().Be(raw.Last().Date, "đuôi chuỗi không bị cắt");
    }

    // ---------- Độ lệch chuẩn / biến động ----------

    [Fact]
    public void AnnualizedVolatilityPercent_ConstantSeries_IsZero()
    {
        VolatilityBudgetCalculator.AnnualizedVolatilityPercent(Flat(0.01m, 30)).Should().Be(0m);
    }

    [Fact]
    public void AnnualizedVolatilityPercent_ScalesBySqrt252()
    {
        // Chuỗi ±1% xen kẽ: độ lệch chuẩn ngày = 0,01 → năm = 0,01 × √252 = 15,87%
        VolatilityBudgetCalculator.AnnualizedVolatilityPercent(Alternating(60))
            .Should().BeApproximately(15.87m, 0.05m);
    }

    [Fact]
    public void AnnualizedVolatilityPercent_TooFewObservations_IsZero()
    {
        VolatilityBudgetCalculator.AnnualizedVolatilityPercent(Array.Empty<DatedReturn>()).Should().Be(0m);
        VolatilityBudgetCalculator.AnnualizedVolatilityPercent(Dated(new[] { 0.05m })).Should().Be(0m);
    }

    // ---------- Tương quan / hiệp phương sai ----------

    [Fact]
    public void Correlation_IdenticalSeries_IsOne()
    {
        var a = Dated(new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m });

        VolatilityBudgetCalculator.Correlation(a, a).Should().BeApproximately(1m, 0.0001m);
    }

    [Fact]
    public void Correlation_MirroredSeries_IsMinusOne()
    {
        var a = Dated(new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m });
        var b = a.Select(r => new DatedReturn(r.Date, -r.Value)).ToArray();

        VolatilityBudgetCalculator.Correlation(a, b).Should().BeApproximately(-1m, 0.0001m);
    }

    [Fact]
    public void Correlation_FlatSeries_IsZeroNotNaN()
    {
        VolatilityBudgetCalculator.Correlation(Flat(0.01m, 10), Alternating(10, 0.02m)).Should().Be(0m);
    }

    [Fact]
    public void Correlation_NoOverlappingDates_IsZero()
    {
        var may = Dated(new[] { 0.01m, -0.02m, 0.03m });
        var july = Dated(new[] { 0.01m, -0.02m, 0.03m }, dayOffset: 60);

        VolatilityBudgetCalculator.Correlation(may, july).Should().Be(0m);
    }

    /// <summary>
    /// Ca chống hồi quy cho lỗi ghép lệch. Bản trước ghép hai chuỗi theo ĐUÔI cùng độ dài; khi một
    /// mã thiếu đúng một phiên ở giữa, mọi cặp trước điểm đó bị ghép lệch một ngày và tương quan
    /// sai hoàn toàn — mà không gì báo hiệu.
    /// </summary>
    [Fact]
    public void Correlation_OneSeriesMissingAMidSeriesDay_StillMatchesByDate()
    {
        var full = Dated(new[] { 0.01m, -0.02m, 0.03m, -0.01m, 0.02m, -0.03m });
        // Giống hệt, nhưng khuyết ngày thứ 3 (index 2).
        var gapped = full.Where((_, i) => i != 2).ToArray();

        var correlation = VolatilityBudgetCalculator.Correlation(full, gapped);

        // Ghép đúng ngày thì hai chuỗi trùng khớp trên phần giao → tương quan phải là 1.
        correlation.Should().BeApproximately(1m, 0.0001m,
            "ghép theo ngày; ghép theo vị trí sẽ cho một con số khác hẳn và sai");
    }

    [Fact]
    public void AlignedObservationCount_SameLengthDifferentDates_IsTheIntersection()
    {
        // Hai chuỗi CÙNG độ dài nhưng lệch tập ngày. Math.Min hai độ dài sẽ báo 5; số cặp ghép
        // được thật chỉ là 4. Báo 5 là nói quá độ tin cậy của chính ước lượng đang hiển thị.
        var a = Dated(new[] { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m });
        var b = Dated(new[] { 0.01m, 0.02m, 0.03m, 0.04m, 0.05m }, dayOffset: 1);

        VolatilityBudgetCalculator.AlignedObservationCount(a, b).Should().Be(4);
        Math.Min(a.Length, b.Length).Should().Be(5, "đây chính là con số sai mà bản trước báo ra");
    }

    [Fact]
    public void AlignedObservationCount_NoOverlap_IsZero()
    {
        VolatilityBudgetCalculator.AlignedObservationCount(
            Dated(new[] { 0.01m, 0.02m }),
            Dated(new[] { 0.01m, 0.02m }, dayOffset: 60))
            .Should().Be(0);
    }

    [Theory]
    [InlineData(10_000_000_000)]   // 10 tỷ
    [InlineData(100_000_000_000)]  // 100 tỷ
    [InlineData(500_000_000_000)]  // 500 tỷ
    public void SolveMaxAllocation_LargePortfolioAndWildSymbol_DoesNotOverflow(decimal portfolioValue)
    {
        // b = 2·ρ·V·(σ_p·σ_x − σ_b²) với σ theo đơn vị PHẦN TRĂM, nên b² lớn theo BÌNH PHƯƠNG giá
        // trị danh mục. Hai nhánh thoát sớm chặn bớt: c > 0 buộc σ_danh mục ≤ σ_ngân sách, a ≤ 0
        // buộc σ_mã > σ_ngân sách. Nhưng trong khe còn lại, V đủ lớn vẫn vượt decimal.MaxValue
        // (7,9×10²⁸). Ném ở đây là panel "không bao giờ chặn" lại trả 500, rồi frontend nuốt mất
        // và panel biến mất im lặng — hỏng đúng kiểu tính năng này sinh ra để tránh.
        // σ_mã 300%/năm là biên trên thực tế: mã trần/sàn ±15% mỗi phiên cho 0,15·√252 ≈ 238%.
        var act = () => VolatilityBudgetCalculator.SolveMaxAllocation(
            portfolioValue: portfolioValue,
            portfolioVolPercent: 99m,
            symbolVolPercent: 300m,
            correlation: 1m,
            budgetVolPercent: 100m);

        act.Should().NotThrow<OverflowException>();
    }

    [Fact]
    public void SolveMaxAllocation_LargePortfolio_StillGivesTheSameAnswerAsSmallOne()
    {
        // Bài toán thuần nhất bậc một theo V: nhân đôi danh mục thì trần cũng nhân đôi. Ghim tính
        // chất này để việc chuyển sang double không lặng lẽ làm hỏng độ chính xác.
        var small = VolatilityBudgetCalculator.SolveMaxAllocation(1_000_000_000m, 19.4m, 48.9m, 0.42m, 21.1m);
        var large = VolatilityBudgetCalculator.SolveMaxAllocation(10_000_000_000m, 19.4m, 48.9m, 0.42m, 21.1m);

        small.Should().NotBeNull();
        large.Should().NotBeNull();
        (large!.Value / small!.Value).Should().BeApproximately(10m, 0.0001m);
    }

    // ---------- Chuỗi lợi suất danh mục ----------

    [Fact]
    public void WeightedSeries_SingleHolding_ReturnsThatSeries()
    {
        var series = new IReadOnlyList<DatedReturn>[] { Dated(new[] { 0.01m, -0.02m }) };

        var result = VolatilityBudgetCalculator.WeightedSeries(new[] { 500m }, series);

        result.Select(r => r.Value).Should().BeEquivalentTo(new[] { 0.01m, -0.02m });
    }

    [Fact]
    public void WeightedSeries_WeightsByValueNotCount()
    {
        var series = new IReadOnlyList<DatedReturn>[]
        {
            Dated(new[] { 0.10m, 0.10m }),
            Dated(new[] { 0.00m, 0.00m })
        };

        // 900 vs 100 → lợi suất danh mục nghiêng hẳn về mã lớn: 0,9 × 0,10 = 0,09
        var result = VolatilityBudgetCalculator.WeightedSeries(new[] { 900m, 100m }, series);

        result[0].Value.Should().BeApproximately(0.09m, 0.0001m);
    }

    [Fact]
    public void WeightedSeries_DiversificationLowersVolatility()
    {
        var up = Alternating(40, 0.02m);
        var down = up.Select(r => new DatedReturn(r.Date, -r.Value)).ToArray();

        var portfolio = VolatilityBudgetCalculator.WeightedSeries(
            new[] { 100m, 100m }, new IReadOnlyList<DatedReturn>[] { up, down });

        VolatilityBudgetCalculator.AnnualizedVolatilityPercent(portfolio)
            .Should().BeApproximately(0m, 0.0001m);
    }

    [Fact]
    public void WeightedSeries_UsesDateIntersection_NotTailLength()
    {
        var full = Dated(new[] { 0.10m, 0.10m, 0.10m, 0.10m });
        var gapped = new[] { full[0], full[2], full[3] };   // khuyết ngày thứ hai

        var result = VolatilityBudgetCalculator.WeightedSeries(
            new[] { 100m, 100m }, new IReadOnlyList<DatedReturn>[] { full, gapped });

        result.Should().HaveCount(3, "chỉ giữ ngày có mặt ở CẢ HAI chuỗi");
        result.Select(r => r.Date).Should().Equal(full[0].Date, full[2].Date, full[3].Date);
        result.Should().OnlyContain(r => Math.Abs(r.Value - 0.10m) < 0.0001m);
    }

    [Fact]
    public void WeightedSeries_NoOverlappingDates_ReturnsEmpty()
    {
        var may = Dated(new[] { 0.01m, 0.02m });
        var july = Dated(new[] { 0.01m, 0.02m }, dayOffset: 60);

        VolatilityBudgetCalculator.WeightedSeries(
            new[] { 100m, 100m }, new IReadOnlyList<DatedReturn>[] { may, july })
            .Should().BeEmpty();
    }

    [Fact]
    public void WeightedSeries_NoHoldingsOrZeroValue_ReturnsEmpty()
    {
        VolatilityBudgetCalculator.WeightedSeries(
            Array.Empty<decimal>(), Array.Empty<IReadOnlyList<DatedReturn>>()).Should().BeEmpty();

        VolatilityBudgetCalculator.WeightedSeries(
            new[] { 0m }, new IReadOnlyList<DatedReturn>[] { Dated(new[] { 0.01m }) }).Should().BeEmpty();
    }

    // ---------- Quy đổi ngưỡng sụt giảm → ngân sách biến động ----------

    [Fact]
    public void DrawdownToVolatilityBudgetPercent_TenPercent_IsTwentyOnePointOne()
    {
        // Ghim NGUYÊN VĂN. Đây là hằng số quyết định tính năng có dùng được không: nếu diễn giải
        // theo năm thay vì 21 phiên, 10% cho ra 6,1%/năm và trần luôn bằng 0 (ADR-0014).
        VolatilityBudgetCalculator.DrawdownToVolatilityBudgetPercent(10m)
            .Should().BeApproximately(21.1m, 0.1m);
    }

    [Fact]
    public void DrawdownToVolatilityBudgetPercent_IsLinearInDrawdown()
    {
        var ten = VolatilityBudgetCalculator.DrawdownToVolatilityBudgetPercent(10m);
        var twenty = VolatilityBudgetCalculator.DrawdownToVolatilityBudgetPercent(20m);

        twenty.Should().BeApproximately(ten * 2m, 0.01m);
    }

    [Fact]
    public void DrawdownToVolatilityBudgetPercent_NonPositive_IsZero()
    {
        VolatilityBudgetCalculator.DrawdownToVolatilityBudgetPercent(0m).Should().Be(0m);
        VolatilityBudgetCalculator.DrawdownToVolatilityBudgetPercent(-5m).Should().Be(0m);
    }

    // ---------- Giải trần khối lượng ----------

    [Fact]
    public void SolveMaxAllocation_RoundTrips_ToExactlyTheBudget()
    {
        const decimal v = 1_000_000_000m, sigmaP = 19.4m, sigmaX = 31.7m, rho = 0.5m, budget = 21.1m;

        var a = VolatilityBudgetCalculator.SolveMaxAllocation(v, sigmaP, sigmaX, rho, budget);

        a.Should().NotBeNull();
        a!.Value.Should().BeGreaterThan(0m);

        var projected = VolatilityBudgetCalculator.ProjectedVolatilityPercent(v, sigmaP, a.Value, sigmaX, rho);
        projected.Should().BeApproximately(budget, 0.01m);
    }

    [Fact]
    public void SolveMaxAllocation_HigherCorrelation_GivesSmallerCeiling()
    {
        const decimal v = 1_000_000_000m, sigmaP = 19.4m, sigmaX = 31.7m, budget = 21.1m;

        var low = VolatilityBudgetCalculator.SolveMaxAllocation(v, sigmaP, sigmaX, 0.0m, budget);
        var high = VolatilityBudgetCalculator.SolveMaxAllocation(v, sigmaP, sigmaX, 0.9m, budget);

        // Đây là toàn bộ lý do tính năng tồn tại: mã tương quan cao được mua ít hơn.
        high.Should().NotBeNull();
        low.Should().NotBeNull();
        high!.Value.Should().BeLessThan(low!.Value);
    }

    [Fact]
    public void SolveMaxAllocation_PortfolioAlreadyOverBudget_IsZero()
    {
        VolatilityBudgetCalculator.SolveMaxAllocation(
            portfolioValue: 1_000_000_000m, portfolioVolPercent: 30m,
            symbolVolPercent: 25m, correlation: 0.4m, budgetVolPercent: 21.1m)
            .Should().Be(0m, "đã vượt ngân sách thì mọi khoản mua thêm đều không hợp lệ");
    }

    [Fact]
    public void SolveMaxAllocation_SymbolCalmerThanBudget_IsUnconstrained()
    {
        VolatilityBudgetCalculator.SolveMaxAllocation(
            portfolioValue: 1_000_000_000m, portfolioVolPercent: 19.4m,
            symbolVolPercent: 10m, correlation: 0.3m, budgetVolPercent: 21.1m)
            .Should().BeNull();
    }

    [Fact]
    public void SolveMaxAllocation_SymbolVolEqualsBudget_IsUnconstrainedNotDivideByZero()
    {
        // A = 0 — nhánh từng được viết thành phép chia bậc nhất. Không có nghiệm hữu hạn ở đây:
        // trộn hai tài sản với ρ ≤ 1 cho biến động không vượt max(σ_danh mục, σ_mã), mà max đó
        // đúng bằng ngân sách. Nghiệm hữu hạn sẽ đòi ρ > 1.
        VolatilityBudgetCalculator.SolveMaxAllocation(
            portfolioValue: 1_000_000_000m, portfolioVolPercent: 19.4m,
            symbolVolPercent: 21.1m, correlation: 0.5m, budgetVolPercent: 21.1m)
            .Should().BeNull();

        // Chứng minh bằng số: giải ngân gấp mười lần danh mục vẫn không vượt ngân sách.
        VolatilityBudgetCalculator.ProjectedVolatilityPercent(
            1_000_000_000m, 19.4m, 10_000_000_000m, 21.1m, 0.5m)
            .Should().BeLessThanOrEqualTo(21.1m);
    }

    [Fact]
    public void SolveMaxAllocation_SymbolSlightlyAboveBudget_HasFiniteCeiling()
    {
        var a = VolatilityBudgetCalculator.SolveMaxAllocation(
            portfolioValue: 1_000_000_000m, portfolioVolPercent: 19.4m,
            symbolVolPercent: 21.5m, correlation: 0.5m, budgetVolPercent: 21.1m);

        a.Should().NotBeNull();
        a!.Value.Should().BeGreaterThan(0m);

        VolatilityBudgetCalculator.ProjectedVolatilityPercent(1_000_000_000m, 19.4m, a.Value, 21.5m, 0.5m)
            .Should().BeApproximately(21.1m, 0.01m);
    }

    [Fact]
    public void SolveMaxAllocation_EmptyPortfolio_BoundedBySymbolVolatilityAlone()
    {
        VolatilityBudgetCalculator.SolveMaxAllocation(0m, 0m, 31.7m, 0m, 21.1m).Should().Be(0m);
        VolatilityBudgetCalculator.SolveMaxAllocation(0m, 0m, 10m, 0m, 21.1m).Should().BeNull();
    }

    [Fact]
    public void SolveMaxAllocation_ZeroOrNegativeBudget_IsZero()
    {
        VolatilityBudgetCalculator.SolveMaxAllocation(1_000_000_000m, 19.4m, 31.7m, 0.5m, 0m)
            .Should().Be(0m);
    }

    [Fact]
    public void SolveMaxAllocation_ZeroSymbolVolatility_IsUnconstrained()
    {
        VolatilityBudgetCalculator.SolveMaxAllocation(1_000_000_000m, 19.4m, 0m, 0m, 21.1m)
            .Should().BeNull();
    }

    // ---------- Biến động sau lệnh ----------

    [Fact]
    public void ProjectedVolatilityPercent_ZeroAllocation_EqualsCurrent()
    {
        VolatilityBudgetCalculator.ProjectedVolatilityPercent(1_000_000_000m, 19.4m, 0m, 31.7m, 0.5m)
            .Should().BeApproximately(19.4m, 0.0001m);
    }

    [Fact]
    public void ProjectedVolatilityPercent_PerfectlyCorrelatedSameVol_StaysFlat()
    {
        VolatilityBudgetCalculator.ProjectedVolatilityPercent(1_000_000_000m, 20m, 500_000_000m, 20m, 1m)
            .Should().BeApproximately(20m, 0.01m);
    }

    [Fact]
    public void ProjectedVolatilityPercent_UncorrelatedAdd_LowersVolatility()
    {
        var projected = VolatilityBudgetCalculator.ProjectedVolatilityPercent(
            1_000_000_000m, 20m, 1_000_000_000m, 20m, 0m);

        projected.Should().BeLessThan(20m);
        projected.Should().BeApproximately(14.14m, 0.01m, "√(0,5² + 0,5²) × 20 = 14,14");
    }

    [Fact]
    public void ProjectedVolatilityPercent_ZeroTotalValue_IsZero()
    {
        VolatilityBudgetCalculator.ProjectedVolatilityPercent(0m, 0m, 0m, 31.7m, 0.5m).Should().Be(0m);
    }

    // ---------- Đóng góp rủi ro biên ----------

    [Fact]
    public void MarginalRiskContributionPercent_SingleAsset_IsOneHundred()
    {
        VolatilityBudgetCalculator.MarginalRiskContributionPercent(1m, 0.04m, 0.04m)
            .Should().BeApproximately(100m, 0.0001m);
    }

    [Fact]
    public void MarginalRiskContributionPercent_CanExceedCapitalWeight()
    {
        VolatilityBudgetCalculator.MarginalRiskContributionPercent(0.14m, 0.063m, 0.04m)
            .Should().BeGreaterThan(14m);
    }

    [Fact]
    public void MarginalRiskContributionPercent_ZeroVariance_IsZeroNotDivideByZero()
    {
        VolatilityBudgetCalculator.MarginalRiskContributionPercent(0.5m, 0.01m, 0m).Should().Be(0m);
    }
}
