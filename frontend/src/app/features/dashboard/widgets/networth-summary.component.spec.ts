/**
 * NetWorth Summary widget — compact 3-line block ở vị trí #2 trên Home.
 * Hiển thị: Net Worth (assets - debt) + reality gap so với mục tiêu CAGR.
 *
 * Mục đích: thay thế phần CAGR target progress nằm trong Compound Growth Tracker
 * cũ (vị trí giữa page) bằng widget ngắn ở top, giúp user thấy gap ngay khi mở app.
 */
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { NetWorthSummaryComponent } from './networth-summary.component';
import { NetWorthSummaryDto } from '../../../core/services/personal-finance.service';

const SUMMARY_WITH_PROFILE: NetWorthSummaryDto = {
  hasProfile: true,
  totalAssets: 535_000_000,
  securitiesValue: 350_000_000,
  goldTotal: 50_000_000,
  savingsTotal: 100_000_000,
  emergencyTotal: 30_000_000,
  idleCashTotal: 5_000_000,
  totalDebt: 0,
  netWorth: 535_000_000,
  hasHighInterestConsumerDebt: false,
  monthlyExpense: 15_000_000,
  healthScore: 85,
  ruleChecks: [],
  accounts: [],
  debts: [],
};

const SUMMARY_NO_PROFILE: NetWorthSummaryDto = {
  ...SUMMARY_WITH_PROFILE,
  hasProfile: false,
  netWorth: 0,
  totalAssets: 0,
};

describe('NetWorthSummaryComponent', () => {
  let fixture: ComponentFixture<NetWorthSummaryComponent>;
  let component: NetWorthSummaryComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [NetWorthSummaryComponent],
      providers: [provideRouter([])],
    });
    fixture = TestBed.createComponent(NetWorthSummaryComponent);
    component = fixture.componentInstance;
  });

  it('renders Net Worth value when summary.hasProfile = true', () => {
    component.summary = SUMMARY_WITH_PROFILE;
    component.cagrValue = 11;
    component.cagrTarget = 15;
    fixture.detectChanges();

    const root = fixture.debugElement.query(By.css('[data-test="networth-root"]'));
    expect(root).toBeTruthy();

    const value = fixture.debugElement.query(By.css('[data-test="networth-value"]'));
    // VndCurrencyPipe formats with non-breaking spaces — assert digit string survives.
    expect(value.nativeElement.textContent).toContain('535');
  });

  it('hides widget when summary.hasProfile = false', () => {
    component.summary = SUMMARY_NO_PROFILE;
    fixture.detectChanges();

    const root = fixture.debugElement.query(By.css('[data-test="networth-root"]'));
    expect(root).toBeFalsy();
  });

  it('hides widget when summary is null', () => {
    component.summary = null;
    fixture.detectChanges();

    const root = fixture.debugElement.query(By.css('[data-test="networth-root"]'));
    expect(root).toBeFalsy();
  });

  it('shows red reality gap label when cagrValue is behind target', () => {
    component.summary = SUMMARY_WITH_PROFILE;
    component.cagrValue = 11;   // gap = 11 - 15 = -4 → behind by 4 percentage points
    component.cagrTarget = 15;
    fixture.detectChanges();

    const gap = fixture.debugElement.query(By.css('[data-test="cagr-gap"]'));
    expect(gap).toBeTruthy();
    expect(gap.nativeElement.textContent).toContain('Lệch');
    expect(gap.nativeElement.textContent).toContain('mục tiêu');
    expect(gap.nativeElement.classList).toContain('text-red-600');
  });

  it('does NOT show gap label when cagrValue meets or exceeds target', () => {
    component.summary = SUMMARY_WITH_PROFILE;
    component.cagrValue = 18;
    component.cagrTarget = 15;
    fixture.detectChanges();

    const gap = fixture.debugElement.query(By.css('[data-test="cagr-gap"]'));
    expect(gap).toBeFalsy();
  });

  it('does NOT show gap label when cagrValue is 0 (no CAGR data yet)', () => {
    component.summary = SUMMARY_WITH_PROFILE;
    component.cagrValue = 0;
    component.cagrTarget = 15;
    fixture.detectChanges();

    const gap = fixture.debugElement.query(By.css('[data-test="cagr-gap"]'));
    expect(gap).toBeFalsy();
  });

  it('does NOT show gap label at boundary cagrValue === cagrTarget', () => {
    // Boundary case: exactly at target → not behind, no gap label
    component.summary = SUMMARY_WITH_PROFILE;
    component.cagrValue = 15;
    component.cagrTarget = 15;
    fixture.detectChanges();

    const gap = fixture.debugElement.query(By.css('[data-test="cagr-gap"]'));
    expect(gap).toBeFalsy();
  });

  it('shows gap label with full magnitude when cagrValue is negative', () => {
    // Negative CAGR (lỗ thực): hiển thị gap = target - (-5) = 20 điểm %
    component.summary = SUMMARY_WITH_PROFILE;
    component.cagrValue = -5;
    component.cagrTarget = 15;
    fixture.detectChanges();

    const gap = fixture.debugElement.query(By.css('[data-test="cagr-gap"]'));
    expect(gap).toBeTruthy();
    expect(gap.nativeElement.textContent).toContain('20.0');
  });

  it('renders link to /personal-finance', () => {
    component.summary = SUMMARY_WITH_PROFILE;
    fixture.detectChanges();

    const link = fixture.debugElement.query(By.css('a[data-test="networth-root"]'));
    expect(link.attributes['ng-reflect-router-link']).toBe('/personal-finance');
  });
});
