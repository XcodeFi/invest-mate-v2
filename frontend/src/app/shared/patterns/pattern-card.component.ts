import { Component, EventEmitter, Input, Output } from '@angular/core';
import { PatternCandidate, PatternCriterion } from '../../core/services/market-data.service';

const fmt = (v: number, digits = 1): string =>
  v.toLocaleString('vi-VN', { minimumFractionDigits: digits, maximumFractionDigits: digits });
const price = (v: number): string => v.toLocaleString('vi-VN', { maximumFractionDigits: 2 });

/**
 * Thẻ "Ứng viên mô hình: Cốc tay cầm": số đo, từng điều kiện đạt/không, trạng thái, phần "nếu sai" (cắt lỗ, mục tiêu đo, lãi/rủi ro theo R)
 * và lời nhắc. Chỉ HIỂN THỊ số server tính, không điểm số, không xác suất. Mọi chuỗi (kể cả từ server) hiển thị dạng văn bản.
 */
@Component({
  selector: 'app-pattern-card',
  standalone: true,
  host: { class: 'block' },
  template: `
    @if (candidate; as c) {
      <section class="mb-4 rounded-lg border border-sky-200 bg-sky-50/40 p-4 text-sm text-gray-700" data-testid="pattern-card" [attr.data-state]="c.state">
        <div class="flex flex-wrap items-baseline justify-between gap-2 mb-2">
          <h3 class="font-bold text-gray-800">Ứng viên mô hình: {{ c.typeVi }}</h3>
          <span class="text-xs font-semibold rounded-full px-2 py-0.5 bg-white border border-sky-200 text-sky-800" data-testid="pattern-state">{{ c.stateVi }}</span>
        </div>

        <dl class="grid grid-cols-2 gap-x-4 gap-y-1 mb-3" data-testid="pattern-measures">
          <dt class="text-gray-500">Cốc</dt>
          <dd data-testid="pattern-cup">{{ weeks(c) }} tuần, sâu {{ pct(c.cupDepthPct) }}%</dd>
          <dt class="text-gray-500">Tay cầm</dt>
          <dd data-testid="pattern-handle">{{ c.handleLengthSessions }} phiên, sâu {{ pct(c.handleDepthPct) }}%</dd>
          <dt class="text-gray-500">Pivot</dt>
          <dd data-testid="pattern-pivot">{{ price(c.pivotPrice) }} ({{ distanceText(c) }})</dd>
          <dt class="text-gray-500">Khối lượng nến cuối</dt>
          <dd data-testid="pattern-volume">{{ c.volumeVsAvg50 != null ? pct(c.volumeVsAvg50, 2) + '× trung bình 50 phiên' : '—' }}</dd>
        </dl>

        <div class="rounded-lg bg-white border border-amber-200 px-3 py-2 mb-3" data-testid="pattern-downside">
          <p class="font-semibold text-red-700" data-testid="pattern-risk">Nếu sai: −{{ pct(c.riskPct) }}% từ pivot tới điểm cắt lỗ</p>
          <p class="text-xs text-gray-600">Cắt lỗ {{ price(c.stopPrice) }}, ngay dưới đáy tay cầm.</p>
          <p class="mt-1" data-testid="pattern-target">Mục tiêu đo {{ price(c.targetPrice) }} (+{{ pct(c.rewardPct) }}% từ pivot) · lãi/rủi ro <strong>{{ pct(c.rewardRisk) }}R</strong></p>
          <p class="text-xs text-gray-600">Mục tiêu = pivot cộng độ sâu của cốc: quy ước hình học, không phải dự báo.</p>
        </div>

        <ul class="space-y-1 mb-3" data-testid="pattern-criteria">
          @for (k of c.criteria; track k.key) {
            <li data-testid="pattern-criterion" [attr.data-key]="k.key" [attr.data-passed]="k.passed" class="flex gap-2">
              <span [class]="k.passed ? 'text-green-700' : 'text-red-700'" [attr.aria-label]="k.passed ? 'Đạt' : 'Không đạt'">{{ k.passed ? '✓' : '✗' }}</span>
              <span>{{ k.rule }}@if (k.value != null) { <span class="text-gray-500"> · {{ valueText(k) }}</span> }@if (!k.required) { <span class="text-gray-500"> (tham khảo, không bắt buộc)</span> }</span>
            </li>
          }
        </ul>

        <label class="flex items-center gap-2 text-xs mb-2">
          <input type="checkbox" data-testid="pattern-show" [checked]="showOnChart" (change)="toggle($event)">
          Vẽ cốc, pivot, cắt lỗ và mục tiêu trên biểu đồ
        </label>
        @if (showOnChart && !chartReady) {
          <p class="text-xs text-gray-500 mb-2" data-testid="pattern-chart-hint">Chọn một nhóm chỉ báo để tải biểu đồ rồi mô hình sẽ hiện trên đó.</p>
        }

        <p class="text-xs text-gray-600" data-testid="pattern-disclaimer">{{ c.disclaimer }}</p>
      </section>
    }
  `
})
export class PatternCardComponent {
  @Input() candidate: PatternCandidate | null | undefined = null;
  @Input() showOnChart = true;
  /** Biểu đồ đã có chuỗi để vẽ chưa. */
  @Input() chartReady = true;
  @Output() showOnChartChange = new EventEmitter<boolean>();

  pct = (v: number, digits = 1): string => fmt(v, digits);
  price = price;

  weeks(c: PatternCandidate): string { return fmt(c.cupLengthWeeks, 1); }

  distanceText(c: PatternCandidate): string {
    return c.distanceToPivotPct > 0 ? `còn ${fmt(c.distanceToPivotPct)}% tới pivot` : `đã trên pivot ${fmt(-c.distanceToPivotPct)}%`;
  }

  valueText(k: PatternCriterion): string {
    const v = k.value as number;
    return k.unit === 'giá' ? price(v) : `${fmt(v, 1)}${k.unit === '%' ? '%' : ' ' + k.unit}`;
  }

  toggle(event: Event): void {
    this.showOnChartChange.emit((event.target as HTMLInputElement).checked);
  }
}
