# Gắn hồ sơ công ty vào timeline + symbol click được toàn app — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: dùng superpowers:subagent-driven-development (khuyến nghị) hoặc superpowers:executing-plans để thi hành từng task. Các bước dùng checkbox (`- [ ]`) để theo dõi.

**Goal:** Mọi mã chứng khoán hiển thị trong app đều bấm được để mở timeline của mã đó, và timeline hiển thị luôn các mốc của hồ sơ công ty (ký / agent sửa / hết hạn) cạnh nhật ký và lệnh.

**Architecture:** Một attribute directive dùng chung (`appSymbolLink`) gắn lên phần tử đang hiển thị mã — không phải component mới, để không phải đổi cấu trúc template ở hàng chục chỗ. Timeline mở rộng `TimelineItemDto.Type` sang `"dossier"`, dữ liệu lấy từ `CompanyDossier` hiện có.

**Tech Stack:** .NET 9 (Clean Architecture, MediatR), Angular 19 standalone + inline template, MongoDB Driver 3.6.0, xUnit + FluentAssertions + Moq, Karma + Jasmine.

## Trạng thái đi vào plan này

Cổng hồ sơ công ty **đã xong cả ba chặng** (PR #147 chặng 1, #149 + #150 chặng 2, #151 chặng 3). Plan gốc: [`2026-08-09-company-dossier-guard.md`](2026-08-09-company-dossier-guard.md) — đọc checkpoint chặng 2 và chặng 3 ở cuối file đó trước khi bắt đầu.

Plan này là việc **mới**, phát sinh từ hai ý trong phiên 2026-08-10:

1. "Với các symbol của các mã trong toàn bộ app thì đều được gắn link vào" → Task 1–3.
2. "Sau khi có hồ sơ công ty này thì gắn nó vào với tính năng timeline nhật ký" → Task 4–5.

**Quyết định đã chốt với người dùng:** symbol click → **`/symbol-timeline/:symbol`** (không phải trang hồ sơ). Lý do: timeline gộp nhật ký + lệnh + alert + kỳ nắm giữ, trả lời đúng câu "tôi đã làm gì với mã này"; còn trang hồ sơ thì đa số mã chưa có hồ sơ nên sẽ mở ra form trống.

## Global Constraints

- Route đích: `/symbol-timeline/:symbol` (đã tồn tại trong `frontend/src/app/app.routes.ts:151`).
- Mọi text hiển thị **tiếng Việt có dấu đầy đủ**.
- Inline template trong `@Component({ template: \`...\` })` — không tách file `.html`. **Không** dùng backtick trong HTML comment (đóng template literal sớm → TS1005 cascade).
- Input nhập mã vẫn dùng `appUppercase` (`UppercaseDirective`); directive mới **chỉ** làm link, không chạm chuyện uppercase.
- Không có auth interceptor toàn cục: service mới phải tự gắn header như `company-dossier.service.ts` đang làm.
- `TimelineItemDto.Type` hiện là string `"journal" | "trade" | "alert" | "event"` — thêm giá trị, **không** đổi kiểu.
- TDD: test trước, chạy để thấy đỏ, rồi mới implement.

---

## Task 1: `SymbolLinkDirective`

**Files:**
- Create: `frontend/src/app/shared/directives/symbol-link.directive.ts`
- Test: `frontend/src/app/shared/directives/symbol-link.directive.spec.ts`

**Interfaces:**
- Produces: `SymbolLinkDirective`, selector `[appSymbolLink]`, input `appSymbolLink: string`

Vì sao directive chứ không phải component: mã đang nằm trong hàng chục template dưới dạng `{{ x.symbol }}` bên trong `<span>`, `<td>`, `<div>`. Một component `<app-symbol-link>` buộc phải sửa cấu trúc từng chỗ; một attribute directive chỉ cần thêm một attribute vào phần tử đang có.

- [ ] **Step 1: Viết test**

```typescript
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SymbolLinkDirective } from './symbol-link.directive';

@Component({
  standalone: true,
  imports: [SymbolLinkDirective],
  template: `<span [appSymbolLink]="sym">{{ sym }}</span>`,
})
class HostComponent { sym = 'hpg'; }

describe('SymbolLinkDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('điều hướng tới timeline của mã, chuẩn hoá về chữ in', () => {
    const spy = spyOn(router, 'navigate');
    fixture.nativeElement.querySelector('span').click();
    expect(spy).toHaveBeenCalledWith(['/symbol-timeline', 'HPG']);
  });

  it('mã rỗng thì không gắn link và không điều hướng', () => {
    fixture.componentInstance.sym = '   ';
    fixture.detectChanges();
    const spy = spyOn(router, 'navigate');
    const el = fixture.nativeElement.querySelector('span');
    el.click();
    expect(spy).not.toHaveBeenCalled();
    expect(el.getAttribute('role')).toBeNull();
  });

  it('bấm Enter cũng đi được (a11y — không chỉ chuột)', () => {
    const spy = spyOn(router, 'navigate');
    fixture.nativeElement.querySelector('span')
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(spy).toHaveBeenCalledWith(['/symbol-timeline', 'HPG']);
  });

  it('không chặn click của phần tử cha khi mã rỗng', () => {
    // Nếu directive luôn stopPropagation thì hàng bảng có (click) riêng sẽ chết theo.
    fixture.componentInstance.sym = '';
    fixture.detectChanges();
    let bubbled = false;
    fixture.nativeElement.addEventListener('click', () => (bubbled = true));
    fixture.nativeElement.querySelector('span').click();
    expect(bubbled).toBeTrue();
  });
});
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `cd frontend && npx ng test --watch=false --include='**/symbol-link.directive.spec.ts'`
Expected: FAIL — directive chưa tồn tại

- [ ] **Step 3: Implement**

```typescript
import { Directive, HostBinding, HostListener, Input, inject } from '@angular/core';
import { Router } from '@angular/router';

/**
 * Gắn lên phần tử đang hiển thị một mã chứng khoán để bấm được sang timeline của mã đó.
 * Attribute directive (không phải component) để không phải đổi cấu trúc template ở hàng chục chỗ.
 */
@Directive({ selector: '[appSymbolLink]', standalone: true })
export class SymbolLinkDirective {
  private router = inject(Router);

  @Input('appSymbolLink') symbol = '';

  private get normalized(): string {
    return (this.symbol ?? '').trim().toUpperCase();
  }

  // Mã rỗng thì không giả vờ là link: con trỏ, role, tabindex đều tắt.
  @HostBinding('class.cursor-pointer') get clickable() { return !!this.normalized; }
  @HostBinding('class.hover:underline') get underline() { return !!this.normalized; }
  @HostBinding('attr.role') get role() { return this.normalized ? 'link' : null; }
  @HostBinding('attr.tabindex') get tabindex() { return this.normalized ? 0 : null; }
  @HostBinding('attr.title') get title() {
    return this.normalized ? `Xem dòng thời gian ${this.normalized}` : null;
  }

  @HostListener('click', ['$event'])
  onClick(event: Event): void {
    if (!this.normalized) return;   // để click nổi lên cha như bình thường
    event.stopPropagation();
    this.router.navigate(['/symbol-timeline', this.normalized]);
  }

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    if (!this.normalized) return;
    event.preventDefault();
    event.stopPropagation();
    this.router.navigate(['/symbol-timeline', this.normalized]);
  }
}
```

- [ ] **Step 4: Chạy test, xác nhận pass** — 4 test

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/shared/directives/symbol-link.directive.ts frontend/src/app/shared/directives/symbol-link.directive.spec.ts
git commit -m "feat(shared): directive gắn link mã chứng khoán sang dòng thời gian"
```

---

## Task 2: Khảo sát chỗ hiển thị mã và chốt danh sách áp dụng

**Files:** không sửa code ở task này — chỉ tạo danh sách để Task 3 thi hành.

Việc quan trọng nhất của task này là **đừng áp bừa**. Có ba loại chỗ hiển thị mã, và chỉ một loại nên gắn link:

| Loại | Ví dụ | Gắn link? |
|---|---|---|
| Mã trong bảng/thẻ danh sách, nằm trong phần tử riêng | ô "MÃ CK" ở bảng trade plan, thẻ vị thế | ✅ |
| Mã bên trong một hàng đã có `(click)` riêng | hàng bảng mở modal chi tiết | ✅ nhưng phải `stopPropagation` (Task 1 đã làm) |
| Mã trong câu văn, trong `<option>`, trong input, trong heading của **chính** trang mã đó | "Hồ sơ công ty: HPG", `<option>` chọn mã | ❌ — link tới chính trang đang mở, hoặc phá `<select>` |

- [ ] **Step 1: Liệt kê ứng viên**

```bash
cd frontend/src/app
grep -rn "{{ *[a-zA-Z_.]*symbol *}}\|{{ *[a-zA-Z_.]*Symbol *}}" --include=*.ts features/ shared/ | grep -v spec.ts
```

- [ ] **Step 2: Ghi bảng phân loại vào chính file plan này**

Mỗi dòng: `file:line` · loại (1/2/3) · gắn hay không · lý do nếu không. Bảng này là đầu vào của Task 3, và là bằng chứng "đã xem hết" chứ không phải sửa vài chỗ rồi tuyên bố xong.

- [ ] **Step 3: Commit bảng**

```bash
git add docs/superpowers/plans/2026-08-10-dossier-symbol-links-and-timeline.md
git commit -m "docs(plan): bảng phân loại chỗ hiển thị mã cho symbol link"
```

---

## Task 3: Áp directive theo bảng ở Task 2

**Files:** các file trong bảng Task 2 (dự kiến 8–15 file feature).

- [ ] **Step 1: Áp từng file**

Với mỗi chỗ đã đánh ✅: thêm `SymbolLinkDirective` vào `imports` của component, và thêm attribute vào phần tử đang hiển thị mã:

```html
<span [appSymbolLink]="p.symbol">{{ p.symbol }}</span>
```

- [ ] **Step 2: Chạy build + toàn bộ test frontend**

Run: `cd frontend && npx ng build --configuration development && npx ng test --watch=false`
Expected: build sạch, test không giảm số lượng.

**Cạm bẫy đã biết:** thêm một directive vào `imports` của component nào thì component đó phải là standalone (cả app này đều standalone). Nếu quên `imports`, Angular **không** báo lỗi — attribute bị bỏ qua im lặng và mã trông như thường, chỉ là không bấm được. Vì vậy Step 3 phải kiểm bằng DOM, không kiểm bằng mắt.

- [ ] **Step 3: Test DOM một mẫu đại diện**

Chọn 2 trang (một bảng, một thẻ), viết spec kiểm `[role="link"]` xuất hiện đúng số lần bằng số mã hiển thị. Đây là cách duy nhất bắt được ca "quên `imports`".

- [ ] **Step 4: Verify browser**

Mở `localhost:4200`, bấm một mã ở mỗi trang đã sửa, xác nhận sang đúng `/symbol-timeline/<mã>`. Dán ảnh/kết quả.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app
git commit -m "feat(ui): mã chứng khoán bấm được sang dòng thời gian ở các trang danh sách"
```

---

## Task 4: Mốc hồ sơ công ty trong timeline (backend)

**Files:**
- Modify: `src/InvestmentApp.Application/JournalEntries/Queries/GetSymbolTimeline/GetSymbolTimelineQuery.cs`
- Test: `tests/InvestmentApp.Application.Tests/JournalEntries/GetSymbolTimelineDossierTests.cs` (tạo mới nếu chưa có thư mục)

**Interfaces:**
- Consumes: `ICompanyDossierRepository.GetAsync(userId, symbol)`
- Produces: các `TimelineItemDto` với `Type = "dossier"`, `Data = { action, freshness, version }`

**Giới hạn phải nói thẳng trước khi code:** `CompanyDossier` **không lưu lịch sử**. Nó chỉ có `ReviewedAt`, `ConfirmedAt`, `AgentDraftedAt`, `Version`. Nghĩa là timeline chỉ dựng được **tối đa 2 mốc** (lần ký gần nhất, lần agent sửa gần nhất) — không phải lịch sử tiến hoá của luận điểm.

Muốn có lịch sử thật thì phải lưu snapshot mỗi lần ký, và đó là một việc riêng (đã nằm trong danh sách ngoài phạm vi của plan gốc: *"snapshot hồ sơ đóng băng vào plan lúc arm"*). **Không** âm thầm làm luôn ở task này; nếu người dùng muốn thì tách plan mới.

- [ ] **Step 1: Viết test**

```csharp
[Fact]
public async Task Timeline_ShouldIncludeDossierSignedMarker()
{
    // ConfirmedAt là mốc duy nhất chắc chắn có nghĩa: "người này đã đọc và chịu trách nhiệm".
    var dossier = ConfirmedDossier("HPG");
    _dossierRepo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(dossier);

    var result = await Sut().Handle(new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "HPG" }, default);

    result.Items.Should().Contain(i => i.Type == "dossier");
}

[Fact]
public async Task Timeline_WithoutDossier_ShouldNotAddAnyDossierItem()
{
    _dossierRepo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync((CompanyDossier?)null);

    var result = await Sut().Handle(new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "HPG" }, default);

    result.Items.Should().NotContain(i => i.Type == "dossier");
}

[Fact]
public async Task Timeline_AgentDraftAfterConfirm_ShouldAppearAsSeparateMarker()
{
    // Agent sửa sau khi ký là mốc đáng thấy: nó kéo ConfirmedAt về null và cổng chặn lại.
    var dossier = ConfirmedThenAgentDrafted("HPG");
    _dossierRepo.Setup(r => r.GetAsync("user-1", "HPG")).ReturnsAsync(dossier);

    var result = await Sut().Handle(new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "HPG" }, default);

    result.Items.Count(i => i.Type == "dossier").Should().Be(2);
}
```

- [ ] **Step 2: Chạy test, xác nhận fail**

Run: `dotnet test tests/InvestmentApp.Application.Tests --filter GetSymbolTimelineDossier`

- [ ] **Step 3: Implement**

Inject `ICompanyDossierRepository` vào handler. Sau khi dựng các item hiện có, thêm:
- `ConfirmedAt != null` → item `{ action: "signed" }` tại `ConfirmedAt`
- `AgentDraftedAt != null` → item `{ action: "agent-drafted" }` tại `AgentDraftedAt`

Giữ nguyên cách sort item hiện có của query — **đọc trước khi thêm**, đừng đoán là nó sort theo `Timestamp` desc.

- [ ] **Step 4: Chạy test, xác nhận pass** — 3 test

- [ ] **Step 5: Commit**

```bash
git add src/InvestmentApp.Application/JournalEntries tests/InvestmentApp.Application.Tests
git commit -m "feat(timeline): thêm mốc ký và mốc agent sửa hồ sơ vào dòng thời gian của mã"
```

---

## Task 5: Hiển thị mốc hồ sơ trên timeline + tài liệu

**Files:**
- Modify: `frontend/src/app/features/symbol-timeline/symbol-timeline.component.ts`
- Modify: `docs/architecture.md`, `docs/business-domain.md`, `docs/features.md`
- Modify: `frontend/src/assets/CHANGELOG.md`, `frontend/src/assets/docs/ho-so-cong-ty.md`

- [ ] **Step 1: Đọc cách component render `Type` hiện có**

Nó đang switch trên `"journal" | "trade" | "alert" | "event"`. Thêm nhánh `"dossier"` — **kiểm xem nhánh mặc định làm gì**: nếu nó bỏ qua item lạ trong im lặng thì Task 4 đã đúng mà UI vẫn trống, và đó là loại lỗi mất nhiều thời gian nhất để tìm.

- [ ] **Step 2: Viết spec DOM**

```typescript
it('hiện mốc ký hồ sơ trên dòng thời gian', () => {
  component.timeline = { symbol: 'HPG', items: [
    { type: 'dossier', timestamp: '2026-08-01T00:00:00Z', data: { action: 'signed' } }
  ] } as any;
  fixture.detectChanges();

  expect(fixture.nativeElement.textContent).toContain('Ký hồ sơ');
});
```

- [ ] **Step 3: Implement nhánh render**

Icon riêng + nhãn tiếng Việt: `signed` → "Ký hồ sơ công ty", `agent-drafted` → "Trợ lý AI sửa hồ sơ — chờ bạn ký lại". Có link `[appSymbolLink]` hoặc `routerLink` sang trang hồ sơ.

- [ ] **Step 4: Chạy toàn bộ test + verify browser**

Run: `dotnet test` (tắt API trước — API đang chạy sẽ khoá DLL và `Api.Tests` bị bỏ qua **im lặng**) và `cd frontend && npx ng test --watch=false`. Dán output.

- [ ] **Step 5: Cập nhật tài liệu + CHANGELOG**

Nói rõ giới hạn: timeline hiện **2 mốc gần nhất**, không phải lịch sử tiến hoá luận điểm — vì hồ sơ chưa lưu snapshot.

- [ ] **Step 6: Commit + PR**

Mở PR bằng skill `/pr` — **không** tự gõ `gh pr create` (bỏ qua cổng code-review và cổng quét bí mật).

---

## Ghi chú thi hành

- Task 1–3 và Task 4–5 **độc lập nhau**. Nếu hết thời gian thì làm xong 1–3 và mở PR riêng; đừng để hai việc dở dang trong một nhánh.
- Task nào có bước verify thật (browser, curl) thì **phải dán output**. Đánh `[x]` mà không chạy là hoàn thành trên giấy.
- Đọc `.claude/` + `CLAUDE.md` trước khi commit: commit message tiếng Việt có dấu, review sub-agent là bắt buộc, quét bí mật là cổng cứng.
- Ngoài phạm vi, ghi rõ để không ai tưởng bị bỏ sót: **snapshot hồ sơ theo từng lần ký** (điều kiện để timeline có lịch sử thật), neo hạn tươi theo kỳ BCTC, và dropdown nhóm ngành 24hmoney (đảo quyết định Q6 của spec gốc — nếu làm thì phải sửa Q6 tại chỗ, không thêm ghi chú ở cuối).
