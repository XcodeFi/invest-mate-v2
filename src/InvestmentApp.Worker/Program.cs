using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Repositories;
using InvestmentApp.Infrastructure.Services;
using InvestmentApp.Worker;
using InvestmentApp.Worker.Jobs;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

// Register MongoDB conventions and class maps (must run before any DB access)
var conventionPack = new ConventionPack
{
    new IgnoreExtraElementsConvention(true)
};
ConventionRegistry.Register("InvestmentAppConventions", conventionPack, _ => true);

if (!BsonClassMap.IsClassMapRegistered(typeof(AggregateRoot)))
{
    BsonClassMap.RegisterClassMap<AggregateRoot>(cm =>
    {
        cm.SetIsRootClass(true);
        cm.MapIdMember(x => x.Id)
          .SetSerializer(new MongoDB.Bson.Serialization.Serializers.StringSerializer(MongoDB.Bson.BsonType.String));
        cm.MapMember(x => x.Version);
    });
}

var builder = Host.CreateApplicationBuilder(args);

// Configure MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("MongoDb");
    return new MongoClient(connectionString);
});

builder.Services.AddScoped<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var databaseName = builder.Configuration["MongoDb:DatabaseName"];
    return client.GetDatabase(databaseName);
});

// Repositories
builder.Services.AddScoped<IPortfolioRepository, PortfolioRepository>();
builder.Services.AddScoped<ITradeRepository, TradeRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IStockPriceRepository, StockPriceRepository>();
builder.Services.AddScoped<IMarketIndexRepository, MarketIndexRepository>();
builder.Services.AddScoped<ICapitalFlowRepository, CapitalFlowRepository>();
builder.Services.AddScoped<IPortfolioSnapshotRepository, PortfolioSnapshotRepository>();
builder.Services.AddScoped<IStopLossTargetRepository, StopLossTargetRepository>();
builder.Services.AddScoped<IStrategyRepository, StrategyRepository>();
builder.Services.AddScoped<IBacktestRepository, BacktestRepository>();
builder.Services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();

// Services
builder.Services.AddScoped<IStockPriceService, StockPriceService>();
builder.Services.AddScoped<IPnLService, PnLService>();
builder.Services.AddScoped<IMarketDataProvider, MockMarketDataProvider>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<BacktestEngine>();

// Background jobs
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<PriceSnapshotJob>();
builder.Services.AddHostedService<BacktestJob>();
builder.Services.AddHostedService<ExchangeRateJob>();

var host = builder.Build();
host.Run();
