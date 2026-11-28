import { NAV_GROUPS } from '../../nav/nav-config';

export interface PaletteItem {
  kind: 'page' | 'symbol';
  /** Đường dẫn đích (đã có mã nếu là mục mã). */
  path: string;
  label: string;
  icon: string;
  /** Tên nhóm menu (mục trang) hoặc gợi ý ngắn (mục mã). */
  hint: string;
}

/** Bỏ dấu tiếng Việt và hạ chữ thường để "ke hoach" tìm được "Kế hoạch GD". */
export function normalize(text: string): string {
  return (text ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase().trim();
}

/** Mã chứng khoán: chữ cái đầu, tổng 2–10 ký tự chữ/số (VCB, FPT, E1VFVN30). Không có dấu hay khoảng trắng. */
export const SYMBOL_PATTERN = /^[A-Z][A-Z0-9]{1,9}$/;

/** Trang lấy từ CÙNG cấu hình với menu trái và menu điện thoại; không có danh sách riêng. */
export function allPages(): PaletteItem[] {
  return NAV_GROUPS.flatMap(g => g.items.map(i => ({ kind: 'page' as const, path: i.path, label: i.label, icon: i.icon, hint: g.label })));
}

/** Các mục hiện ra cho ô nhập: trang khớp trước (mọi từ đều phải có mặt), rồi một dòng "Mở mã X" nếu ô nhập giống một mã. */
export function buildItems(query: string): PaletteItem[] {
  const q = (query ?? '').trim();
  const tokens = normalize(q).split(/\s+/).filter(Boolean);
  const pages = allPages().filter(p => {
    const hay = normalize(p.label + ' ' + p.hint);
    return tokens.every(t => hay.includes(t));
  });
  const symbol = q.toUpperCase();
  if (SYMBOL_PATTERN.test(symbol)) {
    pages.push({ kind: 'symbol', path: `/symbol-timeline/${symbol}`, label: `Mở mã ${symbol}`, icon: '🕒', hint: 'Dòng thời gian mã' });
  }
  return pages;
}
