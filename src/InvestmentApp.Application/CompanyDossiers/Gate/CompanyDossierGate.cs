using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.CompanyDossiers.Gate;

public class CompanyDossierGate : ICompanyDossierGate
{
    private const int LargeBusinessModelMinChars = 30;
    private const int LargeMoatMinChars = 30;
    private const int LargeRiskFactorMinCount = 3;
    private const int LargeSignalMinChars = 20;

    private readonly ICompanyDossierRepository _repo;

    public CompanyDossierGate(ICompanyDossierRepository repo) => _repo = repo;

    public async Task<DossierGateResult> EvaluateAsync(string userId, string symbol,
        decimal planSize, decimal? accountBalance, CancellationToken ct)
    {
        var dossier = await _repo.GetAsync(userId, symbol);
        if (dossier is null) return DossierGateResult.Fail("missing");

        switch (dossier.GetFreshness(DateTime.UtcNow))
        {
            case DossierFreshness.Unconfirmed: return DossierGateResult.Fail("unconfirmed");
            case DossierFreshness.Expired: return DossierGateResult.Fail("expired");
        }

        // Guard `> 0` là bắt buộc để khớp TradePlan.EnsureDisciplineGate:171.
        // Thiếu nó thì AccountBalance = 0 cho threshold = 0, mọi lệnh >= 0 nên
        // MỌI lệnh rơi vào tầng lớn — trong khi số dư 0 nghĩa là chưa biết gì, đúng như null.
        var requireFull = accountBalance.HasValue
            && accountBalance.Value > 0m
            && planSize >= accountBalance.Value * TradePlan.LargeTierThreshold;

        var missing = requireFull ? CheckLarge(dossier) : CheckSmall(dossier);

        return missing.Count == 0
            ? DossierGateResult.Ok()
            : DossierGateResult.Fail("insufficient", missing.ToArray());
    }

    public async Task EnsureAsync(string userId, string symbol,
        decimal planSize, decimal? accountBalance, CancellationToken ct)
    {
        var result = await EvaluateAsync(userId, symbol, planSize, accountBalance, ct);
        if (!result.Passed)
            throw new DossierGateException(symbol.Trim().ToUpperInvariant(), result);
    }

    private static List<string> CheckSmall(CompanyDossier d)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(d.BusinessModel))
            missing.Add("businessModel: cần ít nhất một câu, đang để trống");

        if (!d.Moats.Any(m => !string.IsNullOrWhiteSpace(m.Description)))
            missing.Add("moats: cần ≥ 1 moat có mô tả, đang có 0");

        if (d.RiskFactors.Count == 0)
            missing.Add("riskFactors: cần ≥ 1, đang có 0");

        return missing;
    }

    private static List<string> CheckLarge(CompanyDossier d)
    {
        var missing = new List<string>();

        if (d.BusinessModel.Length < LargeBusinessModelMinChars)
            missing.Add($"businessModel: cần ≥ {LargeBusinessModelMinChars} ký tự, đang có {d.BusinessModel.Length}");

        if (!d.Moats.Any(m => m.Description.Length >= LargeMoatMinChars))
        {
            var longest = d.Moats.Count == 0 ? 0 : d.Moats.Max(m => m.Description.Length);
            missing.Add($"moats: cần ít nhất 1 moat mô tả ≥ {LargeMoatMinChars} ký tự, dài nhất đang có {longest}");
        }

        if (d.RiskFactors.Count < LargeRiskFactorMinCount)
            missing.Add($"riskFactors: cần ≥ {LargeRiskFactorMinCount}, đang có {d.RiskFactors.Count}");

        // Nêu luôn độ dài hiện tại của từng cái thiếu — nói "chưa đủ" mà không nói
        // thiếu bao nhiêu thì người dùng phải đoán.
        var shortSignals = d.RiskFactors
            .Where(r => r.ObservableSignal.Length < LargeSignalMinChars)
            .Select(r => $"hạng {r.Rank} ({r.ObservableSignal.Length} ký tự)")
            .ToList();

        if (shortSignals.Count > 0)
            missing.Add($"observableSignal: cần ≥ {LargeSignalMinChars} ký tự ở yếu tố {string.Join(", ", shortSignals)}");

        // RiskFactor.Description cũng chỉ bắt buộc ObservableSignal ở entity, không bắt
        // buộc Description — cùng lỗ hổng như moat. Không đổi câu đếm ở trên (test khác
        // đã ghim), thêm cảnh báo riêng cho hạng có mô tả rỗng.
        var emptyDescriptionRanks = d.RiskFactors
            .Where(r => string.IsNullOrWhiteSpace(r.Description))
            .Select(r => r.Rank.ToString())
            .ToList();

        if (emptyDescriptionRanks.Count > 0)
            missing.Add($"riskFactors: cần mô tả ở mọi yếu tố, đang để trống ở hạng {string.Join(", ", emptyDescriptionRanks)}");

        return missing;
    }
}
