# 🤖 AI Agent Guide - Investment Mate v2 Enterprise

Hướng dẫn toàn diện cho AI agents trong việc phát triển và maintain hệ thống Investment Mate v2 - một nền tảng quản lý danh mục đầu tư doanh nghiệp enterprise-grade.

## 📋 Thông tin Tổng quan

### Mục tiêu Dự án
Xây dựng hệ thống quản lý danh mục đầu tư doanh nghiệp với:
- **Scalability**: Hỗ trợ hàng triệu users và transactions
- **Reliability**: 99.9% uptime với comprehensive error handling
- **Security**: Enterprise-grade security với audit trails
- **Performance**: Sub-second response times với optimized queries
- **Maintainability**: Clean Architecture với comprehensive testing

### Phạm vi Business
- **Portfolio Management**: Tạo và quản lý danh mục đầu tư
- **Trade Execution**: Ghi nhận giao dịch mua/bán chứng khoán
- **P&L Calculation**: Tính toán lãi/lỗ real-time theo average cost method
- **Reporting**: Báo cáo hiệu suất và analytics
- **User Management**: Xác thực và phân quyền users

## 🏗️ Kiến trúc Enterprise

### Clean Architecture Implementation

```
src/
├── InvestmentApp.Api/           # 🚪 API Gateway Layer
│   ├── Controllers/             # REST API endpoints
│   ├── Middleware/              # Cross-cutting concerns
│   ├── Filters/                 # Action filters
│   └── DTOs/                    # Data transfer objects
│
├── InvestmentApp.Application/   # 🔄 Application Services Layer
│   ├── Commands/                # CQRS Commands
│   ├── Queries/                 # CQRS Queries
│   ├── Validators/              # Input validation
│   ├── Interfaces/              # Service contracts
│   └── Services/                # Application services
│
├── InvestmentApp.Domain/        # 🎯 Domain Core Layer
│   ├── Entities/                # Aggregate roots & entities
│   ├── ValueObjects/            # Value objects
│   ├── Events/                  # Domain events
│   ├── Exceptions/              # Domain exceptions
│   └── Interfaces/              # Domain services
│
├── InvestmentApp.Infrastructure/# 🛠️ Infrastructure Layer
│   ├── Persistence/             # Database implementation
│   ├── Repositories/            # Data access
│   ├── Services/                # External services
│   └── Configurations/          # Infrastructure config
│
└── InvestmentApp.Worker/        # ⚙️ Background Processing Layer
    ├── Services/                # Background services
    ├── Jobs/                    # Scheduled jobs
    └── Handlers/                # Event handlers
```

### Design Principles

#### 1. Dependency Inversion
- High-level modules không phụ thuộc vào low-level modules
- Cả hai phụ thuộc vào abstractions
- Interfaces định nghĩa trong Application layer
- Implementations trong Infrastructure layer

#### 2. Single Responsibility
- Mỗi class có một lý do duy nhất để thay đổi
- Domain entities chứa business logic
- Application services orchestrate use cases
- Infrastructure services handle external concerns

#### 3. CQRS Pattern
- Commands: Thay đổi state (Create, Update, Delete)
- Queries: Đọc data (Get, List, Search)
- Separate models cho write vs read operations
- Eventual consistency giữa command và query models

## 🔧 Technology Stack Enterprise

### Backend (.NET 8 Enterprise)
```xml
<!-- InvestmentApp.Api.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>enterprise-investment-app</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <!-- Enterprise Packages -->
    <PackageReference Include="MediatR" Version="12.1.1" />
    <PackageReference Include="FluentValidation" Version="11.7.1" />
    <PackageReference Include="MongoDB.Driver" Version="2.22.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.1" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="8.0.1" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="8.0.1" />
  </ItemGroup>
</Project>
```

### Database (MongoDB Enterprise)
```javascript
// Database Configuration
{
  "replicaSet": "investment-rs",
  "sharding": true,
  "authentication": "SCRAM-SHA-256",
  "encryption": "AES256",
  "auditLog": {
    "enabled": true,
    "destination": "file"
  }
}
```

### Infrastructure (Docker Enterprise)
```yaml
# docker-compose.prod.yml
version: '3.8'
services:
  api:
    image: investment-api:latest
    replicas: 3
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    secrets:
      - mongodb-connection
      - jwt-secret
      - google-oauth
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  mongodb:
    image: mongo:7.0-enterprise
    volumes:
      - mongodb_data:/data/db
      - mongodb_audit:/data/audit
    secrets:
      - mongodb-keyfile
    command: --replSet investment-rs --keyFile /run/secrets/mongodb-keyfile

  redis:
    image: redis:7.2-alpine
    command: redis-server --requirepass $REDIS_PASSWORD
```

## 📊 Domain Model Enterprise

### Core Aggregates

#### Portfolio Aggregate
```csharp
public class Portfolio : AggregateRoot
{
    private readonly List<Trade> _trades = new();
    public IReadOnlyCollection<Trade> Trades => _trades.AsReadOnly();

    public string UserId { get; private set; }
    public string Name { get; private set; }
    public Money InitialCapital { get; private set; }
    public PortfolioStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Portfolio() { } // EF Core

    public Portfolio(string userId, string name, Money initialCapital)
    {
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        InitialCapital = initialCapital ?? throw new ArgumentNullException(nameof(initialCapital));
        Status = PortfolioStatus.Active;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new PortfolioCreatedEvent(this));
    }

    public void AddTrade(Trade trade)
    {
        if (trade == null) throw new ArgumentNullException(nameof(trade));
        if (Status != PortfolioStatus.Active)
            throw new DomainException("Cannot add trades to inactive portfolio");

        _trades.Add(trade);
        AddDomainEvent(new TradeAddedToPortfolioEvent(this, trade));
    }

    public void ClosePortfolio()
    {
        if (Status != PortfolioStatus.Active)
            throw new DomainException("Portfolio is not active");

        Status = PortfolioStatus.Closed;
        AddDomainEvent(new PortfolioClosedEvent(this));
    }
}
```

#### Trade Aggregate
```csharp
public class Trade : Entity
{
    public string PortfolioId { get; private set; }
    public StockSymbol Symbol { get; private set; }
    public TradeType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public Money Price { get; private set; }
    public Money Fee { get; private set; }
    public Money Tax { get; private set; }
    public DateTime TradeDate { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Trade() { } // EF Core

    public Trade(string portfolioId, StockSymbol symbol, TradeType type,
                decimal quantity, Money price, Money fee = null, Money tax = null)
    {
        PortfolioId = portfolioId ?? throw new ArgumentNullException(nameof(portfolioId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        Type = type;
        Quantity = quantity > 0 ? quantity : throw new DomainException("Quantity must be positive");
        Price = price ?? throw new ArgumentNullException(nameof(price));
        Fee = fee ?? Money.Zero(price.Currency);
        Tax = tax ?? Money.Zero(price.Currency);
        TradeDate = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }
}
```

### Value Objects

#### Money (Enterprise Implementation)
```csharp
public class Money : IEquatable<Money>
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }

    private static readonly HashSet<string> SupportedCurrencies = new()
    {
        "USD", "EUR", "GBP", "JPY", "CAD", "AUD"
    };

    public static Money Zero(string currency = "USD") => new(0, currency);

    private Money() { } // EF Core

    public Money(decimal amount, string currency)
    {
        if (!SupportedCurrencies.Contains(currency))
            throw new DomainException($"Unsupported currency: {currency}");

        Amount = Math.Round(amount, 2);
        Currency = currency;
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("Cannot add money with different currencies");

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("Cannot subtract money with different currencies");

        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor)
    {
        return new Money(Amount * factor, Currency);
    }

    public override bool Equals(object? obj) => Equals(obj as Money);

    public bool Equals(Money? other)
    {
        if (other is null) return false;
        return Amount == other.Amount && Currency == other.Currency;
    }

    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    public static bool operator ==(Money left, Money right) => left.Equals(right);
    public static bool operator !=(Money left, Money right) => !left.Equals(right);
    public static Money operator +(Money left, Money right) => left.Add(right);
    public static Money operator -(Money left, Money right) => left.Subtract(right);
}
```

#### StockSymbol
```csharp
public class StockSymbol : IEquatable<StockSymbol>
{
    private static readonly Regex SymbolRegex = new(@"^[A-Z]{1,5}(\.[A-Z]{1,2})?$",
        RegexOptions.Compiled);

    public string Value { get; private set; }

    private StockSymbol() { } // EF Core

    public StockSymbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Stock symbol cannot be empty");

        value = value.ToUpperInvariant().Trim();

        if (!SymbolRegex.IsMatch(value))
            throw new DomainException($"Invalid stock symbol format: {value}");

        Value = value;
    }

    public override bool Equals(object? obj) => Equals(obj as StockSymbol);

    public bool Equals(StockSymbol? other)
    {
        if (other is null) return false;
        return Value == other.Value;
    }

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(StockSymbol left, StockSymbol right) => left.Equals(right);
    public static bool operator !=(StockSymbol left, StockSymbol right) => !left.Equals(right);

    public override string ToString() => Value;
}
```

## 🔄 CQRS Implementation Enterprise

### Command Pipeline
```csharp
// Base Command
public abstract class BaseCommand : IRequest<Result>
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = null!;
}

// CreatePortfolio Command
public class CreatePortfolioCommand : BaseCommand
{
    public string Name { get; set; } = null!;
    public decimal InitialCapital { get; set; }
    public string Currency { get; set; } = "USD";
}

// Command Handler with Enterprise Features
public class CreatePortfolioCommandHandler : IRequestHandler<CreatePortfolioCommand, Result<string>>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IAuditService _auditService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public CreatePortfolioCommandHandler(
        IPortfolioRepository portfolioRepository,
        IAuditService auditService,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        _portfolioRepository = portfolioRepository;
        _auditService = auditService;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task<Result<string>> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate business rules
            var existingPortfolio = await _portfolioRepository.GetByUserIdAndNameAsync(
                request.UserId, request.Name, cancellationToken);

            if (existingPortfolio != null)
                return Result.Failure<string>("Portfolio with this name already exists");

            // Create domain object
            var portfolio = new Portfolio(
                request.UserId,
                request.Name,
                new Money(request.InitialCapital, request.Currency));

            // Persist
            await _portfolioRepository.AddAsync(portfolio, cancellationToken);

            // Audit
            await _auditService.LogAsync(
                entityType: "Portfolio",
                entityId: portfolio.Id,
                action: "Created",
                details: $"Portfolio '{request.Name}' created with ${request.InitialCapital}",
                userId: request.UserId,
                correlationId: _correlationIdAccessor.CorrelationId);

            return Result.Success(portfolio.Id);
        }
        catch (DomainException ex)
        {
            await _auditService.LogAsync(
                entityType: "Portfolio",
                entityId: string.Empty,
                action: "CreateFailed",
                details: $"Domain validation failed: {ex.Message}",
                userId: request.UserId,
                correlationId: _correlationIdAccessor.CorrelationId);

            return Result.Failure<string>(ex.Message);
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(
                entityType: "Portfolio",
                entityId: string.Empty,
                action: "CreateError",
                details: $"Unexpected error: {ex.Message}",
                userId: request.UserId,
                correlationId: _correlationIdAccessor.CorrelationId);

            throw;
        }
    }
}
```

### Query Implementation
```csharp
// Query with Pagination
public class GetPortfoliosQuery : IRequest<Result<PaginatedList<PortfolioDto>>>
{
    public string UserId { get; set; } = null!;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Descending;
}

// Query Handler
public class GetPortfoliosQueryHandler : IRequestHandler<GetPortfoliosQuery, Result<PaginatedList<PortfolioDto>>>
{
    private readonly IPortfolioRepository _portfolioRepository;

    public GetPortfoliosQueryHandler(IPortfolioRepository portfolioRepository)
    {
        _portfolioRepository = portfolioRepository;
    }

    public async Task<Result<PaginatedList<PortfolioDto>>> Handle(GetPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var portfolios = await _portfolioRepository.GetByUserIdAsync(
            request.UserId,
            request.PageNumber,
            request.PageSize,
            request.SortBy,
            request.SortDirection,
            cancellationToken);

        var portfolioDtos = portfolios.Items.Select(MapToDto).ToList();

        return Result.Success(new PaginatedList<PortfolioDto>(
            portfolioDtos,
            portfolios.TotalCount,
            request.PageNumber,
            request.PageSize));
    }

    private PortfolioDto MapToDto(Portfolio portfolio)
    {
        return new PortfolioDto
        {
            Id = portfolio.Id,
            Name = portfolio.Name,
            InitialCapital = portfolio.InitialCapital.Amount,
            Currency = portfolio.InitialCapital.Currency,
            Status = portfolio.Status.ToString(),
            CreatedAt = portfolio.CreatedAt,
            TradeCount = portfolio.Trades.Count
        };
    }
}
```

## 🗄️ MongoDB Enterprise Implementation

### Repository Pattern Enterprise
```csharp
public interface IPortfolioRepository : IRepository<Portfolio>
{
    Task<Portfolio?> GetByUserIdAndNameAsync(string userId, string name, CancellationToken cancellationToken = default);
    Task<PaginatedList<Portfolio>> GetByUserIdAsync(string userId, int pageNumber, int pageSize,
        string? sortBy, SortDirection sortDirection, CancellationToken cancellationToken = default);
    Task<IEnumerable<Portfolio>> GetActivePortfoliosAsync(CancellationToken cancellationToken = default);
}

public class PortfolioRepository : MongoRepository<Portfolio>, IPortfolioRepository
{
    public PortfolioRepository(IMongoDatabase database) : base(database, "portfolios")
    {
        // Enterprise Indexing Strategy
        CreateIndexesAsync().GetAwaiter().GetResult();
    }

    private async Task CreateIndexesAsync()
    {
        var collection = _database.GetCollection<Portfolio>(_collectionName);

        // Compound index for user queries
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<Portfolio>(
                Builders<Portfolio>.IndexKeys.Combine(
                    Builders<Portfolio>.IndexKeys.Ascending(p => p.UserId),
                    Builders<Portfolio>.IndexKeys.Ascending(p => p.Status),
                    Builders<Portfolio>.IndexKeys.Descending(p => p.CreatedAt)
                ),
                new CreateIndexOptions { Name = "user_status_created_idx" }
            )
        );

        // Text index for search
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<Portfolio>(
                Builders<Portfolio>.IndexKeys.Text(p => p.Name),
                new CreateIndexOptions { Name = "name_text_idx" }
            )
        );

        // Unique compound index
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<Portfolio>(
                Builders<Portfolio>.IndexKeys.Combine(
                    Builders<Portfolio>.IndexKeys.Ascending(p => p.UserId),
                    Builders<Portfolio>.IndexKeys.Ascending(p => p.Name)
                ),
                new CreateIndexOptions
                {
                    Name = "user_name_unique_idx",
                    Unique = true,
                    PartialFilterExpression = Builders<Portfolio>.Filter.Eq(p => p.IsDeleted, false)
                }
            )
        );
    }

    public async Task<Portfolio?> GetByUserIdAndNameAsync(string userId, string name, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(
            Builders<Portfolio>.Filter.And(
                Builders<Portfolio>.Filter.Eq(p => p.UserId, userId),
                Builders<Portfolio>.Filter.Eq(p => p.Name, name),
                Builders<Portfolio>.Filter.Eq(p => p.IsDeleted, false)
            )
        ).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PaginatedList<Portfolio>> GetByUserIdAsync(string userId, int pageNumber, int pageSize,
        string? sortBy, SortDirection sortDirection, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Portfolio>.Filter.And(
            Builders<Portfolio>.Filter.Eq(p => p.UserId, userId),
            Builders<Portfolio>.Filter.Eq(p => p.IsDeleted, false)
        );

        var sort = sortBy switch
        {
            "name" => sortDirection == SortDirection.Ascending
                ? Builders<Portfolio>.Sort.Ascending(p => p.Name)
                : Builders<Portfolio>.Sort.Descending(p => p.Name),
            "createdAt" => sortDirection == SortDirection.Ascending
                ? Builders<Portfolio>.Sort.Ascending(p => p.CreatedAt)
                : Builders<Portfolio>.Sort.Descending(p => p.CreatedAt),
            _ => Builders<Portfolio>.Sort.Descending(p => p.CreatedAt)
        };

        var totalCount = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _collection.Find(filter)
            .Sort(sort)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<Portfolio>(items, (int)totalCount, pageNumber, pageSize);
    }

    public async Task<IEnumerable<Portfolio>> GetActivePortfoliosAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(
            Builders<Portfolio>.Filter.And(
                Builders<Portfolio>.Filter.Eq(p => p.Status, PortfolioStatus.Active),
                Builders<Portfolio>.Filter.Eq(p => p.IsDeleted, false)
            )
        ).ToListAsync(cancellationToken);
    }
}
```

### Base Repository
```csharp
public abstract class MongoRepository<T> : IRepository<T> where T : AggregateRoot
{
    protected readonly IMongoCollection<T> _collection;
    protected readonly IMongoDatabase _database;
    protected readonly string _collectionName;

    protected MongoRepository(IMongoDatabase database, string collectionName)
    {
        _database = database;
        _collectionName = collectionName;
        _collection = database.GetCollection<T>(collectionName);
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(
            Builders<T>.Filter.And(
                Builders<T>.Filter.Eq(e => e.Id, id),
                Builders<T>.Filter.Eq(e => e.IsDeleted, false)
            )
        ).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(
            Builders<T>.Filter.Eq(e => e.IsDeleted, false)
        ).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Version++;

        var filter = Builders<T>.Filter.And(
            Builders<T>.Filter.Eq(e => e.Id, entity.Id),
            Builders<T>.Filter.Eq(e => e.Version, entity.Version - 1) // Optimistic concurrency
        );

        var result = await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);

        if (result.ModifiedCount == 0)
            throw new ConcurrencyException($"Entity {entity.Id} was modified by another process");
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        // Soft delete
        var update = Builders<T>.Update
            .Set(e => e.IsDeleted, true)
            .Set(e => e.DeletedAt, DateTime.UtcNow);

        await _collection.UpdateOneAsync(
            Builders<T>.Filter.Eq(e => e.Id, id),
            update,
            cancellationToken: cancellationToken);
    }
}
```

## 🔐 Security Enterprise

### Authentication & Authorization
```csharp
// Startup Configuration
public void ConfigureServices(IServiceCollection services)
{
    // JWT Authentication
    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Configuration["Jwt:Issuer"],
            ValidAudience = Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    // Validate user still exists and is active
                    var userRepository = context.HttpContext.RequestServices
                        .GetRequiredService<IUserRepository>();
                    var user = await userRepository.GetByIdAsync(userId);
                    if (user == null || !user.IsActive)
                    {
                        context.Fail("User not found or inactive");
                    }
                }
            }
        };
    });

    // Google OAuth
    services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = Configuration["GoogleOAuth:ClientId"]!;
            options.ClientSecret = Configuration["GoogleOAuth:ClientSecret"]!;
            options.CallbackPath = "/auth/google/callback";

            options.Events = new OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    // Enrich claims with additional user info
                    var email = context.Identity?.FindFirst(ClaimTypes.Email)?.Value;
                    if (!string.IsNullOrEmpty(email))
                    {
                        var userRepository = context.HttpContext.RequestServices
                            .GetRequiredService<IUserRepository>();
                        var user = await userRepository.GetByEmailAsync(email);
                        if (user != null)
                        {
                            context.Identity?.AddClaim(new Claim("user_id", user.Id));
                            context.Identity?.AddClaim(new Claim("user_role", user.Role.ToString()));
                        }
                    }
                }
            };
        });

    // Authorization Policies
    services.AddAuthorization(options =>
    {
        options.AddPolicy("PortfolioOwner", policy =>
            policy.RequireClaim("scope", "portfolio.read", "portfolio.write"));

        options.AddPolicy("AdminOnly", policy =>
            policy.RequireRole("Admin"));
    });
}
```

### Audit Logging Enterprise
```csharp
public interface IAuditService
{
    Task LogAsync(string entityType, string entityId, string action,
        string details, string userId, string? correlationId = null);
    Task<IEnumerable<AuditEntry>> GetAuditTrailAsync(string entityType,
        string entityId, DateTime? fromDate = null, DateTime? toDate = null);
}

public class AuditService : IAuditService
{
    private readonly IMongoCollection<AuditEntry> _auditCollection;

    public AuditService(IMongoDatabase database)
    {
        _auditCollection = database.GetCollection<AuditEntry>("audit_log");

        // Create indexes for audit queries
        _auditCollection.Indexes.CreateOne(
            Builders<AuditEntry>.IndexKeys.Combine(
                Builders<AuditEntry>.IndexKeys.Ascending(a => a.EntityType),
                Builders<AuditEntry>.IndexKeys.Ascending(a => a.EntityId),
                Builders<AuditEntry>.IndexKeys.Descending(a => a.Timestamp)
            )
        );
    }

    public async Task LogAsync(string entityType, string entityId, string action,
        string details, string userId, string? correlationId = null)
    {
        var auditEntry = new AuditEntry
        {
            Id = Guid.NewGuid().ToString(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Details = details,
            UserId = userId,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            IpAddress = GetClientIpAddress(),
            UserAgent = GetUserAgent()
        };

        await _auditCollection.InsertOneAsync(auditEntry);
    }

    public async Task<IEnumerable<AuditEntry>> GetAuditTrailAsync(string entityType,
        string entityId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var filter = Builders<AuditEntry>.Filter.And(
            Builders<AuditEntry>.Filter.Eq(a => a.EntityType, entityType),
            Builders<AuditEntry>.Filter.Eq(a => a.EntityId, entityId)
        );

        if (fromDate.HasValue)
            filter &= Builders<AuditEntry>.Filter.Gte(a => a.Timestamp, fromDate.Value);

        if (toDate.HasValue)
            filter &= Builders<AuditEntry>.Filter.Lte(a => a.Timestamp, toDate.Value);

        return await _auditCollection.Find(filter)
            .Sort(Builders<AuditEntry>.Sort.Descending(a => a.Timestamp))
            .ToListAsync();
    }
}

public class AuditEntry
{
    public string Id { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string Details { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string CorrelationId { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
```

## 📊 P&L Engine Enterprise

### Advanced P&L Calculation
```csharp
public class PnLService : IPnLService
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IStockPriceService _stockPriceService;
    private readonly ICacheService _cacheService;

    public PnLService(
        ITradeRepository tradeRepository,
        IStockPriceService stockPriceService,
        ICacheService cacheService)
    {
        _tradeRepository = tradeRepository;
        _stockPriceService = stockPriceService;
        _cacheService = cacheService;
    }

    public async Task<PortfolioPnLSummary> CalculatePortfolioPnLAsync(
        string portfolioId, CancellationToken cancellationToken = default)
    {
        // Try cache first
        var cacheKey = $"pnl:portfolio:{portfolioId}";
        var cachedResult = await _cacheService.GetAsync<PortfolioPnLSummary>(cacheKey);
        if (cachedResult != null)
            return cachedResult;

        var trades = await _tradeRepository.GetByPortfolioIdAsync(portfolioId, cancellationToken);
        var tradesBySymbol = trades.GroupBy(t => t.Symbol.Value);

        var positionPnLs = new List<PositionPnL>();
        var totalRealizedPnL = Money.Zero("USD");

        foreach (var symbolGroup in tradesBySymbol)
        {
            var positionPnL = await CalculatePositionPnLAsync(
                portfolioId, new StockSymbol(symbolGroup.Key), cancellationToken);
            positionPnLs.Add(positionPnL);
            totalRealizedPnL += positionPnL.RealizedPnL;
        }

        var totalValue = new Money(positionPnLs.Sum(p => p.CurrentValue.Amount), "USD");
        var totalCost = new Money(positionPnLs.Sum(p => p.Quantity * p.AverageCost.Amount), "USD");
        var totalUnrealizedPnL = new Money(positionPnLs.Sum(p => p.UnrealizedPnL.Amount), "USD");

        var summary = new PortfolioPnLSummary
        {
            PortfolioId = portfolioId,
            TotalValue = totalValue,
            TotalCost = totalCost,
            TotalUnrealizedPnL = totalUnrealizedPnL,
            TotalRealizedPnL = totalRealizedPnL,
            TotalUnrealizedPnLPercentage = totalCost.Amount > 0
                ? (totalUnrealizedPnL.Amount / totalCost.Amount) * 100 : 0,
            Positions = positionPnLs,
            CalculatedAt = DateTime.UtcNow
        };

        // Cache result for 5 minutes
        await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromMinutes(5));

        return summary;
    }

    public async Task<PositionPnL> CalculatePositionPnLAsync(
        string portfolioId, StockSymbol symbol, CancellationToken cancellationToken = default)
    {
        var trades = await _tradeRepository.GetByPortfolioIdAndSymbolAsync(
            portfolioId, symbol.Value, cancellationToken);

        var (quantity, averageCost, realizedPnL) = CalculateAverageCostAndRealizedPnL(trades);
        var currentPrice = await _stockPriceService.GetCurrentPriceAsync(symbol);
        var currentValue = new Money(quantity * currentPrice.Amount, currentPrice.Currency);
        var costBasis = new Money(quantity * averageCost.Amount, averageCost.Currency);
        var unrealizedPnL = new Money(currentValue.Amount - costBasis.Amount, currentPrice.Currency);

        return new PositionPnL
        {
            Symbol = symbol,
            Quantity = quantity,
            AverageCost = averageCost,
            CurrentValue = currentValue,
            UnrealizedPnL = unrealizedPnL,
            UnrealizedPnLPercentage = costBasis.Amount > 0
                ? (unrealizedPnL.Amount / costBasis.Amount) * 100 : 0,
            RealizedPnL = realizedPnL
        };
    }

    private (decimal quantity, Money averageCost, Money realizedPnL) CalculateAverageCostAndRealizedPnL(
        IEnumerable<Trade> trades)
    {
        var buyTrades = trades.Where(t => t.Type == TradeType.Buy)
            .OrderBy(t => t.TradeDate).ToList();
        var sellTrades = trades.Where(t => t.Type == TradeType.Sell)
            .OrderBy(t => t.TradeDate).ToList();

        decimal totalQuantity = 0;
        decimal totalCost = 0;
        decimal realizedPnL = 0;

        // Calculate average cost from buy trades
        foreach (var buy in buyTrades)
        {
            totalCost += buy.Quantity * buy.Price.Amount;
            totalQuantity += buy.Quantity;
        }

        var averageCost = totalQuantity > 0
            ? new Money(totalCost / totalQuantity, "USD")
            : Money.Zero("USD");

        // Calculate realized P&L from sell trades
        foreach (var sell in sellTrades)
        {
            var sellValue = sell.Quantity * sell.Price.Amount;
            var costBasis = sell.Quantity * averageCost.Amount;
            realizedPnL += sellValue - costBasis;
            totalQuantity -= sell.Quantity;
        }

        return (totalQuantity, averageCost, new Money(realizedPnL, "USD"));
    }
}
```

## 🧪 Testing Enterprise

### Unit Tests Structure
```
tests/
├── InvestmentApp.Domain.Tests/
│   ├── Entities/
│   │   ├── PortfolioTests.cs
│   │   ├── TradeTests.cs
│   │   └── UserTests.cs
│   ├── ValueObjects/
│   │   ├── MoneyTests.cs
│   │   └── StockSymbolTests.cs
│   └── Events/
│       └── DomainEventTests.cs
├── InvestmentApp.Application.Tests/
│   ├── Commands/
│   │   ├── CreatePortfolioCommandTests.cs
│   │   └── CreateTradeCommandTests.cs
│   ├── Queries/
│   │   ├── GetPortfolioQueryTests.cs
│   │   └── GetTradesQueryTests.cs
│   └── Validators/
│       └── CreatePortfolioCommandValidatorTests.cs
├── InvestmentApp.Infrastructure.Tests/
│   ├── Repositories/
│   │   ├── PortfolioRepositoryTests.cs
│   │   └── TradeRepositoryTests.cs
│   └── Services/
│       ├── PnLServiceTests.cs
│       └── AuditServiceTests.cs
└── InvestmentApp.Api.Tests/
    ├── Controllers/
    │   ├── PortfoliosControllerTests.cs
    │   └── TradesControllerTests.cs
    └── Integration/
        └── ApiIntegrationTests.cs
```

### Enterprise Test Example
```csharp
[Fact]
public async Task CreatePortfolioCommand_WithValidData_ShouldCreatePortfolioAndAudit()
{
    // Arrange
    var command = new CreatePortfolioCommand
    {
        UserId = "user123",
        Name = "Test Portfolio",
        InitialCapital = 10000,
        Currency = "USD"
    };

    var portfolioRepository = new Mock<IPortfolioRepository>();
    portfolioRepository.Setup(r => r.GetByUserIdAndNameAsync(
        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Portfolio?)null);
    portfolioRepository.Setup(r => r.AddAsync(
        It.IsAny<Portfolio>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var auditService = new Mock<IAuditService>();
    var correlationIdAccessor = new Mock<ICorrelationIdAccessor>();
    correlationIdAccessor.Setup(c => c.CorrelationId).Returns("test-correlation-id");

    var handler = new CreatePortfolioCommandHandler(
        portfolioRepository.Object,
        auditService.Object,
        correlationIdAccessor.Object);

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNullOrEmpty();

    portfolioRepository.Verify(r => r.AddAsync(
        It.Is<Portfolio>(p =>
            p.UserId == command.UserId &&
            p.Name == command.Name &&
            p.InitialCapital.Amount == command.InitialCapital),
        It.IsAny<CancellationToken>()), Times.Once);

    auditService.Verify(a => a.LogAsync(
        "Portfolio",
        It.IsAny<string>(),
        "Created",
        It.Is<string>(s => s.Contains("Test Portfolio")),
        command.UserId,
        "test-correlation-id"), Times.Once);
}
```

## 🚀 Deployment Enterprise

### Docker Enterprise Setup
```dockerfile
# InvestmentApp.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["InvestmentApp.Api/InvestmentApp.Api.csproj", "InvestmentApp.Api/"]
COPY ["InvestmentApp.Application/InvestmentApp.Application.csproj", "InvestmentApp.Application/"]
COPY ["InvestmentApp.Domain/InvestmentApp.Domain.csproj", "InvestmentApp.Domain/"]
COPY ["InvestmentApp.Infrastructure/InvestmentApp.Infrastructure.csproj", "InvestmentApp.Infrastructure/"]
RUN dotnet restore "InvestmentApp.Api/InvestmentApp.Api.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "InvestmentApp.Api/InvestmentApp.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "InvestmentApp.Api/InvestmentApp.Api.csproj" -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "InvestmentApp.Api.dll"]
```

### Kubernetes Deployment
```yaml
# k8s/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: investment-api
spec:
  replicas: 3
  selector:
    matchLabels:
      app: investment-api
  template:
    metadata:
      labels:
        app: investment-api
    spec:
      containers:
      - name: api
        image: investment-api:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__MongoDb
          valueFrom:
            secretKeyRef:
              name: mongodb-secret
              key: connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
```

### CI/CD Pipeline Enterprise
```yaml
# .github/workflows/ci-cd.yml
name: CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      mongodb:
        image: mongo:7.0
        ports:
          - 27017:27017

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Test
      run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"

    - name: Upload coverage
      uses: codecov/codecov-action@v3

  build-and-deploy:
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'

    steps:
    - name: Build and push Docker image
      uses: docker/build-push-action@v4
      with:
        context: .
        push: true
        tags: investment-api:latest

    - name: Deploy to production
      uses: azure/webapps-deploy@v2
      with:
        app-name: investment-api
        images: investment-api:latest
```

## 📈 Monitoring & Observability

### Application Metrics
```csharp
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddApplicationInsightsTelemetry();

    services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddMongoDBInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter());
}
```

### Health Checks Enterprise
```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddMongoDb(
                mongodbConnectionString: Configuration["ConnectionStrings:MongoDb"]!,
                name: "mongodb",
                timeout: TimeSpan.FromSeconds(10))
            .AddUrlGroup(
                new Uri("https://api.marketdata.com/health"),
                name: "market-data-api",
                timeout: TimeSpan.FromSeconds(10))
            .AddDiskStorageHealthCheck(
                diskPath: "/",
                name: "disk-storage",
                minimumFreeMegabytes: 1024)
            .AddProcessAllocatedMemoryHealthCheck(
                maximumMegabytesAllocated: 512,
                name: "process-memory");

        services.AddHealthChecksUI()
            .AddInMemoryStorage();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.UseHealthChecksUI(options =>
        {
            options.UIPath = "/health-ui";
        });
    }
}
```

## 🔧 Development Guidelines Enterprise

### Code Review Checklist
- [ ] **Architecture**: Tuân thủ Clean Architecture và CQRS
- [ ] **Domain**: Business logic trong Domain layer
- [ ] **Validation**: Input validation đầy đủ
- [ ] **Error Handling**: Comprehensive error handling
- [ ] **Security**: Authentication và authorization
- [ ] **Performance**: Efficient database queries
- [ ] **Testing**: Unit tests và integration tests
- [ ] **Documentation**: Code comments và API docs

### Performance Optimization
1. **Database**: Proper indexing, query optimization
2. **Caching**: Redis cho frequently accessed data
3. **Async/Await**: All I/O operations asynchronous
4. **Pagination**: Large datasets pagination
5. **Compression**: Response compression
6. **CDN**: Static assets delivery

### Security Best Practices
1. **Input Validation**: Validate all inputs
2. **Authentication**: JWT with secure keys
3. **Authorization**: Role-based access control
4. **Audit Logging**: Complete activity tracking
5. **CORS**: Configured cross-origin policies
6. **Rate Limiting**: API rate limiting
7. **Data Encryption**: Sensitive data encryption

---

## 🎯 AI Agent Responsibilities

Khi phát triển Investment Mate v2, AI agents phải:

### 1. **Architecture Compliance**
- Tuân thủ Clean Architecture 4 layers
- Implement CQRS pattern đúng cách
- Domain-Driven Design với rich domain models
- Event Sourcing cho audit trails

### 2. **Code Quality Standards**
- Comprehensive error handling
- Input validation với FluentValidation
- Unit tests với xUnit và FluentAssertions
- Integration tests với TestContainers

### 3. **Enterprise Features**
- Audit logging cho tất cả operations
- Correlation IDs cho request tracing
- Health checks và monitoring
- Security best practices

### 4. **Performance & Scalability**
- Efficient database queries với proper indexing
- Caching strategies với Redis
- Asynchronous processing
- Pagination cho large datasets

### 5. **Documentation & Maintenance**
- Code comments theo C# documentation standards
- API documentation với Swagger/OpenAPI
- README và setup instructions
- Troubleshooting guides

**Nhớ**: Đây là hệ thống enterprise critical với high standards về security, performance, và reliability. Mọi thay đổi phải được review kỹ và test thoroughly trước khi deploy to production.</content>
<parameter name="filePath">d:\invest-mate-v2\project\AI_AGENT_GUIDE_ENTERPRISE.md