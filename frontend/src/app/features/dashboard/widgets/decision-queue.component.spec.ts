/**
 * Decision Queue widget — vị trí #1 trên Home (P3 + P4 Decision Engine v1.1).
 * Tests:
 *   1. Empty state hiển thị khi 0 alert + streak ≥ 1 day → ✅ "Hôm nay đang kỷ luật" + 🔥 X ngày.
 *   2. Empty state ẩn streak badge khi streak = 0 hoặc hasData = false.
 *   3. Active queue render N items, sort theo severity (Critical đầu tiên).
 *   4. Cap 5 items, hiện overflow link khi tổng > 5.
 *   5. Severity/type label đúng tiếng Việt.
 *   6. Loading skeleton hiện trước khi service trả về.
 *   7. BÁN button điều hướng sang màn hình bán, form fill sẵn từ kế hoạch (KHÔNG resolve ngay).
 *   8. (P4) GIỮ button expand inline note form.
 *   9. (P4) Submit button disabled khi note < 20 chars.
 *  10. (P4) Item bị remove khỏi list sau resolve thành công.
 *  11. (P4) BÁN button ẩn khi item không có tradePlanId.
 */
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';
import { DecisionQueueComponent } from './decision-queue.component';
import { DecisionService, DecisionItemDto, DecisionQueueDto, DecisionType } from '../../../core/services/decision.service';
import { DisciplineService, DisciplineStreakDto } from '../../../core/services/discipline.service';
import { TradePlanService, TradePlan } from '../../../core/services/trade-plan.service';

const mockItem = (over: Partial<DecisionItemDto> = {}): DecisionItemDto => ({
  id: 'StopLossHit:p1:FPT',
  type: 'StopLossHit',
  severity: 'Critical',
  symbol: 'FPT',
  portfolioId: 'p1',
  portfolioName: 'Main',
  headline: 'FPT đã thủng SL 89.5 (giá 89.4)',
  thesisOrReason: null,
  currentPrice: 89.4,
  plannedExitPrice: 89.5,
  tradePlanId: null,
  dueAt: new Date().toISOString(),
  createdAt: new Date().toISOString(),
  ...over,
});

describe('DecisionQueueComponent', () => {
  let fixture: ComponentFixture<DecisionQueueComponent>;
  let component: DecisionQueueComponent;
  let decisionSpy: jasmine.SpyObj<DecisionService>;
  let disciplineSpy: jasmine.SpyObj<DisciplineService>;

  let planSpy: jasmine.SpyObj<TradePlanService>;

  function setup(queue: DecisionQueueDto, streak: DisciplineStreakDto) {
    decisionSpy = jasmine.createSpyObj('DecisionService', ['getQueue', 'resolve']);
    disciplineSpy = jasmine.createSpyObj('DisciplineService', ['getStreak']);
    planSpy = jasmine.createSpyObj('TradePlanService', ['updateStopLoss', 'getById']);
    decisionSpy.getQueue.and.returnValue(of(queue));
    disciplineSpy.getStreak.and.returnValue(of(streak));
    planSpy.updateStopLoss.and.returnValue(of(undefined as void));
    planSpy.getById.and.returnValue(of({ quantity: 300 } as unknown as TradePlan));

    TestBed.configureTestingModule({
      imports: [DecisionQueueComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DecisionService, useValue: decisionSpy },
        { provide: DisciplineService, useValue: disciplineSpy },
        { provide: TradePlanService, useValue: planSpy },
      ],
    });
    fixture = TestBed.createComponent(DecisionQueueComponent);
    component = fixture.componentInstance;
  }

  // -----------------------------------------------------------------
  // Empty state
  // -----------------------------------------------------------------
  it('renders positive empty state with streak when no items and streak ≥ 1', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 7, hasData: true });
    fixture.detectChanges();

    const empty = fixture.debugElement.query(By.css('[data-test="decision-queue-empty"]'));
    expect(empty).toBeTruthy();
    expect(empty.nativeElement.textContent).toContain('Hôm nay đang kỷ luật');

    const streak = fixture.debugElement.query(By.css('[data-test="streak-badge"]'));
    expect(streak).toBeTruthy();
    expect(streak.nativeElement.textContent).toContain('7 ngày');
  });

  it('hides streak badge when daysWithoutViolation = 0', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 0, hasData: true });
    fixture.detectChanges();

    const empty = fixture.debugElement.query(By.css('[data-test="decision-queue-empty"]'));
    expect(empty).toBeTruthy();
    const streak = fixture.debugElement.query(By.css('[data-test="streak-badge"]'));
    expect(streak).toBeFalsy();
  });

  it('hides streak badge when hasData = false (new user)', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    const streak = fixture.debugElement.query(By.css('[data-test="streak-badge"]'));
    expect(streak).toBeFalsy();
  });

  // -----------------------------------------------------------------
  // Active queue
  // -----------------------------------------------------------------
  it('renders item cards with critical first', () => {
    const items: DecisionItemDto[] = [
      mockItem({ id: '1', severity: 'Warning', symbol: 'VNM', headline: 'VNM thesis review' }),
      mockItem({ id: '2', severity: 'Critical', symbol: 'FPT' }),
    ];
    // Server-side đã sort severity desc; widget chỉ render theo thứ tự nhận về.
    items.sort((a, b) => (a.severity === 'Critical' ? -1 : 1));
    setup({ items, totalCount: items.length }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    const cards = fixture.debugElement.queryAll(By.css('[data-test="decision-item"]'));
    expect(cards.length).toBe(2);
    expect(cards[0].nativeElement.textContent).toContain('FPT');

    const count = fixture.debugElement.query(By.css('[data-test="decision-queue-count"]'));
    expect(count.nativeElement.textContent.trim()).toBe('2');
  });

  it('caps visible items at 5 and shows overflow link', () => {
    const items = Array.from({ length: 8 }, (_, i) =>
      mockItem({ id: `i${i}`, symbol: `SYM${i}` })
    );
    setup({ items, totalCount: items.length }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    const cards = fixture.debugElement.queryAll(By.css('[data-test="decision-item"]'));
    expect(cards.length).toBe(5);

    const overflow = fixture.debugElement.query(By.css('[data-test="overflow-link"]'));
    expect(overflow).toBeTruthy();
    expect(overflow.nativeElement.textContent).toContain('5/8');
  });

  it('renders Vietnamese labels for severity and type', () => {
    setup(
      { items: [mockItem({ severity: 'Critical', type: 'StopLossHit' })], totalCount: 1 },
      { daysWithoutViolation: 0, hasData: false }
    );
    fixture.detectChanges();

    const card = fixture.debugElement.query(By.css('[data-test="decision-item"]'));
    expect(card.nativeElement.textContent).toContain('Khẩn cấp');
    expect(card.nativeElement.textContent).toContain('Stop-loss');
  });

  it('hides empty state when items present', () => {
    setup({ items: [mockItem()], totalCount: 1 }, { daysWithoutViolation: 5, hasData: true });
    fixture.detectChanges();

    const empty = fixture.debugElement.query(By.css('[data-test="decision-queue-empty"]'));
    expect(empty).toBeFalsy();
  });

  it('hides active queue block when items empty', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 3, hasData: true });
    fixture.detectChanges();

    const active = fixture.debugElement.query(By.css('[data-test="decision-queue-active"]'));
    expect(active).toBeFalsy();
  });

  // -----------------------------------------------------------------
  // Action route helper
  // -----------------------------------------------------------------
  it('routes StopLossHit to /risk-dashboard with symbol param', () => {
    setup({ items: [mockItem()], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    const route = component.getActionRoute(component.items[0]);
    const params = component.getActionParams(component.items[0]);
    expect(route).toEqual(['/risk-dashboard']);
    expect(params).toEqual({ symbol: 'FPT' });
  });

  it('routes ScenarioTrigger to /trade-plan with loadPlan param', () => {
    const item = mockItem({ type: 'ScenarioTrigger', tradePlanId: 'plan-1' });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    expect(component.getActionRoute(item)).toEqual(['/trade-plan']);
    expect(component.getActionParams(item)).toEqual({ loadPlan: 'plan-1' });
  });

  it('routes ThesisReviewDue to /symbol-timeline with symbol + planId when tradePlanId present', () => {
    const item = mockItem({ type: 'ThesisReviewDue', symbol: 'VNM', tradePlanId: 'plan-vnm' });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    expect(component.getActionRoute(item)).toEqual(['/symbol-timeline']);
    expect(component.getActionParams(item)).toEqual({ symbol: 'VNM', planId: 'plan-vnm' });
  });

  it('routes ThesisReviewDue to /symbol-timeline with symbol param even when tradePlanId missing', () => {
    // Phòng thủ, không phải regression: backend luôn set TradePlanId = plan.Id cho ThesisReviewDue.
    // Chốt luật "không nhánh nào được làm mất symbol" — mất symbol thì /symbol-timeline render rỗng.
    const item = mockItem({ type: 'ThesisReviewDue', symbol: 'FPT', tradePlanId: null });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    expect(component.getActionRoute(item)).toEqual(['/symbol-timeline']);
    expect(component.getActionParams(item)).toEqual({ symbol: 'FPT' });
  });

  it('Xử lý link rendered with data-test="btn-process" for E2E consistency', () => {
    const item = mockItem();
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('[data-test="btn-process"]'))).toBeTruthy();
  });

  // -----------------------------------------------------------------
  // P4 inline actions — BÁN / GIỮ
  // -----------------------------------------------------------------
  it('hides BÁN button when item has no tradePlanId (StopLossHit fallback)', () => {
    const item = mockItem({ tradePlanId: null });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('[data-test="btn-sell"]'))).toBeFalsy();
    expect(fixture.debugElement.query(By.css('[data-test="btn-hold"]'))).toBeTruthy();
  });

  it('BÁN điều hướng sang màn hình bán với form fill sẵn từ kế hoạch', (done) => {
    const item = mockItem({
      id: 'ScenarioTrigger:plan-x:n1', type: 'ScenarioTrigger', tradePlanId: 'plan-x',
      symbol: 'FPT', portfolioId: 'p1', currentPrice: 74_100,
    });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    planSpy.getById.and.returnValue(of({ quantity: 300 } as unknown as TradePlan));
    const router = TestBed.inject(Router);
    const nav = spyOn(router, 'navigate').and.resolveTo(true);
    fixture.detectChanges();

    fixture.debugElement.query(By.css('[data-test="btn-sell"]')).nativeElement.click();

    setTimeout(() => {
      expect(nav).toHaveBeenCalledWith(['/trades/create'], {
        queryParams: {
          symbol: 'FPT', portfolioId: 'p1', direction: 'Sell',
          planId: 'plan-x', price: 74_100, quantity: 300,
        },
      });
      done();
    }, 0);
  });

  it('BÁN không gọi resolve nữa — không ghi cờ dập cảnh báo khi chưa bán thật', (done) => {
    // Bản cũ POST ExecuteSell ngay lúc bấm: lệnh bán được tạo VÀ một journal Decision được ghi
    // làm cờ dập thẻ cho hết ngày VN. Xoá lệnh bán ở trang giao dịch không xoá cờ đó, nên
    // hành động đã hoàn tác mà cảnh báo vẫn mất. Không ghi gì lúc bấm thì không có gì phải dọn.
    const item = mockItem({ tradePlanId: 'plan-x' });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture.detectChanges();

    fixture.debugElement.query(By.css('[data-test="btn-sell"]')).nativeElement.click();

    setTimeout(() => {
      expect(decisionSpy.resolve).not.toHaveBeenCalled();
      expect(component.items.length).toBe(1);
      done();
    }, 0);
  });

  it('BÁN vẫn điều hướng khi không lấy được kế hoạch — chỉ thiếu ô số lượng', (done) => {
    const item = mockItem({ tradePlanId: 'plan-x', symbol: 'FPT', portfolioId: 'p1', currentPrice: 74_100 });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    planSpy.getById.and.returnValue(throwError(() => ({ status: 404 })));
    const nav = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture.detectChanges();

    fixture.debugElement.query(By.css('[data-test="btn-sell"]')).nativeElement.click();

    setTimeout(() => {
      expect(nav).toHaveBeenCalledWith(['/trades/create'], {
        queryParams: {
          symbol: 'FPT', portfolioId: 'p1', direction: 'Sell',
          planId: 'plan-x', price: 74_100,
        },
      });
      done();
    }, 0);
  });

  it('expands inline note form when GIỮ clicked', () => {
    const item = mockItem();
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    fixture.debugElement.query(By.css('[data-test="btn-hold"]')).nativeElement.click();
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.css('[data-test="note-textarea"]'))).toBeTruthy();
    // BÁN button hidden when note form expanded
    expect(fixture.debugElement.query(By.css('[data-test="btn-sell"]'))).toBeFalsy();
  });

  it('disables submit button when note shorter than 20 chars', () => {
    const item = mockItem();
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    component.expandNote(item);
    component.noteDraft = 'ngắn';
    fixture.detectChanges();

    const btn: HTMLButtonElement = fixture.debugElement.query(By.css('[data-test="btn-submit-hold"]')).nativeElement;
    expect(btn.disabled).toBeTrue();
  });

  it('shows error message at item-level when GIỮ API fails', () => {
    const item = mockItem({ id: 'i-err', symbol: 'FPT', tradePlanId: 'plan-1' });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    decisionSpy.resolve.and.returnValue(throwError(() => ({ error: { message: 'Plan đã bị xóa' } })));
    fixture.detectChanges();

    component.expandNote(component.items[0]);
    component.noteDraft = 'Giữ vì nền hỗ trợ vẫn còn nguyên, chưa vỡ';
    component.submitHold(component.items[0]);
    fixture.detectChanges();

    const err = fixture.debugElement.query(By.css('[data-test="resolve-error"]'));
    expect(err).toBeTruthy();
    expect(err.nativeElement.textContent).toContain('Plan đã bị xóa');
    // Item still in list (no optimistic remove on failure)
    expect(component.items.length).toBe(1);
  });

  it('removes item from list after successful resolve (optimistic)', () => {
    const items: DecisionItemDto[] = [
      mockItem({ id: 'i1', symbol: 'FPT', tradePlanId: 'plan-1' }),
      mockItem({ id: 'i2', symbol: 'VNM', tradePlanId: 'plan-2' }),
    ];
    setup({ items: [...items], totalCount: items.length }, { daysWithoutViolation: 0, hasData: false });
    decisionSpy.resolve.and.returnValue(of({ resultId: 'j1', message: 'OK', resultType: 'JournalEntry' }));
    fixture.detectChanges();

    component.expandNote(component.items[0]);
    component.noteDraft = 'Giữ vì nền hỗ trợ vẫn còn nguyên, chưa vỡ';
    component.submitHold(component.items[0]);
    fixture.detectChanges();

    expect(component.items.length).toBe(1);
    expect(component.items[0].id).toBe('i2');
  });

  // -----------------------------------------------------------------
  // Hai loại quyết định phía vào lệnh (BuyOpportunity / MissingStopLoss).
  // Bản cũ dùng chuỗi if kết bằng return mặc định nên type lạ bị dán nhãn
  // "Review thesis" và điều hướng về /symbol-timeline — sai mà không báo lỗi.
  // -----------------------------------------------------------------
  it('trả nhãn tiếng Việt cho mọi loại quyết định', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 0, hasData: false });

    expect(component.typeLabel('StopLossHit')).toBe('Stop-loss');
    expect(component.typeLabel('ScenarioTrigger')).toBe('Kịch bản');
    expect(component.typeLabel('ThesisReviewDue')).toBe('Review thesis');
    expect(component.typeLabel('BuyOpportunity')).toBe('Cơ hội mua');
    expect(component.typeLabel('MissingStopLoss')).toBe('Thiếu stop-loss');
  });

  it('điều hướng đúng màn cho hai loại mới', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 0, hasData: false });

    expect(component.getActionRoute(mockItem({ type: 'BuyOpportunity' }))).toEqual(['/watchlist']);
    expect(component.getActionRoute(mockItem({ type: 'MissingStopLoss' }))).toEqual(['/risk-dashboard']);
    expect(component.getActionParams(mockItem({ type: 'BuyOpportunity', symbol: 'VNM' }))).toEqual({ symbol: 'VNM' });
    expect(component.getActionParams(mockItem({ type: 'MissingStopLoss', symbol: 'MWG' }))).toEqual({ symbol: 'MWG' });
  });

  it('gửi kèm portfolioId khi GIỮ + ghi lý do', () => {
    // Thiếu portfolioId thì backend ghi journal không gắn danh mục, suppression mất phạm vi
    // và resolve một mã ở danh mục này sẽ giấu cảnh báo cùng mã ở danh mục khác.
    const item = mockItem({ id: 'i1', type: 'StopLossHit', portfolioId: 'p1', symbol: 'FPT', tradePlanId: null });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    decisionSpy.resolve.and.returnValue(of({ resultId: 'j1', message: 'OK', resultType: 'JournalEntry' }));
    fixture.detectChanges();

    component.expandNote(component.items[0]);
    component.noteDraft = 'Giữ vì thị trường chung đang hồi phục';
    component.submitHold(component.items[0]);

    expect(decisionSpy.resolve).toHaveBeenCalledWith('i1', jasmine.objectContaining({
      action: 'HoldWithJournal',
      symbol: 'FPT',
      portfolioId: 'p1',
    }));
  });

  it('không để trống nhãn/route khi gặp type lạ (FE cache cũ vs API mới)', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 0, hasData: false });
    const unknown = 'SomeFutureType' as DecisionType;

    expect(component.typeLabel(unknown)).toBe('Khác');
    expect(component.getActionRoute(mockItem({ type: unknown }))).toEqual(['/symbol-timeline']);
    expect(component.getActionParams(mockItem({ type: unknown, symbol: 'SSI' }))).toEqual({ symbol: 'SSI' });
  });

  it('không nhánh nào của getActionParams được làm mất symbol', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 0, hasData: false });

    // ScenarioTrigger thiếu plan: trước đây rơi vào `return {}` → /trade-plan trống trơn.
    expect(component.getActionParams(
      mockItem({ type: 'ScenarioTrigger', symbol: 'HPG', tradePlanId: null })
    )).toEqual({ symbol: 'HPG' });
  });

  it('ẩn nút BÁN cho cả hai loại mới (không có tradePlanId)', () => {
    setup({ items: [], totalCount: 0 }, { daysWithoutViolation: 0, hasData: false });

    expect(component.canExecuteSell(mockItem({ type: 'BuyOpportunity', tradePlanId: null }))).toBeFalse();
    expect(component.canExecuteSell(mockItem({ type: 'MissingStopLoss', tradePlanId: null }))).toBeFalse();
  });

  // -----------------------------------------------------------------
  // appSymbolLink — mẫu đại diện cho thẻ (card)
  // -----------------------------------------------------------------
  it('mã trong mỗi thẻ bấm được sang dòng thời gian', () => {
    // Quên đưa SymbolLinkDirective vào `imports` thì Angular BỎ QUA attribute
    // trong im lặng — mã trông y hệt, chỉ là không bấm được. Kiểm bằng DOM là
    // cách duy nhất bắt được ca đó; nhìn bằng mắt thì không.
    setup(
      { items: [mockItem({ symbol: 'HPG' }), mockItem({ id: 'x2', symbol: 'FPT' })], totalCount: 2 },
      { daysWithoutViolation: 0, hasData: true }
    );
    fixture.detectChanges();

    const links = fixture.debugElement.queryAll(By.css('[role="link"][title^="Xem dòng thời gian"]'));
    expect(links.length).toBe(2);
    expect(links.map(l => l.nativeElement.getAttribute('title')))
      .toEqual(['Xem dòng thời gian HPG', 'Xem dòng thời gian FPT']);
  });
  // -----------------------------------------------------------------
  // Dời SL ngay trên thẻ (ADR-0017)
  // -----------------------------------------------------------------
  describe('dời SL trên thẻ', () => {
    it('hiện nút dời SL khi item có tradePlanId và giá SL', () => {
      setup({ items: [mockItem({ tradePlanId: 'plan-1', plannedExitPrice: 89.5 })], totalCount: 1 },
        { daysWithoutViolation: 0, hasData: true });
      fixture.detectChanges();

      expect(fixture.debugElement.query(By.css('[data-test="btn-move-sl"]'))).toBeTruthy();
    });

    it('ẩn nút dời SL khi item không có tradePlanId', () => {
      setup({ items: [mockItem({ tradePlanId: null })], totalCount: 1 },
        { daysWithoutViolation: 0, hasData: true });
      fixture.detectChanges();

      expect(fixture.debugElement.query(By.css('[data-test="btn-move-sl"]'))).toBeNull();
    });

    it('ẩn nút dời SL trên thẻ MissingStopLoss — không có ngưỡng nào để dời', () => {
      setup({
        items: [mockItem({
          type: 'MissingStopLoss' as DecisionType, tradePlanId: null, plannedExitPrice: null
        })], totalCount: 1
      }, { daysWithoutViolation: 0, hasData: true });
      fixture.detectChanges();

      expect(fixture.debugElement.query(By.css('[data-test="btn-move-sl"]'))).toBeNull();
    });

    it('gửi đúng planId + giá mới rồi tải lại queue', () => {
      setup({ items: [mockItem({ tradePlanId: 'plan-1', plannedExitPrice: 89.5 })], totalCount: 1 },
        { daysWithoutViolation: 0, hasData: true });
      fixture.detectChanges();
      decisionSpy.getQueue.calls.reset();

      component.openMoveSl(component.items[0]);
      component.submitMoveSl({ newStopLoss: 92, reason: 'dời lên sau khi chốt 50%' });

      expect(planSpy.updateStopLoss).toHaveBeenCalledWith('plan-1', {
        newStopLoss: 92, reason: 'dời lên sau khi chốt 50%'
      });
      expect(decisionSpy.getQueue).toHaveBeenCalled();
      expect(component.slMoveItem).toBeNull();
    });

    it('lỗi API thì giữ modal mở và hiện lỗi theo item', () => {
      setup({ items: [mockItem({ tradePlanId: 'plan-1', plannedExitPrice: 89.5 })], totalCount: 1 },
        { daysWithoutViolation: 0, hasData: true });
      fixture.detectChanges();
      planSpy.updateStopLoss.and.returnValue(throwError(() => new Error('boom')));

      component.openMoveSl(component.items[0]);
      component.submitMoveSl({ newStopLoss: 92 });
      fixture.detectChanges();

      expect(component.slMoveItem).not.toBeNull();
      expect(component.errorFor('StopLossHit:p1:FPT')).toBeTruthy();
    });
  });
});
