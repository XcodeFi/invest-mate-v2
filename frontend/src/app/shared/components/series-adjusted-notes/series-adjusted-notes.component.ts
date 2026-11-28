import { Component, Input } from '@angular/core';
import { formatAsOf, tradingDaysBehind } from '../../indicators/adjusted-age';

/** Phần của chuỗi chỉ báo mà ghi chú cần: cờ đã điều chỉnh và ngày nến cuối của chuỗi đã điều chỉnh. */
export interface AdjustedInfo {
  adjusted?: boolean | null;
  adjustedAsOf?: string | null;
}

/**
 * Hai ghi chú về chất lượng giá của chuỗi chỉ báo: chưa điều chỉnh theo sự kiện quyền (nguồn dự phòng) và chuỗi đã điều chỉnh
 * cũ hơn 1 phiên (nguồn bị chặn, dùng bản lưu). Truyền null khi chưa có chuỗi hoặc chưa chọn nhóm. Đặt NGOÀI khối dính.
 */
@Component({
  selector: 'app-series-adjusted-notes',
  standalone: true,
  template: `
    @if (series && series.adjusted === false) {
      <p class="mb-4 text-xs text-amber-700 bg-amber-50 border border-amber-100 rounded-lg px-3 py-2" data-testid="ta-unadjusted-note">
        Dữ liệu chưa điều chỉnh theo sự kiện quyền, nến chỉ có giá đóng cửa: quanh ngày giao dịch không hưởng quyền, giá có thể tụt giả và làm lệch chỉ báo.
      </p>
    }
    @if (series && staleDate) {
      <p class="mb-4 text-xs text-amber-700 bg-amber-50 border border-amber-100 rounded-lg px-3 py-2" data-testid="ta-adjusted-asof-note">
        Dữ liệu đã điều chỉnh cập nhật tới {{ staleDate }}: các phiên gần đây chưa có trong chuỗi này.
      </p>
    }
  `
})
export class SeriesAdjustedNotesComponent {
  @Input() series: AdjustedInfo | null = null;

  /** dd/MM/yyyy của chuỗi đã điều chỉnh khi nó lùi hơn 1 phiên so với hôm nay; rỗng khi không cần ghi chú. */
  get staleDate(): string {
    const asOf = this.series?.adjustedAsOf;
    return tradingDaysBehind(asOf, new Date()) > 1 ? formatAsOf(asOf) : '';
  }
}
