using InvestmentApp.Application.Interfaces;
using InvestmentApp.Infrastructure.Repositories;
using InvestmentApp.Infrastructure.Services;
using InvestmentApp.Worker;
using MongoDB.Driver;

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

// Services
builder.Services.AddScoped<IStockPriceService, StockPriceService>();
builder.Services.AddScoped<IPnLService, PnLService>();
builder.Services.AddScoped<IMarketDataProvider, MockMarketDataProvider>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
