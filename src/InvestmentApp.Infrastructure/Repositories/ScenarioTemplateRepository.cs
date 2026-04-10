using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class ScenarioTemplateRepository : IScenarioTemplateRepository
{
    private readonly IMongoCollection<ScenarioTemplate> _collection;

    public ScenarioTemplateRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ScenarioTemplate>("scenario_templates");

        var userIndex = Builders<ScenarioTemplate>.IndexKeys.Ascending(t => t.UserId);
        _collection.Indexes.CreateOneAsync(new CreateIndexModel<ScenarioTemplate>(userIndex));
    }

    public async Task<List<ScenarioTemplate>> GetByUserIdAsync(string userId)
    {
        return await _collection.Find(t => t.UserId == userId)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<ScenarioTemplate?> GetByIdAsync(string id)
    {
        return await _collection.Find(t => t.Id == id).FirstOrDefaultAsync();
    }

    public async Task CreateAsync(ScenarioTemplate template)
    {
        await _collection.InsertOneAsync(template);
    }

    public async Task DeleteAsync(string id)
    {
        await _collection.DeleteOneAsync(t => t.Id == id);
    }
}
