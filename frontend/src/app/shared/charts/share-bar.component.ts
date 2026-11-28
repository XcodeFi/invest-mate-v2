import { Component, Input } from '@angular/core';
import { ChartFrameComponent } from './chart-frame.component';
import { ChartTip, ChartTipComponent, tipPosition, tipPositionFromElement } from './chart-tip.component';
import { formatPlainPct } from './chart-format';

interface Segment { name: string; pct: number; color: string; onFill: string; other: boolean }

/** Bốn slot đầu của bảng màu theo THỨ TỰ CỐ ĐỊNH (xanh, cam, ngọc, vàng), "Khác" là màu trung tính. Màu theo vị trí xếp hạng ở đây vì danh sách luôn là top 4 hiện tại. */
const SLOT_COLORS = ['var(--viz-series-1)', 'var(--viz-series-2)', 'var(--viz-series-3)', 'var(--viz-series-4)'];
/** Chữ đặt trong khối màu: mực tối cho MỌI khối (trắng chỉ đạt 2,0–4,1:1 trên các màu của bảng, tối đa 4,8:1 cho mực tối ở xanh sáng). */
const ON_FILL = ['var(--viz-on-fill)', 'var(--viz-on-fill)', 'var(--viz-on-fill)', 'var(--viz-on-fill)'];
const MIN_LABEL_PCT = 12;

/**
 * Cơ cấu cổ đông: MỘT thanh xếp chồng 100% (không phải bánh hay donut): 4 cổ đông lớn nhất + "Khác" (phần còn lại để đủ 100%). Khối cách nhau 2px nền;
 * nhãn % chỉ đặt trong khối khi đủ chỗ (khối hẹp để chú giải và tooltip mang số), chú giải luôn có và ghi tên + %. Chữ nhãn dùng màu chữ, chỉ nhãn TRONG khối
 * dùng trắng/đen theo độ sáng nền. Tên cổ đông từ nguồn ngoài: luôn hiển thị dạng văn bản.
 */
@Component({
  selector: 'app-share-bar',
  standalone: true,
  imports: [ChartFrameComponent, ChartTipComponent],
  template: `
    <app-chart-frame title="Cơ cấu cổ đông" [hasData]="segments.length > 0" testId="share-bar">
      <div class="sb-wrap" #wrap>
        <div class="sb-bar" role="img" [attr.aria-label]="ariaAll">
          @for (s of segments; track s.name) {
            <div class="sb-seg" data-testid="sb-seg" [attr.data-name]="s.name" [attr.data-pct]="s.pct" tabindex="0"
              [style.flex-grow]="s.pct" [style.background]="s.color" [attr.aria-label]="s.name + ': ' + pctText(s.pct)"
              (pointermove)="hover(s, $event, wrap)" (pointerleave)="tip = null" (focus)="focusTip(s, $event, wrap)" (blur)="tip = null">
              @if (s.pct >= minLabelPct) { <span class="sb-in" data-testid="sb-in" [style.color]="s.onFill">{{ pctText(s.pct) }}</span> }
            </div>
          }
        </div>
        <ul class="sb-legend" data-testid="sb-legend">
          @for (s of segments; track s.name) {
            <li><i class="sb-key" [style.background]="s.color"></i> {{ s.name }} <strong>{{ pctText(s.pct) }}</strong></li>
          }
        </ul>
        <app-chart-tip [tip]="tip"></app-chart-tip>
      </div>
      <table table class="sb-table">
        <thead><tr><th>Cổ đông</th><th>Tỷ lệ</th></tr></thead>
        <tbody>
          @for (s of segments; track s.name) {
            <tr data-testid="sb-row"><td>{{ s.name }}</td><td>{{ pctText(s.pct) }}</td></tr>
          }
        </tbody>
      </table>
    </app-chart-frame>
  `,
  styles: [`
    :host { display: block; }
    .sb-wrap { position: relative; min-width: 260px; }
    .sb-bar { display: flex; gap: 2px; height: 28px; }
    .sb-seg { flex-basis: 0; min-width: 4px; border-radius: 4px; display: flex; align-items: center; padding-left: 6px; outline: none; }
    .sb-seg:hover, .sb-seg:focus { filter: brightness(1.12); }
    .sb-in { font-size: 0.6875rem; font-weight: 600; white-space: nowrap; }
    .sb-legend { display: flex; flex-wrap: wrap; gap: 0.25rem 0.75rem; margin-top: 0.5rem; font-size: 0.6875rem; color: var(--viz-text); }
    .sb-key { display: inline-block; width: 10px; height: 10px; border-radius: 2px; margin-right: 3px; vertical-align: -1px; }
    .sb-table { width: 100%; font-size: 0.6875rem; color: var(--viz-text); border-collapse: collapse; }
    .sb-table th { text-align: right; font-weight: 400; color: var(--viz-text-2); padding: 2px 4px; }
    .sb-table td { text-align: right; padding: 2px 4px; border-top: 1px solid var(--viz-grid); }
    .sb-table th:first-child, .sb-table td:first-child { text-align: left; }
  `]
})
export class ShareBarComponent {
  @Input() holders: { name: string | null; percentage: number }[] = [];

  readonly minLabelPct = MIN_LABEL_PCT;
  tip: ChartTip | null = null;

  /** Top 4 theo tỷ lệ + "Khác" = phần còn lại đến 100 (không âm). Cổ đông tỷ lệ ≤ 0 hay thiếu tên bị bỏ. */
  get segments(): Segment[] {
    const named = (this.holders ?? [])
      .filter(h => h.percentage > 0 && h.name && h.name.trim())
      .sort((a, b) => b.percentage - a.percentage)
      .slice(0, 4);
    if (named.length === 0) return [];
    const segs: Segment[] = named.map((h, i) => ({ name: h.name!.trim(), pct: h.percentage, color: SLOT_COLORS[i], onFill: ON_FILL[i], other: false }));
    const total = segs.reduce((s, x) => s + x.pct, 0);
    if (total > 100) {
      // Dữ liệu vô lý (tổng top 4 quá 100%): quy về tỷ lệ để thanh vẫn đúng 100%, không tạo "Khác" âm.
      return segs.map(s => ({ ...s, pct: (s.pct / total) * 100 }));
    }
    const rest = Math.round((100 - total) * 100) / 100;
    if (rest > 0) segs.push({ name: 'Khác', pct: rest, color: 'var(--viz-neutral)', onFill: 'var(--viz-on-fill)', other: true });
    return segs;
  }

  pctText(v: number): string { return formatPlainPct(v); }

  get ariaAll(): string {
    return 'Cơ cấu cổ đông: ' + this.segments.map(s => `${s.name} ${this.pctText(s.pct)}`).join(', ');
  }

  private tipOf(s: Segment, pos: { x: number; y: number }): ChartTip {
    return { ...pos, title: s.name, rows: [{ label: 'tỷ lệ sở hữu', value: this.pctText(s.pct), key: s.color }] };
  }

  hover(s: Segment, event: MouseEvent, host: HTMLElement): void {
    this.tip = this.tipOf(s, tipPosition(event, host));
  }

  focusTip(s: Segment, event: Event, host: HTMLElement): void {
    this.tip = this.tipOf(s, tipPositionFromElement(event.target as Element, host));
  }
}
