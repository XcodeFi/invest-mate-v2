using System.Reflection;
using System.Text.Json;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Seed;

public class SeedDataService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<SeedDataService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SeedDataService(IMongoDatabase database, ILogger<SeedDataService> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task SeedAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting seed data initialization...");

        await SeedStrategyTemplatesAsync(cancellationToken);
        await SeedRiskProfileTemplatesAsync(cancellationToken);

        _logger.LogInformation("Seed data initialization completed.");
    }

    private async Task SeedStrategyTemplatesAsync(CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<StrategyTemplate>("strategy_templates");

        var existingCount = await collection.CountDocumentsAsync(
            FilterDefinition<StrategyTemplate>.Empty, cancellationToken: cancellationToken);

        if (existingCount > 0)
        {
            _logger.LogInformation("Strategy templates already seeded ({Count} found). Skipping.", existingCount);
            return;
        }

        var templates = LoadEmbeddedJson<List<StrategyTemplate>>("strategy_templates.json");
        if (templates is { Count: > 0 })
        {
            await collection.InsertManyAsync(templates, cancellationToken: cancellationToken);
            _logger.LogInformation("Seeded {Count} strategy templates.", templates.Count);

            // Create indexes
            var indexKeys = Builders<StrategyTemplate>.IndexKeys;
            await collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<StrategyTemplate>(indexKeys.Ascending(t => t.Category)),
                new CreateIndexModel<StrategyTemplate>(indexKeys.Ascending(t => t.DifficultyLevel)),
                new CreateIndexModel<StrategyTemplate>(indexKeys.Ascending(t => t.TimeFrame)),
                new CreateIndexModel<StrategyTemplate>(indexKeys.Ascending(t => t.SortOrder))
            }, cancellationToken);
        }
    }

    private async Task SeedRiskProfileTemplatesAsync(CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<RiskProfileTemplate>("risk_profile_templates");

        var existingCount = await collection.CountDocumentsAsync(
            FilterDefinition<RiskProfileTemplate>.Empty, cancellationToken: cancellationToken);

        if (existingCount > 0)
        {
            _logger.LogInformation("Risk profile templates already seeded ({Count} found). Skipping.", existingCount);
            return;
        }

        var templates = LoadEmbeddedJson<List<RiskProfileTemplate>>("risk_profile_templates.json");
        if (templates is { Count: > 0 })
        {
            await collection.InsertManyAsync(templates, cancellationToken: cancellationToken);
            _logger.LogInformation("Seeded {Count} risk profile templates.", templates.Count);

            var indexKeys = Builders<RiskProfileTemplate>.IndexKeys;
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<RiskProfileTemplate>(indexKeys.Ascending(t => t.SortOrder)),
                cancellationToken: cancellationToken);
        }
    }

    private static T? LoadEmbeddedJson<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            throw new FileNotFoundException($"Embedded resource '{fileName}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }
}
