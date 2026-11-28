import { PatternCandidate, TechnicalSeries } from '../../core/services/market-data.service';
import { GroupLayers } from '../components/indicator-chart/indicator-chart.types';
import { contrastRatio } from '../theme/contrast';
import { chartColors } from '../components/indicator-chart/indicator-chart.theme';
import { buildPatternLayers, withPattern } from './pattern-layers';

const N = 10;
const series = (): TechnicalSeries => ({
  symbol: 'FPT', dates: Array.from({ length: N }, (_, i) => `2026-03-${String(i + 1).padStart(2, '0')}`),
  open: Array(N).fill(100), high: Array(N).fill(101), low: Array(N).fill(99), close: Array.from({ length: N }, (_, i) => 100 + i),
} as unknown as TechnicalSeries);

const cup = (over: Partial<PatternCandidate> = {}): PatternCandidate => ({
  type: 'cup-with-handle', typeVi: 'Cốc tay cầm', state: 'near-pivot', stateVi: 'Sát pivot',
  startDate: '2026-03-02T00:00:00', bottomDate: '2026-03-04T00:00:00', rightLipDate: '2026-03-07T00:00:00',
  cupLengthWeeks: 22, cupDepthPct: 21.5, handleLengthSessions: 8, handleDepthPct: 9.4,
  pivotPrice: 108, distanceToPivotPct: 2.1, volumeVsAvg50: 0.8,
  stopPrice: 97.5, riskPct: 9.7, targetPrice: 125, rewardPct: 15.7, rewardRisk: 1.6,
  criteria: [], disclaimer: 'x', ...over,
});

const empty = (): GroupLayers => ({ overlays: [], panes: [], priceLines: [], notes: [] });

describe('buildPatternLayers: vẽ cốc, tay cầm, pivot, cắt lỗ, mục tiêu', () => {
  it('colours the closes of the cup (left peak → right lip) and of the handle (right lip → last bar), nothing outside', () => {
    const l = buildPatternLayers(cup(), series());

    const cupLine = l.overlays.find(o => o.label.startsWith('Cốc'))!;
    const handleLine = l.overlays.find(o => o.label === 'Tay cầm')!;
    expect(cupLine.values).toEqual([null, 101, 102, 103, 104, 105, 106, null, null, null]);
    expect(handleLine.values).toEqual([null, null, null, null, null, null, 106, 107, 108, 109]);
  });

  it('draws three price lines with the percentages from the server (pivot, stop "−X% từ pivot", measured target with R)', () => {
    const l = buildPatternLayers(cup(), series());

    expect(l.priceLines.map(p => p.price)).toEqual([108, 97.5, 125]);
    expect(l.priceLines[0].label).toContain('Pivot 108');
    expect(l.priceLines[0].label).toContain('còn 2,1% tới pivot');
    expect(l.priceLines[1].label).toContain('Cắt lỗ 97,5');
    expect(l.priceLines[1].label).toContain('−9,7% từ pivot');
    expect(l.priceLines[2].label).toContain('Mục tiêu đo 125');
    expect(l.priceLines[2].label).toContain('+15,7%');
    expect(l.priceLines[2].label).toContain('1,6R');
  });

  it('says "đã trên pivot" when the price is already above it', () => {
    const l = buildPatternLayers(cup({ distanceToPivotPct: -1.1, state: 'broke-out-unconfirmed' }), series());

    expect(l.priceLines[0].label).toContain('đã trên pivot 1,1%');
  });

  it('accepts ISO dates with a time part and plain dates alike', () => {
    const a = buildPatternLayers(cup({ startDate: '2026-03-02', rightLipDate: '2026-03-07' }), series());
    const b = buildPatternLayers(cup(), series());

    expect(a.overlays[0].values).toEqual(b.overlays[0].values);
  });

  it('a cup that starts before the visible series only draws the part that is visible', () => {
    const l = buildPatternLayers(cup({ startDate: '2025-12-01T00:00:00' }), series());

    expect(l.overlays[0].values.filter(v => v !== null).length).toBe(7);
  });

  it('every line also has a short chart title (Pivot, Cắt lỗ −X%, Mục tiêu +Y% · NR) so the labels on the chart do not pile up at the right edge', () => {
    const l = buildPatternLayers(cup(), series());

    expect(l.priceLines.map(p => p.shortLabel)).toEqual(['Pivot', 'Cắt lỗ −9,7%', 'Mục tiêu +15,7% · 1,6R']);
    l.priceLines.forEach(p => expect(p.shortLabel!.length).withContext(p.label).toBeLessThan(p.label.length));
  });

  it('adds no price level of its own beyond the three and no notes or panes', () => {
    const l = buildPatternLayers(cup(), series());

    expect(l.panes).toEqual([]);
    expect(l.notes).toEqual([]);
    expect(l.priceLines.length).toBe(3);
  });
});

describe('màu đường của mô hình', () => {
  it('both line colours reach 3:1 (graphics) on the light and on the dark chart background', () => {
    const l = buildPatternLayers(cup(), series());
    const colours = l.overlays.map(o => o.color!);
    expect(colours.length).toBe(2);

    for (const c of colours) {
      expect(contrastRatio(c, chartColors('light').background)).withContext(`${c} trên nền sáng`).toBeGreaterThanOrEqual(3);
      expect(contrastRatio(c, chartColors('dark').background)).withContext(`${c} trên nền tối`).toBeGreaterThanOrEqual(3);
    }
  });
});

describe('withPattern: gộp vào lớp của nhóm đang chọn', () => {
  it('appends to the group layers, keeping the group lines', () => {
    const base: GroupLayers = { ...empty(), priceLines: [{ label: 'Giá vào', price: 100 }], overlays: [{ label: 'EMA 20', values: [1] }] };

    const merged = withPattern(base, cup(), series(), true);

    expect(merged.priceLines.map(p => p.label)[0]).toBe('Giá vào');
    expect(merged.priceLines.length).toBe(4);
    expect(merged.overlays[0].label).toBe('EMA 20');
    expect(merged.overlays.length).toBe(3);
  });

  it('returns the base untouched when switched off, when there is no candidate, or when the series is not loaded yet', () => {
    const base = empty();

    expect(withPattern(base, cup(), series(), false)).toBe(base);
    expect(withPattern(base, null, series(), true)).toBe(base);
    expect(withPattern(base, undefined, series(), true)).toBe(base);
    expect(withPattern(base, cup(), null, true)).toBe(base);
  });

  it('still draws the pattern when no indicator group is selected (empty base)', () => {
    expect(withPattern(empty(), cup(), series(), true).priceLines.length).toBe(3);
  });
});
