using FluentAssertions;
using InvestmentApp.Application.MarketData.Screener;

namespace InvestmentApp.Application.Tests.MarketData.Screener;

public class ScreenerRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 3, 0, 0, TimeSpan.Zero);

    /// <summary>Mã đạt MỌI tiêu chí mặc định; mỗi test chỉ làm hỏng đúng một điều kiện.</summary>
    private static ScreenerRow Row(string symbol = "AAA", string floor = "HOSE", string? icb = "8000",
        decimal capBn = 20_000m, decimal price = 50_000m, decimal? vol = 1_000_000m, decimal? price10 = 50_000m,
        decimal? pe = 12m, decimal? roe = 18m, decimal? profit = 500m, int? rs1m = 60, int? rs3m = 70, int? rs52w = 55, decimal? beta = 1.1m)
        => new(symbol, floor, icb, capBn, price, vol, price10, pe, 1.5m, roe, 8m, profit, rs1m, rs3m, rs52w, beta);

    private static ScreenerSnapshot Snap(HoseLists? hose, params ScreenerRow[] rows) => new()
    {
        Rows = rows, AsOf = Now, Hose = hose, HoseAsOf = hose == null ? null : Now
    };

    private static HoseLists NoHoseFlags() => new(Array.Empty<HoseStatusEntry>(), Array.Empty<HoseDelisting>());

    private static ScreenerResult Run(ScreenerSnapshot s, ScreenerCriteria? c = null) => ScreenerRules.Evaluate(s, (c ?? new ScreenerCriteria()).Clamped());

    private static string ReasonOf(ScreenerResult r, string symbol)
        => r.Excluded!.Single(x => x.Symbol == symbol).Reason;

    private static ScreenerCriteria WithExcluded(ScreenerCriteria? c = null) => new()
    {
        IncludeExcluded = true,
        Floors = (c ?? new ScreenerCriteria()).Floors,
        IncludeUpcom = c?.IncludeUpcom ?? false,
        MaxPe = c?.MaxPe,
        SkipQualityLayer = c?.SkipQualityLayer ?? false,
        MinRs3m = c?.MinRs3m ?? 50
    };

    // ---------------- Tầng 1: loại

    [Fact]
    public void a_row_meeting_every_default_passes_with_reasons()
    {
        var r = Run(Snap(NoHoseFlags(), Row()));

        r.Passed.Should().ContainSingle();
        r.Passed[0].Symbol.Should().Be("AAA");
        r.Passed[0].Reasons.Should().NotBeEmpty();
        r.Status.Should().Be("ok");
    }

    [Fact]
    public void hose_special_monitoring_list_excludes_with_list_name_and_reason()
    {
        var hose = new HoseLists(new[] { new HoseStatusEntry("AAA", "CK thuộc diện bị cảnh báo", "Vốn chủ sở hữu âm", null) }, Array.Empty<HoseDelisting>());

        var r = Run(Snap(hose, Row()), WithExcluded());

        r.Passed.Should().BeEmpty();
        ReasonOf(r, "AAA").Should().Be("CK thuộc diện bị cảnh báo: Vốn chủ sở hữu âm");
        r.ExcludedCounts.Should().ContainKey("HOSE: CK thuộc diện bị cảnh báo").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void hose_delisting_announced_excludes()
    {
        var hose = new HoseLists(Array.Empty<HoseStatusEntry>(), new[] { new HoseDelisting("AAA", new DateTime(2027, 2, 1)) });

        var r = Run(Snap(hose, Row()), WithExcluded());

        r.Passed.Should().BeEmpty();
        ReasonOf(r, "AAA").Should().Contain("hủy niêm yết");
    }

    [Fact]
    public void hose_lists_do_not_apply_to_hnx_symbols_with_the_same_code()
    {
        var hose = new HoseLists(new[] { new HoseStatusEntry("BBB", "CK thuộc diện bị cảnh báo", "x", null) }, Array.Empty<HoseDelisting>());

        var r = Run(Snap(hose, Row("BBB", floor: "HNX")));

        r.Passed.Should().ContainSingle().Which.Symbol.Should().Be("BBB");
    }

    [Fact]
    public void liquidity_rule_excludes_below_five_billion_per_day()
    {
        // 100.000 cp × 20.000 đ = 2 tỷ < 5 tỷ
        var r = Run(Snap(NoHoseFlags(), Row(vol: 100_000m, price10: 20_000m, price: 20_000m)), WithExcluded());

        r.Passed.Should().BeEmpty();
        ReasonOf(r, "AAA").Should().Contain("Thanh khoản");
    }

    [Fact]
    public void liquidity_boundary_exactly_five_billion_passes()
    {
        var r = Run(Snap(NoHoseFlags(), Row(vol: 100_000m, price10: 50_000m, price: 50_000m)));   // đúng 5 tỷ

        r.Passed.Should().ContainSingle();
    }

    [Fact]
    public void missing_liquidity_data_excludes_rather_than_passes()
    {
        var r = Run(Snap(NoHoseFlags(), Row(vol: null)), WithExcluded());

        r.Passed.Should().BeEmpty();
        ReasonOf(r, "AAA").Should().Contain("Thanh khoản");
    }

    [Fact]
    public void price_rule_excludes_below_five_thousand_dong()
    {
        var r = Run(Snap(NoHoseFlags(), Row(price: 4_900m, price10: 4_900m, vol: 5_000_000m)), WithExcluded());

        r.Passed.Should().BeEmpty();
        ReasonOf(r, "AAA").Should().Contain("Giá");
    }

    [Fact]
    public void market_cap_rule_excludes_below_one_thousand_billion()
    {
        var r = Run(Snap(NoHoseFlags(), Row(capBn: 999m)), WithExcluded());

        r.Passed.Should().BeEmpty();
        ReasonOf(r, "AAA").Should().Contain("Vốn hoá");
    }

    [Fact]
    public void first_failing_rule_is_the_reason()
    {
        // Hỏng cả thanh khoản, giá và vốn hoá: lý do là điều kiện đầu tiên theo thứ tự tầng 1 (thanh khoản).
        var r = Run(Snap(NoHoseFlags(), Row(vol: 1_000m, price10: 4_000m, price: 4_000m, capBn: 10m)), WithExcluded());

        ReasonOf(r, "AAA").Should().Contain("Thanh khoản");
        r.ExcludedCounts.Should().HaveCount(1, "mỗi mã chỉ đếm một lần, theo lý do đầu tiên");
    }

    [Fact]
    public void upcom_excluded_unless_opted_in()
    {
        var snap = Snap(NoHoseFlags(), Row("UUU", floor: "UPCOM"));

        var off = Run(snap, WithExcluded());
        off.Passed.Should().BeEmpty();
        ReasonOf(off, "UUU").Should().Contain("UPCOM");

        var on = Run(snap, new ScreenerCriteria { IncludeUpcom = true });
        on.Passed.Should().ContainSingle().Which.Symbol.Should().Be("UUU");
    }

    [Fact]
    public void hnx_symbols_get_unverified_status_note_per_row()
    {
        var r = Run(Snap(NoHoseFlags(), Row("AAA"), Row("HHH", floor: "HNX")));

        r.Passed.Single(x => x.Symbol == "HHH").Notes.Should().Contain("chưa kiểm được danh sách cảnh báo HNX");
        r.Passed.Single(x => x.Symbol == "AAA").Notes.Should().NotContain(n => n.Contains("HNX"));
    }

    // ---------------- Tầng 2: chất lượng

    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    public void quality_layer_excludes_non_positive_profit(int profit)
    {
        var r = Run(Snap(NoHoseFlags(), Row(profit: profit)), WithExcluded());

        r.Passed.Should().BeEmpty();
        ReasonOf(r, "AAA").Should().Contain("Lợi nhuận");
    }

    [Fact]
    public void quality_layer_excludes_low_roe_and_boundary_passes()
    {
        Run(Snap(NoHoseFlags(), Row(roe: 11.99m))).Passed.Should().BeEmpty();
        Run(Snap(NoHoseFlags(), Row(roe: 12m))).Passed.Should().ContainSingle();
    }

    [Fact]
    public void max_pe_is_off_by_default_and_applies_when_given()
    {
        var snap = Snap(NoHoseFlags(), Row(pe: 80m));

        Run(snap).Passed.Should().ContainSingle("trần P/E mặc định TẮT");
        Run(snap, new ScreenerCriteria { MaxPe = 30m }).Passed.Should().BeEmpty();
        Run(snap, new ScreenerCriteria { MaxPe = 80m }).Passed.Should().ContainSingle();
    }

    [Fact]
    public void skipping_the_quality_layer_lets_loss_making_rows_through()
    {
        var snap = Snap(NoHoseFlags(), Row(profit: -50m, roe: -3m));

        Run(snap).Passed.Should().BeEmpty();
        Run(snap, new ScreenerCriteria { SkipQualityLayer = true }).Passed.Should().ContainSingle();
    }

    // ---------------- Tầng 3: xu hướng (RS là xếp hạng phân vị)

    [Fact]
    public void trend_layer_requires_rs3m_rank_at_or_above_the_minimum()
    {
        Run(Snap(NoHoseFlags(), Row(rs3m: 49))).Passed.Should().BeEmpty();
        Run(Snap(NoHoseFlags(), Row(rs3m: 50))).Passed.Should().ContainSingle();
    }

    [Fact]
    public void missing_rs_data_excludes_as_missing_trend_data()
    {
        var r = Run(Snap(NoHoseFlags(), Row(rs3m: null)), WithExcluded());

        ReasonOf(r, "AAA").Should().Contain("Thiếu dữ liệu xu hướng");
    }

    [Fact]
    public void null_rs_is_its_own_bucket_counted_per_sector_in_lookup_and_never_in_the_tradeoff_bucket()
    {
        var snap = Snap(NoHoseFlags(),
            Row("N1", icb: "8000", rs3m: null),
            Row("N2", icb: "8000", rs3m: null, roe: 3m),   // hỏng cả chất lượng: đếm theo lý do đầu tiên, không vào vùng đánh đổi
            Row("OK", icb: "8000"));

        var r = Run(snap, new ScreenerCriteria { LookupSymbol = "N1" });

        r.ExcludedCounts["Thiếu dữ liệu xu hướng"].Should().Be(1);
        r.ExcludedCounts.Values.Sum().Should().Be(2, "không mã nào bị bỏ rơi khỏi bộ đếm");
        r.SectorCounts["8000"].Excluded.Should().Be(2);
        r.TradeoffCount.Should().Be(0);
        r.Lookup!.State.Should().Be("excluded");
        r.Lookup.Reason.Should().Contain("Thiếu dữ liệu xu hướng");
    }

    [Fact]
    public void the_trend_wording_names_the_percentile_scope_and_never_claims_beating_the_market()
    {
        var r = Run(Snap(NoHoseFlags(), Row("W", rs3m: 40), Row("OK", rs3m: 70)), WithExcluded());

        ReasonOf(r, "W").Should().Contain("P50").And.Contain("toàn bộ mã niêm yết");
        r.Passed[0].Reasons.Should().Contain(x => x.Contains("P50") && x.Contains("toàn bộ mã niêm yết"));
        r.Passed[0].Reasons.Concat(new[] { ReasonOf(r, "W") }).Should().NotContain(x => x.Contains("mạnh hơn thị trường") || x.Contains("VN-Index"));
    }

    // ---------------- Cờ đầu cơ: cờ, không loại

    [Fact]
    public void speculation_is_flag_not_exclusion()
    {
        // RS1T ≥ 95 + vốn hoá nhỏ, beta cao, RS52T ≥ 90 + vốn hoá nhỏ: vẫn nằm trong danh sách, kèm cờ ghi giá trị.
        var r = Run(Snap(NoHoseFlags(), Row(capBn: 2_000m, rs1m: 96, rs52w: 95, beta: 2.1m)));

        var row = r.Passed.Should().ContainSingle().Subject;
        row.Flags.Should().Equal("RS1T 96/99, vốn hoá nhỏ", "beta cao (2.1)", "RS52T 95/99, vốn hoá nhỏ");
    }

    [Theory]
    [InlineData(94, false)]   // RS1T 94 chưa tới 95
    [InlineData(95, true)]
    public void rs1m_flag_threshold_is_95_not_90(int rs1m, bool flagged)
    {
        var flags = Run(Snap(NoHoseFlags(), Row(capBn: 2_000m, rs1m: rs1m, rs52w: 50))).Passed[0].Flags;

        flags.Any(f => f.StartsWith("RS1T")).Should().Be(flagged);
    }

    [Fact]
    public void flags_are_not_raised_for_large_caps()
    {
        Run(Snap(NoHoseFlags(), Row(capBn: 20_000m, rs1m: 99, rs52w: 99))).Passed[0].Flags.Should().BeEmpty();
    }

    [Fact]
    public void surged_then_faded_flag_fires_on_high_rs52w_with_low_rs1m_regardless_of_cap()
    {
        var row = Run(Snap(NoHoseFlags(), Row(capBn: 50_000m, rs1m: 20, rs52w: 90, rs3m: 60))).Passed.Should().ContainSingle().Subject;

        row.Flags.Should().ContainSingle().Which.Should().Be("tăng nóng rồi gãy (RS52T 90/99, RS1T 20/99)");
    }

    [Fact]
    public void no_flags_for_a_large_calm_symbol()
    {
        Run(Snap(NoHoseFlags(), Row())).Passed[0].Flags.Should().BeEmpty();
    }

    // ---------------- HOSE fail-closed

    [Fact]
    public void hose_lists_unavailable_moves_hose_to_unverified_group()
    {
        var r = Run(Snap(null, Row("AAA"), Row("HHH", floor: "HNX")));

        r.Passed.Select(x => x.Symbol).Should().Equal("HHH");
        r.UnverifiedHose.Select(x => x.Symbol).Should().Equal("AAA");
        r.Notes.Should().Contain(n => n.Contains("Chưa kiểm được trạng thái (HOSE)"));
    }

    [Fact]
    public void hose_unavailable_still_excludes_hose_rows_failing_other_rules()
    {
        var r = Run(Snap(null, Row("AAA", capBn: 10m)), WithExcluded());

        r.UnverifiedHose.Should().BeEmpty();
        ReasonOf(r, "AAA").Should().Contain("Vốn hoá");
    }

    // ---------------- Đếm theo ngành và vùng đánh đổi

    [Fact]
    public void per_sector_counts_show_passed_and_excluded()
    {
        var r = Run(Snap(NoHoseFlags(),
            Row("A1", icb: "8000"), Row("A2", icb: "8000", roe: 3m), Row("B1", icb: "1000", capBn: 10m)));

        r.SectorCounts["8000"].Should().BeEquivalentTo(new ScreenerSectorCount { Passed = 1, Excluded = 1 });
        r.SectorCounts["1000"].Should().BeEquivalentTo(new ScreenerSectorCount { Passed = 0, Excluded = 1 });
    }

    [Fact]
    public void tradeoff_bucket_counts_quality_only_exclusions_with_good_trend_and_liquidity()
    {
        var r = Run(Snap(NoHoseFlags(),
            Row("Q1", roe: 3m),                  // chỉ hỏng chất lượng, xu hướng tốt → đếm
            Row("Q2", roe: 3m, rs3m: 20),        // hỏng cả xu hướng → không đếm
            Row("Q3", roe: 3m, capBn: 10m),      // hỏng tầng 1 → không đếm
            Row("OK")));

        r.TradeoffCount.Should().Be(1);
    }

    [Fact]
    public void tradeoff_bucket_includes_rows_excluded_only_by_the_optional_pe_cap()
    {
        var r = Run(Snap(NoHoseFlags(), Row("P1", pe: 80m), Row("OK", pe: 10m)), new ScreenerCriteria { MaxPe = 30m });

        r.TradeoffCount.Should().Be(1);
    }

    // ---------------- Đầu ra: không điểm số, sắp theo bảng chữ cái

    [Fact]
    public void default_sort_alphabetical()
    {
        var r = Run(Snap(NoHoseFlags(), Row("ZZZ"), Row("AAA"), Row("MMM")));

        r.Passed.Select(x => x.Symbol).Should().Equal("AAA", "MMM", "ZZZ");
    }

    [Fact]
    public void no_score_field_and_default_sort_by_alphabet()
    {
        var names = typeof(ScreenerPassedRow).GetProperties().Select(p => p.Name)
            .Concat(typeof(ScreenerResult).GetProperties().Select(p => p.Name));

        names.Should().NotContain(n => n.Contains("Score", StringComparison.OrdinalIgnoreCase)
                                       || n.Contains("Rank", StringComparison.OrdinalIgnoreCase)
                                       || n.Contains("Potential", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void result_always_carries_the_disclaimer()
    {
        Run(Snap(NoHoseFlags())).Disclaimer.Should().Be("Danh sách lọc theo tiêu chí, không phải khuyến nghị mua. Dữ liệu từ nguồn công khai không chính thức, có thể trễ. Cờ dựa trên xếp hạng tương đối, không phải đánh giá rủi ro.");
    }

    // ---------------- Tra cứu và phân trang danh sách loại

    [Fact]
    public void lookup_explains_why_a_symbol_is_missing()
    {
        var snap = Snap(NoHoseFlags(), Row("AAA", roe: 3m), Row("BBB"));

        Run(snap, new ScreenerCriteria { LookupSymbol = "aaa" }).Lookup.Should().BeEquivalentTo(new { Symbol = "AAA", State = "excluded" },
            o => o.ExcludingMissingMembers());
        Run(snap, new ScreenerCriteria { LookupSymbol = "AAA" }).Lookup!.Reason.Should().Contain("ROE");
        Run(snap, new ScreenerCriteria { LookupSymbol = "BBB" }).Lookup!.State.Should().Be("passed");
        Run(snap, new ScreenerCriteria { LookupSymbol = "XYZ" }).Lookup!.State.Should().Be("not_in_universe");
    }

    [Fact]
    public void lookup_reports_unverified_hose()
    {
        Run(Snap(null, Row("AAA")), new ScreenerCriteria { LookupSymbol = "AAA" }).Lookup!.State.Should().Be("unverified_hose");
    }

    [Fact]
    public void excluded_list_is_alphabetical_and_paged()
    {
        var rows = Enumerable.Range(0, 5).Select(i => Row($"S{i}", roe: 1m)).Reverse().ToArray();

        var page = Run(Snap(NoHoseFlags(), rows), new ScreenerCriteria { IncludeExcluded = true, ExcludedSkip = 1, ExcludedTake = 2 });

        page.ExcludedTotal.Should().Be(5);
        page.Excluded!.Select(x => x.Symbol).Should().Equal("S1", "S2");
    }

    [Fact]
    public void excluded_list_is_absent_unless_requested()
    {
        Run(Snap(NoHoseFlags(), Row(roe: 1m))).Excluded.Should().BeNull();
    }

    // ---------------- Tham số được kẹp, sàn chỉ nhận enum

    [Fact]
    public void params_clamped_and_floors_enum_only()
    {
        var c = new ScreenerCriteria
        {
            MinAdtvBn = -5m, MinPriceVnd = 1e12m, MinMarketCapBn = -1m, MinRoe = 5_000m, MaxPe = -3m, MinRs3m = 500,
            ExcludedSkip = -9, ExcludedTake = 1_000_000
        }.Clamped();

        c.MinAdtvBn.Should().Be(0m);
        c.MinPriceVnd.Should().Be(1_000_000m);
        c.MinMarketCapBn.Should().Be(0m);
        c.MinRoe.Should().Be(100m);
        c.MaxPe.Should().Be(0m);
        c.MinRs3m.Should().Be(99);
        c.ExcludedSkip.Should().Be(0);
        c.ExcludedTake.Should().Be(ScreenerCriteria.MaxExcludedTake);

        ScreenerCriteria.ParseFloors("hose, hnx,HOSE").Should().Equal("HOSE", "HNX");
        ScreenerCriteria.ParseFloors(null).Should().Equal("HOSE", "HNX");
        var bad = () => ScreenerCriteria.ParseFloors("HOSE,NYSE");
        bad.Should().Throw<ArgumentException>().Which.Message.Should().Contain("NYSE");
    }

    // ---------------- Đột biến: bỏ luật thì phải đỏ (các test trên ghim từng luật; test dưới ghim thứ tự đếm)

    [Fact]
    public void unavailable_snapshot_returns_no_rows_and_the_reason()
    {
        var snap = new ScreenerSnapshot { UnavailableReason = "Dữ liệu sàng lọc quá cũ" };

        var r = Run(snap);

        r.Status.Should().Be("unavailable");
        r.Reason.Should().Be("Dữ liệu sàng lọc quá cũ");
        r.Passed.Should().BeEmpty();
    }

    [Fact]
    public void stale_snapshot_is_reported_stale_with_as_of()
    {
        var snap = new ScreenerSnapshot { Rows = new[] { Row() }, AsOf = Now, Stale = true, Hose = NoHoseFlags(), HoseAsOf = Now };

        var r = Run(snap);

        r.Status.Should().Be("stale");
        r.Stale.Should().BeTrue();
        r.AsOf.Should().Be(Now);
        r.Passed.Should().ContainSingle();
    }
}
