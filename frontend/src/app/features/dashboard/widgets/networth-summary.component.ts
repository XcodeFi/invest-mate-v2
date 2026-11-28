import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NetWorthSummaryDto } from '../../../core/services/personal-finance.service';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';

/**
 * Widget "Tài chính cá nhân" — vị trí #2 trên Home (sau Decision Queue). Gộp hai thẻ cũ cùng đọc một `netWorthSummary`:
 * Net Worth + Reality Gap CAGR, "Tổng tài sản − Nợ", cảnh báo nợ tiêu dùng lãi cao, thanh sức khoẻ tài chính và phần chia tài sản
 * (CK · Vàng · TK · DP · NR) thu gọn mặc định. Chưa thiết lập hồ sơ thì hiện thẻ mời thiết lập ở chính vị trí này.
 * Gốc là <div>, KHÔNG phải <a>: chỉ có MỘT link ("Xem chi tiết →" ở đầu thẻ) nên nút "Chi tiết ▾" không bị link cha nuốt.
 */
@Component({
  selector: 'app-networth-summary',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe],
  template: `
    <div *ngIf="summary?.hasProfile"
         data-test="networth-root"
         class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6">
      <div class="flex items-center justify-between mb-1">
        <span class="text-xs font-semibold text-gray-700 flex items-center gap-1">
          <span class="text-base">💰</span> Tài chính cá nhân
        </span>
        <a routerLink="/personal-finance" class="text-xs text-blue-600 font-medium hover:underline">Xem chi tiết →</a>
      </div>
      <div class="flex items-baseline justify-between mb-1">
        <span data-test="networth-label" class="text-xs text-gray-500">💎 Net Worth</span>
        <span data-test="networth-value"
              class="text-2xl font-bold"
              [class.text-emerald-700]="summary!.netWorth >= 0"
              [class.text-red-700]="summary!.netWorth < 0">
          {{ summary!.netWorth | vndCurrency }}
        </span>
      </div>
      <div *ngIf="showGap"
           data-test="cagr-gap"
           class="text-xs font-medium text-red-600 mb-1">
        🔴 Lệch {{ gapPercent.toFixed(1) }}% so với mục tiêu CAGR {{ cagrTarget }}%/năm
      </div>
      <div data-test="assets-debt-line" class="flex items-baseline justify-between text-[11px] text-gray-500 mb-2">
        <span>Tổng tài sản {{ summary!.totalAssets | vndCurrency }}</span>
        <span *ngIf="summary!.totalDebt > 0">− Nợ {{ summary!.totalDebt | vndCurrency }}</span>
      </div>
      <div *ngIf="summary!.hasHighInterestConsumerDebt"
           data-test="high-interest-warning"
           class="bg-red-50 border border-red-200 rounded-lg px-2 py-1.5 text-[11px] text-red-700 mb-2">
        ⚠️ Bạn có nợ tiêu dùng lãi &gt; 20%/năm. Cân nhắc trả nợ trước khi đầu tư thêm.
      </div>
      <div class="w-full bg-gray-200 rounded-full h-2 mb-2 overflow-hidden">
        <div data-test="health-bar" class="h-2 transition-all duration-500"
             [class.bg-emerald-500]="summary!.healthScore >= 80"
             [class.bg-amber-500]="summary!.healthScore >= 50 && summary!.healthScore < 80"
             [class.bg-red-500]="summary!.healthScore < 50"
             [style.width.%]="summary!.healthScore"></div>
      </div>
      <div class="flex items-center justify-between text-[11px] text-gray-600 mb-1">
        <span>Sức khỏe tài chính</span>
        <span data-test="health-score" class="font-semibold"
              [class.text-emerald-600]="summary!.healthScore >= 80"
              [class.text-amber-600]="summary!.healthScore >= 50 && summary!.healthScore < 80"
              [class.text-red-600]="summary!.healthScore < 50">
          {{ summary!.healthScore }}/100
        </span>
      </div>
      <button type="button" data-test="breakdown-toggle" (click)="showBreakdown = !showBreakdown"
              [attr.aria-expanded]="showBreakdown"
              class="text-[11px] text-blue-600 hover:underline">
        Chi tiết {{ showBreakdown ? '▴' : '▾' }}
      </button>
      <div *ngIf="showBreakdown" data-test="breakdown" class="flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-gray-600 mt-1">
        <span>📈 CK: <span class="font-semibold">{{ summary!.securitiesValue | vndCurrency }}</span></span>
        <span *ngIf="summary!.goldTotal > 0">🪙 Vàng: <span class="font-semibold">{{ summary!.goldTotal | vndCurrency }}</span></span>
        <span>🏦 TK: <span class="font-semibold">{{ summary!.savingsTotal | vndCurrency }}</span></span>
        <span>🛡️ DP: <span class="font-semibold">{{ summary!.emergencyTotal | vndCurrency }}</span></span>
        <span>💵 NR: <span class="font-semibold">{{ summary!.idleCashTotal | vndCurrency }}</span></span>
      </div>
    </div>

    <!-- Chưa thiết lập tài chính cá nhân -->
    <a *ngIf="summary && !summary.hasProfile" data-test="networth-onboarding" routerLink="/personal-finance"
       class="block bg-gradient-to-r from-blue-50 to-indigo-50 rounded-xl border border-blue-200 p-4 mb-6 hover:border-blue-400 transition-colors">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <span class="text-2xl">💰</span>
          <div>
            <div class="text-sm font-bold text-gray-900">Thiết lập tài chính cá nhân</div>
            <div class="text-xs text-gray-600">Theo dõi tổng tài sản + quỹ dự phòng + vàng tích trữ</div>
          </div>
        </div>
        <span class="text-xs text-blue-600 font-medium">Bắt đầu →</span>
      </div>
    </a>
  `,
})
export class NetWorthSummaryComponent {
  @Input() summary: NetWorthSummaryDto | null = null;
  @Input() cagrValue = 0;
  @Input() cagrTarget = 15;
  /** Phần chia tài sản thu gọn mặc định; không lưu. */
  showBreakdown = false;

  get showGap(): boolean {
    return this.cagrValue !== 0 && this.cagrValue < this.cagrTarget;
  }

  get gapPercent(): number {
    return Math.abs(this.cagrValue - this.cagrTarget);
  }
}
