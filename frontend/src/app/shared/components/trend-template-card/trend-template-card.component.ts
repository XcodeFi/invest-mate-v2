import { Component, Input } from '@angular/core';
import { TrendTemplate, TrendTemplateCriterion } from '../../../core/services/market-data.service';

/**
 * Thẻ "Trend Template (Mark Minervini)": mỗi tiêu chí một dòng với giá trị và khoảng cách theo %. KHÔNG có kết luận tổng "đạt" và không đếm số tiêu chí đạt
 * (sẽ thành tín hiệu mua). Thiếu dữ liệu thì ghi lý do, không bao giờ là 0. Nghĩa của mỗi dòng nằm ở chữ và ký hiệu, không chỉ ở màu.
 */
@Component({
  selector: 'app-trend-template-card',
  standalone: true,
  host: { class: 'block' },
  template: `
    @if (template; as t) {
      <section class="mb-4 rounded-lg border border-gray-200 bg-white px-4 py-3" data-testid="tt-card">
        <h3 class="text-sm font-bold text-gray-800">Trend Template (Mark Minervini)</h3>
        <p class="mt-0.5 text-xs text-gray-500">{{ t.note }}</p>
        <ul class="mt-2 divide-y divide-gray-100 text-sm">
          @for (c of t.criteria; track c.key) {
            <li class="py-1.5 flex flex-wrap items-baseline gap-x-2 gap-y-0.5" data-testid="tt-row" [attr.data-met]="state(c)">
              <span class="w-24 shrink-0 text-xs font-semibold" [class]="stateClass(c)">{{ stateIcon(c) }} {{ stateText(c) }}</span>
              <span class="flex-1 min-w-[12rem] text-gray-800">{{ c.rule }}</span>
              @if (c.value !== null && c.value !== undefined) {
                <span class="tabular-nums text-gray-700" data-testid="tt-value">{{ format(c) }}</span>
              } @else if (c.detail) {
                <span class="text-xs text-gray-500" data-testid="tt-detail">{{ c.detail }}</span>
              }
            </li>
          }
        </ul>
        <p class="mt-2 text-xs text-gray-500">
          Nguồn các tiêu chí: Mark Minervini, Trade Like a Stock Market Wizard (2013). Ông dùng đường trung bình đơn giản (SMA) 50, 150, 200 ngày; thẻ này cũng dùng SMA,
          khác với EMA vẽ trên biểu đồ. Dùng như bộ lọc rủi ro theo xu hướng, không phải công cụ dự báo.
        </p>
      </section>
    }
  `
})
export class TrendTemplateCardComponent {
  @Input() template: TrendTemplate | null | undefined = null;

  state(c: TrendTemplateCriterion): 'true' | 'false' | 'none' {
    return c.met === true ? 'true' : c.met === false ? 'false' : 'none';
  }
  stateIcon(c: TrendTemplateCriterion): string {
    return c.met === true ? '✓' : c.met === false ? '✗' : '—';
  }
  stateText(c: TrendTemplateCriterion): string {
    return c.met === true ? 'thoả' : c.met === false ? 'chưa thoả' : 'Không đủ dữ liệu';
  }
  stateClass(c: TrendTemplateCriterion): string {
    return c.met === true ? 'text-green-600' : c.met === false ? 'text-red-600' : 'text-gray-500';
  }

  /** Khoảng cách theo %, dấu thập phân phẩy; dòng so sánh (ngưỡng 0) có dấu +/−, dòng có ngưỡng khác 0 là số dương "cách" hay "cao hơn". */
  format(c: TrendTemplateCriterion): string {
    const v = c.value as number;
    const abs = Math.abs(v).toLocaleString('vi-VN', { maximumFractionDigits: 2 });
    const signed = c.threshold === 0 || c.threshold === null || c.threshold === undefined;
    const sign = signed ? (v > 0 ? '+' : v < 0 ? '−' : '') : '';
    return `${sign}${abs}${c.unit ?? ''}`;
  }
}
