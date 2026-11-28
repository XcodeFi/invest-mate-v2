import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { MoodJournalComponent } from './mood-journal.component';
import { MoodHistoryDto, MoodService } from '../../core/services/mood.service';

const HISTORY: MoodHistoryDto = {
  from: '2026-11-23', to: '2026-11-29',
  days: [
    { dateKey: '2026-11-23', mood: 'Calm', changes: [{ at: '2026-11-23T02:00:00Z', mood: 'Calm', source: 'web' }], overrode: false, tradeCount: 0, emotionalTrade: false },
    { dateKey: '2026-11-24', mood: 'Fomo', changes: [{ at: '2026-11-24T02:00:00Z', mood: 'Calm', source: 'web' }, { at: '2026-11-24T08:00:00Z', mood: 'Fomo', source: 'mcp' }], overrode: true, tradeCount: 2, emotionalTrade: true },
    { dateKey: '2026-11-25', mood: 'Fear', changes: [], overrode: false, tradeCount: 0, emotionalTrade: false },
    { dateKey: '2026-11-26', mood: 'Revenge', changes: [], overrode: false, tradeCount: 1, emotionalTrade: true },
    { dateKey: '2026-11-27', mood: null, changes: [], overrode: false, tradeCount: 1, emotionalTrade: false },
  ],
};

describe('MoodJournalComponent (/mood-journal)', () => {
  let fixture: ComponentFixture<MoodJournalComponent>;
  let service: jasmine.SpyObj<MoodService>;

  function setup(result: 'ok' | 'error' = 'ok', history: MoodHistoryDto = HISTORY) {
    service = jasmine.createSpyObj<MoodService>('MoodService', ['getHistory']);
    service.getHistory.and.returnValue(result === 'ok' ? of(history) : throwError(() => new Error('x')));
    TestBed.configureTestingModule({ imports: [MoodJournalComponent], providers: [provideRouter([]), { provide: MoodService, useValue: service }] });
    fixture = TestBed.createComponent(MoodJournalComponent);
    fixture.detectChanges();
  }

  const q = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const all = (id: string) => Array.from(fixture.nativeElement.querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[];

  afterEach(() => fixture?.destroy());

  it('asks the server for the default range (no from/to: 90 VN days)', () => {
    setup();

    expect(service.getHistory).toHaveBeenCalledOnceWith();
  });

  it('draws one cell per day with its own symbol, and an empty day is □ "chưa chấm"', () => {
    setup();

    const cells = all('mj-cell');
    expect(cells.map(c => c.getAttribute('data-date'))).toEqual(['2026-11-23', '2026-11-24', '2026-11-25', '2026-11-26', '2026-11-27', '2026-11-28', '2026-11-29']);
    expect(cells.map(c => c.querySelector('[data-testid="mj-glyph"]')!.textContent!.trim())).toEqual(['■', '▲', '▼', '✕', '□', '□', '□']);
    expect(cells[5].getAttribute('aria-label')).toContain('chưa chấm');
  });

  it('each cell has an aria-label and a tooltip with the date, mood, changes (with the agent marked) and trades', () => {
    setup();

    const fomo = all('mj-cell')[1];
    expect(fomo.getAttribute('aria-label')).toContain('24/11/2026');
    expect(fomo.getAttribute('aria-label')).toContain('FOMO');
    expect(fomo.getAttribute('aria-label')).toContain('(từ agent)');
    expect(fomo.getAttribute('aria-label')).toContain('2 lệnh');
    expect(fomo.getAttribute('title')).toBe(fomo.getAttribute('aria-label'));
    expect(fomo.getAttribute('tabindex')).toBe('0');
  });

  it('shows the count per mood and for "chưa chấm"', () => {
    setup();

    expect(q('mj-count-Calm')!.textContent).toContain('1');
    expect(q('mj-count-Fomo')!.textContent).toContain('1');
    expect(q('mj-count-Fear')!.textContent).toContain('1');
    expect(q('mj-count-Revenge')!.textContent).toContain('1');
    expect(q('mj-count-unrated')!.textContent).toContain('3');
  });

  it('lists the days a trade was placed while emotional, newest first, each linking to the trades page', () => {
    setup();

    const rows = all('mj-emotional-row');
    expect(rows.map(r => r.getAttribute('data-date'))).toEqual(['2026-11-26', '2026-11-24']);
    expect(rows[0].textContent).toContain('1 lệnh');
    expect(rows[1].textContent).toContain('FOMO');
    expect(rows[0].querySelector('a')!.getAttribute('href')).toBe('/trades');
  });

  it('says so when no trade was placed while emotional', () => {
    setup('ok', { ...HISTORY, days: HISTORY.days.map(d => ({ ...d, emotionalTrade: false })) });

    expect(all('mj-emotional-row').length).toBe(0);
    expect(q('mj-emotional-empty')!.textContent).toContain('Không có');
  });

  it('clicking a cell shows its detail below the strip', () => {
    setup();

    all('mj-cell')[1].click();
    fixture.detectChanges();

    expect(q('mj-detail')!.textContent).toContain('24/11/2026');
    expect(q('mj-detail')!.textContent).toContain('15:00 FOMO (sợ bỏ lỡ) (từ agent)');   // 08:00Z = 15:00 giờ VN
  });

  it('"Xem bảng" shows the same numbers as the strip: one row per day with a record, same moods, trades and counts', () => {
    setup();

    (q('mj-table-toggle') as HTMLButtonElement).click();
    fixture.detectChanges();

    const rows = all('mj-table-row');
    expect(rows.length).toBe(5);
    expect(rows.map(r => r.getAttribute('data-date'))).toEqual(['2026-11-23', '2026-11-24', '2026-11-25', '2026-11-26', '2026-11-27']);
    expect(rows[1].textContent).toContain('FOMO');
    expect(rows[1].textContent).toContain('2');
    expect(rows[4].textContent).toContain('Chưa chấm');
    expect(q('mj-table-toggle')!.textContent).toContain('Xem biểu đồ');
  });

  it('an empty history still draws the strip of □ and says nothing has been rated yet', () => {
    setup('ok', { from: '2026-11-23', to: '2026-11-25', days: [] });

    expect(all('mj-cell').length).toBe(3);
    expect(q('mj-empty')!.textContent).toContain('Chưa có ngày nào được chấm');
    expect(q('mj-count-unrated')!.textContent).toContain('3');
  });

  it('in a narrow container the strip opens scrolled to the latest week, so today is visible', async () => {
    setup('ok', { from: '2026-08-31', to: '2026-11-28', days: [] });   // 90 ngày
    const host = fixture.nativeElement as HTMLElement;
    const box = document.createElement('div');
    box.style.cssText = 'position:absolute;left:0;top:0;width:360px';
    document.body.appendChild(box);
    box.appendChild(host);
    fixture.destroy();
    fixture = TestBed.createComponent(MoodJournalComponent);
    box.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await new Promise(r => setTimeout(r));

    const scroller = q('mj-scroller')!;
    const last = all('mj-cell').pop()!.getBoundingClientRect();
    const view = scroller.getBoundingClientRect();
    expect(scroller.scrollWidth).withContext('điều kiện phép thử: lưới rộng hơn khung').toBeGreaterThan(scroller.clientWidth);
    expect(scroller.scrollLeft).toBeGreaterThan(0);
    expect(last.right).toBeLessThanOrEqual(view.right + 1);
    expect(last.left).toBeGreaterThanOrEqual(view.left - 1);
    box.remove();
  });

  it('a load failure shows an error instead of an empty strip', () => {
    setup('error');

    expect(q('mj-error')).not.toBeNull();
    expect(all('mj-cell').length).toBe(0);
  });

  it('the legend names every symbol, so meaning never depends on colour', () => {
    setup();

    const legend = q('mj-legend')!.textContent!;
    ['■', '▲', '▼', '✕', '□', 'Bình tĩnh', 'FOMO', 'Sợ', 'Cay cú', 'Chưa chấm'].forEach(t => expect(legend).toContain(t));
  });
});
