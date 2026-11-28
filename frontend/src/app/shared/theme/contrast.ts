/** Tính độ tương phản WCAG 2.x giữa hai màu, để chặn cặp chữ/nền khó đọc trong chế độ tối. */

export type Rgb = [number, number, number];

export function parseHex(hex: string): Rgb {
  const h = hex.trim().replace('#', '');
  const full = h.length === 3 ? h.split('').map(c => c + c).join('') : h;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) throw new Error(`Màu hex không hợp lệ: ${hex}`);
  return [parseInt(full.slice(0, 2), 16), parseInt(full.slice(2, 4), 16), parseInt(full.slice(4, 6), 16)];
}

export function toHex(rgb: Rgb): string {
  return '#' + rgb.map(c => Math.max(0, Math.min(255, Math.round(c))).toString(16).padStart(2, '0')).join('');
}

/** Độ sáng tương đối (0 = đen, 1 = trắng) theo WCAG. */
export function relativeLuminance(rgb: Rgb): number {
  const [r, g, b] = rgb.map(v => {
    const c = v / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** Tỉ số tương phản 1–21 giữa hai màu hex (không phụ thuộc thứ tự). */
export function contrastRatio(a: string, b: string): number {
  const la = relativeLuminance(parseHex(a));
  const lb = relativeLuminance(parseHex(b));
  const [hi, lo] = la >= lb ? [la, lb] : [lb, la];
  return (hi + 0.05) / (lo + 0.05);
}

/** Màu `fg` với độ trong suốt `alpha` (0–1) đặt lên nền `bgHex`: màu thật người xem thấy, dùng để đo chữ trên ô tô màu mờ. */
export function blendOver(fg: Rgb, alpha: number, bgHex: string): string {
  const bg = parseHex(bgHex);
  return toHex([0, 1, 2].map(i => fg[i] * alpha + bg[i] * (1 - alpha)) as Rgb);
}

/**
 * Làm sáng màu đường biểu đồ khi nó quá tối so với nền tối (tương phản dưới `minRatio`): trộn dần với trắng cho tới khi đủ.
 * Màu đã đủ tương phản thì giữ nguyên, nên các màu nhận diện được (xanh, đỏ, cam...) không đổi.
 */
export function ensureContrastOnDark(colorHex: string, bgHex: string, minRatio = 3): string {
  let rgb: Rgb;
  try {
    rgb = parseHex(colorHex);
  } catch {
    return colorHex;   // rgba(...), tên màu, hex ngắn: không đoán, giữ nguyên thay vì làm trắng cả biểu đồ
  }
  for (let step = 0; step < 20 && contrastRatio(toHex(rgb), bgHex) < minRatio; step++) {
    rgb = rgb.map(c => c + (255 - c) * 0.15) as Rgb;
  }
  return toHex(rgb);
}
