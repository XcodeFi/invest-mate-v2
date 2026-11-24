import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EstimateDto } from '../../../core/services/trade-plan.service';

/**
 * Khi review: ước lượng trước khi vào lệnh (bản đã khoá lúc khớp đầu tiên) đặt cạnh R thực tế, kèm lý do vượt cổng kỳ vọng
 * và dấu "vào lệnh không qua cổng". Chỉ lệnh Mua. Thiếu dữ liệu hiện n/a (không phải 0R).
 */
@Component({
  selector: 'app-estimate-vs-actual',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="direction === 'Buy'" class="bg-white rounded-lg p-3 text-sm space-y-1" data-testid="estimate-vs-actual">
      <div class="font-medium text-gray-700">Ước lượng so với thực tế</div>

      <div *ngIf="estimate; else noEstimate">
        <span class="text-gray-500">Ước lượng (khoá lúc khớp lệnh đầu tiên):</span>
        <span class="font-medium ml-1">EV {{ formatR(estimate.evR) }} · thắng {{ formatPct(estimate.impliedWinProbability) }}</span>
        <span *ngIf="!estimate.feesIncluded" class="text-xs text-gray-500 ml-1">(chưa trừ phí)</span>
        <span class="text-gray-400 mx-1">·</span>
        <span class="font-semibold" [class.text-green-600]="(realizedR ?? 0) > 0" [class.text-red-600]="(realizedR ?? 0) < 0">Thực tế: {{ formatR(realizedR) }}</span>
      </div>
      <ng-template #noEstimate>
        <div>
          <span class="text-gray-500">Kế hoạch này chưa có ước lượng kỳ vọng.</span>
          <span class="font-semibold ml-1">Thực tế: {{ formatR(realizedR) }}</span>
        </div>
      </ng-template>

      <div *ngIf="evGateReason" class="text-gray-700" data-testid="ev-gate-reason-shown">
        <span class="text-gray-500">Lý do vượt cổng kỳ vọng:</span> {{ evGateReason }}
      </div>
      <div *ngIf="evGateSkipped" class="text-amber-700" data-testid="ev-gate-skipped">Vào lệnh không qua cổng kỳ vọng</div>
    </div>
  `
})
export class EstimateVsActualComponent {
  @Input() direction = 'Buy';
  @Input() estimate: EstimateDto | null | undefined = null;
  /** R thực tế (PnL ròng / 1R); null = không biết rủi ro ban đầu. */
  @Input() realizedR: number | null | undefined = null;
  @Input() evGateReason: string | null | undefined = null;
  @Input() evGateSkipped: boolean | undefined = false;

  formatR(r: number | null | undefined): string {
    if (r == null) return 'n/a';
    return `${r > 0 ? '+' : ''}${r.toFixed(2)}R`;
  }

  formatPct(p: number | null | undefined): string {
    return p == null ? 'n/a' : `${Math.round(p)}%`;
  }
}
