import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of, Subject, throwError } from 'rxjs';
import { MarketDataComponent } from './market-data.component';
import { MarketDataService, TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { CompanyDossierService } from '../../core/services/company-dossier.service';

// Stub đủ mọi trường (kể cả fibonacci, ema200) — từng test ghi đè phần cần thiếu.
function fullTa(): TechnicalAnalysis {
  return {
    symbol: 'HPG',
    analyzedAt: '2026-11-21T00:00:00Z',
    dataPoints: 250,
    currentPrice: 28000,
    priceChange: 300,
    priceChangePercent: 1.08,
    currentVolume: 1_200_000,
    ema20: 27500,
    ema50: 27000,
    emaTrend: 'bullish',
    rsi14: 55.2,
    rsiSignal: 'neutral',
    macdLine: 120,
    signalLine: 100,
    macdHistogram: 20,
    macdSignal: 'buy',
    avgVolume20: 1_000_000,
    volumeRatio: 1.2,
    volumeSignal: 'normal',
    supportLevels: [26500, 27000],
    resistanceLevels: [28500, 29000],
    overallSignal: 'buy',
    overallSignalVi: 'MUA',
    bullishCount: 5,
    bearishCount: 2,
    neutralCount: 3,
    bollingerUpper: 29000,
    bollingerMiddle: 27500,
    bollingerLower: 26000,
    bollingerBandwidth: 10.9,
    bollingerPercentB: 0.667,
    bollingerSignal: 'neutral',
    ema200: 25000,
    ema21: 27400,
    highestHigh22: 29200,
    lowestLow22: 26100,
    fibonacci: {
      swingHigh: 29200,
      swingLow: 26100,
      retracement236: 28468,
      retracement382: 28016,
      retracement500: 27650,
      retracement618: 27284,
      retracement786: 26763,
      extension1272: 30043,
      extension1618: 31116,
    },
    atr14: 550,
    atrPercent: 1.96,
    stochasticK: 62.1,
    stochasticD: 58.4,
    stochasticSignal: 'neutral',
    adx14: 28.3,
    plusDi: 24.1,
    minusDi: 15.2,
    adxSignal: 'trending',
    obv: 15_300_000,
    obvSignal: 'rising',
    mfi14: 61.5,
    mfiSignal: 'neutral',
    confluenceScore: 68.5,
    marketCondition: 'trending',
    marketConditionVi: 'Có xu hướng',
    suggestedStrategy: 'Giao dịch theo xu hướng',
    rsiDivergence: undefined,
    macdDivergence: undefined,
    divergenceSignal: undefined,
    divergenceSignalVi: undefined,
    suggestedEntry: 27800,
    suggestedStopLoss: 26800,
    suggestedTarget: 29800,
    riskRewardRatio: 2.0,
  };
}

describe('MarketDataComponent — panel phân tích kỹ thuật theo 6 khối', () => {
  let fixture: ComponentFixture<MarketDataComponent>;

  function setup(analysis: TechnicalAnalysis | null) {
    const marketSpy = jasmine.createSpyObj<MarketDataService>('MarketDataService', [
      'getMarketOverview', 'getBatchPrices', 'getTopFluctuation',
    ]);
    marketSpy.getMarketOverview.and.returnValue(of([]));
    marketSpy.getBatchPrices.and.returnValue(of([]));
    marketSpy.getTopFluctuation.and.returnValue(of([]));
    const notifySpy = jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']);
    const dossierSpy = jasmine.createSpyObj<CompanyDossierService>('CompanyDossierService', ['get']);

    TestBed.configureTestingModule({
      imports: [MarketDataComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MarketDataService, useValue: marketSpy },
        { provide: NotificationService, useValue: notifySpy },
        { provide: CompanyDossierService, useValue: dossierSpy },
      ],
    });
    fixture = TestBed.createComponent(MarketDataComponent);
    fixture.componentInstance.analysis = analysis;
    fixture.detectChanges();
  }

  function groupValues(): (string | null)[] {
    return fixture.debugElement.queryAll(By.css('[data-group]'))
      .map(el => el.nativeElement.getAttribute('data-group'));
  }

  it('6 tiêu đề khối render đúng thứ tự', () => {
    setup(fullTa());

    expect(groupValues()).toEqual([
      'tong-hop', 'xu-huong', 'dong-luong', 'bien-dong', 'dong-tien', 'moc-gia',
    ]);
  });

  it('không có analysis thì không có tiêu đề khối nào', () => {
    setup(null);

    expect(groupValues().length).toBe(0);
  });

  it('confluence null và không có suggestedEntry thì ẩn khối Tổng hợp', () => {
    const ta = fullTa();
    ta.confluenceScore = undefined;
    ta.suggestedEntry = undefined;
    setup(ta);

    expect(groupValues()).toEqual([
      'xu-huong', 'dong-luong', 'bien-dong', 'dong-tien', 'moc-gia',
    ]);
  });

  it('hỗ trợ/kháng cự rỗng vẫn hiện 2 thẻ với Chưa xác định', () => {
    const ta = fullTa();
    ta.supportLevels = [];
    ta.resistanceLevels = [];
    setup(ta);

    const mocGia = fixture.debugElement.query(By.css('[data-group="moc-gia"]'));
    expect(mocGia).toBeTruthy();
    const text: string = fixture.nativeElement.textContent;
    const matches = text.match(/Chưa xác định/g) ?? [];
    expect(matches.length).toBeGreaterThanOrEqual(2);
    expect(text).toContain('Hỗ trợ');
    expect(text).toContain('Kháng cự');
  });

  it('marketCondition trending thì hiện thẻ Trạng thái thị trường trong nhóm Xu hướng', () => {
    setup(fullTa());

    const text: string = fixture.nativeElement.textContent;
    const idx = text.indexOf('Trạng thái thị trường');
    expect(idx).toBeGreaterThan(text.indexOf('📈 Xu hướng'));
    expect(idx).toBeLessThan(text.indexOf('⚡ Động lượng'));
    expect(text).toContain('Có xu hướng');
  });

  it('marketCondition unknown thì ẩn thẻ Trạng thái nhưng nhóm Xu hướng vẫn còn', () => {
    const ta = fullTa();
    ta.marketCondition = 'unknown';
    setup(ta);

    expect(fixture.debugElement.query(By.css('[data-group="xu-huong"]'))).toBeTruthy();
    const text: string = fixture.nativeElement.textContent;
    expect(text).not.toContain('Trạng thái thị trường');
  });

  it('có fibonacci thì thẻ Fibonacci hiện 5 mốc retracement', () => {
    setup(fullTa());

    const levels = fixture.debugElement.queryAll(By.css('[data-fib-level]'));
    expect(levels.length).toBe(5);
    const labels = levels.map(l => (l.nativeElement.textContent as string));
    for (const expected of ['23.6%', '38.2%', '50%', '61.8%', '78.6%']) {
      expect(labels.some(t => t.includes(expected)))
        .withContext(`thiếu mốc ${expected}`).toBeTrue();
    }
  });

  it('thiếu fibonacci thì thẻ hiện Không đủ dữ liệu', () => {
    const ta = fullTa();
    ta.fibonacci = undefined;
    setup(ta);

    const fibCard = fixture.debugElement.query(By.css('[data-card="fibonacci"]'));
    expect(fibCard).toBeTruthy();
    expect(fibCard.nativeElement.textContent).toContain('Không đủ dữ liệu');
    expect(fixture.debugElement.queryAll(By.css('[data-fib-level]')).length).toBe(0);
  });
});

// ---------------------------------------------------------------------------------------------
// Biểu đồ chỉ báo theo nhóm: bấm tiêu đề khối → tải chuỗi + phân tích song song, vẽ nhóm đó.
// ---------------------------------------------------------------------------------------------

function fullSeries(symbol = 'HPG'): TechnicalSeries {
  const n = 60;
  const nums = (): number[] => Array.from({ length: n }, (_, i) => 100 + i);
  const vals = (): (number | null)[] => Array.from({ length: n }, (_, i) => 50 + i);
  return {
    symbol,
    dates: Array.from({ length: n }, (_, i) => new Date(Date.UTC(2026, 0, 1 + i)).toISOString()),
    open: nums(), high: nums(), low: nums(), close: nums(), volume: nums(),
    ema20: vals(), ema50: vals(),
    bollingerUpper: vals(), bollingerMiddle: vals(), bollingerLower: vals(),
    rsi14: vals(), stochK: vals(), stochD: vals(),
    macdLine: vals(), macdSignal: vals(), macdHistogram: vals(),
    adx: vals(), plusDi: vals(), minusDi: vals(),
    atr14: vals(), obv: vals(), mfi14: vals(), volumeSma20: vals(),
  };
}

describe('MarketDataComponent — bấm nhóm để xem biểu đồ', () => {
  let fixture: ComponentFixture<MarketDataComponent>;
  let component: MarketDataComponent;
  let marketSpy: jasmine.SpyObj<MarketDataService>;

  function setup() {
    marketSpy = jasmine.createSpyObj<MarketDataService>('MarketDataService', [
      'getMarketOverview', 'getBatchPrices', 'getTopFluctuation', 'getTechnicalSeries', 'getTechnicalAnalysis',
    ]);
    marketSpy.getMarketOverview.and.returnValue(of([]));
    marketSpy.getBatchPrices.and.returnValue(of([]));
    marketSpy.getTopFluctuation.and.returnValue(of([]));
    marketSpy.getTechnicalSeries.and.returnValue(of(fullSeries()));
    marketSpy.getTechnicalAnalysis.and.returnValue(of({ ...fullTa(), currentPrice: 99999 }));

    TestBed.configureTestingModule({
      imports: [MarketDataComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MarketDataService, useValue: marketSpy },
        { provide: NotificationService, useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']) },
        { provide: CompanyDossierService, useValue: jasmine.createSpyObj<CompanyDossierService>('CompanyDossierService', ['get']) },
      ],
    });
    fixture = TestBed.createComponent(MarketDataComponent);
    component = fixture.componentInstance;
    component.analysis = fullTa();
    fixture.detectChanges();
  }

  const header = (group: string): HTMLElement =>
    fixture.debugElement.query(By.css(`[data-group="${group}"]`)).nativeElement;
  const click = (group: string) => { header(group).click(); fixture.detectChanges(); };
  const chartEl = () => fixture.debugElement.query(By.css('app-indicator-chart'));
  const text = (): string => fixture.nativeElement.textContent;

  afterEach(() => fixture?.destroy());

  it('before any click shows hint "Bấm tên nhóm để xem biểu đồ" and no chart', () => {
    setup();

    expect(text()).toContain('Bấm tên nhóm để xem biểu đồ');
    expect(chartEl()).toBeNull();
    expect(marketSpy.getTechnicalSeries).not.toHaveBeenCalled();
  });

  it('clicking a group header selects it and sets aria-pressed=true', () => {
    setup();

    click('xu-huong');

    expect(component.selectedGroup).toBe('xu-huong');
    expect(header('xu-huong').getAttribute('aria-pressed')).toBe('true');
    expect(header('dong-luong').getAttribute('aria-pressed')).toBe('false');
    expect(chartEl()).not.toBeNull();
    expect(text()).not.toContain('Bấm tên nhóm để xem biểu đồ');
  });

  it('clicking the selected header again clears selection and keeps the candles-only chart', () => {
    setup();
    click('xu-huong');

    click('xu-huong');

    expect(component.selectedGroup).toBeNull();
    expect(header('xu-huong').getAttribute('aria-pressed')).toBe('false');
    expect(chartEl()).not.toBeNull();
    expect(component.groupLayers.overlays).toEqual([]);
    expect(component.groupLayers.panes).toEqual([]);
  });

  it('first click loads series and analysis together and replaces analysis', () => {
    setup();

    click('xu-huong');

    expect(marketSpy.getTechnicalSeries).toHaveBeenCalledTimes(1);
    expect(marketSpy.getTechnicalSeries).toHaveBeenCalledWith('HPG');
    expect(marketSpy.getTechnicalAnalysis).toHaveBeenCalledTimes(1);
    expect(marketSpy.getTechnicalAnalysis).toHaveBeenCalledWith('HPG');
    expect(component.analysis!.currentPrice).toBe(99999);
  });

  it('second click on another group uses cache', () => {
    setup();
    click('xu-huong');

    click('dong-luong');

    expect(component.selectedGroup).toBe('dong-luong');
    expect(marketSpy.getTechnicalSeries).toHaveBeenCalledTimes(1);
    expect(marketSpy.getTechnicalAnalysis).toHaveBeenCalledTimes(1);
    expect(component.groupLayers.panes.map(p => p.title)).toEqual(['RSI', 'MACD', 'Stochastic']);
  });

  it('Enter and Space on the header toggle the group', () => {
    setup();

    header('xu-huong').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();
    expect(component.selectedGroup).toBe('xu-huong');

    header('xu-huong').dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }));
    fixture.detectChanges();
    expect(component.selectedGroup).toBeNull();
  });

  it('late response for previous symbol is ignored', () => {
    setup();
    const oldSeries$ = new Subject<TechnicalSeries>();
    const oldAnalysis$ = new Subject<TechnicalAnalysis>();
    marketSpy.getTechnicalSeries.and.returnValue(oldSeries$);
    marketSpy.getTechnicalAnalysis.and.callFake((sym: string) =>
      sym === 'HPG' ? oldAnalysis$ : of({ ...fullTa(), symbol: 'VNM' }));

    click('xu-huong');
    expect(component.chartLoading).toBeTrue();

    component.loadTechnicalAnalysis('VNM');
    fixture.detectChanges();
    oldSeries$.next(fullSeries('HPG'));
    oldSeries$.complete();
    oldAnalysis$.next(fullTa());
    oldAnalysis$.complete();
    fixture.detectChanges();

    expect(component.analysis!.symbol).toBe('VNM');
    expect(component.currentSeries).toBeNull();
    expect(component.seriesCache.size).toBe(0);
    expect(component.selectedGroup).toBeNull();
    expect(component.chartLoading).toBeFalse();
    expect(chartEl()).toBeNull();
  });

  it('series error shows message and clears selection', () => {
    setup();
    marketSpy.getTechnicalSeries.and.returnValue(throwError(() => new Error('500')));

    click('xu-huong');

    expect(text()).toContain('Không tải được dữ liệu biểu đồ');
    expect(component.selectedGroup).toBeNull();
    expect(header('xu-huong').getAttribute('aria-pressed')).toBe('false');
    expect(fixture.debugElement.queryAll(By.css('[data-group]')).length).toBe(6);
    expect(component.analysis!.currentPrice).toBe(28000);
  });

  it('clicking create-plan button does not toggle group', () => {
    setup();
    const planSpy = spyOn(component, 'onCreatePlanFromSignal');
    const button = fixture.debugElement.queryAll(By.css('button'))
      .find(b => (b.nativeElement.textContent as string).includes('Tạo Trade Plan'))!;

    button.nativeElement.click();
    fixture.detectChanges();

    expect(planSpy).toHaveBeenCalledTimes(1);
    expect(component.selectedGroup).toBeNull();
    expect(marketSpy.getTechnicalSeries).not.toHaveBeenCalled();
  });
});
