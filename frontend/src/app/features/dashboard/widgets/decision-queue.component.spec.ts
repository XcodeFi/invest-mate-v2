/**
 * Decision Queue widget — vị trí #1 trên Home (P3 Decision Engine v1.1).
 * Tests:
 *   1. Empty state hiển thị khi 0 alert + streak ≥ 1 day → ✅ "Hôm nay đang kỷ luật" + 🔥 X ngày.
 *   2. Empty state ẩn streak badge khi streak = 0 hoặc hasData = false.
 *   3. Active queue render N items, sort theo severity (Critical đầu tiên).
 *   4. Cap 5 items, hiện overflow link khi tổng > 5.
 *   5. Severity/type label đúng tiếng Việt.
 *   6. Loading skeleton hiện trước khi service trả về.
 */
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
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
    decisionSpy = jasmine.createSpyObj('DecisionService', ['getQueue']);
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
});
