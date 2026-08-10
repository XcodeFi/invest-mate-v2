using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.CompanyDossiers.DTOs;

public class CompanyDossierDto
{
    public string Symbol { get; set; } = string.Empty;
    public string BusinessModel { get; set; } = string.Empty;
    public List<MoatItem> Moats { get; set; } = new();
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime ReviewedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? AgentDraftedAt { get; set; }

    /// <summary>"Unconfirmed" | "Fresh" | "NeedsReview" | "Expired" — từ dossier.GetFreshness().ToString().</summary>
    public string Freshness { get; set; } = string.Empty;

    public static CompanyDossierDto FromEntity(CompanyDossier dossier, DateTime utcNow) => new()
    {
        Symbol = dossier.Symbol,
        BusinessModel = dossier.BusinessModel,
        Moats = dossier.Moats,
        RiskFactors = dossier.RiskFactors,
        Notes = dossier.Notes,
        ReviewedAt = dossier.ReviewedAt,
        ConfirmedAt = dossier.ConfirmedAt,
        AgentDraftedAt = dossier.AgentDraftedAt,
        Freshness = dossier.GetFreshness(utcNow).ToString()
    };
}

public class DossierGateStatusDto
{
    public string Symbol { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Reason { get; set; }
    public List<string> Missing { get; set; } = new();

    /// <summary>"Unconfirmed" | "Fresh" | "NeedsReview" | "Expired" | null khi chưa có hồ sơ.</summary>
    public string? Freshness { get; set; }
}
