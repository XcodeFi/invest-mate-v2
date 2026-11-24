/**
 * Kiểu dữ liệu của biểu đồ nhiều tầng. Nằm ở shared/ để component biểu đồ không phải import ngược
 * từ một feature; mỗi trang tự dựng GroupLayers theo ngữ cảnh của mình (xem ta-group-layers.ts).
 */

export interface LineLayer {
  label: string;
  /** Mảng cùng độ dài với dates; null = chưa có điểm (bỏ qua khi vẽ, không phải 0). */
  values: (number | null)[];
  color?: string;
  kind?: 'line' | 'histogram';
}

/** Một tầng biểu đồ riêng (thang riêng) xếp dưới tầng giá. guides = vạch ngang tham chiếu (30/70, 20/80, 25). */
export interface PaneLayer {
  title: string;
  lines: LineLayer[];
  guides?: number[];
}

export interface PriceLineLayer {
  label: string;
  price: number;
}

/** Mọi thứ cần vẽ: đường đè lên nến, các tầng dưới, đường ngang trên tầng giá, và ghi chú (thiếu dữ liệu...). */
export interface GroupLayers {
  overlays: LineLayer[];
  panes: PaneLayer[];
  priceLines: PriceLineLayer[];
  notes: string[];
}

export interface IndicatorChartCandles {
  open: number[];
  high: number[];
  low: number[];
  close: number[];
}
