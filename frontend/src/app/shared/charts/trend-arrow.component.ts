import { Component, Input } from '@angular/core';
import { formatSignedPct } from './chart-format';

/**
 * Mũi tên xu hướng cho một con số có kỳ so sánh: ▲/▼/▬ kèm % có dấu (hoặc ghi chú khi không có phần trăm, như "từ lỗ sang lãi").
 * Chiều theo SỐ (tăng là ▲ dù chỉ tiêu "xấu khi tăng"); màu chỉ là mã hoá phụ: xanh tăng, đỏ giảm, xám đi ngang, và `neutral` ép xám cho
 * chỉ tiêu xấu khi tăng (v1). Không có kỳ so sánh (direction null) thì KHÔNG vẽ gì: không bịa mũi tên. Dữ liệu (chiều, %) do server tính.
 */
@Component({
  selector: 'app-trend-arrow',
  standalone: true,
  template: `
    @if (direction) {
      <span data-testid="trend-arrow" class="viz-arrow" [class]="colorClass()" [attr.data-direction]="direction"
        [attr.aria-label]="ariaLabel()">
        <span aria-hidden="true">{{ glyph() }}</span> {{ text() }}@if (label) { <span class="viz-arrow-label"> {{ label }}</span> }
      </span>
    }
  `,
  styles: [`
    .viz-arrow { font-size: inherit; font-weight: 600; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
    .viz-arrow-label { font-weight: 400; color: var(--viz-text-2); }
    .viz-up { color: var(--viz-up); }
    .viz-down { color: var(--viz-down); }
    .viz-flat { color: var(--viz-flat); }
  `]
})
export class TrendArrowComponent {
  @Input() direction: 'up' | 'down' | 'flat' | null | undefined = null;
  @Input() pct: number | null | undefined = null;
  @Input() note: string | null | undefined = null;
  /** Chỉ tiêu xấu khi tăng: giữ chiều mũi tên theo số nhưng tô xám. */
  @Input() neutral = false;
  /** Chú thích đi kèm, ví dụ "so với cùng kỳ". */
  @Input() label = '';

  glyph(): string {
    return this.direction === 'up' ? '▲' : this.direction === 'down' ? '▼' : '▬';
  }

  text(): string {
    if (this.pct != null) return formatSignedPct(this.pct);
    if (this.note) return this.note;
    return '—';
  }

  colorClass(): string {
    if (this.neutral || this.direction === 'flat') return 'viz-arrow viz-flat';
    return this.direction === 'up' ? 'viz-arrow viz-up' : 'viz-arrow viz-down';
  }

  ariaLabel(): string {
    const word = this.direction === 'up' ? 'tăng' : this.direction === 'down' ? 'giảm' : 'đi ngang';
    const detail = this.pct != null ? formatSignedPct(this.pct) : (this.note ?? '');
    return [word, detail, this.label].filter(Boolean).join(' ');
  }
}
