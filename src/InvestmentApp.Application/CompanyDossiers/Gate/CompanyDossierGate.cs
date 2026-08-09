using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.CompanyDossiers.Gate;

public class CompanyDossierGate : ICompanyDossierGate
{
    private const decimal LargeTierThreshold = 0.05m;
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

        var requireFull = accountBalance.HasValue
            && planSize >= accountBalance.Value * LargeTierThreshold;

        var missing = requireFull ? CheckLarge(dossier) : CheckSmall(dossier);

        return missing.Count == 0
            ? DossierGateResult.Ok()
            : new DossierGateResult(false, "insufficient", missing);
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

        if (d.Moats.Count == 0)
            missing.Add("moats: cần ≥ 1, đang có 0");

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
            missing.Add($"moats: cần ít nhất 1 moat mô tả ≥ {LargeMoatMinChars} ký tự");

        if (d.RiskFactors.Count < LargeRiskFactorMinCount)
            missing.Add($"riskFactors: cần ≥ {LargeRiskFactorMinCount}, đang có {d.RiskFactors.Count}");

        var shortSignals = d.RiskFactors
            .Where(r => r.ObservableSignal.Length < LargeSignalMinChars)
            .Select(r => r.Rank)
            .ToList();

        if (shortSignals.Count > 0)
            missing.Add($"observableSignal: cần ≥ {LargeSignalMinChars} ký tự ở yếu tố hạng {string.Join(", ", shortSignals)}");

        return missing;
    }
}
