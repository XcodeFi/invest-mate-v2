import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ExpectancyRBlockComponent } from './expectancy-r-block.component';
import {
  ExpectancyStatsDto, StrategyExpectancyDto, TradePlanService
} from '../../../core/services/trade-plan.service';

const stats = (over: Partial<ExpectancyStatsDto> = {}): ExpectancyStatsDto => ({
  strategyId: 's1', strategyName: 'Breakout', n: 12, wins: 6, losses: 4, draws: 2,
  winRate: 0.6, winRateLow95: 0.3, winRateHigh95: 0.83,
  avgWinR: 2, avgLossR: -1, expectancyR: 0.67, profitFactorR: 3, ...over,
});

describe('ExpectancyRBlockComponent', () => {
  let fixture: ComponentFixture<ExpectancyRBlockComponent>;
  let component: ExpectancyRBlockComponent;
  let planSpy: jasmine.SpyObj<TradePlanService>;

  const text = (): string => fixture.nativeElement.textContent;
  const rows = () => fixture.nativeElement.querySelectorAll('[data-testid="expectancy-row"]');

  function setup(result: StrategyExpectancyDto | 'error') {
    planSpy = jasmine.createSpyObj<TradePlanService>('TradePlanService', ['getExpectancy']);
    planSpy.getExpectancy.and.returnValue(result === 'error' ? throwError(() => new Error('500')) : of(result));
    TestBed.configureTestingModule({
      imports: [ExpectancyRBlockComponent],
      providers: [{ provide: TradePlanService, useValue: planSpy }],
    });
    fixture = TestBed.createComponent(ExpectancyRBlockComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('shows n and the 95% interval next to the win rate', () => {
    setup({ total: stats({ strategyId: null, strategyName: 'Tất cả' }), strategies: [stats()] });

    expect(text()).toContain('Breakout');
    expect(text()).toContain('n = 12');
    expect(text()).toMatch(/60%/);
    expect(text()).toMatch(/30%\s*[–-]\s*83%/);
  });

  it("n<10 shows 'chưa đủ mẫu để tin'", () => {
    setup({ total: stats({ n: 4, strategyId: null, strategyName: 'Tất cả' }), strategies: [stats({ n: 4 })] });

    expect(text()).toContain('chưa đủ mẫu để tin');
  });

  it('n>=10 does not show the small-sample warning', () => {
    setup({ total: stats({ n: 25, strategyId: null, strategyName: 'Tất cả' }), strategies: [stats({ n: 25 })] });

    expect(text()).not.toContain('chưa đủ mẫu để tin');
  });

  it('missing values show n/a, never 0', () => {
    setup({
      total: stats({ n: 2, strategyId: null, strategyName: 'Tất cả' }),
      strategies: [stats({ n: 2, winRate: null, winRateLow95: null, winRateHigh95: null,
        avgWinR: null, avgLossR: null, expectancyR: null, profitFactorR: null })],
    });

    const row = rows()[0].textContent as string;
    expect((row.match(/n\/a/g) ?? []).length).toBeGreaterThanOrEqual(5);
    expect(row).not.toContain('0.00R');
  });

  it('profit factor without losses shows n/a (not infinity or 999)', () => {
    setup({ total: stats({ n: 12, strategyId: null, strategyName: 'Tất cả' }),
      strategies: [stats({ losses: 0, avgLossR: null, profitFactorR: null })] });

    expect(rows()[0].textContent).toContain('n/a');
    expect(text()).not.toContain('999');
  });

  it('shows draws separately', () => {
    setup({ total: stats({ strategyId: null, strategyName: 'Tất cả' }), strategies: [stats()] });

    expect(rows()[0].textContent).toContain('6 thắng');
    expect(rows()[0].textContent).toContain('4 thua');
    expect(rows()[0].textContent).toContain('2 hoà');
  });

  it('empty result shows a clear empty state', () => {
    setup({ total: stats({ n: 0, strategyId: null, strategyName: 'Tất cả', winRate: null, expectancyR: null }), strategies: [] });

    expect(text()).toContain('Chưa có chiến dịch đã đóng nào tính được R');
    expect(rows().length).toBe(0);
  });

  it('error shows a message and keeps the page usable', () => {
    setup('error');

    expect(text()).toContain('Không tải được thống kê kỳ vọng');
  });

  it('labels the block as closed campaigns only and Buy plans only', () => {
    setup({ total: stats({ strategyId: null, strategyName: 'Tất cả' }), strategies: [stats()] });

    expect(text()).toContain('đã đóng');
    expect(text()).toContain('lệnh Mua');
  });

  it('formatR and formatPct keep sign and n/a conventions', () => {
    setup({ total: stats({ strategyId: null, strategyName: 'Tất cả' }), strategies: [] });

    expect(component.formatR(0.67)).toBe('+0.67R');
    expect(component.formatR(-1)).toBe('-1.00R');
    expect(component.formatR(null)).toBe('n/a');
    expect(component.formatPct(0.6)).toBe('60%');
    expect(component.formatPct(null)).toBe('n/a');
  });
});
