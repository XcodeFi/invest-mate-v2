# Tỷ trọng ngành — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Làm sống hạn mức tập trung ngành 40% (đang là luật chết), rồi hiện tỷ trọng ngành hiện tại và sau lệnh vào khối cảnh báo kiểm-trước trên form lập kế hoạch.

**Architecture:** Không thêm entity, không thêm service. Đổi nguồn tra ngành trong `RiskCalculationService` sang provider đang sống, thêm một query + một endpoint nhẹ trong `RiskController`, thêm một dòng vào khối kiểm-trước đã có ở `trade-plan.component.ts`.

**Tech Stack:** .NET 9 (xUnit + FluentAssertions + Moq), Angular 19 (Karma + Jasmine), MongoDB Driver 3.6.0.

**Spec:** [`2026-08-10-sector-concentration-design.md`](../../specs/2026-08-10-sector-concentration-design.md) — Q1–Q6 là nguồn quyết định, task nào lệch spec thì spec thắng.

## Global Constraints

- Mọi text hiển thị: **tiếng Việt có dấu đầy đủ**. Commit message tiếng Việt có dấu, prefix conventional-commit tiếng Anh.
- TDD bắt buộc: test đỏ trước, rồi implement. Mọi test mới phải qua mutation check.
- **Không** đưa tỷ trọng ngành vào bất kỳ đường chặn nào (spec Q1). Không disable nút nào trên form.
- Phần trăm không tính được thì trả `null`, không trả `0` (spec Q4). Assert `null` tuyệt đối trong test.
- Mẫu số phép chiếu **không** cộng `addValue` (spec Q3).
- Không bật lại `TcbsFundamentalDataProvider`, không sửa mặc định 40%.
- **Không** thêm field ngành vào `CompanyDossier` (spec Q6) — để cửa sổ dùng thử trả lời provider phủ thiếu tới đâu.

---

### Task 1: Đổi nguồn tra ngành, làm sống luật 40%

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs:376-388` và `:434-446`
- Test: `tests/InvestmentApp.Infrastructure.Tests/Services/RiskCalculationServiceOptimizationTests.cs` — **file này đã có và đã test `GetPortfolioOptimization`**, nên harness dựng 13 dependency của constructor có sẵn ở đó. Thêm test vào file này, đừng tạo file mới và đừng dựng harness mới.

**Interfaces:**
- Consumes: `IComprehensiveStockDataProvider.GetComprehensiveDataAsync(string symbol, CancellationToken ct = default)` → `ComprehensiveStockData?`, ngành ở `.Company.Industry`. **Đã được inject sẵn** vào constructor (`comprehensiveProvider`, [dòng 42](../../../../src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs#L42)) — không sửa DI, không sửa `Program.cs`.
- Produces: `SectorExposure.IsOverweight` lần đầu có thể bằng `true`.

- [ ] **Step 1: Test đỏ — 3 mã cùng ngành chiếm 60% phải cho `IsOverweight = true`**

Đây là test chứng minh luật từng chết. Chạy trên code chưa sửa **phải đỏ**; nếu nó xanh thì test dựng sai mock, không phải luật đã hoạt động.

```csharp
[Fact]
public async Task GetPortfolioOptimization_ThreeSymbolsSameSector60Percent_ShouldFlagOverweight()
{
    // comprehensiveProvider trả Company.Industry = "Tài nguyên cơ bản" cho HPG, HSG, NKG
    // tổng 60tr trên totalValue 100tr, RiskProfile.MaxSectorExposurePercent = 40
    // → đúng 1 SectorExposure, Sector = "Tài nguyên cơ bản", ExposurePercent = 60,
    //   IsOverweight = true
}
```

Dùng lại harness đã có trong `RiskCalculationServiceOptimizationTests.cs` — chỉ thêm phần dựng `comprehensiveProvider` trả `Company.Industry`. Nếu harness ở đó chưa mock `IComprehensiveStockDataProvider` (trước Task này nó chưa cần), thêm mock theo đúng cách file đó đang mock 12 dependency còn lại.

- [ ] **Step 2: Chạy test, xác nhận đỏ vì `SectorExposures` không có mục nào `IsOverweight`**

- [ ] **Step 3: Đổi nguồn tra ngành**

```csharp
// Fetch sector data — dùng provider đang được đăng ký thật. IFundamentalDataProvider
// hiện là NoOpFundamentalDataProvider (Program.cs:201) nên luôn trả null, khiến hạn mức
// ngành 40% chưa từng bắn lần nào.
string? sector = null;
try
{
    var comprehensive = await _comprehensiveProvider.GetComprehensiveDataAsync(symbol, cancellationToken);
    sector = comprehensive?.Company?.Industry;
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Không tra được ngành cho {Symbol}", symbol);
}
```

Giữ nguyên `try/catch` và mức log: một mã tra lỗi không được làm sập cả phép tính danh mục.

- [ ] **Step 4: Chạy test, xác nhận xanh**

- [ ] **Step 5: Test đỏ — rổ "Không xác định" vượt hạn mức cũng phải `IsOverweight = true`**

Spec Q2. Dựng 2 mã provider trả `Industry = null` chiếm 60% → rổ "Không xác định" phải `IsOverweight = true`.

- [ ] **Step 6: Bỏ `IsOverweight = false` hardcode**

Tại [dòng 445](../../../../src/InvestmentApp.Infrastructure/Services/RiskCalculationService.cs#L445), đổi thành cùng phép so như các rổ khác:

```csharp
IsOverweight = exposurePercent > maxSectorExposure
```

Cần đưa `exposurePercent` ra biến trước khi dựng object, vì hiện chỗ này tính `Math.Round` ngay trong initializer.

- [ ] **Step 7: Mutation check**

Trả lại `IsOverweight = false` → test Step 5 phải đỏ. Trả `_fundamentalDataProvider` lại vào Step 3 → test Step 1 phải đỏ. Hoàn lại cả hai.

- [ ] **Step 8: Chạy `dotnet test tests/InvestmentApp.Infrastructure.Tests`, commit**

```bash
git add src tests
git commit -m "fix(risk): tra ngành qua provider đang sống để hạn mức tập trung ngành 40% bắn được"
```

---

### Task 2: Query + endpoint tỷ trọng ngành cho một lệnh dự kiến

**Files:**
- Create: `src/InvestmentApp.Application/Risk/Queries/GetSectorExposureForPlan/GetSectorExposureForPlanQuery.cs`
- Modify: `src/InvestmentApp.Api/Controllers/RiskController.cs`
- Test: `tests/InvestmentApp.Application.Tests/Risk/GetSectorExposureForPlanQueryTests.cs`

**Interfaces:**
- Produces: `SectorExposureForPlanDto { string? Sector, decimal? CurrentPercent, decimal? ProjectedPercent, decimal LimitPercent, List<string> SameSectorSymbols }`.
- Consumes: `IComprehensiveStockDataProvider`, `IPnLService`, `IRiskProfileRepository`, `IPortfolioRepository` — cùng bộ mà `RiskCalculationService` đã dùng.

**Không** gọi `GetPortfolioOptimizationQuery` (spec Q5): query đó quét cả danh mục kèm P&L từng vị thế, còn endpoint này bị gọi lại mỗi lần người dùng sửa số lượng trên form.

- [ ] **Step 1: Test đỏ cho công thức chiếu — ca phân biệt Q3 đúng/sai**

```csharp
[Fact]
public async Task Handle_WhenAddingToExistingSector_ShouldNotAddPlanSizeToDenominator()
{
    // sectorValue = 32tr, totalValue = 100tr (đã gồm tiền mặt), addValue = 9tr
    // ProjectedPercent phải = 41m
    // KHÔNG phải 37.61m — đó là kết quả khi cộng addValue vào mẫu số
}
```

Assert đúng `41m`. Nếu chỉ assert `> 40` thì cả hai công thức đều pass và test không phân biệt được gì.

- [ ] **Step 2: Test đỏ cho ca không tính được — phải là `null`, không phải `0`**

Ba ca riêng, mỗi ca một `[Fact]`: `totalValue = 0`; portfolio không tồn tại; provider trả `Company.Industry = null`. Cả ba assert `CurrentPercent.Should().BeNull()` và `ProjectedPercent.Should().BeNull()` — assert `BeNull()`, không assert `Should().NotBe(0)`.

- [ ] **Step 3: Test đỏ — `SameSectorSymbols` không chứa mã đang lập kế hoạch**

Đang giữ HPG, HSG, NKG; lập kế hoạch cho HPG → trả `["HSG", "NKG"]`.

- [ ] **Step 4: Implement handler, chạy 5 test xanh**

`LimitPercent` đọc từ `RiskProfile.MaxSectorExposurePercent` của người dùng, không hardcode 40.

- [ ] **Step 5: Thêm endpoint vào `RiskController`**

Theo đúng nếp 8 endpoint đang có trong controller đó (`portfolio/{portfolioId}/<tên>`, `[Authorize]` JWT ở cấp class):

```csharp
[HttpGet("portfolio/{portfolioId}/sector-exposure")]
public async Task<IActionResult> GetSectorExposure(
    string portfolioId, [FromQuery] string symbol, [FromQuery] decimal addValue,
    CancellationToken ct)
```

`symbol` và `addValue` **bắt buộc** — cùng lý do với `gate-status` (xem `docs/architecture.md`): thay giá trị thiếu bằng 0 làm endpoint trả một con số trông như thật.

- [ ] **Step 6: Test controller — 401 khi không có JWT, 200 với payload đúng shape**

- [ ] **Step 7: Mutation check**

Cộng `addValue` vào mẫu số → test Step 1 đỏ. Đổi `null` thành `0` ở một ca → test Step 2 đỏ. Hoàn lại.

- [ ] **Step 8: Chạy `dotnet test`, commit**

---

### Task 3: Dòng ngành trong khối cảnh báo kiểm-trước

**Files:**
- Modify: `frontend/src/app/core/services/risk.service.ts` (thêm `sectorExposureForPlan`)
- Modify: `frontend/src/app/features/trade-plan/trade-plan.component.ts`
- Test: `frontend/src/app/features/trade-plan/trade-plan.component.spec.ts`

**Interfaces:**
- Consumes: `GET /api/v1/risk/portfolio/{portfolioId}/sector-exposure?symbol=&addValue=`.

**Phải tra trước, không đoán:** khối cảnh báo kiểm-trước hiện tại gọi `gate-status` với debounce 500ms. Tìm handler đó trong `trade-plan.component.ts` và **gọi thêm** endpoint mới trong cùng handler, cùng điều kiện "đã đủ số để tính" — không tạo debounce thứ hai. Service mới phải tự đính header auth: **không có global auth interceptor** trong project này.

- [ ] **Step 1: Test đỏ — không gọi endpoint khi chưa chọn portfolio**

Chưa có `portfolioId` thì không phát request nào. Assert `httpMock.expectNone`.

- [ ] **Step 2: Test đỏ — render đúng chữ khi vượt hạn mức**

Backend trả `{ sector: "Tài nguyên cơ bản", currentPercent: 32, projectedPercent: 41, limitPercent: 40, sameSectorSymbols: ["HSG","NKG"] }` → DOM có ngành, có cả `32` và `41`, có `40`, và có `HSG`. Assert trên DOM đã render, không assert lên biến của component.

- [ ] **Step 3: Test đỏ — `projectedPercent = null` render "n/a", không render "0"**

Assert văn bản **không** chứa `0%`. Đây là ca dễ pass giả nếu chỉ assert "có chứa n/a".

- [ ] **Step 4: Test đỏ — cảnh báo này không khoá nút nào**

Assert `canSaveDraft()` không tham chiếu dữ liệu ngành, và nút "Lưu nháp" vẫn enable khi `projectedPercent` vượt hạn mức. Cùng cách kiểm mà cảnh báo kiểm-trước hiện tại đã dùng.

- [ ] **Step 5: Implement, chạy `ng test` xanh**

Chữ hiển thị tiếng Việt có dấu đầy đủ. Không thêm màu đỏ gây hiểu là bị chặn — đây là thông tin, không phải lỗi.

- [ ] **Step 6: Verify bằng browser** theo skill `qa-verify`: một mã có ngành, một mã provider không trả ngành. Chụp lại cả hai trạng thái.

- [ ] **Step 7: Commit**

---

### Task 4: Tài liệu

**Files:**
- Modify: `docs/architecture.md` (endpoint mới + ghi rõ `IFundamentalDataProvider` là NoOp)
- Modify: `docs/business-domain.md` (quy tắc tập trung ngành nay có hiệu lực, công thức chiếu, quy ước `null`)
- Modify: `docs/project-context.md` (thêm bug pattern: luật có UI mà không có đường bắn)
- Modify: `frontend/src/assets/CHANGELOG.md` (version kế tiếp — **đọc entry trên cùng rồi theo nếp đó, đừng bịa**)
- Create: hướng dẫn người dùng + **đăng ký Help topic** (thêm file mà không đăng ký thì trang Hướng dẫn không thấy)

- [ ] **Step 1: Cập nhật 4 file docs**

`project-context.md` phải ghi được bài học ở dạng dùng lại được: *một luật hiển thị đủ hạn mức và con số trên UI nhưng không có đường nào bắn cảnh báo thì tệ hơn chưa làm, vì người đọc tin là đã được canh.* Kèm cách phát hiện: lần theo interface mà luật đọc dữ liệu từ đó, kiểm xem `Program.cs` đăng ký implementation nào.

- [ ] **Step 2: Viết hướng dẫn người dùng + đăng ký Help topic**

Tra chỗ registry topic hiện có rồi thêm entry. Không tìm ra registry thì **dừng và báo**, đừng chỉ thêm file.

- [ ] **Step 3: Commit**

---

## Checkpoint — Task 1 + Task 2 (xong, 2026-08-10)

- **Quyết định lệch khỏi plan, có lý do:** plan viết handler tự tính tỷ trọng. Thực tế đặt phép tính vào `RiskCalculationService.GetSectorExposureForPlanAsync` vì công thức `totalValue` sống ở đó — tính lại trong handler là tạo bản sao thứ hai của công thức. Handler chỉ kiểm quyền rồi gọi, giống `GetPortfolioOptimizationQueryHandler`. Kéo theo: test công thức nằm ở `Infrastructure.Tests`, không phải `Application.Tests` như plan viết.
- **Hai helper gom về một chỗ:** `ComputeTotalValue(...)` (dùng bởi cả đường optimization lẫn đường mới) và `ResolveIndustryAsync(...)` (thay khối try/catch trùng lặp).
- **Phát hiện trong lúc làm:** test `GetPortfolioOptimizationAsync_SectorOverweight_ReturnsSectorAlert` **đang xanh** trước khi sửa, dù production không thể đạt `IsOverweight = true` — vì helper `SetupFundamentals` mock `IFundamentalDataProvider`, đúng cái interface được đăng ký là NoOp. Đã gộp helper đó vào `SetupIndustry` (mock provider thật) nên 19 test trong file giờ canh đúng đường production đi.
- **Files changed:** `IRiskCalculationService.cs` (+method, +`SectorExposureForPlan`), `RiskCalculationService.cs`, `RiskController.cs`, `Risk/Queries/GetSectorExposureForPlan/GetSectorExposureForPlanQuery.cs` (mới), `RiskCalculationServiceOptimizationTests.cs`, `GetSectorExposureForPlanQueryHandlerTests.cs` (mới).
- **Tests:** 1751/1751 (baseline master 1742, +9). Mutation check đạt cho cả hai thay đổi Task 1.
- **Next:** Task 3 (FE). Đọc `trade-plan.component.ts` tìm handler debounce 500ms đang gọi `gate-status`, gọi thêm `GET /api/v1/risk/portfolio/{portfolioId}/sector-exposure?symbol=&addValue=` trong **cùng** handler, cùng điều kiện "đã đủ số". Thêm method vào `risk.service.ts` và **tự đính header auth** — project không có global auth interceptor. Rồi Task 4 (ADR-0012 + 4 docs + CHANGELOG + Help topic).

## Chặng sau — chốt 2026-08-10, làm SAU Task 1-4

Không thuộc phạm vi PR này, ghi lại để không rơi:

**A. Hiện sàn giao dịch trong thông tin công ty.** Provider đã trả `ComprehensiveStockData.Company.Exchange` (từ `company.Floor`). Chỉ cần hiện lên trang hồ sơ công ty — **không** dựng taxonomy sàn mới: mã sàn đã hardcode 3 chỗ trong repo (`market-data.component.ts:728` dạng mảng FE, `GetTopFluctuationQuery.cs:9` dạng comment, `HmoneyMarketDataProvider.cs:176` dạng switch). Nếu sau này cần dùng chung thì hợp nhất 3 chỗ đó, đừng thêm bản thứ tư.

**B. Hardcode danh sách nhóm ngành thành dropdown.** Nhóm ngành gần như không đổi nên hardcode được, nhưng **repo hiện không có taxonomy nào** và nguồn duy nhất là `indicators.GroupName` trả về theo từng mã. Trình tự bắt buộc: dò 24hmoney qua provider cho một rọ mã đại diện (VN30 + vài mã HNX/UPCOM) → thu `GroupName` thật → trình danh sách cho chủ sở hữu xem → mới hardcode. **Không bịa taxonomy.** Rủi ro phải canh: danh sách hardcode lệch với nhãn provider trả về sẽ sinh hai taxonomy song song, nên nhãn hardcode phải là **chính chuỗi** provider trả, không phải bản dịch lại.

Việc này đảo lại **spec Q6** (đang ghi: chưa thêm field ngành vào hồ sơ, chờ cửa sổ dùng thử). Khi thi hành phải sửa Q6 tại chỗ trong spec kèm lý do đổi ý, không để hai câu trái nhau trong cùng tài liệu.

## Thứ tự

Task 1 → 2 → 3 → 4. Task 1 giao được giá trị một mình (risk-dashboard có số thật) nên nếu phải dừng giữa đường thì dừng sau Task 1, không dừng giữa Task 2.

**Ưu tiên so với việc khác:** thấp hơn chặng 2 của hồ sơ công ty (đường ghi trade plan của agent đang tắt) và thấp hơn Task 1 của [plan dọn nợ](../2026-08-10-company-dossier-backlog.md) (race trên `ux_user_symbol` gây 500).
