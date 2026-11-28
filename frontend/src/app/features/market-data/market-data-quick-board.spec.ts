import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable, of, throwError } from 'rxjs';
import { MarketDataComponent } from './market-data.component';
import { BatchPrice, MarketDataService } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { CompanyDossierService } from '../../core/services/company-dossier.service';
import { AuthService } from '../../core/services/auth.service';
import { ActivePosition, PositionsService } from '../../core/services/positions.service';
import { TradePlan, TradePlanService } from '../../core/services/trade-plan.service';
import { WatchlistDetail, WatchlistService, WatchlistSummary } from '../../core/services/watchlist.service';

const KEY = 'imv2.quickBoard.symbols';
const DEFAULT = 'VNM,FPT,VCB,HPG,MWG,VIC';

const position = (symbol: string) => ({ symbol } as unknown as ActivePosition);
const plan = (symbol: string) => ({ symbol } as unknown as TradePlan);
const wl = (id: string, name: string, itemCount: number, emoji = '⭐') => ({ id, name, emoji, itemCount } as unknown as WatchlistSummary);
const wlDetail = (id: string, symbols: string[]) => ({ id, items: symbols.map(symbol => ({ symbol })) } as unknown as WatchlistDetail);

describe('MarketDataComponent: Bảng giá nhanh nhớ danh sách và gợi ý xem nhanh', () => {
  let fixture: ComponentFixture<MarketDataComponent>;
  let market: jasmine.SpyObj<MarketDataService>;
  let positions: jasmine.SpyObj<PositionsService>;
  let plans: jasmine.SpyObj<TradePlanService>;
  let watchlists: jasmine.SpyObj<WatchlistService>;

  interface Sources {
    positions?: Observable<ActivePosition[]>;
    plans?: Observable<TradePlan[]>;
    watchlists?: Observable<WatchlistSummary[]>;
    details?: Record<string, Observable<WatchlistDetail>>;
    batch?: Observable<BatchPrice[]>;
  }

  function setup(src: Sources = {}) {
    market = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getMarketOverview', 'getBatchPrices', 'getTopFluctuation', 'getSignalSetups']);
    market.getMarketOverview.and.returnValue(of([]));
    market.getTopFluctuation.and.returnValue(of([]));
    market.getBatchPrices.and.returnValue(src.batch ?? of([{ symbol: 'X', date: '2026-11-27', close: 100, volume: 1000 } as BatchPrice]));
    positions = jasmine.createSpyObj<PositionsService>('PositionsService', ['getAll']);
    positions.getAll.and.returnValue(src.positions ?? of([]));
    plans = jasmine.createSpyObj<TradePlanService>('TradePlanService', ['getAll']);
    plans.getAll.and.returnValue(src.plans ?? of([]));
    watchlists = jasmine.createSpyObj<WatchlistService>('WatchlistService', ['getAll', 'getDetail']);
    watchlists.getAll.and.returnValue(src.watchlists ?? of([]));
    watchlists.getDetail.and.callFake((id: string) => (src.details ?? {})[id] ?? throwError(() => new Error('x')));
    TestBed.configureTestingModule({
      imports: [MarketDataComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: MarketDataService, useValue: market },
        { provide: PositionsService, useValue: positions },
        { provide: TradePlanService, useValue: plans },
        { provide: WatchlistService, useValue: watchlists },
        { provide: NotificationService, useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['error', 'success']) },
        { provide: CompanyDossierService, useValue: jasmine.createSpyObj<CompanyDossierService>('CompanyDossierService', ['get']) },
        { provide: AuthService, useValue: { getToken: () => null } },
      ],
    });
    fixture = TestBed.createComponent(MarketDataComponent);
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const chips = () => Array.from(el().querySelectorAll('[data-testid="quick-chip"]')) as HTMLButtonElement[];
  const chipLabels = () => chips().map(c => (c.textContent ?? '').trim());
  const input = () => el().querySelector('input[data-testid="quick-board-input"]') as HTMLInputElement;
  const clickChip = (startsWith: string) => {
    chips().find(c => (c.textContent ?? '').trim().startsWith(startsWith))!.click();
    fixture.detectChanges();
  };
  const lastBatch = () => market.getBatchPrices.calls.mostRecent().args[0];

  beforeEach(() => { try { localStorage.removeItem(KEY); } catch { /* bỏ qua */ } });
  afterEach(() => { fixture?.destroy(); try { localStorage.removeItem(KEY); } catch { /* bỏ qua */ } });

  describe('nhớ danh sách', () => {
    it('restores_saved_symbols_on_open', () => {
      localStorage.setItem(KEY, 'HPG,SSI,ACB');
      setup();

      expect(fixture.componentInstance.batchSymbols).toBe('HPG,SSI,ACB');
      expect(market.getBatchPrices).toHaveBeenCalledWith(['HPG', 'SSI', 'ACB']);
    });

    it('falls_back_to_default_when_storage_empty_or_blocked', () => {
      setup();
      expect(fixture.componentInstance.batchSymbols).toBe(DEFAULT);
      expect(market.getBatchPrices).toHaveBeenCalledWith(DEFAULT.split(','));
      fixture.destroy();

      localStorage.setItem(KEY, '  ,, ');   // chỉ có dấu phẩy và khoảng trắng: coi như rỗng
      TestBed.resetTestingModule();
      setup();
      expect(fixture.componentInstance.batchSymbols).toBe(DEFAULT);
      fixture.destroy();

      const original = Object.getOwnPropertyDescriptor(window, 'localStorage')!;
      Object.defineProperty(window, 'localStorage', { configurable: true, get: () => { throw new Error('bị chặn'); } });
      try {
        TestBed.resetTestingModule();
        expect(() => setup()).not.toThrow();
        expect(fixture.componentInstance.batchSymbols).toBe(DEFAULT);
      } finally {
        Object.defineProperty(window, 'localStorage', original);
      }
    });

    it('saves_after_successful_load', () => {
      setup();
      fixture.componentInstance.batchSymbols = ' vnm, fpt ,VNM,,ssi ';

      fixture.componentInstance.loadBatchPrices();

      expect(localStorage.getItem(KEY)).toBe('VNM,FPT,SSI');   // chuẩn hoá: hoa, bỏ trống, bỏ trùng
    });

    it('input_shows_normalised_list_after_load', async () => {
      setup();
      fixture.componentInstance.batchSymbols = 'fpt, hpg, FPT';

      fixture.componentInstance.loadBatchPrices();
      fixture.detectChanges();
      await fixture.whenStable();

      expect(fixture.componentInstance.batchSymbols).toBe('FPT,HPG');
      expect(input().value).toBe('FPT,HPG');
    });

    it('does not save a list that returned no prices (typo list)', () => {
      setup({ batch: of([]) });
      localStorage.removeItem(KEY);
      fixture.componentInstance.batchSymbols = 'XYZ12';

      fixture.componentInstance.loadBatchPrices();

      expect(localStorage.getItem(KEY)).toBeNull();
    });

    it('keeps the typed text when the load fails', () => {
      setup({ batch: throwError(() => new Error('x')) });
      fixture.componentInstance.batchSymbols = 'fpt, hpg, FPT';

      fixture.componentInstance.loadBatchPrices();

      expect(fixture.componentInstance.batchSymbols).toBe('fpt, hpg, FPT');
    });

    it('does not save when the load fails', () => {
      setup({ batch: throwError(() => new Error('x')) });
      localStorage.removeItem(KEY);
      fixture.componentInstance.batchSymbols = 'ACB,MBB';

      fixture.componentInstance.loadBatchPrices();

      expect(localStorage.getItem(KEY)).toBeNull();
    });

    it('does not break when saving is blocked', () => {
      setup();
      const original = Object.getOwnPropertyDescriptor(window, 'localStorage')!;
      Object.defineProperty(window, 'localStorage', { configurable: true, get: () => { throw new Error('bị chặn'); } });
      try {
        fixture.componentInstance.batchSymbols = 'ACB';
        expect(() => fixture.componentInstance.loadBatchPrices()).not.toThrow();
        expect(fixture.componentInstance.batchPrices.length).toBe(1);   // bảng vẫn hiện dù không lưu được
      } finally {
        Object.defineProperty(window, 'localStorage', original);
      }
    });
  });

  describe('gợi ý xem nhanh hôm nay', () => {
    it('shows the three kinds of chips with counts, above the input', () => {
      setup({
        positions: of([position('HPG'), position('FPT'), position('hpg')]),
        plans: of([plan('VNM'), plan('MWG')]),
        watchlists: of([wl('w1', 'Ngân hàng', 4, '🏦')]),
      });

      expect(chipLabels()).toEqual(['Đang nắm giữ (2)', 'Mã có kế hoạch mở (2)', '🏦 Ngân hàng (4)']);
      expect(el().textContent).toContain('Gợi ý xem nhanh hôm nay');
    });

    it('chip_fills_input_dedupes_and_loads', async () => {
      setup({ positions: of([position('hpg'), position('FPT'), position('HPG'), position(' fpt ')]) });

      clickChip('Đang nắm giữ');
      await fixture.whenStable();   // ngModel ghi vào ô nhập ở microtask kế tiếp

      expect(input().value).toBe('HPG,FPT');
      expect(fixture.componentInstance.batchSymbols).toBe('HPG,FPT');
      expect(lastBatch()).toEqual(['HPG', 'FPT']);
      expect(localStorage.getItem(KEY)).toBe('HPG,FPT');   // và được nhớ
    });

    it('open plans chip loads the plan symbols', () => {
      setup({ plans: of([plan('vnm'), plan('MWG')]) });

      clickChip('Mã có kế hoạch mở');

      expect(lastBatch()).toEqual(['VNM', 'MWG']);
      expect(plans.getAll).toHaveBeenCalledWith(true);
    });

    it('a watchlist chip loads the watchlist detail on click, not before', () => {
      setup({
        watchlists: of([wl('w1', 'Ngân hàng', 3)]),
        details: { w1: of(wlDetail('w1', ['vcb', 'ACB', 'ACB'])) },
      });
      expect(watchlists.getDetail).not.toHaveBeenCalled();

      clickChip('⭐ Ngân hàng');

      expect(watchlists.getDetail).toHaveBeenCalledWith('w1');
      expect(lastBatch()).toEqual(['VCB', 'ACB']);
    });

    it('empty_source_hides_chip (no positions, no plans, empty watchlists)', () => {
      setup({ positions: of([]), plans: of([]), watchlists: of([wl('w1', 'Trống', 0), wl('w2', 'Có mã', 2)]) });

      expect(chipLabels()).toEqual(['⭐ Có mã (2)']);
    });

    it('no chips and no hint at all when every source is empty', () => {
      setup();

      expect(chips().length).toBe(0);
      expect(el().textContent).not.toContain('Gợi ý xem nhanh hôm nay');
    });

    it('failing_source_hides_only_that_chip (silently, the board still works)', () => {
      setup({
        positions: throwError(() => new Error('x')),
        plans: of([plan('VNM')]),
        watchlists: throwError(() => new Error('y')),
      });

      expect(chipLabels()).toEqual(['Mã có kế hoạch mở (1)']);
      expect(market.getBatchPrices).toHaveBeenCalled();   // bảng vẫn tải
      expect((TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>).error).not.toHaveBeenCalled();
    });

    it('failing plans source hides only the plans chip', () => {
      setup({
        positions: of([position('HPG')]),
        plans: throwError(() => new Error('x')),
        watchlists: of([wl('w1', 'Ngân hàng', 2)]),
      });

      expect(chipLabels()).toEqual(['Đang nắm giữ (1)', '⭐ Ngân hàng (2)']);
    });

    it('a watchlist with an empty detail leaves the input and the board untouched', () => {
      setup({ watchlists: of([wl('w1', 'Rỗng', 2)]), details: { w1: of(wlDetail('w1', [])) } });
      const before = fixture.componentInstance.batchSymbols;
      const callsBefore = market.getBatchPrices.calls.count();

      clickChip('⭐ Rỗng');

      expect(fixture.componentInstance.batchSymbols).toBe(before);
      expect(market.getBatchPrices.calls.count()).toBe(callsBefore);
    });

    it('a watchlist whose detail fails to load does not break the board or notify', () => {
      setup({ watchlists: of([wl('w1', 'Hỏng', 3)]) });   // getDetail mặc định ném lỗi
      const callsBefore = market.getBatchPrices.calls.count();

      clickChip('⭐ Hỏng');

      expect(chipLabels()).toEqual([]);   // chip lỗi biến mất

      expect(market.getBatchPrices.calls.count()).toBe(callsBefore);
      expect((TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>).error).not.toHaveBeenCalled();
    });

    it('the typed list keeps working with the button, and the typed list is what gets remembered', () => {
      setup();

      input().value = 'ssi, vnd';
      input().dispatchEvent(new Event('input'));
      fixture.detectChanges();
      (Array.from(el().querySelectorAll('button')).find(b => (b.textContent ?? '').includes('Xem giá')) as HTMLButtonElement).click();

      expect(lastBatch()).toEqual(['SSI', 'VND']);
      expect(localStorage.getItem(KEY)).toBe('SSI,VND');
    });
  });
});
