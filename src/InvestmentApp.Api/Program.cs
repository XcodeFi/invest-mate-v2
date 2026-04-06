using InvestmentApp.Api.Controllers;
using InvestmentApp.Api.Middleware;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Services;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Configuration;
using InvestmentApp.Infrastructure.Persistence;
using InvestmentApp.Infrastructure.Repositories;
using InvestmentApp.Infrastructure.Seed;
using InvestmentApp.Infrastructure.Services;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Serilog;
using Swashbuckle.AspNetCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

// Disable default JWT claim type mapping so "sub" stays as "sub"
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Register MongoDB conventions before any class maps are created
var conventionPack = new ConventionPack
{
    new IgnoreExtraElementsConvention(true)
};
ConventionRegistry.Register("InvestmentAppConventions", conventionPack, _ => true);

// Explicitly register AggregateRoot class map so derived classes don't re-map Id to _id
if (!BsonClassMap.IsClassMapRegistered(typeof(AggregateRoot)))
{
    BsonClassMap.RegisterClassMap<AggregateRoot>(cm =>
    {
        cm.SetIsRootClass(true);
        cm.MapIdMember(x => x.Id)
          .SetSerializer(new MongoDB.Bson.Serialization.Serializers.StringSerializer(MongoDB.Bson.BsonType.String));
        cm.MapMember(x => x.Version);
        // DomainEvents intentionally not mapped
    });
}

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

// Add services to the container
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Disable automatic 400 for model state errors.
        // Server-assigned properties (UserId, Id) are non-nullable in commands
        // but not sent from client — controllers set them from JWT/route params.
        // FluentValidation handles business validation via MediatR pipeline.
        options.SuppressModelStateInvalidFilter = true;
    });
builder.Services.AddEndpointsApiExplorer();

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

// Configure MediatR - scans entire Application assembly for all handlers
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(
        typeof(InvestmentApp.Application.Portfolios.Commands.CreatePortfolio.CreatePortfolioCommand).Assembly
    );
});

// Configure FluentValidation
builder.Services.AddFluentValidationAutoValidation().AddFluentValidationClientsideAdapters();

// Configure Repositories
builder.Services.AddScoped<IPortfolioRepository, PortfolioRepository>();
builder.Services.AddScoped<ITradeRepository, TradeRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IStockPriceRepository, StockPriceRepository>();
builder.Services.AddScoped<IMarketIndexRepository, MarketIndexRepository>();
builder.Services.AddScoped<ICapitalFlowRepository, CapitalFlowRepository>();
builder.Services.AddScoped<IPortfolioSnapshotRepository, PortfolioSnapshotRepository>();
builder.Services.AddScoped<IRiskProfileRepository, RiskProfileRepository>();
builder.Services.AddScoped<IStopLossTargetRepository, StopLossTargetRepository>();
builder.Services.AddScoped<IStrategyRepository, StrategyRepository>();
builder.Services.AddScoped<ITradeJournalRepository, TradeJournalRepository>();
builder.Services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
builder.Services.AddScoped<IAlertHistoryRepository, AlertHistoryRepository>();
builder.Services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
builder.Services.AddScoped<IBacktestRepository, BacktestRepository>();
builder.Services.AddScoped<ITradePlanRepository, TradePlanRepository>();
builder.Services.AddScoped<IDailyRoutineRepository, DailyRoutineRepository>();
builder.Services.AddScoped<IRoutineTemplateRepository, RoutineTemplateRepository>();
builder.Services.AddScoped<IWatchlistRepository, WatchlistRepository>();
builder.Services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
builder.Services.AddScoped<IMarketEventRepository, MarketEventRepository>();

// Configure Services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IStockPriceService, StockPriceService>();
builder.Services.AddScoped<IPnLService, PnLService>();
// In-memory cache for market data
builder.Services.AddMemoryCache();
builder.Services.Configure<InvestmentApp.Infrastructure.Services.Hmoney.MarketDataProviderOptions>(
    builder.Configuration.GetSection("MarketDataProvider"));

// 24hmoney.vn market data provider (BaseUrl + timeout from appsettings.json)
var mdpConfig = builder.Configuration.GetSection("MarketDataProvider");
builder.Services.AddHttpClient<InvestmentApp.Infrastructure.Services.Hmoney.HmoneyMarketDataProvider>(client =>
{
    client.BaseAddress = new Uri(mdpConfig["BaseUrl"]!);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(mdpConfig.GetValue<int>("TimeoutSeconds", 15));
});
builder.Services.AddScoped<IMarketDataProvider>(sp =>
    sp.GetRequiredService<InvestmentApp.Infrastructure.Services.Hmoney.HmoneyMarketDataProvider>());
builder.Services.AddScoped<IStockInfoProvider>(sp =>
    sp.GetRequiredService<InvestmentApp.Infrastructure.Services.Hmoney.HmoneyMarketDataProvider>());
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<ICashFlowAdjustedReturnService, CashFlowAdjustedReturnService>();
builder.Services.AddScoped<IRiskCalculationService, RiskCalculationService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<BacktestEngine>();
builder.Services.AddScoped<ITechnicalIndicatorService, TechnicalIndicatorService>();
builder.Services.AddScoped<InvestmentApp.Application.Common.Interfaces.IBehavioralAnalysisService, BehavioralAnalysisService>();

// Vietstock event crawl provider
builder.Services.AddHttpClient<InvestmentApp.Infrastructure.Services.Vietstock.VietstockEventProvider>(client =>
{
    client.BaseAddress = new Uri("https://finance.vietstock.vn");
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<InvestmentApp.Application.Common.Interfaces.IVietstockEventProvider>(sp =>
    sp.GetRequiredService<InvestmentApp.Infrastructure.Services.Vietstock.VietstockEventProvider>());

// TCBS fundamental data provider — disabled (API down since 2026-03).
// Code kept in Infrastructure/Services/Tcbs/ as fallback if 24hmoney becomes unavailable.
// To re-enable: uncomment the block below and remove the no-op registration.
// var externalApis = builder.Configuration.GetSection("ExternalApis");
// builder.Services.AddHttpClient<InvestmentApp.Infrastructure.Services.Tcbs.TcbsFundamentalDataProvider>(client =>
// {
//     client.BaseAddress = new Uri(externalApis["Tcbs:BaseUrl"]!);
//     client.DefaultRequestHeaders.Add("Accept", "application/json");
//     client.Timeout = TimeSpan.FromSeconds(externalApis.GetValue("Tcbs:TimeoutSeconds", 15));
// });
// builder.Services.AddScoped<IFundamentalDataProvider>(sp =>
//     sp.GetRequiredService<InvestmentApp.Infrastructure.Services.Tcbs.TcbsFundamentalDataProvider>());

// No-op fallback — satisfies DI for AiAssistantService._fundamentalProvider
builder.Services.AddScoped<IFundamentalDataProvider, InvestmentApp.Infrastructure.Services.NoOpFundamentalDataProvider>();

// 24hmoney comprehensive data provider (for AI comprehensive analysis)
builder.Services.AddHttpClient<InvestmentApp.Infrastructure.Services.Hmoney.HmoneyComprehensiveDataProvider>(client =>
{
    client.BaseAddress = new Uri(mdpConfig["BaseUrl"]!);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(mdpConfig.GetValue<int>("TimeoutSeconds", 15));
});
builder.Services.AddScoped<IComprehensiveStockDataProvider>(sp =>
    sp.GetRequiredService<InvestmentApp.Infrastructure.Services.Hmoney.HmoneyComprehensiveDataProvider>());

// Configure Trading Fees
builder.Services.Configure<TradingFeesConfig>(builder.Configuration.GetSection("TradingFees"));
builder.Services.AddScoped<IFeeConfiguration, FeeConfiguration>();
builder.Services.AddScoped<IFeeCalculationService, FeeCalculationService>();
builder.Services.AddScoped<IPerformanceMetricsService, PerformanceMetricsService>();
builder.Services.AddScoped<IStrategyPerformanceService, StrategyPerformanceService>();
builder.Services.AddScoped<IAlertEvaluationService, AlertEvaluationService>();
builder.Services.AddScoped<IScenarioEvaluationService, ScenarioEvaluationService>();
builder.Services.AddTransient<SeedDataService>();

// AI Services
var externalApis = builder.Configuration.GetSection("ExternalApis");
builder.Services.AddScoped<IAiSettingsRepository, AiSettingsRepository>();
builder.Services.AddScoped<IAiKeyEncryptionService, AiKeyEncryptionService>();
builder.Services.AddHttpClient<ClaudeApiService>(client =>
{
    client.BaseAddress = new Uri(externalApis["Anthropic:BaseUrl"]!);
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient<GeminiApiService>(client =>
{
    client.BaseAddress = new Uri(externalApis["Google:BaseUrl"]!);
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddScoped<IAiChatServiceFactory, AiChatServiceFactory>();
builder.Services.AddScoped<IAiAssistantService, AiAssistantService>();

// Configure Data Protection — persist keys to MongoDB so they survive Cloud Run restarts/deploys
var dpMongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDb"));
var dpMongoDb = dpMongoClient.GetDatabase(builder.Configuration["MongoDb:DatabaseName"] ?? "InvestmentApp");
var mongoXmlRepository = new InvestmentApp.Infrastructure.Services.MongoDbXmlRepository(dpMongoDb);
builder.Services.AddDataProtection()
    .SetApplicationName("InvestmentApp");
// PostConfigure runs AFTER all Configure<T> registrations, ensuring it overrides the default FileSystem repo
builder.Services.PostConfigure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(options =>
{
    options.XmlRepository = mongoXmlRepository;
});

// Configure Authentication & Authorization
var isDevelopment = builder.Environment.IsDevelopment();
var cookieSecurePolicy = isDevelopment ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
var cookieSameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.SameSite = cookieSameSite;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.HttpOnly = true;
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["GoogleOAuth:ClientId"]!;
    options.ClientSecret = builder.Configuration["GoogleOAuth:ClientSecret"]!;
    options.CallbackPath = "/api/v1/auth/google/callback";

    options.Scope.Add("profile");
    options.Scope.Add("email");

    options.SaveTokens = true;

    options.CorrelationCookie.SameSite = cookieSameSite;
    options.CorrelationCookie.SecurePolicy = cookieSecurePolicy;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    var jwtKey = builder.Configuration["Jwt:Key"]!;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = cookieSameSite;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = ".AspNetCore.Cookies";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Investment App API", Version = "v1" });

    // Use fully qualified type names to avoid schema ID conflicts
    c.CustomSchemaIds(type => type.FullName);

    // Add JWT Bearer token support
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure CORS
var corsOrigins = (builder.Configuration["CorsOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowCredentials()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Seed template data on startup
using (var scope = app.Services.CreateScope())
{
    var seedService = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await seedService.SeedAllAsync();
}

// Configure the HTTP request pipeline
// Note: ForwardedHeaders handled via ASPNETCORE_FORWARDEDHEADERS_ENABLED env var on Cloud Run

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var enableSwagger = app.Configuration.GetValue<bool>("EnableSwagger", app.Environment.IsDevelopment());
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = app.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
    Secure = app.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always
});
app.UseCors("AllowAll");

// Add custom middleware
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoints
app.MapGet("/health", async (IMongoClient mongo) =>
{
    try
    {
        await mongo.GetDatabase("admin").RunCommandAsync<MongoDB.Bson.BsonDocument>(
            new MongoDB.Bson.BsonDocument("ping", 1));
        return Results.Ok(new { status = "healthy", db = "connected", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", db = "disconnected", error = ex.Message, timestamp = DateTime.UtcNow },
            statusCode: 503);
    }
});

app.MapGet("/health/live", () => Results.Ok(new { status = "alive", timestamp = DateTime.UtcNow }));
app.MapGet("/health/ready", async (IMongoClient mongo) =>
{
    try
    {
        await mongo.GetDatabase("admin").RunCommandAsync<MongoDB.Bson.BsonDocument>(
            new MongoDB.Bson.BsonDocument("ping", 1));
        return Results.Ok(new { status = "ready", timestamp = DateTime.UtcNow });
    }
    catch
    {
        return Results.Json(new { status = "not ready", timestamp = DateTime.UtcNow }, statusCode: 503);
    }
});

app.Run();
