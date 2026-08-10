# Hồ sơ công ty — dọn nợ sau chặng 1

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Trạng thái (soát 2026-08-10):** Task 3 **xong** (đi kèm PR #149/#150 — `gateStatus` đã có caller ở `trade-plan.component.ts`, trang danh sách có `loadError`, bộ đếm dùng `trim()`, hai component gọi `dossierFreshnessLabel` dùng chung). Task 4 **xong** (4a + 4b, v2.76.1). **Còn lại: Task 1** (race `ux_user_symbol` — repository vẫn `InsertOneAsync` trơ, chưa bắt `DuplicateKey`; lưu ý method tên **`CreateAsync`**, không phải `AddAsync` như plan ghi bên dưới), **Task 2** (chờ chốt A/B).

**Goal:** Đóng các mục đã biết nhưng cố ý để ngoài PR #147, để chặng 2 khởi động trên nền sạch.

**Architecture:** Không thêm thành phần mới. Sửa tại chỗ trong `CompanyDossierRepository`, `UpdateTradePlanCommand`, hai component FE hồ sơ và `trade-plan.component.ts`.

**Tech Stack:** .NET 9 (xUnit + FluentAssertions + Moq), Angular 19 (Karma + Jasmine), MongoDB Driver 3.6.0.

**Xuất xứ:** PR #147 (chặng 1) + báo cáo `scratch/qa-reports/qa-verify-company-dossier-20260810-0030z.md`. Không mục nào trong đây là bypass của cổng — cổng đã đóng cả năm cửa hậu ở PR #147. Đây là độ bền và chất lượng hiển thị.

**Không thuộc plan này:**
- Chặng 2 (fundamentals REST + 5 MCP tool + panel) và chặng 3 (đề xuất `InvalidationRule` + danh sách chờ soát) — ở [`done/2026-08-09-company-dossier-guard.md`](done/2026-08-09-company-dossier-guard.md), Task 9–14, **đã xong cả hai** (PR #150, #151). Ưu tiên "chặng 2 trước Task 1" trong plan này đã hết hiệu lực.
- Ngành nghề doanh nghiệp + đo tập trung ngành — tính năng mới, phải qua brainstorming trước, không đưa vào plan sửa nợ.

## Global Constraints

- Mọi text hiển thị: **tiếng Việt có dấu đầy đủ**. Commit message tiếng Việt có dấu, prefix conventional-commit tiếng Anh.
- TDD bắt buộc: test đỏ trước, rồi implement. Sau mỗi task chạy lại project test tương ứng.
- Ngưỡng 5% chỉ đọc từ `TradePlan.LargeTierThreshold`, không hardcode lần thứ năm.
- Không sửa hợp đồng `gate-status` (3 param bắt buộc + `Freshness`) — xem `docs/architecture.md`.
- Bất biến ADR-0011 D9 phải giữ: giá trị cổng chấm bằng giá trị plan thực sự lưu xuống.

---

### Task 1: Race find-then-insert trên `ux_user_symbol`

**Files:**
- Modify: `src/InvestmentApp.Infrastructure/Repositories/CompanyDossierRepository.cs`
- Modify: `src/InvestmentApp.Application/Common/Interfaces/ICompanyDossierRepository.cs` (nếu cần phơi exception mới)
- Test: `tests/InvestmentApp.Infrastructure.Tests/Repositories/CompanyDossierRepositoryTests.cs`

**Interfaces:**
- Produces: `DuplicateDossierException` (hoặc tên tương đương) — `UpsertCompanyDossierCommandHandler` bắt để retry.

**Vấn đề:** `AddAsync` là `InsertOneAsync` trần ([CompanyDossierRepository.cs:39](../../../src/InvestmentApp.Infrastructure/Repositories/CompanyDossierRepository.cs#L39)). Luồng upsert là find → insert-on-miss. Hai request đồng thời cho cùng `(UserId, Symbol)` đều thấy `null`, đều insert, cái thứ hai vi phạm index unique `ux_user_symbol` → `MongoWriteException` không ai bắt → **500**. Người dùng bấm Lưu hai lần nhanh là gặp. Đây là repository đầu tiên trong Infrastructure có index unique, nên chưa có tiền lệ xử lý trong codebase — mẫu chuẩn: index unique + đổi `MongoWriteException` thành exception có kiểu + caller retry một lần bằng find.

- [ ] **Step 1: Test đỏ — insert trùng khoá ném exception có kiểu, không phải MongoWriteException**

Test phải assert **kiểu** exception, không assert "có throw" — `InsertOneAsync` vốn đã throw, nên assert suông sẽ xanh cả khi chưa sửa gì.

```csharp
[Fact]
public async Task AddAsync_WhenUserSymbolAlreadyExists_ShouldThrowDuplicateDossierException()
{
    // Cần Mongo thật hoặc test double có kiểm index; nếu suite Infrastructure đang chạy
    // không cần Mongo, dựng test ở tầng bắt-và-đổi-kiểu thay vì tầng driver:
    // giả lập IMongoCollection ném MongoWriteException với ServerErrorCategory
    // DuplicateKey và assert repository đổi thành DuplicateDossierException.
}
```

Đọc các test Infrastructure hiện có trước để biết suite này dựng `IMongoCollection` bằng cách nào — **không đoán**. Nếu suite không mock được `IMongoCollection`, dừng và báo lại thay vì tự thêm dependency mới.

- [ ] **Step 2: Chạy test, xác nhận đỏ vì kiểu exception sai**

- [ ] **Step 3: Đổi kiểu exception trong repository**

Phân biệt bằng **tên index**, không bằng chuỗi message — cùng một collection sau này có thể có index unique thứ hai.

```csharp
public async Task AddAsync(CompanyDossier dossier)
{
    try
    {
        await _collection.InsertOneAsync(dossier);
    }
    catch (MongoWriteException ex) when (
        ex.WriteError?.Category == ServerErrorCategory.DuplicateKey
        && ex.WriteError.Message.Contains("ux_user_symbol"))
    {
        throw new DuplicateDossierException(dossier.UserId, dossier.Symbol, ex);
    }
}
```

- [ ] **Step 4: Test đỏ cho retry ở handler**

`UpsertCompanyDossierCommandHandler`: repository `GetAsync` trả `null` lần đầu, `AddAsync` ném `DuplicateDossierException`, `GetAsync` lần hai trả document → handler phải update lên document đó và **không** để exception thoát ra. Assert `UpdateAsync` được gọi đúng một lần.

- [ ] **Step 5: Implement retry, chạy test xanh**

Retry **đúng một lần**. Vòng lặp không giới hạn ở đây là đổi 500 thành treo request.

- [ ] **Step 6: Mutation check**

Bỏ điều kiện `Contains("ux_user_symbol")` → test vẫn xanh (không phân biệt được index nào)? Nếu vẫn xanh thì test chưa đủ, phải thêm ca index khác.

- [ ] **Step 7: Chạy `dotnet test tests/InvestmentApp.Infrastructure.Tests` + `tests/InvestmentApp.Application.Tests`, commit**

```bash
git add src tests
git commit -m "fix(dossier): đổi lỗi trùng khoá ux_user_symbol thành exception có kiểu và retry một lần"
```

---

### Task 2: Đường sửa gọi `SetLots` với `lots: []` làm `Quantity = 0`

**Files:**
- Modify: `src/InvestmentApp.Application/TradePlans/Commands/UpdateTradePlan/UpdateTradePlanCommand.cs:142`
- Test: `tests/InvestmentApp.Application.Tests/CompanyDossiers/TradePlanDossierGateWiringTests.cs`

**Vấn đề:** guard của đường sửa là `request.EntryMode != null && request.Lots != null` — thiếu `Count > 0` mà đường tạo có. `lots: []` làm `SetLots(mode, [])` chạy và gán `Quantity = lots.Sum(...) = 0` **sau** khi cổng đã chấm theo quantity cũ. **Fail an toàn** (cổng chấm to hơn giá trị lưu xuống, không phải bypass) và có sẵn từ trước nhánh hồ sơ công ty — nên đã cố ý để ngoài PR #147. Nhưng nó tạo ra plan `Quantity = 0`, là dữ liệu vô nghĩa.

**Phải tra trước khi sửa:** FE hiện gửi `lots: undefined` khi rỗng ([trade-plan.component.ts:3278](../../../frontend/src/app/features/trade-plan/trade-plan.component.ts#L3278) — `lots.length > 0 ? map : undefined`), nên thêm `Count > 0` **không** phá FE. Nhưng nó cũng có nghĩa là **hiện không có cách nào xoá hết lots qua API** — gửi `undefined` thì `SetLots` không chạy, lots cũ còn nguyên. Quyết định cần chốt với chủ sở hữu trước khi code:

- Phương án A: thêm `Count > 0` → hết `Quantity = 0`, và xoá lots vẫn không làm được (giữ nguyên hiện trạng, không tệ thêm).
- Phương án B: thêm `Count > 0` **và** một cờ rõ nghĩa (`ClearLots = true`) để xoá lots có chủ đích, kèm gán lại `Quantity` từ header.

**Đã chốt (2026-08-10): phương án A** — chỉ thêm `Count > 0`. Hết `Quantity = 0`; chấp nhận việc xoá hết lots qua API vẫn không làm được (giữ nguyên hiện trạng, không tệ thêm). Không thêm cờ `ClearLots` — chưa có nhu cầu thật, thêm cờ là thêm một đường ghi phải bảo vệ.

- [x] **Step 1: Hỏi chủ sở hữu A hay B** → **A**
- [ ] **Step 2: Test đỏ cho phương án đã chốt**
- [ ] **Step 3: Implement, giữ `willApplyLots` là một biến duy nhất (ADR-0011 D9)**
- [ ] **Step 4: Chạy `dotnet test tests/InvestmentApp.Application.Tests`, commit**

---

### Task 3: Bốn mục chất lượng FE từ báo cáo QA — XONG (đi kèm PR #149/#150)

**Files:**
- Modify: `frontend/src/app/features/company-dossier/company-dossier-list.component.ts`
- Modify: `frontend/src/app/features/company-dossier/company-dossier-detail.component.ts`
- Modify: `frontend/src/app/core/services/company-dossier.service.ts`
- Modify: `frontend/src/app/features/market-data/market-data.component.ts`
- Test: `.spec.ts` cạnh mỗi component

**3a. `gateStatus()` chưa có caller nào** — service có phương thức đúng chữ ký nhưng không ai gọi. Luồng "Tạo Trade Plan từ gợi ý" ở trang thị trường dùng `get(symbol)` vì chỗ đó chưa biết số lượng và số dư. Hệ quả: hồ sơ `Fresh` nhưng **chưa đủ cho lệnh lớn** vẫn đi qua màn đó, chỉ bị chặn sau khi điền hết form. Sửa: hoặc gọi `gateStatus` khi đã đủ ba số, hoặc bỏ `gateStatus` khỏi service nếu quyết định là màn đó không kiểm theo size. **Không để phương thức chết trong service.**

**3b. Trang danh sách nuốt lỗi tải** — tải thất bại và "chưa có hồ sơ nào" hiển thị giống nhau. Thêm state lỗi riêng với câu tiếng Việt và nút thử lại. Test: `httpMock` trả 500 → DOM có câu lỗi, **không** có câu rỗng.

**3c. Đếm ký tự không trim còn `canSign()` thì trim** — chuỗi 30 khoảng trắng hiện "30/30" mà nút ký vẫn khoá, không nói vì sao. Cho bộ đếm dùng cùng một hàm chuẩn hoá với `canSign()`.

**3d. `freshnessLabel()`/`freshnessClass()` trùng nguyên văn** giữa trang danh sách và trang chi tiết. Đã có `dossierFreshnessLabel`/`dossierFreshnessBadgeClass` trong service — hai component phải gọi hàm chung đó thay vì tự khai lại.

- [ ] **Step 1: Test đỏ cho từng mục (4 spec)**
- [ ] **Step 2: Implement từng mục, chạy `ng test` sau mỗi mục**
- [ ] **Step 3: Verify bằng browser** theo `.claude/skills/qa-verify` — riêng 3b phải thấy state lỗi thật
- [ ] **Step 4: Commit**

---

### Task 4: Hai mục tài liệu / tooling — XONG (v2.76.1)

**Files:**
- Modify: skill `qa-verify` (phần Prerequisites + câu lệnh mint ở Step 2)
- Modify: `src/InvestmentApp.Application/CompanyDossiers/Gate/CompanyDossierGate.cs`

**4a. Drift trong skill `qa-verify`** — skill ghi `phdfieldkidpro@gmail.com` nhưng `StableJwtMint.ALLOWED_EMAILS` chỉ có `investmate.support@gmail.com`. Chạy theo skill nguyên văn sẽ fail ở `EnsureEmailAllowed`. Sửa skill cho khớp source (không sửa source cho khớp skill).

**4b. Câu thông báo `riskFactors` lệch mẫu** — `"riskFactors: mô tả không được để trống ở hạng {ranks}"` là một mệnh đề, trong khi các câu cùng bộ theo mẫu "cần X, đang có Y". Đồng bộ để FE render `missing[]` đọc thành một khối nhất quán. ~~Có test đang assert nguyên văn câu này~~ — thực tế test `LargeTier_RiskFactorWithBlankDescription_ShouldBlock` chỉ assert **lỏng** bằng ba `Contains`, nên đổi câu vẫn xanh; phải siết thành assertion nguyên văn thì mới có bước đỏ.

- [x] **Step 1: Sửa skill, đọc lại `StableJwtMint.ALLOWED_EMAILS` để lấy đúng email**
- [x] **Step 2: Test đỏ cho câu thông báo mới, sửa gate, chạy `dotnet test tests/InvestmentApp.Application.Tests`**
- [x] **Step 3: Commit**

**Kết quả:** câu mới là `"riskFactors: cần mô tả ở mọi yếu tố, đang để trống ở hạng {ranks}"`. Email đúng là `investmate.support@gmail.com`, đã sửa ở dòng 26 (Prerequisites) và dòng 59 (`MINT_EMAIL`) của `.claude/commands/qa-verify.md`. Thêm test `LargeTier_MultipleBlankDescriptions_ShouldListEveryRank` ghim cách nối nhiều hạng. 403 pass.

---

## Thứ tự đề nghị

Task 3 và Task 4 đã xong. Còn Task 1 (độ bền dữ liệu, người dùng gặp được ngay bằng cách bấm Lưu hai lần) — làm trước. Task 2 chờ chốt A/B.
