import { IndicatorChartMarker } from './indicator-chart.types';

/** Marker đã đặt lên một phiên thật của trục thời gian (khớp SeriesMarker của lightweight-charts). */
export interface SnappedMarker {
  time: string;
  position: 'aboveBar' | 'belowBar';
  color: string;
  shape: 'arrowUp' | 'arrowDown' | 'circle' | 'square';
  tone?: 'trapBull' | 'trapBear';
  text: string;
}

/**
 * Đặt marker lên các phiên giao dịch. `times` là các ngày YYYY-MM-DD tăng dần của trục thời gian. Marker rơi vào ngày không có
 * phiên (cuối tuần, ngày lễ) thì gắn vào phiên gần nhất PHÍA TRƯỚC; sau phiên cuối thì gắn vào phiên cuối; trước phiên đầu thì bỏ
 * (không có phiên nào để giữ nó). Trả marker đã sắp theo thời gian (biểu đồ yêu cầu) và các dòng tooltip gom theo phiên.
 */
export function snapMarkers(
  markers: readonly IndicatorChartMarker[], times: readonly string[]
): { markers: SnappedMarker[]; tooltips: Map<string, string[]> } {
  const snapped: SnappedMarker[] = [];
  const tooltips = new Map<string, string[]>();
  if (times.length === 0) return { markers: snapped, tooltips };

  for (const marker of markers) {
    const day = (marker.time ?? '').substring(0, 10);
    const k = sessionIndexFor(times, day);
    if (k < 0) continue;
    const time = times[k];
    snapped.push({ time, position: marker.position, color: marker.color, shape: marker.shape, ...(marker.tone ? { tone: marker.tone } : {}), text: marker.text });
    if (marker.tooltip?.length) tooltips.set(time, [...(tooltips.get(time) ?? []), ...marker.tooltip]);
  }
  snapped.sort((a, b) => a.time.localeCompare(b.time));
  return { markers: snapped, tooltips };
}

/** Chỉ số phiên gần nhất không sau `day` (YYYY-MM-DD) trong mảng ngày tăng dần: phiên của chính ngày đó, hoặc phiên trước nó; -1 nếu ngày nằm trước phiên đầu. */
export function sessionIndexFor(times: readonly string[], day: string): number {
  let lo = 0, hi = times.length - 1, found = -1;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (times[mid] <= day) { found = mid; lo = mid + 1; } else hi = mid - 1;
  }
  return found;
}

/**
 * Dữ liệu một chuỗi theo trục thời gian chung: điểm null thành điểm trống (có time, không có value) để mọi chuỗi cùng số bar
 * với tầng giá. `colors` (chỉ cột) cho màu riêng từng điểm khi có.
 */
export function toSeriesData(
  times: readonly string[], sourceIndex: readonly number[], values: readonly (number | null)[], colors?: readonly (string | null)[]
): { time: string; value?: number; color?: string }[] {
  return sourceIndex.map((src, k) => {
    const v = values[src];
    if (v === null || v === undefined) return { time: times[k] };
    const color = colors?.[src];
    return color ? { time: times[k], value: v, color } : { time: times[k], value: v };
  });
}
