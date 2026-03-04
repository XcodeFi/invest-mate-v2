# 🤖 GitHub Copilot - Hướng dẫn Phát triển Investment Mate v2

Hướng dẫn chi tiết cho AI agents (GitHub Copilot, Claude, GPT) trong việc phát triển và maintain hệ thống Investment Mate v2.

## 🎯 Tổng quan Dự án

**Investment Mate v2** là hệ thống quản lý danh mục đầu tư doanh nghiệp được xây dựng theo:
- **Clean Architecture** với 4 layers rõ ràng
- **CQRS Pattern** cho command/query separation
- **Domain-Driven Design (DDD)** với rich domain models
- **Event Sourcing** cho audit trails
- **Microservices Architecture** với API, Worker, và Frontend

## 🏗️ Kiến trúc Hệ thống

### Layer Structure
```
src/
├── InvestmentApp.Api/           # 🚪 Presentation Layer
├── InvestmentApp.Application/   # 🔄 Application Layer
├── InvestmentApp.Domain/        # 🎯 Domain Layer
├── InvestmentApp.Infrastructure/# 🛠️ Infrastructure Layer
└── InvestmentApp.Worker/        # ⚙️ Background Processing
```

### Design Patterns
- **Repository Pattern**: Data access abstraction
- **Unit of Work**: Transaction management
- **Mediator Pattern**: CQRS với MediatR
- **Factory Pattern**: Domain object creation
- **Strategy Pattern**: P&L calculation algorithms

## 📋 Quy tắc Phát triển

### 1. Code Quality Standards

#### ✅ DO
```csharp
// Good: Rich domain model với business logic
public class Portfolio : AggregateRoot
{
    public void AddTrade(Trade trade)
    {
        if (trade.Quantity <= 0)
            throw new DomainException("Trade quantity must be positive");

        // Business logic here
        _trades.Add(trade);
        AddDomainEvent(new TradeAddedEvent(trade));
    }
}
```

#### ❌ DON'T
```csharp
// Bad: Anemic domain model
public class Portfolio
{
    public List<Trade> Trades { get; set; } = new();

    public void AddTrade(Trade trade)
    {
        Trades.Add(trade); // No validation, no business rules
    }
}
```

### 2. Naming Conventions

#### Classes & Interfaces
```csharp
// Domain Entities
public class Portfolio : AggregateRoot
public class Trade : Entity

// Value Objects
public class Money : IEquatable<Money>
public class StockSymbol : IEquatable<StockSymbol>

// Services
public interface IPnLService
public class PnLService : IPnLService

// Commands & Queries
public class CreatePortfolioCommand : IRequest<string>
public class GetPortfolioQuery : IRequest<PortfolioDto>
```

#### Methods & Properties
```csharp
// Commands
public async Task<string> Handle(CreatePortfolioCommand request)

// Queries
public async Task<PortfolioDto> Handle(GetPortfolioQuery request)

// Domain methods
public void AddTrade(Trade trade)
public void UpdateProfile(string name)

// Private methods
private void ValidateTrade(Trade trade)
private Money CalculateAverageCost()
```

### 3. Error Handling

#### Domain Exceptions
```csharp
// Domain layer - business rule violations
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

// Usage
if (portfolio.InitialCapital <= 0)
    throw new DomainException("Initial capital must be positive");
```

#### Application Exceptions
```csharp
// Application layer - use case failures
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
    {
        Errors = errors;
    }
}
```

### 4. Validation Rules

#### FluentValidation
```csharp
public class CreatePortfolioCommandValidator : AbstractValidator<CreatePortfolioCommand>
{
    public CreatePortfolioCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Portfolio name is required")
            .MaximumLength(100).WithMessage("Portfolio name must not exceed 100 characters");

        RuleFor(x => x.InitialCapital)
            .GreaterThan(0).WithMessage("Initial capital must be greater than 0")
            .LessThanOrEqualTo(10000000).WithMessage("Initial capital cannot exceed 10 million");
    }
}
```

## 🔄 CQRS Implementation

### Commands
```csharp
// Command
public class CreatePortfolioCommand : IRequest<string>
{
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal InitialCapital { get; set; }
}

// Handler
public class CreatePortfolioCommandHandler : IRequestHandler<CreatePortfolioCommand, string>
{
    public async Task<string> Handle(CreatePortfolioCommand request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### Queries
```csharp
// Query
public class GetPortfolioQuery : IRequest<PortfolioDto>
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

// Handler
public class GetPortfolioQueryHandler : IRequestHandler<GetPortfolioQuery, PortfolioDto>
{
    public async Task<PortfolioDto> Handle(GetPortfolioQuery request, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

## 🗄️ MongoDB Best Practices

### Document Design
```csharp
// Portfolio document
{
  "_id": "uuid",
  "userId": "uuid",
  "name": "My Portfolio",
  "initialCapital": 10000.00,
  "createdAt": "2024-01-01T00:00:00Z",
  "isDeleted": false,
  "version": 1
}

// Trade document
{
  "_id": "uuid",
  "portfolioId": "uuid",
  "symbol": "AAPL",
  "type": "BUY",
  "quantity": 10,
  "price": 150.25,
  "fee": 5.00,
  "tax": 0.00,
  "tradeDate": "2024-01-01T10:00:00Z",
  "createdAt": "2024-01-01T10:00:00Z"
}
```

### Indexing Strategy
```csharp
// Compound indexes for performance
collection.Indexes.CreateOne(new CreateIndexModel<Trade>(
    Builders<Trade>.IndexKeys.Combine(
        Builders<Trade>.IndexKeys.Ascending(t => t.PortfolioId),
        Builders<Trade>.IndexKeys.Ascending(t => t.Symbol)
    )
));

// Single indexes
collection.Indexes.CreateOne(Builders<Trade>.IndexKeys.Ascending(t => t.TradeDate));
collection.Indexes.CreateOne(Builders<Trade>.IndexKeys.Ascending(t => t.IsDeleted));
```

## 🔐 Security Guidelines

### Authentication
```csharp
// JWT token generation
public string GenerateToken(User user)
{
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim("provider", user.Provider)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: _configuration["Jwt:Issuer"],
        audience: _configuration["Jwt:Audience"],
        claims: claims,
        expires: DateTime.Now.AddMinutes(60),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### Authorization
```csharp
// Controller protection
[Authorize]
[ApiController]
[Route("api/v1/portfolios")]
public class PortfoliosController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreatePortfolio([FromBody] CreatePortfolioCommand command)
    {
        command.UserId = User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();
        // Implementation
    }
}
```

## 📊 P&L Calculation Logic

### Average Cost Method
```csharp
private (decimal quantity, Money averageCost, Money realizedPnL) CalculateAverageCostAndRealizedPnL(IEnumerable<Trade> trades)
{
    var buyTrades = trades.Where(t => t.Type == TradeType.Buy).OrderBy(t => t.CreatedAt).ToList();
    var sellTrades = trades.Where(t => t.Type == TradeType.Sell).OrderBy(t => t.CreatedAt).ToList();

    decimal totalQuantity = 0;
    decimal totalCost = 0;
    decimal realizedPnL = 0;

    // Process buy trades
    foreach (var buy in buyTrades)
    {
        totalCost += buy.Quantity * buy.Price.Amount;
        totalQuantity += buy.Quantity;
    }

    var averageCost = totalQuantity > 0 ? new Money(totalCost / totalQuantity, "USD") : new Money(0, "USD");

    // Process sell trades
    foreach (var sell in sellTrades)
    {
        realizedPnL += sell.Quantity * (sell.Price.Amount - averageCost.Amount);
        totalQuantity -= sell.Quantity;
    }

    return (totalQuantity, averageCost, new Money(realizedPnL, "USD"));
}
```

## 🧪 Testing Guidelines

### Unit Tests
```csharp
[Fact]
public void Portfolio_AddTrade_WithValidTrade_ShouldAddTradeAndRaiseEvent()
{
    // Arrange
    var portfolio = new Portfolio("user123", "Test Portfolio", 10000);
    var trade = new Trade("AAPL", TradeType.Buy, 10, new Money(150.25m, "USD"));

    // Act
    portfolio.AddTrade(trade);

    // Assert
    portfolio.DomainEvents.Should().ContainSingle(e => e is TradeCreatedEvent);
}

[Fact]
public void Portfolio_AddTrade_WithNegativeQuantity_ShouldThrowDomainException()
{
    // Arrange
    var portfolio = new Portfolio("user123", "Test Portfolio", 10000);
    var trade = new Trade("AAPL", TradeType.Buy, -5, new Money(150.25m, "USD"));

    // Act & Assert
    FluentActions.Invoking(() => portfolio.AddTrade(trade))
        .Should().Throw<DomainException>()
        .WithMessage("Trade quantity must be positive");
}
```

### Integration Tests
```csharp
[Fact]
public async Task CreatePortfolioCommand_WithValidData_ShouldCreatePortfolio()
{
    // Arrange
    var command = new CreatePortfolioCommand
    {
        UserId = "user123",
        Name = "Test Portfolio",
        InitialCapital = 10000
    };

    // Act
    var portfolioId = await _mediator.Send(command);

    // Assert
    portfolioId.Should().NotBeNullOrEmpty();

    var portfolio = await _portfolioRepository.GetByIdAsync(portfolioId);
    portfolio.Should().NotBeNull();
    portfolio.Name.Should().Be("Test Portfolio");
}
```

## 🚀 Deployment Guidelines

### Docker Best Practices
```dockerfile
# Multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["InvestmentApp.Api/InvestmentApp.Api.csproj", "InvestmentApp.Api/"]
RUN dotnet restore "InvestmentApp.Api/InvestmentApp.Api.csproj"
COPY . .
RUN dotnet build "InvestmentApp.Api/InvestmentApp.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "InvestmentApp.Api/InvestmentApp.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "InvestmentApp.Api.dll"]
```

### Environment Configuration
```bash
# Development
export ASPNETCORE_ENVIRONMENT=Development
export ConnectionStrings__MongoDb=mongodb://localhost:27017
export MongoDb__DatabaseName=investmentapp

# Production
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__MongoDb=mongodb://mongodb:27017
export MongoDb__DatabaseName=investmentapp_prod
```

## 📝 Documentation Standards

### Code Comments
```csharp
/// <summary>
/// Calculates the profit and loss for a portfolio using the average cost method.
/// </summary>
/// <param name="portfolioId">The unique identifier of the portfolio</param>
/// <param name="cancellationToken">Cancellation token for async operations</param>
/// <returns>Portfolio P&L summary with realized and unrealized gains</returns>
public async Task<PortfolioPnLSummary> CalculatePortfolioPnLAsync(
    string portfolioId,
    CancellationToken cancellationToken = default)
```

### API Documentation
```csharp
[HttpPost]
[ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> CreatePortfolio([FromBody] CreatePortfolioCommand command)
{
    // Implementation
}
```

## 🔧 Development Workflow

### 1. Feature Development
1. Tạo issue/feature branch
2. Implement domain logic trước
3. Viết tests cho domain logic
4. Implement application services
5. Add API endpoints
6. Update documentation

### 2. Code Review Checklist
- [ ] Domain logic đúng business rules
- [ ] Input validation đầy đủ
- [ ] Error handling appropriate
- [ ] Tests coverage >= 80%
- [ ] Documentation updated
- [ ] Security considerations

### 3. Commit Message Format
```
feat: add Google OAuth authentication
fix: resolve P&L calculation bug
docs: update API documentation
refactor: simplify trade validation logic
test: add integration tests for portfolio creation
```

## 🎯 Performance Optimization

### Database Optimization
- Sử dụng compound indexes cho queries thường xuyên
- Implement pagination cho large datasets
- Cache frequently accessed data
- Use projections để chỉ lấy fields cần thiết

### Application Performance
- Async/await cho tất cả I/O operations
- Connection pooling cho database
- Response compression
- Background processing cho heavy calculations

## 🚨 Error Monitoring

### Logging Strategy
```csharp
// Structured logging
_logger.LogInformation("Portfolio created {PortfolioId} for user {UserId}",
    portfolio.Id, userId);

_logger.LogError(ex, "Failed to calculate P&L for portfolio {PortfolioId}",
    portfolioId);
```

### Health Checks
```csharp
// Add health checks
builder.Services.AddHealthChecks()
    .AddMongoDb(mongodbConnectionString)
    .AddUrlGroup(new Uri("https://api.example.com/health"));
```

---

## 🤖 AI Agent Responsibilities

Khi làm việc với codebase này, AI agents phải:

1. **Tuân thủ Clean Architecture** - Không vi phạm layer boundaries
2. **Implement Domain-Driven Design** - Rich domain models với business logic
3. **Follow CQRS Pattern** - Separate commands và queries
4. **Write Comprehensive Tests** - Unit tests cho domain logic, integration tests cho workflows
5. **Maintain Code Quality** - Naming conventions, error handling, documentation
6. **Consider Security** - Authentication, authorization, input validation
7. **Optimize Performance** - Efficient database queries, caching strategies
8. **Document Changes** - Update README, API docs khi cần

**Remember**: Code is read more than written. Prioritize readability, maintainability, and testability over clever optimizations.</content>
<parameter name="filePath">d:\invest-mate-v2\project\.github\copilot-instructions.md