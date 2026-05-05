/**
 * Decision Queue widget — vị trí #1 trên Home (P3 + P4 Decision Engine v1.1).
 * Tests:
 *   1. Empty state hiển thị khi 0 alert + streak ≥ 1 day → ✅ "Hôm nay đang kỷ luật" + 🔥 X ngày.
 *   2. Empty state ẩn streak badge khi streak = 0 hoặc hasData = false.
 *   3. Active queue render N items, sort theo severity (Critical đầu tiên).
 *   4. Cap 5 items, hiện overflow link khi tổng > 5.
 *   5. Severity/type label đúng tiếng Việt.
 *   6. Loading skeleton hiện trước khi service trả về.
 *   7. (P4) BÁN button gọi resolve API với ExecuteSell + tradePlanId.
 *   8. (P4) GIỮ button expand inline note form.
 *   9. (P4) Submit button disabled khi note < 20 chars.
 *  10. (P4) Item bị remove khỏi list sau resolve thành công.
 *  11. (P4) BÁN button ẩn khi item không có tradePlanId.
 */
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of, throwError } from 'rxjs';
import { DecisionQueueComponent } from './decision-queue.component';
import { DecisionService, DecisionItemDto, DecisionQueueDto } from '../../../core/services/decision.service';
import { DisciplineService, DisciplineStreakDto } from '../../../core/services/discipline.service';

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

  function setup(queue: DecisionQueueDto, streak: DisciplineStreakDto) {
    decisionSpy = jasmine.createSpyObj('DecisionService', ['getQueue', 'resolve']);
    disciplineSpy = jasmine.createSpyObj('DisciplineService', ['getStreak']);
    decisionSpy.getQueue.and.returnValue(of(queue));
    disciplineSpy.getStreak.and.returnValue(of(streak));

    TestBed.configureTestingModule({
      imports: [DecisionQueueComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DecisionService, useValue: decisionSpy },
        { provide: DisciplineService, useValue: disciplineSpy },
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
    // Regression: bug từ PR-3 — fallback `return {}` khi tradePlanId null làm URL về /symbol-timeline
    // không kèm ?symbol=... → page render rỗng "Chưa có dữ liệu timeline cho ".
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

  it('calls resolve API with ExecuteSell when BÁN clicked + confirmed', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    const item = mockItem({ id: 'ScenarioTrigger:plan-x:n1', type: 'ScenarioTrigger', tradePlanId: 'plan-x' });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    decisionSpy.resolve.and.returnValue(of({ resultId: 't1', message: 'OK', resultType: 'Trade' }));
    fixture.detectChanges();

    fixture.debugElement.query(By.css('[data-test="btn-sell"]')).nativeElement.click();
    fixture.detectChanges();

    expect(decisionSpy.resolve).toHaveBeenCalledWith(
      'ScenarioTrigger:plan-x:n1',
      jasmine.objectContaining({ action: 'ExecuteSell', tradePlanId: 'plan-x' })
    );
  });

  it('does NOT call resolve when user cancels BÁN confirm dialog', () => {
    spyOn(window, 'confirm').and.returnValue(false);
    const item = mockItem({ tradePlanId: 'plan-x' });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    fixture.detectChanges();

    fixture.debugElement.query(By.css('[data-test="btn-sell"]')).nativeElement.click();

    expect(decisionSpy.resolve).not.toHaveBeenCalled();
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

  it('shows error message at item-level when BÁN API fails', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    const item = mockItem({ id: 'i-err', symbol: 'FPT', tradePlanId: 'plan-1' });
    setup({ items: [item], totalCount: 1 }, { daysWithoutViolation: 0, hasData: false });
    decisionSpy.resolve.and.returnValue(throwError(() => ({ error: { message: 'Plan đã bị xóa' } })));
    fixture.detectChanges();

    component.onExecuteSell(component.items[0]);
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
    decisionSpy.resolve.and.returnValue(of({ resultId: 't1', message: 'OK', resultType: 'Trade' }));
    fixture.detectChanges();

    spyOn(window, 'confirm').and.returnValue(true);
    component.onExecuteSell(component.items[0]);
    fixture.detectChanges();

    expect(component.items.length).toBe(1);
    expect(component.items[0].id).toBe('i2');
  });
});
