using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Per-user (1:1) aggregate cho tổng quan tài chính cá nhân: tài khoản (CK, tiết kiệm, dự phòng, nhàn rỗi, vàng),
/// chi tiêu hàng tháng, và nguyên tắc tài chính (quỹ dự phòng, cap đầu tư, sàn tiết kiệm). Hỗ trợ health score 0-100.
/// </summary>
public class FinancialProfile : AggregateRoot
{
    public string UserId { get; private set; } = null!;
    public decimal MonthlyExpense { get; private set; }
    public List<FinancialAccount> Accounts { get; private set; } = new();
    public FinancialRules Rules { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    public FinancialProfile() { }

    public static FinancialProfile Create(string userId, decimal monthlyExpense)
    {
        if (userId is null) throw new ArgumentNullException(nameof(userId));
        if (monthlyExpense <= 0m)
            throw new ArgumentOutOfRangeException(nameof(monthlyExpense), "MonthlyExpense phải > 0");

        var profile = new FinancialProfile
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            MonthlyExpense = monthlyExpense,
            Rules = FinancialRules.Default(),
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // 4 default accounts. Gold tạo on-demand khi user có nhu cầu.
        profile.Accounts.Add(FinancialAccount.Create(FinancialAccountType.Securities, "Chứng khoán", 0m));
        profile.Accounts.Add(FinancialAccount.Create(FinancialAccountType.Savings, "Tiết kiệm", 0m));
        profile.Accounts.Add(FinancialAccount.Create(FinancialAccountType.Emergency, "Quỹ dự phòng", 0m));
        profile.Accounts.Add(FinancialAccount.Create(FinancialAccountType.IdleCash, "Tiền nhàn rỗi", 0m));

        return profile;
    }

    public void UpdateMonthlyExpense(decimal amount)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "MonthlyExpense phải > 0");
        MonthlyExpense = amount;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateRules(int? emergencyFundMonths = null, decimal? maxInvestmentPercent = null, decimal? minSavingsPercent = null)
    {
        Rules = Rules.With(emergencyFundMonths, maxInvestmentPercent, minSavingsPercent);
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public FinancialAccount UpsertAccount(
        string? accountId,
        FinancialAccountType type,
        string name,
        decimal balance,
        decimal? interestRate = null,
        string? note = null,
        GoldBrand? goldBrand = null,
        GoldType? goldType = null,
        decimal? goldQuantity = null)
    {
        ValidateAccountFields(type, balance, interestRate, goldBrand, goldType, goldQuantity);

        FinancialAccount account;
        if (accountId is null)
        {
            if (type == FinancialAccountType.Securities && Accounts.Any(a => a.Type == FinancialAccountType.Securities))
                throw new InvalidOperationException("Tài khoản Chứng khoán được tự động tạo khi khởi tạo profile — không cho phép tạo thêm");
            account = FinancialAccount.Create(type, name, balance, interestRate, note, goldBrand, goldType, goldQuantity);
            Accounts.Add(account);
        }
        else
        {
            account = Accounts.FirstOrDefault(a => a.Id == accountId)
                ?? throw new InvalidOperationException($"Không tìm thấy tài khoản với id {accountId}");
            account.Update(type, name, balance, interestRate, note, goldBrand, goldType, goldQuantity);
        }

        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
        return account;
    }

    public void RemoveAccount(string accountId)
    {
        var account = Accounts.FirstOrDefault(a => a.Id == accountId)
            ?? throw new InvalidOperationException($"Không tìm thấy tài khoản với id {accountId}");

        if (account.Type == FinancialAccountType.Securities)
            throw new InvalidOperationException("Tài khoản Chứng khoán không thể xóa thủ công — giá trị tự đồng bộ từ danh mục đầu tư");

        if (account.Balance > 0m)
            throw new InvalidOperationException("Không thể xóa tài khoản có số dư > 0. Hãy đặt số dư về 0 trước khi xóa.");

        Accounts.Remove(account);
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    /// <summary>
    /// Tổng tài sản = sum(balance của tất cả tài khoản non-Securities) + securitiesValue (live từ portfolio).
    /// Securities account.Balance không được dùng — thay bằng securitiesValue param.
    /// </summary>
    public decimal GetTotalAssets(decimal securitiesValue)
    {
        if (securitiesValue < 0m)
            throw new ArgumentOutOfRangeException(nameof(securitiesValue), "securitiesValue phải >= 0");
        var nonSecuritiesBalance = Accounts
            .Where(a => a.Type != FinancialAccountType.Securities)
            .Sum(a => a.Balance);
        return nonSecuritiesBalance + securitiesValue;
    }

    /// <summary>
    /// Health score 0-100. 3 rules, điểm trừ tỷ lệ thuận với mức vi phạm **so với target của rule** (không phải so với totalAssets):
    /// 1. Emergency ≥ MonthlyExpense × EmergencyFundMonths — deficit/required × 40, cap 40
    /// 2. Investment (securities + gold) ≤ MaxInvestmentPercent% × totalAssets — excess/maxInvestment × 30, cap 30
    /// 3. Savings ≥ MinSavingsPercent% × totalAssets — deficit/requiredSavings × 30, cap 30
    /// Nghĩa: vi phạm bằng 100% target → trừ điểm tối đa. Consistent semantics across cả 3 rules.
    /// totalAssets == 0 → return 0 (không đánh giá được).
    /// </summary>
    public int CalculateHealthScore(decimal securitiesValue)
    {
        var totalAssets = GetTotalAssets(securitiesValue);
        if (totalAssets <= 0m) return 0;

        var emergencyTotal = SumBalanceByType(FinancialAccountType.Emergency);
        var savingsTotal = SumBalanceByType(FinancialAccountType.Savings);
        var goldTotal = SumBalanceByType(FinancialAccountType.Gold);
        var investmentTotal = securitiesValue + goldTotal;

        decimal score = 100m;

        // Rule 1: Emergency fund (max -40)
        var requiredEmergency = MonthlyExpense * Rules.EmergencyFundMonths;
        if (requiredEmergency > 0m && emergencyTotal < requiredEmergency)
        {
            var deficitRatio = (requiredEmergency - emergencyTotal) / requiredEmergency;
            score -= Math.Min(40m, deficitRatio * 40m);
        }

        // Rule 2: Investment cap (max -30)
        var maxInvestment = totalAssets * (Rules.MaxInvestmentPercent / 100m);
        if (maxInvestment > 0m && investmentTotal > maxInvestment)
        {
            var excessRatio = (investmentTotal - maxInvestment) / maxInvestment;
            score -= Math.Min(30m, excessRatio * 30m);
        }
        else if (maxInvestment == 0m && investmentTotal > 0m)
        {
            // Edge case: MaxInvestmentPercent=0 → bất kỳ đầu tư nào cũng vi phạm tối đa
            score -= 30m;
        }

        // Rule 3: Savings floor (max -30)
        var requiredSavings = totalAssets * (Rules.MinSavingsPercent / 100m);
        if (requiredSavings > 0m && savingsTotal < requiredSavings)
        {
            var deficitRatio = (requiredSavings - savingsTotal) / requiredSavings;
            score -= Math.Min(30m, deficitRatio * 30m);
        }

        return (int)Math.Clamp(Math.Round(score), 0m, 100m);
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Restore()
    {
        IsDeleted = false;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private decimal SumBalanceByType(FinancialAccountType type) =>
        Accounts.Where(a => a.Type == type).Sum(a => a.Balance);

    private static void ValidateAccountFields(
        FinancialAccountType type,
        decimal balance,
        decimal? interestRate,
        GoldBrand? goldBrand,
        GoldType? goldType,
        decimal? goldQuantity)
    {
        if (balance < 0m)
            throw new ArgumentOutOfRangeException(nameof(balance), "Balance phải >= 0");

        // InterestRate chỉ áp dụng cho Savings
        if (type != FinancialAccountType.Savings && interestRate.HasValue)
            throw new InvalidOperationException("InterestRate chỉ áp dụng cho tài khoản Savings");

        // Gold fields chỉ áp dụng cho Gold type
        if (type != FinancialAccountType.Gold)
        {
            if (goldBrand.HasValue || goldType.HasValue || goldQuantity.HasValue)
                throw new InvalidOperationException("GoldBrand/GoldType/GoldQuantity chỉ áp dụng cho tài khoản Gold");
            return;
        }

        // Type == Gold: 3 Gold field phải all-null (manual mode) hoặc all-set (auto-calc mode)
        var setCount = (goldBrand.HasValue ? 1 : 0)
                     + (goldType.HasValue ? 1 : 0)
                     + (goldQuantity.HasValue ? 1 : 0);
        if (setCount != 0 && setCount != 3)
            throw new InvalidOperationException("GoldBrand/GoldType/GoldQuantity phải all-null (manual) hoặc all-set (auto-calc)");

        if (goldQuantity.HasValue && goldQuantity.Value <= 0m)
            throw new ArgumentOutOfRangeException(nameof(goldQuantity), "GoldQuantity phải > 0");
    }
}
