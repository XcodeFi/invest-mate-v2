using InvestmentApp.Api.Controllers;
using InvestmentApp.Api.Middleware;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Services;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Configuration;
using InvestmentApp.Infrastructure.Persistence;
using InvestmentApp.Infrastructure.Repositories;
using InvestmentApp.Infrastructure.Services;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication;
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
using System.Text;

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
builder.Services.AddControllers();
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

// Configure Services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IStockPriceService, StockPriceService>();
builder.Services.AddScoped<IPnLService, PnLService>();
builder.Services.AddScoped<IMarketDataProvider, MockMarketDataProvider>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<ICashFlowAdjustedReturnService, CashFlowAdjustedReturnService>();
builder.Services.AddScoped<IRiskCalculationService, RiskCalculationService>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();
builder.Services.AddScoped<BacktestEngine>();

// Configure Trading Fees
builder.Services.Configure<TradingFeesConfig>(builder.Configuration.GetSection("TradingFees"));
builder.Services.AddScoped<IFeeConfiguration, FeeConfiguration>();
builder.Services.AddScoped<IFeeCalculationService, FeeCalculationService>();
builder.Services.AddScoped<IPerformanceMetricsService, PerformanceMetricsService>();
builder.Services.AddScoped<IStrategyPerformanceService, StrategyPerformanceService>();
builder.Services.AddScoped<IAlertEvaluationService, AlertEvaluationService>();

// Configure Data Protection for OAuth state cookies
builder.Services.AddDataProtection()
    .SetApplicationName("InvestmentApp");

// Configure Authentication & Authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    // Configure cookie for OAuth flow in development (HTTP)
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
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

    // Configure correlation and state cookies for HTTP development
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
})
// TODO: Revert JwtBearer config after testing - currently bypasses token validation
.AddJwtBearer(options =>
{
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var claims = new[] {
                new System.Security.Claims.Claim("sub", "test-user-id"),
                new System.Security.Claims.Claim("email", "test@test.com")
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme);
            context.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
            context.Success();
            return Task.CompletedTask;
        }
    };
});

builder.Services.ConfigureApplicationCookie(options =>
{
    // Configure for HTTP development environment
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowCredentials()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Commented out for HTTP development
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Lax,
    Secure = CookieSecurePolicy.SameAsRequest
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
