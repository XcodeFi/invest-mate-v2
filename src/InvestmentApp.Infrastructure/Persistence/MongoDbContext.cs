using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Persistence;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDB");
        var databaseName = configuration.GetValue<string>("MongoDB:DatabaseName") ?? "investmentdb";

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoDatabase Database => _database;
}