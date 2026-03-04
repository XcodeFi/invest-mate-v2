using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;

namespace InvestmentApp.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _collection;

    public UserRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<User>("users");

        // Create unique index on email
        var emailIndex = Builders<User>.IndexKeys.Ascending(u => u.Email);
        _collection.Indexes.CreateOne(new CreateIndexModel<User>(emailIndex, new CreateIndexOptions { Unique = true }));
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, id),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false)
        );
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<User>.Filter.Eq(u => u.IsDeleted, false);
        return await _collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Email, email),
            Builders<User>.Filter.Eq(u => u.IsDeleted, false)
        );
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(entity, null, cancellationToken);
    }

    public async Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, entity.Id);
        await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, id);
        var update = Builders<User>.Update.Set(u => u.IsDeleted, true);
        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}