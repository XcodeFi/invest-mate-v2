using System.Globalization;

namespace InvestmentApp.Application.MarketData.Screener;

/// <summary>
/// Bộ luật sàng lọc, thuần hàm: áp ngưỡng LÊN kho dữ liệu đã lấy (không bao giờ quyết định việc gọi nguồn).
/// Tầng 1 loại (diện đặc biệt/hủy niêm yết HOSE, thanh khoản, giá, vốn hoá), tầng 2 chất lượng, tầng 3 xu hướng.
/// Cờ đầu cơ chỉ gắn nhãn, không loại. Cố ý không có điểm số hay thứ hạng (ADR-0020).
/// </summary>
public static class ScreenerRules
{
    private const string HnxNote = "chưa kiểm được danh sách cảnh báo HNX";
    private const string UpcomNote = "chưa kiểm được danh sách cảnh báo UPCOM";
    private const decimal SmallCapBn = 5_000m;
    private const int Rs1mHotRank = 95;
    private const int Rs52wHotRank = 90;
    private const int FadedRs1mRank = 20;
    private const decimal HighBeta = 1.8m;

    private enum Category { Exclusion, Quality, Trend }

    private sealed record Failure(Category Category, string Key, string Detail);

    public static ScreenerResult Evaluate(ScreenerSnapshot snapshot, ScreenerCriteria criteria)
    {
        if (snapshot.UnavailableReason != null)
        {
            return new ScreenerResult
            {
                Status = "unavailable",
                Reason = snapshot.UnavailableReason,
                Notes = snapshot.Notes.ToList(),
                Lookup = criteria.LookupSymbol == null ? null
                    : new ScreenerLookup { Symbol = criteria.LookupSymbol, State = "not_in_universe", Reason = snapshot.UnavailableReason }
            };
        }

        var floors = criteria.Floors.ToHashSet(StringComparer.Ordinal);
        if (criteria.IncludeUpcom) floors.Add("UPCOM");

        var statusBySymbol = snapshot.Hose?.Statuses
            .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var delisted = snapshot.Hose?.Delistings.Select(d => d.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var passed = new List<ScreenerPassedRow>();
        var unverified = new List<ScreenerPassedRow>();
        var excluded = new List<ScreenerExcludedRow>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var sectors = new Dictionary<string, ScreenerSectorCount>(StringComparer.Ordinal);
        var tradeoff = 0;
        ScreenerLookup? lookup = null;

        foreach (var row in snapshot.Rows)
        {
            var hoseUnverified = false;
            var failures = Evaluate(row, criteria, floors, statusBySymbol, delisted, snapshot.Hose == null, ref hoseUnverified);
            var sector = string.IsNullOrWhiteSpace(row.IcbGroup) ? "n/a" : row.IcbGroup!;
            if (!sectors.TryGetValue(sector, out var sectorCount)) sectors[sector] = sectorCount = new ScreenerSectorCount();

            string state;
            string? reason = null;
            if (failures.Count > 0)
            {
                var first = failures[0];
                excluded.Add(new ScreenerExcludedRow { Symbol = row.Symbol, Floor = row.Floor, Reason = first.Detail });
                counts[first.Key] = counts.GetValueOrDefault(first.Key) + 1;
                sectorCount.Excluded++;
                if (failures.All(f => f.Category == Category.Quality)) tradeoff++;
                state = "excluded";
                reason = first.Detail;
            }
            else if (hoseUnverified)
            {
                unverified.Add(ToPassed(row, criteria));
                state = "unverified_hose";
            }
            else
            {
                passed.Add(ToPassed(row, criteria));
                sectorCount.Passed++;
                state = "passed";
            }

            if (criteria.LookupSymbol != null && string.Equals(row.Symbol, criteria.LookupSymbol, StringComparison.OrdinalIgnoreCase))
                lookup = new ScreenerLookup { Symbol = row.Symbol, State = state, Reason = reason };
        }

        if (criteria.LookupSymbol != null && lookup == null)
            lookup = new ScreenerLookup
            {
                Symbol = criteria.LookupSymbol,
                State = "not_in_universe",
                Reason = "Mã không có trong dữ liệu sàng lọc (không niêm yết, hoặc thiếu giá/vốn hoá ở nguồn)."
            };

        var notes = snapshot.Notes.ToList();
        if (snapshot.Hose == null)
            notes.Add("Chưa kiểm được trạng thái (HOSE): không lấy được danh sách diện đặc biệt/hủy niêm yết của HOSE, nên các mã HOSE đạt tiêu chí được tách riêng, chưa xếp vào danh sách đạt.");
        else if (snapshot.HoseStale)
            notes.Add("Danh sách diện đặc biệt/hủy niêm yết của HOSE là dữ liệu cũ (lần làm mới gần nhất bị lỗi).");

        var result = new ScreenerResult
        {
            Status = snapshot.Stale ? "stale" : "ok",
            Stale = snapshot.Stale,
            AsOf = snapshot.AsOf,
            HoseListsAsOf = snapshot.HoseAsOf,
            HoseListsStale = snapshot.HoseStale,
            UniverseCount = snapshot.Rows.Count,
            Passed = passed.OrderBy(r => r.Symbol, StringComparer.Ordinal).ToList(),
            UnverifiedHose = unverified.OrderBy(r => r.Symbol, StringComparer.Ordinal).ToList(),
            ExcludedCounts = counts.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value),
            SectorCounts = sectors.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value),
            TradeoffCount = tradeoff,
            Lookup = lookup,
            Notes = notes
        };

        if (!criteria.IncludeExcluded) return result;

        var ordered = excluded.OrderBy(e => e.Symbol, StringComparer.Ordinal).ToList();
        return new ScreenerResult
        {
            Status = result.Status, Stale = result.Stale, AsOf = result.AsOf, HoseListsAsOf = result.HoseListsAsOf,
            HoseListsStale = result.HoseListsStale, UniverseCount = result.UniverseCount, Passed = result.Passed,
            UnverifiedHose = result.UnverifiedHose, ExcludedCounts = result.ExcludedCounts, SectorCounts = result.SectorCounts,
            TradeoffCount = result.TradeoffCount, Lookup = result.Lookup, Notes = result.Notes,
            ExcludedTotal = ordered.Count,
            Excluded = ordered.Skip(criteria.ExcludedSkip).Take(criteria.ExcludedTake).ToList()
        };
    }

    private static List<Failure> Evaluate(
        ScreenerRow row, ScreenerCriteria c, HashSet<string> floors,
        Dictionary<string, HoseStatusEntry>? statusBySymbol, HashSet<string>? delisted, bool hoseListsMissing,
        ref bool hoseUnverified)
    {
        var failures = new List<Failure>();

        if (!floors.Contains(row.Floor))
        {
            failures.Add(row.Floor == "UPCOM"
                ? new Failure(Category.Exclusion, "UPCOM chưa bật", "UPCOM chưa được bật (thanh khoản thấp, nhiều mã đầu cơ, chưa có danh sách cảnh báo)")
                : new Failure(Category.Exclusion, "Ngoài sàn đã chọn", $"Sàn {row.Floor} không nằm trong phạm vi đã chọn"));
            return failures;
        }

        if (row.Floor == "HOSE")
        {
            if (hoseListsMissing) hoseUnverified = true;
            else
            {
                if (statusBySymbol!.TryGetValue(row.Symbol, out var status))
                    failures.Add(new Failure(Category.Exclusion, $"HOSE: {status.ListName}", $"{status.ListName}: {status.Reason}"));
                else if (delisted!.Contains(row.Symbol))
                    failures.Add(new Failure(Category.Exclusion, "HOSE: sắp hủy niêm yết", "HOSE đã công bố hủy niêm yết"));
            }
        }

        // Tầng 1: thanh khoản, giá, vốn hoá. Thiếu dữ liệu thì loại (không coi là đạt).
        var adtv = row.AdtvBn;
        if (adtv is null || adtv < c.MinAdtvBn)
            failures.Add(new Failure(Category.Exclusion, "Thanh khoản thấp",
                adtv is null ? "Thanh khoản: thiếu dữ liệu khối lượng giao dịch"
                    : $"Thanh khoản thấp: giá trị giao dịch bình quân 10 phiên {Fmt(adtv.Value)} tỷ, dưới {Fmt(c.MinAdtvBn)} tỷ"));
        if (row.PriceVnd < c.MinPriceVnd)
            failures.Add(new Failure(Category.Exclusion, "Giá thấp", $"Giá {Fmt(row.PriceVnd)} đ, dưới {Fmt(c.MinPriceVnd)} đ"));
        if (row.MarketCapBn < c.MinMarketCapBn)
            failures.Add(new Failure(Category.Exclusion, "Vốn hoá nhỏ", $"Vốn hoá {Fmt(row.MarketCapBn)} tỷ, dưới {Fmt(c.MinMarketCapBn)} tỷ"));

        // Tầng 2: chất lượng.
        if (!c.SkipQualityLayer)
        {
            if (row.ProfitAfterTaxBn is null || row.Roe is null)
                failures.Add(new Failure(Category.Quality, "Thiếu dữ liệu chất lượng", "Thiếu dữ liệu lợi nhuận hoặc ROE"));
            else
            {
                if (row.ProfitAfterTaxBn <= 0)
                    failures.Add(new Failure(Category.Quality, "Lợi nhuận sau thuế không dương", "Lợi nhuận sau thuế không dương"));
                if (row.Roe < c.MinRoe)
                    failures.Add(new Failure(Category.Quality, "ROE thấp", $"ROE {Fmt(row.Roe.Value)}%, dưới {Fmt(c.MinRoe)}%"));
            }
        }
        if (c.MaxPe is { } maxPe && !(row.Pe4Q is > 0 && row.Pe4Q <= maxPe))
            failures.Add(new Failure(Category.Quality, "P/E ngoài trần",
                row.Pe4Q is > 0 ? $"P/E {Fmt(row.Pe4Q.Value)}, trên trần {Fmt(maxPe)}" : "P/E không hợp lệ (âm hoặc thiếu) khi đang đặt trần P/E"));

        // Tầng 3: xu hướng. RS là xếp hạng phân vị 1–99, nên "mạnh hơn thị trường" nghĩa là xếp hạng đạt ngưỡng (mặc định 50 = trung vị).
        if (row.Rs3m is null)
            failures.Add(new Failure(Category.Trend, "Thiếu dữ liệu xu hướng", "Thiếu dữ liệu xu hướng: nguồn không có xếp hạng sức mạnh 3 tháng của mã này"));
        else if (row.Rs3m < c.MinRs3m)
            failures.Add(new Failure(Category.Trend, "Sức mạnh giá 3 tháng yếu", $"Xếp hạng sức mạnh 3 tháng {row.Rs3m}/99, dưới ngưỡng P{c.MinRs3m} (trong toàn bộ mã niêm yết)"));

        return failures;
    }

    private static ScreenerPassedRow ToPassed(ScreenerRow row, ScreenerCriteria c)
    {
        var reasons = new List<string>
        {
            $"Thanh khoản đạt (giá trị giao dịch bình quân 10 phiên {Fmt(row.AdtvBn ?? 0)} tỷ)",
            $"Giá {Fmt(row.PriceVnd)} đ, vốn hoá {Fmt(row.MarketCapBn)} tỷ"
        };
        if (!c.SkipQualityLayer) reasons.Add($"Chất lượng: lãi ròng dương, ROE {Fmt(row.Roe ?? 0)}%");
        reasons.Add($"Xu hướng: xếp hạng sức mạnh 3 tháng {row.Rs3m}/99, từ P{c.MinRs3m} trở lên (trong toàn bộ mã niêm yết)");

        // Cờ biến động: dựa trên xếp hạng tương đối, chỉ để người dùng cân nhắc, không loại mã. Mỗi cờ ghi kèm giá trị.
        var flags = new List<string>();
        if (row.Rs1m >= Rs1mHotRank && row.MarketCapBn < SmallCapBn)
            flags.Add($"RS1T {row.Rs1m}/99, vốn hoá nhỏ");
        if (row.Beta > HighBeta)
            flags.Add($"beta cao ({Fmt(row.Beta.Value)})");
        if (row.Rs52w >= Rs52wHotRank && row.MarketCapBn < SmallCapBn)
            flags.Add($"RS52T {row.Rs52w}/99, vốn hoá nhỏ");
        if (row.Rs52w >= Rs52wHotRank && row.Rs1m <= FadedRs1mRank)
            flags.Add($"tăng nóng rồi gãy (RS52T {row.Rs52w}/99, RS1T {row.Rs1m}/99)");

        var notes = new List<string>();
        if (row.Floor == "HNX") notes.Add(HnxNote);
        if (row.Floor == "UPCOM") notes.Add(UpcomNote);

        return new ScreenerPassedRow
        {
            Symbol = row.Symbol, Floor = row.Floor, Sector = row.IcbGroup, AdtvBn = row.AdtvBn, MarketCapBn = row.MarketCapBn,
            PriceVnd = row.PriceVnd, Roe = row.Roe, Pe = row.Pe4Q, Rs3m = row.Rs3m,
            Reasons = reasons, Flags = flags, Notes = notes
        };
    }

    private static string Fmt(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
