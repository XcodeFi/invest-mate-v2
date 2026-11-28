import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CampaignAnalyticsComponent } from './campaign-analytics.component';
import { CampaignAnalyticsDto, TradePlanService } from '../../core/services/trade-plan.service';

describe('CampaignAnalyticsComponent — review còn mở không kéo tỉ lệ thắng', () => {
  let fixture: ComponentFixture<CampaignAnalyticsComponent>;
  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);
  const txt = (id: string): string => (q(id)?.textContent ?? '').replace(/\s+/g, ' ').trim();

  function analytics(over: Partial<CampaignAnalyticsDto>): CampaignAnalyticsDto {
    return {
      totalCampaigns: 4, winningCampaigns: 1, losingCampaigns: 1, excludedOpenReviews: 0,
      winRate: 50, averagePnLPercent: 2.5, averagePnLPerDay: 25_000, totalAccumulatedPnL: 800_000, averageHoldingDays: 10,
      trend: [], ...over
    };
  }

  function render(a: CampaignAnalyticsDto) {
    const svc = jasmine.createSpyObj<TradePlanService>('TradePlanService', ['getCampaignAnalytics', 'getPendingReview', 'getAll']);
    svc.getCampaignAnalytics.and.returnValue(of(a));
    svc.getPendingReview.and.returnValue(of([]));
    svc.getAll.and.returnValue(of([]));
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [CampaignAnalyticsComponent],
      providers: [provideRouter([]), { provide: TradePlanService, useValue: svc }]
    });
    fixture = TestBed.createComponent(CampaignAnalyticsComponent);
    fixture.detectChanges();
  }

  it('shows one line with the number of excluded open reviews when it is above 0', () => {
    render(analytics({ excludedOpenReviews: 2 }));

    expect(txt('excluded-open-line')).toBe('2 chiến dịch còn mở/thiếu lệnh mua không tính vào tỉ lệ thắng.');
  });

  it('shows no such line when nothing is excluded, or for an old payload without the field', () => {
    render(analytics({ excludedOpenReviews: 0 }));
    expect(q('excluded-open-line')).toBeNull();

    render(analytics({ excludedOpenReviews: undefined }));
    expect(q('excluded-open-line')).toBeNull();
  });

  it('renders the win rate and the average when there are closed reviews', () => {
    render(analytics({ winRate: 50, averagePnLPercent: 2.5 }));

    expect(txt('win-rate')).toContain('50.0%');
    expect(txt('avg-pnl')).toContain('+2.50%');
  });

  it('shows n/a, not 0%, when every review is still open', () => {
    render(analytics({ totalCampaigns: 3, winningCampaigns: 0, losingCampaigns: 0, excludedOpenReviews: 3, winRate: null, averagePnLPercent: null, averagePnLPerDay: null }));

    expect(txt('win-rate')).toContain('n/a');
    expect(txt('win-rate')).not.toContain('0.0%');
    expect(txt('avg-pnl')).toContain('n/a');
    expect(txt('excluded-open-line')).toContain('3 chiến dịch');
  });
});
