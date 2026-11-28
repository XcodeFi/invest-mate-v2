import { TA_GROUPS, TaGroup } from './ta-group-layers';

/** Khoá localStorage nhớ các nhóm chỉ báo đã gắn sao (mảng JSON các khoá nhóm). Chỉ là tuỳ chọn giao diện, không có bề mặt MCP. */
export const GROUP_STARS_KEY = 'ta-group-stars';

const KNOWN = new Set<string>(TA_GROUPS.map(g => g.key));

/** Đọc các nhóm đã gắn sao: không đọc được, hỏng hoặc có khoá lạ thì bỏ khoá đó, không bao giờ ném lỗi. Thứ tự theo TA_GROUPS. */
export function readGroupStars(): TaGroup[] {
  try {
    const raw = JSON.parse(localStorage.getItem(GROUP_STARS_KEY) ?? '[]');
    if (!Array.isArray(raw)) return [];
    return TA_GROUPS.map(g => g.key).filter(k => raw.includes(k));
  } catch {
    return [];
  }
}

export function writeGroupStars(stars: TaGroup[]): void {
  try { localStorage.setItem(GROUP_STARS_KEY, JSON.stringify(stars.filter(k => KNOWN.has(k)))); } catch { /* bị chặn: chỉ không nhớ được */ }
}

/** Nhóm có sao đứng đầu theo thứ tự TA_GROUPS; null khi chưa gắn sao nhóm nào. Dùng để mở sẵn nhóm ở lần tải đầu khi URL không chỉ định nhóm. */
export function firstStarredGroup(): TaGroup | null {
  return readGroupStars()[0] ?? null;
}

/** Nhóm có sao lên trước, mỗi phần giữ thứ tự TA_GROUPS. */
export function orderByStars<T extends { key: TaGroup }>(groups: readonly T[], stars: readonly TaGroup[]): T[] {
  return [...groups.filter(g => stars.includes(g.key)), ...groups.filter(g => !stars.includes(g.key))];
}
