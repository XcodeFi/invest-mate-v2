import { MoodChangeDto, MoodDayDto, MoodHistoryDto, MoodState } from '../../core/services/mood.service';
import { MOOD_LABELS } from '../dashboard/widgets/patience-quotes';

/**
 * Hình dạng của mỗi tâm trạng trên lịch: biểu tượng RIÊNG (nghĩa không bao giờ chỉ nằm ở màu) và token màu (--mj-*, khai ở component, sáng/tối).
 * Bốn hue danh tính theo THỨ TỰ CỐ ĐỊNH của bảng màu mặc định (xanh, cam, ngọc, vàng); không dùng màu trạng thái xanh/đỏ cho danh mục.
 */
export interface MoodMark { glyph: string; token: string; label: string }

export const MOOD_MARK: Record<MoodState, MoodMark> = {
  Calm: { glyph: '■', token: '--mj-1', label: MOOD_LABELS.Calm },
  Fomo: { glyph: '▲', token: '--mj-2', label: MOOD_LABELS.Fomo },
  Fear: { glyph: '▼', token: '--mj-3', label: MOOD_LABELS.Fear },
  Revenge: { glyph: '✕', token: '--mj-4', label: MOOD_LABELS.Revenge },
};

export const UNRATED_MARK: MoodMark = { glyph: '□', token: '--mj-none', label: 'Chưa chấm' };

export interface Cell {
  dateKey: string;
  /** 0 = thứ Hai … 6 = Chủ nhật. */
  weekday: number;
  /** Ngày do server trả về; null khi ngày đó không có chấm tâm trạng lẫn lệnh. */
  day: MoodDayDto | null;
  /** Tâm trạng cuối ngày; null = chưa chấm. */
  mood: MoodState | null;
}

const DAY_MS = 86_400_000;

function parseKey(key: string): number {
  const [y, m, d] = key.split('-').map(Number);
  return Date.UTC(y, m - 1, d);
}

function keyOf(ms: number): string {
  return new Date(ms).toISOString().substring(0, 10);
}

/** Mỗi ngày VN từ `from` tới `to` một ô, kể cả ngày trống (chưa chấm). Chỉ ghép dữ liệu của server, không tính lại gì. */
export function buildCells(history: MoodHistoryDto): Cell[] {
  const byKey = new Map(history.days.map(d => [d.dateKey, d]));
  const cells: Cell[] = [];
  for (let t = parseKey(history.from); t <= parseKey(history.to); t += DAY_MS) {
    const dateKey = keyOf(t);
    const day = byKey.get(dateKey) ?? null;
    cells.push({ dateKey, weekday: (new Date(t).getUTCDay() + 6) % 7, day, mood: day?.mood ?? null });
  }
  return cells;
}

/** Số ô trống đầu lưới để cột đầu bắt đầu đúng thứ trong tuần (thứ Hai ở hàng đầu). */
export function leadingBlanks(cells: Cell[]): number {
  return cells.length > 0 ? cells[0].weekday : 0;
}

export interface MoodCounts { Calm: number; Fomo: number; Fear: number; Revenge: number; unrated: number }

export function countByMood(cells: Cell[]): MoodCounts {
  const counts: MoodCounts = { Calm: 0, Fomo: 0, Fear: 0, Revenge: 0, unrated: 0 };
  for (const c of cells) {
    if (c.mood) counts[c.mood]++;
    else counts.unrated++;
  }
  return counts;
}

/** Các ngày server đánh dấu đặt lệnh khi đang cảm xúc, mới nhất trước. Cờ do server tính; ở đây không suy ra lại. */
export function emotionalDays(history: MoodHistoryDto): MoodDayDto[] {
  return history.days.filter(d => d.emotionalTrade).sort((a, b) => b.dateKey.localeCompare(a.dateKey));
}

const VN_TIME = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', hour12: false });

export function changeLine(c: MoodChangeDto): string {
  const at = c.at.endsWith('Z') || /[+-]\d{2}:?\d{2}$/.test(c.at) ? c.at : c.at + 'Z';
  return `${VN_TIME.format(new Date(at))} ${MOOD_LABELS[c.mood]}${c.source === 'mcp' ? ' (từ agent)' : ''}`;
}

export function formatDateKey(key: string): string {
  const [y, m, d] = key.split('-');
  return `${d}/${m}/${y}`;
}

/** Nhãn đầy đủ của một ô: dùng cho aria-label, tooltip và bảng chi tiết ngày. */
export function cellLabel(cell: Cell): string {
  const parts = [formatDateKey(cell.dateKey), cell.mood ? MOOD_LABELS[cell.mood] : 'chưa chấm'];
  const day = cell.day;
  if (day) {
    if (day.changes.length > 0) parts.push('các lần đổi: ' + day.changes.map(changeLine).join(', '));
    if (day.tradeCount > 0) parts.push(`${day.tradeCount} lệnh`);
    if (day.overrode) parts.push('đã bấm qua lớp phủ');
    if (day.emotionalTrade) parts.push('đặt lệnh khi đang cảm xúc');
  }
  return parts.join(' · ');
}
