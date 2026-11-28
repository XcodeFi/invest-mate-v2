import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { BehaviorSubject, of } from 'rxjs';
import { SymbolTimelineComponent } from './symbol-timeline.component';
import { JournalEntryService } from '../../core/services/journal-entry.service';
import { MarketDataService, TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { MarketEventService } from '../../core/services/market-event.service';
import { NotificationService } from '../../core/services/notification.service';

const N = 330;
const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const daysAgo = (n: number) => { const d = new Date(); d.setDate(d.getDate() - n); return d; };

const series = (): TechnicalSeries => {
  const flat = (v: number) => Array.from({ length: N }, () => v);
  return {
    adjusted: true, symbol: 'VNM', dates: Array.from({ length: N }, (_, i) => `${iso(daysAgo(N - 1 - i))}T00:00:00Z`),
    open: flat(100), high: flat(101), low: flat(99), close: flat(100), volume: flat(1000),
    ema20: flat(112), ema50: flat(100), ema200: flat(98), bollingerUpper: flat(110), bollingerMiddle: flat(100), bollingerLower: flat(90),
    rsi14: flat(72), stochK: flat(50), stochD: flat(50), macdLine: flat(2), macdSignal: flat(1), macdHistogram: flat(1),
    adx: flat(18), plusDi: flat(20), minusDi: flat(15), atr14: flat(2), obv: flat(1000), mfi14: flat(50), volumeSma20: flat(1000),
  };
};

const analysis = (): TechnicalAnalysis => ({
  symbol: 'VNM', currentPrice: 100, ema20: 112, supportLevels: [95], resistanceLevels: [105],
  suggestedEntry: 100, suggestedStopLoss: 95, suggestedTarget: 110, confluenceScore: 68.5, overallSignal: 'buy', overallSignalVi: 'MUA',
} as unknown as TechnicalAnalysis);

/** Hai mục: giao dịch lúc 00:30 giờ VN ngày 20/11 (= 17:30 UTC ngày 19/11) và một nhật ký ngày 18/11. */
const tradeItem = { type: 'trade', timestamp: '2026-11-19T17:30:00Z', data: { id: 't1', tradeType: 'SELL', quantity: 100, price: 50000 } };
const journalItem = { type: 'journal', timestamp: '2026-11-18T03:00:00Z', data: { id: 'j1', entryType: 'Observation', title: 'x' } };

describe('SymbolTimelineComponent — trạng thái trên URL (range, grp, focus)', () => {
  let fixture: ComponentFixture<SymbolTimelineComponent>;
  let component: SymbolTimelineComponent;
  let query$: BehaviorSubject<ParamMap>;
  let navigate: jasmine.Spy;
  let journal: jasmine.SpyObj<JournalEntryService>;
  const route = { snapshot: { paramMap: convertToParamMap({ symbol: 'vnm' }), queryParams: {} as Record<string, string> } } as any;

  function setup(initialQuery: Record<string, string> = {}) {
    route.snapshot.queryParams = initialQuery;
    query$ = new BehaviorSubject<ParamMap>(convertToParamMap(initialQuery));
    route.queryParamMap = query$.asObservable();
    const market = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getTechnicalSeries', 'getTechnicalAnalysis']);
    market.getTechnicalSeries.and.callFake(() => of(series()));
    market.getTechnicalAnalysis.and.callFake(() => of(analysis()));
    journal = jasmine.createSpyObj<JournalEntryService>('JournalEntryService', ['getTimeline', 'create']);
    journal.getTimeline.and.callFake(() => of({ symbol: 'VNM', items: [tradeItem, journalItem].map(i => ({ ...i })), holdingPeriods: [], emotionSummary: null, behavioralPatterns: [] } as any));
    TestBed.configureTestingModule({
      imports: [SymbolTimelineComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ActivatedRoute, useValue: route },
        { provide: MarketDataService, useValue: market },
        { provide: JournalEntryService, useValue: journal },
        { provide: MarketEventService, useValue: jasmine.createSpyObj<MarketEventService>('MarketEventService', ['create']) },
        { provide: NotificationService, useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success', 'warning']) },
      ],
    });
    navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(SymbolTimelineComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    fixture.detectChanges();
  }

  const emit = (q: Record<string, string>) => { query$.next(convertToParamMap(q)); fixture.detectChanges(); };
  const lastNavigate = () => navigate.calls.mostRecent().args[1] as any;
  const timelineCalls = () => journal.getTimeline.calls.count();

  afterEach(() => fixture?.destroy());

  // ───── đọc từ URL ─────

  it('opens with the range and the indicator group from the URL', () => {
    setup({ range: '6T', grp: 'xu-huong' });

    expect(component.selectedRange).toBe(6);
    expect(component.selectedGroup).toBe('xu-huong');
    expect(timelineCalls()).toBe(1);
  });

  it('without the keys the page keeps its defaults (3T, no group)', () => {
    setup();

    expect(component.selectedRange).toBe(3);
    expect(component.selectedGroup).toBeNull();
  });

  it('ignores invalid values and keeps the defaults', () => {
    setup({ range: '9T', grp: 'bậy', focus: '2026-13-45' });

    expect(component.selectedRange).toBe(3);
    expect(component.selectedGroup).toBeNull();
    expect(component.selectedItemIndex).toBe(-1);
  });

  it('a range change in the URL (Back, a pasted link) re-applies the range and reloads once', () => {
    setup({ range: '3T' });
    const before = timelineCalls();

    emit({ range: '1N' });

    expect(component.selectedRange).toBe(12);
    expect(timelineCalls()).toBe(before + 1);
  });

  it('a group change in the URL re-applies the group without reloading the timeline', () => {
    setup({ range: '3T' });
    const before = timelineCalls();

    emit({ range: '3T', grp: 'dong-luong' });
    expect(component.selectedGroup).toBe('dong-luong');

    emit({ range: '3T' });   // khoá grp mất khỏi URL: về không nhóm
    expect(component.selectedGroup).toBeNull();
    expect(timelineCalls()).toBe(before);
  });

  it('an unchanged URL is a no-op (no reload, no state reset)', () => {
    setup({ range: '6T' });
    const before = timelineCalls();

    emit({ range: '6T' });
    emit({ range: '6T', tradeId: 'x-khong-lien-quan' });

    expect(timelineCalls()).toBe(before);
    expect(component.selectedRange).toBe(6);
  });

  // ───── ghi lên URL ─────

  it('picking a range writes it with merge + replaceUrl and touches no other key', () => {
    setup();

    component.setDateRange(1);

    const args = navigate.calls.mostRecent().args;
    expect(args[0]).toEqual([]);
    expect(lastNavigate().relativeTo).toBe(route);
    expect(lastNavigate().queryParamsHandling).toBe('merge');
    expect(lastNavigate().replaceUrl).toBeTrue();
    expect(lastNavigate().queryParams).toEqual({ range: '1T', grp: null, focus: null });
    expect(Object.keys(lastNavigate().queryParams)).not.toContain('symbol');
    expect(Object.keys(lastNavigate().queryParams)).not.toContain('tradeId');
  });

  it('toggling a group writes grp, toggling it off removes the key', () => {
    setup({ range: '3T' });

    component.toggleGroup('moc-gia');
    expect(lastNavigate().queryParams.grp).toBe('moc-gia');

    component.toggleGroup('moc-gia');
    expect(lastNavigate().queryParams.grp).toBeNull();
  });

  it('selecting a timeline item writes the VN date of the item as focus (00:30 VN on the 20th is the 20th, not the 19th)', () => {
    setup();
    const index = component.filteredItems.findIndex(i => i.type === 'trade');

    component.selectTimelineItem(index);

    expect(component.selectedItemIndex).toBe(index);
    expect(lastNavigate().queryParams.focus).toBe('2026-11-20');
  });

  it('the echo of our own write does not reload or reset anything', () => {
    setup({ range: '3T' });
    component.setDateRange(6);
    const before = timelineCalls();

    emit({ range: '6T' });   // đúng điều router phát lại sau lần ghi của chính ta

    expect(timelineCalls()).toBe(before);
    expect(component.selectedRange).toBe(6);
  });

  it('range, grp and focus round-trip: what the page writes, the page reads back to the same state', () => {
    setup();
    component.setDateRange(12);
    component.toggleGroup('xu-huong');
    component.selectTimelineItem(component.filteredItems.findIndex(i => i.type === 'journal'));
    const written = lastNavigate().queryParams as Record<string, string>;

    const second = TestBed.createComponent(SymbolTimelineComponent);
    query$.next(convertToParamMap(Object.fromEntries(Object.entries(written).filter(([, v]) => v != null)) as Record<string, string>));
    second.detectChanges();

    expect(second.componentInstance.selectedRange).toBe(12);
    expect(second.componentInstance.selectedGroup).toBe('xu-huong');
    expect(second.componentInstance.filteredItems[second.componentInstance.selectedItemIndex]?.type).toBe('journal');
    second.destroy();
  });

  // ───── focus ─────

  it('focus in the URL selects the item of that VN date once the timeline is loaded', () => {
    setup({ focus: '2026-11-20' });

    expect(component.filteredItems[component.selectedItemIndex]?.type).toBe('trade');
  });

  it('a focus date with no item selects nothing', () => {
    setup({ focus: '2026-01-01' });

    expect(component.selectedItemIndex).toBe(-1);
  });

  it('a focus change in the URL moves the selection without reloading', () => {
    setup({ range: '3T', focus: '2026-11-20' });
    const before = timelineCalls();

    emit({ range: '3T', focus: '2026-11-18' });

    expect(component.filteredItems[component.selectedItemIndex]?.type).toBe('journal');
    expect(timelineCalls()).toBe(before);
  });

  // ───── tradeId giữ nguyên ─────

  it('tradeId behaviour is unchanged: a review link without range still widens the default to 12 months', () => {
    setup({ tradeId: 't1' });

    expect(component.selectedRange).toBe(12);
    expect(component.reviewTradeId).toBe('t1');
  });

  it('tradeId wins over range on first load: the review flow keeps its 12 months', () => {
    setup({ tradeId: 't1', range: '1T' });

    expect(component.selectedRange).toBe(12);
  });

  it('leaving review mode removes tradeId only (merge) and does not reset the widened range', () => {
    setup({ tradeId: 't1' });
    const before = timelineCalls();

    component.cancelReviewMode();
    emit({});   // tradeId đã bị xoá khỏi URL, không khoá nào khác

    expect(navigate).toHaveBeenCalledWith([], jasmine.objectContaining({ queryParams: { tradeId: null }, queryParamsHandling: 'merge', replaceUrl: true }));
    expect(component.selectedRange).toBe(12);
    expect(timelineCalls()).toBe(before);
  });
});
