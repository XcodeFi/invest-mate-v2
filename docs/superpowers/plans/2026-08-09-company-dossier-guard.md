# Hồ sơ công ty — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Không cho tạo trade plan mới cho một mã khi chưa có hồ sơ hiểu biết về doanh nghiệp đó, đã được người dùng ký và còn hiệu lực.

**Architecture:** Aggregate mới `CompanyDossier` khóa `(UserId, Symbol)`, sống độc lập với `TradePlan`. Gate nằm ở Application layer (`ICompanyDossierGate`) vì nó phải đọc một aggregate khác — bắn ở đầu `CreateTradePlanCommandHandler` và ở `UpdateTradePlanCommandHandler` khi size vượt ngưỡng 5%. Agent qua MCP ghi được hồ sơ nhưng không ký được: `ConfirmedAt` chỉ đặt bởi `Confirm()`, chỉ với tới qua endpoint JWT.

**Tech Stack:** .NET 9 · MongoDB Driver 3.6.0 · MediatR · xUnit + FluentAssertions + Moq · Angular 19 standalone + Tailwind + ngModel · ModelContextProtocol.Server

**Spec:** [`docs/superpowers/specs/2026-08-09-company-dossier-design.md`](../specs/2026-08-09-company-dossier-design.md)

## Global Constraints

- Nhánh làm việc: `feature/company-dossier-guard`. **Không** commit lên `master`.
- Collection MongoDB đặt tên **snake_case** (`company_dossiers`), field BSON **PascalCase** — project không đăng ký convention camelCase nào.
- Collection property trên entity khai `List<T> { get; private set; }`. **Không** dùng `IReadOnlyList` trên field `private readonly` — driver deserialize về rỗng.
- Múi giờ Việt Nam là **offset cố định +07:00, không có DST**. Dùng `TimeSpan.FromHours(7)`, không dùng `TimeZoneInfo.FindSystemTimeZoneById` (id khác nhau giữa Windows và Linux).
- Mọi text UI **tiếng Việt có dấu đầy đủ**. Input symbol dùng directive `appUppercase`.
- Component Angular: standalone, **inline template** trong `@Component({ template: \`...\` })`. Không tạo file `.html` riêng. Không dùng backtick chưa escape trong comment HTML bên trong template.
- Thứ tự nút trong modal: `[Hủy] → [primary]`, primary bên phải.
- TDD bắt buộc: Red → Green → Refactor. Chạy `dotnet test` sau mỗi task.
- Commit message tiếng Việt có dấu, prefix conventional-commit tiếng Anh. **Không** thêm trailer `Co-Authored-By`.
- `ExceptionMiddleware` hiện map `InvalidOperationException → 409`. Muốn 400 phải có nhánh riêng đặt **trước** switch chung.

---

## File Structure

**Domain**
- `src/InvestmentApp.Domain/Entities/CompanyDossier.cs` — aggregate + `MoatItem` + `RiskFactor` + `DossierFreshness`. Một file vì ba type sau chỉ tồn tại phục vụ aggregate này, theo đúng cách `InvalidationRule.cs` gom rule + enum.

**Application**
- `src/InvestmentApp.Application/Common/Interfaces/ICompanyDossierRepository.cs`
- `src/InvestmentApp.Application/CompanyDossiers/DTOs/CompanyDossierDto.cs` — DTO + `DossierGateStatusDto`
- `src/InvestmentApp.Application/CompanyDossiers/Gate/ICompanyDossierGate.cs` — interface + `DossierGateResult` + `DossierGateException`
- `src/InvestmentApp.Application/CompanyDossiers/Gate/CompanyDossierGate.cs` — implementation
- `src/InvestmentApp.Application/CompanyDossiers/Commands/UpsertCompanyDossier/UpsertCompanyDossierCommand.cs` — command + handler (một file, theo khuôn `CreateTradePlanCommand.cs`)
- `src/InvestmentApp.Application/CompanyDossiers/Commands/ConfirmCompanyDossier/ConfirmCompanyDossierCommand.cs`
- `src/InvestmentApp.Application/CompanyDossiers/Queries/GetCompanyDossier/GetCompanyDossierQuery.cs`
- `src/InvestmentApp.Application/CompanyDossiers/Queries/ListCompanyDossiers/ListCompanyDossiersQuery.cs`
- `src/InvestmentApp.Application/CompanyDossiers/Queries/GetDossierGateStatus/GetDossierGateStatusQuery.cs`
- `src/InvestmentApp.Application/Market/Queries/GetCompanyFundamentals/GetCompanyFundamentalsQuery.cs` — query + handler + DTO

**Infrastructure**
- `src/InvestmentApp.Infrastructure/Repositories/CompanyDossierRepository.cs`

**Api**
- `src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs`
- `src/InvestmentApp.Api/Mcp/CompanyDossierTools.cs`
- Modify: `src/InvestmentApp.Api/Middleware/ExceptionMiddleware.cs` (nhánh `DossierGateException`)
- Modify: `src/InvestmentApp.Api/Program.cs` (DI)
- Modify: `src/InvestmentApp.Api/Controllers/MarketDataController.cs` (endpoint fundamentals)

**Frontend**
- `frontend/src/app/core/services/company-dossier.service.ts`
- `frontend/src/app/features/company-dossier/company-dossier-list.component.ts`
- `frontend/src/app/features/company-dossier/company-dossier-detail.component.ts`
- `frontend/src/app/features/company-dossier/fundamentals-panel.component.ts`
- Modify: `frontend/src/app/app.routes.ts`, `frontend/src/app/features/trade-plan/trade-plan.component.ts`, `frontend/src/app/features/market-data/market-data.component.ts`

**Tests**
- `tests/InvestmentApp.Domain.Tests/Entities/CompanyDossierTests.cs`
- `tests/InvestmentApp.Domain.Tests/Entities/CompanyDossierFreshnessTests.cs`
- `tests/InvestmentApp.Application.Tests/CompanyDossiers/CompanyDossierGateTests.cs`
- `tests/InvestmentApp.Application.Tests/CompanyDossiers/TradePlanDossierGateWiringTests.cs`
- `tests/InvestmentApp.Application.Tests/Market/GetCompanyFundamentalsQueryHandlerTests.cs`
- `tests/InvestmentApp.Api.Tests/Mcp/CompanyDossierToolsDiscoveryTests.cs`

---

# CHẶNG 1 — Entity + gate + trang hồ sơ

Hết chặng này guard đã hoạt động, nhưng còn phải gõ tay.

## Task 1: Entity `CompanyDossier`

**Files:**
- Create: `src/InvestmentApp.Domain/Entities/CompanyDossier.cs`
- Test: `tests/InvestmentApp.Domain.Tests/Entities/CompanyDossierTests.cs`
- Test: `tests/InvestmentApp.Domain.Tests/Entities/CompanyDossierFreshnessTests.cs`

**Interfaces:**
- Consumes: `AggregateRoot` ([AggregateRoot.cs](../../../src/InvestmentApp.Domain/Entities/AggregateRoot.cs)), `InvalidationTrigger` ([InvalidationRule.cs:34](../../../src/InvestmentApp.Domain/Entities/InvalidationRule.cs#L34))
- Produces:
  - `CompanyDossier(string userId, string symbol, string businessModel, List<MoatItem> moats, List<RiskFactor> riskFactors, string? notes = null)`
  - `void UpdateByOwner(string businessModel, List<MoatItem> moats, List<RiskFactor> riskFactors, string? notes)`
  - `void UpdateByAgent(string businessModel, List<MoatItem> moats, List<RiskFactor> riskFactors, string? notes)`
  - `void Confirm()`
  - `DossierFreshness GetFreshness(DateTime utcNow)`
  - `enum DossierFreshness { Unconfirmed, Fresh, NeedsReview, Expired }`
  - `class MoatItem { string Description }`
  - `class RiskFactor { int Rank; string Description; string ObservableSignal; bool IsDealBreaker; InvalidationTrigger? SuggestedTrigger }`

- [ ] **Step 1: Viết test cho bất biến và ranking**

```csharp
using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class CompanyDossierTests
{
    private static RiskFactor Risk(int rank, string signal = "Biên gộp 2 quý liên tiếp giảm hơn 3 điểm",
        bool dealBreaker = false)
        => new() { Rank = rank, Description = $"Rủi ro {rank}", ObservableSignal = signal, IsDealBreaker = dealBreaker };

    private static CompanyDossier Create(
        string businessModel = "Bán thép xây dựng và HRC cho nhà thầu nội địa",
        List<RiskFactor>? risks = null)
        => new("user-1", " hpg ", businessModel,
            new List<MoatItem> { new() { Description = "Lò cao quy mô lớn nhất nội địa, chi phí đơn vị thấp" } },
            risks ?? new List<RiskFactor> { Risk(1) });

    [Fact]
    public void Ctor_ShouldNormalizeSymbol()
        => Create().Symbol.Should().Be("HPG");

    [Fact]
    public void Ctor_EmptySymbol_ShouldThrow()
    {
        var action = () => new CompanyDossier("user-1", "   ", "abc", new(), new());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_RiskFactorWithoutObservableSignal_ShouldThrow()
    {
        var action = () => Create(risks: new List<RiskFactor> { Risk(1, signal: "  ") });
        action.Should().Throw<ArgumentException>()
            .WithMessage("*dấu hiệu*");
    }

    [Fact]
    public void Ctor_TwoDealBreakers_ShouldThrow()
    {
        var action = () => Create(risks: new List<RiskFactor>
        {
            Risk(1, dealBreaker: true),
            Risk(2, dealBreaker: true)
        });
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*hủy diệt*");
    }

    [Fact]
    public void Ctor_SparseRanks_ShouldBeDensifiedByOrder()
    {
        var dossier = Create(risks: new List<RiskFactor> { Risk(9), Risk(3), Risk(7) });

        dossier.RiskFactors.Select(r => r.Rank).Should().Equal(1, 2, 3);
        dossier.RiskFactors.Select(r => r.Description).Should().Equal("Rủi ro 3", "Rủi ro 7", "Rủi ro 9");
    }

    [Fact]
    public void UpdateByAgent_ShouldClearConfirmation()
    {
        var dossier = Create();
        dossier.Confirm();

        dossier.UpdateByAgent("Mô hình mới do agent viết lại", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), null);

        dossier.ConfirmedAt.Should().BeNull();
        dossier.AgentDraftedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateByOwner_ShouldKeepConfirmation()
    {
        var dossier = Create();
        dossier.Confirm();
        var signedAt = dossier.ConfirmedAt;

        dossier.UpdateByOwner("Người dùng tự sửa lại mô hình kinh doanh", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), null);

        dossier.ConfirmedAt.Should().Be(signedAt);
        dossier.AgentDraftedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateByOwner_ShouldNotPushTheFreshnessClock()
    {
        // Chỉ Confirm() đẩy đồng hồ. Nếu sửa nội dung cũng đẩy thì hồ sơ đã
        // hết hạn chỉ cần sửa một ký tự ở ô ghi chú là hồi sinh mà không ai đọc tin mới.
        var dossier = Create();
        dossier.Confirm();
        var reviewedAt = dossier.ReviewedAt;

        dossier.UpdateByOwner("Người dùng sửa lại đúng một chỗ nhỏ", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), "thêm ghi chú");

        dossier.ReviewedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public void UpdateByOwner_OnExpiredDossier_ShouldStayExpiredUntilSigned()
    {
        var dossier = Create();
        dossier.Confirm();
        var now = dossier.ReviewedAt.AddDays(200);
        dossier.GetFreshness(now).Should().Be(DossierFreshness.Expired);

        dossier.UpdateByOwner("Sửa nội dung nhưng chưa ký lại", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), null);

        dossier.GetFreshness(now).Should().Be(DossierFreshness.Expired);
    }

    [Fact]
    public void UpdateByAgent_ShouldNotPushTheFreshnessClock()
    {
        var dossier = Create();
        dossier.Confirm();
        var reviewedAt = dossier.ReviewedAt;

        dossier.UpdateByAgent("Agent viết lại mô hình kinh doanh", dossier.Moats.ToList(),
            dossier.RiskFactors.ToList(), null);

        dossier.ReviewedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public void Confirm_ShouldSetBothTimestamps()
    {
        var dossier = Create();
        dossier.Confirm();

        dossier.ConfirmedAt.Should().NotBeNull();
        dossier.ReviewedAt.Should().BeCloseTo(dossier.ConfirmedAt!.Value, TimeSpan.FromSeconds(1));
    }
}
```

- [ ] **Step 2: Viết test cho hạn tươi**

```csharp
using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class CompanyDossierFreshnessTests
{
    private static CompanyDossier Confirmed()
    {
        var dossier = new CompanyDossier("user-1", "HPG", "Bán thép xây dựng cho nhà thầu",
            new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành" } },
            new List<RiskFactor>
            {
                new() { Rank = 1, Description = "Giá HRC Trung Quốc", ObservableSignal = "Giá HRC giảm quá 10% trong 1 tháng" }
            });
        dossier.Confirm();
        return dossier;
    }

    [Fact]
    public void Unconfirmed_ShouldBeUnconfirmedRegardlessOfReviewedAt()
    {
        var dossier = new CompanyDossier("user-1", "HPG", "abc", new(), new());
        dossier.GetFreshness(DateTime.UtcNow).Should().Be(DossierFreshness.Unconfirmed);
    }

    [Theory]
    [InlineData(0, DossierFreshness.Fresh)]
    [InlineData(89, DossierFreshness.Fresh)]
    [InlineData(90, DossierFreshness.NeedsReview)]
    [InlineData(179, DossierFreshness.NeedsReview)]
    [InlineData(180, DossierFreshness.Expired)]
    [InlineData(400, DossierFreshness.Expired)]
    public void GetFreshness_ShouldFollowDayBoundaries(int daysElapsed, DossierFreshness expected)
    {
        var dossier = Confirmed();
        var now = dossier.ReviewedAt.AddDays(daysElapsed);

        dossier.GetFreshness(now).Should().Be(expected);
    }

    [Fact]
    public void GetFreshness_ShouldUseVietnamCalendarDay()
    {
        // Ký lúc 18:00 UTC ngày 1 = 01:00 VN ngày 2. 89 ngày VN sau vẫn Fresh,
        // trong khi so sánh thuần UTC sẽ ra 89.x ngày và dễ lệch một ngày.
        var dossier = Confirmed();
        var reviewedVnDate = dossier.ReviewedAt.AddHours(7).Date;
        var nowUtc = reviewedVnDate.AddDays(89).AddHours(-7).AddHours(20);

        dossier.GetFreshness(nowUtc).Should().Be(DossierFreshness.Fresh);
    }

    [Fact]
    public void Confirm_OnExpiredDossier_ShouldReturnToFresh()
    {
        var dossier = Confirmed();
        var later = dossier.ReviewedAt.AddDays(200);
        dossier.GetFreshness(later).Should().Be(DossierFreshness.Expired);

        dossier.Confirm();

        dossier.GetFreshness(DateTime.UtcNow).Should().Be(DossierFreshness.Fresh);
    }
}
```

- [ ] **Step 3: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Domain.Tests --filter CompanyDossier`
Expected: FAIL — `CS0246: The type or namespace name 'CompanyDossier' could not be found`

- [ ] **Step 4: Implement entity**

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

/// <summary>Hồ sơ hiểu biết về một doanh nghiệp. Sống theo mã, không theo lệnh.</summary>
public class CompanyDossier : AggregateRoot
{
    /// <summary>Asia/Ho_Chi_Minh là offset cố định, không có DST.</summary>
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private const int NeedsReviewAfterDays = 90;
    private const int ExpiresAfterDays = 180;

    public string UserId { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;
    public string BusinessModel { get; private set; } = null!;
    public List<MoatItem> Moats { get; private set; } = new();
    public List<RiskFactor> RiskFactors { get; private set; } = new();
    public string? Notes { get; private set; }
    public DateTime ReviewedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? AgentDraftedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    [BsonConstructor]
    private CompanyDossier() { }

    public CompanyDossier(string userId, string symbol, string businessModel,
        List<MoatItem> moats, List<RiskFactor> riskFactors, string? notes = null)
    {
        Id = Guid.NewGuid().ToString();
        UserId = Require(userId, "Mã người dùng");
        Symbol = Require(symbol, "Mã cổ phiếu").ToUpperInvariant();
        BusinessModel = businessModel?.Trim() ?? string.Empty;
        Moats = moats ?? new();
        RiskFactors = Normalize(riskFactors ?? new());
        Notes = notes;
        CreatedAt = UpdatedAt = ReviewedAt = DateTime.UtcNow;
    }

    public void UpdateByOwner(string businessModel, List<MoatItem> moats,
        List<RiskFactor> riskFactors, string? notes)
    {
        Apply(businessModel, moats, riskFactors, notes);
    }

    public void UpdateByAgent(string businessModel, List<MoatItem> moats,
        List<RiskFactor> riskFactors, string? notes)
    {
        Apply(businessModel, moats, riskFactors, notes);
        AgentDraftedAt = DateTime.UtcNow;
        ConfirmedAt = null;   // người dùng chưa đọc bản mới
    }

    public void Confirm()
    {
        var now = DateTime.UtcNow;
        ReviewedAt = now;
        ConfirmedAt = now;
        UpdatedAt = now;
        IncrementVersion();
    }

    public DossierFreshness GetFreshness(DateTime utcNow)
    {
        if (ConfirmedAt is null) return DossierFreshness.Unconfirmed;

        var days = (utcNow.Add(VnOffset).Date - ReviewedAt.Add(VnOffset).Date).TotalDays;

        if (days >= ExpiresAfterDays) return DossierFreshness.Expired;
        if (days >= NeedsReviewAfterDays) return DossierFreshness.NeedsReview;
        return DossierFreshness.Fresh;
    }

    private void Apply(string businessModel, List<MoatItem> moats,
        List<RiskFactor> riskFactors, string? notes)
    {
        BusinessModel = businessModel?.Trim() ?? string.Empty;
        Moats = moats ?? new();
        RiskFactors = Normalize(riskFactors ?? new());
        Notes = notes;
        // KHÔNG chạm ReviewedAt — chỉ Confirm() đẩy đồng hồ hạn tươi. Nếu sửa
        // nội dung cũng đẩy, hồ sơ Expired chỉ cần sửa một ký tự là hồi sinh.
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private static string Require(string value, string label)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{label} không được rỗng")
            : value.Trim();

    private static List<RiskFactor> Normalize(List<RiskFactor> factors)
    {
        foreach (var f in factors)
        {
            if (string.IsNullOrWhiteSpace(f.ObservableSignal))
                throw new ArgumentException(
                    "Mỗi yếu tố rủi ro phải có dấu hiệu quan sát được", nameof(factors));
        }

        if (factors.Count(f => f.IsDealBreaker) > 1)
            throw new InvalidOperationException(
                "Chỉ được đánh dấu tối đa một yếu tố hủy diệt");

        var ordered = factors.OrderBy(f => f.Rank).ToList();
        for (int i = 0; i < ordered.Count; i++) ordered[i].Rank = i + 1;
        return ordered;
    }
}

public class MoatItem
{
    public string Description { get; set; } = string.Empty;
}

public class RiskFactor
{
    /// <summary>1 = nguy hiểm nhất. Entity tự chuẩn hóa về dense 1..N.</summary>
    public int Rank { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>"Biết nó đang xảy ra bằng gì" — bắt buộc, không có thì không phải rủi ro.</summary>
    public string ObservableSignal { get; set; } = string.Empty;

    /// <summary>Xảy ra thì bán hết, không phải chỉ cắt một lệnh. Tối đa 1 mỗi hồ sơ.</summary>
    public bool IsDealBreaker { get; set; }

    public InvalidationTrigger? SuggestedTrigger { get; set; }
}

public enum DossierFreshness
{
    Unconfirmed,
    Fresh,
    NeedsReview,
    Expired
}
```

- [ ] **Step 5: Chạy test, xác nhận pass**

Run: `dotnet test tests/InvestmentApp.Domain.Tests --filter CompanyDossier`
Expected: PASS — 15 test

- [ ] **Step 6: Commit**

```bash
git add src/InvestmentApp.Domain/Entities/CompanyDossier.cs tests/InvestmentApp.Domain.Tests/Entities/CompanyDossier*.cs
git commit -m "feat(dossier): entity CompanyDossier với bất biến rank và hạn tươi"
```

---

## Task 2: Repository + đăng ký DI

**Files:**
- Create: `src/InvestmentApp.Application/Common/Interfaces/ICompanyDossierRepository.cs`
- Create: `src/InvestmentApp.Infrastructure/Repositories/CompanyDossierRepository.cs`
- Modify: `src/InvestmentApp.Api/Program.cs:107-114` (khối `AddScoped<I…Repository, …>`)

**Interfaces:**
- Consumes: `CompanyDossier` (Task 1)
- Produces:
  - `Task<CompanyDossier?> GetAsync(string userId, string symbol)`
  - `Task<List<CompanyDossier>> GetByUserIdAsync(string userId)`
  - `Task CreateAsync(CompanyDossier dossier)`
  - `Task UpdateAsync(CompanyDossier dossier)`

Task này **không có unit test riêng** — nó chỉ là mapping sang MongoDB, không chứa nhánh logic nào. Nó được verify thật ở Task 6 Step 7 (curl vòng đời create → get → confirm trên DB dev). Đừng viết mock test cho nó; test mock một repository là test chính cái mock.

- [ ] **Step 1: Viết interface**

```csharp
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

public interface ICompanyDossierRepository
{
    Task<CompanyDossier?> GetAsync(string userId, string symbol);
    Task<List<CompanyDossier>> GetByUserIdAsync(string userId);
    Task CreateAsync(CompanyDossier dossier);
    Task UpdateAsync(CompanyDossier dossier);
}
```

- [ ] **Step 2: Viết implementation**

```csharp
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using MongoDB.Driver;

namespace InvestmentApp.Infrastructure.Repositories;

public class CompanyDossierRepository : ICompanyDossierRepository
{
    private readonly IMongoCollection<CompanyDossier> _collection;

    public CompanyDossierRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CompanyDossier>("company_dossiers");

        // Một hồ sơ cho mỗi mã, cho mỗi người dùng. Cả hai field luôn có giá trị
        // nên KHÔNG cần sparse (sparse + unique bỏ qua doc absent, không bỏ qua null).
        var keys = Builders<CompanyDossier>.IndexKeys
            .Ascending(d => d.UserId)
            .Ascending(d => d.Symbol);
        _collection.Indexes.CreateOneAsync(new CreateIndexModel<CompanyDossier>(
            keys,
            new CreateIndexOptions { Unique = true, Name = "ux_user_symbol" }));
    }

    public async Task<CompanyDossier?> GetAsync(string userId, string symbol)
    {
        var normalized = symbol.Trim().ToUpperInvariant();
        return await _collection
            .Find(d => d.UserId == userId && d.Symbol == normalized)
            .FirstOrDefaultAsync();
    }

    public async Task<List<CompanyDossier>> GetByUserIdAsync(string userId)
        => await _collection.Find(d => d.UserId == userId)
            .SortBy(d => d.Symbol)
            .ToListAsync();

    public async Task CreateAsync(CompanyDossier dossier)
        => await _collection.InsertOneAsync(dossier);

    public async Task UpdateAsync(CompanyDossier dossier)
        => await _collection.ReplaceOneAsync(d => d.Id == dossier.Id, dossier);
}
```

- [ ] **Step 3: Đăng ký DI**

Thêm vào `src/InvestmentApp.Api/Program.cs` ngay sau dòng `AddScoped<IRiskProfileRepository, RiskProfileRepository>();`:

```csharp
builder.Services.AddScoped<ICompanyDossierRepository, CompanyDossierRepository>();
```

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: Build succeeded, 0 error

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/Common/Interfaces/ICompanyDossierRepository.cs src/InvestmentApp.Infrastructure/Repositories/CompanyDossierRepository.cs src/InvestmentApp.Api/Program.cs
git commit -m "feat(dossier): repository company_dossiers với unique index (UserId, Symbol)"
```

---

## Task 3: Gate logic

**Files:**
- Create: `src/InvestmentApp.Application/CompanyDossiers/Gate/ICompanyDossierGate.cs`
- Create: `src/InvestmentApp.Application/CompanyDossiers/Gate/CompanyDossierGate.cs`
- Test: `tests/InvestmentApp.Application.Tests/CompanyDossiers/CompanyDossierGateTests.cs`
- Modify: `src/InvestmentApp.Api/Program.cs` (DI)

**Interfaces:**
- Consumes: `ICompanyDossierRepository` (Task 2), `CompanyDossier.GetFreshness` (Task 1)
- Produces:
  - `record DossierGateResult(bool Passed, string? Reason, List<string> Missing)`
  - `class DossierGateException : InvalidOperationException { string Symbol; DossierGateResult Result; }`
  - `Task<DossierGateResult> EvaluateAsync(string userId, string symbol, decimal planSize, decimal? accountBalance, CancellationToken ct)`
  - `Task EnsureAsync(...)` — cùng tham số, throw `DossierGateException` nếu không pass

Ngưỡng, sao chép đúng công thức `EnsureDisciplineGate` ([TradePlan.cs:171](../../../src/InvestmentApp.Domain/Entities/TradePlan.cs#L171)):

| | Nhỏ (`planSize < 5%` hoặc `accountBalance` null) | Lớn |
|---|---|---|
| `BusinessModel` | không rỗng | ≥ 30 ký tự |
| `Moats` | ≥ 1 | ≥ 1 và có ít nhất 1 cái `Description` ≥ 30 ký tự |
| `RiskFactors` | ≥ 1 | ≥ 3, mỗi `ObservableSignal` ≥ 20 ký tự |

- [ ] **Step 1: Viết test**

```csharp
using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

public class CompanyDossierGateTests
{
    private readonly Mock<ICompanyDossierRepository> _repo = new();

    private CompanyDossierGate Sut() => new(_repo.Object);

    private static CompanyDossier Dossier(
        string businessModel = "Bán thép xây dựng và HRC cho nhà thầu nội địa toàn quốc",
        int moatLength = 40,
        int riskCount = 3,
        int signalLength = 30,
        bool confirmed = true,
        int ageDays = 0)
    {
        var risks = Enumerable.Range(1, riskCount).Select(i => new RiskFactor
        {
            Rank = i,
            Description = $"Rủi ro số {i}",
            ObservableSignal = new string('x', signalLength)
        }).ToList();

        var d = new CompanyDossier("user-1", "HPG", businessModel,
            new List<MoatItem> { new() { Description = new string('m', moatLength) } },
            risks);

        if (confirmed) d.Confirm();
        if (ageDays > 0) typeof(CompanyDossier)
            .GetProperty(nameof(CompanyDossier.ReviewedAt))!
            .SetValue(d, DateTime.UtcNow.AddDays(-ageDays));
        return d;
    }

    private void Setup(CompanyDossier? dossier)
        => _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(dossier);

    // planSize 12_000_000 trên account 100_000_000 = 12% → tầng lớn
    private const decimal LargeSize = 12_000_000m;
    private const decimal SmallSize = 2_000_000m;
    private const decimal Account = 100_000_000m;

    [Fact]
    public async Task NoDossier_ShouldReturnMissing()
    {
        Setup(null);
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Be("missing");
    }

    [Fact]
    public async Task Unconfirmed_ShouldReturnUnconfirmed()
    {
        Setup(Dossier(confirmed: false));
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Reason.Should().Be("unconfirmed");
    }

    [Fact]
    public async Task Expired_ShouldReturnExpired()
    {
        Setup(Dossier(ageDays: 200));
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Reason.Should().Be("expired");
    }

    [Fact]
    public async Task AgentUpsertedAfterSigning_ShouldReturnUnconfirmed()
    {
        var d = Dossier();
        d.UpdateByAgent("Agent vừa viết lại mô hình kinh doanh của doanh nghiệp",
            d.Moats.ToList(), d.RiskFactors.ToList(), null);
        Setup(d);

        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Reason.Should().Be("unconfirmed");
    }

    [Fact]
    public async Task OwnerUpdatedAfterSigning_ShouldStillPass()
    {
        var d = Dossier();
        d.UpdateByOwner("Người dùng tự sửa lại mô hình kinh doanh cho rõ hơn",
            d.Moats.ToList(), d.RiskFactors.ToList(), null);
        Setup(d);

        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task SmallTier_MinimalContent_ShouldPass()
    {
        Setup(Dossier(businessModel: "Bán thép", moatLength: 5, riskCount: 1, signalLength: 10));
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task SmallTier_EmptyBusinessModel_ShouldBlock()
    {
        Setup(Dossier(businessModel: "   ", riskCount: 1));
        var result = await Sut().EvaluateAsync("user-1", "HPG", SmallSize, Account, default);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Be("insufficient");
        result.Missing.Should().Contain(m => m.Contains("businessModel"));
    }

    [Fact]
    public async Task LargeTier_TwoRiskFactors_ShouldBlockWithCounts()
    {
        Setup(Dossier(riskCount: 2));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Passed.Should().BeFalse();
        result.Missing.Should().Contain("riskFactors: cần ≥ 3, đang có 2");
    }

    [Fact]
    public async Task LargeTier_ShortObservableSignal_ShouldBlock()
    {
        Setup(Dossier(signalLength: 19));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Passed.Should().BeFalse();
        result.Missing.Should().Contain(m => m.Contains("observableSignal"));
    }

    [Fact]
    public async Task NullAccountBalance_ShouldUseSmallTier()
    {
        Setup(Dossier(businessModel: "Bán thép", moatLength: 5, riskCount: 1, signalLength: 10));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, null, default);

        result.Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1_000_000)]
    public async Task NonPositiveAccountBalance_ShouldUseSmallTier(decimal balance)
    {
        // Khớp guard `AccountBalance.Value > 0m` của TradePlan.EnsureDisciplineGate:179.
        // Thiếu guard này thì threshold = 0 và mọi lệnh rơi vào tầng lớn.
        Setup(Dossier(businessModel: "Bán thép", moatLength: 5, riskCount: 1, signalLength: 10));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, balance, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task LargeTier_VietnameseBusinessModelOf30Chars_ShouldPass()
    {
        // 30 ký tự có dấu — phải đếm bằng ký tự, không lệch
        const string vn = "Bán thép xây dựng cho nhà thầu";
        vn.Length.Should().Be(30);
        Setup(Dossier(businessModel: vn));

        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ExpiredAndThin_ShouldReportExpiredNotInsufficient()
    {
        // Ghim thứ tự ưu tiên. Code hiện đúng vì switch freshness return trước
        // mọi kiểm tra nội dung, nhưng không có test thì refactor đảo thứ tự sẽ lặng lẽ hồi quy.
        Setup(Dossier(ageDays: 200, riskCount: 1, businessModel: "Ngắn"));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Reason.Should().Be("expired");
    }

    [Fact]
    public async Task SizeExactlyAtFivePercent_ShouldUseLargeTier()
    {
        // Ghim `>=` chứ không phải `>`. Không có test ở đúng mốc thì off-by-one
        // ở biên ngưỡng lọt qua toàn bộ suite.
        Setup(Dossier(businessModel: "Bán thép", moatLength: 5, riskCount: 1, signalLength: 10));
        var result = await Sut().EvaluateAsync("user-1", "HPG", Account * 0.05m, Account, default);

        result.Passed.Should().BeFalse();
        result.Reason.Should().Be("insufficient");
    }

    [Fact]
    public async Task NeedsReview_ShouldStillPassTheGate()
    {
        // 90–179 ngày chỉ nhắc, không chặn. Chỉ Expired mới chặn.
        Setup(Dossier(ageDays: 120));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task LargeTier_ShortMoat_ShouldReportCurrentLongestLength()
    {
        Setup(Dossier(moatLength: 12));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Missing.Should().Contain(m => m.Contains("moats") && m.Contains("12"));
    }

    [Fact]
    public async Task LargeTier_ShortSignal_ShouldReportCurrentLength()
    {
        Setup(Dossier(signalLength: 19));
        var result = await Sut().EvaluateAsync("user-1", "HPG", LargeSize, Account, default);

        result.Missing.Should().Contain(m => m.Contains("observableSignal") && m.Contains("19 ký tự"));
    }

    [Fact]
    public async Task EnsureAsync_WhenBlocked_ShouldThrowWithPayload()
    {
        Setup(null);
        var act = () => Sut().EnsureAsync("user-1", "HPG", SmallSize, Account, default);

        var ex = (await act.Should().ThrowAsync<DossierGateException>()).Which;
        ex.Symbol.Should().Be("HPG");
        ex.Result.Reason.Should().Be("missing");
    }
}
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter CompanyDossierGateTests`
Expected: FAIL — `CS0246: 'CompanyDossierGate' could not be found`

- [ ] **Step 3: Implement interface + exception**

```csharp
namespace InvestmentApp.Application.CompanyDossiers.Gate;

/// <param name="Reason">"missing" | "unconfirmed" | "expired" | "insufficient" | null khi pass</param>
public record DossierGateResult(bool Passed, string? Reason, List<string> Missing)
{
    public static DossierGateResult Ok() => new(true, null, new());
    public static DossierGateResult Fail(string reason, params string[] missing)
        => new(false, reason, missing.ToList());
}

/// <summary>
/// Kế thừa InvalidOperationException có chủ đích: nếu nhánh riêng trong ExceptionMiddleware
/// bị xóa thì hành vi thoái về 409 Conflict, không thành 500.
/// </summary>
public class DossierGateException : InvalidOperationException
{
    public string Symbol { get; }
    public DossierGateResult Result { get; }

    public DossierGateException(string symbol, DossierGateResult result)
        : base($"Chưa đủ hồ sơ công ty cho mã {symbol}")
    {
        Symbol = symbol;
        Result = result;
    }
}

public interface ICompanyDossierGate
{
    Task<DossierGateResult> EvaluateAsync(string userId, string symbol,
        decimal planSize, decimal? accountBalance, CancellationToken ct);

    Task EnsureAsync(string userId, string symbol,
        decimal planSize, decimal? accountBalance, CancellationToken ct);
}
```

- [ ] **Step 4: Implement gate**

```csharp
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.CompanyDossiers.Gate;

public class CompanyDossierGate : ICompanyDossierGate
{
    private const decimal LargeTierThreshold = 0.05m;
    private const int LargeBusinessModelMinChars = 30;
    private const int LargeMoatMinChars = 30;
    private const int LargeRiskFactorMinCount = 3;
    private const int LargeSignalMinChars = 20;

    private readonly ICompanyDossierRepository _repo;

    public CompanyDossierGate(ICompanyDossierRepository repo) => _repo = repo;

    public async Task<DossierGateResult> EvaluateAsync(string userId, string symbol,
        decimal planSize, decimal? accountBalance, CancellationToken ct)
    {
        var dossier = await _repo.GetAsync(userId, symbol);
        if (dossier is null) return DossierGateResult.Fail("missing");

        switch (dossier.GetFreshness(DateTime.UtcNow))
        {
            case DossierFreshness.Unconfirmed: return DossierGateResult.Fail("unconfirmed");
            case DossierFreshness.Expired: return DossierGateResult.Fail("expired");
        }

        // Guard `> 0` là bắt buộc để khớp TradePlan.EnsureDisciplineGate:171.
        // Thiếu nó thì AccountBalance = 0 cho threshold = 0, mọi lệnh >= 0 nên
        // MỌI lệnh rơi vào tầng lớn — trong khi số dư 0 nghĩa là chưa biết gì, đúng như null.
        var requireFull = accountBalance.HasValue
            && accountBalance.Value > 0m
            && planSize >= accountBalance.Value * LargeTierThreshold;

        var missing = requireFull ? CheckLarge(dossier) : CheckSmall(dossier);

        return missing.Count == 0
            ? DossierGateResult.Ok()
            : new DossierGateResult(false, "insufficient", missing);
    }

    public async Task EnsureAsync(string userId, string symbol,
        decimal planSize, decimal? accountBalance, CancellationToken ct)
    {
        var result = await EvaluateAsync(userId, symbol, planSize, accountBalance, ct);
        if (!result.Passed)
            throw new DossierGateException(symbol.Trim().ToUpperInvariant(), result);
    }

    private static List<string> CheckSmall(CompanyDossier d)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(d.BusinessModel))
            missing.Add("businessModel: cần ít nhất một câu, đang để trống");

        if (d.Moats.Count == 0)
            missing.Add("moats: cần ≥ 1, đang có 0");

        if (d.RiskFactors.Count == 0)
            missing.Add("riskFactors: cần ≥ 1, đang có 0");

        return missing;
    }

    private static List<string> CheckLarge(CompanyDossier d)
    {
        var missing = new List<string>();

        if (d.BusinessModel.Length < LargeBusinessModelMinChars)
            missing.Add($"businessModel: cần ≥ {LargeBusinessModelMinChars} ký tự, đang có {d.BusinessModel.Length}");

        if (!d.Moats.Any(m => m.Description.Length >= LargeMoatMinChars))
        {
            var longest = d.Moats.Count == 0 ? 0 : d.Moats.Max(m => m.Description.Length);
            missing.Add($"moats: cần ít nhất 1 moat mô tả ≥ {LargeMoatMinChars} ký tự, dài nhất đang có {longest}");
        }

        if (d.RiskFactors.Count < LargeRiskFactorMinCount)
            missing.Add($"riskFactors: cần ≥ {LargeRiskFactorMinCount}, đang có {d.RiskFactors.Count}");

        // Nêu luôn độ dài hiện tại của từng cái thiếu — nói "chưa đủ" mà không nói
        // thiếu bao nhiêu thì người dùng phải đoán.
        var shortSignals = d.RiskFactors
            .Where(r => r.ObservableSignal.Length < LargeSignalMinChars)
            .Select(r => $"hạng {r.Rank} ({r.ObservableSignal.Length} ký tự)")
            .ToList();

        if (shortSignals.Count > 0)
            missing.Add($"observableSignal: cần ≥ {LargeSignalMinChars} ký tự ở yếu tố {string.Join(", ", shortSignals)}");

        return missing;
    }
}
```

- [ ] **Step 5: Đăng ký DI**

Thêm vào `Program.cs` cạnh khối repository:

```csharp
builder.Services.AddScoped<ICompanyDossierGate, CompanyDossierGate>();
```

- [ ] **Step 6: Chạy test, xác nhận pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter CompanyDossierGateTests`
Expected: PASS — 12 test

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Application/CompanyDossiers/Gate src/InvestmentApp.Api/Program.cs tests/InvestmentApp.Application.Tests/CompanyDossiers/CompanyDossierGateTests.cs
git commit -m "feat(dossier): gate đánh giá hồ sơ theo size, kèm lý do và danh sách thiếu"
```

---

## Task 4: Nối gate vào luồng tạo plan

**Files:**
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/CreateTradePlan/CreateTradePlanCommand.cs:69-90` (handler ctor + đầu `Handle`)
- Test: `tests/InvestmentApp.Application.Tests/CompanyDossiers/TradePlanDossierGateWiringTests.cs`

**Interfaces:**
- Consumes: `ICompanyDossierGate.EnsureAsync` (Task 3)
- Produces: không có API mới — thay đổi hành vi của `CreateTradePlanCommandHandler`

Điểm bắn phải ở **đầu** `Handle`, trước mọi thứ khác. Nhánh auto-transition `Draft → Ready → InProgress` khi `request.Status == "Executed"` nằm ở dòng 156–163 của cùng file, tức là *sau* điểm bắn, nên tự động được bao. Không đặt gate ở giữa hoặc cuối.

- [ ] **Step 1: Viết test**

```csharp
using FluentAssertions;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.TradePlans.Commands.CreateTradePlan;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

public class TradePlanDossierGateWiringTests
{
    private readonly Mock<ICompanyDossierGate> _gate = new();

    [Fact]
    public async Task Create_WhenGateBlocks_ShouldThrowBeforePersisting()
    {
        _gate.Setup(g => g.EnsureAsync("user-1", "HPG", It.IsAny<decimal>(),
                It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HPG",
                DossierGateResult.Fail("missing")));

        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out var repo);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
        repo.Verify(r => r.CreateAsync(It.IsAny<Domain.Entities.TradePlan>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithStatusExecuted_ShouldStillRunGateFirst()
    {
        _gate.Setup(g => g.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<decimal?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DossierGateException("HPG",
                DossierGateResult.Fail("missing")));

        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out _);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");
        command.Status = "Executed";
        command.TradeId = "trade-1";

        var act = () => handler.Handle(command, default);

        await act.Should().ThrowAsync<DossierGateException>();
    }

    [Fact]
    public async Task Create_WhenGatePasses_ShouldPassPlanSizeAndBalance()
    {
        var handler = TestFactory.CreateTradePlanHandler(_gate.Object, out _);
        var command = TestFactory.CreateCommand(userId: "user-1", symbol: "HPG");
        command.Quantity = 100;
        command.EntryPrice = 80_000m;
        command.AccountBalance = 100_000_000m;

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync("user-1", "HPG",
            8_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

`TestFactory` là helper cục bộ trong file test — dựng handler với tất cả dependency còn lại là `Mock.Of<T>()` và trả ra mock repository qua `out` để verify. Tra ctor thực tế của `CreateTradePlanCommandHandler` rồi viết helper khớp danh sách dependency đó; đừng đoán.

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter TradePlanDossierGateWiringTests`
Expected: FAIL — ctor `CreateTradePlanCommandHandler` chưa nhận `ICompanyDossierGate`

- [ ] **Step 3: Thêm dependency vào handler**

Trong `CreateTradePlanCommandHandler`: thêm `ICompanyDossierGate` vào ctor, gán vào field `_dossierGate`.

- [ ] **Step 4: Thêm điểm bắn ở đầu `Handle`**

Chèn ngay dòng đầu tiên của `Handle`, trước mọi validate/lookup khác:

```csharp
var planSize = request.Quantity * request.EntryPrice;
await _dossierGate.EnsureAsync(request.UserId, request.Symbol, planSize, request.AccountBalance, cancellationToken);
```

- [ ] **Step 5: Chạy test, xác nhận pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter TradePlanDossierGateWiringTests`
Expected: PASS — 3 test

- [ ] **Step 6: Chạy toàn bộ test Application để bắt regression**

Run: `dotnet test tests/InvestmentApp.Application.Tests`
Expected: PASS toàn bộ. Test `CreateTradePlanCommandHandler` cũ sẽ đỏ vì ctor đổi — sửa chúng bằng cách truyền `Mock.Of<ICompanyDossierGate>()` (mock mặc định không throw nên gate coi như pass).

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Application/TradePlans tests/InvestmentApp.Application.Tests
git commit -m "feat(dossier): chặn tạo trade plan khi chưa có hồ sơ công ty"
```

---

## Task 5: Nối gate vào luồng sửa plan khi size vượt ngưỡng

**Files:**
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/UpdateTradePlan/UpdateTradePlanCommand.cs` (handler)
- Test: `tests/InvestmentApp.Application.Tests/CompanyDossiers/TradePlanDossierGateWiringTests.cs` (thêm vào file Task 4)

**Interfaces:**
- Consumes: `ICompanyDossierGate.EnsureAsync` (Task 3)
- Produces: không có API mới

Chỉ chạy gate khi size **cũ dưới ngưỡng và size mới từ ngưỡng trở lên**. `accountBalance` null thì không có ngưỡng nào để vượt → không bao giờ chạy gate; đây là hệ quả có ý thức, không phải bỏ sót.

- [ ] **Step 1: Viết test**

```csharp
    [Fact]
    public async Task Update_CrossingThreshold_ShouldRunGate()
    {
        // cũ: 100 × 20.000 = 2tr trên account 100tr = 2% → dưới ngưỡng
        // mới: 100 × 120.000 = 12tr = 12% → trên ngưỡng
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 100, existingEntryPrice: 20_000m, accountBalance: 100_000_000m);

        var command = TestFactory.UpdateCommand(quantity: 100, entryPrice: 120_000m);

        await handler.Handle(command, default);

        _gate.Verify(g => g.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(),
            12_000_000m, 100_000_000m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_StayingBelowThreshold_ShouldNotRunGate()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 100, existingEntryPrice: 20_000m, accountBalance: 100_000_000m);

        var command = TestFactory.UpdateCommand(quantity: 100, entryPrice: 30_000m); // 3%

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_AlreadyAboveThreshold_ShouldNotRunGate()
    {
        // Plan đã lớn từ trước — Q6 "chỉ guard plan mới", không soi lại
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 100, existingEntryPrice: 120_000m, accountBalance: 100_000_000m);

        var command = TestFactory.UpdateCommand(quantity: 100, entryPrice: 130_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_NullAccountBalance_ShouldNotRunGate()
    {
        var handler = TestFactory.UpdateTradePlanHandler(_gate.Object,
            existingQuantity: 100, existingEntryPrice: 20_000m, accountBalance: null);

        var command = TestFactory.UpdateCommand(quantity: 1_000_000, entryPrice: 120_000m);

        await handler.Handle(command, default);

        _gate.VerifyNoOtherCalls();
    }
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter TradePlanDossierGateWiringTests`
Expected: FAIL — 4 test mới đỏ

- [ ] **Step 3: Implement trong `UpdateTradePlanCommandHandler`**

Sau khi load plan cũ, trước khi gọi `plan.Update(...)`:

```csharp
var threshold = (plan.AccountBalance ?? 0m) * 0.05m;
var oldSize = plan.Quantity * plan.EntryPrice;
var newSize = request.Quantity * request.EntryPrice;

// Vá cửa hậu "tạo nhỏ rồi sửa lớn". Chỉ bắn khi thực sự vượt ngưỡng lên,
// để không phá nguyên tắc "plan có rồi thì thôi".
if (plan.AccountBalance.HasValue && oldSize < threshold && newSize >= threshold)
{
    await _dossierGate.EnsureAsync(plan.UserId, plan.Symbol, newSize, plan.AccountBalance, cancellationToken);
}
```

- [ ] **Step 4: Chạy test, xác nhận pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests`
Expected: PASS toàn bộ

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/TradePlans tests/InvestmentApp.Application.Tests
git commit -m "feat(dossier): kiểm tra hồ sơ khi sửa plan làm size vượt ngưỡng 5%"
```

---

## Task 6: API hồ sơ công ty

**Files:**
- Create: `src/InvestmentApp.Application/CompanyDossiers/DTOs/CompanyDossierDto.cs`
- Create: `src/InvestmentApp.Application/CompanyDossiers/Commands/UpsertCompanyDossier/UpsertCompanyDossierCommand.cs`
- Create: `src/InvestmentApp.Application/CompanyDossiers/Commands/ConfirmCompanyDossier/ConfirmCompanyDossierCommand.cs`
- Create: `src/InvestmentApp.Application/CompanyDossiers/Queries/GetCompanyDossier/GetCompanyDossierQuery.cs`
- Create: `src/InvestmentApp.Application/CompanyDossiers/Queries/ListCompanyDossiers/ListCompanyDossiersQuery.cs`
- Create: `src/InvestmentApp.Application/CompanyDossiers/Queries/GetDossierGateStatus/GetDossierGateStatusQuery.cs`
- Create: `src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs`
- Modify: `src/InvestmentApp.Api/Middleware/ExceptionMiddleware.cs:31-56`

**Interfaces:**
- Consumes: `ICompanyDossierRepository` (Task 2), `ICompanyDossierGate` (Task 3)
- Produces:
  - `CompanyDossierDto { Symbol, BusinessModel, Moats[], RiskFactors[], Notes, ReviewedAt, ConfirmedAt, AgentDraftedAt, Freshness }` — `Freshness` là string (`"Unconfirmed"|"Fresh"|"NeedsReview"|"Expired"`)
  - `DossierGateStatusDto { Symbol, Passed, Reason, Missing[] }`
  - `UpsertCompanyDossierCommand { UserId, Symbol, BusinessModel, Moats, RiskFactors, Notes, ByAgent }`
  - Route: `GET|POST|PUT /api/v1/company-dossiers[/{symbol}]`, `POST /api/v1/company-dossiers/{symbol}/confirm`, `GET /api/v1/company-dossiers/{symbol}/gate-status`

`UpsertCompanyDossierCommand.ByAgent` quyết định gọi `UpdateByAgent` hay `UpdateByOwner`. Controller JWT **luôn** set `ByAgent = false`; MCP tool (Task 10) **luôn** set `true`. Cờ này chỉ tồn tại ở tầng command, entity vẫn có hai phương thức riêng.

- [ ] **Step 1: Viết test cho handler upsert + confirm**

```csharp
using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.Commands.ConfirmCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.CompanyDossiers;

public class CompanyDossierCommandTests
{
    private readonly Mock<ICompanyDossierRepository> _repo = new();

    private static UpsertCompanyDossierCommand Command(bool byAgent) => new()
    {
        UserId = "user-1",
        Symbol = "hpg",
        BusinessModel = "Bán thép xây dựng và HRC cho nhà thầu nội địa",
        Moats = new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành nội địa" } },
        RiskFactors = new List<RiskFactor>
        {
            new() { Rank = 1, Description = "Giá HRC Trung Quốc", ObservableSignal = "Giá HRC giảm quá 10% trong một tháng" }
        },
        ByAgent = byAgent
    };

    [Fact]
    public async Task Upsert_WhenNoExisting_ShouldCreateUnconfirmed()
    {
        _repo.Setup(r => r.GetAsync("user-1", "hpg")).ReturnsAsync((CompanyDossier?)null);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(Command(byAgent: false), default);

        _repo.Verify(r => r.CreateAsync(It.Is<CompanyDossier>(d =>
            d.Symbol == "HPG" && d.ConfirmedAt == null)), Times.Once);
    }

    [Fact]
    public async Task Upsert_ByAgent_ShouldClearConfirmation()
    {
        var existing = Existing(confirmed: true);
        _repo.Setup(r => r.GetAsync("user-1", "hpg")).ReturnsAsync(existing);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(Command(byAgent: true), default);

        existing.ConfirmedAt.Should().BeNull();
        existing.AgentDraftedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Upsert_ByOwner_ShouldKeepConfirmation()
    {
        var existing = Existing(confirmed: true);
        _repo.Setup(r => r.GetAsync("user-1", "hpg")).ReturnsAsync(existing);
        var handler = new UpsertCompanyDossierCommandHandler(_repo.Object);

        await handler.Handle(Command(byAgent: false), default);

        existing.ConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Confirm_WhenMissing_ShouldThrowKeyNotFound()
    {
        _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync((CompanyDossier?)null);
        var handler = new ConfirmCompanyDossierCommandHandler(_repo.Object);

        var act = () => handler.Handle(
            new ConfirmCompanyDossierCommand { UserId = "user-1", Symbol = "HPG" }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private static CompanyDossier Existing(bool confirmed)
    {
        var d = new CompanyDossier("user-1", "HPG", "Mô hình cũ",
            new List<MoatItem> { new() { Description = "Moat cũ" } },
            new List<RiskFactor> { new() { Rank = 1, Description = "Rủi ro cũ", ObservableSignal = "Dấu hiệu cũ đủ dài" } });
        if (confirmed) d.Confirm();
        return d;
    }
}
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter CompanyDossierCommandTests`
Expected: FAIL — handler chưa tồn tại

- [ ] **Step 3: Implement command + handler upsert**

```csharp
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;

public class UpsertCompanyDossierCommand : IRequest<string>
{
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string BusinessModel { get; set; } = string.Empty;
    public List<MoatItem> Moats { get; set; } = new();
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public string? Notes { get; set; }

    /// <summary>true khi lệnh đến từ MCP. Controller JWT luôn để false.</summary>
    public bool ByAgent { get; set; }
}

public class UpsertCompanyDossierCommandHandler : IRequestHandler<UpsertCompanyDossierCommand, string>
{
    private readonly ICompanyDossierRepository _repo;

    public UpsertCompanyDossierCommandHandler(ICompanyDossierRepository repo) => _repo = repo;

    public async Task<string> Handle(UpsertCompanyDossierCommand request, CancellationToken ct)
    {
        var existing = await _repo.GetAsync(request.UserId, request.Symbol);

        if (existing is null)
        {
            var created = new CompanyDossier(request.UserId, request.Symbol,
                request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);

            if (request.ByAgent)
                created.UpdateByAgent(request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);

            await _repo.CreateAsync(created);
            return created.Id;
        }

        if (request.ByAgent)
            existing.UpdateByAgent(request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);
        else
            existing.UpdateByOwner(request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);

        await _repo.UpdateAsync(existing);
        return existing.Id;
    }
}
```

- [ ] **Step 4: Implement confirm + 3 query**

`ConfirmCompanyDossierCommandHandler`: load theo `(UserId, Symbol)`, `throw new KeyNotFoundException($"Chưa có hồ sơ cho mã {symbol}")` nếu null, gọi `Confirm()`, `UpdateAsync`.

`GetCompanyDossierQuery` → `CompanyDossierDto?`. `ListCompanyDossiersQuery` → `List<CompanyDossierDto>`. `GetDossierGateStatusQuery { UserId, Symbol, Quantity?, EntryPrice?, AccountBalance? }` → `DossierGateStatusDto`, gọi `ICompanyDossierGate.EvaluateAsync` với `planSize = (Quantity ?? 0) * (EntryPrice ?? 0)`.

Map `Freshness` bằng `dossier.GetFreshness(DateTime.UtcNow).ToString()`.

- [ ] **Step 5: Implement controller**

```csharp
using InvestmentApp.Application.CompanyDossiers.Commands.ConfirmCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.DTOs;
using InvestmentApp.Application.CompanyDossiers.Queries.GetCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Queries.GetDossierGateStatus;
using InvestmentApp.Application.CompanyDossiers.Queries.ListCompanyDossiers;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/company-dossiers")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CompanyDossiersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyDossiersController(IMediator mediator) => _mediator = mediator;

    private string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet]
    [ProducesResponseType(typeof(List<CompanyDossierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new ListCompanyDossiersQuery { UserId = GetUserId() }, ct));

    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(CompanyDossierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string symbol, CancellationToken ct)
    {
        var dto = await _mediator.Send(
            new GetCompanyDossierQuery { UserId = GetUserId(), Symbol = symbol }, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{symbol}/gate-status")]
    [ProducesResponseType(typeof(DossierGateStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GateStatus(string symbol,
        [FromQuery] int? quantity, [FromQuery] decimal? entryPrice,
        [FromQuery] decimal? accountBalance, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDossierGateStatusQuery
        {
            UserId = GetUserId(), Symbol = symbol,
            Quantity = quantity, EntryPrice = entryPrice, AccountBalance = accountBalance
        }, ct));

    [HttpPut("{symbol}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(string symbol,
        [FromBody] UpsertCompanyDossierRequest? request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest(new { error = "Body request không hợp lệ." });

        var id = await _mediator.Send(new UpsertCompanyDossierCommand
        {
            UserId = GetUserId(),
            Symbol = symbol,
            BusinessModel = request.BusinessModel,
            Moats = request.Moats,
            RiskFactors = request.RiskFactors,
            Notes = request.Notes,
            ByAgent = false   // cửa JWT là người dùng, không bao giờ là agent
        }, ct);

        return Ok(new { id });
    }

    [HttpPost("{symbol}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(string symbol, CancellationToken ct)
    {
        await _mediator.Send(
            new ConfirmCompanyDossierCommand { UserId = GetUserId(), Symbol = symbol }, ct);
        return Ok();
    }
}

public class UpsertCompanyDossierRequest
{
    public string BusinessModel { get; set; } = string.Empty;
    public List<MoatItem> Moats { get; set; } = new();
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public string? Notes { get; set; }
}
```

- [ ] **Step 6: Thêm nhánh `DossierGateException` vào middleware**

Chèn vào `ExceptionMiddleware.HandleExceptionAsync` **ngay trước** nhánh `if (exception is ValidationException ve)`:

```csharp
// Đặt TRƯỚC switch chung: switch map InvalidOperationException → 409,
// còn gate cần 400 kèm body có cấu trúc để FE liệt kê được thiếu gì.
if (exception is DossierGateException dge)
{
    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    await context.Response.WriteAsJsonAsync(new
    {
        code = "DOSSIER_GATE_FAILED",
        symbol = dge.Symbol,
        reason = dge.Result.Reason,
        missing = dge.Result.Missing
    });
    return;
}
```

- [ ] **Step 7: Chạy test + verify thật bằng curl**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter CompanyDossier`
Expected: PASS

Rồi verify vòng đời thật (đây là chỗ Task 2 được kiểm chứng). Mint JWT bằng test `MintStableJwt`, đặt `ASPNETCORE_ENVIRONMENT=Development`, chạy API, và **kiểm tra `DatabaseName` trong config trước khi ghi** — `appsettings.Development.json` có thể trỏ vào DB prod.

Gửi body bằng file UTF-8 + `--data-binary`, không dùng `-d` inline (tiếng Việt có dấu bị mangle trên Windows). Keys phải **PascalCase**.

```bash
# 1. Chưa có hồ sơ → tạo plan phải bị chặn 400 DOSSIER_GATE_FAILED
curl -s -o - -w "\n%{http_code}\n" -X POST http://localhost:5000/api/v1/trade-plans \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  --data-binary @/tmp/plan.json

# 2. Tạo hồ sơ → GET thấy Freshness = "Unconfirmed"
curl -s -X PUT http://localhost:5000/api/v1/company-dossiers/HPG \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  --data-binary @/tmp/dossier.json
curl -s http://localhost:5000/api/v1/company-dossiers/HPG -H "Authorization: Bearer $JWT"

# 3. Ký → Freshness = "Fresh" → tạo plan thành công
curl -s -X POST http://localhost:5000/api/v1/company-dossiers/HPG/confirm -H "Authorization: Bearer $JWT"
curl -s -o - -w "\n%{http_code}\n" -X POST http://localhost:5000/api/v1/trade-plans \
  -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" \
  --data-binary @/tmp/plan.json
```

Expected: bước 1 → `400` + `{"code":"DOSSIER_GATE_FAILED","reason":"missing"}`; bước 2 → `"freshness":"Unconfirmed"`; bước 3 → `200`.

- [ ] **Step 8: Commit**

```bash
git add src/InvestmentApp.Application/CompanyDossiers src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs src/InvestmentApp.Api/Middleware/ExceptionMiddleware.cs tests/InvestmentApp.Application.Tests/CompanyDossiers
git commit -m "feat(dossier): API hồ sơ công ty và trả 400 có cấu trúc khi gate chặn"
```

---

## Task 7: Frontend — service + hai trang + banner

**Files:**
- Create: `frontend/src/app/core/services/company-dossier.service.ts`
- Create: `frontend/src/app/features/company-dossier/company-dossier-list.component.ts`
- Create: `frontend/src/app/features/company-dossier/company-dossier-detail.component.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/features/trade-plan/trade-plan.component.ts`
- Modify: `frontend/src/app/features/market-data/market-data.component.ts` (điều hướng khi chưa có hồ sơ — Step 7)
- Test: `frontend/src/app/features/company-dossier/company-dossier-detail.component.spec.ts`

**Interfaces:**
- Consumes: API Task 6
- Produces:
  - `CompanyDossierService.get(symbol) | list() | upsert(symbol, payload) | confirm(symbol) | gateStatus(symbol, quantity?, entryPrice?)`
  - Route `/company-dossier` và `/company-dossier/:symbol`
  - `interface CompanyDossierDto`, `RiskFactorDto`, `DossierGateStatusDto`

Yêu cầu bắt buộc của trang chi tiết:

1. Ô `businessModel` — label "Doanh nghiệp này kiếm tiền bằng gì?", helper text "Nêu sản phẩm/dịch vụ và ai trả tiền. 'Tiềm năng', 'đầu ngành' KHÔNG phải câu trả lời." Đếm ký tự sống kèm chỉ báo tầng: `"27/30 (Size 6,2% tài khoản — bắt buộc ≥ 30)"`.
2. Danh sách moat — thêm/xóa dòng.
3. Danh sách yếu tố rủi ro — mỗi dòng: mô tả · **dấu hiệu quan sát được (bắt buộc)** · dropdown `SuggestedTrigger` (5 giá trị Việt hóa) · checkbox "Yếu tố hủy diệt" (**disable khi đã có một cái khác được tick**) · nút `▲` `▼` đổi thứ tự.
4. Ô ghi chú tự do — nói rõ "không ảnh hưởng điều kiện chặn".
5. Nút ký ở **cuối trang, sau nội dung**, không cạnh nút Lưu. Nhãn: `Fresh`/`NeedsReview` → "Vẫn đúng"; `Expired` → "Đã cập nhật tin mới và xác nhận"; `Unconfirmed` → "Tôi đã đọc và chịu trách nhiệm".
6. Khi `agentDraftedAt` mới hơn `confirmedAt` (hoặc `confirmedAt` null mà `agentDraftedAt` có) → banner: `"Agent đã cập nhật lúc {agentDraftedAt | date:'short':'':'vi'} — chưa xác nhận"`.
7. Việt hóa `InvalidationTrigger`: `EarningsMiss` → "KQKD không đạt", `TrendBreak` → "Gãy trend", `NewsShock` → "Tin sốc", `ThesisTimeout` → "Quá hạn chờ", `Manual` → "Tự nhận định".

- [ ] **Step 1: Viết spec test**

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { CompanyDossierDetailComponent } from './company-dossier-detail.component';

describe('CompanyDossierDetailComponent', () => {
  let fixture: ComponentFixture<CompanyDossierDetailComponent>;
  let component: CompanyDossierDetailComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyDossierDetailComponent, HttpClientTestingModule],
    }).compileComponents();
    fixture = TestBed.createComponent(CompanyDossierDetailComponent);
    component = fixture.componentInstance;
  });

  it('chỉ cho tick một yếu tố hủy diệt', () => {
    component.riskFactors = [
      { rank: 1, description: 'A', observableSignal: 'x', isDealBreaker: true, suggestedTrigger: null },
      { rank: 2, description: 'B', observableSignal: 'y', isDealBreaker: false, suggestedTrigger: null },
    ];
    expect(component.dealBreakerDisabled(1)).toBe(true);
    expect(component.dealBreakerDisabled(0)).toBe(false);
  });

  it('nút ▲ đổi thứ tự và đánh lại rank dense', () => {
    component.riskFactors = [
      { rank: 1, description: 'A', observableSignal: 'x', isDealBreaker: false, suggestedTrigger: null },
      { rank: 2, description: 'B', observableSignal: 'y', isDealBreaker: false, suggestedTrigger: null },
    ];
    component.moveUp(1);
    expect(component.riskFactors.map(r => r.description)).toEqual(['B', 'A']);
    expect(component.riskFactors.map(r => r.rank)).toEqual([1, 2]);
  });

  it('nhãn nút ký đổi theo trạng thái tươi', () => {
    component.freshness = 'Unconfirmed';
    expect(component.signLabel()).toBe('Tôi đã đọc và chịu trách nhiệm');
    component.freshness = 'Expired';
    expect(component.signLabel()).toBe('Đã cập nhật tin mới và xác nhận');
    component.freshness = 'Fresh';
    expect(component.signLabel()).toBe('Vẫn đúng');
  });

  it('cảnh báo khi agent sửa mà chưa ký', () => {
    component.confirmedAt = null;
    component.agentDraftedAt = '2026-08-09T03:00:00Z';
    expect(component.showAgentDraftWarning()).toBe(true);
  });
});
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `cd frontend && npx ng test --watch=false --include='**/company-dossier-detail.component.spec.ts'`
Expected: FAIL — không tìm thấy component

- [ ] **Step 3: Implement service**

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export type DossierFreshness = 'Unconfirmed' | 'Fresh' | 'NeedsReview' | 'Expired';

export interface RiskFactorDto {
  rank: number;
  description: string;
  observableSignal: string;
  isDealBreaker: boolean;
  suggestedTrigger: string | null;
}

export interface CompanyDossierDto {
  symbol: string;
  businessModel: string;
  moats: { description: string }[];
  riskFactors: RiskFactorDto[];
  notes: string | null;
  reviewedAt: string;
  confirmedAt: string | null;
  agentDraftedAt: string | null;
  freshness: DossierFreshness;
}

export interface DossierGateStatusDto {
  symbol: string;
  passed: boolean;
  reason: string | null;
  missing: string[];
}

@Injectable({ providedIn: 'root' })
export class CompanyDossierService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/company-dossiers`;

  list(): Observable<CompanyDossierDto[]> {
    return this.http.get<CompanyDossierDto[]>(this.base);
  }

  get(symbol: string): Observable<CompanyDossierDto> {
    return this.http.get<CompanyDossierDto>(`${this.base}/${symbol}`);
  }

  upsert(symbol: string, payload: {
    BusinessModel: string;
    Moats: { Description: string }[];
    RiskFactors: {
      Rank: number; Description: string; ObservableSignal: string;
      IsDealBreaker: boolean; SuggestedTrigger: string | null;
    }[];
    Notes: string | null;
  }): Observable<{ id: string }> {
    // PascalCase bắt buộc — API case-sensitive, camelCase bind về null → 500
    return this.http.put<{ id: string }>(`${this.base}/${symbol}`, payload);
  }

  confirm(symbol: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${symbol}/confirm`, {});
  }

  gateStatus(symbol: string, quantity?: number, entryPrice?: number): Observable<DossierGateStatusDto> {
    const params: Record<string, string> = {};
    if (quantity != null) params['quantity'] = String(quantity);
    if (entryPrice != null) params['entryPrice'] = String(entryPrice);
    return this.http.get<DossierGateStatusDto>(`${this.base}/${symbol}/gate-status`, { params });
  }
}
```

- [ ] **Step 4: Implement hai component**

`company-dossier-detail.component.ts` — standalone, inline template, `FormsModule`, `UppercaseDirective`. Public API mà test dựa vào:

```typescript
riskFactors: RiskFactorDto[] = [];
freshness: DossierFreshness = 'Unconfirmed';
confirmedAt: string | null = null;
agentDraftedAt: string | null = null;

dealBreakerDisabled(index: number): boolean {
  const otherHasIt = this.riskFactors.some((r, i) => i !== index && r.isDealBreaker);
  return otherHasIt && !this.riskFactors[index].isDealBreaker;
}

moveUp(index: number): void {
  if (index === 0) return;
  const items = [...this.riskFactors];
  [items[index - 1], items[index]] = [items[index], items[index - 1]];
  this.riskFactors = items.map((r, i) => ({ ...r, rank: i + 1 }));
}

moveDown(index: number): void {
  if (index >= this.riskFactors.length - 1) return;
  this.moveUp(index + 1);
}

signLabel(): string {
  if (this.freshness === 'Expired') return 'Đã cập nhật tin mới và xác nhận';
  if (this.freshness === 'Unconfirmed') return 'Tôi đã đọc và chịu trách nhiệm';
  return 'Vẫn đúng';
}

showAgentDraftWarning(): boolean {
  if (!this.agentDraftedAt) return false;
  if (!this.confirmedAt) return true;
  return new Date(this.agentDraftedAt) > new Date(this.confirmedAt);
}
```

`company-dossier-list.component.ts` — bảng mã · trạng thái tươi (badge màu: `Fresh` xanh, `NeedsReview` vàng, `Expired` đỏ, `Unconfirmed` xám) · số yếu tố rủi ro · ngày soát gần nhất · link sang chi tiết.

Mọi binding ngày dùng `| date:'short':'':'vi'` — locale `vi` đã được register ở `main.ts`, không cần làm lại.

- [ ] **Step 5: Thêm route**

```typescript
{
  path: 'company-dossier',
  loadComponent: () => import('./features/company-dossier/company-dossier-list.component')
    .then(m => m.CompanyDossierListComponent),
},
{
  path: 'company-dossier/:symbol',
  loadComponent: () => import('./features/company-dossier/company-dossier-detail.component')
    .then(m => m.CompanyDossierDetailComponent),
},
```

- [ ] **Step 6: Banner trong form Trade Plan**

Trong `trade-plan.component.ts`, ở nhánh xử lý lỗi khi submit: nếu `err.error?.code === 'DOSSIER_GATE_FAILED'` thì set `dossierGateError = err.error` và render banner đỏ liệt kê từng dòng `missing`, kèm link `[routerLink]="['/company-dossier', dossierGateError.symbol]"` nhãn "→ Viết hồ sơ {{symbol}}".

- [ ] **Step 7: Điều hướng từ nút "Tạo Trade Plan từ gợi ý" (spec §8.2)**

Đây là luồng bị quyết định Q3 làm gãy: chặn ở lúc tạo plan nghĩa là mã chưa có hồ sơ thì không tạo được plan từ Smart Signals. Không để người dùng mất phần auto-fill.

Ở chỗ xử lý click nút đó trong `market-data.component.ts`: trước khi điều hướng sang form trade plan, gọi `gateStatus(symbol)`. Nếu `passed === false` thì stash phần auto-fill rồi chuyển sang trang hồ sơ:

```typescript
private readonly PENDING_PLAN_KEY = 'pendingTradePlanDraft';

onCreatePlanFromSignal(suggestion: { symbol: string; entryPrice: number; stopLoss: number; target: number }): void {
  this.dossierService.gateStatus(suggestion.symbol).subscribe(status => {
    if (status.passed) {
      this.router.navigate(['/trade-plan'], { queryParams: { ...suggestion } });
      return;
    }
    sessionStorage.setItem(this.PENDING_PLAN_KEY, JSON.stringify(suggestion));
    this.router.navigate(['/company-dossier', suggestion.symbol], {
      queryParams: { returnTo: 'trade-plan' },
    });
  });
}
```

Ở `company-dossier-detail.component.ts`, sau khi ký thành công và `returnTo === 'trade-plan'`: đọc `sessionStorage`, xóa key, điều hướng sang `/trade-plan` với đúng query params đã stash. Nếu key không có thì vẫn điều hướng, chỉ là không có auto-fill.

Thêm spec test:

```typescript
it('trả lại đúng entry/SL/TP đã stash sau khi ký', () => {
  const draft = { symbol: 'HPG', entryPrice: 28000, stopLoss: 26000, target: 33000 };
  sessionStorage.setItem('pendingTradePlanDraft', JSON.stringify(draft));

  const restored = component.consumePendingPlanDraft();

  expect(restored).toEqual(draft);
  expect(sessionStorage.getItem('pendingTradePlanDraft')).toBeNull();
});

it('không vỡ khi không có draft nào được stash', () => {
  sessionStorage.removeItem('pendingTradePlanDraft');
  expect(component.consumePendingPlanDraft()).toBeNull();
});
```

```typescript
consumePendingPlanDraft(): Record<string, unknown> | null {
  const raw = sessionStorage.getItem('pendingTradePlanDraft');
  if (!raw) return null;
  sessionStorage.removeItem('pendingTradePlanDraft');
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}
```

- [ ] **Step 8: Chạy test, xác nhận pass**

Run: `cd frontend && npx ng test --watch=false --include='**/company-dossier-detail.component.spec.ts'`
Expected: PASS — 6 spec

- [ ] **Step 9: Verify trực quan trên browser**

Chạy FE dev server, mở bằng chrome-devtools MCP với **`127.0.0.1`**, không phải `localhost` (MCP browser từ chối `localhost`). Chụp ảnh `/company-dossier/HPG`: kiểm tra tiếng Việt có dấu đầy đủ, nút ký nằm cuối trang, checkbox hủy diệt thứ hai bị disable, đếm ký tự sống hoạt động.

Rồi đi trọn luồng gãy: từ market-data bấm "Tạo Trade Plan từ gợi ý" cho một mã **chưa có hồ sơ** → phải sang trang hồ sơ → viết + ký → phải quay về form trade plan với entry/SL/TP còn nguyên. Đây là luồng dễ vỡ nhất của cả feature, không được bỏ qua bước verify này.

- [ ] **Step 10: Commit**

```bash
git add frontend/src/app/core/services/company-dossier.service.ts frontend/src/app/features/company-dossier frontend/src/app/app.routes.ts frontend/src/app/features/trade-plan/trade-plan.component.ts frontend/src/app/features/market-data
git commit -m "feat(dossier): trang hồ sơ công ty, banner chặn và luồng quay lại từ gợi ý"
```

---

## Task 8: Tài liệu + ADR cho chặng 1

**Files:**
- Create: `docs/adr/0010-company-dossier-gate-at-plan-creation.md`
- Modify: `docs/architecture.md`, `docs/business-domain.md`, `docs/features.md`, `docs/project-context.md`
- Modify: `frontend/src/assets/CHANGELOG.md`
- Create: `frontend/src/assets/docs/ho-so-cong-ty.md` + đăng ký Help topic

**Interfaces:** không có code mới.

- [ ] **Step 1: Viết ADR-0010**

Theo [template](../../adr/template.md). Hai quyết định phải ghi, vì cả hai đi ngược tiền lệ hoặc phản trực giác:

1. **Chặn ở lúc tạo plan, không ở `Draft → Ready`** — gate kỷ luật thesis hiện có chặn ở transition. Ghi rõ đánh đổi: nút "Tạo Trade Plan từ gợi ý" phải điều hướng, và người dùng chịu áp lực viết vội khi giá đang chạm điểm mua.
2. **Agent viết được, không ký được** — không có MCP tool nào đặt `ConfirmedAt`. Ghi rõ đây là cố ý, kèm test 35 canh.

- [ ] **Step 2: Cập nhật docs dev**

- `architecture.md` — `CompanyDossier`, `CompanyDossierRepository`, `CompanyDossierGate`, `CompanyDossiersController`, 2 trang FE, nhánh mới trong `ExceptionMiddleware`.
- `business-domain.md` — entity map + quan hệ `CompanyDossier ↔ TradePlan`, bảng ngưỡng theo size, quy tắc hạn tươi 90/180.
- `features.md` — section "Hồ sơ công ty & điều kiện chặn lập kế hoạch".
- `project-context.md` — quyết định UX chặn-ở-lúc-tạo và đánh đổi.

- [ ] **Step 3: Viết hướng dẫn người dùng**

`frontend/src/assets/docs/ho-so-cong-ty.md` — cách viết moat và rủi ro có dấu hiệu quan sát được, kèm ví dụ thị trường VN cho từng loại trigger. **Phải đăng ký Help topic**, không chỉ thêm file — tra chỗ registry topic hiện có và thêm entry.

- [ ] **Step 4: CHANGELOG**

Thêm entry ở **đầu** `frontend/src/assets/CHANGELOG.md` với version thực tế kế tiếp.

- [ ] **Step 5: Commit**

```bash
git add docs frontend/src/assets
git commit -m "docs: tài liệu và ADR-0010 cho hồ sơ công ty"
```

---

# CHẶNG 2 — Phơi dữ liệu doanh nghiệp + MCP

Hết chặng này agent điền hộ được. **Đây là chặng làm guard hết đau — không cắt chặng này.**

## Task 9: Query fundamentals + endpoint REST

**Files:**
- Create: `src/InvestmentApp.Application/Market/Queries/GetCompanyFundamentals/GetCompanyFundamentalsQuery.cs`
- Modify: `src/InvestmentApp.Api/Controllers/MarketDataController.cs`
- Test: `tests/InvestmentApp.Application.Tests/Market/GetCompanyFundamentalsQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `IComprehensiveStockDataProvider.GetComprehensiveDataAsync(symbol, ct)` — **`using InvestmentApp.Application.Interfaces`**, KHÔNG phải `Application.Common.Interfaces`. File nằm ở `Common/Interfaces/` nhưng namespace khai là `Application.Interfaces`.
- Produces:
  - `GetCompanyFundamentalsQuery { Symbol } : IRequest<CompanyFundamentalsDto>`
  - `CompanyFundamentalsDto { Symbol, Company, Indicators, IncomeStatements[], Peers[], DividendEvents[], BusinessPlan, AnalystReports[], ForeignTrading[], UnavailableSections[] }`
  - Route `GET /api/v1/market/stock/{symbol}/fundamentals`

`UnavailableSections` là điểm sống còn: `NoOpFundamentalDataProvider` tồn tại trong Infrastructure, nên provider chưa cấu hình sẽ trả null. Rỗng **không** được hiểu là bằng không — nếu không agent sẽ viết hồ sơ từ null và sinh ra hồ sơ qua được gate mà rỗng nội dung.

- [ ] **Step 1: Viết test**

```csharp
using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Market.Queries.GetCompanyFundamentals;
using Moq;

namespace InvestmentApp.Application.Tests.Market;

public class GetCompanyFundamentalsQueryHandlerTests
{
    private readonly Mock<IComprehensiveStockDataProvider> _provider = new();

    private GetCompanyFundamentalsQueryHandler Sut() => new(_provider.Object);

    [Fact]
    public async Task WhenProviderReturnsNull_ShouldThrowKeyNotFound()
    {
        _provider.Setup(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComprehensiveStockData?)null);

        var act = () => Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task WhenCompanyAndIndicatorsBothNull_ShouldThrowRatherThanReturnEmpty()
    {
        _provider.Setup(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData { Symbol = "HPG" });

        var act = () => Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*không lấy được dữ liệu doanh nghiệp*");
    }

    [Fact]
    public async Task WhenIncomeStatementsEmpty_ShouldFlagUnavailableSection()
    {
        _provider.Setup(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData
            {
                Symbol = "HPG",
                Indicators = new FinanceIndicators { PE = 12.3m, ROE = 18.2m }
            });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.UnavailableSections.Should().Contain("incomeStatements");
        dto.UnavailableSections.Should().Contain("peers");
        dto.Indicators!.PE.Should().Be(12.3m);
    }

    [Fact]
    public async Task ShouldNormalizeSymbolBeforeCallingProvider()
    {
        _provider.Setup(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComprehensiveStockData
            {
                Symbol = "HPG",
                Indicators = new FinanceIndicators { PE = 1m }
            });

        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = " hpg " }, default);

        _provider.Verify(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter GetCompanyFundamentals`
Expected: FAIL — query chưa tồn tại

- [ ] **Step 3: Implement query + handler**

DTO phơi trực tiếp các type của `ComprehensiveStockData` (`CompanyOverview`, `FinanceIndicators`, `IncomeStatementItem`, `PeerStock`, `DividendEvent`, `CompanyPlan`, `AnalystReport`, `ForeignTradingDay`) — chúng đã là POCO thuần trong Application layer, không cần map lại.

Handler: normalize symbol, gọi provider, `throw new KeyNotFoundException($"Không tìm thấy mã {symbol}")` nếu null, `throw new KeyNotFoundException($"Provider không lấy được dữ liệu doanh nghiệp cho {symbol}")` nếu cả `Company` và `Indicators` null, rồi build `UnavailableSections` bằng cách thêm tên section cho mỗi collection rỗng / object null.

- [ ] **Step 4: Thêm endpoint**

Thêm vào `MarketDataController` cạnh action `stock/{symbol}/detail`:

```csharp
[HttpGet("stock/{symbol}/fundamentals")]
[ProducesResponseType(typeof(CompanyFundamentalsDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetFundamentals(string symbol, CancellationToken ct)
    => Ok(await _mediator.Send(new GetCompanyFundamentalsQuery { Symbol = symbol }, ct));
```

Nếu controller đó chưa inject `IMediator` thì thêm vào ctor, giữ nguyên các dependency hiện có.

- [ ] **Step 5: Chạy test, xác nhận pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter GetCompanyFundamentals`
Expected: PASS — 4 test

- [ ] **Step 6: Verify bằng curl**

```bash
curl -s "http://localhost:5000/api/v1/market/stock/HPG/fundamentals" -H "Authorization: Bearer $JWT" | head -c 800
```

Expected: có `indicators.pe`, `indicators.roe`, `incomeStatements` không rỗng, `unavailableSections` là `[]` hoặc liệt kê đúng phần thiếu.

- [ ] **Step 7: Commit**

```bash
git add src/InvestmentApp.Application/Market src/InvestmentApp.Api/Controllers/MarketDataController.cs tests/InvestmentApp.Application.Tests/Market
git commit -m "feat(market): phơi dữ liệu doanh nghiệp 24hmoney qua endpoint fundamentals"
```

---

## Task 10: MCP tools

**Files:**
- Create: `src/InvestmentApp.Api/Mcp/CompanyDossierTools.cs`
- Test: `tests/InvestmentApp.Api.Tests/Mcp/CompanyDossierToolsDiscoveryTests.cs`

**Interfaces:**
- Consumes: mọi query/command Task 6 + `GetCompanyFundamentalsQuery` Task 9
- Produces: 5 MCP tool — `list_company_dossiers`, `get_company_dossier`, `get_company_fundamentals`, `get_dossier_gate_status`, `upsert_company_dossier`

Ba bẫy MCP của project này:

1. **Param phẳng ở tầng ngoài** — `symbol`, `businessModel`, `moats`, `riskFactors` là param riêng biệt, không bọc trong một object `command`. Mảng object thì bình thường; wrapper bọc tất cả mới là cái phải tránh.
2. **Param tùy chọn đặt sau `ct` và phải có `= null`** — nullable một mình không đủ, thiếu default là schema đánh dấu `required`.
3. **Mọi service inject phải được đăng ký DI** — nếu không cả object graph của nó rò vào `inputSchema`.

**Không có tool xác nhận hồ sơ.** Đây là điểm tựa của thiết kế, không phải sơ suất.

- [ ] **Step 1: Viết discovery test**

```csharp
using System.Text.Json;
using FluentAssertions;
using InvestmentApp.Api.Mcp;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Tests.Mcp;

public class CompanyDossierToolsDiscoveryTests
{
    private static IReadOnlyList<McpServerTool> Tools()
        => typeof(CompanyDossierTools)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Any())
            .Select(m => McpServerTool.Create(m))
            .ToList();

    [Fact]
    public void UpsertTool_RequiredArray_ShouldNotContainCommandWrapper()
    {
        var tool = Tools().Single(t => t.ProtocolTool.Name == "upsert_company_dossier");
        var schema = JsonSerializer.Serialize(tool.ProtocolTool.InputSchema);

        schema.Should().NotContain("\"command\"");
    }

    [Fact]
    public void UpsertTool_OptionalNotes_ShouldNotBeRequired()
    {
        var tool = Tools().Single(t => t.ProtocolTool.Name == "upsert_company_dossier");
        var required = tool.ProtocolTool.InputSchema
            .GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()).ToList();

        required.Should().Contain("symbol");
        required.Should().NotContain("notes");
    }

    [Fact]
    public void NoTool_ShouldBeAbleToConfirmADossier()
    {
        // Q8: agent viết được nhưng không ký được. Nếu ai thêm tool confirm cho tiện,
        // test này đỏ trước khi guard bị tháo âm thầm.
        var names = Tools().Select(t => t.ProtocolTool.Name).ToList();

        names.Should().NotContain(n => n.Contains("confirm"));
        names.Should().BeEquivalentTo(
            "list_company_dossiers", "get_company_dossier", "get_company_fundamentals",
            "get_dossier_gate_status", "upsert_company_dossier");
    }
}
```

Nếu API dựng `McpServerTool` khác cách trên, tra file test MCP hiện có trong `tests/` rồi khớp theo — đừng đoán API của SDK.

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter CompanyDossierToolsDiscovery`
Expected: FAIL — `CompanyDossierTools` chưa tồn tại

- [ ] **Step 3: Implement tools**

```csharp
using System.ComponentModel;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.DTOs;
using InvestmentApp.Application.CompanyDossiers.Queries.GetCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Queries.GetDossierGateStatus;
using InvestmentApp.Application.CompanyDossiers.Queries.ListCompanyDossiers;
using InvestmentApp.Application.Market.Queries.GetCompanyFundamentals;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class CompanyDossierTools
{
    [McpServerTool(Name = "list_company_dossiers", ReadOnly = true)]
    [Description("Danh sách hồ sơ công ty đã viết, kèm trạng thái tươi (Unconfirmed/Fresh/NeedsReview/Expired) và số yếu tố rủi ro.")]
    public static async Task<List<CompanyDossierDto>> List(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new ListCompanyDossiersQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_company_dossier", ReadOnly = true)]
    [Description("Hồ sơ công ty của một mã. Trả null nếu chưa viết.")]
    public static async Task<CompanyDossierDto?> Get(
        [Description("Mã chứng khoán, vd HPG.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetCompanyDossierQuery { UserId = http.GetUserId(), Symbol = symbol }, ct);

    [McpServerTool(Name = "get_company_fundamentals", ReadOnly = true)]
    [Description("Số liệu doanh nghiệp từ 24hmoney: P/E, P/B, ROE, ROA, EPS, doanh thu/lợi nhuận theo quý, cổ phiếu cùng ngành, cổ tức, kế hoạch kinh doanh, cơ cấu cổ đông, ban lãnh đạo, đơn vị kiểm toán. Dùng làm nguyên liệu TRƯỚC khi viết hồ sơ. Kiểm tra unavailableSections — phần rỗng nghĩa là không lấy được, KHÔNG phải bằng không.")]
    public static async Task<CompanyFundamentalsDto> Fundamentals(
        [Description("Mã chứng khoán, vd HPG.")] string symbol,
        IMediator mediator, CancellationToken ct)
        => await mediator.Send(new GetCompanyFundamentalsQuery { Symbol = symbol }, ct);

    [McpServerTool(Name = "get_dossier_gate_status", ReadOnly = true)]
    [Description("Cho biết một lệnh dự kiến có qua được điều kiện hồ sơ hay không, và thiếu gì. Gọi trước create_trade_plan để khỏi ăn lỗi 400.")]
    public static async Task<DossierGateStatusDto> GateStatus(
        [Description("Mã chứng khoán, vd HPG.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Số lượng dự kiến mua (bỏ trống = coi như lệnh nhỏ).")] int? quantity = null,
        [Description("Giá vào dự kiến (bỏ trống = coi như lệnh nhỏ).")] decimal? entryPrice = null)
        => await mediator.Send(new GetDossierGateStatusQuery
        {
            UserId = http.GetUserId(), Symbol = symbol,
            Quantity = quantity, EntryPrice = entryPrice
        }, ct);

    [McpServerTool(Name = "upsert_company_dossier")]
    [Description("Ghi hồ sơ công ty (tạo mới hoặc ghi đè). LƯU Ý: hồ sơ do agent ghi luôn ở trạng thái CHƯA XÁC NHẬN — người dùng phải tự đọc và ký trên web mới mở được điều kiện tạo plan. Không có tool nào xác nhận thay được.")]
    public static async Task<object> Upsert(
        [Description("Mã chứng khoán, vd HPG.")] string symbol,
        [Description("Doanh nghiệp kiếm tiền bằng gì — nêu sản phẩm/dịch vụ và ai trả tiền. Lệnh từ 5% tài khoản trở lên cần ≥ 30 ký tự.")] string businessModel,
        [Description("Danh sách lợi thế cạnh tranh bền (moat).")] List<MoatItem> moats,
        [Description("Danh sách yếu tố có thể tác động xấu, xếp theo Rank 1 = nguy hiểm nhất. Mỗi yếu tố BẮT BUỘC có observableSignal — dấu hiệu để biết nó đang xảy ra. Tối đa 1 yếu tố isDealBreaker.")] List<RiskFactor> riskFactors,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Ghi chú tự do, không ảnh hưởng điều kiện chặn.")] string? notes = null)
    {
        var id = await mediator.Send(new UpsertCompanyDossierCommand
        {
            UserId = http.GetUserId(),
            Symbol = symbol,
            BusinessModel = businessModel,
            Moats = moats,
            RiskFactors = riskFactors,
            Notes = notes,
            ByAgent = true   // cửa MCP luôn là agent — không bao giờ giữ ConfirmedAt
        }, ct);

        return new { id, confirmed = false, note = "Người dùng phải ký trên web mới mở được điều kiện tạo plan." };
    }
}
```

- [ ] **Step 4: Chạy test, xác nhận pass**

Run: `dotnet test tests/InvestmentApp.Api.Tests --filter CompanyDossierToolsDiscovery`
Expected: PASS — 3 test

- [ ] **Step 5: Verify bằng MCP thật**

Gọi lần lượt `get_company_fundamentals` → `upsert_company_dossier` → `get_dossier_gate_status` cho một mã. Expected: sau upsert, `gate_status` trả `reason = "unconfirmed"`. Đây là bằng chứng cửa hậu Q8 đã bịt trên đường thật, không chỉ trong unit test.

- [ ] **Step 6: Commit**

```bash
git add src/InvestmentApp.Api/Mcp/CompanyDossierTools.cs tests/InvestmentApp.Api.Tests/Mcp
git commit -m "feat(mcp): 5 tool hồ sơ công ty, agent ghi được nhưng không ký được"
```

---

## Task 11: Panel số liệu cạnh ô viết + tài liệu chặng 2

**Files:**
- Create: `frontend/src/app/features/company-dossier/fundamentals-panel.component.ts`
- Modify: `frontend/src/app/features/company-dossier/company-dossier-detail.component.ts`
- Modify: `frontend/src/app/core/services/market-data.service.ts`
- Modify: `docs/architecture.md`, `docs/features.md`, `docs/business-domain.md`, `frontend/src/assets/CHANGELOG.md`

**Interfaces:**
- Consumes: `GET /market/stock/{symbol}/fundamentals` (Task 9)
- Produces: `<app-fundamentals-panel [symbol]="symbol">` — standalone, input `symbol`, tự fetch

Panel nằm **cạnh** ô viết (grid 2 cột trên desktop, xếp dọc trên mobile), không chồng lên. Số liệu là nguyên liệu, **không** tính vào điều kiện chặn — panel phải nói rõ điều đó bằng một dòng chữ nhỏ.

Phải hiển thị `unavailableSections`: mỗi phần không lấy được ghi "không lấy được dữ liệu", **không** render số 0.

- [ ] **Step 1: Viết spec test**

```typescript
it('hiển thị "không lấy được" cho phần thiếu, không hiển thị 0', () => {
  component.data = {
    symbol: 'HPG',
    indicators: { pe: 12.3, roe: 18.2 },
    incomeStatements: [],
    unavailableSections: ['incomeStatements'],
  } as any;

  expect(component.isUnavailable('incomeStatements')).toBe(true);
  expect(component.isUnavailable('indicators')).toBe(false);
});
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `cd frontend && npx ng test --watch=false --include='**/fundamentals-panel.component.spec.ts'`
Expected: FAIL

- [ ] **Step 3: Implement panel**

```typescript
isUnavailable(section: string): boolean {
  return (this.data?.unavailableSections ?? []).includes(section);
}
```

Render: thẻ chỉ số (P/E, P/B, ROE, ROA, EPS, vốn hóa, Beta, 52W min/max, đơn vị kiểm toán + cờ Big4) · bảng doanh thu/lợi nhuận theo quý · bảng cổ phiếu cùng ngành · cổ tức · kế hoạch kinh doanh · cơ cấu cổ đông lớn · ban lãnh đạo. Mỗi khối nằm trong `@if (!isUnavailable('...')) { … } @else { <span>không lấy được dữ liệu</span> }`.

- [ ] **Step 4: Nhúng vào trang chi tiết**

Bọc nội dung trang trong `grid lg:grid-cols-2 gap-6`, form bên trái, panel bên phải.

- [ ] **Step 5: Chạy test + verify browser**

Run: `cd frontend && npx ng test --watch=false --include='**/fundamentals-panel.component.spec.ts'`
Expected: PASS

Rồi mở `127.0.0.1` bằng chrome-devtools MCP, chụp `/company-dossier/HPG`, xác nhận panel nằm cạnh form và số liệu thật hiện ra.

- [ ] **Step 6: Cập nhật docs chặng 2 + commit**

```bash
git add frontend docs
git commit -m "feat(dossier): panel số liệu doanh nghiệp cạnh ô viết hồ sơ"
```

---

# CHẶNG 3 — Đề xuất invalidation rule + nhắc soát lại

Phần trả lại thời gian và phần nhắc. **Nếu phải cắt thì cắt chặng này.**

## Task 12: Đề xuất `InvalidationRule` từ Top-3 rủi ro

**Files:**
- Create: `src/InvestmentApp.Application/CompanyDossiers/Queries/GetSuggestedInvalidationRules/GetSuggestedInvalidationRulesQuery.cs`
- Modify: `src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs`
- Modify: `frontend/src/app/features/trade-plan/trade-plan.component.ts`
- Test: `tests/InvestmentApp.Application.Tests/CompanyDossiers/GetSuggestedInvalidationRulesQueryTests.cs`

**Interfaces:**
- Consumes: `ICompanyDossierRepository` (Task 2)
- Produces:
  - `GetSuggestedInvalidationRulesQuery { UserId, Symbol } : IRequest<List<SuggestedInvalidationRuleDto>>`
  - `SuggestedInvalidationRuleDto { Trigger, Detail, MeetsMinLength, SourceRank }`
  - Route `GET /api/v1/company-dossiers/{symbol}/suggested-rules`

Công thức:

```
Trigger = riskFactor.SuggestedTrigger ?? InvalidationTrigger.Manual
Detail  = $"{riskFactor.Description} — dấu hiệu: {riskFactor.ObservableSignal}"
```

`MeetsMinLength` = `Detail.Length >= 20` — ngưỡng của gate kỷ luật thesis hiện có. Ở tầng nhỏ `ObservableSignal` có thể ngắn nên `Detail` có thể không đạt; khi đó FE vẫn hiển thị nguyên văn cho người dùng bổ sung, **không** lặng lẽ tạo một rule sẽ bị từ chối.

- [ ] **Step 1: Viết test**

```csharp
[Fact]
public async Task ShouldReturnTopThreeByRankWithComposedDetail()
{
    var risks = Enumerable.Range(1, 5).Select(i => new RiskFactor
    {
        Rank = i,
        Description = $"Rủi ro {i}",
        ObservableSignal = $"Dấu hiệu quan sát được số {i}",
        SuggestedTrigger = i == 1 ? InvalidationTrigger.EarningsMiss : null
    }).ToList();

    _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(Dossier(risks));

    var result = await Sut().Handle(
        new GetSuggestedInvalidationRulesQuery { UserId = "user-1", Symbol = "HPG" }, default);

    result.Should().HaveCount(3);
    result[0].Trigger.Should().Be(InvalidationTrigger.EarningsMiss);
    result[1].Trigger.Should().Be(InvalidationTrigger.Manual);
    result[0].Detail.Should().Be("Rủi ro 1 — dấu hiệu: Dấu hiệu quan sát được số 1");
    result.Select(r => r.SourceRank).Should().Equal(1, 2, 3);
}

[Fact]
public async Task ShortDetail_ShouldBeFlaggedNotDropped()
{
    var risks = new List<RiskFactor>
    {
        new() { Rank = 1, Description = "A", ObservableSignal = "B" }
    };
    _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(Dossier(risks));

    var result = await Sut().Handle(
        new GetSuggestedInvalidationRulesQuery { UserId = "user-1", Symbol = "HPG" }, default);

    result.Should().HaveCount(1);
    result[0].MeetsMinLength.Should().BeFalse();
}

[Fact]
public async Task NoDossier_ShouldReturnEmptyList()
{
    _repo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync((CompanyDossier?)null);

    var result = await Sut().Handle(
        new GetSuggestedInvalidationRulesQuery { UserId = "user-1", Symbol = "HPG" }, default);

    result.Should().BeEmpty();
}
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter GetSuggestedInvalidationRules`
Expected: FAIL

- [ ] **Step 3: Implement query + endpoint + FE**

Query: load hồ sơ, `return new()` nếu null, `OrderBy(Rank).Take(3)`, map theo công thức trên.

Endpoint: `[HttpGet("{symbol}/suggested-rules")]` trên `CompanyDossiersController`.

FE trong `trade-plan.component.ts`: khi symbol thay đổi và không rỗng, gọi `suggestedRules(symbol)`. Render trong section "Điều kiện thesis sai" một khối "Từ hồ sơ công ty" với checkbox mỗi đề xuất — **mặc định không tick**. Tick thì push vào `invalidationCriteria`. Đề xuất có `meetsMinLength = false` hiển thị badge vàng "cần bổ sung cho đủ 20 ký tự".

- [ ] **Step 4: Chạy test, xác nhận pass**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter GetSuggestedInvalidationRules`
Expected: PASS — 3 test

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/CompanyDossiers/Queries/GetSuggestedInvalidationRules src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs frontend/src/app/features/trade-plan tests/InvestmentApp.Application.Tests/CompanyDossiers
git commit -m "feat(dossier): đề xuất invalidation rule từ 3 rủi ro cao nhất của hồ sơ"
```

---

## Task 13: Mục "Hồ sơ cần soát lại" + badge dashboard

**Files:**
- Create: `src/InvestmentApp.Application/CompanyDossiers/Queries/GetDossiersNeedingReview/GetDossiersNeedingReviewQuery.cs`
- Modify: `src/InvestmentApp.Api/Controllers/CompanyDossiersController.cs`
- Modify: trang `/pending-reviews` và widget dashboard tương ứng
- Test: `tests/InvestmentApp.Application.Tests/CompanyDossiers/GetDossiersNeedingReviewQueryTests.cs`

**Interfaces:**
- Consumes: `ICompanyDossierRepository`, `CompanyDossier.GetFreshness` (Task 1)
- Produces:
  - `GetDossiersNeedingReviewQuery { UserId } : IRequest<List<DossierReviewItemDto>>`
  - `DossierReviewItemDto { Symbol, Freshness, ReviewedAt, DaysOverdue }`
  - Route `GET /api/v1/company-dossiers/needing-review`

`Expired` xếp **trước** `NeedsReview`, trong mỗi nhóm sort `DaysOverdue` giảm dần. `DaysOverdue` đếm từ mốc 90 ngày, tính theo ngày lịch VN.

- [ ] **Step 1: Viết test**

```csharp
[Fact]
public async Task ShouldReturnExpiredFirstThenNeedsReview_SortedByOverdueDesc()
{
    _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>
    {
        Aged("AAA", days: 95),   // NeedsReview, overdue 5
        Aged("BBB", days: 200),  // Expired, overdue 110
        Aged("CCC", days: 150),  // NeedsReview, overdue 60
        Aged("DDD", days: 300),  // Expired, overdue 210
        Aged("EEE", days: 10),   // Fresh — không xuất hiện
    });

    var result = await Sut().Handle(new GetDossiersNeedingReviewQuery { UserId = "user-1" }, default);

    result.Select(r => r.Symbol).Should().Equal("DDD", "BBB", "CCC", "AAA");
}

[Fact]
public async Task UnconfirmedDossier_ShouldAppearInList()
{
    _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<CompanyDossier>
    {
        Unconfirmed("FFF")
    });

    var result = await Sut().Handle(new GetDossiersNeedingReviewQuery { UserId = "user-1" }, default);

    result.Should().ContainSingle().Which.Freshness.Should().Be("Unconfirmed");
}
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter GetDossiersNeedingReview`
Expected: FAIL

- [ ] **Step 3: Implement query + endpoint + FE**

Query: `GetByUserIdAsync`, lọc bỏ `Fresh`, tính `DaysOverdue`, sort `Expired` → `Unconfirmed` → `NeedsReview` rồi `DaysOverdue` desc.

FE: thêm section "Hồ sơ cần soát lại" vào `/pending-reviews`, card màu theo trạng thái (`Expired` đỏ, `Unconfirmed` xám, `NeedsReview` vàng), link sang chi tiết. Dashboard: badge count, **ẩn hoàn toàn khi bằng 0**.

- [ ] **Step 4: Chạy test, xác nhận pass**

Run: `dotnet test`
Expected: PASS toàn bộ solution

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/CompanyDossiers src/InvestmentApp.Api frontend tests
git commit -m "feat(dossier): nhắc soát lại hồ sơ ở pending-reviews và badge dashboard"
```

---

## Task 14: Tài liệu chặng 3 + đóng gói

**Files:**
- Modify: `docs/architecture.md`, `docs/features.md`, `docs/business-domain.md`, `docs/project-context.md`
- Modify: `frontend/src/assets/CHANGELOG.md`, `frontend/src/assets/docs/ho-so-cong-ty.md`
- Create: `docs/handoffs/HANDOFF-2026-XX-XX-company-dossier.md`

- [ ] **Step 1: Đồng bộ tài liệu**

Bổ sung phần đề xuất rule, mục soát lại, badge. Cập nhật hướng dẫn người dùng: mô tả luồng "agent viết → mình đọc và ký", vì đó là phần dễ gây bất ngờ nhất khi dùng thật.

- [ ] **Step 2: Chạy toàn bộ test lần cuối**

Run: `dotnet test` và `cd frontend && npx ng test --watch=false`
Expected: PASS cả hai. Dán output làm bằng chứng, không tự nhận là pass.

- [ ] **Step 3: Viết handoff**

`docs/handoffs/HANDOFF-<ngày>-company-dossier.md` — trạng thái từng chặng, quyết định đã chốt Q1–Q12, việc còn lại (neo hạn tươi theo BCTC, snapshot hồ sơ vào plan).

- [ ] **Step 4: Commit + mở PR**

```bash
git add docs frontend/src/assets
git commit -m "docs: hoàn tất tài liệu hồ sơ công ty và handoff"
```

Mở PR bằng skill `/pr` — **không** tự gõ `gh pr create`, vì làm tay sẽ bỏ qua cổng code-review và cổng quét bí mật.

---

## Ghi chú thi hành

- Sau mỗi task chạy đúng lệnh test ghi trong task đó. Task nào có bước verify thật (curl, MCP, browser) thì **phải dán output** — đánh dấu `[x]` mà không chạy là hoàn thành trên giấy.
- Chặng 1 phải xong trọn vẹn mới sang chặng 2. Chặng 2 mới là chặng làm guard hết đau — nếu chỉ có chặng 1, guard là một cái cửa chặn không có chìa.
- Ba việc còn để lại ngoài phạm vi, ghi rõ để không ai tưởng bị bỏ sót: neo hạn tươi theo kỳ BCTC mới, snapshot hồ sơ đóng băng vào plan lúc arm, và đưa các mục due-diligence (ban lãnh đạo, cơ cấu sở hữu, pha loãng, tập trung khách hàng, đòn bẩy, dòng tiền) thành điều kiện gate.
