import { TechnicalSeries } from '../../core/services/market-data.service';

/**
 * Cắt chuỗi chỉ báo từ ngày `fromDay` (YYYY-MM-DD, gồm cả ngày đó) trở đi. Chỉ báo được tính trên chuỗi dài hơn khoảng hiển thị
 * (để EMA 200, MACD... có đủ dữ liệu khởi động) rồi cắt ở đây, nên đầu khoảng không bị trống. Mọi mảng cùng độ dài với `dates`
 * đều được cắt; mảng rỗng hoặc không có (backend cũ) giữ nguyên. Không sửa đầu vào.
 */
export function sliceSeriesFrom(series: TechnicalSeries, fromDay: string): TechnicalSeries {
  const n = series.dates.length;
  const start = series.dates.findIndex(d => d.substring(0, 10) >= fromDay);
  const from = start < 0 ? n : start;
  const out: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(series)) {
    out[key] = Array.isArray(value) && value.length === n ? value.slice(from) : value;
  }
  return out as unknown as TechnicalSeries;
}
