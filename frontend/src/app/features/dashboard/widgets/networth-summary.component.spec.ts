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

  it('the root is a div (not a link) and the header "Xem chi tiết →" is the only link, to /personal-finance', () => {
    component.summary = SUMMARY_WITH_PROFILE;
    fixture.detectChanges();

    const root = fixture.debugElement.query(By.css('[data-test="networth-root"]'));
    expect(root.nativeElement.tagName).toBe('DIV');
    const links = fixture.debugElement.queryAll(By.css('[routerLink], a'));
    expect(links.length).toBe(1);
    expect(links[0].nativeElement.getAttribute('href')).toBe('/personal-finance');
    expect(links[0].nativeElement.textContent).toContain('Xem chi tiết');
  });

  const show = (over: Partial<NetWorthSummaryDto> = {}) => {
    component.summary = { ...SUMMARY_WITH_PROFILE, ...over };
    fixture.detectChanges();
  };
  const q = (id: string) => fixture.debugElement.query(By.css(`[data-test="${id}"]`));

  it('title is "Tài chính cá nhân" and the big number is labelled Net Worth', () => {
    show();

    expect(fixture.nativeElement.textContent).toContain('Tài chính cá nhân');
    expect(q('networth-label').nativeElement.textContent).toContain('Net Worth');
  });

  it('shows "Tổng tài sản X − Nợ Y" and hides the debt part when debt is 0', () => {
    show({ totalDebt: 40_000_000 });
    expect(q('assets-debt-line').nativeElement.textContent).toContain('Tổng tài sản');
    expect(q('assets-debt-line').nativeElement.textContent).toContain('− Nợ');

    show({ totalDebt: 0 });
    expect(q('assets-debt-line').nativeElement.textContent).toContain('Tổng tài sản');
    expect(q('assets-debt-line').nativeElement.textContent).not.toContain('Nợ');
  });

  it('negative Net Worth is red, non-negative is green', () => {
    show({ netWorth: -5_000_000 });
    expect(q('networth-value').nativeElement.classList).toContain('text-red-700');

    show({ netWorth: 10_000_000 });
    expect(q('networth-value').nativeElement.classList).toContain('text-emerald-700');
  });

  it('shows the high-interest consumer-debt warning only when flagged', () => {
    show({ hasHighInterestConsumerDebt: true });
    expect(q('high-interest-warning')).toBeTruthy();

    show({ hasHighInterestConsumerDebt: false });
    expect(q('high-interest-warning')).toBeFalsy();
  });

  it('health bar colour follows the score: 49 red, 50 amber, 79 amber, 80 emerald; width tracks the score, 0 is 0', () => {
    const bar = () => q('health-bar').nativeElement as HTMLElement;
    const colourAt = (score: number) => { show({ healthScore: score }); return Array.from(bar().classList).find(c => c.startsWith('bg-') && c !== 'bg-gray-200'); };

    expect(colourAt(49)).toBe('bg-red-500');
    expect(colourAt(50)).toBe('bg-amber-500');
    expect(colourAt(79)).toBe('bg-amber-500');
    expect(colourAt(80)).toBe('bg-emerald-500');
    show({ healthScore: 0 });
    expect(bar().style.width).toBe('0%');
    show({ healthScore: 85 });
    expect(bar().style.width).toBe('85%');
    expect(q('health-score').nativeElement.textContent).toContain('85/100');
  });

  it('the health score text is dark enough on white in light mode (amber-700, not amber-600) and keeps the bar colour', () => {
    show({ healthScore: 73 });

    const text = q('health-score').nativeElement as HTMLElement;
    expect(text.classList).toContain('text-amber-700');
    expect(text.classList).not.toContain('text-amber-600');
    expect(q('health-bar').nativeElement.classList).toContain('bg-amber-500');
  });

  it('the Chi tiết toggle has a hit area of at least 32px (padding + min-height) even though its text is small', () => {
    show();

    const toggle = q('breakdown-toggle').nativeElement as HTMLButtonElement;
    expect(toggle.classList).toContain('min-h-[32px]');
    expect(toggle.classList).toContain('py-2');
  });

  it('the breakdown is collapsed by default and the toggle shows and hides it without navigating', () => {
    show();
    expect(q('breakdown')).toBeFalsy();
    const toggle = q('breakdown-toggle').nativeElement as HTMLButtonElement;
    expect(toggle.getAttribute('type')).toBe('button');
    expect(toggle.textContent).toContain('Chi tiết ▾');

    toggle.click();
    fixture.detectChanges();
    expect(q('breakdown')).toBeTruthy();
    expect(q('breakdown-toggle').nativeElement.textContent).toContain('Chi tiết ▴');
    expect(fixture.debugElement.queryAll(By.css('a')).length).toBe(1);   // vẫn chỉ có link ở đầu thẻ

    (q('breakdown-toggle').nativeElement as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(q('breakdown')).toBeFalsy();
  });

  it('the breakdown lists CK, TK, DP, NR and hides Vàng when gold is 0', () => {
    show({ goldTotal: 0 });
    (q('breakdown-toggle').nativeElement as HTMLButtonElement).click();
    fixture.detectChanges();

    const text = q('breakdown').nativeElement.textContent;
    ['CK', 'TK', 'DP', 'NR'].forEach(k => expect(text).toContain(k));
    expect(text).not.toContain('Vàng');

    show({ goldTotal: 50_000_000 });
    expect(q('breakdown').nativeElement.textContent).toContain('Vàng');
  });

  it('keeps the CAGR gap line in the same card', () => {
    component.cagrValue = 11;
    component.cagrTarget = 15;
    show();

    expect(q('networth-root').nativeElement.contains(q('cagr-gap').nativeElement)).toBeTrue();
  });

  it('without a profile the top slot shows the onboarding card instead (one link to /personal-finance)', () => {
    component.summary = SUMMARY_NO_PROFILE;
    fixture.detectChanges();

    const onboarding = q('networth-onboarding');
    expect(onboarding).toBeTruthy();
    expect(onboarding.nativeElement.textContent).toContain('Thiết lập tài chính cá nhân');
    expect(onboarding.nativeElement.getAttribute('href')).toBe('/personal-finance');
    expect(q('networth-root')).toBeFalsy();
  });

  it('with a null summary (still loading) nothing is shown', () => {
    component.summary = null;
    fixture.detectChanges();

    expect(q('networth-onboarding')).toBeFalsy();
    expect(q('networth-root')).toBeFalsy();
  });
});
