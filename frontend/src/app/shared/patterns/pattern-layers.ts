import { PatternCandidate, TechnicalSeries } from '../../core/services/market-data.service';
import { GroupLayers } from '../components/indicator-chart/indicator-chart.types';

const COLOR = { cup: '#0ea5e9', handle: '#f97316' };

const day = (iso: string): string => (iso ?? '').slice(0, 10);

/** Giá hiển thị trong nhãn đường ngang: số kiểu Việt, tối đa 2 chữ số thập phân. */
const price = (v: number): string => v.toLocaleString('vi-VN', { maximumFractionDigits: 2 });
const pct = (v: number): string => v.toLocaleString('vi-VN', { maximumFractionDigits: 1, minimumFractionDigits: 1 });

/**
 * Lớp vẽ của một ứng viên mô hình: hai đoạn giá đóng cửa tô màu (cốc từ đỉnh trái tới vành phải, tay cầm từ vành phải tới nến cuối) và ba đường ngang
 * có nhãn %: pivot (kèm khoảng cách), cắt lỗ ("−X% từ pivot") và mục tiêu đo ("+Y% · N R"). Chỉ VẼ số server đã tính; không tính lại gì.
 * Ngày của ứng viên nằm ngoài chuỗi đang xem thì đoạn tương ứng chỉ vẽ phần giao nhau.
 */
export function buildPatternLayers(c: PatternCandidate, s: TechnicalSeries): GroupLayers {
  const dates = s.dates.map(day);
  const start = day(c.startDate);
  const lip = day(c.rightLipDate);
  const segment = (from: string, to: string): (number | null)[] =>
    dates.map((d, i) => (d >= from && d <= to ? s.close[i] ?? null : null));

  const cup = segment(start, lip);
  const handle = segment(lip, dates[dates.length - 1] ?? lip);
  const overlays = [
    ...(cup.some(v => v !== null) ? [{ label: 'Cốc (từ đỉnh trái tới vành phải)', values: cup, color: COLOR.cup }] : []),
    ...(handle.some(v => v !== null) ? [{ label: 'Tay cầm', values: handle, color: COLOR.handle }] : []),
  ];

  const distance = c.distanceToPivotPct;
  const pivotNote = distance > 0 ? `còn ${pct(distance)}% tới pivot` : `đã trên pivot ${pct(-distance)}%`;
  return {
    overlays,
    panes: [],
    priceLines: [
      { label: `Pivot ${price(c.pivotPrice)} (${pivotNote})`, price: c.pivotPrice },
      { label: `Cắt lỗ ${price(c.stopPrice)} (−${pct(c.riskPct)}% từ pivot)`, price: c.stopPrice },
      { label: `Mục tiêu đo ${price(c.targetPrice)} (+${pct(c.rewardPct)}% · ${pct(c.rewardRisk)}R)`, price: c.targetPrice },
    ],
    notes: [],
  };
}

/** Gộp lớp mô hình vào lớp của nhóm đang chọn (nhóm có thể là null: vẫn vẽ mô hình). Tắt hoặc không có ứng viên thì giữ nguyên. */
export function withPattern(base: GroupLayers, candidate: PatternCandidate | null | undefined, series: TechnicalSeries | null, enabled: boolean): GroupLayers {
  if (!enabled || !candidate || !series) return base;
  const p = buildPatternLayers(candidate, series);
  return {
    overlays: [...base.overlays, ...p.overlays],
    panes: base.panes,
    priceLines: [...base.priceLines, ...p.priceLines],
    notes: base.notes,
  };
}
