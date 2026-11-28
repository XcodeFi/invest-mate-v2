import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { MarketDataComponent } from './market-data.component';
import { MarketDataService, TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { CompanyDossierService } from '../../core/services/company-dossier.service';
import { AuthService } from '../../core/services/auth.service';
import { INDICATOR_GUIDES } from './indicator-guides';

const N = 40;
const flat = (v: number): number[] => Array.from({ length: N }, () => v);

// undefined = backend cũ chưa có trường; true/false/null như API
let adjustedFlag: boolean | null | undefined;

const series = (): TechnicalSeries => ({
  ...(adjustedFlag === undefined ? {} : { adjusted: adjustedFlag }),
  symbol: 'HPG', dates: Array.from({ length: N }, (_, i) => `2026-01-${String((i % 28) + 1).padStart(2, '0')}`),
  open: flat(100), high: flat(101), low: flat(99), close: flat(100), volume: flat(1000),
  ema20: flat(105), ema50: flat(100), bollingerUpper: flat(110), bollingerMiddle: flat(100), bollingerLower: flat(90),
  rsi14: flat(72), stochK: flat(50), stochD: flat(50), macdLine: flat(2), macdSignal: flat(1), macdHistogram: flat(1),
  adx: flat(18), plusDi: flat(20), minusDi: flat(15), atr14: flat(2), obv: flat(1000), mfi14: flat(50), volumeSma20: flat(1000),
});

const analysis = (): TechnicalAnalysis => ({
  symbol: 'HPG', currentPrice: 100, supportLevels: [95], resistanceLevels: [105],
  suggestedEntry: 100, suggestedStopLoss: 95, suggestedTarget: 110, confluenceScore: 68.5,
  overallSignal: 'buy', overallSignalVi: 'MUA',
} as unknown as TechnicalAnalysis);

describe('MarketDataComponent — khối "Cách đọc" của nhóm đang chọn', () => {
  let fixture: ComponentFixture<MarketDataComponent>;

  function setup() {
    const marketSpy = jasmine.createSpyObj<MarketDataService>('MarketDataService', [
      'getMarketOverview', 'getBatchPrices', 'getTopFluctuation', 'getTechnicalSeries', 'getTechnicalAnalysis',
    ]);
    marketSpy.getMarketOverview.and.returnValue(of([]));
    marketSpy.getBatchPrices.and.returnValue(of([]));
    marketSpy.getTopFluctuation.and.returnValue(of([]));
    marketSpy.getTechnicalSeries.and.returnValue(of(series()));
    marketSpy.getTechnicalAnalysis.and.returnValue(of(analysis()));
    TestBed.configureTestingModule({
      imports: [MarketDataComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: MarketDataService, useValue: marketSpy },
        { provide: NotificationService, useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']) },
        { provide: CompanyDossierService, useValue: jasmine.createSpyObj<CompanyDossierService>('CompanyDossierService', ['get']) },
        // AuthService thật đọc localStorage không bọc try/catch khi khởi tạo; giả để test chỉ kiểm khối Cách đọc
        { provide: AuthService, useValue: { getToken: () => null } },
      ],
    });
    fixture = TestBed.createComponent(MarketDataComponent);
    fixture.componentInstance.analysis = analysis();
    fixture.detectChanges();
  }

  const click = (group: string) => {
    (fixture.debugElement.query(By.css(`[data-group="${group}"]`)).nativeElement as HTMLElement).click();
    fixture.detectChanges();
  };
  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);

  beforeEach(() => { adjustedFlag = undefined; try { localStorage.removeItem('ta-guide-open'); } catch { /* bỏ qua */ } });
  afterEach(() => { fixture?.destroy(); try { localStorage.removeItem('ta-guide-open'); } catch { /* bỏ qua */ } });

  it('no block when no group', () => {
    setup();

    expect(q('ta-guide')).toBeNull();
  });

  it('guide block shows for selected group with all its indicators', () => {
    setup();
    click('xu-huong');

    const block = q('ta-guide')!;
    expect(block.textContent).toContain('Cách đọc nhóm Xu hướng');
    INDICATOR_GUIDES['xu-huong'].items.forEach(item => {
      expect(block.textContent).withContext(item.name).toContain(item.name);
      expect(block.textContent).withContext(item.name).toContain(item.what);
      expect(block.textContent).withContext(item.name).toContain(item.pitfall);
      item.howToRead.forEach(line => expect(block.textContent).toContain(line));
    });
  });

  it('shows the quick reading of the latest values before the explanations', () => {
    setup();
    click('dong-luong');

    const now = q('ta-guide-now')!;
    expect(now.textContent).toContain('RSI đang 72, trên 70: giá đã tăng nóng, dễ điều chỉnh.');
    expect(now.textContent).toContain('MACD đang trên đường tín hiệu: động lượng nghiêng về tăng.');
    const block = q('ta-guide')!;
    expect(block.textContent!.indexOf('RSI đang 72')).toBeLessThan(block.textContent!.indexOf(INDICATOR_GUIDES['dong-luong'].items[0].what));
  });

  it('lives outside the sticky chart area so it cannot grow the sticky block', () => {
    setup();
    click('xu-huong');

    const area = q('ta-chart-area')!;
    expect(area.contains(q('ta-guide'))).toBeFalse();
  });

  it('changes with the selected group and disappears when the group is deselected', () => {
    setup();
    click('xu-huong');
    click('bien-dong');
    expect(q('ta-guide')!.textContent).toContain('Cách đọc nhóm Biến động');
    expect(q('ta-guide')!.textContent).not.toContain('Cách đọc nhóm Xu hướng');

    click('bien-dong');
    expect(q('ta-guide')).toBeNull();
  });

  it('ends with the reminder and a link to the full guide', () => {
    setup();
    click('xu-huong');

    const block = q('ta-guide')!;
    expect(block.textContent).toContain('Chỉ báo mô tả quá khứ, không đảm bảo tương lai');
    expect(block.textContent).toContain('không ra quyết định chỉ từ một chỉ báo');
    const link = block.querySelector('a[data-testid="ta-guide-help-link"]') as HTMLAnchorElement;
    expect(link.textContent).toContain('Xem hướng dẫn đầy đủ (Phân tích thị trường)');
    expect(link.getAttribute('href')).toBe('/help?topic=phan-tich-thi-truong');
  });

  it('block collapsible and remembers state', () => {
    setup();
    click('xu-huong');
    expect(q('ta-guide-body')).not.toBeNull();

    (q('ta-guide-toggle') as HTMLElement).click();
    fixture.detectChanges();
    expect(q('ta-guide-body')).toBeNull();
    expect(q('ta-guide-toggle')!.getAttribute('aria-expanded')).toBe('false');
    expect(localStorage.getItem('ta-guide-open')).toBe('0');

    // dựng lại component: vẫn thu gọn
    fixture.destroy();
    TestBed.resetTestingModule();
    setup();
    click('xu-huong');
    expect(q('ta-guide')).not.toBeNull();
    expect(q('ta-guide-body')).toBeNull();

    (q('ta-guide-toggle') as HTMLElement).click();
    fixture.detectChanges();
    expect(q('ta-guide-body')).not.toBeNull();
    expect(localStorage.getItem('ta-guide-open')).toBe('1');
  });

  it('localStorage throwing does not break', () => {
    // Trình duyệt chặn dữ liệu trang: chính việc truy cập window.localStorage đã ném lỗi
    const original = Object.getOwnPropertyDescriptor(window, 'localStorage')!;
    Object.defineProperty(window, 'localStorage', { configurable: true, get: () => { throw new Error('bị chặn'); } });
    try {
      setup();

      expect(() => click('xu-huong')).not.toThrow();
      expect(q('ta-guide-body')).not.toBeNull();      // mặc định mở
      expect(() => { (q('ta-guide-toggle') as HTMLElement).click(); fixture.detectChanges(); }).not.toThrow();
      expect(q('ta-guide-body')).toBeNull();          // vẫn thu gọn được trong phiên này
    } finally {
      Object.defineProperty(window, 'localStorage', original);
    }
  });

  describe('ghi chú giá chưa điều chỉnh', () => {
    it('shows the note when the series is not adjusted', () => {
      adjustedFlag = false;
      setup();
      click('xu-huong');

      const note = q('ta-unadjusted-note')!;
      expect(note).not.toBeNull();
      expect(note.textContent).toContain('Dữ liệu chưa điều chỉnh theo sự kiện quyền, nến chỉ có giá đóng cửa');
    });

    it('lives outside the sticky chart area so it cannot grow the sticky block', () => {
      adjustedFlag = false;
      setup();
      click('xu-huong');

      expect(q('ta-chart-area')!.contains(q('ta-unadjusted-note'))).toBeFalse();
    });

    it('no note when adjusted', () => {
      adjustedFlag = true;
      setup();
      click('xu-huong');

      expect(q('ta-unadjusted-note')).toBeNull();
    });

    it('no note when the backend does not say (field missing or null)', () => {
      adjustedFlag = null;
      setup();
      click('xu-huong');
      expect(q('ta-unadjusted-note')).toBeNull();

      fixture.destroy();
      TestBed.resetTestingModule();
      adjustedFlag = undefined;
      setup();
      click('xu-huong');
      expect(q('ta-unadjusted-note')).toBeNull();
    });

    it('no note before a group is chosen', () => {
      adjustedFlag = false;
      setup();

      expect(q('ta-unadjusted-note')).toBeNull();
    });
  });

  it('uses textContent for the guide text (nothing is interpreted as html)', () => {
    setup();
    click('xu-huong');

    expect(q('ta-guide')!.querySelector('script')).toBeNull();
  });
});
