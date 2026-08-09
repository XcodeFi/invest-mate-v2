using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

public interface ICompanyDossierRepository
{
    Task<CompanyDossier?> GetAsync(string userId, string symbol);
    Task<List<CompanyDossier>> GetByUserIdAsync(string userId);
    Task CreateAsync(CompanyDossier dossier);
    Task UpdateAsync(CompanyDossier dossier);
}
