import { TrapEvent } from '../../core/services/market-data.service';
import { IndicatorChartMarker } from '../components/indicator-chart/indicator-chart.types';

const day = (iso: string): string => (iso ?? '').slice(0, 10);
const dm = (iso: string): string => { const [, m, d] = day(iso).split('-'); return `${d}/${m}`; };
const num = (v: number): string => v.toLocaleString('vi-VN', { maximumFractionDigits: 2 });
const pct = (v: number): string => v.toLocaleString('vi-VN', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/**
 * Mốc bẫy trên biểu đồ. Mốc đặt ở nến phá vỡ (hồi tố) và chỉ có khi đã được xác nhận (t+5), nên KHÔNG có mốc cho các phiên gần nhất và không phải dự báo.
 * Phá vỡ thất bại: mũi tên xuống đỏ phía trên nến (người mua đuổi bị kẹt). Bẫy giảm giá: mũi tên lên xanh phía dưới nến (người bán tháo bị kẹt).
 * Mỗi mốc có "Nếu sai" (% và cắt lỗ quy ước). KHÔNG có tần suất hay xác suất. Chuỗi chỉ đi vào tooltip (hiển thị dạng văn bản).
 */
export function buildTrapMarkers(events: TrapEvent[] | null | undefined): IndicatorChartMarker[] {
  return (events ?? []).map(e => {
    const bull = e.type === 'failed-breakout';
    const level = bull ? 'pivot' : 'hỗ trợ';
    return {
      time: day(e.breakoutDate),
      position: bull ? 'aboveBar' : 'belowBar',
      color: bull ? '#dc2626' : '#16a34a',
      shape: bull ? 'arrowDown' : 'arrowUp',
      text: bull ? 'Bẫy tăng' : 'Bẫy giảm',
      tooltip: [
        `${e.typeVi} (xác nhận ngày ${dm(e.confirmedDate)})`,
        e.description,
        `Nếu sai: −${pct(e.riskPct)}% từ ${level} ${num(e.levelPrice)} tới cắt lỗ quy ước ${num(e.stopPrice)}`,
        `Giá đã đi xuyên qua ${level} tối đa ${pct(e.excursionPct)}% trong cửa sổ xác nhận`,
        e.disclaimer,
      ],
    } as IndicatorChartMarker;
  });
}
