import { Component, Input } from '@angular/core';
import { ChartFrameComponent } from './chart-frame.component';
import { formatPlainPct } from './chart-format';

/**
 * Vùng giá 52 tuần: thanh ngang đáy → đỉnh với một chấm đánh dấu giá hiện tại (vị trí do server tính, kẹp 0–100). Nhãn: đáy, đỉnh, hiện tại.
 * Đơn vị nguồn là nghìn đồng, hiển thị đổi sang đồng. Thiếu giá thì vẫn vẽ vùng đáy–đỉnh kèm "Chưa có giá hiện tại" (không đặt chấm ở 0).
 */
@Component({
  selector: 'app-range-bar',
  standalone: true,
  imports: [ChartFrameComponent],
  template: `
    <app-chart-frame title="Vùng giá 52 tuần" [hasData]="low != null && high != null" testId="range-bar">
      <div class="rb-wrap">
        <div class="rb-track" role="img" [attr.aria-label]="ariaLabel">
          @if (positionPct != null) {
            <span class="rb-dot" data-testid="rb-dot" [style.left.%]="positionPct"></span>
          }
        </div>
        <div class="rb-labels">
          <span data-testid="rb-low">Đáy {{ vnd(low) }}</span>
          <span data-testid="rb-price" class="rb-price">
            @if (price != null) { Hiện tại {{ vnd(price) }}@if (positionPct != null) { · {{ pct(positionPct) }} vùng } }
            @else { Chưa có giá hiện tại }
          </span>
          <span data-testid="rb-high">Đỉnh {{ vnd(high) }}</span>
        </div>
        @if (outsideRange) {
          <p class="rb-note" data-testid="rb-outside">Giá nằm ngoài vùng đáy–đỉnh đã công bố (số 52 tuần của nguồn có thể cũ).</p>
        }
      </div>
      <table table class="rb-table">
        <thead><tr><th>Đáy 52T</th><th>Đỉnh 52T</th><th>Giá hiện tại</th><th>Vị trí</th></tr></thead>
        <tbody><tr data-testid="rb-row"><td>{{ vnd(low) }}</td><td>{{ vnd(high) }}</td><td>{{ vnd(price) }}</td><td>{{ positionPct != null ? pct(positionPct) : '—' }}</td></tr></tbody>
      </table>
    </app-chart-frame>
  `,
  styles: [`
    :host { display: block; }
    .rb-wrap { min-width: 260px; padding: 6px 6px 0; }
    .rb-track { position: relative; height: 8px; border-radius: 4px; background: var(--viz-track); }
    .rb-dot { position: absolute; top: 50%; width: 12px; height: 12px; margin: -6px 0 0 -6px; border-radius: 50%; background: var(--viz-series-1); box-shadow: 0 0 0 2px var(--viz-surface); }
    .rb-labels { display: flex; justify-content: space-between; gap: 0.5rem; margin-top: 0.5rem; font-size: 0.6875rem; color: var(--viz-text-2); font-variant-numeric: tabular-nums; }
    .rb-price { color: var(--viz-text); font-weight: 600; text-align: center; }
    .rb-note { margin-top: 0.25rem; font-size: 0.6875rem; color: var(--viz-text-2); }
    .rb-table { width: 100%; font-size: 0.6875rem; color: var(--viz-text); border-collapse: collapse; }
    .rb-table th { text-align: right; font-weight: 400; color: var(--viz-text-2); padding: 2px 4px; }
    .rb-table td { text-align: right; padding: 2px 4px; border-top: 1px solid var(--viz-grid); }
  `]
})
export class RangeBarComponent {
  /** Đáy/đỉnh/giá theo nghìn đồng, như nguồn trả về. */
  @Input() low: number | null | undefined = null;
  @Input() high: number | null | undefined = null;
  @Input() price: number | null | undefined = null;
  /** 0–100, do server tính. */
  @Input() positionPct: number | null | undefined = null;
  @Input() outsideRange = false;

  vnd(thousands: number | null | undefined): string {
    return thousands == null ? '—' : `${Math.round(thousands * 1000).toLocaleString('vi-VN')} đ`;
  }

  pct(v: number): string { return formatPlainPct(v); }

  get ariaLabel(): string {
    return `Giá ${this.price != null ? this.vnd(this.price) : 'chưa có'} trong vùng ${this.vnd(this.low)} đến ${this.vnd(this.high)}`;
  }
}
