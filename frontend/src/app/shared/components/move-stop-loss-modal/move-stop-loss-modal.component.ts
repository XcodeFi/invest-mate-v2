import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NumMaskDirective } from '../../directives/num-mask.directive';

/**
 * Dời stop-loss của một kế hoạch đang giữ vị thế. Dùng chung cho danh sách kế hoạch,
 * Decision Queue và trang Quản lý rủi ro — gate lý do nằm ở đây nên cả ba mặt cùng một luật.
 *
 * Nới SL được phép nhưng bắt buộc lý do: cơ chế răn đe là điểm kỷ luật (đếm từ
 * StopLossHistory), không phải cái khoá. Xem ADR-0017.
 */
@Component({
  selector: 'app-move-stop-loss-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, NumMaskDirective],
  template: `
    <div *ngIf="open"
      class="fixed inset-0 z-[60] bg-black/50 flex items-center justify-center p-4"
      (click)="cancel.emit()">
      <div class="bg-white rounded-xl shadow-2xl max-w-md w-full p-5 space-y-3" (click)="$event.stopPropagation()">
        <h3 class="text-lg font-bold text-gray-800 flex items-center gap-2">
          <span>🛡</span> Dời stop-loss {{ symbol }}
        </h3>

        <p class="text-sm text-gray-600">
          SL hiện tại: <strong>{{ currentStopLoss | number:'1.0-0' }}</strong>
          <span class="text-gray-400"> · {{ direction === 'Buy' ? 'Vị thế mua' : 'Vị thế bán' }}</span>
        </p>

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">SL mới</label>
          <input [(ngModel)]="newStopLoss" type="text" inputmode="numeric" appNumMask [emptyWhenZero]="true"
            class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-amber-500"
            placeholder="Giá cắt lỗ mới">
        </div>

        <div *ngIf="isWidening" class="p-2.5 bg-amber-50 border border-amber-300 rounded-lg text-xs text-amber-800">
          Đây là <strong>nới SL</strong> — chấp nhận lỗ xa hơn. Lần nới này được đếm vào
          <strong>điểm kỷ luật</strong>. Ghi rõ lý do để lần review chiến dịch còn đọc lại được.
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-700 mb-1">
            Lý do
            <span *ngIf="isWidening" class="text-red-500">*</span>
            <span *ngIf="!isWidening" class="text-gray-400 font-normal">(tuỳ chọn)</span>
          </label>
          <textarea [(ngModel)]="reason" rows="2"
            class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-amber-500"
            [placeholder]="isWidening ? 'Vì sao chấp nhận lỗ xa hơn?' : 'VD: pyramid xong, dời SL cả cụm lên'"></textarea>
        </div>

        <p *ngIf="error" class="text-sm text-red-600">{{ error }}</p>

        <div class="flex items-center justify-end gap-2 pt-2 border-t border-gray-200">
          <button type="button" (click)="cancel.emit()"
            class="px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-100 rounded-lg">Hủy</button>
          <button type="button" (click)="submit()" [disabled]="submitting"
            class="px-4 py-1.5 text-sm bg-amber-600 hover:bg-amber-700 disabled:opacity-50 text-white rounded-lg font-medium">
            {{ submitting ? 'Đang lưu...' : 'Dời SL' }}
          </button>
        </div>
      </div>
    </div>
  `
})
export class MoveStopLossModalComponent implements OnChanges {
  @Input() open = false;
  @Input() symbol = '';
  @Input() currentStopLoss = 0;
  @Input() direction: 'Buy' | 'Sell' = 'Buy';
  @Input() submitting = false;

  @Output() confirm = new EventEmitter<{ newStopLoss: number; reason?: string }>();
  @Output() cancel = new EventEmitter<void>();

  newStopLoss = 0;
  reason = '';
  error: string | null = null;

  private wasOpen = false;

  ngOnChanges(): void {
    if (this.open && !this.wasOpen) {
      this.newStopLoss = this.currentStopLoss;
      this.reason = '';
      this.error = null;
    }
    this.wasOpen = this.open;
  }

  /** Nới = SL đi xa giá vào hơn. Buy: xuống. Sell: lên. */
  get isWidening(): boolean {
    if (!this.newStopLoss || !this.currentStopLoss) return false;
    return this.direction === 'Buy'
      ? this.newStopLoss < this.currentStopLoss
      : this.newStopLoss > this.currentStopLoss;
  }

  submit(): void {
    this.error = null;

    if (!this.newStopLoss || this.newStopLoss <= 0) {
      this.error = 'Nhập giá SL mới lớn hơn 0.';
      return;
    }
    if (this.newStopLoss === this.currentStopLoss) {
      this.error = 'SL mới trùng SL hiện tại.';
      return;
    }
    if (this.isWidening && !this.reason.trim()) {
      this.error = 'Nới SL bắt buộc ghi lý do.';
      return;
    }

    this.confirm.emit({
      newStopLoss: this.newStopLoss,
      reason: this.reason.trim() || undefined
    });
  }
}
