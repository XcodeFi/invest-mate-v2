import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExpectancyStatsDto, StrategyExpectancyDto, TradePlanService } from '../../../core/services/trade-plan.service';

/** Dưới ngưỡng này, % thắng dao động quá mạnh để tin (khoảng Wilson rất rộng). */
const MIN_RELIABLE_SAMPLE = 10;

/**
 * Khối "Kỳ vọng theo R": thống kê các chiến dịch ĐÃ ĐÓNG (kế hoạch Mua đã review), mỗi chiến lược một dòng, kèm số mẫu n
 * và khoảng tin cậy 95% của % thắng. Dữ liệu thiếu hiện n/a (không phải 0); mẫu nhỏ ghi rõ "chưa đủ mẫu để tin".
 * Không thay các chỉ số tiền cũ — đó là con số theo lệnh, gồm cả lãi/lỗ chưa chốt.
 */
@Component({
  selector: 'app-expectancy-r-block',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="border rounded-lg p-4 bg-white" data-testid="expectancy-r-block">
      <h3 class="text-base font-semibold text-gray-900">Kỳ vọng theo R (chiến dịch đã đóng)</h3>
      <p class="text-xs text-gray-500 mb-3">
        Chỉ tính kế hoạch lệnh Mua đã review. 1R = rủi ro ban đầu (|giá vào − cắt lỗ| × số lượng lúc chốt kế hoạch);
        thắng khi R &gt; +0,1, thua khi R &lt; −0,1, còn lại là hoà. Kỳ vọng R là trung bình R của mọi chiến dịch, kể cả hoà.
      </p>

      <div *ngIf="loading" class="text-sm text-gray-500 py-4 text-center">Đang tải...</div>
      <div *ngIf="error" role="alert" class="text-sm text-red-600 bg-red-50 rounded-lg px-3 py-2" data-testid="expectancy-error">{{ error }}</div>

      <ng-container *ngIf="!loading && !error && data">
        <div *ngIf="data.total.n === 0" class="text-sm text-gray-500 py-2" data-testid="expectancy-empty">
          Chưa có chiến dịch đã đóng nào tính được R. Kế hoạch cần được chốt (Sẵn sàng) trước khi vào lệnh và review sau khi đóng.
        </div>

        <div *ngIf="data.total.n > 0" class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="text-left text-xs text-gray-500 border-b">
                <th class="py-1 pr-3">Chiến lược</th>
                <th class="py-1 pr-3 text-right">Mẫu</th>
                <th class="py-1 pr-3 text-right">% thắng (95%)</th>
                <th class="py-1 pr-3 text-right">Thắng / thua / hoà</th>
                <th class="py-1 pr-3 text-right">R thắng TB</th>
                <th class="py-1 pr-3 text-right">R thua TB</th>
                <th class="py-1 pr-3 text-right">Kỳ vọng R</th>
                <th class="py-1 text-right">PF (R)</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let s of rowsOf(data)" data-testid="expectancy-row" class="border-b last:border-0" [class.font-semibold]="s.strategyName === 'Tất cả'">
                <td class="py-1.5 pr-3">{{ s.strategyName }}</td>
                <td class="py-1.5 pr-3 text-right">
                  n = {{ s.n }}
                  <div *ngIf="s.n < minSample" class="text-xs text-amber-600">chưa đủ mẫu để tin</div>
                </td>
                <td class="py-1.5 pr-3 text-right">
                  {{ formatPct(s.winRate) }}
                  <div *ngIf="s.winRateLow95 != null && s.winRateHigh95 != null" class="text-xs text-gray-500">
                    {{ formatPct(s.winRateLow95) }} – {{ formatPct(s.winRateHigh95) }}
                  </div>
                </td>
                <td class="py-1.5 pr-3 text-right">{{ s.wins }} thắng · {{ s.losses }} thua · {{ s.draws }} hoà</td>
                <td class="py-1.5 pr-3 text-right">{{ formatR(s.avgWinR) }}</td>
                <td class="py-1.5 pr-3 text-right">{{ formatR(s.avgLossR) }}</td>
                <td class="py-1.5 pr-3 text-right font-medium"
                  [class.text-green-600]="(s.expectancyR ?? 0) > 0" [class.text-red-600]="(s.expectancyR ?? 0) < 0">
                  {{ formatR(s.expectancyR) }}
                </td>
                <td class="py-1.5 text-right">{{ s.profitFactorR == null ? 'n/a' : (s.profitFactorR | number:'1.2-2') }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </ng-container>
    </div>
  `
})
export class ExpectancyRBlockComponent implements OnInit {
  readonly minSample = MIN_RELIABLE_SAMPLE;
  data: StrategyExpectancyDto | null = null;
  loading = true;
  error: string | null = null;

  constructor(private planService: TradePlanService) {}

  ngOnInit(): void {
    this.planService.getExpectancy().subscribe({
      next: result => { this.data = result; this.loading = false; },
      error: () => { this.error = 'Không tải được thống kê kỳ vọng'; this.loading = false; }
    });
  }

  /** Các chiến lược, rồi dòng tổng cuối cùng (chỉ khi có từ 2 nhóm trở lên, nếu không dòng tổng trùng nhóm duy nhất). */
  rowsOf(d: StrategyExpectancyDto): ExpectancyStatsDto[] {
    return d.strategies.length > 1 ? [...d.strategies, d.total] : d.strategies;
  }

  formatR(r: number | null | undefined): string {
    if (r == null) return 'n/a';
    return `${r > 0 ? '+' : ''}${r.toFixed(2)}R`;
  }

  formatPct(p: number | null | undefined): string {
    return p == null ? 'n/a' : `${Math.round(p * 100)}%`;
  }
}
