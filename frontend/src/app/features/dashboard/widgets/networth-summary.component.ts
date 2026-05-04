import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NetWorthSummaryDto } from '../../../core/services/personal-finance.service';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';

/**
 * NetWorth widget compact — vị trí #2 trên Home (sau Decision Queue, trước Compound Growth Tracker).
 * Hiển thị 3 dòng: Net Worth + Reality Gap so với mục tiêu CAGR.
 * Compound Growth Tracker đầy đủ vẫn ở giữa page cho user muốn deep-dive.
 */
@Component({
  selector: 'app-networth-summary',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe],
  template: `
    <a *ngIf="summary?.hasProfile"
       data-test="networth-root"
       routerLink="/personal-finance"
       class="block bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6 hover:border-blue-300 transition-colors">
      <div class="flex items-center justify-between mb-1">
        <span class="text-xs font-semibold text-gray-700 flex items-center gap-1">
          <span class="text-base">💎</span> Tổng tài sản (Net Worth)
        </span>
        <span class="text-xs text-blue-600 font-medium">Xem chi tiết →</span>
      </div>
      <div class="flex items-baseline justify-between mb-1">
        <span data-test="networth-value"
              class="text-2xl font-bold"
              [class.text-emerald-700]="summary!.netWorth >= 0"
              [class.text-red-700]="summary!.netWorth < 0">
          {{ summary!.netWorth | vndCurrency }}
        </span>
      </div>
      <div *ngIf="showGap"
           data-test="cagr-gap"
           class="text-xs font-medium text-red-600">
        🔴 Lệch {{ gapPercent.toFixed(1) }}% so với mục tiêu CAGR {{ cagrTarget }}%/năm
      </div>
    </a>
  `,
})
export class NetWorthSummaryComponent {
  @Input() summary: NetWorthSummaryDto | null = null;
  @Input() cagrValue = 0;
  @Input() cagrTarget = 15;

  get showGap(): boolean {
    return this.cagrValue !== 0 && this.cagrValue < this.cagrTarget;
  }

  get gapPercent(): number {
    return Math.abs(this.cagrValue - this.cagrTarget);
  }
}
