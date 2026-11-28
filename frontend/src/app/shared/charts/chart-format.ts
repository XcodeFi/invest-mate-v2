/** Định dạng số cho biểu đồ cơ bản: tiền tính bằng TỶ đồng, phần trăm một chữ số, dấu âm là dấu trừ thật (U+2212). Null luôn là dấu gạch, không bao giờ là 0. */

const MINUS = '−';
const DASH = '—';

function vi(value: number, digits: number): string {
  return value.toLocaleString('vi-VN', { minimumFractionDigits: 0, maximumFractionDigits: digits });
}

/** 12345 → "12,3 nghìn tỷ"; 1234 → "1.234 tỷ"; 45,3 → "45,3 tỷ"; 0,45 → "0,45 tỷ". Giá trị đầy đủ nên nằm ở tooltip. */
export function formatCompactVn(billions: number | null | undefined): string {
  if (billions == null || !Number.isFinite(billions)) return DASH;
  const sign = billions < 0 ? MINUS : '';
  const abs = Math.abs(billions);
  if (abs >= 10_000) return `${sign}${vi(abs / 1000, 1)} nghìn tỷ`;
  if (abs >= 100) return `${sign}${vi(abs, 0)} tỷ`;
  if (abs >= 1) return `${sign}${vi(abs, 1)} tỷ`;
  return `${sign}${vi(abs, 2)} tỷ`;
}

/** +12,3% / −8,1% / 0,0%: luôn có dấu để nghĩa không phụ thuộc màu. */
export function formatSignedPct(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return DASH;
  const rounded = Math.round(value * 10) / 10;
  if (rounded === 0) return `0,0%`;
  return `${rounded > 0 ? '+' : MINUS}${withOneDecimal(Math.abs(rounded))}%`;
}

/** 18,6% (không dấu cộng); âm có dấu trừ. */
export function formatPlainPct(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return DASH;
  const rounded = Math.round(value * 10) / 10;
  return `${rounded < 0 ? MINUS : ''}${withOneDecimal(Math.abs(rounded))}%`;
}

function withOneDecimal(abs: number): string {
  return abs.toLocaleString('vi-VN', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}

/** "Q3/2026" → "Q3/26"; chuỗi không phải nhãn quý giữ nguyên. */
export function quarterShortLabel(period: string | null | undefined): string {
  const m = /^Q([1-4])\/(\d{4})$/.exec(period ?? '');
  return m ? `Q${m[1]}/${m[2].substring(2)}` : (period ?? '');
}
