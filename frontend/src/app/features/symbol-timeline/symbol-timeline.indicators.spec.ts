import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Subject, of, throwError } from 'rxjs';
import { SymbolTimelineComponent } from './symbol-timeline.component';
import { IndicatorChartComponent } from '../../shared/components/indicator-chart/indicator-chart.component';
import { JournalEntryService } from '../../core/services/journal-entry.service';
import { MarketDataService, TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { MarketEventService } from '../../core/services/market-event.service';
import { NotificationService } from '../../core/services/notification.service';

const N = 330;
const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const daysAgo = (n: number) => { const d = new Date(); d.setDate(d.getDate() - n); return d; };

let adjustedFlag: boolean | null | undefined = true;
let adjustedAsOfFlag: string | null | undefined;

/** Chuỗi 330 ngày liên tiếp kết thúc hôm nay; EMA 20 có giá trị từ ngày đầu (đã khởi động) để kiểm việc cắt khoảng. */
let seriesAgeDays = 0;   // > 0: chuỗi kết thúc cách đây chừng đó ngày (để khoảng đang xem không có phiên nào)

const series = (months = 12): TechnicalSeries => {
  const flat = (v: number) => Array.from({ length: N }, () => v);
  return {
    ...(adjustedFlag === undefined ? {} : { adjusted: adjustedFlag }),
    ...(adjustedAsOfFlag === undefined ? {} : { adjustedAsOf: adjustedAsOfFlag }),
    symbol: 'VNM', dates: Array.from({ length: N }, (_, i) => `${iso(daysAgo(N - 1 - i + seriesAgeDays))}T00:00:00Z`),
    open: flat(100), high: flat(101), low: flat(99), close: flat(100), volume: flat(1000),
    ema20: flat(100 + months), ema50: flat(100), ema200: flat(98), bollingerUpper: flat(110), bollingerMiddle: flat(100), bollingerLower: flat(90),
    rsi14: flat(72), stochK: flat(50), stochD: flat(50), macdLine: flat(2), macdSignal: flat(1), macdHistogram: flat(1),
    adx: flat(18), plusDi: flat(20), minusDi: flat(15), atr14: flat(2), obv: flat(1000), mfi14: flat(50), volumeSma20: flat(1000),
  };
};

const analysis = (months = 12): TechnicalAnalysis => ({
  symbol: 'VNM', currentPrice: 100, ema20: 100 + months, supportLevels: [95], resistanceLevels: [105],
  suggestedEntry: 100, suggestedStopLoss: 95, suggestedTarget: 110, confluenceScore: 68.5,
  overallSignal: 'buy', overallSignalVi: 'MUA',
} as unknown as TechnicalAnalysis);

let items: any[] = [];
const timeline = () => ({ symbol: 'VNM', items, holdingPeriods: [], emotionSummary: null, behavioralPatterns: [] });

describe('SymbolTimelineComponent — chỉ báo theo nhóm trên biểu đồ', () => {
  let fixture: ComponentFixture<SymbolTimelineComponent>;
  let marketSpy: jasmine.SpyObj<MarketDataService>;
  let notify: jasmine.SpyObj<NotificationService>;

  /** Mỗi lần gọi chuỗi ở chế độ deferred trả một Subject để test tự quyết thứ tự trả lời. */
  let pending: Subject<TechnicalSeries>[] = [];

  function setup(opts: { seriesError?: boolean; deferred?: boolean; analysisError?: boolean } = {}) {
    pending = [];
    marketSpy = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getTechnicalSeries', 'getTechnicalAnalysis']);
    marketSpy.getTechnicalSeries.and.callFake((_symbol: string, months?: number) => {
      if (opts.seriesError) return throwError(() => new Error('x'));
      if (opts.deferred) { const s = new Subject<TechnicalSeries>(); pending.push(s); return s; }
      return of(series(months));
    });
    marketSpy.getTechnicalAnalysis.and.callFake((_symbol: string, months?: number) =>
      opts.analysisError ? throwError(() => new Error('y')) : of(analysis(months)));
    const journalSpy = jasmine.createSpyObj<JournalEntryService>('JournalEntryService', ['getTimeline', 'create']);
    journalSpy.getTimeline.and.callFake(() => of(timeline() as any));
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success', 'warning']);
    TestBed.configureTestingModule({
      imports: [SymbolTimelineComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ symbol: 'vnm' }), queryParams: {} } } },
        { provide: MarketDataService, useValue: marketSpy },
        { provide: JournalEntryService, useValue: journalSpy },
        { provide: MarketEventService, useValue: jasmine.createSpyObj<MarketEventService>('MarketEventService', ['create']) },
        { provide: NotificationService, useValue: notify },
      ],
    });
    fixture = TestBed.createComponent(SymbolTimelineComponent);
    fixture.detectChanges();
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const q = (testid: string) => el().querySelector(`[data-testid="${testid}"]`);
  const chart = () => fixture.debugElement.query(By.directive(IndicatorChartComponent))?.componentInstance as IndicatorChartComponent;
  const click = (group: string) => {
    (el().querySelector(`button[data-group="${group}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  };

  beforeEach(() => { seriesAgeDays = 0; adjustedFlag = true; adjustedAsOfFlag = undefined; items = []; try { localStorage.removeItem('ta-guide-open'); } catch { /* bỏ qua */ } });
  afterEach(() => fixture?.destroy());

  it('shows group chips and no guide block until a group is chosen', () => {
    setup();

    expect(el().querySelectorAll('button[data-group]').length).toBe(6);
    expect(q('ta-guide')).toBeNull();

    click('xu-huong');

    expect(q('ta-guide')).not.toBeNull();
    expect(q('ta-guide')!.textContent).toContain('Cách đọc nhóm');
  });

  it('chosen group draws its overlays on the chart; choosing it again clears them', () => {
    setup();
    expect(chart().layers.overlays.length).toBe(0);

    click('xu-huong');
    expect(chart().layers.overlays.map(o => o.label.toLowerCase()).join()).toContain('ema');

    click('xu-huong');
    expect(chart().layers.overlays.length).toBe(0);
    expect(q('ta-guide')).toBeNull();
  });

  it('old ema / sr / fib / candle toggles are gone', () => {
    setup();

    const labels = Array.from(el().querySelectorAll('button')).map(b => (b.textContent ?? '').trim());
    expect(labels).not.toContain('EMA');
    expect(labels).not.toContain('S/R');
    expect(labels).not.toContain('Fibonacci');
    expect(labels).not.toContain('Nến');
  });

  it('asks for one fixed indicator window whatever the range, then shows only the chosen range', () => {
    setup();   // khoảng mặc định 3 tháng

    expect(marketSpy.getTechnicalSeries).toHaveBeenCalledWith('VNM', 36);
    expect(marketSpy.getTechnicalAnalysis).toHaveBeenCalledWith('VNM', 36);
    const dates = chart().dates;
    expect(dates.length).toBeGreaterThan(80);
    expect(dates.length).toBeLessThan(100);
    const from = new Date(); from.setMonth(from.getMonth() - 3);
    expect(dates[0].substring(0, 10) >= iso(from)).toBeTrue();

    click('xu-huong');
    const ema20 = chart().layers.overlays.find(o => o.label.includes('20'))!;
    expect(ema20.values[0]).not.toBeNull();   // đầu khoảng không bị trống vì đã được tính trên chuỗi dài hơn
    expect(ema20.values.length).toBe(dates.length);
  });

  it('last_bar_indicators_identical_across_ranges_and_equal_analysis', () => {
    setup();
    click('xu-huong');
    const lastEma20 = () => { const v = chart().layers.overlays.find(o => o.label.includes('20'))!.values; return v[v.length - 1]; };
    const seen: (number | null)[] = [lastEma20()];

    for (const label of ['1T', '6T', '1N', '3T']) {
      (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === label) as HTMLButtonElement).click();
      fixture.detectChanges();
      seen.push(lastEma20());
    }

    // cùng một cửa sổ cho mọi khoảng và cho analysis, nên giá trị phiên cuối không phụ thuộc khoảng đang xem
    const seriesMonths = marketSpy.getTechnicalSeries.calls.allArgs().map(a => a[1]);
    const analysisMonths = marketSpy.getTechnicalAnalysis.calls.allArgs().map(a => a[1]);
    expect(new Set(seriesMonths)).toEqual(new Set([36]));
    expect(new Set(analysisMonths)).toEqual(new Set([36]));
    expect(new Set(seen).size).toBe(1);
    expect(seen[0]).toBe(136);   // = ema20 của analysis cùng cửa sổ (100 + 36)
  });

  it('changing the date range widens the visible slice from the same window', () => {
    setup();
    const before = chart().dates.length;

    (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === '1N') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(chart().dates.length).toBeGreaterThan(before);
  });

  it('range_switch_does_not_refetch_series_or_analysis', () => {
    setup();
    expect(marketSpy.getTechnicalSeries).toHaveBeenCalledTimes(1);
    expect(marketSpy.getTechnicalAnalysis).toHaveBeenCalledTimes(1);

    for (const label of ['1T', '6T', '1N', '3T']) {
      (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === label) as HTMLButtonElement).click();
      fixture.detectChanges();
    }

    expect(marketSpy.getTechnicalSeries).toHaveBeenCalledTimes(1);
    expect(marketSpy.getTechnicalAnalysis).toHaveBeenCalledTimes(1);
    expect(marketSpy.getTechnicalSeries.calls.mostRecent().args).toEqual(['VNM', 36]);
    expect(chart().dates.length).toBeGreaterThan(80);   // đã quay lại 3T từ cùng một chuỗi
  });

  it('a range switch still refetches the timeline (the only call that depends on the range)', () => {
    setup();
    const journal = TestBed.inject(JournalEntryService) as jasmine.SpyObj<JournalEntryService>;
    const before = journal.getTimeline.calls.count();

    (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === '6T') as HTMLButtonElement).click();

    expect(journal.getTimeline.calls.count()).toBe(before + 1);
  });

  it('a series answer that lands after a range switch is cut to the NEW range, and no second request is made', () => {
    setup({ deferred: true });                       // lần tải đầu: khoảng mặc định 3 tháng, chưa có câu trả lời
    (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === '1N') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(pending.length).toBe(1);

    pending[0].next(series()); pending[0].complete();
    fixture.detectChanges();

    expect(chart().dates.length).toBeGreaterThan(300);   // cắt theo 1N, không phải 3T
  });

  it('series_error_clears_previous_series', () => {
    setup();
    expect(chart()).toBeDefined();
    marketSpy.getTechnicalSeries.and.returnValue(throwError(() => new Error('x')));

    fixture.componentInstance.symbol = 'FPT';        // mã khác thì mới tải lại chuỗi
    fixture.componentInstance.loadData();
    fixture.detectChanges();

    expect(fixture.componentInstance.series).toBeNull();
    expect(fixture.componentInstance.chartMarkers).toEqual([]);
    expect(fixture.componentInstance.groupLayers.panes).toEqual([]);
    expect(chart()).toBeUndefined();
    expect(q('timeline-chart-error')).not.toBeNull();
  });

  it('an empty slice (the series ends before the chosen range starts) shows a message instead of a blank chart', () => {
    seriesAgeDays = 400;
    setup();

    expect(q('timeline-chart-empty')!.textContent).toContain('Không có dữ liệu giá trong khoảng này');
    expect(chart()).toBeUndefined();
  });

  it('no empty-slice message when the range has bars', () => {
    setup();

    expect(q('timeline-chart-empty')).toBeNull();
  });

  it('an analysis failure shows one short line instead of silently empty groups', () => {
    setup({ analysisError: true });

    expect(q('timeline-analysis-error')!.textContent).toContain('Không tải được chỉ báo, thử lại sau');
    expect(chart()).toBeDefined();   // nến và mốc vẫn vẽ
  });

  it('no analysis error line when the analysis loads', () => {
    setup();

    expect(q('timeline-analysis-error')).toBeNull();
  });

  it('passes trade, journal and event markers to the chart', () => {
    const d = `${iso(daysAgo(10))}T03:00:00Z`;
    items = [
      { type: 'trade', timestamp: d, data: { tradeType: 'BUY', price: 63000, quantity: 100 } },
      { type: 'journal', timestamp: d, data: { title: 'Vào lệnh' } },
      { type: 'event', timestamp: d, data: { eventType: 'News', title: 'Tin' } },
    ];
    setup();

    expect(chart().markers.map(m => m.text)).toEqual(['T', 'J', 'E']);
    expect(chart().markerCount).toBe(3);
  });

  it('trade tooltip says giá gốc when the trade price is outside that session range on an adjusted series', () => {
    items = [{ type: 'trade', timestamp: `${iso(daysAgo(20))}T03:00:00Z`, data: { tradeType: 'BUY', price: 63000, quantity: 100 } }];
    setup();   // nến của chuỗi giả quanh 99..101, giá giao dịch 63.000 nằm ngoài

    expect(chart().markers.find(m => m.text === 'T')!.tooltip![0]).toContain('(giá gốc)');
  });

  it('no giá gốc label when the trade price is inside the session range', () => {
    items = [{ type: 'trade', timestamp: `${iso(daysAgo(20))}T03:00:00Z`, data: { tradeType: 'BUY', price: 100, quantity: 100 } }];
    setup();

    expect(chart().markers.find(m => m.text === 'T')!.tooltip![0]).not.toContain('(giá gốc)');
  });

  it('no giá gốc label on an unadjusted series', () => {
    adjustedFlag = false;
    items = [{ type: 'trade', timestamp: `${iso(daysAgo(20))}T03:00:00Z`, data: { tradeType: 'BUY', price: 63000, quantity: 100 } }];
    setup();

    expect(chart().markers.find(m => m.text === 'T')!.tooltip![0]).not.toContain('(giá gốc)');
  });

  it('markers on the chart do not break when there are no timeline items', () => {
    setup();

    expect(chart().markers).toEqual([]);
    expect(chart().chartCount).toBe(1);
  });

  it('journals with an emotion add a bottom pane of coloured bars', () => {
    items = [{ type: 'journal', timestamp: `${iso(daysAgo(10))}T03:00:00Z`, data: { title: 'a', emotionalState: 'Tự tin', confidenceLevel: 8 } }];
    setup();

    expect(chart().layers.panes.length).toBe(1);
    expect(chart().layers.panes[0].title).toContain('Cảm xúc');
    expect(chart().chartCount).toBe(2);
  });

  it('shows the unadjusted note outside the chart when the source fell back', () => {
    adjustedFlag = false;
    setup();

    expect(q('ta-unadjusted-note')).not.toBeNull();
  });

  it('shows the stale adjusted-data note with the as-of date', () => {
    adjustedAsOfFlag = '2026-11-16T00:00:00Z';
    setup();

    expect(q('ta-adjusted-asof-note')!.textContent).toContain('16/11/2026');
  });

  it('a failed series request notifies and shows an error instead of the chart', () => {
    setup({ seriesError: true });

    expect(notify.error).toHaveBeenCalled();
    expect(q('timeline-chart-error')).not.toBeNull();
    expect(chart()).toBeUndefined();
  });
});
