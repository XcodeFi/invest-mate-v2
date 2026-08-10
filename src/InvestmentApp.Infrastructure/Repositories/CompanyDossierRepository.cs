using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class CompanyDossierRepository : ICompanyDossierRepository
{
    private readonly IMongoCollection<CompanyDossier> _collection;

    public CompanyDossierRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CompanyDossier>("company_dossiers");

        // Một hồ sơ cho mỗi mã, cho mỗi người dùng. Cả hai field luôn có giá trị
        // nên KHÔNG cần sparse (sparse + unique bỏ qua doc absent, không bỏ qua null).
        var keys = Builders<CompanyDossier>.IndexKeys
            .Ascending(d => d.UserId)
            .Ascending(d => d.Symbol);
        _collection.Indexes.CreateOne(new CreateIndexModel<CompanyDossier>(
            keys,
            new CreateIndexOptions { Unique = true, Name = "ux_user_symbol" }));
    }

    public async Task<CompanyDossier?> GetAsync(string userId, string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        return await _collection
            .Find(d => d.UserId == userId && d.Symbol == normalized)
            .FirstOrDefaultAsync();
    }

    public async Task<List<CompanyDossier>> GetByUserIdAsync(string userId)
        => await _collection.Find(d => d.UserId == userId)
            .SortBy(d => d.Symbol)
            .ToListAsync();

    public async Task CreateAsync(CompanyDossier dossier)
        => await _collection.InsertOneAsync(dossier);

    public async Task UpdateAsync(CompanyDossier dossier)
        => await _collection.ReplaceOneAsync(d => d.Id == dossier.Id, dossier);
}
