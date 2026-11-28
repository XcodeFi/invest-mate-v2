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
  /** Chỉ cho cột (histogram): màu riêng từng cột, cùng độ dài với dates; null/thiếu thì dùng `color`. */
  colors?: (string | null)[];
}

/** Vạch ngang tham chiếu có nghĩa: value trên thang của tầng, label hiện cạnh vạch và trong chú giải ("Quá mua"). */
export interface GuideLine {
  value: number;
  label: string;
}

/** Một tầng biểu đồ riêng (thang riêng) xếp dưới tầng giá. guides = vạch ngang tham chiếu (30/70, 20/80, 25). */
export interface PaneLayer {
  title: string;
  lines: LineLayer[];
  guides?: GuideLine[];
}

export interface PriceLineLayer {
  label: string;
  /** Tiêu đề NGẮN vẽ ngay trên biểu đồ (để các nhãn không chồng lên nhau ở mép phải); chú giải bên dưới vẫn dùng `label` đầy đủ. */
  shortLabel?: string;
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

/**
 * Một mốc đặt lên chuỗi giá (giao dịch, nhật ký, sự kiện...). `time` là ngày ISO (lấy 10 ký tự đầu); ngày không có phiên
 * thì gắn vào phiên gần nhất phía trước. `tooltip` là các dòng hiện khi trỏ chuột vào phiên đó (chỉ textContent).
 */
export interface IndicatorChartMarker {
  time: string;
  position: 'aboveBar' | 'belowBar';
  color: string;
  shape: 'arrowUp' | 'arrowDown' | 'circle' | 'square';
  /** Tên sắc màu theo giao diện (xem ChartColors): có thì biểu đồ lấy màu theo chế độ sáng/tối thay cho `color` để chữ marker luôn đọc được. */
  tone?: 'trapBull' | 'trapBear';
  text: string;
  tooltip?: string[];
}
