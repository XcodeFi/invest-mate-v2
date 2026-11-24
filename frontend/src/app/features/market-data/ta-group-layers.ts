import { TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import {
  GroupLayers, GuideLine, LineLayer, PaneLayer, PriceLineLayer
} from '../../shared/components/indicator-chart/indicator-chart.types';

/** Sáu khối của panel Phân tích kỹ thuật — trùng giá trị data-group trên tiêu đề khối. */
export type TaGroup = 'tong-hop' | 'xu-huong' | 'dong-luong' | 'bien-dong' | 'dong-tien' | 'moc-gia';

export type { GroupLayers, GuideLine, LineLayer, PaneLayer, PriceLineLayer };

const NO_DATA = 'Không đủ dữ liệu';

const COLOR = {
  ema20: '#2563eb',
  ema50: '#f59e0b',
  bandOuter: '#94a3b8',
  bandMiddle: '#64748b',
  adx: '#7c3aed',
  plusDi: '#16a34a',
  minusDi: '#dc2626',
  rsi: '#7c3aed',
  macd: '#2563eb',
  macdSignal: '#f59e0b',
  macdHist: '#94a3b8',
  stochK: '#2563eb',
  stochD: '#f59e0b',
  atr: '#0891b2',
  volume: '#94a3b8',
  volumeSma: '#f59e0b',
  obv: '#2563eb',
  mfi: '#7c3aed',
};

/** Vạch tham chiếu dùng chung: trên ngưỡng trên là quá mua, dưới ngưỡng dưới là quá bán. */
const overboughtOversold = (high: number, low: number): GuideLine[] => [
  { value: high, label: 'Quá mua' },
  { value: low, label: 'Quá bán' },
];

const hasData = (values: (number | null)[]): boolean => values.some(v => v !== null && v !== undefined);

const empty = (): GroupLayers => ({ overlays: [], panes: [], priceLines: [], notes: [] });

/**
 * Giữ các đường có dữ liệu; mỗi đường toàn null thì thành một ghi chú "<tên>: Không đủ dữ liệu".
 * Tầng không còn đường nào có dữ liệu thì bị bỏ, và ghi chú lấy theo tên tầng thay vì lặp từng đường.
 */
function keepWithData(lines: LineLayer[], notes: string[]): LineLayer[] {
  const kept = lines.filter(l => hasData(l.values));
  lines.filter(l => !hasData(l.values)).forEach(l => notes.push(`${l.label}: ${NO_DATA}`));
  return kept;
}

function pane(title: string, lines: LineLayer[], notes: string[], guides?: GuideLine[]): PaneLayer | null {
  if (!lines.some(l => hasData(l.values))) {
    notes.push(`${title}: ${NO_DATA}`);
    return null;
  }
  const kept = keepWithData(lines, notes);
  return { title, lines: kept, ...(guides ? { guides } : {}) };
}

/**
 * Ánh xạ nhóm chỉ báo → lớp vẽ. Hàm thuần: không đụng DOM hay HTTP.
 * `group === null` (chưa chọn nhóm nào) → mọi mảng rỗng, chỉ còn nến.
 */
export function buildGroupLayers(group: TaGroup | null, s: TechnicalSeries, a: TechnicalAnalysis): GroupLayers {
  const out = empty();
  if (group === null) return out;

  const addPane = (p: PaneLayer | null) => { if (p) out.panes.push(p); };

  switch (group) {
    case 'tong-hop': {
      if (a.suggestedEntry === undefined || a.suggestedEntry === null) {
        out.notes.push('Chưa có gợi ý giao dịch');
        break;
      }
      out.priceLines.push({ label: 'Giá vào', price: a.suggestedEntry });
      if (a.suggestedStopLoss != null) out.priceLines.push({ label: 'Cắt lỗ', price: a.suggestedStopLoss });
      if (a.suggestedTarget != null) out.priceLines.push({ label: 'Chốt lời', price: a.suggestedTarget });
      break;
    }

    case 'xu-huong': {
      out.overlays = keepWithData([
        { label: 'EMA20 (TB 20 phiên)', values: s.ema20, color: COLOR.ema20 },
        { label: 'EMA50 (TB 50 phiên)', values: s.ema50, color: COLOR.ema50 },
      ], out.notes);
      addPane(pane('ADX (độ mạnh xu hướng)', [
        { label: 'ADX', values: s.adx, color: COLOR.adx },
        { label: '+DI (lực tăng)', values: s.plusDi, color: COLOR.plusDi },
        { label: '−DI (lực giảm)', values: s.minusDi, color: COLOR.minusDi },
      ], out.notes, [{ value: 25, label: 'Có xu hướng' }]));
      break;
    }

    case 'dong-luong': {
      addPane(pane('RSI (sức mạnh tương đối)', [{ label: 'RSI14', values: s.rsi14, color: COLOR.rsi }], out.notes, overboughtOversold(70, 30)));
      addPane(pane('MACD (động lượng)', [
        { label: 'Histogram', values: s.macdHistogram, color: COLOR.macdHist, kind: 'histogram' },
        { label: 'MACD', values: s.macdLine, color: COLOR.macd },
        { label: 'Signal (đường tín hiệu)', values: s.macdSignal, color: COLOR.macdSignal },
      ], out.notes));
      addPane(pane('Stochastic (dao động)', [
        { label: '%K (nhanh)', values: s.stochK, color: COLOR.stochK },
        { label: '%D (chậm)', values: s.stochD, color: COLOR.stochD },
      ], out.notes, overboughtOversold(80, 20)));
      break;
    }

    case 'bien-dong': {
      out.overlays = keepWithData([
        { label: 'Bollinger trên', values: s.bollingerUpper, color: COLOR.bandOuter },
        { label: 'Bollinger giữa', values: s.bollingerMiddle, color: COLOR.bandMiddle },
        { label: 'Bollinger dưới', values: s.bollingerLower, color: COLOR.bandOuter },
      ], out.notes);
      addPane(pane('ATR14 (biên độ dao động)', [{ label: 'ATR14', values: s.atr14, color: COLOR.atr }], out.notes));
      break;
    }

    case 'dong-tien': {
      addPane(pane('Khối lượng', [
        { label: 'Khối lượng', values: s.volume, color: COLOR.volume, kind: 'histogram' },
        { label: 'TB 20 phiên', values: s.volumeSma20, color: COLOR.volumeSma },
      ], out.notes));
      addPane(pane('OBV (dòng tiền tích luỹ)', [{ label: 'OBV', values: s.obv, color: COLOR.obv }], out.notes));
      addPane(pane('MFI (dòng tiền)', [{ label: 'MFI14', values: s.mfi14, color: COLOR.mfi }], out.notes, overboughtOversold(80, 20)));
      break;
    }

    case 'moc-gia': {
      (a.supportLevels ?? []).forEach((price, i) => out.priceLines.push({ label: `Hỗ trợ ${i + 1}`, price }));
      (a.resistanceLevels ?? []).forEach((price, i) => out.priceLines.push({ label: `Kháng cự ${i + 1}`, price }));
      const fib = a.fibonacci;
      if (fib) {
        out.priceLines.push(
          { label: 'Đỉnh swing', price: fib.swingHigh },
          { label: 'Fib 23.6%', price: fib.retracement236 },
          { label: 'Fib 38.2%', price: fib.retracement382 },
          { label: 'Fib 50%', price: fib.retracement500 },
          { label: 'Fib 61.8%', price: fib.retracement618 },
          { label: 'Fib 78.6%', price: fib.retracement786 },
          { label: 'Đáy swing', price: fib.swingLow },
        );
      }
      if (out.priceLines.length === 0) out.notes.push('Chưa có mốc giá');
      break;
    }
  }

  return out;
}
