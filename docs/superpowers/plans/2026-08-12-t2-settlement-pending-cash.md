# Tiền bán chờ về T+2 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tách tiền bán chứng khoán chưa về tài khoản (chu kỳ T+2) ra khỏi "Tiền mặt khả dụng", với lịch nghỉ lễ lưu trong MongoDB và nhập được qua endpoint + MCP.

**Architecture:** Một hàm thuần `SettlementCalculator` suy ngày về từ `Trade.TradeDate` + tập ngày nghỉ do caller nạp từ `IMarketClosureRepository` — không persist ngày thanh toán vào `Trade`, không sửa `PortfolioCashCalculator`. Đại lượng mới `PendingSettlementCash` chảy qua `PortfolioSummaryDto` xuống hero card và cửa sổ ghi lệnh, và qua `AiAssistantService` vào bản tin AI.

**Tech Stack:** .NET 9, Clean Architecture (Domain → Application → Infrastructure → Api), MongoDB Driver 3.6.0, MediatR, FluentValidation, ModelContextProtocol server, Angular 19 standalone + inline template + Tailwind, xUnit + FluentAssertions + Moq, Karma + Jasmine.

**Spec:** [`docs/superpowers/specs/2026-08-12-t2-settlement-pending-cash-design.md`](../specs/2026-08-12-t2-settlement-pending-cash-design.md)

## Global Constraints

- **Tiếng Việt có dấu đầy đủ** cho mọi text hiển thị và mọi commit message. Prefix conventional-commit giữ tiếng Anh (`feat`/`fix`/`docs`).
- **TDD bắt buộc:** Red → Green → Refactor. Test viết trước, chạy cho thấy đỏ, rồi mới implement.
- **Không thêm `Co-Authored-By`** vào commit.
- **MongoDB:** collection snake_case, field PascalCase (không có convention camelCase nào được đăng ký). Ngày thuần phải có `[BsonDateTimeOptions(Kind = DateTimeKind.Utc)]`, nếu không Mongo ghi nửa đêm giờ local thành 17:00Z hôm trước và mọi so sánh ở biên lệch 1 ngày.
- **MCP tool nhận tham số phẳng** (ADR-0008). Tham số optional phải nằm **sau** `CancellationToken ct` và có `= null`, nếu không schema đánh dấu nó `required`.
- **Không cache** lịch nghỉ — app một người dùng, document rất bé. Đây là quyết định có ý thức của spec §6.
- **"Hôm nay" luôn là ngày lịch Việt Nam** qua `VietnamDate`, không bao giờ `DateTime.UtcNow.Date` trần.
- `dotnet test` **âm thầm bỏ** project có DLL đang bị khoá bởi app đang chạy mà tổng vẫn báo Passed. Sau mỗi lần chạy, đối chiếu số project trong output; nếu thiếu thì tắt backend đang chạy rồi chạy lại.

---

## File Structure

| File | Trách nhiệm | Task |
|---|---|---|
| `src/InvestmentApp.Domain/Entities/MarketClosure.cs` | Một ngày HOSE đóng cửa. Bất biến | 1 |
| `src/InvestmentApp.Application/RepositoryInterfaces.cs` | Thêm `IMarketClosureRepository` | 2 |
| `src/InvestmentApp.Infrastructure/Repositories/MarketClosureRepository.cs` | Mongo impl + unique index có tên | 2 |
| `src/InvestmentApp.Application/MarketClosures/Commands/AddMarketClosures/AddMarketClosuresCommand.cs` | Nhập mảng ngày, idempotent | 3 |
| `src/InvestmentApp.Application/MarketClosures/Commands/RemoveMarketClosure/RemoveMarketClosureCommand.cs` | Xoá một ngày | 3 |
| `src/InvestmentApp.Application/MarketClosures/Queries/GetMarketClosures/GetMarketClosuresQuery.cs` | Đọc theo năm, nhóm theo tháng | 3 |
| `src/InvestmentApp.Api/Controllers/MarketClosuresController.cs` | Scheme JWT | 4 |
| `src/InvestmentApp.Api/Controllers/AiAgentMarketClosuresController.cs` | Scheme ApiKey, mirror | 4 |
| `src/InvestmentApp.Api/Mcp/MarketClosureTools.cs` | 3 tool MCP | 4 |
| `scripts/migrations/2026-08-12-market-closures-2026.mongo.js` | Seed 12 ngày nghỉ 2026 | 5 |
| `src/InvestmentApp.Application/Common/VietnamDate.cs` | Thêm `Today(utcNow)` | 6 |
| `src/InvestmentApp.Application/Common/SettlementCalculator.cs` | Hàm thuần T+2 | 6 |
| `src/InvestmentApp.Application/Portfolios/Queries/GetAllPortfolios/GetAllPortfoliosQuery.cs` | 2 field mới trên DTO | 7 |
| `frontend/src/app/core/services/portfolio.service.ts` | 2 field mới trên interface | 7 |
| `frontend/src/app/features/dashboard/dashboard.component.ts` | Dòng chờ về trên hero card | 8 |
| `frontend/src/app/features/capital-flows/capital-flows.component.ts` | Dòng chờ về, cả per-portfolio và overall | 8 |
| `frontend/src/app/features/trades/trade-create/trade-create.component.ts` | Cảnh báo mềm, field riêng | 9 |
| `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` | 2 tag mới + sửa dòng hướng dẫn | 10 |
| `docs/adr/0016-t2-settlement-pending-cash.md` | ADR | 11 |

---

### Task 1: Entity `MarketClosure`

**Files:**
- Create: `src/InvestmentApp.Domain/Entities/MarketClosure.cs`
- Test: `tests/InvestmentApp.Domain.Tests/Entities/MarketClosureTests.cs`

**Interfaces:**
- Consumes: `AggregateRoot` (đã có, cung cấp `Id`).
- Produces: `MarketClosure(string userId, DateTime date, string? note = null)`; property `UserId`, `Date` (DateTime, nửa đêm UTC), `Note`, `CreatedAt`.

- [ ] **Step 1: Viết test đỏ**

Tạo `tests/InvestmentApp.Domain.Tests/Entities/MarketClosureTests.cs`:

```csharp
using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class MarketClosureTests
{
    [Fact]
    public void Date_duoc_chuan_hoa_ve_nua_dem_Utc()
    {
        var closure = new MarketClosure("user1", new DateTime(2026, 2, 17, 15, 30, 0), "Tết Bính Ngọ");

        closure.Date.Should().Be(new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc));
        closure.Date.Kind.Should().Be(DateTimeKind.Utc);
        closure.Note.Should().Be("Tết Bính Ngọ");
        closure.UserId.Should().Be("user1");
        closure.Id.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("2026-08-22")] // thứ Bảy
    [InlineData("2026-08-23")] // Chủ nhật
    public void Cuoi_tuan_bi_tu_choi_vi_da_la_ngay_nghi(string date)
    {
        var act = () => new MarketClosure("user1", DateTime.Parse(date));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Cuối tuần*");
    }

    [Fact]
    public void UserId_null_thi_nem()
    {
        var act = () => new MarketClosure(null!, new DateTime(2026, 1, 1));

        act.Should().Throw<ArgumentNullException>();
    }
}
```

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `dotnet test tests/InvestmentApp.Domain.Tests --filter MarketClosureTests`
Expected: FAIL — build error `CS0246: The type or namespace name 'MarketClosure' could not be found`.

- [ ] **Step 3: Implement**

Tạo `src/InvestmentApp.Domain/Entities/MarketClosure.cs`:

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>
/// Một ngày Sở Giao dịch Chứng khoán đóng cửa vì nghỉ lễ. Bất biến — sửa = xoá và tạo lại.
/// T7/CN không lưu ở đây, suy ra từ <see cref="DayOfWeek"/>.
/// </summary>
public class MarketClosure : AggregateRoot
{
    public string UserId { get; private set; } = null!;

    // Ngày thuần. Thiếu attribute này thì Mongo ghi nửa đêm giờ local thành 17:00Z hôm trước,
    // đọc lên không còn là nửa đêm và phép đếm phiên lệch 1 ngày.
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Date { get; private set; }

    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }

    [BsonConstructor]
    public MarketClosure() { } // MongoDB

    public MarketClosure(string userId, DateTime date, string? note = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        if (Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            throw new ArgumentException("Cuối tuần đã là ngày nghỉ, không cần lưu", nameof(date));
        Note = note;
        CreatedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Chạy test cho thấy xanh**

Run: `dotnet test tests/InvestmentApp.Domain.Tests --filter MarketClosureTests`
Expected: PASS — 4 test (1 + 2 theory + 1).

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Domain/Entities/MarketClosure.cs tests/InvestmentApp.Domain.Tests/Entities/MarketClosureTests.cs
git commit -m "feat(domain): entity MarketClosure cho ngày nghỉ giao dịch"
```

---

### Task 2: Repository `IMarketClosureRepository` + Mongo impl

**Files:**
- Modify: `src/InvestmentApp.Application/RepositoryInterfaces.cs` (thêm interface sau `IMoodCheckInRepository`, khoảng dòng 63)
- Create: `src/InvestmentApp.Infrastructure/Repositories/MarketClosureRepository.cs`
- Modify: `src/InvestmentApp.Api/Program.cs:189` (thêm một dòng đăng ký DI ngay dưới `IMoodCheckInRepository`)

**Interfaces:**
- Consumes: `MarketClosure` từ Task 1.
- Produces:
  - `Task<IEnumerable<MarketClosure>> GetByUserAndRangeAsync(string userId, DateTime fromInclusive, DateTime toInclusive, CancellationToken ct = default)`
  - `Task<bool> TryAddAsync(MarketClosure entity, CancellationToken ct = default)` — `false` nghĩa là đã tồn tại
  - `Task<bool> DeleteByDateAsync(string userId, DateTime date, CancellationToken ct = default)`
  - `Task<DateTime?> GetLatestDateAsync(string userId, CancellationToken ct = default)`

- [ ] **Step 1: Thêm interface**

Vào `src/InvestmentApp.Application/RepositoryInterfaces.cs`, chèn ngay sau khối `DuplicateMoodCheckInException` (kết thúc khoảng dòng 63):

```csharp
/// <summary>
/// Ngày nghỉ giao dịch, một bản ghi cho mỗi (user, ngày). T7/CN không lưu.
/// </summary>
public interface IMarketClosureRepository
{
    Task<IEnumerable<MarketClosure>> GetByUserAndRangeAsync(
        string userId, DateTime fromInclusive, DateTime toInclusive, CancellationToken cancellationToken = default);

    /// <summary>Trả <c>false</c> khi ngày đó đã có — nhập trùng là no-op, không phải lỗi.</summary>
    Task<bool> TryAddAsync(MarketClosure entity, CancellationToken cancellationToken = default);

    /// <summary>Trả <c>false</c> khi không có gì để xoá.</summary>
    Task<bool> DeleteByDateAsync(string userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>Ngày nghỉ xa nhất đã nhập — mốc "lịch đã biết tới đâu" cho bản tin.</summary>
    Task<DateTime?> GetLatestDateAsync(string userId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Implement repository**

Tạo `src/InvestmentApp.Infrastructure/Repositories/MarketClosureRepository.cs`:

```csharp
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class MarketClosureRepository : IMarketClosureRepository
{
    private const string UniqueIndexName = "ux_user_date";

    private readonly IMongoCollection<MarketClosure> _collection;

    public MarketClosureRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MarketClosure>("market_closures");

        // Một bản ghi cho mỗi (user, ngày) — nhập lại cùng ngày là no-op, không đẻ bản thứ hai.
        _collection.Indexes.CreateOne(new CreateIndexModel<MarketClosure>(
            Builders<MarketClosure>.IndexKeys
                .Ascending(c => c.UserId)
                .Ascending(c => c.Date),
            new CreateIndexOptions { Unique = true, Name = UniqueIndexName }));
    }

    public async Task<IEnumerable<MarketClosure>> GetByUserAndRangeAsync(
        string userId, DateTime fromInclusive, DateTime toInclusive, CancellationToken cancellationToken = default)
    {
        var from = DateTime.SpecifyKind(fromInclusive.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toInclusive.Date, DateTimeKind.Utc);

        return await _collection
            .Find(c => c.UserId == userId && c.Date >= from && c.Date <= to)
            .SortBy(c => c.Date)
            .ToListAsync(cancellationToken);
    }

    // Phân biệt bằng TÊN index, không bằng "có phải DuplicateKey không": collection này sau có
    // thêm index unique thứ hai thì lỗi của nó không được đội lốt trùng (UserId, Date).
    public async Task<bool> TryAddAsync(MarketClosure entity, CancellationToken cancellationToken = default)
    {
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (
            ex.WriteError?.Category == ServerErrorCategory.DuplicateKey
            && ex.WriteError.Message.Contains(UniqueIndexName))
        {
            return false;
        }
    }

    public async Task<bool> DeleteByDateAsync(string userId, DateTime date, CancellationToken cancellationToken = default)
    {
        var target = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var result = await _collection.DeleteOneAsync(
            c => c.UserId == userId && c.Date == target, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<DateTime?> GetLatestDateAsync(string userId, CancellationToken cancellationToken = default)
    {
        var latest = await _collection
            .Find(c => c.UserId == userId)
            .SortByDescending(c => c.Date)
            .FirstOrDefaultAsync(cancellationToken);
        return latest?.Date;
    }
}
```

- [ ] **Step 3: Đăng ký DI**

Vào `src/InvestmentApp.Api/Program.cs`, ngay dưới dòng 189 (`IMoodCheckInRepository`):

```csharp
builder.Services.AddScoped<IMarketClosureRepository, MarketClosureRepository>();
```

- [ ] **Step 4: Build để chắc chưa vỡ gì**

Run: `dotnet build src/InvestmentApp.Api`
Expected: Build succeeded, 0 error.

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/RepositoryInterfaces.cs src/InvestmentApp.Infrastructure/Repositories/MarketClosureRepository.cs src/InvestmentApp.Api/Program.cs
git commit -m "feat(infra): repository market_closures với unique index (UserId, Date)"
```

---

### Task 3: Command + Query cho lịch nghỉ

**Files:**
- Create: `src/InvestmentApp.Application/MarketClosures/Commands/AddMarketClosures/AddMarketClosuresCommand.cs`
- Create: `src/InvestmentApp.Application/MarketClosures/Commands/RemoveMarketClosure/RemoveMarketClosureCommand.cs`
- Create: `src/InvestmentApp.Application/MarketClosures/Queries/GetMarketClosures/GetMarketClosuresQuery.cs`
- Test: `tests/InvestmentApp.Application.Tests/MarketClosures/MarketClosureHandlerTests.cs`

**Interfaces:**
- Consumes: `IMarketClosureRepository` (Task 2), `MarketClosure` (Task 1).
- Produces:
  - `AddMarketClosuresCommand(string UserId, IReadOnlyList<DateTime> Dates, string? Note) : IRequest<AddMarketClosuresResult>`
  - `AddMarketClosuresResult(int Added, int SkippedWeekend, int AlreadyExisted)`
  - `RemoveMarketClosureCommand(string UserId, DateTime Date) : IRequest<bool>`
  - `GetMarketClosuresQuery(string UserId, int Year) : IRequest<MarketClosureYearDto>`
  - `MarketClosureYearDto(int Year, List<MarketClosureMonthDto> Months)`, `MarketClosureMonthDto(int Month, List<MarketClosureDayDto> Days)`, `MarketClosureDayDto(int Day, string? Note)`

- [ ] **Step 1: Viết test đỏ**

Tạo `tests/InvestmentApp.Application.Tests/MarketClosures/MarketClosureHandlerTests.cs`:

```csharp
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.MarketClosures;

public class MarketClosureHandlerTests
{
    private readonly Mock<IMarketClosureRepository> _repo = new();

    [Fact]
    public async Task Nhap_ca_dot_le_thi_dem_dung_so_them_moi()
    {
        _repo.Setup(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AddMarketClosuresCommandHandler(_repo.Object);
        var result = await handler.Handle(new AddMarketClosuresCommand("user1",
            new[] { new DateTime(2026, 2, 16), new DateTime(2026, 2, 17), new DateTime(2026, 2, 18) },
            "Tết Bính Ngọ"), CancellationToken.None);

        result.Added.Should().Be(3);
        result.SkippedWeekend.Should().Be(0);
        result.AlreadyExisted.Should().Be(0);
    }

    [Fact]
    public async Task Cuoi_tuan_bi_bo_qua_chu_khong_lam_vo_ca_lo()
    {
        _repo.Setup(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AddMarketClosuresCommandHandler(_repo.Object);
        var result = await handler.Handle(new AddMarketClosuresCommand("user1",
            new[]
            {
                new DateTime(2026, 4, 27),  // thứ Hai — hợp lệ
                new DateTime(2026, 8, 22),  // thứ Bảy — bỏ qua
                new DateTime(2026, 8, 23)   // Chủ nhật — bỏ qua
            }, null), CancellationToken.None);

        result.Added.Should().Be(1);
        result.SkippedWeekend.Should().Be(2);
        _repo.Verify(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Nhap_trung_thi_bao_da_ton_tai_chu_khong_nem()
    {
        _repo.Setup(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new AddMarketClosuresCommandHandler(_repo.Object);
        var result = await handler.Handle(new AddMarketClosuresCommand("user1",
            new[] { new DateTime(2026, 1, 1) }, null), CancellationToken.None);

        result.Added.Should().Be(0);
        result.AlreadyExisted.Should().Be(1);
    }

    [Fact]
    public async Task Doc_theo_nam_thi_nhom_theo_thang_va_ghi_chu_o_cap_ngay()
    {
        _repo.Setup(r => r.GetByUserAndRangeAsync("user1",
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MarketClosure("user1", new DateTime(2026, 4, 27), "Giỗ Tổ Hùng Vương"),
                new MarketClosure("user1", new DateTime(2026, 4, 30), "Ngày Chiến thắng"),
                new MarketClosure("user1", new DateTime(2026, 5, 1), "Quốc tế Lao động")
            });

        var handler = new GetMarketClosuresQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetMarketClosuresQuery("user1", 2026), CancellationToken.None);

        result.Year.Should().Be(2026);
        result.Months.Should().HaveCount(2);

        var april = result.Months.Single(m => m.Month == 4);
        april.Days.Should().HaveCount(2);
        // Tháng 4/2026 có HAI đợt lễ khác nhau — ghi chú phải ở cấp ngày, không phải cấp tháng.
        april.Days.Single(d => d.Day == 27).Note.Should().Be("Giỗ Tổ Hùng Vương");
        april.Days.Single(d => d.Day == 30).Note.Should().Be("Ngày Chiến thắng");
    }
}
```

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter MarketClosureHandlerTests`
Expected: FAIL — build error `CS0246` cho `AddMarketClosuresCommandHandler` và `GetMarketClosuresQueryHandler`.

- [ ] **Step 3: Implement `AddMarketClosuresCommand`**

Tạo `src/InvestmentApp.Application/MarketClosures/Commands/AddMarketClosures/AddMarketClosuresCommand.cs`:

```csharp
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;

/// <summary>
/// Nhập ngày nghỉ giao dịch. Nhận một ngày, một đợt lễ, hay cả năm — cùng một đường.
/// Idempotent: gửi lại danh sách đã nhập thì không đẻ bản ghi thứ hai.
/// </summary>
public record AddMarketClosuresCommand(
    string UserId,
    IReadOnlyList<DateTime> Dates,
    string? Note) : IRequest<AddMarketClosuresResult>;

/// <summary>
/// Đếm tách ba nhóm để người gọi biết chuyện gì đã xảy ra. Dán cả năm vào mà chỉ nhận
/// một con số tổng thì không phân biệt được "đã nhập rồi" với "bị bỏ vì cuối tuần".
/// </summary>
public record AddMarketClosuresResult(int Added, int SkippedWeekend, int AlreadyExisted);

public class AddMarketClosuresCommandHandler
    : IRequestHandler<AddMarketClosuresCommand, AddMarketClosuresResult>
{
    private readonly IMarketClosureRepository _repository;

    public AddMarketClosuresCommandHandler(IMarketClosureRepository repository)
        => _repository = repository;

    public async Task<AddMarketClosuresResult> Handle(
        AddMarketClosuresCommand request, CancellationToken cancellationToken)
    {
        int added = 0, skippedWeekend = 0, alreadyExisted = 0;

        foreach (var date in request.Dates.Select(d => d.Date).Distinct())
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                skippedWeekend++;
                continue;
            }

            var closure = new MarketClosure(request.UserId, date, request.Note);
            if (await _repository.TryAddAsync(closure, cancellationToken)) added++;
            else alreadyExisted++;
        }

        return new AddMarketClosuresResult(added, skippedWeekend, alreadyExisted);
    }
}
```

- [ ] **Step 4: Implement `RemoveMarketClosureCommand`**

Tạo `src/InvestmentApp.Application/MarketClosures/Commands/RemoveMarketClosure/RemoveMarketClosureCommand.cs`:

```csharp
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;

/// <summary>Xoá một ngày nghỉ — dùng khi nghị định đổi hoặc nhập nhầm.</summary>
public record RemoveMarketClosureCommand(string UserId, DateTime Date) : IRequest<bool>;

public class RemoveMarketClosureCommandHandler : IRequestHandler<RemoveMarketClosureCommand, bool>
{
    private readonly IMarketClosureRepository _repository;

    public RemoveMarketClosureCommandHandler(IMarketClosureRepository repository)
        => _repository = repository;

    public async Task<bool> Handle(RemoveMarketClosureCommand request, CancellationToken cancellationToken)
        => await _repository.DeleteByDateAsync(request.UserId, request.Date, cancellationToken);
}
```

- [ ] **Step 5: Implement `GetMarketClosuresQuery`**

Tạo `src/InvestmentApp.Application/MarketClosures/Queries/GetMarketClosures/GetMarketClosuresQuery.cs`:

```csharp
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;

public record GetMarketClosuresQuery(string UserId, int Year) : IRequest<MarketClosureYearDto>;

public record MarketClosureYearDto(int Year, List<MarketClosureMonthDto> Months);

public record MarketClosureMonthDto(int Month, List<MarketClosureDayDto> Days);

/// <summary>Ghi chú ở cấp NGÀY: một tháng có thể chứa hai đợt lễ khác nhau (4/2026).</summary>
public record MarketClosureDayDto(int Day, string? Note);

public class GetMarketClosuresQueryHandler : IRequestHandler<GetMarketClosuresQuery, MarketClosureYearDto>
{
    private readonly IMarketClosureRepository _repository;

    public GetMarketClosuresQueryHandler(IMarketClosureRepository repository)
        => _repository = repository;

    public async Task<MarketClosureYearDto> Handle(
        GetMarketClosuresQuery request, CancellationToken cancellationToken)
    {
        var from = new DateTime(request.Year, 1, 1);
        var to = new DateTime(request.Year, 12, 31);

        var closures = await _repository.GetByUserAndRangeAsync(
            request.UserId, from, to, cancellationToken);

        var months = closures
            .GroupBy(c => c.Date.Month)
            .OrderBy(g => g.Key)
            .Select(g => new MarketClosureMonthDto(
                g.Key,
                g.OrderBy(c => c.Date)
                    .Select(c => new MarketClosureDayDto(c.Date.Day, c.Note))
                    .ToList()))
            .ToList();

        return new MarketClosureYearDto(request.Year, months);
    }
}
```

- [ ] **Step 6: Chạy test cho thấy xanh**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter MarketClosureHandlerTests`
Expected: PASS — 4 test.

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Application/MarketClosures tests/InvestmentApp.Application.Tests/MarketClosures
git commit -m "feat(app): command nhập/xoá và query đọc lịch nghỉ theo tháng"
```

---

### Task 4: Endpoint JWT + sibling ApiKey + 3 tool MCP

**Files:**
- Create: `src/InvestmentApp.Api/Controllers/MarketClosuresController.cs`
- Create: `src/InvestmentApp.Api/Controllers/AiAgentMarketClosuresController.cs`
- Create: `src/InvestmentApp.Api/Mcp/MarketClosureTools.cs`
- Test: `tests/InvestmentApp.Api.Tests/Mcp/MarketClosureToolsTests.cs`

**Interfaces:**
- Consumes: `AddMarketClosuresCommand`, `RemoveMarketClosureCommand`, `GetMarketClosuresQuery` (Task 3).
- Produces: 3 tool MCP tên `list_market_closures`, `add_market_closures`, `remove_market_closure`.

Tool MCP tự được nạp qua `.WithToolsFromAssembly()` ở [Program.cs:426](../../../src/InvestmentApp.Api/Program.cs#L426) — **không** cần đăng ký tay.

- [ ] **Step 1: Viết test đỏ cho tool MCP**

Tạo `tests/InvestmentApp.Api.Tests/Mcp/MarketClosureToolsTests.cs`:

```csharp
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

public class MarketClosureToolsTests
{
    [Fact]
    public async Task add_market_closures_chuyen_mang_chuoi_thanh_ngay()
    {
        var mediator = new Mock<IMediator>();
        AddMarketClosuresCommand? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<AddMarketClosuresCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AddMarketClosuresResult>, CancellationToken>((c, _) => sent = (AddMarketClosuresCommand)c)
            .ReturnsAsync(new AddMarketClosuresResult(2, 0, 0));

        var http = McpTestContext.HttpAccessorFor("user1");

        var result = await MarketClosureTools.AddMarketClosures(
            new[] { "2026-04-30", "2026-05-01" }, mediator.Object, http, CancellationToken.None, "Lễ 30/4");

        result.Added.Should().Be(2);
        sent!.UserId.Should().Be("user1");
        sent.Dates.Should().BeEquivalentTo(new[] { new DateTime(2026, 4, 30), new DateTime(2026, 5, 1) });
        sent.Note.Should().Be("Lễ 30/4");
    }

    [Fact]
    public async Task Ngay_sai_dinh_dang_bao_ro_can_gui_gi()
    {
        var mediator = new Mock<IMediator>();
        var http = McpTestContext.HttpAccessorFor("user1");

        var act = async () => await MarketClosureTools.AddMarketClosures(
            new[] { "30/04/2026" }, mediator.Object, http, CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*YYYY-MM-DD*");
    }
}
```

> **Lưu ý cho người thi hành:** `McpTestContext` đã tồn tại trong `tests/InvestmentApp.Api.Tests/Mcp/`. Mở nó ra xem helper nào có sẵn để dựng `IHttpContextAccessor` mang `userId`; nếu tên khác `HttpAccessorFor` thì dùng tên thật của nó thay vì thêm helper mới.

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter MarketClosureToolsTests`
Expected: FAIL — `CS0246` cho `MarketClosureTools`.

- [ ] **Step 3: Implement tool MCP**

Tạo `src/InvestmentApp.Api/Mcp/MarketClosureTools.cs`:

```csharp
using System.ComponentModel;
using System.Globalization;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class MarketClosureTools
{
    private const string DateFormat = "yyyy-MM-dd";

    [McpServerTool(Name = "list_market_closures", ReadOnly = true)]
    [Description("Liệt kê ngày nghỉ giao dịch của một năm, nhóm theo tháng. Thứ Bảy và Chủ nhật không nằm trong danh sách vì đã được suy ra tự động.")]
    public static async Task<MarketClosureYearDto> ListMarketClosures(
        [Description("Năm cần xem, ví dụ 2026.")] int year,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetMarketClosuresQuery(http.GetUserId(), year), ct);

    [McpServerTool(Name = "add_market_closures")]
    [Description("Nhập ngày nghỉ giao dịch (nghỉ lễ). Gửi được một ngày, một đợt lễ, hay cả năm trong cùng một lần gọi. Nhập lại ngày đã có là no-op. Thứ Bảy/Chủ nhật gửi lên sẽ bị bỏ qua vì đã là ngày nghỉ.")]
    public static async Task<AddMarketClosuresResult> AddMarketClosures(
        [Description("Danh sách ngày, mỗi phần tử dạng YYYY-MM-DD. Ví dụ [\"2026-04-30\",\"2026-05-01\"].")] string[] dates,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Ghi chú chung cho cả đợt, ví dụ \"Tết Bính Ngọ\".")] string? note = null)
        => await mediator.Send(new AddMarketClosuresCommand(http.GetUserId(), Parse(dates), note), ct);

    [McpServerTool(Name = "remove_market_closure")]
    [Description("Xoá một ngày nghỉ giao dịch đã nhập. Dùng khi lịch nghỉ được điều chỉnh hoặc nhập nhầm.")]
    public static async Task<bool> RemoveMarketClosure(
        [Description("Ngày cần xoá, dạng YYYY-MM-DD.")] string date,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new RemoveMarketClosureCommand(http.GetUserId(), ParseOne(date)), ct);

    private static IReadOnlyList<DateTime> Parse(string[] dates)
        => dates.Select(ParseOne).ToList();

    // Lỗi phải nói rõ phải gửi gì cho đúng, không chỉ nói là sai.
    private static DateTime ParseOne(string value)
        => DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : throw new ArgumentException(
                $"Ngày \"{value}\" không đúng định dạng. Cần dạng YYYY-MM-DD, ví dụ 2026-04-30.", nameof(value));
}
```

- [ ] **Step 4: Implement controller JWT**

Tạo `src/InvestmentApp.Api/Controllers/MarketClosuresController.cs`:

```csharp
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/market-closures")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MarketClosuresController : ControllerBase
{
    private readonly IMediator _mediator;

    public MarketClosuresController(IMediator mediator) => _mediator = mediator;

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    public record AddRequest(List<DateTime> Dates, string? Note);

    [HttpGet]
    [ProducesResponseType(typeof(MarketClosureYearDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int year)
        => Ok(await _mediator.Send(new GetMarketClosuresQuery(GetUserId(), year)));

    [HttpPost]
    [ProducesResponseType(typeof(AddMarketClosuresResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Add([FromBody] AddRequest request)
        => Ok(await _mediator.Send(new AddMarketClosuresCommand(GetUserId(), request.Dates, request.Note)));

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(DateTime date)
        => await _mediator.Send(new RemoveMarketClosureCommand(GetUserId(), date))
            ? NoContent()
            : NotFound();
}
```

- [ ] **Step 5: Implement sibling controller ApiKey**

Tạo `src/InvestmentApp.Api/Controllers/AiAgentMarketClosuresController.cs`. Chỉ đổi scheme + route, thân hàm sao y bản JWT:

```csharp
using InvestmentApp.Api.Auth;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>Lịch nghỉ giao dịch cho agent (scheme ApiKey). Mirror MarketClosuresController.</summary>
[ApiController]
[Route("api/v1/ai/agent/market-closures")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class AiAgentMarketClosuresController : AiAgentControllerBase
{
    public AiAgentMarketClosuresController(IMediator mediator) : base(mediator) { }

    public record AddRequest(List<DateTime> Dates, string? Note);

    [HttpGet]
    [ProducesResponseType(typeof(MarketClosureYearDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int year)
        => Ok(await _mediator.Send(new GetMarketClosuresQuery(GetUserId(), year)));

    [HttpPost]
    [ProducesResponseType(typeof(AddMarketClosuresResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Add([FromBody] AddRequest request)
        => Ok(await _mediator.Send(new AddMarketClosuresCommand(GetUserId(), request.Dates, request.Note)));

    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(DateTime date)
        => await _mediator.Send(new RemoveMarketClosureCommand(GetUserId(), date))
            ? NoContent()
            : NotFound();
}
```

- [ ] **Step 6: Thêm test ngang giá cho sibling controller**

Spec §9 đòi "sibling controller ApiKey trả cùng dữ liệu như bản JWT". Thêm vào `tests/InvestmentApp.Api.Tests/Controllers/AiAgentExposeControllersTests.cs`:

```csharp
    [Fact]
    public async Task AiAgentMarketClosures_gui_cung_query_nhu_ban_JWT()
    {
        GetMarketClosuresQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetMarketClosuresQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<MarketClosureYearDto>, CancellationToken>((q, _) => sent = (GetMarketClosuresQuery)q)
            .ReturnsAsync(new MarketClosureYearDto(2026, new List<MarketClosureMonthDto>()));

        var controller = NewController<AiAgentMarketClosuresController>();
        var result = await controller.Get(2026);

        result.Should().BeOfType<OkObjectResult>();
        sent!.Year.Should().Be(2026);
        sent.UserId.Should().Be(TestUserId);
    }

    [Fact]
    public void AiAgentMarketClosures_dung_scheme_ApiKey_va_route_agent()
    {
        var type = typeof(AiAgentMarketClosuresController);

        type.GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/v1/ai/agent/market-closures");
        type.GetCustomAttribute<AuthorizeAttribute>()!.AuthenticationSchemes
            .Should().Be(ApiKeyAuthenticationDefaults.Scheme);
    }
```

> **Lưu ý cho người thi hành:** mở file đó xem helper dựng controller và hằng số userId tên thật là gì (chỗ dòng 421-424 đã có khuôn `Callback<IRequest<...>, CancellationToken>` — dùng đúng khuôn đó, vì Moq bind theo kiểu tham số khai báo nên phải là `IRequest<TResponse>` rồi cast).

- [ ] **Step 7: Chạy test cho thấy xanh**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter "MarketClosureToolsTests|AiAgentExposeControllersTests"`
Expected: PASS — 2 test tool + 2 test ngang giá + toàn bộ test cũ của file expose.

- [ ] **Step 8: Commit**

```bash
git add src/InvestmentApp.Api/Controllers/MarketClosuresController.cs src/InvestmentApp.Api/Controllers/AiAgentMarketClosuresController.cs src/InvestmentApp.Api/Mcp/MarketClosureTools.cs tests/InvestmentApp.Api.Tests/Mcp/MarketClosureToolsTests.cs
git commit -m "feat(api): endpoint và tool MCP nhập/đọc/xoá lịch nghỉ giao dịch"
```

---

### Task 5: Script seed 12 ngày nghỉ 2026

**Files:**
- Create: `scripts/migrations/2026-08-12-market-closures-2026.mongo.js`
- Test: `tests/InvestmentApp.Application.Tests/MarketClosures/MarketClosureSeedConsistencyTests.cs`

**Interfaces:**
- Consumes: collection `market_closures` (Task 2).
- Produces: hằng số `Vn2026Closures` dùng chung cho test golden ở Task 6 — **12 chuỗi ngày**, đúng thứ tự tăng dần.

Nguồn: thông báo lịch nghỉ giao dịch 2026 của HOSE — Tết Dương lịch 01/01; Tết Nguyên đán Bính Ngọ 16–20/02; Giỗ Tổ Hùng Vương 27/04; 30/04–01/05; Quốc khánh 31/08–02/09. Tổng 12 phiên. T7 22/08 là ngày làm việc bù nhưng HOSE không giao dịch — đã bị loại theo `DayOfWeek`, không ghi vào seed.

- [ ] **Step 1: Viết test đỏ ghim danh sách**

Tạo `tests/InvestmentApp.Application.Tests/MarketClosures/MarketClosureSeedConsistencyTests.cs`:

```csharp
using FluentAssertions;

namespace InvestmentApp.Application.Tests.MarketClosures;

/// <summary>
/// Ghim danh sách ngày nghỉ 2026 mà các ca golden T+2 dựa vào. Sửa script seed mà không
/// sửa đây (hoặc ngược lại) thì test golden Tết vẫn xanh trong khi dữ liệu thật đã lệch.
/// </summary>
public static class Vn2026Closures
{
    public static readonly string[] Dates =
    {
        "2026-01-01",
        "2026-02-16", "2026-02-17", "2026-02-18", "2026-02-19", "2026-02-20",
        "2026-04-27",
        "2026-04-30", "2026-05-01",
        "2026-08-31", "2026-09-01", "2026-09-02"
    };
}

public class MarketClosureSeedConsistencyTests
{
    [Fact]
    public void Seed_2026_co_dung_12_phien_nghi()
    {
        Vn2026Closures.Dates.Should().HaveCount(12);
        Vn2026Closures.Dates.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Khong_ngay_nao_trong_seed_la_cuoi_tuan()
    {
        foreach (var date in Vn2026Closures.Dates)
        {
            var parsed = DateTime.Parse(date);
            parsed.DayOfWeek.Should().NotBe(DayOfWeek.Saturday, $"{date} là thứ Bảy, không cần lưu");
            parsed.DayOfWeek.Should().NotBe(DayOfWeek.Sunday, $"{date} là Chủ nhật, không cần lưu");
        }
    }

    [Fact]
    public void Script_seed_chua_dung_12_ngay_nhu_hang_so()
    {
        var scriptPath = Path.Combine(FindRepoRoot(), "scripts", "migrations",
            "2026-08-12-market-closures-2026.mongo.js");
        var script = File.ReadAllText(scriptPath);

        foreach (var date in Vn2026Closures.Dates)
            script.Should().Contain($"\"{date}\"", $"script seed phải chứa {date}");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy gốc repo");
    }
}
```

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter MarketClosureSeedConsistencyTests`
Expected: FAIL — `Script_seed_chua_dung_12_ngay_nhu_hang_so` ném `FileNotFoundException` vì script chưa tồn tại. Hai test còn lại PASS.

- [ ] **Step 3: Viết script seed**

Tạo `scripts/migrations/2026-08-12-market-closures-2026.mongo.js`:

```javascript
// Migration: seed 12 ngày nghỉ giao dịch 2026 vào collection market_closures.
// Plan: docs/superpowers/plans/2026-08-12-t2-settlement-pending-cash.md Task 5
//
// NAMING NOTES:
// - Collection: `market_closures` (snake_case, đặt trong MarketClosureRepository.cs).
// - Fields: PascalCase (mặc định của MongoDB C# driver, không có convention camelCase).
// - Date lưu nửa đêm UTC — khớp [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] trên entity.
//
// Nguồn: thông báo lịch nghỉ giao dịch năm 2026 của HOSE (12 phiên).
// T7 22/08/2026 là ngày làm việc bù nhưng HOSE không giao dịch — cuối tuần đã bị
// loại theo DayOfWeek trong code nên không cần ghi vào đây.
//
// Idempotent: chạy 2+ lần cho kết quả giống nhau (updateOne + upsert theo UserId + Date).
//
// Usage:
//   mongosh "<connection>/InvestmentApp_prod" --eval 'var USER_ID="<userId>"' scripts/migrations/2026-08-12-market-closures-2026.mongo.js

print("=== Migration 2026-08-12-market-closures-2026 ===");
print("DB: " + db.getName() + ", Collection: market_closures");

if (typeof USER_ID === "undefined" || !USER_ID) {
    throw new Error("Phải truyền USER_ID: mongosh ... --eval 'var USER_ID=\"...\"' <script>");
}

const CLOSURES = [
    { date: "2026-01-01", note: "Tết Dương lịch" },
    { date: "2026-02-16", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-02-17", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-02-18", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-02-19", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-02-20", note: "Tết Nguyên đán Bính Ngọ" },
    { date: "2026-04-27", note: "Giỗ Tổ Hùng Vương" },
    { date: "2026-04-30", note: "Ngày Chiến thắng" },
    { date: "2026-05-01", note: "Quốc tế Lao động" },
    { date: "2026-08-31", note: "Quốc khánh" },
    { date: "2026-09-01", note: "Quốc khánh" },
    { date: "2026-09-02", note: "Quốc khánh" }
];

let inserted = 0, existing = 0;

CLOSURES.forEach(function (item) {
    const utcMidnight = new Date(item.date + "T00:00:00.000Z");
    const result = db.market_closures.updateOne(
        { UserId: USER_ID, Date: utcMidnight },
        {
            $setOnInsert: {
                // Id tự sinh theo (user, ngày) thay vì UUID(): script chạy lại cho ra đúng
                // _id cũ, nên idempotent kể cả khi unique index chưa kịp tạo.
                // AggregateRoot.Id là string và driver map Id → _id theo mặc định.
                _id: "mc-" + USER_ID + "-" + item.date,
                UserId: USER_ID,
                Date: utcMidnight,
                Note: item.note,
                CreatedAt: new Date(),
                Version: 0
            }
        },
        { upsert: true }
    );
    if (result.upsertedCount > 0) inserted++;
    else existing++;
});

print(`[done] thêm mới ${inserted}, đã có sẵn ${existing}, tổng ${CLOSURES.length}`);

const total = db.market_closures.countDocuments({ UserId: USER_ID });
print(`[verify] tổng ngày nghỉ của user: ${total}`);
```

Tên field đã đối chiếu `AggregateRoot`: `Id` (string, driver map thành `_id`) và `Version` (int). Không có field nào khác trên base class cần ghi.

- [ ] **Step 4: Chạy test cho thấy xanh**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter MarketClosureSeedConsistencyTests`
Expected: PASS — 3 test.

- [ ] **Step 5: Commit**

```bash
git add scripts/migrations/2026-08-12-market-closures-2026.mongo.js tests/InvestmentApp.Application.Tests/MarketClosures/MarketClosureSeedConsistencyTests.cs
git commit -m "feat(scripts): seed 12 ngày nghỉ giao dịch 2026 kèm test ghim danh sách"
```

---

### Task 6: `VietnamDate.Today` + `SettlementCalculator`

**Files:**
- Modify: `src/InvestmentApp.Application/Common/VietnamDate.cs` (thêm một method)
- Create: `src/InvestmentApp.Application/Common/SettlementCalculator.cs`
- Test: `tests/InvestmentApp.Application.Tests/Common/SettlementCalculatorTests.cs`

**Interfaces:**
- Consumes: `Trade` (`TradeDate`, `TradeType`, `Quantity`, `Price`, `Fee`, `Tax`), `Vn2026Closures.Dates` (Task 5).
- Produces:
  - `VietnamDate.Today(DateTime utcNow) → DateTime`
  - `SettlementCalculator.SettlementSessions` = `2`
  - `SettlementCalculator.IsTradingDay(DateTime date, IReadOnlySet<DateOnly> closedDates) → bool`
  - `SettlementCalculator.SettlementDateOf(DateTime tradeDate, IReadOnlySet<DateOnly> closedDates) → DateTime`
  - `SettlementCalculator.PendingSellProceeds(IEnumerable<Trade> trades, DateTime asOfVnDate, IReadOnlySet<DateOnly> closedDates) → (decimal Amount, DateTime? LastArrivalDate)`

- [ ] **Step 1: Viết test đỏ**

Tạo `tests/InvestmentApp.Application.Tests/Common/SettlementCalculatorTests.cs`:

```csharp
using FluentAssertions;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Tests.MarketClosures;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Common;

public class SettlementCalculatorTests
{
    private static readonly IReadOnlySet<DateOnly> Closures2026 =
        Vn2026Closures.Dates.Select(d => DateOnly.Parse(d)).ToHashSet();

    private static readonly IReadOnlySet<DateOnly> NoClosures = new HashSet<DateOnly>();

    private static Trade Sell(string date, decimal qty, decimal price, decimal fee = 0m, decimal tax = 0m)
        => new(portfolioId: "p1", symbol: "HHV", tradeType: TradeType.SELL,
            quantity: qty, price: price, fee: fee, tax: tax, tradeDate: DateTime.Parse(date));

    private static Trade Buy(string date, decimal qty, decimal price)
        => new(portfolioId: "p1", symbol: "HHV", tradeType: TradeType.BUY,
            quantity: qty, price: price, fee: 0m, tax: 0m, tradeDate: DateTime.Parse(date));

    // Hai ca dưới lấy thẳng từ thông báo của HOSE: nghỉ Tết 16–20/02/2026 nên giao dịch
    // ngày 12/02 thanh toán 23/02, và giao dịch ngày 13/02 thanh toán 24/02.
    [Theory]
    [InlineData("2026-02-12", "2026-02-23")]
    [InlineData("2026-02-13", "2026-02-24")]
    public void Golden_Tet_2026_khop_thong_bao_HOSE(string tradeDate, string expected)
    {
        SettlementCalculator.SettlementDateOf(DateTime.Parse(tradeDate), Closures2026)
            .Should().Be(DateTime.Parse(expected));
    }

    [Fact]
    public void Ban_thu_Nam_thi_tien_ve_thu_Hai_vi_vat_qua_cuoi_tuan()
    {
        // 2026-06-11 là thứ Năm, không có lễ quanh đó: T+1 = thứ Sáu 12/6, T+2 = thứ Hai 15/6.
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 6, 11), Closures2026)
            .Should().Be(new DateTime(2026, 6, 15));
    }

    [Fact]
    public void Khong_co_ngay_nghi_nao_thi_chi_bo_cuoi_tuan()
    {
        // Cùng ngày 12/02 nhưng tập ngày nghỉ rỗng: T+1 = 13/2 (thứ Sáu), T+2 = 16/2 (thứ Hai).
        SettlementCalculator.SettlementDateOf(new DateTime(2026, 2, 12), NoClosures)
            .Should().Be(new DateTime(2026, 2, 16));
    }

    [Fact]
    public void Tien_cho_ve_tru_phi_va_thue()
    {
        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m, fee: 30_000m, tax: 20_000m) };

        var (amount, _) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 12), Closures2026);

        amount.Should().Be(1_000m * 20_000m - 30_000m - 20_000m);
    }

    [Fact]
    public void Lenh_da_ve_thi_khong_con_tinh_la_cho_ve()
    {
        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m) };

        // asOf = đúng ngày về (15/6) → đã về, không còn chờ.
        var (amount, last) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 15), Closures2026);

        amount.Should().Be(0m);
        last.Should().BeNull();
    }

    [Fact]
    public void Lenh_mua_khong_tao_tien_cho_ve()
    {
        var trades = new[] { Buy("2026-06-11", 1_000m, 20_000m) };

        var (amount, _) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 12), Closures2026);

        amount.Should().Be(0m);
    }

    [Fact]
    public void Ngay_ve_hien_thi_la_moc_xa_nhat_trong_cac_lenh_dang_cho()
    {
        var trades = new[]
        {
            Sell("2026-06-11", 100m, 10_000m),  // về 15/6
            Sell("2026-06-12", 100m, 10_000m)   // về 16/6
        };

        var (_, last) = SettlementCalculator.PendingSellProceeds(
            trades, new DateTime(2026, 6, 12), Closures2026);

        last.Should().Be(new DateTime(2026, 6, 16));
    }

    [Fact]
    public void Bat_bien_da_ve_cong_cho_ve_bang_TotalSold()
    {
        var trades = new[]
        {
            Sell("2026-05-04", 500m, 15_000m, fee: 10_000m),   // đã về từ lâu
            Sell("2026-06-11", 300m, 21_000m, fee: 8_000m),    // còn chờ
            Buy("2026-06-01", 1_000m, 12_000m)                 // không liên quan
        };
        var asOf = new DateTime(2026, 6, 12);

        var totalSold = trades
            .Where(t => t.TradeType == TradeType.SELL)
            .Sum(t => t.Quantity * t.Price - t.Fee - t.Tax);

        var (pending, _) = SettlementCalculator.PendingSellProceeds(trades, asOf, Closures2026);
        var settled = totalSold - pending;

        settled.Should().Be(500m * 15_000m - 10_000m);
        (settled + pending).Should().Be(totalSold);
    }

    [Fact]
    public void Hom_nay_tinh_theo_gio_Viet_Nam_khong_phai_UTC()
    {
        // 01:00 giờ VN ngày 15/6 = 18:00 UTC ngày 14/6. Dùng ngày UTC sẽ ra 14/6 và
        // giữ tiền ở trạng thái chờ về thêm một ngày.
        var utc = new DateTime(2026, 6, 14, 18, 0, 0, DateTimeKind.Utc);

        VietnamDate.Today(utc).Should().Be(new DateTime(2026, 6, 15));

        var trades = new[] { Sell("2026-06-11", 1_000m, 20_000m) };
        var (amount, _) = SettlementCalculator.PendingSellProceeds(
            trades, VietnamDate.Today(utc), Closures2026);

        amount.Should().Be(0m, "tiền về ngày 15/6, mà giờ VN đã sang 15/6");
    }
}
```

> **Lưu ý cho người thi hành:** mở `src/InvestmentApp.Domain/Entities/Trade.cs` xem thứ tự và tên tham số constructor thật rồi sửa hai helper `Sell`/`Buy` cho khớp. Constructor có normalize `Symbol` bằng `ToUpper().Trim()`, đừng bọc thêm.

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter SettlementCalculatorTests`
Expected: FAIL — `CS0246` cho `SettlementCalculator`, và `CS0117` cho `VietnamDate.Today`.

- [ ] **Step 3: Thêm `Today` vào `VietnamDate`**

Vào `src/InvestmentApp.Application/Common/VietnamDate.cs`, thêm ngay sau `ToDateKey`:

```csharp
    /// <summary>Ngày lịch VN của một mốc UTC, phần giờ bằng 0.</summary>
    public static DateTime Today(DateTime utcNow) => ToLocal(utcNow).Date;
```

- [ ] **Step 4: Implement `SettlementCalculator`**

Tạo `src/InvestmentApp.Application/Common/SettlementCalculator.cs`:

```csharp
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Chu kỳ thanh toán T+2 của chứng khoán Việt Nam: bán hôm nay thì tiền về sau 2 phiên
/// giao dịch. Hàm thuần, không I/O — tập ngày nghỉ do caller nạp từ
/// <c>IMarketClosureRepository</c> rồi truyền vào, nên test được không cần DB.
/// </summary>
public static class SettlementCalculator
{
    public const int SettlementSessions = 2;

    /// <summary>Phiên giao dịch = không phải T7/CN và không nằm trong danh sách nghỉ lễ.</summary>
    public static bool IsTradingDay(DateTime date, IReadOnlySet<DateOnly> closedDates)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
           && !closedDates.Contains(DateOnly.FromDateTime(date.Date));

    /// <summary>Ngày tiền/cổ phiếu về ví: <paramref name="tradeDate"/> cộng 2 phiên giao dịch.</summary>
    public static DateTime SettlementDateOf(DateTime tradeDate, IReadOnlySet<DateOnly> closedDates)
    {
        var date = tradeDate.Date;
        var counted = 0;

        while (counted < SettlementSessions)
        {
            date = date.AddDays(1);
            if (IsTradingDay(date, closedDates)) counted++;
        }

        return date;
    }

    /// <summary>
    /// Tiền bán chưa về ví tại ngày <paramref name="asOfVnDate"/> (phải là ngày lịch VN —
    /// dùng <see cref="VietnamDate.Today"/>), kèm mốc về XA NHẤT trong các lệnh còn chờ.
    /// </summary>
    public static (decimal Amount, DateTime? LastArrivalDate) PendingSellProceeds(
        IEnumerable<Trade> trades, DateTime asOfVnDate, IReadOnlySet<DateOnly> closedDates)
    {
        var asOf = asOfVnDate.Date;
        var total = 0m;
        DateTime? last = null;

        foreach (var trade in trades)
        {
            if (trade.TradeType != TradeType.SELL) continue;

            // .Date cả hai vế: bản ghi cũ trong Mongo có thể không còn là nửa đêm.
            var arrival = SettlementDateOf(trade.TradeDate.Date, closedDates);
            if (arrival <= asOf) continue;

            total += trade.Quantity * trade.Price - trade.Fee - trade.Tax;
            if (last is null || arrival > last.Value) last = arrival;
        }

        return (total, last);
    }
}
```

- [ ] **Step 5: Chạy test cho thấy xanh**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter SettlementCalculatorTests`
Expected: PASS — 11 test (2 theory + 9 fact).

- [ ] **Step 6: Commit**

```bash
git add src/InvestmentApp.Application/Common/VietnamDate.cs src/InvestmentApp.Application/Common/SettlementCalculator.cs tests/InvestmentApp.Application.Tests/Common/SettlementCalculatorTests.cs
git commit -m "feat(app): SettlementCalculator tính ngày về T+2 và tiền bán chờ về"
```

---

### Task 7: `PendingSettlementCash` vào `PortfolioSummaryDto`

**Files:**
- Modify: `src/InvestmentApp.Application/Portfolios/Queries/GetAllPortfolios/GetAllPortfoliosQuery.cs`
- Modify: `frontend/src/app/core/services/portfolio.service.ts:16` (thêm 2 field vào interface `PortfolioSummary`)
- Test: `tests/InvestmentApp.Application.Tests/Portfolios/Queries/GetAllPortfoliosQueryHandlerTests.cs` (file đã tồn tại — thêm test, giữ nguyên test cũ)

**Interfaces:**
- Consumes: `SettlementCalculator` (Task 6), `IMarketClosureRepository` (Task 2), `VietnamDate.Today` (Task 6).
- Produces: `PortfolioSummaryDto.PendingSettlementCash` (`decimal`), `PortfolioSummaryDto.PendingSettlementArrivalDate` (`DateTime?`).

- [ ] **Step 1: Viết test đỏ**

Thêm vào `tests/InvestmentApp.Application.Tests/Portfolios/Queries/GetAllPortfoliosQueryHandlerTests.cs`. Constructor của handler nhận thêm một tham số nên mọi test cũ trong file sẽ vỡ build — sửa chỗ dựng handler cho khớp, đừng xoá test cũ:

```csharp
    [Fact]
    public async Task Tien_ban_chua_ve_duoc_tach_ra_va_khong_vuot_TotalSold()
    {
        // Lệnh bán 2 ngày trước hôm nay theo giờ VN → chắc chắn còn chờ về.
        var todayVn = VietnamDate.Today(DateTime.UtcNow);
        var recentSell = todayVn.AddDays(-1);

        _portfolioRepository.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Portfolio("Chính", 100_000_000m, "user1") });
        _tradeRepository.Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Trade("p1", "HHV", TradeType.SELL, 1_000m, 20_000m, 30_000m, 20_000m, recentSell)
            });
        _capitalFlowRepository.Setup(r => r.GetTotalFlowByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _marketClosureRepository.Setup(r => r.GetByUserAndRangeAsync("user1",
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MarketClosure>());

        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        var summary = result.Single();
        summary.PendingSettlementCash.Should().Be(1_000m * 20_000m - 30_000m - 20_000m);
        summary.PendingSettlementCash.Should().BeLessThanOrEqualTo(summary.TotalSold);
        summary.PendingSettlementArrivalDate.Should().NotBeNull();
        summary.PendingSettlementArrivalDate!.Value.Should().BeAfter(todayVn);
    }

    [Fact]
    public async Task Khong_co_lenh_ban_nao_thi_tien_cho_ve_bang_khong()
    {
        _portfolioRepository.Setup(r => r.GetByUserIdAsync("user1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Portfolio("Chính", 100_000_000m, "user1") });
        _tradeRepository.Setup(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Trade>());
        _capitalFlowRepository.Setup(r => r.GetTotalFlowByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _marketClosureRepository.Setup(r => r.GetByUserAndRangeAsync("user1",
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MarketClosure>());

        var result = await _handler.Handle(new GetAllPortfoliosQuery { UserId = "user1" }, CancellationToken.None);

        result.Single().PendingSettlementCash.Should().Be(0m);
        result.Single().PendingSettlementArrivalDate.Should().BeNull();
    }
```

> **Đã đối chiếu file test thật (2026-08-12):** mock tên là `_portfolioRepo`, `_tradeRepo`, `_flowRepo` — **không** phải `_portfolioRepository`. Constructor là `new Portfolio(userId, name, initialCapital)` — **userId đứng TRƯỚC**. Đổi hai đoạn test ở trên cho khớp, thêm field `_closureRepo` và truyền vào `_handler` làm tham số thứ tư. File này **không** cần `using Xunit;` (project Application.Tests có global using), khác với Domain.Tests.

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter GetAllPortfoliosQueryHandlerTests`
Expected: FAIL — build error: `PortfolioSummaryDto` không có `PendingSettlementCash`, và constructor handler không nhận `IMarketClosureRepository`.

- [ ] **Step 3: Sửa handler + DTO**

Trong `src/InvestmentApp.Application/Portfolios/Queries/GetAllPortfolios/GetAllPortfoliosQuery.cs`:

Thêm `using InvestmentApp.Application.Common;` ở đầu file. Thêm field + tham số constructor:

```csharp
    private readonly IMarketClosureRepository _marketClosureRepository;

    public GetAllPortfoliosQueryHandler(
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository,
        ICapitalFlowRepository capitalFlowRepository,
        IMarketClosureRepository marketClosureRepository)
    {
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
        _capitalFlowRepository = capitalFlowRepository;
        _marketClosureRepository = marketClosureRepository;
    }
```

Trong `Handle`, nạp lịch nghỉ **một lần trước vòng lặp** (không nạp lại mỗi danh mục):

```csharp
        var portfolios = await _portfolioRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var result = new List<PortfolioSummaryDto>();

        // Nạp một lần cho mọi danh mục. Cửa sổ ±30 ngày quanh hôm nay là quá đủ:
        // T+2 chỉ bước tối đa vài ngày kể từ lệnh gần nhất.
        var todayVn = VietnamDate.Today(DateTime.UtcNow);
        var closures = await _marketClosureRepository.GetByUserAndRangeAsync(
            request.UserId, todayVn.AddDays(-30), todayVn.AddDays(30), cancellationToken);
        var closedDates = closures.Select(c => DateOnly.FromDateTime(c.Date)).ToHashSet();
```

Trong thân vòng lặp, sau khi tính `totalSold`:

```csharp
            var (pendingCash, pendingArrival) = SettlementCalculator.PendingSellProceeds(
                tradeList, todayVn, closedDates);
```

Và thêm vào object khởi tạo `PortfolioSummaryDto`:

```csharp
                TotalSold = totalSold,
                PendingSettlementCash = pendingCash,
                PendingSettlementArrivalDate = pendingArrival
```

Thêm vào `PortfolioSummaryDto`:

```csharp
    /// <summary>Tiền bán chưa về ví theo chu kỳ T+2. Đã nằm TRONG <see cref="TotalSold"/>.</summary>
    public decimal PendingSettlementCash { get; set; }

    /// <summary>Ngày về xa nhất trong các lệnh còn chờ. <c>null</c> khi không còn gì chờ.</summary>
    public DateTime? PendingSettlementArrivalDate { get; set; }
```

- [ ] **Step 4: Chạy test cho thấy xanh**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter GetAllPortfoliosQueryHandlerTests`
Expected: PASS — toàn bộ test cũ + 2 test mới.

- [ ] **Step 5: Chạy cả bộ backend để bắt call-site vỡ**

Run: `dotnet test`
Expected: PASS toàn bộ. Nếu có chỗ khác dựng `GetAllPortfoliosQueryHandler` bằng tay (test MCP, test controller) thì sửa cho khớp constructor mới. **Đếm số project trong output** — thiếu project nào là do DLL bị khoá, tắt backend đang chạy rồi chạy lại.

- [ ] **Step 6: Thêm field vào interface frontend**

Trong `frontend/src/app/core/services/portfolio.service.ts`, thêm vào interface `PortfolioSummary` (cạnh `totalSold` ở dòng 16):

```typescript
  pendingSettlementCash: number;
  pendingSettlementArrivalDate: string | null;
```

- [ ] **Step 7: Sửa fixture frontend đang vỡ type**

Run: `cd frontend && npx tsc --noEmit -p tsconfig.spec.json`
Expected: lỗi ở các fixture dựng `PortfolioSummary` thiếu 2 field mới — thêm `pendingSettlementCash: 0, pendingSettlementArrivalDate: null` vào từng chỗ. Ít nhất hai file: `capital-flows.component.spec.ts:21` và `trade-create.component.spec.ts:109-110`.

- [ ] **Step 8: Commit**

```bash
git add src/InvestmentApp.Application/Portfolios/Queries/GetAllPortfolios/GetAllPortfoliosQuery.cs tests/InvestmentApp.Application.Tests/Portfolios/Queries/GetAllPortfoliosQueryHandlerTests.cs frontend/src/app/core/services/portfolio.service.ts frontend/src/app/features/capital-flows/capital-flows.component.spec.ts frontend/src/app/features/trades/trade-create/trade-create.component.spec.ts
git commit -m "feat(app): PortfolioSummaryDto mang tiền bán chờ về và ngày về dự kiến"
```

---

### Task 8: Dòng "chờ về" trên hero card

**Files:**
- Modify: `frontend/src/app/features/dashboard/dashboard.component.ts` (template quanh dòng 372, getters quanh dòng 737)
- Modify: `frontend/src/app/features/capital-flows/capital-flows.component.ts` (template dòng 75 và 222, getters dòng 406 và `overallView` dòng 446)
- Test: `frontend/src/app/features/capital-flows/capital-flows.component.spec.ts`

**Interfaces:**
- Consumes: `PortfolioSummary.pendingSettlementCash`, `.pendingSettlementArrivalDate` (Task 7).
- Produces: getter `pendingSettlementCash`, `pendingSettlementLabel` trên cả hai component; field `pendingSettlementCash`, `pendingSettlementLabel` trên `CapitalView`.

- [ ] **Step 1: Viết test đỏ**

Thêm vào `frontend/src/app/features/capital-flows/capital-flows.component.spec.ts`:

```typescript
  describe('tiền bán chờ về T+2', () => {
    it('cộng dồn tiền chờ về của mọi danh mục trong overallView', () => {
      component.portfolios = [
        portfolio({ id: 'p1', currentCapital: 100_000_000, totalInvested: 80_000_000, totalSold: 30_000_000, pendingSettlementCash: 30_000_000, pendingSettlementArrivalDate: '2026-06-16' }),
        portfolio({ id: 'p2', currentCapital: 70_000_000, totalInvested: 20_000_000, totalSold: 10_000_000, pendingSettlementCash: 0, pendingSettlementArrivalDate: null })
      ];
      component.overallSummary = overallSummary({ totalCurrentCapital: 170_000_000 });

      expect(component.overallView!.pendingSettlementCash).toBe(30_000_000);
    });

    it('nhãn ghi ngày về xa nhất trong các danh mục', () => {
      component.portfolios = [
        portfolio({ id: 'p1', pendingSettlementCash: 10_000_000, pendingSettlementArrivalDate: '2026-06-15' }),
        portfolio({ id: 'p2', pendingSettlementCash: 5_000_000, pendingSettlementArrivalDate: '2026-06-17' })
      ];
      component.overallSummary = overallSummary({ totalCurrentCapital: 170_000_000 });

      expect(component.overallView!.pendingSettlementLabel).toBe('dự kiến 17/06');
    });

    it('không có gì chờ về thì nhãn rỗng', () => {
      component.portfolios = [portfolio({ id: 'p1', pendingSettlementCash: 0, pendingSettlementArrivalDate: null })];
      component.overallSummary = overallSummary({ totalCurrentCapital: 100_000_000 });

      expect(component.overallView!.pendingSettlementCash).toBe(0);
      expect(component.overallView!.pendingSettlementLabel).toBe('');
    });
  });
```

> **Lưu ý cho người thi hành:** file spec này đã có helper `portfolio(...)`; mở ra xem nó có sẵn `overallSummary(...)` hay tên khác, dùng đúng tên thật. Nhớ đây là ngày dạng chuỗi `"YYYY-MM-DD"` — **không** dùng `new Date("2026-06-17").getMonth()`, vì parse ra UTC rồi đọc bằng getter local là lệch tháng ở múi giờ âm. Cắt chuỗi hoặc dùng `Date.UTC` + `getUTC*`.

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `cd frontend && npm test -- --watch=false --include='**/capital-flows.component.spec.ts'`
Expected: FAIL — `pendingSettlementCash` không tồn tại trên `overallView`.

- [ ] **Step 3: Thêm getters vào `capital-flows.component.ts`**

Thêm vào interface `CapitalView` (dòng 17, cạnh `cashBalance`):

```typescript
  pendingSettlementCash: number;
  pendingSettlementLabel: string;
```

Thêm getters cạnh `cashBalance` (dòng 406):

```typescript
  get pendingSettlementCash(): number {
    return this.selectedPortfolio?.pendingSettlementCash || 0;
  }

  get pendingSettlementLabel(): string {
    return CapitalFlowsComponent.arrivalLabel(
      [this.selectedPortfolio?.pendingSettlementArrivalDate || null],
      this.pendingSettlementCash);
  }

  /// Nhãn ngày về. Cắt chuỗi "YYYY-MM-DD" thay vì new Date(...) — parse ra UTC rồi
  /// đọc bằng getMonth() local là lệch tháng ở múi giờ âm.
  static arrivalLabel(dates: (string | null)[], amount: number): string {
    if (amount <= 0) return '';
    const latest = dates.filter((d): d is string => !!d).sort().pop();
    if (!latest) return '';
    const [, month, day] = latest.split('-');
    return `dự kiến ${day}/${month}`;
  }
```

Trong `overallView` (dòng 446), thêm vào phần tính và object trả về:

```typescript
    const pendingSettlementCash = this.portfolios.reduce((sum, p) => sum + (p.pendingSettlementCash || 0), 0);
    const pendingSettlementLabel = CapitalFlowsComponent.arrivalLabel(
      this.portfolios.map(p => p.pendingSettlementArrivalDate || null), pendingSettlementCash);
```

```typescript
      pendingSettlementCash,
      pendingSettlementLabel,
```

- [ ] **Step 4: Thêm dòng vào template `capital-flows`**

Ngay dưới dòng 75 (`overallView.cashBalance`):

```html
            @if (overallView.pendingSettlementCash > 0) {
              <div class="text-xs text-amber-700 mt-0.5">
                trong đó {{ overallView.pendingSettlementCash | vndCurrency }} chờ về — {{ overallView.pendingSettlementLabel }}
              </div>
            }
```

Và dưới dòng 222 (`cashBalance` của danh mục đang chọn):

```html
            @if (pendingSettlementCash > 0) {
              <div class="text-xs text-amber-700 mt-0.5">
                trong đó {{ pendingSettlementCash | vndCurrency }} chờ về — {{ pendingSettlementLabel }}
              </div>
            }
```

- [ ] **Step 5: Chạy test cho thấy xanh**

Run: `cd frontend && npm test -- --watch=false --include='**/capital-flows.component.spec.ts'`
Expected: PASS.

- [ ] **Step 6: Làm tương tự cho `dashboard.component.ts`**

Thêm getters cạnh `cashBalance` (dòng 737):

```typescript
  get pendingSettlementCash(): number {
    return this.portfolioSummaries.reduce((s, p) => s + (p.pendingSettlementCash || 0), 0);
  }

  get pendingSettlementLabel(): string {
    if (this.pendingSettlementCash <= 0) return '';
    const latest = this.portfolioSummaries
      .map(p => p.pendingSettlementArrivalDate)
      .filter((d): d is string => !!d)
      .sort()
      .pop();
    if (!latest) return '';
    const [, month, day] = latest.split('-');
    return `dự kiến ${day}/${month}`;
  }
```

Và ngay dưới dòng 372 trong template:

```html
              @if (pendingSettlementCash > 0) {
                <div class="text-xs text-amber-700 mt-0.5">
                  trong đó {{ pendingSettlementCash | vndCurrency }} chờ về — {{ pendingSettlementLabel }}
                </div>
              }
```

- [ ] **Step 7: Build frontend**

Run: `cd frontend && npm run build`
Expected: Build thành công, 0 error.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/app/features/capital-flows/ frontend/src/app/features/dashboard/dashboard.component.ts
git commit -m "feat(ui): hero card hiện tiền bán chờ về T+2 và ngày về dự kiến"
```

---

### Task 9: Cảnh báo mềm ở cửa sổ ghi lệnh MUA

**Files:**
- Modify: `frontend/src/app/features/trades/trade-create/trade-create.component.ts` (method `validateQuantity` dòng 474-495, template)
- Test: `frontend/src/app/features/trades/trade-create/trade-create.component.spec.ts`

**Interfaces:**
- Consumes: `PortfolioSummary.pendingSettlementCash` (Task 7).
- Produces: field `settlementWarning: string` trên component.

**Bắt buộc dùng field RIÊNG, không dùng lại `quantityError`.** `quantityError` là chuỗi chặn lưu và cũng đang phục vụ nhánh BÁN; nhồi cảnh báo vào đó là biến cảnh báo thành chặn cứng, đúng cái mà Q7 của spec đã loại.

- [ ] **Step 1: Viết test đỏ**

Thêm vào `frontend/src/app/features/trades/trade-create/trade-create.component.spec.ts`:

```typescript
  describe('cảnh báo tiền bán chưa về', () => {
    it('vượt tiền đã về thì cảnh báo nhưng KHÔNG chặn lưu', () => {
      // Tổng tiền 100tr, trong đó 40tr chờ về → đã về 60tr. Lệnh 70tr.
      component.portfolios = [portfolioFixture({
        id: 'p1', currentCapital: 100_000_000, totalInvested: 0, totalSold: 0,
        pendingSettlementCash: 40_000_000, pendingSettlementArrivalDate: '2026-06-17'
      })];
      component.form.portfolioId = 'p1';
      component.form.tradeType = TradeType.BUY;
      component.form.quantity = 7_000;
      component.form.price = 10_000;

      component.validateQuantity();

      expect(component.settlementWarning).toContain('ứng trước tiền bán');
      expect(component.settlementWarning).toContain('10.000.000');
      expect(component.quantityError).toBe('');
    });

    it('nằm trong tiền đã về thì không cảnh báo', () => {
      component.portfolios = [portfolioFixture({
        id: 'p1', currentCapital: 100_000_000, totalInvested: 0, totalSold: 0,
        pendingSettlementCash: 40_000_000, pendingSettlementArrivalDate: '2026-06-17'
      })];
      component.form.portfolioId = 'p1';
      component.form.tradeType = TradeType.BUY;
      component.form.quantity = 5_000;
      component.form.price = 10_000;

      component.validateQuantity();

      expect(component.settlementWarning).toBe('');
    });

    it('lệnh BÁN không bị cảnh báo tiền chờ về', () => {
      component.portfolios = [portfolioFixture({
        id: 'p1', currentCapital: 100_000_000, pendingSettlementCash: 40_000_000
      })];
      component.form.portfolioId = 'p1';
      component.form.tradeType = TradeType.SELL;
      component.form.quantity = 1_000;
      component.form.price = 10_000;

      component.validateQuantity();

      expect(component.settlementWarning).toBe('');
    });
  });
```

> **Lưu ý cho người thi hành:** file spec này đã có fixture danh mục ở dòng 109-110; mở ra xem nó là helper hay array literal, dùng đúng dạng thật thay cho `portfolioFixture(...)`.

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `cd frontend && npm test -- --watch=false --include='**/trade-create.component.spec.ts'`
Expected: FAIL — `settlementWarning` không tồn tại.

- [ ] **Step 3: Implement**

Thêm field cạnh `quantityError`:

```typescript
  /// Cảnh báo mềm, KHÔNG chặn lưu: form này ghi lệnh đã khớp, có thể đã dùng
  /// dịch vụ ứng trước tiền bán. Tách khỏi quantityError vì chuỗi đó chặn lưu.
  settlementWarning = '';
```

Trong `validateQuantity()`, thêm ngay đầu method (reset) và trong nhánh BUY, sau khối `remainingCash` hiện có:

```typescript
  validateQuantity(): void {
    this.quantityError = '';
    this.settlementWarning = '';
    if (this.form.tradeType === TradeType.BUY && this.form.quantity > 0) {
      if (this.form.quantity % 100 !== 0) {
        this.quantityError = 'Lệnh MUA phải là lô chẵn (bội số của 100)';
      } else if (this.form.price > 0) {
        const tradeValue = this.form.quantity * this.form.price;
        const portfolio = this.portfolios.find(p => p.id === this.form.portfolioId);
        if (portfolio) {
          const remainingCash = portfolio.currentCapital - portfolio.totalInvested + portfolio.totalSold;
          if (tradeValue > remainingCash) {
            this.quantityError = `Giá trị lệnh (${tradeValue.toLocaleString('vi-VN')}đ) vượt quá tiền còn lại của danh mục (${remainingCash.toLocaleString('vi-VN')}đ)`;
          } else {
            const settledCash = remainingCash - (portfolio.pendingSettlementCash || 0);
            if (tradeValue > settledCash) {
              const shortfall = tradeValue - settledCash;
              this.settlementWarning =
                `Vượt tiền đã về ${shortfall.toLocaleString('vi-VN')}đ — cần ứng trước tiền bán.`;
            }
          }
        }
      }
    }
    // ... nhánh SELL giữ nguyên
  }
```

Thêm vào template, ngay dưới chỗ đang hiện `quantityError`:

```html
        @if (settlementWarning) {
          <p class="text-xs text-amber-700 mt-1">{{ settlementWarning }}</p>
        }
```

- [ ] **Step 4: Chạy test cho thấy xanh**

Run: `cd frontend && npm test -- --watch=false --include='**/trade-create.component.spec.ts'`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/trades/trade-create/
git commit -m "feat(ui): cảnh báo mềm khi lệnh mua vượt phần tiền đã về"
```

---

### Task 10: Bản tin AI

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs` (`FormatCashNetWorthSection` dòng 96-119, chỗ dựng section, dòng hướng dẫn 2153)
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs`

**Interfaces:**
- Consumes: `SettlementCalculator` (Task 6), `IMarketClosureRepository` (Task 2).
- Produces: 2 tham số mới trên `FormatCashNetWorthSection`: `decimal? pendingSettlementCash`, `DateTime? closuresKnownThrough` — đặt **cuối** danh sách, sau `missingCashPortfolios`, có default `null` để mọi call-site cũ không vỡ.

- [ ] **Step 1: Viết test đỏ**

Thêm vào `tests/InvestmentApp.Infrastructure.Tests/Services/AiAssistantServiceDailyDigestTests.cs`:

```csharp
    [Fact]
    public void Tien_ban_cho_ve_duoc_in_tach_khoi_portfolio_cash()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 287_903_688m, portfolioCash: 287_903_688m,
            idleCash: null, netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 0,
            pendingSettlementCash: 30_000_000m,
            closuresKnownThrough: new DateTime(2026, 9, 2));

        section.Should().Contain("<portfolio_cash>287,903,688 VND</portfolio_cash>");
        section.Should().Contain("<portfolio_cash_pending>30,000,000 VND</portfolio_cash_pending>");
        section.Should().Contain("<market_closures_known_through>2026-09-02</market_closures_known_through>");
    }

    [Fact]
    public void Khong_co_tien_cho_ve_thi_khong_in_tag_pending()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 100_000_000m, portfolioCash: 100_000_000m,
            idleCash: null, netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 0,
            pendingSettlementCash: 0m,
            closuresKnownThrough: new DateTime(2026, 9, 2));

        section.Should().NotContain("portfolio_cash_pending");
    }

    [Fact]
    public void Thieu_trades_thi_pending_la_na_tuyet_doi_khong_phai_0()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 0m, portfolioCash: null,
            idleCash: null, netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 1,
            pendingSettlementCash: null,
            closuresKnownThrough: null);

        section.Should().Contain("<portfolio_cash>n/a</portfolio_cash>");
        section.Should().Contain("<portfolio_cash_pending>n/a</portfolio_cash_pending>");
        section.Should().NotContain("<portfolio_cash_pending>0 VND</portfolio_cash_pending>");
    }

    [Fact]
    public void Chua_nhap_lich_nghi_nao_thi_known_through_la_na()
    {
        var section = AiAssistantService.FormatCashNetWorthSection(
            investableCapital: 100_000_000m, portfolioCash: 100_000_000m,
            idleCash: null, netWorth: null, totalAssets: null, totalDebt: null, healthScore: null,
            missingCashPortfolios: 0,
            pendingSettlementCash: 5_000_000m,
            closuresKnownThrough: null);

        section.Should().Contain("<market_closures_known_through>n/a</market_closures_known_through>");
    }
```

Chú ý ba trạng thái phân biệt của `pendingSettlementCash`: `null` = chưa tính được (in `n/a`), `0` = không có gì chờ (không in tag), `> 0` = in số. Gộp `null` với `0` là đúng hình thái bug mà ADR-0007 đã sửa một lần.

- [ ] **Step 2: Chạy test cho thấy đỏ**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter AiAssistantServiceDailyDigestTests --logger "console;verbosity=detailed"`
Expected: FAIL — build error: `FormatCashNetWorthSection` không có tham số `pendingSettlementCash`.

- [ ] **Step 3: Sửa `FormatCashNetWorthSection`**

Đổi signature (2 tham số mới ở cuối, có default):

```csharp
    public static string FormatCashNetWorthSection(decimal investableCapital, decimal? portfolioCash,
        decimal? idleCash, decimal? netWorth, decimal? totalAssets, decimal? totalDebt, int? healthScore,
        int missingCashPortfolios = 0,
        decimal? pendingSettlementCash = null,
        DateTime? closuresKnownThrough = null)
```

Thêm ngay sau dòng in `<portfolio_cash>` (dòng 107):

```csharp
        // Ba trạng thái: null = chưa tính được → n/a; 0 = không có gì chờ → không in;
        // > 0 = in số. Gộp null với 0 là nói "không có tiền chờ" khi thật ra là "chưa biết".
        if (!pendingSettlementCash.HasValue)
            sb.AppendLine("  <portfolio_cash_pending>n/a</portfolio_cash_pending>");
        else if (pendingSettlementCash.Value > 0)
            sb.AppendLine($"  <portfolio_cash_pending>{Vnd(pendingSettlementCash)}</portfolio_cash_pending>");

        sb.AppendLine($"  <market_closures_known_through>{(closuresKnownThrough.HasValue ? closuresKnownThrough.Value.ToString("yyyy-MM-dd") : "n/a")}</market_closures_known_through>");
```

- [ ] **Step 4: Nối dữ liệu thật vào chỗ dựng section**

Trong `AiAssistantService`, tại vòng lặp dựng digest (quanh dòng 1982), cộng dồn tiền chờ về song song với `cash`:

```csharp
            decimal? pending = trades != null
                ? SettlementCalculator.PendingSellProceeds(trades, todayVn, closedDates).Amount
                : null;
```

Trong đó `todayVn` và `closedDates` dựng **một lần** trước vòng lặp, cùng chỗ nạp các task khác:

```csharp
        var todayVn = VietnamDate.Today(DateTime.UtcNow);
        var closures = await _marketClosureRepository.GetByUserAndRangeAsync(
            userId, todayVn.AddDays(-30), todayVn.AddDays(30), ct);
        var closedDates = closures.Select(c => DateOnly.FromDateTime(c.Date)).ToHashSet();
        var closuresKnownThrough = await _marketClosureRepository.GetLatestDateAsync(userId, ct);
```

`AiAssistantService` cần thêm `IMarketClosureRepository` vào constructor.

Thêm `decimal? PendingCash` vào record `PortfolioDigestRow` và truyền `pending` vào khi dựng row, ngay cạnh `cash`. Rồi cộng dồn **đúng khuôn `totalCash` đang dùng ở dòng 2034-2036** — cùng luật, cùng hình dạng, không phát minh luật mới:

```csharp
        decimal? totalPendingCash = portfolioRows.Count > 0 && portfolioRows.All(r => !r.PendingCash.HasValue)
            ? null
            : portfolioRows.Where(r => r.PendingCash.HasValue).Sum(r => r.PendingCash!.Value);
```

Cuối cùng truyền vào lời gọi `FormatCashNetWorthSection` ở dòng 2045-2049, thêm hai tham số vào cuối:

```csharp
        sb.AppendLine(FormatCashNetWorthSection(
            investableCapital, totalCash, idleCash,
            profile?.GetNetWorth(totalMarketValue), profile?.GetTotalAssets(totalMarketValue),
            profile?.GetTotalDebt(), profile?.CalculateHealthScore(totalMarketValue),
            portfolioRows.Count(r => !r.Cash.HasValue),
            totalPendingCash,
            closuresKnownThrough));
```

`investableCapital` (dòng 2042) **giữ nguyên** — nó là tổng nền vốn, còn việc trừ phần chưa về là việc của advisor theo dòng hướng dẫn ở Step 5. Đổi `investableCapital` là đổi luôn position sizing của mọi kế hoạch, ngoài phạm vi PR này.

- [ ] **Step 5: Sửa dòng hướng dẫn cho advisor**

Tại dòng 2153, sửa thành:

```
   - Tiền khả dụng = <portfolio_cash> (tiền trong tài khoản chứng khoán) + <idle_cash> (tiền ngoài, từ hồ sơ tài chính). Đừng nói 'hết tiền' khi <portfolio_cash> còn số dư. Nếu có <portfolio_cash_pending>, phần đó là tiền bán CHƯA về ví (chu kỳ T+2) — trừ ra khi gợi ý khối lượng, vì chưa dùng mua được nếu không ứng trước tiền bán.
```

- [ ] **Step 6: Chạy test cho thấy xanh**

Run: `dotnet test tests/InvestmentApp.Infrastructure.Tests --filter AiAssistant --logger "console;verbosity=detailed"`
Expected: PASS — 4 test mới + toàn bộ test digest cũ. Nếu test wiring cũ vỡ vì constructor đổi, thêm mock `IMarketClosureRepository` trả mảng rỗng.

- [ ] **Step 7: Chạy cả bộ**

Run: `dotnet test`
Expected: PASS toàn bộ. Đếm số project trong output.

- [ ] **Step 8: Commit**

```bash
git add src/InvestmentApp.Infrastructure/Services/AiAssistantService.cs tests/InvestmentApp.Infrastructure.Tests/Services/
git commit -m "feat(ai): bản tin tách tiền bán chờ về và mốc lịch nghỉ đã nhập"
```

---

### Task 11: ADR + đồng bộ tài liệu

**Files:**
- Create: `docs/adr/0016-t2-settlement-pending-cash.md` (theo `docs/adr/template.md`)
- Modify: `docs/business-domain.md` (dòng 117 công thức cash; thêm entity + collection mới)
- Modify: `docs/architecture.md` (repository, endpoint, MCP tool mới)
- Modify: `docs/features.md`
- Modify: `frontend/src/assets/CHANGELOG.md`
- Create: `frontend/src/assets/docs/tien-ban-cho-ve.md` + đăng ký Help topic

- [ ] **Step 1: Đọc template ADR trước khi viết**

Run: `cat docs/adr/template.md` và `cat docs/adr/README.md`
Viết theo đúng các section của template. Số hiệu tiếp theo là **0016** (cao nhất hiện có là 0015).

Nội dung bắt buộc có: bối cảnh (tiền bán vào cash ngay tại ngày khớp, 4 bề mặt bị ảnh hưởng); quyết định (đại lượng riêng, hàm thuần, lịch nghỉ trong DB, nhập theo từng ngày, cảnh báo mềm không chặn); **hai hướng bị loại kèm lý do** (persist `SettlementDate` vào `Trade`; hardcode bảng lễ theo năm); hệ quả (ADR-0007 không bị ảnh hưởng vì risk/snapshot không cộng tiền bán; rủi ro còn lại là quên nhập lịch nghỉ).

- [ ] **Step 2: Sửa `docs/business-domain.md`**

Dòng 117 hiện là:

```
- **Cash còn lại** = `CurrentCapital − TotalInvested + TotalSold` — tiền mặt khả dụng để vào lệnh mới
```

Sửa tại chỗ (không thêm ghi chú ở nơi khác):

```
- **Cash còn lại** = `CurrentCapital − TotalInvested + TotalSold` — tổng tiền trong tài khoản
- **Tiền đã về** = `Cash còn lại − PendingSettlementCash` — phần dùng mua được ngay. `PendingSettlementCash` là tiền bán chưa về theo chu kỳ T+2, xem ADR-0016
```

Thêm entity `MarketClosure` + collection `market_closures` vào bảng entity.

- [ ] **Step 3: Sửa `docs/architecture.md`**

Thêm: `MarketClosureRepository` vào bảng repository; `MarketClosuresController` + `AiAgentMarketClosuresController` vào bảng controller; 3 tool MCP vào bảng tool; `SettlementCalculator` vào nhóm hàm thuần cạnh `PositionBuilder` / `PortfolioCashCalculator`.

- [ ] **Step 4: Viết user guide**

Tạo `frontend/src/assets/docs/tien-ban-cho-ve.md`. Giải thích bằng tiếng Việt có dấu: T+2 là gì, vì sao bán rồi mà chưa dùng tiền được, dòng "chờ về" trên hero card nghĩa là gì, cảnh báo ở cửa sổ ghi lệnh nghĩa là gì, và **mỗi khi HOSE công bố lịch nghỉ thì nhập qua trợ lý AI** (nêu tên tool `add_market_closures`).

Đăng ký topic vào danh sách Help. Tìm chỗ đăng ký: `grep -rn "assets/docs" frontend/src/app`

- [ ] **Step 5: Thêm mục CHANGELOG**

Thêm mục mới vào đầu `frontend/src/assets/CHANGELOG.md` theo đúng khuôn các mục đang có.

- [ ] **Step 6: Rà tài liệu không lệch code**

Run: `grep -rn "TotalSold" docs/ | grep -v "docs/plans\|docs/superpowers"`
Kiểm mọi chỗ mô tả công thức cash đã nhắc tới `PendingSettlementCash`.

- [ ] **Step 7: Commit**

```bash
git add docs/ frontend/src/assets/
git commit -m "docs: ADR-0016 tiền bán chờ về T+2, đồng bộ tài liệu và hướng dẫn người dùng"
```

---

## Checkpoint — Task 1-3 (xong 2026-08-12)

- **Đã làm:** Task 1 (entity `MarketClosure`), Task 2 (`IMarketClosureRepository` + Mongo impl + DI), Task 3 (add/remove command + get query).
- **Commit:** `ea1c440` → `5be95fb` → `7a900af` trên `feature/t2-settlement-pending-cash`.
- **Test:** 11 test mới (4 Domain + 7 Application). Cả bộ **2015 pass / 0 fail**, đủ 4 project.
- **Tầng bị ảnh hưởng:** Domain, Application, Infrastructure, Api (chỉ 1 dòng DI).
- **Lệch so với plan khi thi hành:**
  - Chặn cuối tuần đặt ở **handler** (đếm `SkippedWeekend`) chứ không để entity ném — dán cả năm vào mà có một ngày T7 thì cả lô vỡ. Entity vẫn ném, giữ làm lớp chặn cuối.
  - Thêm 3 ca test ngoài plan: ngày trùng trong cùng một lô chỉ ghi một lần; xoá ngày không tồn tại trả `false`; query truyền đúng biên 1/1–31/12.
  - Sửa plan Task 7: mock thật tên `_portfolioRepo`/`_tradeRepo`/`_flowRepo`, và `Portfolio(userId, name, capital)` — **userId trước**. Plan cũ đoán sai cả hai.
- **Next:** Task 4 (controller JWT + sibling ApiKey + 3 tool MCP + test ngang giá) rồi Task 5 (script seed 2026). Đọc trước: `src/InvestmentApp.Api/Mcp/PortfolioTools.cs`, `src/InvestmentApp.Api/Controllers/AiAgentPortfoliosController.cs`, và `tests/InvestmentApp.Api.Tests/Mcp/McpTestContext.cs` để lấy đúng tên helper dựng `IHttpContextAccessor`. Hết Task 5 là đủ Mốc 1 → chạy `/code-review` rồi mới push + PR.

## Verify trước khi mở PR

1. `dotnet test` — PASS toàn bộ, **đếm số project** trong output cho khớp số project test có thật.
2. `cd frontend && npm test -- --watch=false` — PASS.
3. `cd frontend && npm run build` — 0 error.
4. Chạy script seed lên DB dev, rồi `GET /api/v1/market-closures?year=2026` — phải trả 12 ngày nhóm theo 5 tháng.
5. `/qa-verify` — mở `/dashboard` và `/capital-flows`, chụp màn hình dòng "chờ về". Nếu dữ liệu thật không có lệnh bán nào trong 2 phiên gần nhất thì ghi một lệnh bán thử trên dev để dòng đó hiện ra, chụp, rồi xoá.
6. Quét secret trên diff — chặn commit nếu có key/token/URL prod.
7. `/code-review` — bắt buộc, không bỏ dù PR trông chỉ là wiring.
