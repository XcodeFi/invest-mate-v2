import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { FundamentalsPanelComponent } from './fundamentals-panel.component';
import { CompanyDossierDetailComponent } from './company-dossier-detail.component';

/** Bố cục thật của trang hồ sơ ở 400px: biểu đồ rộng 560px phải cuộn BÊN TRONG khung của nó, không đẩy cả trang rộng ra. */
describe('CompanyDossierDetailComponent: khối số liệu doanh nghiệp không làm tràn trang ở 400px', () => {
  let fixture: ComponentFixture<CompanyDossierDetailComponent>;

  const quarter = (n: number, year: number) => ({
    period: `Q${n}/${year}`, year, quarter: n, revenue: 13788.5, revenueSameQuarterLastYear: 16624.7, revenueYoyPct: -17.1, revenueDirection: 'down',
    netProfit: 2570.3, netProfitSameQuarterLastYear: 2740.3, netProfitYoyPct: null, netProfitDirection: 'down', netProfitNote: 'từ lãi sang lỗ',
    netMargin: 18.6, netMarginSameQuarterLastYear: 16.5, netMarginChangePp: 2.2,
  });

  const render = (width: number) => {
    fixture = TestBed.createComponent(CompanyDossierDetailComponent);
    const host = fixture.nativeElement as HTMLElement;
    host.style.display = 'block';
    host.style.width = `${width}px`;
    host.style.overflowX = 'auto';
    document.body.appendChild(host);
    fixture.componentInstance.symbol = 'FPT';
    fixture.detectChanges();
    fixture.componentInstance.loading = false;
    fixture.componentInstance.mode = 'view';
    fixture.detectChanges();
    const panel = fixture.debugElement.query(By.directive(FundamentalsPanelComponent)).componentInstance as FundamentalsPanelComponent;
    panel.data = {
      symbol: 'FPT', company: null, indicators: { pe: 12, min52W: 43.9, max52W: 68.2 }, peers: [], dividendEvents: [], businessPlan: null, unavailableSections: [],
      incomeStatements: [{ period: 'Q2/2026', revenue: 1, netProfit: 1, grossProfit: null }],
      trend: {
        quarters: [quarter(2, 2026), quarter(1, 2026), quarter(4, 2025), quarter(3, 2025), quarter(2, 2025), quarter(1, 2025), quarter(4, 2024), quarter(3, 2024)],
        ttm: null, annual: [], range52W: null, note: 'Doanh nghiệp báo cáo theo quý; không có số liệu tháng',
      },
    } as any;
    fixture.detectChanges();
    return host;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyDossierDetailComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });
  afterEach(() => fixture?.destroy());

  it('at_400px_the_chart_scrolls_inside_its_frame_and_the_page_does_not_widen', () => {
    const host = render(400);

    const frame = host.querySelector('app-quarter-bars [data-testid="chart-body"]') as HTMLElement;
    expect(frame).withContext('khung biểu đồ quý').not.toBeNull();
    expect(host.scrollWidth).withContext('trang không rộng ra').toBeLessThanOrEqual(host.clientWidth);
    expect(frame.getBoundingClientRect().right).withContext('khung nằm trong 400px').toBeLessThanOrEqual(host.getBoundingClientRect().right + 0.5);
    expect(frame.scrollWidth).withContext('biểu đồ giữ cỡ gốc').toBeGreaterThan(frame.clientWidth);
  });
});
