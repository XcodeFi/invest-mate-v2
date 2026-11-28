import { Component, Input } from '@angular/core';
import { BusinessPlanTarget } from '../../core/services/market-data.service';
import { ChartFrameComponent } from './chart-frame.component';
import { formatCompactVn, formatPlainPct } from './chart-format';

interface PlanRow { label: string; actual: number | null; planned: number | null; pct: number | null; fill: number | null; over: boolean }

/**
 * Kế hoạch kinh doanh: thanh tiến độ (bullet) cho từng chỉ tiêu, thực hiện so với kế hoạch cả năm, kèm % hoàn thành và kỳ luỹ kế đã đạt.
 * % lấy từ nguồn; thiếu cả % lẫn số thực hiện/kế hoạch thì dòng đó nói "Chưa có dữ liệu" (không vẽ thanh 0%). Vượt kế hoạch thì thanh đầy kèm nhãn.
 */
@Component({
  selector: 'app-plan-bars',
  standalone: true,
  imports: [ChartFrameComponent],
  template: `
    <app-chart-frame title="Kế hoạch kinh doanh" [hasData]="rows.length > 0" testId="plan-bars" [subtitle]="subtitle">
      <div class="pb-list">
        @for (row of rows; track $index) {
          <div class="pb-row" data-testid="pb-row" [attr.data-label]="row.label">
            <div class="pb-head">
              <span class="pb-label">{{ row.label }}</span>
              <span class="pb-value" data-testid="pb-value">
                @if (row.fill != null) {
                  {{ compact(row.actual) }} / {{ compact(row.planned) }} · {{ pctText(row.pct) }}
                  @if (row.over) { <span class="pb-over" data-testid="pb-over">vượt kế hoạch</span> }
                } @else {
                  <span data-testid="pb-row-empty">Chưa có dữ liệu</span>
                }
              </span>
            </div>
            @if (row.fill != null) {
              <div class="pb-track" role="img" [attr.aria-label]="row.label + ': ' + pctText(row.pct) + ' kế hoạch'">
                <div class="pb-fill" data-testid="pb-fill" [style.width.%]="row.fill"></div>
              </div>
            }
          </div>
        }
      </div>
      <table table class="pb-table">
        <thead><tr><th>Chỉ tiêu</th><th>Thực hiện</th><th>Kế hoạch</th><th>% KH</th></tr></thead>
        <tbody>
          @for (row of rows; track $index) {
            <tr data-testid="pb-table-row"><td>{{ row.label }}</td><td>{{ compact(row.actual) }}</td><td>{{ compact(row.planned) }}</td><td>{{ pctText(row.pct) }}</td></tr>
          }
        </tbody>
      </table>
    </app-chart-frame>
  `,
  styles: [`
    :host { display: block; }
    .pb-row { margin-bottom: 0.5rem; }
    .pb-head { display: flex; justify-content: space-between; gap: 0.5rem; font-size: 0.6875rem; color: var(--viz-text); }
    .pb-value { color: var(--viz-text-2); font-variant-numeric: tabular-nums; text-align: right; }
    .pb-over { margin-left: 0.25rem; font-weight: 600; color: var(--viz-text); }
    .pb-track { height: 8px; border-radius: 4px; background: var(--viz-track); margin-top: 2px; overflow: hidden; }
    .pb-fill { height: 100%; border-radius: 4px; background: var(--viz-series-1); }
    .pb-table { width: 100%; font-size: 0.6875rem; color: var(--viz-text); border-collapse: collapse; }
    .pb-table th { text-align: right; font-weight: 400; color: var(--viz-text-2); padding: 2px 4px; }
    .pb-table td { text-align: right; padding: 2px 4px; border-top: 1px solid var(--viz-grid); }
    .pb-table th:first-child, .pb-table td:first-child { text-align: left; }
  `]
})
export class PlanBarsComponent {
  @Input() targets: BusinessPlanTarget[] = [];
  @Input() year: number | null = null;
  /** Kỳ luỹ kế của cột "thực hiện". */
  @Input() quarter: number | null = null;

  get subtitle(): string {
    const parts: string[] = [];
    if (this.year) parts.push(`Năm ${this.year}`);
    if (this.quarter) parts.push(`thực hiện luỹ kế tới Q${this.quarter}`);
    return parts.join(' · ');
  }

  get rows(): PlanRow[] {
    return (this.targets ?? []).map(t => {
      const pct = t.percentComplete ?? null;   // số từ nguồn; thiếu thì để trống, không tự chia thực hiện / kế hoạch
      return {
        label: t.label && t.label.trim() ? t.label : '—',
        actual: t.actual ?? null, planned: t.planned ?? null, pct,
        fill: pct == null ? null : Math.max(0, Math.min(100, pct)),
        over: pct != null && pct > 100
      };
    });
  }

  compact(v: number | null): string { return formatCompactVn(v); }
  pctText(v: number | null): string { return formatPlainPct(v); }
}
