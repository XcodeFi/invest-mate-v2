import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';

/** Một dòng số liệu review để so trước/sau. */
export interface ReviewNumbers {
  pnLAmount: number;
  pnLPercent: number;
  realizedR?: number | null;
}

/**
 * Hộp xác nhận "Tính lại theo công thức mới" cho kế hoạch ĐÃ review: hiện số đang lưu và số sẽ có sau khi tính lại.
 * Bài học và ghi chú của người dùng được giữ; số cũ được lưu lại một lần. Nút chính nằm bên phải; lớp phủ z-[60] (header là z-50).
 */
@Component({
  selector: 'app-review-recompute-dialog',
  standalone: true,
  imports: [CommonModule, VndCurrencyPipe],
  template: `
    <div class="fixed inset-0 z-[60] bg-black/50 flex items-center justify-center p-4" data-testid="recompute-dialog">
      <div class="bg-white rounded-lg shadow-xl w-full max-w-lg p-6 space-y-4">
        <h3 class="text-lg font-semibold text-gray-800">Tính lại review theo công thức mới</h3>
        <p class="text-sm text-gray-600">
          Bài học và ghi chú của bạn được giữ nguyên. Số liệu hiện tại được lưu lại một lần để đối chiếu.
        </p>

        <table class="w-full text-sm">
          <thead>
            <tr class="text-xs text-gray-500">
              <th class="text-left font-normal"></th>
              <th class="text-right font-normal">Đang lưu</th>
              <th class="text-right font-normal">Sau khi tính lại</th>
            </tr>
          </thead>
          <tbody>
            <tr data-testid="recompute-pnl">
              <td class="py-1 text-gray-500">P&amp;L</td>
              <td class="py-1 text-right">{{ before.pnLAmount | vndCurrency }}</td>
              <td class="py-1 text-right font-medium">{{ after.pnLAmount | vndCurrency }}</td>
            </tr>
            <tr data-testid="recompute-pct">
              <td class="py-1 text-gray-500">% Lãi/Lỗ</td>
              <td class="py-1 text-right">{{ before.pnLPercent | number:'1.2-2' }}%</td>
              <td class="py-1 text-right font-medium">{{ after.pnLPercent | number:'1.2-2' }}%</td>
            </tr>
            <tr data-testid="recompute-r">
              <td class="py-1 text-gray-500">R thực tế</td>
              <td class="py-1 text-right">{{ formatR(before.realizedR) }}</td>
              <td class="py-1 text-right font-medium">{{ formatR(after.realizedR) }}</td>
            </tr>
          </tbody>
        </table>

        <div class="flex justify-end gap-2 pt-2">
          <button type="button" (click)="cancel.emit()" [disabled]="busy" data-testid="recompute-cancel"
            class="px-4 py-2 border border-gray-300 hover:bg-gray-50 disabled:opacity-50 text-gray-700 rounded-lg text-sm font-medium">Hủy</button>
          <button type="button" (click)="confirm.emit()" [disabled]="busy" data-testid="recompute-confirm"
            class="px-4 py-2 bg-amber-600 hover:bg-amber-700 disabled:bg-gray-300 text-white rounded-lg text-sm font-medium">
            {{ busy ? 'Đang xử lý...' : 'Tính lại' }}
          </button>
        </div>
      </div>
    </div>
  `
})
export class ReviewRecomputeDialogComponent {
  @Input() before!: ReviewNumbers;
  @Input() after!: ReviewNumbers;
  @Input() busy = false;
  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  /** null → "n/a" (không biết, không phải 0R). */
  formatR(r: number | null | undefined): string {
    if (r === null || r === undefined) return 'n/a';
    return (r > 0 ? '+' : '') + r.toFixed(2) + 'R';
  }
}
