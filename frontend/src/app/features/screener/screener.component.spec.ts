import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import { ScreenerComponent } from './screener.component';
import { MarketDataService, ScreenerPassedRow, ScreenerQuery, ScreenerResult } from '../../core/services/market-data.service';
import { AddItemsResult, WatchlistService, WatchlistSummary } from '../../core/services/watchlist.service';

const KEY = 'imv2.quickBoard.symbols';
const DISCLAIMER = 'Danh sách lọc theo tiêu chí, không phải khuyến nghị mua. Dữ liệu từ nguồn công khai không chính thức, có thể trễ. Cờ dựa trên xếp hạng tương đối, không phải đánh giá rủi ro.';

const row = (symbol: string, over: Partial<ScreenerPassedRow> = {}): ScreenerPassedRow => ({
  symbol, floor: 'HOSE', sector: '8000', adtvBn: 12.5, marketCapBn: 20000, priceVnd: 50000, roe: 18, pe: 12, rs3m: 70,
  reasons: ['Thanh khoản đạt'], flags: [], notes: [], ...over,
});

const result = (over: Partial<ScreenerResult> = {}): ScreenerResult => ({
  status: 'ok', stale: false, asOf: '2026-11-28T12:00:00+00:00', hoseListsStale: false, universeCount: 1800,
  passed: [row('AAA'), row('BBB'), row('CCC')], unverifiedHose: [], excludedCounts: { 'ROE thấp': 64, 'Thanh khoản thấp': 549 },
  sectorCounts: { '8000': { passed: 3, excluded: 10 } }, tradeoffCount: 0, notes: [], disclaimer: DISCLAIMER, ...over,
});

const list = (id: string, name: string, isDefault = false): WatchlistSummary =>
  ({ id, name, emoji: '⭐', isDefault, sortOrder: 0, itemCount: 0, createdAt: '', updatedAt: '' });

describe('ScreenerComponent: Sàng lọc cổ phiếu', () => {
  let fixture: ComponentFixture<ScreenerComponent>;
  let market: jasmine.SpyObj<MarketDataService>;
  let watchlists: jasmine.SpyObj<WatchlistService>;
  let router: Router;

  function setup(res: Observable<ScreenerResult> = of(result()), lists: WatchlistSummary[] = [list('w-other', 'Khác'), list('w-def', 'Mặc định', true)]) {
    market = jasmine.createSpyObj<MarketDataService>('MarketDataService', ['getScreener']);
    market.getScreener.and.returnValue(res);
    watchlists = jasmine.createSpyObj<WatchlistService>('WatchlistService', ['getAll', 'addItemsBulk']);
    watchlists.getAll.and.returnValue(of(lists));
    TestBed.configureTestingModule({
      imports: [ScreenerComponent],
      providers: [provideRouter([]), { provide: MarketDataService, useValue: market }, { provide: WatchlistService, useValue: watchlists }],
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(ScreenerComponent);
    fixture.detectChanges();
  }

  const el = () => fixture.nativeElement as HTMLElement;
  const q = (id: string) => el().querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const qa = (id: string) => Array.from(el().querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[];
  const symbols = () => qa('screener-symbol').map(s => (s.textContent ?? '').trim());
  const lastQuery = (): ScreenerQuery => market.getScreener.calls.mostRecent().args[0];
  const click = (id: string) => { q(id)!.click(); fixture.detectChanges(); };
  const type = (id: string, value: string) => {
    const input = q(id) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  beforeEach(() => { try { localStorage.removeItem(KEY); } catch { /* bỏ qua */ } });
  afterEach(() => { fixture?.destroy(); try { localStorage.removeItem(KEY); } catch { /* bỏ qua */ } });

  describe('tải và hiển thị', () => {
    it('loads once on open with the default criteria (HOSE+HNX, UPCOM off, no P/E cap)', () => {
      setup();

      expect(market.getScreener).toHaveBeenCalledTimes(1);
      expect(lastQuery().floors).toBe('HOSE,HNX');
      expect(lastQuery().includeUpcom).toBeFalse();
      expect(lastQuery().maxPe).toBeUndefined();
      expect(lastQuery().skipQualityLayer).toBeFalse();
    });

    it('shows the passed rows in the order the API returned (alphabetical), with no score or rank column', () => {
      setup();

      expect(symbols()).toEqual(['AAA', 'BBB', 'CCC']);
      const headers = qa('screener-th').map(h => (h.textContent ?? '').trim());
      expect(headers.some(h => /điểm|hạng\b|tiềm năng/i.test(h) && !/RS/.test(h))).toBeFalse();
    });

    it('always shows the disclaimer: when ok, when unavailable and when the call fails', () => {
      setup();
      expect(q('screener-disclaimer')!.textContent).toContain('không phải khuyến nghị mua');
      expect(q('screener-disclaimer')!.textContent).toContain('không phải đánh giá rủi ro');
      fixture.destroy();
      TestBed.resetTestingModule();

      setup(of(result({ status: 'unavailable', passed: [], reason: 'Dữ liệu sàng lọc quá cũ' })));
      expect(q('screener-disclaimer')!.textContent).toContain('không phải khuyến nghị mua');
      fixture.destroy();
      TestBed.resetTestingModule();

      setup(throwError(() => new Error('x')));
      expect(q('screener-disclaimer')!.textContent).toContain('không phải khuyến nghị mua');
    });

    it('shows the exclusion counts per reason and the sector counts', () => {
      setup();

      const counts = qa('screener-excluded-count').map(c => (c.textContent ?? '').replace(/\s+/g, ' ').trim());
      expect(counts).toContain('Thanh khoản thấp: 549');
      expect(counts).toContain('ROE thấp: 64');
      expect(q('screener-sectors')!.textContent).toContain('8000');
    });

    it('shows the trade-off line only when there are such symbols', () => {
      setup(of(result({ tradeoffCount: 36 })));
      expect(q('screener-tradeoff')!.textContent).toContain('36');
      expect(q('screener-tradeoff')!.textContent).toContain('xu hướng tốt');
      fixture.destroy();
      TestBed.resetTestingModule();

      setup(of(result({ tradeoffCount: 0 })));
      expect(q('screener-tradeoff')).toBeNull();
    });

    it('stale data shows a banner with the as-of time', () => {
      setup(of(result({ status: 'stale', stale: true })));

      expect(q('screener-stale')!.textContent).toContain('Dữ liệu cũ');
      expect(q('screener-stale')!.textContent).toMatch(/28\/11\/2026/);
      expect(symbols().length).toBe(3);
    });

    it('unavailable shows the reason and no table', () => {
      setup(of(result({ status: 'unavailable', passed: [], reason: 'Dữ liệu sàng lọc quá cũ', excludedCounts: {} })));

      expect(q('screener-unavailable')!.textContent).toContain('Dữ liệu sàng lọc quá cũ');
      expect(q('screener-table')).toBeNull();
    });

    it('a failed call shows a message and keeps the page usable', () => {
      setup(throwError(() => new Error('x')));

      expect(q('screener-error')!.textContent).toContain('Không tải được');
      expect(q('screener-table')).toBeNull();
    });

    it('no rows passing says so instead of showing an empty table', () => {
      setup(of(result({ passed: [] })));

      expect(q('screener-empty')!.textContent).toContain('Không có mã nào');
    });

    it('unverified HOSE symbols are listed separately, never in the passed table', () => {
      setup(of(result({ passed: [row('AAA')], unverifiedHose: [row('HHH')], notes: ['Chưa kiểm được trạng thái (HOSE): lý do'] })));

      expect(symbols()).toEqual(['AAA']);
      const group = q('screener-unverified')!;
      expect(group.textContent).toContain('Chưa kiểm được trạng thái (HOSE)');
      expect(group.textContent).toContain('HHH');
    });

    it('shows volatility flags with their values and per-row notes', () => {
      setup(of(result({ passed: [row('AAA', { flags: ['RS1T 96/99, vốn hoá nhỏ'], notes: ['chưa kiểm được danh sách cảnh báo HNX'] })] })));

      expect(q('screener-flag')!.textContent).toContain('RS1T 96/99, vốn hoá nhỏ');
      expect(q('screener-row-note')!.textContent).toContain('chưa kiểm được danh sách cảnh báo HNX');
      expect(el().textContent).not.toContain('đầu cơ');
    });

    it('renders server strings as text only: markup in a reason or note never becomes an element', () => {
      setup(of(result({ passed: [row('AAA', { reasons: ['<img src=x onerror=alert(1)>Lý do'], notes: ['<b>đậm</b>'] })] })));

      expect(el().querySelector('img')).toBeNull();
      expect(el().querySelector('[data-testid="screener-row-note"] b')).toBeNull();
      expect(el().textContent).toContain('<img src=x onerror=alert(1)>Lý do');
    });

    it('the trend control is worded as a percentile rank over all listed symbols, never as beating the market', () => {
      setup();

      const text = el().textContent ?? '';
      expect(text).toContain('xếp hạng sức mạnh 3 tháng');
      expect(text).toContain('toàn bộ mã niêm yết');
      expect(text).not.toContain('mạnh hơn thị trường');
      expect(text).not.toContain('VN-Index');
    });
  });

  describe('tiêu chí', () => {
    it('ticking UPCOM, setting a P/E cap and skipping the quality layer are sent on the next run', () => {
      setup();

      (q('screener-floor-upcom') as HTMLInputElement).click();
      type('screener-max-pe', '25');
      (q('screener-skip-quality') as HTMLInputElement).click();
      fixture.detectChanges();
      click('screener-run');

      const query = lastQuery();
      expect(query.floors).toBe('HOSE,HNX,UPCOM');
      expect(query.includeUpcom).toBeTrue();
      expect(query.maxPe).toBe(25);
      expect(query.skipQualityLayer).toBeTrue();
    });

    it('an empty P/E cap stays off (not sent)', () => {
      setup();
      type('screener-max-pe', '25');
      type('screener-max-pe', '');
      click('screener-run');

      expect(lastQuery().maxPe).toBeUndefined();
    });

    it('clearing the rank box never prints "Pnull" in the hint and the run falls back to the default', () => {
      setup();
      type('screener-min-rs3m', '');

      expect(el().textContent).not.toContain('null');
      expect(el().textContent).not.toContain('Pundefined');
      click('screener-run');
      expect(lastQuery().minRs3m).toBeUndefined();
    });

    it('numeric thresholds are sent as numbers', () => {
      setup();
      type('screener-min-adtv', '8');
      type('screener-min-roe', '15');
      type('screener-min-rs3m', '60');
      click('screener-run');

      expect(lastQuery().minAdtv).toBe(8);
      expect(lastQuery().minRoe).toBe(15);
      expect(lastQuery().minRs3m).toBe(60);
    });

    it('with every floor unticked the run is refused with a message and nothing is sent', async () => {
      setup();
      await fixture.whenStable();   // ngModel ghi giá trị ban đầu vào ô chọn ở microtask kế tiếp
      const calls = market.getScreener.calls.count();
      (q('screener-floor-hose') as HTMLInputElement).click();
      (q('screener-floor-hnx') as HTMLInputElement).click();
      fixture.detectChanges();

      click('screener-run');

      expect(market.getScreener.calls.count()).toBe(calls);
      expect(q('screener-floor-error')!.textContent).toContain('Chọn ít nhất một sàn');
    });
  });

  describe('sắp xếp', () => {
    it('clicking a column sorts by it, again reverses, and rows with no value go last', () => {
      setup(of(result({ passed: [row('AAA', { marketCapBn: 5000 }), row('BBB', { marketCapBn: 30000 }), row('CCC', { marketCapBn: 12000 }), row('DDD', { roe: null, marketCapBn: 99999 })] })));

      click('screener-sort-cap');
      expect(symbols()).toEqual(['AAA', 'CCC', 'BBB', 'DDD']);
      click('screener-sort-cap');
      expect(symbols()).toEqual(['DDD', 'BBB', 'CCC', 'AAA']);
      click('screener-sort-roe');
      expect(symbols().at(-1)).toBe('DDD');
    });

    it('the symbol column returns to alphabetical', () => {
      setup(of(result({ passed: [row('CCC'), row('AAA'), row('BBB')] })));

      click('screener-sort-symbol');

      expect(symbols()).toEqual(['AAA', 'BBB', 'CCC']);
    });
  });

  describe('gửi sang Bảng giá nhanh', () => {
    const tick = (symbol: string) => {
      (qa('screener-tick').find(t => t.getAttribute('data-symbol') === symbol) as HTMLInputElement).click();
      fixture.detectChanges();
    };

    it('sending ticked symbols saves them for the quick board and opens the market page', () => {
      setup();
      tick('CCC');
      tick('AAA');

      click('screener-send');

      expect(localStorage.getItem(KEY)).toBe('AAA,CCC');
      expect(router.navigate).toHaveBeenCalledWith(['/market-data']);
    });

    it('nothing is ticked by default and the send button is disabled', () => {
      setup();

      expect((q('screener-send') as HTMLButtonElement).disabled).toBeTrue();
      expect(qa('screener-tick').every(t => !(t as HTMLInputElement).checked)).toBeTrue();
    });

    it('the selection is capped at 30 symbols with a message', () => {
      const many = Array.from({ length: 35 }, (_, i) => row(`S${String(i).padStart(2, '0')}`));
      setup(of(result({ passed: many })));

      many.forEach(r => tick(r.symbol));

      expect(q('screener-send')!.textContent).toContain('30');
      expect(q('screener-selection-cap')!.textContent).toContain('tối đa 30');
      click('screener-send');
      expect(localStorage.getItem(KEY)!.split(',').length).toBe(30);
    });

    it('blocked storage: no silent loss. The page says so, keeps the list on screen and does not navigate', async () => {
      setup();
      tick('CCC');
      tick('AAA');
      const original = Object.getOwnPropertyDescriptor(window, 'localStorage')!;
      Object.defineProperty(window, 'localStorage', { configurable: true, get: () => { throw new Error('bị chặn'); } });
      try {
        expect(() => click('screener-send')).not.toThrow();
        expect(router.navigate).not.toHaveBeenCalled();
        expect(q('screener-send-error')!.textContent).toContain('chặn lưu trữ');
        expect(q('screener-send-error')!.textContent).toContain('AAA,CCC');   // để người dùng tự gõ sang Bảng giá nhanh
      } finally {
        Object.defineProperty(window, 'localStorage', original);
      }
    });

    it('a successful send shows no error', () => {
      setup();
      tick('AAA');

      click('screener-send');

      expect(q('screener-send-error')).toBeNull();
    });

    it('a new run clears the ticks of symbols that are no longer listed', () => {
      const next = new Subject<ScreenerResult>();
      setup();
      tick('AAA');
      market.getScreener.and.returnValue(next);

      click('screener-run');
      next.next(result({ passed: [row('BBB')] }));
      next.complete();
      fixture.detectChanges();

      expect((q('screener-send') as HTMLButtonElement).disabled).toBeTrue();
    });
  });

  describe('Vì sao mã X không có?', () => {
    const lookup = (state: string, reason: string | undefined, symbol = 'XYZ') => {
      setup();
      market.getScreener.and.returnValue(of(result({ lookup: { symbol, state, reason } })));
      type('screener-lookup-input', symbol.toLowerCase());
      click('screener-lookup-run');
    };

    it('sends the symbol with the current criteria and shows the exclusion reason', () => {
      lookup('excluded', 'ROE 3%, dưới 12%', 'AAA');

      expect(lastQuery().lookupSymbol).toBe('AAA');
      expect(q('screener-lookup-result')!.textContent).toContain('Bị loại');
      expect(q('screener-lookup-result')!.textContent).toContain('ROE 3%, dưới 12%');
    });

    it('explains the passed, unverified and not-in-universe states', () => {
      lookup('passed', undefined, 'AAA');
      expect(q('screener-lookup-result')!.textContent).toContain('Có trong danh sách');
      fixture.destroy();
      TestBed.resetTestingModule();

      lookup('unverified_hose', undefined, 'HHH');
      expect(q('screener-lookup-result')!.textContent).toContain('chưa kiểm được trạng thái (HOSE)');
      fixture.destroy();
      TestBed.resetTestingModule();

      lookup('not_in_universe', 'Mã không có trong dữ liệu sàng lọc', 'ZZZ');
      expect(q('screener-lookup-result')!.textContent).toContain('Mã không có trong dữ liệu sàng lọc');
    });

    it('explains against the criteria of the list on screen, not against edits not yet applied', () => {
      setup();
      type('screener-min-roe', '12');
      click('screener-run');                         // bảng đang hiện ứng với ROE 12
      type('screener-min-roe', '30');                // sửa ô nhưng chưa bấm Lọc
      market.getScreener.and.returnValue(of(result({ lookup: { symbol: 'AAA', state: 'passed' } })));

      type('screener-lookup-input', 'aaa');
      click('screener-lookup-run');

      expect(lastQuery().lookupSymbol).toBe('AAA');
      expect(lastQuery().minRoe).toBe(12);
    });

    it('does not replace the main list with the lookup answer', () => {
      lookup('excluded', 'x', 'AAA');

      expect(symbols()).toEqual(['AAA', 'BBB', 'CCC']);
    });

    it('an empty box sends nothing', () => {
      setup();
      const calls = market.getScreener.calls.count();

      click('screener-lookup-run');

      expect(market.getScreener.calls.count()).toBe(calls);
    });

    it('a failing lookup shows a message and keeps the page', () => {
      setup();
      market.getScreener.and.returnValue(throwError(() => new Error('x')));
      type('screener-lookup-input', 'aaa');
      click('screener-lookup-run');

      expect(q('screener-lookup-error')!.textContent).toContain('Không tra cứu được');
      expect(symbols().length).toBe(3);
    });
  });
  describe('thêm vào watchlist', () => {
    const tick = (symbol: string) => {
      const box = el().querySelector(`[data-testid="screener-tick"][data-symbol="${symbol}"]`) as HTMLInputElement;
      box.checked = true;
      box.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const added = (over: Partial<AddItemsResult> = {}): AddItemsResult => ({ watchlistId: 'w-def', added: ['AAA', 'CCC'], skipped: [], ...over });
    const msg = () => (q('screener-watchlist-result')?.textContent ?? '').replace(/\s+/g, ' ').trim();

    it('the button is disabled until something is ticked and says how many will be added', () => {
      setup();
      expect((q('screener-watchlist-add') as HTMLButtonElement).disabled).toBeTrue();

      tick('AAA'); tick('CCC');

      expect((q('screener-watchlist-add') as HTMLButtonElement).disabled).toBeFalse();
      expect(q('screener-watchlist-add')!.textContent).toContain('Thêm 2 mã vào watchlist');
    });

    /** Màu thật người xem thấy: chữ và nền (nền trong suốt thì trộn lên nền trang), đo bằng style tính toán của trình duyệt. */
    const measure = (btn: HTMLElement, dark: boolean): number => {
      const root = document.documentElement;
      const had = root.classList.contains('dark');
      root.classList.toggle('dark', dark);
      try {
        const parse = (c: string): [number, number, number, number] => {
          const m = c.match(/[\d.]+/g)!.map(Number);
          return [m[0], m[1], m[2], m[3] ?? 1];
        };
        const lum = ([r, g, b]: number[]) => {
          const f = (v: number) => { const x = v / 255; return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4); };
          return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
        };
        const page = dark ? [17, 24, 39] : [255, 255, 255];
        const cs = getComputedStyle(btn);
        const bgRaw = parse(cs.backgroundColor);
        const bg = bgRaw[3] < 1 ? [0, 1, 2].map(i => bgRaw[i] * bgRaw[3] + page[i] * (1 - bgRaw[3])) : bgRaw.slice(0, 3);
        const fg = parse(cs.color).slice(0, 3);
        const [hi, lo] = [lum(bg), lum(fg)].sort((x, y) => y - x);
        return (hi + 0.05) / (lo + 0.05);
      } finally { root.classList.toggle('dark', had); }
    };

    it('the add button meets AA contrast (>= 4.5:1) in the light and in the dark theme', () => {
      setup();
      tick('AAA');
      const btn = q('screener-watchlist-add') as HTMLButtonElement;
      expect(btn.disabled).toBeFalse();

      expect(measure(btn, false)).withContext('sáng').toBeGreaterThanOrEqual(4.5);
      expect(measure(btn, true)).withContext('tối').toBeGreaterThanOrEqual(4.5);
    });

    it('at 400px the add button stays on one line (not a tall wrapped box) and inside the page', () => {
      setup();
      const host = el();
      host.style.display = 'block';
      host.style.width = '400px';
      document.body.appendChild(host);
      tick('AAA');
      tick('BBB');

      const btn = q('screener-watchlist-add') as HTMLButtonElement;
      const r = btn.getBoundingClientRect();
      expect(r.height).withContext('một dòng').toBeLessThanOrEqual(44);
      expect(r.right).withContext('trong khung 400px').toBeLessThanOrEqual(host.getBoundingClientRect().right + 0.5);
    });

    it('the picker defaults to the default watchlist', () => {
      setup();

      expect((q('screener-watchlist-pick') as HTMLSelectElement).value).toBe('w-def');
    });

    it('adds the ticked symbols (sorted) to the chosen watchlist with an auto note "Từ sàng lọc dd/MM · tiêu chí"', () => {
      setup();
      watchlists.addItemsBulk.and.returnValue(of(added()));
      tick('CCC'); tick('AAA');

      click('screener-watchlist-add');

      expect(watchlists.addItemsBulk).toHaveBeenCalledTimes(1);
      const [id, symbols, note] = watchlists.addItemsBulk.calls.mostRecent().args;
      expect(id).toBe('w-def');
      expect(symbols).toEqual(['AAA', 'CCC']);
      const d = new Date();
      const dd = `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
      expect(note).toContain(`Từ sàng lọc ${dd}`);
      expect(note).toContain('ROE ≥ 12');
      expect((note as string).length).toBeLessThanOrEqual(200);
    });

    it('a different pick sends to that watchlist', () => {
      setup();
      watchlists.addItemsBulk.and.returnValue(of(added({ watchlistId: 'w-other' })));
      tick('AAA');
      const pick = q('screener-watchlist-pick') as HTMLSelectElement;
      pick.value = 'w-other';
      pick.dispatchEvent(new Event('change'));
      fixture.detectChanges();

      click('screener-watchlist-add');

      expect(watchlists.addItemsBulk.calls.mostRecent().args[0]).toBe('w-other');
    });

    it('shows "Đã thêm N, bỏ qua M (đã có)" and marks the rows with a star', () => {
      setup();
      watchlists.addItemsBulk.and.returnValue(of(added({ added: ['AAA'], skipped: ['CCC'] })));
      tick('AAA'); tick('CCC');

      click('screener-watchlist-add');

      expect(msg()).toContain('Đã thêm 1, bỏ qua 1 (đã có)');
      const starred = qa('screener-in-watchlist').map(s => s.dataset['symbol']);
      expect(starred.sort()).toEqual(['AAA', 'CCC']);
    });

    it('rows the server already flags inWatchlist show a star from the start, the others do not', () => {
      setup(of(result({ passed: [row('AAA', { inWatchlist: true }), row('BBB'), row('CCC', { inWatchlist: false })] })));

      expect(qa('screener-in-watchlist').map(s => s.dataset['symbol'])).toEqual(['AAA']);
      expect(qa('screener-in-watchlist')[0].getAttribute('aria-label')).toContain('Đã có trong watchlist');
    });

    it('the star sits OUTSIDE the symbol link so a click on the symbol still goes to the timeline', () => {
      setup(of(result({ passed: [row('AAA', { inWatchlist: true })] })));

      const star = qa('screener-in-watchlist')[0];
      expect(star.closest('[data-testid="screener-symbol"]')).toBeNull();
    });

    it('a failure shows a Vietnamese message and keeps the selection so the user can retry', () => {
      setup();
      watchlists.addItemsBulk.and.returnValue(throwError(() => ({ status: 500 })));
      tick('AAA');

      click('screener-watchlist-add');

      expect(q('screener-watchlist-error')!.textContent).toContain('Không thêm được');
      expect((q('screener-watchlist-add') as HTMLButtonElement).disabled).toBeFalse();
      expect(q('screener-watchlist-add')!.textContent).toContain('Thêm 1 mã');
    });

    it('with no watchlist at all the button stays disabled and points to the Watchlist page', () => {
      setup(of(result()), []);
      tick('AAA');

      expect((q('screener-watchlist-add') as HTMLButtonElement).disabled).toBeTrue();
      expect(q('screener-watchlist-none')!.textContent).toContain('Chưa có watchlist');
    });

    it('a successful add clears the ticks that were sent', () => {
      setup();
      watchlists.addItemsBulk.and.returnValue(of(added()));
      tick('AAA'); tick('CCC');

      click('screener-watchlist-add');

      expect((q('screener-watchlist-add') as HTMLButtonElement).disabled).toBeTrue();
    });

    it('the button cannot fire twice while a request is in flight', () => {
      setup();
      const pending = new Subject<AddItemsResult>();
      watchlists.addItemsBulk.and.returnValue(pending);
      tick('AAA');

      click('screener-watchlist-add');
      expect((q('screener-watchlist-add') as HTMLButtonElement).disabled).withContext('nút khoá trong lúc chờ').toBeTrue();
      fixture.componentInstance.addToWatchlist();   // và chính hàm cũng tự chặn, không chỉ nhờ nút bị khoá

      expect(watchlists.addItemsBulk).toHaveBeenCalledTimes(1);
    });
  });
});
