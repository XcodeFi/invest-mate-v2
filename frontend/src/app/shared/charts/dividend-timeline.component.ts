import { Component, Input } from '@angular/core';
import { DividendEventRow } from '../../core/services/market-data.service';
import { ChartFrameComponent } from './chart-frame.component';

interface YearGroup { year: string; events: DividendEventRow[] }

/**
 * Cổ tức theo năm: nguồn KHÔNG cung cấp giá trị cổ tức dạng số (chỉ có câu tiêu đề tự do), nên KHÔNG vẽ cột và KHÔNG bóc số từ tiêu đề (mong manh, dễ bịa số).
 * Thay vào đó là dòng thời gian theo năm: mỗi sự kiện một chấm, kèm tiêu đề nguyên văn của nguồn và câu nói rõ số liệu chưa có. Nếu về sau nguồn có số,
 * thêm cột ở đây.
 */
@Component({
  selector: 'app-dividend-timeline',
  standalone: true,
  imports: [ChartFrameComponent],
  template: `
    <app-chart-frame title="Cổ tức theo năm" [hasData]="groups.length > 0" testId="dividend-timeline">
      <p class="dt-note" data-testid="dt-note">Nguồn chưa cung cấp giá trị cổ tức dạng số</p>
      @for (g of groups; track g.year) {
        <div class="dt-year" data-testid="dt-year" [attr.data-year]="g.year">
          <span class="dt-label">{{ g.year }}</span>
          <span class="dt-dots">
            @for (e of g.events; track $index) { <i class="dt-dot" data-testid="dt-dot" [attr.title]="text(e.description)"></i> }
          </span>
          <span class="dt-count">{{ g.events.length }} sự kiện</span>
        </div>
        <ul class="dt-list">
          @for (e of g.events; track $index) {
            <li>{{ text(e.exDate) }} — {{ text(e.description) }}</li>
          }
        </ul>
      }
      <table table class="dt-table">
        <thead><tr><th>Ngày GDKHQ</th><th>Ngày thanh toán</th><th>Nội dung</th></tr></thead>
        <tbody>
          @for (e of events; track $index) {
            <tr data-testid="dt-row"><td>{{ text(e.exDate) }}</td><td>{{ text(e.payDate) }}</td><td>{{ text(e.description) }}</td></tr>
          }
        </tbody>
      </table>
    </app-chart-frame>
  `,
  styles: [`
    :host { display: block; }
    .dt-note { font-size: 0.6875rem; font-style: italic; color: var(--viz-text-2); margin-bottom: 0.25rem; }
    .dt-year { display: flex; align-items: center; gap: 0.5rem; font-size: 0.6875rem; color: var(--viz-text); margin-top: 0.25rem; }
    .dt-label { width: 2.5rem; font-weight: 600; }
    .dt-dots { display: inline-flex; gap: 4px; }
    .dt-dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; background: var(--viz-series-1); box-shadow: 0 0 0 2px var(--viz-surface); }
    .dt-count { color: var(--viz-text-2); }
    .dt-list { font-size: 0.6875rem; color: var(--viz-text-2); padding-left: 3rem; }
    .dt-table { width: 100%; font-size: 0.6875rem; color: var(--viz-text); border-collapse: collapse; }
    .dt-table th { text-align: left; font-weight: 400; color: var(--viz-text-2); padding: 2px 4px; }
    .dt-table td { padding: 2px 4px; border-top: 1px solid var(--viz-grid); }
  `]
})
export class DividendTimelineComponent {
  @Input() events: DividendEventRow[] = [];

  /** Gom theo năm của ngày GDKHQ (dd/MM/yyyy), năm mới nhất trước; không đọc được ngày thì vào nhóm "Không rõ năm". */
  get groups(): YearGroup[] {
    const map = new Map<string, DividendEventRow[]>();
    for (const e of this.events ?? []) {
      const m = /(\d{4})\s*$/.exec(e.exDate ?? '');
      const year = m ? m[1] : 'Không rõ năm';
      map.set(year, [...(map.get(year) ?? []), e]);
    }
    return [...map.entries()]
      .sort(([a], [b]) => (a === 'Không rõ năm' ? 1 : b === 'Không rõ năm' ? -1 : Number(b) - Number(a)))
      .map(([year, events]) => ({ year, events }));
  }

  text(v: string | null | undefined): string { return v && v.trim() ? v : '—'; }
}
