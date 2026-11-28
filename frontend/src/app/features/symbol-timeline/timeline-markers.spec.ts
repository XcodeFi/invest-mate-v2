import { buildEmotionPane, buildTimelineMarkers } from './timeline-markers';

const trade = (ts: string, tradeType: 'BUY' | 'SELL', price = 63000, quantity = 100) =>
  ({ type: 'trade' as const, timestamp: ts, data: { tradeType, price, quantity } });
const journal = (ts: string, title = 'Vào lệnh', emotionalState?: string, confidenceLevel?: number) =>
  ({ type: 'journal' as const, timestamp: ts, data: { title, emotionalState, confidenceLevel } });
const event = (ts: string, eventType = 'News', title = 'Tin') => ({ type: 'event' as const, timestamp: ts, data: { eventType, title } });
const alert = (ts: string, title = 'Cảnh báo giá') => ({ type: 'alert' as const, timestamp: ts, data: { title } });
const dossier = (ts: string, action = 'signed') => ({ type: 'dossier' as const, timestamp: ts, data: { action, version: 2 } });

const label = (a: string | undefined) => (a === 'signed' ? 'Ký hồ sơ công ty' : 'Hồ sơ');
// Ba phiên liên tiếp (giá nghìn đồng ×1000 như chuỗi thật); giao dịch 10/09 giá 63.000 nằm trong [62.500; 63.500].
const bars = {
  dates: ['2026-09-09T00:00:00Z', '2026-09-10T00:00:00Z', '2026-09-11T00:00:00Z'],
  low: [62000, 62500, 62800],
  high: [64000, 63500, 64000],
};
const opts = (adjusted: boolean | null = true, b: typeof bars | null = bars) => ({ adjusted, dossierLabel: label, bars: b });

describe('buildTimelineMarkers', () => {
  it('buy is an up arrow below the bar in green, sell a down arrow above in red', () => {
    const [buy, sell] = buildTimelineMarkers([trade('2026-11-19T02:00:00Z', 'BUY'), trade('2026-11-20T02:00:00Z', 'SELL')], opts());

    expect(buy).toEqual(jasmine.objectContaining({ time: '2026-11-19T02:00:00Z', position: 'belowBar', shape: 'arrowUp', color: '#22c55e', text: 'T' }));
    expect(sell).toEqual(jasmine.objectContaining({ position: 'aboveBar', shape: 'arrowDown', color: '#ef4444', text: 'T' }));
  });

  it('journals, events, alerts and dossier of one day are each folded into a single marker with a count', () => {
    const d = '2026-11-19T02:00:00Z';
    const markers = buildTimelineMarkers([
      journal(d), journal(d, 'Thứ hai'), event(d), event(d), event(d), alert(d), dossier(d),
    ], opts());

    expect(markers.map(m => m.text)).toEqual(['J2', 'E3', 'A', 'H']);
    expect(markers.map(m => m.shape)).toEqual(['circle', 'square', 'square', 'circle']);
  });

  it('a single journal, event or alert has no count', () => {
    const d = '2026-11-19T02:00:00Z';

    expect(buildTimelineMarkers([journal(d), event(d), alert(d)], opts()).map(m => m.text)).toEqual(['J', 'E', 'A']);
  });

  it('tooltip lines describe each item (trade side, quantity, price; titles truncated at 40 characters)', () => {
    const d = '2026-11-19T02:00:00Z';
    const long = 'x'.repeat(60);
    const markers = buildTimelineMarkers([trade(d, 'BUY', 63000, 100), journal(d, long), dossier(d)], opts(false));

    const lines = markers.flatMap(m => m.tooltip ?? []);
    expect(lines[0]).toContain('MUA');
    expect(lines[0]).toContain('100');
    expect(lines[0]).toContain('63.000');
    expect(lines[1]).toBe(`Nhật ký · ${'x'.repeat(40)}…`);
    expect(lines[2]).toBe('Hồ sơ · Ký hồ sơ công ty');
  });

  const tradeAt = (price: number, ts = '2026-09-10T02:00:00Z') => buildTimelineMarkers([trade(ts, 'BUY', price)], opts(true))[0].tooltip![0];

  it('adjusted_series_trade_outside_bar_range_labelled_gia_goc', () => {
    expect(tradeAt(70000)).toContain('(giá gốc)');
    expect(tradeAt(60000)).toContain('(giá gốc)');
  });

  it('adjusted_series_trade_inside_range_not_labelled', () => {
    expect(tradeAt(63000)).not.toContain('(giá gốc)');
    expect(tradeAt(62500)).not.toContain('(giá gốc)');
    expect(tradeAt(63500)).not.toContain('(giá gốc)');
  });

  it('a split (price halved) lies far outside the adjusted range and is labelled', () => {
    expect(tradeAt(31500)).toContain('(giá gốc)');
    expect(tradeAt(126000)).toContain('(giá gốc)');
  });

  it('tolerance of 0.5% around the session range: just inside is not labelled, just outside is', () => {
    expect(tradeAt(63800)).not.toContain('(giá gốc)');   // 63.500 × 1,005 = 63.817,5
    expect(tradeAt(63900)).toContain('(giá gốc)');
    expect(tradeAt(62200)).not.toContain('(giá gốc)');   // 62.500 × 0,995 = 62.187,5
    expect(tradeAt(62100)).toContain('(giá gốc)');
  });

  it('unadjusted_series_never_labelled (flat O=H=L=C bars would make almost every trade look outside)', () => {
    const flat = { dates: bars.dates, low: [60000, 60000, 60000], high: [60000, 60000, 60000] };

    expect(buildTimelineMarkers([trade('2026-09-10T02:00:00Z', 'BUY', 63000)], opts(false, flat))[0].tooltip![0]).not.toContain('(giá gốc)');
    expect(buildTimelineMarkers([trade('2026-09-10T02:00:00Z', 'BUY', 63000)], opts(null, flat))[0].tooltip![0]).not.toContain('(giá gốc)');
  });

  it('a trade on a weekend is compared with the previous session; before the first session or after no data there is no label', () => {
    // 2026-09-12 là thứ Bảy: so với phiên 11/09 [62.800; 64.000]
    expect(buildTimelineMarkers([trade('2026-09-12T02:00:00Z', 'BUY', 70000)], opts(true))[0].tooltip![0]).toContain('(giá gốc)');
    expect(buildTimelineMarkers([trade('2026-09-12T02:00:00Z', 'BUY', 63000)], opts(true))[0].tooltip![0]).not.toContain('(giá gốc)');
    expect(buildTimelineMarkers([trade('2026-08-01T02:00:00Z', 'BUY', 70000)], opts(true))[0].tooltip![0]).not.toContain('(giá gốc)');
    expect(buildTimelineMarkers([trade('2026-09-10T02:00:00Z', 'BUY', 70000)], opts(true, null))[0].tooltip![0]).not.toContain('(giá gốc)');
  });

  it('dividend events no longer drive the label (one source of truth: the price against the bar)', () => {
    const markers = buildTimelineMarkers([trade('2026-09-10T02:00:00Z', 'BUY', 63000), event('2026-09-21T00:00:00Z', 'Dividend')], opts(true));

    expect(markers[0].tooltip![0]).not.toContain('(giá gốc)');
  });

  it('no items → no markers', () => {
    expect(buildTimelineMarkers([], opts())).toEqual([]);
  });
});

describe('buildEmotionPane: dải cảm xúc của nhật ký thành một tầng cột', () => {
  const dates = ['2026-11-18', '2026-11-19', '2026-11-20', '2026-11-23'].map(d => `${d}T00:00:00Z`);
  const colors: Record<string, string> = { 'Tự tin': '#22c55e', 'Sợ hãi': '#ef4444' };

  it('puts the confidence level on the session of the journal with that emotion colour', () => {
    const pane = buildEmotionPane(dates, [journal('2026-11-19T03:00:00Z', 'a', 'Tự tin', 8)], colors, '#6b7280')!;

    expect(pane.lines[0].kind).toBe('histogram');
    expect(pane.lines[0].values).toEqual([null, 8, null, null]);
    expect(pane.lines[0].colors).toEqual([null, '#22c55e', null, null]);
  });

  it('a journal on a weekend snaps to the previous session', () => {
    const pane = buildEmotionPane(dates, [journal('2026-11-21T03:00:00Z', 'a', 'Sợ hãi', 3)], colors, '#6b7280')!;

    expect(pane.lines[0].values).toEqual([null, null, 3, null]);
  });

  it('confidence defaults to 5 and an unknown emotion uses the fallback colour; the last journal of a session wins', () => {
    const pane = buildEmotionPane(dates, [
      journal('2026-11-18T01:00:00Z', 'a', 'Tự tin', 9),
      journal('2026-11-18T05:00:00Z', 'b', 'Lạ'),
    ], colors, '#6b7280')!;

    expect(pane.lines[0].values![0]).toBe(5);
    expect(pane.lines[0].colors![0]).toBe('#6b7280');
  });

  it('journals without an emotion, and other item types, are ignored; none left → null', () => {
    expect(buildEmotionPane(dates, [journal('2026-11-19T03:00:00Z', 'a'), event('2026-11-19T00:00:00Z')], colors, '#6b7280')).toBeNull();
  });

  it('a journal before the first session is dropped', () => {
    expect(buildEmotionPane(dates, [journal('2026-11-01T03:00:00Z', 'a', 'Tự tin', 8)], colors, '#6b7280')).toBeNull();
  });
});
