/**
 * Regression test for the dashboard freeze caused by `componentBars()` returning
 * a fresh array on every change-detection cycle.
 *
 * Origin: 2026-04-30 audit. truongpham3491@gmail.com (Admin, 2 active portfolios)
 * reported "browser not responding" on /dashboard. Bisect via Playwright pinned
 * the freeze to the *ngFor over `componentBars()` in this widget — combined
 * with ~20 concurrent subscriptions resolving on the same page, the new array
 * reference per CD pass triggered enough work to saturate the main thread.
 *
 * Fix: precompute `bars` as a stable field set inside `load()`, plus `trackBy`
 * on the *ngFor. These tests lock that contract: bars stays the same reference
 * unless the underlying score actually changes.
 */
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { DisciplineScoreWidgetComponent } from './discipline-score-widget.component';
import { DisciplineService, DisciplineScoreDto } from '../../../core/services/discipline.service';

const SCORE_NULL: DisciplineScoreDto = {
  overall: null,
  label: 'Chưa đủ dữ liệu',
  components: { slIntegrity: null, planQuality: null, reviewTimeliness: null },
  primitives: { stopHonorRate: { value: -1, hit: 0, total: 0 } },
  sampleSize: { totalPlans: 3, closedLossTrades: 0, daysObserved: 90 },
  generatedAt: '2026-04-30T00:00:00Z',
};

const SCORE_FILLED: DisciplineScoreDto = {
  ...SCORE_NULL,
  overall: 75,
  components: { slIntegrity: 80, planQuality: 70, reviewTimeliness: 75 },
};

describe('DisciplineScoreWidgetComponent', () => {
  let serviceSpy: jasmine.SpyObj<DisciplineService>;

  beforeEach(() => {
    serviceSpy = jasmine.createSpyObj('DisciplineService', ['getScore', 'getPendingReviews']);
    serviceSpy.getPendingReviews.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [DisciplineScoreWidgetComponent],
      providers: [
        { provide: DisciplineService, useValue: serviceSpy },
        provideRouter([]),
      ],
    });
  });

  it('initializes bars as empty array (no template fallback to undefined)', () => {
    serviceSpy.getScore.and.returnValue(of(SCORE_NULL));
    const fixture = TestBed.createComponent(DisciplineScoreWidgetComponent);
    const c = fixture.componentInstance;
    expect(c.bars).toEqual([]);
  });

  it('populates bars from score components after load', () => {
    serviceSpy.getScore.and.returnValue(of(SCORE_FILLED));
    const fixture = TestBed.createComponent(DisciplineScoreWidgetComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    expect(c.bars.length).toBe(3);
    expect(c.bars[0]).toEqual({ label: 'Giữ SL đúng kế hoạch', value: 80, weight: 50 });
    expect(c.bars[1]).toEqual({ label: 'Plan đủ kỷ luật', value: 70, weight: 30 });
    expect(c.bars[2]).toEqual({ label: 'Review lý do đầu tư đúng hạn', value: 75, weight: 20 });
  });

  it('keeps null components in bars (UI shows dash, no NaN math)', () => {
    serviceSpy.getScore.and.returnValue(of(SCORE_NULL));
    const fixture = TestBed.createComponent(DisciplineScoreWidgetComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    expect(c.bars.map(b => b.value)).toEqual([null, null, null]);
  });

  it('does NOT recompute bars on each detectChanges (regression: CD storm)', () => {
    serviceSpy.getScore.and.returnValue(of(SCORE_FILLED));
    const fixture = TestBed.createComponent(DisciplineScoreWidgetComponent);
    fixture.detectChanges();
    const c = fixture.componentInstance;

    const ref1 = c.bars;
    fixture.detectChanges();
    fixture.detectChanges();
    fixture.detectChanges();
    const ref2 = c.bars;

    // Must be the SAME reference — otherwise we're back to the freeze condition.
    expect(ref2).toBe(ref1);
  });

  it('trackByLabel returns the label so *ngFor identifies rows by name not index', () => {
    const c = TestBed.createComponent(DisciplineScoreWidgetComponent).componentInstance;
    expect(c.trackByLabel(0, { label: 'foo' })).toBe('foo');
  });
});
