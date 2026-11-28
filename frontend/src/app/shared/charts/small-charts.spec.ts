import { TestBed } from '@angular/core/testing';
import { DividendTimelineComponent } from './dividend-timeline.component';
import { PeerBarsComponent } from './peer-bars.component';
import { RangeBarComponent } from './range-bar.component';
import { ShareBarComponent } from './share-bar.component';
import { PeerRow } from '../../core/services/market-data.service';

const peer = (symbol: string, pe: number | null, over: Partial<PeerRow> = {}): PeerRow =>
  ({ symbol, companyName: null, price: null, pe, pb: 1.2, marketCap: null, changePercent: 1.5, ...over });

describe('PeerBarsComponent: so sánh P/E cùng ngành', () => {
  const make = (symbol: string, selfPe: number | null, peers: PeerRow[]) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [PeerBarsComponent] });
    const fixture = TestBed.createComponent(PeerBarsComponent);
    Object.assign(fixture.componentInstance, { symbol, selfPe, peers });
    fixture.detectChanges();
    return fixture;
  };
  const rows = (f: ReturnType<typeof make>) => Array.from(f.nativeElement.querySelectorAll('[data-testid="pe-row"]')) as HTMLElement[];

  it('peer_chart_highlights_company_and_skips_na_pe', () => {
    const f = make('HPG', 12, [peer('HSG', 8), peer('NKG', 20), peer('SMC', -5), peer('TLH', null)]);

    expect(rows(f).map(r => r.dataset['symbol'])).toEqual(['NKG', 'HPG', 'HSG']);     // xếp P/E cao → thấp
    const self = rows(f).find(r => r.dataset['symbol'] === 'HPG')!;
    expect(self.dataset['self']).toBe('true');
    expect(rows(f).filter(r => r.dataset['self'] === 'true').length).toBe(1);
    // P/E âm hoặc thiếu: không vẽ cột, nhưng được nêu là n/a
    const na = Array.from(f.nativeElement.querySelectorAll('[data-testid="pe-na"]')) as HTMLElement[];
    expect(na.map(n => n.dataset['symbol'])).toEqual(['SMC', 'TLH']);
    expect(na[0].textContent).toContain('n/a');
  });

  it('bar widths are proportional to P/E and the highest fills the track', () => {
    const f = make('HPG', 10, [peer('NKG', 20)]);

    const widths = Array.from(f.nativeElement.querySelectorAll('[data-testid="pe-bar"]')).map(b => (b as HTMLElement).style.width);
    expect(widths).toEqual(['100%', '50%']);
  });

  it('the company uses the accent colour and the peers one neutral colour (different fills)', () => {
    const f = make('HPG', 12, [peer('HSG', 8), peer('NKG', 20)]);

    const fill = (symbol: string) => getComputedStyle(rows(f).find(r => r.dataset['symbol'] === symbol)!.querySelector('[data-testid="pe-bar"]')!).backgroundColor;
    expect(fill('HSG')).toBe(fill('NKG'));
    expect(fill('HPG')).not.toBe(fill('HSG'));
  });

  it('the company with no P/E is listed as n/a rather than dropped', () => {
    const f = make('HAH', null, [peer('VSC', 9)]);

    expect(Array.from(f.nativeElement.querySelectorAll('[data-testid="pe-na"]')).map(n => (n as HTMLElement).dataset['symbol'])).toEqual(['HAH']);
  });

  it('table_toggle_shows_same_numbers: the table lists every symbol with its figures', () => {
    const f = make('HPG', 12, [peer('HSG', 8, { pb: 0.9, changePercent: -2.5 })]);

    const text = Array.from(f.nativeElement.querySelectorAll('[data-testid="pe-table-row"]')).map(r => (r as HTMLElement).textContent!.replace(/\s+/g, ' ').trim());
    expect(text.some(t => t.includes('HSG') && t.includes('8') && t.includes('0,9') && t.includes('−2,5%'))).toBeTrue();
  });

  it('the company itself is not duplicated when the source lists it among its peers', () => {
    const f = make('HPG', 12, [peer('HPG', 99), peer('HSG', 8)]);

    expect(rows(f).filter(r => r.dataset['symbol'] === 'HPG').length).toBe(1);
    expect(rows(f).find(r => r.dataset['symbol'] === 'HPG')!.querySelector('.pe-value')!.textContent!.trim()).toBe('12,0');
  });

  it('missing_section_shows_chua_co_du_lieu without peers and without own P/E', () => {
    const f = make('', null, []);

    expect(f.nativeElement.querySelector('[data-testid="chart-empty"]').textContent).toBe('Chưa có dữ liệu');
  });

  it('symbols from the source are text only', () => {
    const f = make('HPG', 12, [peer('<img src=x onerror=alert(1)>', 5)]);

    expect(f.nativeElement.querySelector('img')).toBeNull();
  });
});

describe('ShareBarComponent: cơ cấu cổ đông', () => {
  const make = (holders: { name: string | null; percentage: number }[]) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [ShareBarComponent] });
    const fixture = TestBed.createComponent(ShareBarComponent);
    fixture.componentInstance.holders = holders;
    fixture.detectChanges();
    return fixture;
  };
  const segs = (f: ReturnType<typeof make>) => Array.from(f.nativeElement.querySelectorAll('[data-testid="sb-seg"]')) as HTMLElement[];

  it('shareholder_bar_top4_plus_khac_sums_100', () => {
    const f = make([
      { name: 'A', percentage: 30 }, { name: 'B', percentage: 20 }, { name: 'C', percentage: 10 }, { name: 'D', percentage: 5 },
      { name: 'E', percentage: 4 }, { name: 'F', percentage: 3 },
    ]);

    expect(segs(f).map(s => s.dataset['name'])).toEqual(['A', 'B', 'C', 'D', 'Khác']);
    const total = segs(f).reduce((sum, s) => sum + Number(s.dataset['pct']), 0);
    expect(total).toBeCloseTo(100, 5);
    expect(Number(segs(f)[4].dataset['pct'])).toBeCloseTo(35, 5);
  });

  it('keeps the largest four whatever order the source lists them in', () => {
    const f = make([{ name: 'S', percentage: 1 }, { name: 'A', percentage: 40 }, { name: 'B', percentage: 25 }, { name: 'C', percentage: 15 }, { name: 'D', percentage: 10 }]);

    expect(segs(f).map(s => s.dataset['name'])).toEqual(['A', 'B', 'C', 'D', 'Khác']);
  });

  it('exactly 100% among the top holders leaves no "Khác" segment', () => {
    const f = make([{ name: 'A', percentage: 60 }, { name: 'B', percentage: 40 }]);

    expect(segs(f).map(s => s.dataset['name'])).toEqual(['A', 'B']);
  });

  it('an impossible total above 100 is scaled to exactly 100 instead of producing a negative "Khác"', () => {
    const f = make([{ name: 'A', percentage: 80 }, { name: 'B', percentage: 60 }]);

    expect(segs(f).reduce((sum, s) => sum + Number(s.dataset['pct']), 0)).toBeCloseTo(100, 5);
    expect(segs(f).every(s => Number(s.dataset['pct']) > 0)).toBeTrue();
  });

  it('holders with no name or a zero share are ignored', () => {
    const f = make([{ name: null, percentage: 50 }, { name: 'A', percentage: 0 }, { name: 'B', percentage: 10 }]);

    expect(segs(f).map(s => s.dataset['name'])).toEqual(['B', 'Khác']);
  });

  it('inline labels only where the segment is wide enough; the legend always names every part with its percent', () => {
    const f = make([{ name: 'A', percentage: 50 }, { name: 'B', percentage: 5 }]);

    const labelled = Array.from(f.nativeElement.querySelectorAll('[data-testid="sb-in"]')).map(x => (x as HTMLElement).textContent!.trim());
    expect(labelled).toContain('50,0%');
    expect(labelled).not.toContain('5,0%');
    const legend = f.nativeElement.querySelector('[data-testid="sb-legend"]').textContent.replace(/\s+/g, ' ');
    expect(legend).toContain('A 50,0%');
    expect(legend).toContain('B 5,0%');
    expect(legend).toContain('Khác 45,0%');
  });

  it('there is a surface gap between the segments (2px) and no stroke around marks', () => {
    const f = make([{ name: 'A', percentage: 50 }, { name: 'B', percentage: 50 }]);

    expect(getComputedStyle(f.nativeElement.querySelector('.sb-bar')).columnGap).toBe('2px');
    expect(getComputedStyle(segs(f)[0]).borderTopWidth).toBe('0px');
  });

  it('table_toggle_shows_same_numbers and missing_section_shows_chua_co_du_lieu', () => {
    const f = make([{ name: 'A', percentage: 30.25 }]);
    const row = (f.nativeElement.querySelectorAll('[data-testid="sb-row"]')[0] as HTMLElement).textContent!;
    expect(row).toContain('A');
    expect(row).toContain('30,3%');

    const empty = make([]);
    expect(empty.nativeElement.querySelector('[data-testid="chart-empty"]').textContent).toBe('Chưa có dữ liệu');
  });

  it('names from the source are text only', () => {
    const f = make([{ name: '<img src=x onerror=alert(1)>', percentage: 30 }]);

    expect(f.nativeElement.querySelector('img')).toBeNull();
  });
});

describe('RangeBarComponent: vùng giá 52 tuần', () => {
  const make = (inputs: Partial<RangeBarComponent>) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [RangeBarComponent] });
    const fixture = TestBed.createComponent(RangeBarComponent);
    Object.assign(fixture.componentInstance, inputs);
    fixture.detectChanges();
    return fixture;
  };
  const text = (f: ReturnType<typeof make>, id: string) => (f.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null)?.textContent?.replace(/\s+/g, ' ').trim();

  it('range_bar_positions_current_price: the marker is at the server position and the labels show low, high and current', () => {
    const f = make({ low: 43.9, high: 68.2, price: 44.6, positionPct: 2.9 });

    expect((f.nativeElement.querySelector('[data-testid="rb-dot"]') as HTMLElement).style.left).toBe('2.9%');
    expect(text(f, 'rb-low')).toBe('Đáy 43.900 đ');
    expect(text(f, 'rb-high')).toBe('Đỉnh 68.200 đ');
    expect(text(f, 'rb-price')).toBe('Hiện tại 44.600 đ · 2,9% vùng');
  });

  it('the marker sits at the ends for 0 and 100 (a real 0 is a position, unlike a missing one)', () => {
    expect((make({ low: 1, high: 2, price: 1, positionPct: 0 }).nativeElement.querySelector('[data-testid="rb-dot"]') as HTMLElement).style.left).toBe('0%');
    expect((make({ low: 1, high: 2, price: 2, positionPct: 100 }).nativeElement.querySelector('[data-testid="rb-dot"]') as HTMLElement).style.left).toBe('100%');
  });

  it('without a current price the range is drawn with a sentence and no marker (never at 0)', () => {
    const f = make({ low: 43.9, high: 68.2, price: null, positionPct: null });

    expect(f.nativeElement.querySelector('[data-testid="rb-dot"]')).toBeNull();
    expect(text(f, 'rb-price')).toBe('Chưa có giá hiện tại');
    expect(text(f, 'rb-low')).toBe('Đáy 43.900 đ');
  });

  it('a price outside the published range is flagged', () => {
    const f = make({ low: 43.9, high: 68.2, price: 70, positionPct: 100, outsideRange: true });

    expect(text(f, 'rb-outside')).toContain('ngoài vùng');
  });

  it('table_toggle_shows_same_numbers: the table has the same figures', () => {
    const f = make({ low: 43.9, high: 68.2, price: 44.6, positionPct: 2.9 });

    const row = text(f, 'rb-row')!;
    expect(row).toContain('43.900 đ');
    expect(row).toContain('68.200 đ');
    expect(row).toContain('44.600 đ');
    expect(row).toContain('2,9%');
  });

  it('missing_section_shows_chua_co_du_lieu when there is no low or no high', () => {
    expect(make({ low: null, high: 10 }).nativeElement.querySelector('[data-testid="chart-empty"]').textContent).toBe('Chưa có dữ liệu');
    expect(make({ low: 10, high: null }).nativeElement.querySelector('[data-testid="chart-empty"]')).not.toBeNull();
  });
});

describe('DividendTimelineComponent: cổ tức theo năm (nguồn không có giá trị số)', () => {
  const make = (events: { exDate: string | null; payDate?: string | null; description: string | null }[]) => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [DividendTimelineComponent] });
    const fixture = TestBed.createComponent(DividendTimelineComponent);
    fixture.componentInstance.events = events.map(e => ({ eventType: null, payDate: null, value: null, ...e }));
    fixture.detectChanges();
    return fixture;
  };
  const years = (f: ReturnType<typeof make>) => Array.from(f.nativeElement.querySelectorAll('[data-testid="dt-year"]')) as HTMLElement[];

  it('groups events by year, newest first, one dot per event', () => {
    const f = make([
      { exDate: '01/03/2025', description: 'Tiền mặt 10%' }, { exDate: '15/09/2026', description: 'Cổ phiếu 20%' }, { exDate: '20/06/2026', description: 'Tiền mặt 5%' },
    ]);

    expect(years(f).map(y => y.dataset['year'])).toEqual(['2026', '2025']);
    expect(years(f)[0].querySelectorAll('[data-testid="dt-dot"]').length).toBe(2);
    expect(years(f)[1].querySelectorAll('[data-testid="dt-dot"]').length).toBe(1);
  });

  it('says plainly that the source has no numeric value and draws no value bars', () => {
    const f = make([{ exDate: '01/03/2025', description: 'Tiền mặt 10%' }]);

    expect(f.nativeElement.querySelector('[data-testid="dt-note"]').textContent).toBe('Nguồn chưa cung cấp giá trị cổ tức dạng số');
    expect(f.nativeElement.querySelector('svg')).toBeNull();
  });

  it('keeps the source title verbatim and never invents a number from it', () => {
    const f = make([{ exDate: '01/03/2025', description: 'Tiền mặt 10%' }]);

    expect(f.nativeElement.textContent).toContain('Tiền mặt 10%');
  });

  it('an event with no readable date goes to its own group at the end', () => {
    const f = make([{ exDate: null, description: 'Họp ĐHCĐ' }, { exDate: '01/03/2025', description: 'Tiền mặt' }]);

    expect(years(f).map(y => y.dataset['year'])).toEqual(['2025', 'Không rõ năm']);
  });

  it('table_toggle_shows_same_numbers and missing_section_shows_chua_co_du_lieu', () => {
    const f = make([{ exDate: '01/03/2025', payDate: '20/03/2025', description: 'Tiền mặt 10%' }]);
    const row = (f.nativeElement.querySelector('[data-testid="dt-row"]') as HTMLElement).textContent!;
    expect(row).toContain('01/03/2025');
    expect(row).toContain('20/03/2025');
    expect(row).toContain('Tiền mặt 10%');

    expect(make([]).nativeElement.querySelector('[data-testid="chart-empty"]').textContent).toBe('Chưa có dữ liệu');
  });

  it('titles from the source are text only', () => {
    const f = make([{ exDate: '01/03/2025', description: '<img src=x onerror=alert(1)>' }]);

    expect(f.nativeElement.querySelector('img')).toBeNull();
  });
});
