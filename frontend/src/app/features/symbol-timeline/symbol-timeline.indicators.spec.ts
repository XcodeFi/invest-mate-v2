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

const series = (): TechnicalSeries => {
  const flat = (v: number) => Array.from({ length: N }, () => v);
  return {
    ...(adjustedFlag === undefined ? {} : { adjusted: adjustedFlag }),
    ...(adjustedAsOfFlag === undefined ? {} : { adjustedAsOf: adjustedAsOfFlag }),
    symbol: 'VNM', dates: Array.from({ length: N }, (_, i) => `${iso(daysAgo(N - 1 - i + seriesAgeDays))}T00:00:00Z`),
    open: flat(100), high: flat(101), low: flat(99), close: flat(100), volume: flat(1000),
    ema20: flat(112), ema50: flat(100), ema200: flat(98), bollingerUpper: flat(110), bollingerMiddle: flat(100), bollingerLower: flat(90),
    rsi14: flat(72), stochK: flat(50), stochD: flat(50), macdLine: flat(2), macdSignal: flat(1), macdHistogram: flat(1),
    adx: flat(18), plusDi: flat(20), minusDi: flat(15), atr14: flat(2), obv: flat(1000), mfi14: flat(50), volumeSma20: flat(1000),
  };
};

const analysis = (): TechnicalAnalysis => ({
  symbol: 'VNM', currentPrice: 100, ema20: 112, supportLevels: [95], resistanceLevels: [105],
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

  function setup(opts: { seriesError?: boolean; deferred?: boolean; analysisError?: boolean; seriesFailOnce?: boolean; analysisFailOnce?: boolean; fixed?: TechnicalSeries } = {}) {
    pending = [];
    marketSpy = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getTechnicalSeries', 'getTechnicalAnalysis']);
    let seriesCalls = 0;
    let analysisCalls = 0;
    marketSpy.getTechnicalSeries.and.callFake(() => {
      seriesCalls++;
      if (opts.seriesError || (opts.seriesFailOnce && seriesCalls === 1)) return throwError(() => new Error('x'));
      if (opts.deferred) { const s = new Subject<TechnicalSeries>(); pending.push(s); return s; }
      return of(opts.fixed ?? series());
    });
    marketSpy.getTechnicalAnalysis.and.callFake(() => {
      analysisCalls++;
      return opts.analysisError || (opts.analysisFailOnce && analysisCalls === 1) ? throwError(() => new Error('y')) : of(analysis());
    });
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
    expect(marketSpy.getTechnicalAnalysis).toHaveBeenCalledWith('VNM');   // phân tích dùng cửa sổ mặc định: mốc hỗ trợ/kháng cự trong 12 tháng như trang Thị trường
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
    expect(new Set(analysisMonths)).toEqual(new Set([undefined]));   // không truyền months
    expect(new Set(seen).size).toBe(1);
    expect(seen[0]).toBe(112);   // = ema20 của analysis (backend tính chỉ báo trên cùng một lịch sử cho mọi months)
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
    expect(marketSpy.getTechnicalAnalysis.calls.mostRecent().args).toEqual(['VNM']);
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

  describe('khoảng ngày gửi cho dòng thời gian tính theo giờ Việt Nam', () => {
    afterEach(() => jasmine.clock().uninstall());

    it('01:00 on 28/11 in Vietnam (18:00Z on 27/11) asks the timeline up to 2026-11-28, from 2026-08-28 for 3 months', () => {
      jasmine.clock().install();
      jasmine.clock().mockDate(new Date('2026-11-27T18:00:00Z'));
      setup();

      const journal = TestBed.inject(JournalEntryService) as jasmine.SpyObj<JournalEntryService>;
      expect(journal.getTimeline).toHaveBeenCalledWith('VNM', '2026-08-28', '2026-11-28');
    });

    it('the same instant for a 12-month range starts on 2025-11-28', () => {
      jasmine.clock().install();
      jasmine.clock().mockDate(new Date('2026-11-27T18:00:00Z'));
      setup();
      const journal = TestBed.inject(JournalEntryService) as jasmine.SpyObj<JournalEntryService>;

      (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === '1N') as HTMLButtonElement).click();

      expect(journal.getTimeline.calls.mostRecent().args).toEqual(['VNM', '2025-11-28', '2026-11-28']);
    });
  });

  it('series_error_clears_previous_series', () => {
    setup();
    expect(chart()).toBeDefined();
    marketSpy.getTechnicalSeries.and.returnValue(throwError(() => new Error('x')));

    fixture.componentInstance.symbol = 'FPT';        // mã khác thì mới tải lại chuỗi
    fixture.componentInstance.loadData();
    fixture.detectChanges();

    expect(fixture.componentInstance.series).toBeNull();
    expect(chart()).toBeUndefined();
    expect(q('timeline-chart-error')).not.toBeNull();
  });

  describe('đổi khoảng cắt theo ngày Việt Nam trên chuỗi đã tải', () => {
    afterEach(() => jasmine.clock().uninstall());

    /** Chuỗi có ngày cố định 20/08 đến 27/11/2026, không phụ thuộc hôm nay. */
    const fixedSeries = (): TechnicalSeries => {
      const base = series();
      const n = 100;
      const dates = Array.from({ length: n }, (_, i) => new Date(Date.UTC(2026, 7, 20 + i)).toISOString());
      const cut = <T>(a: T[]) => a.slice(0, n);
      return { ...base, dates, open: cut(base.open), high: cut(base.high), low: cut(base.low), close: cut(base.close), volume: cut(base.volume),
        ema20: cut(base.ema20), ema50: cut(base.ema50), ema200: cut(base.ema200!), bollingerUpper: cut(base.bollingerUpper), bollingerMiddle: cut(base.bollingerMiddle),
        bollingerLower: cut(base.bollingerLower), rsi14: cut(base.rsi14), stochK: cut(base.stochK), stochD: cut(base.stochD), macdLine: cut(base.macdLine),
        macdSignal: cut(base.macdSignal), macdHistogram: cut(base.macdHistogram), adx: cut(base.adx), plusDi: cut(base.plusDi), minusDi: cut(base.minusDi),
        atr14: cut(base.atr14), obv: cut(base.obv), mfi14: cut(base.mfi14), volumeSma20: cut(base.volumeSma20) };
    };

    it('01:00 on 28/11 in Vietnam: the first visible session for 3T is 28/08, not 27/08 (the UTC date)', () => {
      jasmine.clock().install();
      jasmine.clock().mockDate(new Date('2026-11-27T18:00:00Z'));
      setup({ fixed: fixedSeries() });

      expect(chart().dates[0].substring(0, 10)).toBe('2026-08-28');
      expect(chart().dates[chart().dates.length - 1].substring(0, 10)).toBe('2026-11-27');
    });
  });

  it('a failed series load can be retried by a range switch: the chart appears and the error clears', () => {
    setup({ seriesFailOnce: true });
    expect(q('timeline-chart-error')).not.toBeNull();
    expect(chart()).toBeUndefined();

    (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === '6T') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(marketSpy.getTechnicalSeries).toHaveBeenCalledTimes(2);
    expect(q('timeline-chart-error')).toBeNull();
    expect(chart()).toBeDefined();
  });

  it('an analysis failure is retried on the next range switch and its line clears', () => {
    setup({ analysisFailOnce: true });
    expect(q('timeline-analysis-error')).not.toBeNull();

    (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').trim() === '6T') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(marketSpy.getTechnicalAnalysis).toHaveBeenCalledTimes(2);
    expect(q('timeline-analysis-error')).toBeNull();
    click('xu-huong');
    expect(chart().layers.overlays.length).toBeGreaterThan(0);   // phân tích đã có nên nhóm vẽ được
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
