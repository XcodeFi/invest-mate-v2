import { Theme } from '../../../core/services/theme.service';

/** Màu của biểu đồ theo chế độ giao diện. Nền tối trùng bề mặt thẻ (`--dm-surface` trong dark-theme.css). */
export interface ChartColors {
  background: string;
  text: string;
  grid: string;
  border: string;
  /** Vạch trỏ chuột và nhãn của nó. */
  crosshair: string;
  crosshairLabel: string;
  /** Chữ thay đổi giá (tăng/giảm) trên dòng OHLC. */
  upText: string;
  downText: string;
  /** Chữ và mũi tên marker bẫy tăng (phá vỡ thất bại) và bẫy giảm: là CHỮ trên canvas nên mỗi chế độ có sắc riêng đạt >= 4,5:1 trên nền biểu đồ. */
  trapBull: string;
  trapBear: string;
  /** Màu các đường ngang: cắt lỗ/kháng cự, chốt lời/hỗ trợ/mục tiêu, giá vào, Fibonacci, swing (nhãn của chúng là chữ cùng màu). */
  lineLoss: string;
  lineProfit: string;
  lineEntry: string;
  lineFib: string;
  lineSwing: string;
}

const LIGHT: ChartColors = {
  background: '#ffffff', text: '#374151', grid: '#f3f4f6', border: '#e5e7eb',
  crosshair: '#758696', crosshairLabel: '#4c525e', upText: '#16a34a', downText: '#dc2626',
  trapBull: '#dc2626', trapBear: '#15803d',
  lineLoss: '#dc2626', lineProfit: '#15803d', lineEntry: '#2563eb', lineFib: '#b45309', lineSwing: '#64748b'
};

const DARK: ChartColors = {
  background: '#111827', text: '#cbd5e1', grid: '#1f2937', border: '#334155',
  crosshair: '#64748b', crosshairLabel: '#334155', upText: '#4ade80', downText: '#f87171',
  trapBull: '#f87171', trapBear: '#4ade80',
  lineLoss: '#f87171', lineProfit: '#4ade80', lineEntry: '#60a5fa', lineFib: '#fbbf24', lineSwing: '#94a3b8'
};

export function chartColors(theme: Theme): ChartColors {
  return theme === 'dark' ? DARK : LIGHT;
}
