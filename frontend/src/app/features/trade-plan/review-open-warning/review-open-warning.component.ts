import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Cảnh báo trên review:
 * - còn cổ phiếu chưa bán: P&L và R chỉ tính phần đã chốt, chiến dịch bị loại khỏi thống kê R;
 * - có cổ phiếu bán nhưng chưa gắn lệnh mua (bán vượt): P&L chỉ phần khớp, R = n/a, loại khỏi thống kê;
 * - kế hoạch chỉ có lệnh mua mà lệnh bán nằm ở kế hoạch khác: số "còn mở" chỉ theo lệnh mua đã gắn.
 * Review cũ (null) và đã đóng hết (0) không hiện gì. Cổ tức tiền mặt theo quyền hưởng đã nằm trong P&L; hiện riêng khi có.
 */
@Component({
  selector: 'app-review-open-warning',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="hasOversold" class="rounded-lg border border-red-300 bg-red-50 p-3 text-sm text-red-800 mb-2"
         data-testid="review-oversold-warning">
      Có {{ oversoldQuantity | number:'1.0-0' }} CP bán nhưng chưa gắn lệnh mua — hãy gắn lệnh mua vào kế hoạch rồi bấm Tính lại.
      P&amp;L chỉ tính phần khớp được, R là n/a và chiến dịch này không được tính vào thống kê.
    </div>
    <div *ngIf="hasOpen" class="rounded-lg border border-amber-300 bg-amber-100 p-3 text-sm text-amber-800"
         data-testid="review-open-warning">
      Còn {{ openQuantity | number:'1.0-0' }} CP chưa bán<span *ngIf="sellsInOtherPlan" data-testid="review-other-plan-note"> (theo lệnh mua đã gắn — lệnh bán có thể ở kế hoạch khác)</span>
      — kết quả chỉ tính phần đã chốt.
      Chiến dịch này không được tính vào kỳ vọng R, độ khớp ước lượng và Kelly.
    </div>
    <div *ngIf="(dividendIncome ?? 0) > 0" class="text-xs text-gray-600 mt-1" data-testid="review-dividend-income">
      Đã gồm cổ tức tiền mặt: {{ dividendIncome | number:'1.0-0' }} đ
    </div>
  `
})
export class ReviewOpenWarningComponent {
  @Input() openQuantity: number | null | undefined = null;
  @Input() dividendIncome: number | null | undefined = null;
  @Input() oversoldQuantity: number | null | undefined = null;
  /** Danh sách cờ của review: "thieu-lenh-mua-lien-ket" | "lenh-ban-o-ke-hoach-khac". */
  @Input() flags: string[] | null | undefined = null;

  get hasOpen(): boolean {
    return (this.openQuantity ?? 0) > 0;
  }

  get hasOversold(): boolean {
    return (this.oversoldQuantity ?? 0) > 0;
  }

  get sellsInOtherPlan(): boolean {
    return (this.flags ?? []).includes('lenh-ban-o-ke-hoach-khac');
  }
}
