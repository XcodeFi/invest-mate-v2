import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CalibrationResultDto, TradePlanService } from '../../../core/services/trade-plan.service';

/** Tối thiểu số chiến dịch (thắng + thua) để hiện Brier và câu tóm tắt; khớp ngưỡng phía server. */
const MIN_TOTAL_FOR_SUMMARY = 15;

/**
 * Khối "Độ khớp ước lượng": so % thắng đã ước lượng (ảnh chụp lúc vào lệnh) với kết quả thật của các chiến dịch đã review,
 * chia 4 mức. Mức n &lt; 5 làm mờ ("chưa đủ mẫu"); Brier và câu "lạc quan/bi quan" chỉ có khi đủ 15 mẫu (server trả null dưới ngưỡng).
 * Không có kế hoạch nào có ước lượng đã khoá thì hiện trạng thái rỗng kèm cách có dữ liệu, không phải lỗi.
 */
@Component({
  selector: 'app-calibration-block',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="border rounded-lg p-4 bg-white" data-testid="calibration-block">
      <h3 class="text-base font-semibold text-gray-900">Độ khớp ước lượng</h3>
      <p class="text-xs text-gray-500 mb-3">
        So % thắng bạn ước lượng khi vào lệnh với tỉ lệ thắng thật của các chiến dịch đã review. Chỉ tính kế hoạch Mua có ước lượng
        đã khoá khi khớp lệnh; chiến dịch hoà (|R| ≤ 0,1) không tính.
      </p>

      <div *ngIf="loading" class="text-sm text-gray-500 py-4 text-center">Đang tải...</div>
      <div *ngIf="error" role="alert" class="text-sm text-red-600 bg-red-50 rounded-lg px-3 py-2" data-testid="calibration-error">{{ error }}</div>

      <ng-container *ngIf="!loading && !error && data">
        <div *ngIf="data.n === 0" class="text-sm text-gray-500 py-2" data-testid="calibration-empty">
          Chưa có dữ liệu để so. Cần các kế hoạch có ước lượng xác suất (nhập kịch bản có tổng xác suất 100%) và đã review.
        </div>

        <div *ngIf="data.n > 0">
          <div class="overflow-x-auto">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="text-left text-xs text-gray-500 border-b">
                  <th class="py-1 pr-3">Mức ước lượng</th>
                  <th class="py-1 pr-3 text-right">Ước lượng TB</th>
                  <th class="py-1 pr-3 text-right">Thắng thật</th>
                  <th class="py-1 text-right">Mẫu</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let b of data.buckets" data-testid="calibration-row" class="border-b last:border-0"
                  [class.opacity-50]="!b.sufficient">
                  <td class="py-1.5 pr-3">{{ b.label }}</td>
                  <td class="py-1.5 pr-3 text-right">{{ pct(b.avgEstimated) }}</td>
                  <td class="py-1.5 pr-3 text-right">{{ pct(b.actualWinRate) }}</td>
                  <td class="py-1.5 text-right">
                    n = {{ b.n }}
                    <div *ngIf="!b.sufficient" class="text-xs text-amber-600">{{ b.n === 0 ? 'chưa có mẫu' : 'chưa đủ mẫu' }}</div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div *ngIf="data.sufficient && data.brier != null" class="mt-3 text-sm" data-testid="calibration-brier">
            Brier score: <span class="font-semibold">{{ decimal(data.brier, 3) }}</span>
            <span class="text-xs text-gray-500"> (0 = đoán hoàn hảo; 0,25 = tương đương đoán 50/50; càng thấp càng tốt)</span>
          </div>
          <div *ngIf="data.sufficient && data.optimismPoints != null" class="mt-1 text-sm font-medium" data-testid="calibration-summary">
            {{ summary(data.optimismPoints) }}
          </div>
          <div *ngIf="!data.sufficient" class="mt-3 text-xs text-gray-500" data-testid="calibration-need-more">
            Cần tối thiểu {{ minTotal }} chiến dịch thắng/thua để hiện Brier score và nhận xét lạc quan/bi quan (hiện có {{ data.n }}).
          </div>
        </div>
      </ng-container>
    </div>
  `
})
export class CalibrationBlockComponent implements OnInit {
  /** Tuỳ chọn: chỉ tính một chiến lược. */
  @Input() strategyId?: string;

  readonly minTotal = MIN_TOTAL_FOR_SUMMARY;
  data: CalibrationResultDto | null = null;
  loading = true;
  error: string | null = null;

  constructor(private planService: TradePlanService) {}

  ngOnInit(): void {
    this.planService.getCalibration(this.strategyId).subscribe({
      next: result => { this.data = result; this.loading = false; },
      error: () => { this.error = 'Không tải được độ khớp ước lượng'; this.loading = false; }
    });
  }

  pct(p: number | null | undefined): string {
    return p == null ? 'n/a' : `${Math.round(p * 100)}%`;
  }

  decimal(v: number, digits: number): string {
    return v.toFixed(digits).replace('.', ',');
  }

  summary(points: number): string {
    const abs = Math.abs(points);
    if (abs < 1) return 'Ước lượng của bạn khá khớp với kết quả thật (lệch dưới 1 điểm %).';
    const who = points > 0 ? 'lạc quan' : 'bi quan';
    return `Bạn thường ${who} khoảng ${Math.round(abs)} điểm % so với kết quả thật.`;
  }
}
