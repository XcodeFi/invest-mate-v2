import { Theme } from '../../core/services/theme.service';

/**
 * Màu cho các biểu đồ Chart.js (trang Phân tích, Replay kế hoạch). Biểu đồ nến lightweight-charts có bộ màu riêng ở
 * `indicator-chart.theme.ts`. Chế độ sáng giữ đúng mặc định cũ của Chart.js (chữ #666, lưới đen 5%).
 */
export interface ChartJsColors {
  /** Chữ trục, chú giải. */
  text: string;
  /** Đường lưới. */
  grid: string;
  /** Viền giữa các miếng bánh (trùng màu nền thẻ để miếng như tách rời). */
  slice: string;
}

export function chartJsColors(theme: Theme): ChartJsColors {
  return theme === 'dark'
    ? { text: '#cbd5e1', grid: 'rgba(148, 163, 184, 0.18)', slice: '#111827' }
    : { text: '#666666', grid: 'rgba(0, 0, 0, 0.05)', slice: '#f9fafb' };
}

/** Đặt màu mặc định toàn cục của Chart.js (chữ, viền lưới). Chart đã vẽ phải được dựng lại hoặc update() để nhận màu mới. */
export function applyChartJsDefaults(chart: { defaults: { color: unknown; borderColor: unknown } }, theme: Theme): void {
  const colors = chartJsColors(theme);
  chart.defaults.color = colors.text;
  chart.defaults.borderColor = colors.grid;
}
