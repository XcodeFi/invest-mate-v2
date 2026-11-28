using System.Globalization;
using System.Text;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.Services;
using InvestmentApp.Infrastructure.Services;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Tests.Tools;

/// <summary>
/// WS2b — công cụ CHỈ ĐỌC. Tính lại mọi kế hoạch đã review bằng <see cref="CampaignReviewService"/> mới và so với số đã lưu.
/// Chỉ dùng <c>Find</c>/<c>CountDocuments</c>: không repository (hàm dựng của repository tạo index = ghi), không có chế độ ghi.
/// Chế độ: <c>count</c> (chỉ đếm), <c>test</c> (từng dòng cho tài khoản test), <c>aggregate</c> (tổng hợp cho người khác, không id/mã).
/// </summary>
internal static class ReviewRecomputeDryRun
{
    internal sealed record Row(
        string ShortId, string Symbol,
        decimal OldPnL, decimal NewPnL, decimal? OldR, decimal? NewR,
        decimal OpenQuantity, decimal DividendIncome, bool Changed, string Reason);

    public static Row Compute(TradePlan plan, IReadOnlyList<Trade> trades, IReadOnlyList<CorporateAction> actions)
    {
        var stored = plan.ReviewData ?? throw new ArgumentException("Kế hoạch chưa review", nameof(plan));
        var oldR = RMath.ResolveRealizedR(plan).R;                 // đường đọc hiện tại: giá trị đã lưu hoặc suy từ PnL đã lưu
        var fresh = new CampaignReviewService().CalculateMetrics(plan, trades, actions);

        var open = fresh.OpenQuantity ?? 0m;
        var dividend = fresh.DividendIncome ?? 0m;
        var changed = Math.Abs(fresh.PnLAmount - stored.PnLAmount) > 0.5m
                      || Math.Abs((fresh.RealizedR ?? 0m) - (oldR ?? 0m)) > 0.0001m
                      || (fresh.RealizedR == null) != (oldR == null)
                      || open > 0m || dividend > 0m;

        var reason = changed ? Classify(plan, trades, fresh, stored, open, dividend) : "-";
        return new Row(plan.Id.Length >= 8 ? plan.Id[..8] : plan.Id, plan.Symbol,
            stored.PnLAmount, fresh.PnLAmount, oldR, fresh.RealizedR, open, dividend, changed, reason);
    }

    /// <summary>Nguyên nhân chính của khác biệt: open-qty | dividend | fees-on-open | symbol-filter | other:&lt;chi tiết&gt;.</summary>
    private static string Classify(TradePlan plan, IReadOnlyList<Trade> trades, CampaignReviewData fresh,
        CampaignReviewData stored, decimal open, decimal dividend)
    {
        bool Mine(Trade t) => string.Equals(t.Symbol, plan.Symbol, StringComparison.OrdinalIgnoreCase);
        if (trades.Any(t => !Mine(t))) return "symbol-filter";
        if (dividend > 0m) return "dividend";

        var bought = trades.Where(t => Mine(t) && t.TradeType == TradeType.BUY).Sum(t => t.Quantity);
        var sold = trades.Where(t => Mine(t) && t.TradeType == TradeType.SELL).Sum(t => t.Quantity);
        if (open > 0m) return sold == 0m ? "fees-on-open" : "open-qty";
        if (sold > bought) return "other:oversell";

        var sellBeforeBuySameDay = trades
            .Select((t, i) => (t, i))
            .Any(s => s.t.TradeType == TradeType.SELL
                      && trades.Select((b, j) => (b, j)).Any(b => b.b.TradeType == TradeType.BUY && b.j > s.i && b.b.TradeDate.Date == s.t.TradeDate.Date));
        if (sellBeforeBuySameDay) return "other:same-day-order";

        return Math.Abs(fresh.PnLAmount - stored.PnLAmount) < 1m ? "other:rounding" : "other";
    }

    private static string Money(decimal v) => v.ToString("N0", CultureInfo.InvariantCulture);
    private static string R(decimal? v) => v.HasValue ? v.Value.ToString("0.00", CultureInfo.InvariantCulture) : "n/a";

    public static string FormatTable(IEnumerable<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("plan     | symbol | PnL cu -> moi                     | R cu -> moi    | open qty | dividend  | changed | reason");
        foreach (var r in rows)
            sb.AppendLine($"{r.ShortId} | {r.Symbol,-6} | {Money(r.OldPnL)} -> {Money(r.NewPnL)} | {R(r.OldR)} -> {R(r.NewR)} | {Money(r.OpenQuantity)} | {Money(r.DividendIncome)} | {(r.Changed ? "YES" : "no")} | {r.Reason}");
        return sb.ToString();
    }

    /// <summary>Danh sách giao dịch gắn với một plan: chỉ ngày giờ, chiều, số lượng, giá, id danh mục rút gọn.</summary>
    public static string FormatTrades(IEnumerable<Trade> trades, bool withPlanLink = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("date                | side | qty       | price    | portfolio" + (withPlanLink ? " | plan" : ""));
        foreach (var t in trades.OrderBy(t => t.TradeDate))
            sb.AppendLine($"{t.TradeDate:yyyy-MM-dd HH:mm:ss} | {t.TradeType,-4} | {Money(t.Quantity)} | {Money(t.Price)} | {(t.PortfolioId.Length >= 8 ? t.PortfolioId[..8] : t.PortfolioId)}"
                          + (withPlanLink ? $" | {(t.TradePlanId is { Length: >= 8 } id ? id[..8] : "-")}" : ""));
        return sb.ToString();
    }

    /// <summary>Chỉ số đếm; không id, không mã, không số tiền theo từng dòng.</summary>
    public static string FormatAggregate(IEnumerable<Row> rows)
    {
        var list = rows.ToList();
        return $"reviewed plans: {list.Count}; changed: {list.Count(r => r.Changed)}; " +
               $"with open quantity: {list.Count(r => r.OpenQuantity > 0)}; with cash dividend: {list.Count(r => r.DividendIncome > 0)}; " +
               $"R became n/a: {list.Count(r => r.OldR != null && r.NewR == null)}";
    }

    private static void RegisterConventions()
    {
        ConventionRegistry.Register("InvestmentAppConventions", new ConventionPack { new IgnoreExtraElementsConvention(true) }, _ => true);
        if (!BsonClassMap.IsClassMapRegistered(typeof(AggregateRoot)))
        {
            BsonClassMap.RegisterClassMap<AggregateRoot>(cm =>
            {
                cm.SetIsRootClass(true);
                cm.MapIdMember(x => x.Id)
                  .SetSerializer(new MongoDB.Bson.Serialization.Serializers.StringSerializer(MongoDB.Bson.BsonType.String));
                cm.MapMember(x => x.Version);
            });
        }
    }

    /// <summary>mode: count | test | aggregate | others (cùng bảng như test nhưng cho user KHÁC tài khoản test; người dùng đã cho phép). Chỉ đọc.</summary>
    public static async Task<string> RunLiveAsync(string conn, string dbName, string mode, string? testEmail, string? planPrefix = null)
    {
        RegisterConventions();
        var db = new MongoClient(conn).GetDatabase(dbName);
        var plansCol = db.GetCollection<TradePlan>("trade_plans");
        var tradesCol = db.GetCollection<Trade>("trades");
        var portfoliosCol = db.GetCollection<Portfolio>("portfolios");
        var actionsCol = db.GetCollection<CorporateAction>("corporate_actions");
        var usersCol = db.GetCollection<User>("users");

        var reviewed = await plansCol.Find(p => p.Status == TradePlanStatus.Reviewed && !p.IsDeleted).ToListAsync();
        var userCount = reviewed.Select(p => p.UserId).Distinct().Count();
        if (mode == "count")
            return $"reviewed plans: {reviewed.Count}; distinct users with reviewed plans: {userCount}";

        if (mode is "trades" or "trades-symbol")
        {
            if (string.IsNullOrWhiteSpace(planPrefix) || planPrefix.Length < 8) throw new InvalidOperationException("Cần DRYRUN_PLAN_ID (ít nhất 8 ký tự đầu)");
            var matches = reviewed.Where(p => p.Id.StartsWith(planPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1) return $"plan prefix matches {matches.Count} reviewed plans";
            var target = matches[0];
            var ownedIds = (await portfoliosCol.Find(p => p.UserId == target.UserId).ToListAsync()).Select(p => p.Id).ToHashSet();
            var linkedIds = new HashSet<string>();
            if (target.TradeId != null) linkedIds.Add(target.TradeId);
            if (target.TradeIds != null) linkedIds.UnionWith(target.TradeIds);
            var linked = new List<Trade>();
            if (linkedIds.Count > 0) linked.AddRange(await tradesCol.Find(t => linkedIds.Contains(t.Id)).ToListAsync());
            foreach (var t in await tradesCol.Find(t => t.TradePlanId == target.Id).ToListAsync())
                if (linked.All(x => x.Id != t.Id)) linked.Add(t);
            linked = linked.Where(t => ownedIds.Contains(t.PortfolioId)).ToList();
            if (mode == "trades") return FormatTrades(linked);

            // trades-symbol: mọi giao dịch cùng mã trong cùng các danh mục, kèm 8 ký tự đầu của plan mà giao dịch được gắn ("-" nếu không)
            var portfolioIds = linked.Select(t => t.PortfolioId).Distinct().ToList();
            var sym = target.Symbol;
            var all = await tradesCol.Find(t => portfolioIds.Contains(t.PortfolioId) && t.Symbol == sym).ToListAsync();
            return FormatTrades(all, withPlanLink: true);
        }

        string? testUserId = null;
        if (!string.IsNullOrWhiteSpace(testEmail))
            testUserId = (await usersCol.Find(u => u.Email == testEmail && !u.IsDeleted).FirstOrDefaultAsync())?.Id;

        IEnumerable<TradePlan> scope = mode switch
        {
            "test" => testUserId == null ? throw new InvalidOperationException("Cần DRYRUN_TEST_EMAIL khớp một user") : reviewed.Where(p => p.UserId == testUserId),
            "aggregate" => reviewed.Where(p => p.UserId != testUserId),
            "others" => testUserId == null ? throw new InvalidOperationException("Cần DRYRUN_TEST_EMAIL để loại tài khoản test") : reviewed.Where(p => p.UserId != testUserId),
            _ => throw new ArgumentException($"Chế độ lạ: {mode}")
        };

        var rows = new List<Row>();
        foreach (var plan in scope)
        {
            var owned = (await portfoliosCol.Find(p => p.UserId == plan.UserId).ToListAsync()).Select(p => p.Id).ToHashSet();
            var ids = new HashSet<string>();
            if (plan.TradeId != null) ids.Add(plan.TradeId);
            if (plan.TradeIds != null) ids.UnionWith(plan.TradeIds);

            var trades = new List<Trade>();
            if (ids.Count > 0)
                trades.AddRange(await tradesCol.Find(t => ids.Contains(t.Id)).ToListAsync());
            foreach (var t in await tradesCol.Find(t => t.TradePlanId == plan.Id).ToListAsync())
                if (trades.All(x => x.Id != t.Id)) trades.Add(t);
            trades = trades.Where(t => owned.Contains(t.PortfolioId)).ToList();   // cùng luật sở hữu với review thật

            var actions = new List<CorporateAction>();
            foreach (var (pid, sym) in trades.Select(t => (t.PortfolioId, Symbol: t.Symbol.ToUpperInvariant())).Distinct())
                actions.AddRange(await actionsCol.Find(a => a.PortfolioId == pid && a.Symbol == sym).ToListAsync());

            rows.Add(Compute(plan, trades, actions));
        }

        return mode is "test" or "others" ? FormatTable(rows) + FormatAggregate(rows) : FormatAggregate(rows);
    }
}
