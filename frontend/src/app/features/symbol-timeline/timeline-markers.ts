import { IndicatorChartMarker, PaneLayer } from '../../shared/components/indicator-chart/indicator-chart.types';
import { sessionIndexFor } from '../../shared/components/indicator-chart/chart-markers';

/** Phần của một mục dòng thời gian mà việc dựng marker cần (khớp TimelineItem, không phụ thuộc service). */
export interface MarkerItem {
  type: 'journal' | 'trade' | 'alert' | 'event' | 'dossier';
  timestamp: string;
  data: any;
}

export interface TimelineMarkerOptions {
  /** Cờ `adjusted` của chuỗi giá đang vẽ; chỉ khi true thì giá thô của giao dịch mới khác mặt bằng giá của biểu đồ. */
  adjusted: boolean | null | undefined;
  /** Nhãn của mốc hồ sơ theo hành động (ký, agent sửa...). */
  dossierLabel: (action: string | undefined) => string;
}

/** Sự kiện làm giá điều chỉnh: cổ tức (tiền hoặc cổ phiếu) và phát hành thêm. */
const PRICE_ADJUSTING_EVENTS = new Set(['Dividend', 'RightsIssue']);

const day = (timestamp: string): string => (timestamp ?? '').substring(0, 10);

function truncate(text: string | undefined, max: number): string {
  if (!text) return '(không có tiêu đề)';
  return text.length > max ? text.substring(0, max) + '…' : text;
}

/**
 * Marker của dòng thời gian đặt lên biểu đồ: giao dịch (mỗi lệnh một mũi tên), nhật ký/sự kiện/cảnh báo/hồ sơ (mỗi loại một
 * marker mỗi ngày, có số khi nhiều hơn một). Kèm các dòng tooltip. Trục thời gian và việc gắn marker vào phiên do biểu đồ lo.
 * Giá giao dịch là giá gốc còn nến đã điều chỉnh: khi chuỗi đã điều chỉnh và có cổ tức hoặc phát hành thêm SAU ngày giao dịch,
 * tooltip ghi rõ "(giá gốc)". Marker đặt theo thời gian nên vị trí không lệch.
 */
export function buildTimelineMarkers(items: readonly MarkerItem[], options: TimelineMarkerOptions): IndicatorChartMarker[] {
  const adjustingDays = options.adjusted === true
    ? items.filter(i => i.type === 'event' && PRICE_ADJUSTING_EVENTS.has(i.data?.eventType)).map(i => day(i.timestamp))
    : [];

  const byDay = new Map<string, MarkerItem[]>();
  for (const item of items) {
    const d = day(item.timestamp);
    if (!byDay.has(d)) byDay.set(d, []);
    byDay.get(d)!.push(item);
  }

  const markers: IndicatorChartMarker[] = [];
  for (const [d, dayItems] of byDay) {
    const time = dayItems[0].timestamp;

    for (const t of dayItems.filter(i => i.type === 'trade')) {
      const isBuy = t.data.tradeType === 'BUY';
      const rawPrice = adjustingDays.some(a => a > d) ? ' (giá gốc)' : '';
      const price = (t.data.price ?? 0).toLocaleString('vi-VN');
      markers.push({
        time, position: isBuy ? 'belowBar' : 'aboveBar', color: isBuy ? '#22c55e' : '#ef4444', shape: isBuy ? 'arrowUp' : 'arrowDown', text: 'T',
        tooltip: [`${isBuy ? 'MUA' : 'BÁN'} ${t.data.quantity} × ${price}đ${rawPrice}`]
      });
    }

    const folded = (type: MarkerItem['type'], letter: string, color: string, shape: 'circle' | 'square', line: (i: MarkerItem) => string) => {
      const group = dayItems.filter(i => i.type === type);
      if (group.length === 0) return;
      markers.push({
        time, position: 'aboveBar', color, shape, text: group.length > 1 ? `${letter}${group.length}` : letter,
        tooltip: group.map(line)
      });
    };
    folded('journal', 'J', '#6366f1', 'circle', i => `Nhật ký · ${truncate(i.data.title, 40)}`);
    folded('event', 'E', '#f59e0b', 'square', i => `Sự kiện · ${truncate(i.data.title, 40)}`);
    folded('alert', 'A', '#f97316', 'square', i => `Cảnh báo · ${truncate(i.data.title ?? 'Cảnh báo', 40)}`);
    folded('dossier', 'H', '#a5b4fc', 'circle', i => `Hồ sơ · ${truncate(options.dossierLabel(i.data?.action), 40)}`);
  }
  return markers;
}

/**
 * Dải cảm xúc của nhật ký thành một tầng cột: mỗi nhật ký có cảm xúc đặt mức tự tin (mặc định 5) lên phiên của nó (cuối tuần
 * thì phiên trước), màu theo cảm xúc. Nhiều nhật ký cùng phiên thì cái sau thắng. Không có nhật ký nào dùng được thì null.
 */
export function buildEmotionPane(
  dates: readonly string[], items: readonly MarkerItem[], emotionColors: Record<string, string>, fallbackColor: string
): PaneLayer | null {
  const times = dates.map(d => d.substring(0, 10));
  const values: (number | null)[] = dates.map(() => null);
  const colors: (string | null)[] = dates.map(() => null);
  let any = false;

  const journals = items
    .filter(i => i.type === 'journal' && i.data?.emotionalState)
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  for (const j of journals) {
    const k = sessionIndexFor(times, day(j.timestamp));
    if (k < 0) continue;
    values[k] = j.data.confidenceLevel ?? 5;
    colors[k] = emotionColors[j.data.emotionalState] ?? fallbackColor;
    any = true;
  }
  return any
    ? { title: 'Cảm xúc khi ghi nhật ký (cao cột = mức tự tin)', lines: [{ label: 'Tự tin', values, kind: 'histogram', colors }] }
    : null;
}
