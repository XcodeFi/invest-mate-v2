import { TechnicalSeries } from '../../core/services/market-data.service';
import { sliceSeriesFrom } from './slice-series';

const series = (): TechnicalSeries => ({
  symbol: 'FPT', adjusted: true, adjustedAsOf: '2026-11-24T00:00:00Z',
  dates: ['2026-11-18', '2026-11-19', '2026-11-20', '2026-11-23', '2026-11-24'].map(d => `${d}T00:00:00Z`),
  open: [1, 2, 3, 4, 5], high: [2, 3, 4, 5, 6], low: [0, 1, 2, 3, 4], close: [1.5, 2.5, 3.5, 4.5, 5.5], volume: [10, 20, 30, 40, 50],
  ema20: [null, null, 3, 3.5, 4], ema50: [null, null, null, null, 4], ema200: [null, null, null, null, null],
  bollingerUpper: [], bollingerMiddle: [], bollingerLower: [], rsi14: [null, 50, 55, 60, 65], stochK: [], stochD: [], macdLine: [],
  macdSignal: [], macdHistogram: [], adx: [], plusDi: [], minusDi: [], atr14: [], obv: [], mfi14: [], volumeSma20: [],
});

describe('sliceSeriesFrom: cắt chuỗi chỉ báo theo khoảng hiển thị sau khi đã tính trên chuỗi dài hơn', () => {
  it('keeps only the bars from the given day (inclusive) in every aligned array', () => {
    const s = sliceSeriesFrom(series(), '2026-11-20');

    expect(s.dates.length).toBe(3);
    expect(s.dates[0].startsWith('2026-11-20')).toBeTrue();
    expect(s.open).toEqual([3, 4, 5]);
    expect(s.close).toEqual([3.5, 4.5, 5.5]);
    expect(s.volume).toEqual([30, 40, 50]);
    expect(s.ema20).toEqual([3, 3.5, 4]);
    expect(s.rsi14).toEqual([55, 60, 65]);
  });

  it('keeps the warmed-up indicator values (they are cut, not recomputed)', () => {
    const s = sliceSeriesFrom(series(), '2026-11-23');

    expect(s.ema20).toEqual([3.5, 4]);
    expect(s.ema50).toEqual([null, 4]);
  });

  it('keeps the scalar fields and the arrays that are empty or missing', () => {
    const s = sliceSeriesFrom(series(), '2026-11-20');

    expect(s.symbol).toBe('FPT');
    expect(s.adjusted).toBeTrue();
    expect(s.adjustedAsOf).toBe('2026-11-24T00:00:00Z');
    expect(s.bollingerUpper).toEqual([]);

    const old = series();
    delete old.ema200;
    expect(sliceSeriesFrom(old, '2026-11-20').ema200).toBeUndefined();
  });

  it('a day before the first bar keeps everything; a day after the last keeps nothing', () => {
    expect(sliceSeriesFrom(series(), '2020-01-01').dates.length).toBe(5);
    expect(sliceSeriesFrom(series(), '2030-01-01').dates.length).toBe(0);
  });

  it('does not mutate the input', () => {
    const input = series();
    sliceSeriesFrom(input, '2026-11-23');

    expect(input.dates.length).toBe(5);
    expect(input.close.length).toBe(5);
  });
});
