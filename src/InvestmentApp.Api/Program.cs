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
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Serilog;
using Swashbuckle.AspNetCore;
using System.Text;

// Register MongoDB class map for AggregateRoot base class
// This prevents the duplicate '_id' mapping issue in MongoDB Driver v3 with inheritance
if (!BsonClassMap.IsClassMapRegistered(typeof(InvestmentApp.Domain.Entities.AggregateRoot)))
{
    BsonClassMap.RegisterClassMap<InvestmentApp.Domain.Entities.AggregateRoot>(cm =>
    {
        cm.AutoMap();
        cm.MapIdProperty(c => c.Id);
        cm.SetIsRootClass(true);
        cm.SetIgnoreExtraElements(true);
        // Exclude domain events list from serialization
        cm.UnmapProperty(c => c.DomainEvents);
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

// Configure Services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IStockPriceService, StockPriceService>();
builder.Services.AddScoped<IPnLService, PnLService>();
builder.Services.AddScoped<IMarketDataProvider, MockMarketDataProvider>();
builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<ICashFlowAdjustedReturnService, CashFlowAdjustedReturnService>();
builder.Services.AddScoped<IRiskCalculationService, RiskCalculationService>();

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
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
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

// Add simple health check endpoint
app.MapGet("/health", () => "API is running!");

app.Run();
