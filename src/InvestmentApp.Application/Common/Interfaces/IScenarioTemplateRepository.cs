using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

public interface IScenarioTemplateRepository
{
    Task<List<ScenarioTemplate>> GetByUserIdAsync(string userId);
    Task<ScenarioTemplate?> GetByIdAsync(string id);
    Task CreateAsync(ScenarioTemplate template);
    Task DeleteAsync(string id);
}
