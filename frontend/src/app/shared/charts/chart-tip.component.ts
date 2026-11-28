import { Component, Input } from '@angular/core';

/** Nội dung một tooltip. Mọi chuỗi chỉ được hiển thị dạng văn bản (nội suy Angular, không HTML): tên và nhãn đến từ dữ liệu bên ngoài. */
export interface ChartTip {
  /** Toạ độ so với khung chứa biểu đồ (position: relative), px. */
  x: number;
  y: number;
  title: string;
  rows: { label: string; value: string; /** Màu khoá nhỏ (CSS color), tuỳ chọn. */ key?: string }[];
  note?: string;
}

const TIP_WIDTH = 170;

/**
 * Cạnh trái và phải (theo toạ độ của `host`) của phần ĐANG NHÌN THẤY: biểu đồ có thể rộng hơn khung cuộn bao quanh nó (điện thoại), khi đó tooltip phải
 * kẹp theo khung cuộn chứ không theo cả biểu đồ.
 */
function visibleBounds(host: HTMLElement): { left: number; right: number } {
  const rect = host.getBoundingClientRect();
  const scroller = host.closest('[data-testid="chart-body"]') as HTMLElement | null;
  if (!scroller) return { left: 0, right: rect.width };
  const s = scroller.getBoundingClientRect();
  return { left: Math.max(s.left - rect.left, 0), right: Math.min(s.right - rect.left, rect.width) };
}

function clampX(x: number, host: HTMLElement): number {
  const { left, right } = visibleBounds(host);
  return Math.max(Math.min(x, right - TIP_WIDTH), left);
}

/** Vị trí tooltip theo con trỏ, kẹp trong phần nhìn thấy của khung để không tràn mép. */
export function tipPosition(event: MouseEvent, host: HTMLElement): { x: number; y: number } {
  const rect = host.getBoundingClientRect();
  return { x: clampX(event.clientX - rect.left + 12, host), y: Math.max(event.clientY - rect.top + 12, 0) };
}

/** Vị trí tooltip khi tiêu điểm bàn phím rơi vào một phần tử (cùng nội dung như khi rê chuột). */
export function tipPositionFromElement(el: Element, host: HTMLElement): { x: number; y: number } {
  const rect = host.getBoundingClientRect();
  const r = el.getBoundingClientRect();
  const x = clampX(r.left - rect.left + r.width / 2, host);
  return { x, y: Math.max(r.top - rect.top + r.height + 6, 0) };
}

/** Tooltip dùng chung: giá trị là phần nổi bật, nhãn đứng sau; mỗi dòng có khoá màu ngắn thay cho ô vuông. */
@Component({
  selector: 'app-chart-tip',
  standalone: true,
  template: `
    @if (tip) {
      <div role="tooltip" data-testid="chart-tip" class="viz-tip" [style.left.px]="tip.x" [style.top.px]="tip.y">
        <div class="viz-tip-title">{{ tip.title }}</div>
        @for (row of tip.rows; track $index) {
          <div class="viz-tip-row" data-testid="chart-tip-row">
            @if (row.key) { <span class="viz-tip-key" [style.background]="row.key"></span> }
            <strong class="viz-tip-value">{{ row.value }}</strong>
            <span class="viz-tip-label">{{ row.label }}</span>
          </div>
        }
        @if (tip.note) { <div class="viz-tip-note">{{ tip.note }}</div> }
      </div>
    }
  `,
  styles: [`
    :host { display: contents; }
    .viz-tip {
      position: absolute; z-index: 20; pointer-events: none; min-width: 9rem; max-width: 15rem;
      padding: 6px 8px; border-radius: 6px; font-size: 11px; line-height: 1.35;
      background: var(--viz-surface); color: var(--viz-text);
      border: 1px solid var(--viz-axis); box-shadow: 0 2px 8px rgba(0, 0, 0, 0.25);
    }
    .viz-tip-title { font-weight: 600; margin-bottom: 2px; }
    .viz-tip-row { display: flex; align-items: center; gap: 6px; }
    .viz-tip-key { display: inline-block; width: 10px; height: 2px; flex: none; }
    .viz-tip-value { font-weight: 600; }
    .viz-tip-label, .viz-tip-note { color: var(--viz-text-2); }
    .viz-tip-note { margin-top: 2px; }
  `]
})
export class ChartTipComponent {
  @Input() tip: ChartTip | null = null;
}
