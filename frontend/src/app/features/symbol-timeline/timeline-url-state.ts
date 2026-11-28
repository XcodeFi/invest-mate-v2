import { ParamMap } from '@angular/router';
import { TA_GROUPS, TaGroup } from '../../shared/indicators/ta-group-layers';

/**
 * Trạng thái của trang Dòng thời gian mã nằm trên URL: `range` (1T/3T/6T/1N), `grp` (nhóm chỉ báo) và `focus` (ngày của mục đang chọn).
 * Chỉ ba khoá này; `symbol` và `tradeId` vẫn do logic sẵn có của trang quản lý. Khoá vắng hoặc sai giá trị thì `undefined` (không có ý kiến).
 */
export const RANGE_BY_KEY: Record<string, number> = { '1T': 1, '3T': 3, '6T': 6, '1N': 12 };

export interface TimelineUrlState {
  /** Số tháng. */
  range?: number;
  group?: TaGroup;
  /** Ngày YYYY-MM-DD (giờ VN). Chỉ xác định ngày, không xác định mục: nhiều mục cùng ngày thì chọn mục đầu tiên của ngày đó. */
  focus?: string;
}

const GROUP_KEYS = new Set<string>(TA_GROUPS.map(g => g.key));

/** Số tháng → nhãn trên URL (null nếu không phải một trong bốn khoảng). */
export function rangeKey(months: number): string | null {
  return Object.keys(RANGE_BY_KEY).find(k => RANGE_BY_KEY[k] === months) ?? null;
}

function isRealDate(text: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(text)) return false;
  const [y, m, d] = text.split('-').map(Number);
  const date = new Date(Date.UTC(y, m - 1, d));
  return date.getUTCFullYear() === y && date.getUTCMonth() === m - 1 && date.getUTCDate() === d;
}

export function parseTimelineQuery(params: ParamMap): TimelineUrlState {
  const state: TimelineUrlState = {};
  const range = params.get('range');
  if (range && Object.hasOwn(RANGE_BY_KEY, range)) state.range = RANGE_BY_KEY[range];
  const grp = params.get('grp');
  if (grp && GROUP_KEYS.has(grp)) state.group = grp as TaGroup;
  const focus = params.get('focus');
  if (focus && isRealDate(focus)) state.focus = focus;
  return state;
}

/** Tham số query để ghi lên URL cùng `queryParamsHandling: 'merge'`: null xoá khoá. `range` luôn được ghi tường minh. */
export function buildTimelineQuery(state: { range: number; group: TaGroup | null; focus: string | null }): Record<string, string | null> {
  return { range: rangeKey(state.range), grp: state.group, focus: state.focus };
}
