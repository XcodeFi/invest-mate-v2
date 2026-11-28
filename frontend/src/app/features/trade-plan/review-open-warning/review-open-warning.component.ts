import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Review khi còn cổ phiếu chưa bán: P&L và R chỉ tính phần đã chốt (giá vốn bình quân × số đã bán), phần còn giữ
 * không bị coi là lỗ. Chiến dịch loại khỏi thống kê R cho tới khi bán hết. Review cũ (null) và đã đóng hết (0) không hiện gì.
 * Cổ tức tiền mặt theo quyền hưởng đã nằm trong P&L; hiện riêng khi có để người dùng đối chiếu.
 */
@Component({
  selector: 'app-review-open-warning',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="hasOpen" class="rounded-lg border border-amber-300 bg-amber-100 p-3 text-sm text-amber-800"
         data-testid="review-open-warning">
      Còn {{ openQuantity | number:'1.0-0' }} CP chưa bán — kết quả chỉ tính phần đã chốt.
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

  get hasOpen(): boolean {
    return (this.openQuantity ?? 0) > 0;
  }
}
