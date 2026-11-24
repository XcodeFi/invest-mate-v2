import { TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { buildGroupLayers, TaGroup } from './ta-group-layers';

const N = 60;
const filled = (): (number | null)[] => Array.from({ length: N }, (_, i) => 100 + i);
const nulls = (): (number | null)[] => Array.from({ length: N }, () => null);
const nums = (): number[] => Array.from({ length: N }, (_, i) => 100 + i);

function series(overrides: Partial<TechnicalSeries> = {}): TechnicalSeries {
  return {
    symbol: 'FPT',
    dates: Array.from({ length: N }, (_, i) => `2026-01-${String((i % 28) + 1).padStart(2, '0')}`),
    open: nums(), high: nums(), low: nums(), close: nums(), volume: nums(),
    ema20: filled(), ema50: filled(),
    bollingerUpper: filled(), bollingerMiddle: filled(), bollingerLower: filled(),
    rsi14: filled(), stochK: filled(), stochD: filled(),
    macdLine: filled(), macdSignal: filled(), macdHistogram: filled(),
    adx: filled(), plusDi: filled(), minusDi: filled(),
    atr14: filled(), obv: filled(), mfi14: filled(), volumeSma20: filled(),
    ...overrides,
  };
}

function analysis(overrides: Partial<TechnicalAnalysis> = {}): TechnicalAnalysis {
  return {
    symbol: 'FPT',
    supportLevels: [95, 90],
    resistanceLevels: [120, 130],
    suggestedEntry: 100, suggestedStopLoss: 95, suggestedTarget: 120,
    fibonacci: {
      swingHigh: 130, swingLow: 80,
      retracement236: 118, retracement382: 111, retracement500: 105, retracement618: 99, retracement786: 91,
      extension1272: 140, extension1618: 150,
    },
    ...overrides,
  } as TechnicalAnalysis;
}

describe('buildGroupLayers', () => {
  it('null group → empty layers', () => {
    const l = buildGroupLayers(null, series(), analysis());
    expect(l.overlays).toEqual([]);
    expect(l.panes).toEqual([]);
    expect(l.priceLines).toEqual([]);
    expect(l.notes).toEqual([]);
  });

  it('tong-hop → entry, stop-loss and target price lines, no panes', () => {
    const l = buildGroupLayers('tong-hop', series(), analysis());
    expect(l.priceLines.map(p => p.label)).toEqual(['Giá vào', 'Cắt lỗ', 'Chốt lời']);
    expect(l.priceLines.map(p => p.price)).toEqual([100, 95, 120]);
    expect(l.overlays.length).toBe(0);
    expect(l.panes.length).toBe(0);
    expect(l.notes).toEqual([]);
  });

  it('tong-hop without suggestedEntry → no priceLines, note "Chưa có gợi ý giao dịch"', () => {
    const l = buildGroupLayers('tong-hop', series(),
      analysis({ suggestedEntry: undefined, suggestedStopLoss: undefined, suggestedTarget: undefined }));
    expect(l.priceLines).toEqual([]);
    expect(l.notes).toContain('Chưa có gợi ý giao dịch');
  });

  it('xu-huong → overlays EMA20, EMA50; pane ADX with guides [25]', () => {
    const l = buildGroupLayers('xu-huong', series(), analysis());
    expect(l.overlays.map(o => o.label)).toEqual(['EMA20', 'EMA50']);
    expect(l.panes.length).toBe(1);
    expect(l.panes[0].title).toBe('ADX');
    expect(l.panes[0].lines.map(x => x.label)).toEqual(['ADX', '+DI', '−DI']);
    expect(l.panes[0].guides).toEqual([25]);
  });

  it('dong-luong → 3 panes RSI [30,70], MACD, Stochastic [20,80]; no overlays', () => {
    const l = buildGroupLayers('dong-luong', series(), analysis());
    expect(l.overlays).toEqual([]);
    expect(l.panes.map(p => p.title)).toEqual(['RSI', 'MACD', 'Stochastic']);
    expect(l.panes[0].guides).toEqual([30, 70]);
    expect(l.panes[1].guides).toBeUndefined();
    expect(l.panes[1].lines.map(x => x.label)).toEqual(['Histogram', 'MACD', 'Signal']);
    expect(l.panes[1].lines[0].kind).toBe('histogram');
    expect(l.panes[2].guides).toEqual([20, 80]);
  });

  it('bien-dong → Bollinger upper/middle/lower overlays and ATR pane', () => {
    const l = buildGroupLayers('bien-dong', series(), analysis());
    expect(l.overlays.map(o => o.label)).toEqual(['Bollinger trên', 'Bollinger giữa', 'Bollinger dưới']);
    expect(l.panes.map(p => p.title)).toEqual(['ATR14']);
  });

  it('dong-tien → volume (+SMA20), OBV and MFI [20,80] panes', () => {
    const l = buildGroupLayers('dong-tien', series(), analysis());
    expect(l.overlays).toEqual([]);
    expect(l.panes.map(p => p.title)).toEqual(['Khối lượng', 'OBV', 'MFI']);
    expect(l.panes[0].lines.map(x => x.label)).toEqual(['Khối lượng', 'SMA20 khối lượng']);
    expect(l.panes[0].lines[0].kind).toBe('histogram');
    expect(l.panes[2].guides).toEqual([20, 80]);
  });

  it('moc-gia → priceLines include every support, resistance and 7 fibonacci labels', () => {
    const l = buildGroupLayers('moc-gia', series(), analysis());
    const labels = l.priceLines.map(p => p.label);
    expect(labels).toContain('Hỗ trợ 1');
    expect(labels).toContain('Hỗ trợ 2');
    expect(labels).toContain('Kháng cự 1');
    expect(labels).toContain('Kháng cự 2');
    ['Đỉnh swing', 'Fib 23.6%', 'Fib 38.2%', 'Fib 50%', 'Fib 61.8%', 'Fib 78.6%', 'Đáy swing']
      .forEach(x => expect(labels).toContain(x));
    expect(l.priceLines.length).toBe(2 + 2 + 7);
    expect(l.priceLines.find(p => p.label === 'Fib 61.8%')!.price).toBe(99);
    expect(l.overlays).toEqual([]);
    expect(l.panes).toEqual([]);
  });

  it('moc-gia without fibonacci → still draws supports and resistances', () => {
    const l = buildGroupLayers('moc-gia', series(), analysis({ fibonacci: undefined }));
    expect(l.priceLines.length).toBe(4);
  });

  it('all-null ema50 → note "EMA50: Không đủ dữ liệu" and the line is dropped', () => {
    const l = buildGroupLayers('xu-huong', series({ ema50: nulls() }), analysis());
    expect(l.notes).toContain('EMA50: Không đủ dữ liệu');
    expect(l.overlays.map(o => o.label)).toEqual(['EMA20']);
  });

  it('pane whose lines are all null is dropped with one note named after the pane', () => {
    const l = buildGroupLayers('dong-luong',
      series({ macdLine: nulls(), macdSignal: nulls(), macdHistogram: nulls() }), analysis());
    expect(l.panes.map(p => p.title)).toEqual(['RSI', 'Stochastic']);
    expect(l.notes).toEqual(['MACD: Không đủ dữ liệu']);
  });

  it('under-20-bars series (every indicator all-null) never throws and keeps one note per missing indicator', () => {
    const allNull = series({
      ema20: nulls(), ema50: nulls(), bollingerUpper: nulls(), bollingerMiddle: nulls(), bollingerLower: nulls(),
      rsi14: nulls(), stochK: nulls(), stochD: nulls(), macdLine: nulls(), macdSignal: nulls(), macdHistogram: nulls(),
      adx: nulls(), plusDi: nulls(), minusDi: nulls(), atr14: nulls(), obv: nulls(), mfi14: nulls(), volumeSma20: nulls(),
    });
    const groups: TaGroup[] = ['tong-hop', 'xu-huong', 'dong-luong', 'bien-dong', 'dong-tien', 'moc-gia'];
    for (const g of groups) {
      expect(() => buildGroupLayers(g, allNull, analysis())).not.toThrow();
    }
    const trend = buildGroupLayers('xu-huong', allNull, analysis());
    expect(trend.overlays).toEqual([]);
    expect(trend.panes).toEqual([]);
    expect(trend.notes).toEqual(['EMA20: Không đủ dữ liệu', 'EMA50: Không đủ dữ liệu', 'ADX: Không đủ dữ liệu']);
  });

  it('partially-null line is kept (warm-up nulls are skipped by the chart, not here)', () => {
    const warm = filled();
    warm[0] = null;
    const l = buildGroupLayers('xu-huong', series({ ema20: warm }), analysis());
    expect(l.overlays[0].values[0]).toBeNull();
    expect(l.notes).toEqual([]);
  });
});
