import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SignalBaseRate, SignalSetup, SignalSetups } from '../../../core/services/market-data.service';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';

/**
 * Lịch sử setup Pullback trên MỘT mã. Mở đầu bằng khối "đang kích hoạt" (vào lệnh / cắt lỗ / mục tiêu, kèm trạng thái nếu đang mở), rồi một
 * dòng đếm. Tỉ lệ thắng và EV chỉ hiện khi đủ 30 lệnh đã đóng, hiếm khi xảy ra với một mã. Không liệt kê từng lệnh: danh sách kết cục
 * khiến người xem tự đếm ra "tỉ lệ thắng" từ vài lệnh. Dòng "Kết quả quá khứ, không đảm bảo tương lai" luôn hiện.
 */
@Component({
  selector: 'app-signal-setups-panel',
  standalone: true,
  imports: [CommonModule, VndCurrencyPipe],
  template: `
    <section class="border rounded-lg p-5 mb-6" data-testid="signal-setups">
      <h3 class="text-sm font-semibold text-gray-800 mb-3" data-testid="signal-title">Setup Pullback trên {{ symbol }}</h3>

      <p *ngIf="loading" class="text-sm text-gray-500" data-testid="signal-loading">Đang tải lịch sử setup...</p>
      <p *ngIf="error && !loading" class="text-sm text-red-600" data-testid="signal-error">{{ error }}</p>

      <ng-container *ngIf="data && !loading">
        <p *ngIf="data.reasonUnavailable" class="text-sm text-gray-500" data-testid="signal-reason">{{ data.reasonUnavailable }}</p>

        <ng-container *ngIf="!data.reasonUnavailable">
          <div *ngFor="let s of data.setups">
            <!-- Khối "đang kích hoạt" đứng đầu -->
            <div *ngIf="s.activePlan as plan; else noActive" class="bg-blue-50 rounded-lg p-4 mb-3" data-testid="signal-active">
              <div class="text-xs text-blue-700 font-medium mb-2">
                Đang kích hoạt từ {{ plan.signalDate | date:'dd/MM/yyyy' }}
                <span *ngIf="plan.status === 'dang-mo'">— đang mở, ngày thứ {{ plan.dayInTrade }}</span>
                <span *ngIf="plan.status !== 'dang-mo'">— chưa tới phiên vào lệnh</span>
              </div>
              <div class="grid grid-cols-3 gap-3 text-sm">
                <div>
                  <span class="text-gray-500">Vào lệnh<span *ngIf="plan.entryIsEstimate"> (ước tính = giá đóng cửa)</span>:</span>
                  <span class="font-mono font-medium ml-1">{{ plan.entry | vndCurrency }}</span>
                </div>
                <div>
                  <span class="text-gray-500">Cắt lỗ:</span>
                  <span class="font-mono font-medium text-red-600 ml-1">{{ plan.stop | vndCurrency }}</span>
                </div>
                <div>
                  <span class="text-gray-500">Mục tiêu:</span>
                  <span class="font-mono font-medium text-green-600 ml-1">{{ plan.target | vndCurrency }}</span>
                </div>
              </div>
              <p class="mt-2 text-xs text-gray-500" data-testid="signal-active-method">{{ activeMethodNote }}</p>
            </div>
            <ng-template #noActive>
              <p class="text-sm text-gray-500 mb-3" data-testid="signal-inactive">Chưa có tín hiệu trong 3 phiên gần nhất.</p>
            </ng-template>

            <!-- Một dòng đếm; chưa đủ mẫu thì nói thẳng -->
            <p class="text-sm text-gray-700" data-testid="signal-counts">{{ countsLine(s) }}</p>

            <table *ngIf="s.sampleSufficient" class="text-sm mt-2" data-testid="signal-stats">
              <tbody>
                <tr>
                  <td class="pr-4 text-gray-500">Tỉ lệ thắng</td>
                  <td>{{ pct(s.winRate) }} (khoảng 95%: {{ pct(s.winRateLow) }} – {{ pct(s.winRateHigh) }})</td>
                </tr>
                <tr>
                  <td class="pr-4 text-gray-500">EV theo R</td>
                  <td>{{ r(s.evR) }} (khoảng 95%: {{ r(s.evRLow) }} – {{ r(s.evRHigh) }})</td>
                </tr>
              </tbody>
            </table>

            <!-- Tỉ lệ nền VN30: dưới dòng đếm của chính mã, kèm lưu ý. Không bao giờ điền sẵn vào form kế hoạch -->
            <div *ngIf="s.baseRate as b" class="mt-3 pt-3 border-t" data-testid="signal-baserate-block">
              <p class="text-sm text-gray-700" data-testid="signal-baserate">{{ baseRateLine(b) }}</p>
              <ul class="mt-1 text-xs text-gray-500 list-disc pl-4 space-y-0.5" data-testid="signal-baserate-notes">
                <li *ngFor="let n of b.notes">{{ n }}</li>
              </ul>
            </div>
            <p *ngIf="!s.baseRate && s.baseRateReason" class="mt-3 text-xs text-gray-500" data-testid="signal-baserate-reason">Tỉ lệ nền VN30: {{ s.baseRateReason }}</p>
          </div>

          <ul class="mt-3 text-xs text-gray-500 list-disc pl-4 space-y-0.5" data-testid="signal-notes">
            <li *ngFor="let n of data.notes">{{ n }}</li>
          </ul>
        </ng-container>
      </ng-container>

      <p class="mt-3 text-xs italic text-gray-500" data-testid="signal-disclaimer">{{ disclaimer }}</p>
    </section>
  `
})
export class SignalSetupsPanelComponent {
  @Input() symbol = '';
  @Input() data: SignalSetups | null = null;
  @Input() loading = false;
  @Input() error: string | null = null;

  /** Mức của khối "đang kích hoạt" theo luật setup, khác với thẻ "Gợi ý giao dịch" (dựa trên hỗ trợ/kháng cự): nói rõ để không đọc là mâu thuẫn. */
  readonly activeMethodNote =
    'Theo luật setup Pullback: vào giá mở cửa phiên sau, cắt lỗ 2×ATR, mục tiêu 2R. Khác với thẻ Gợi ý giao dịch, vốn dựa trên hỗ trợ/kháng cự.';

  /** Luôn hiện, kể cả khi đang tải hay lỗi. */
  get disclaimer(): string {
    return this.data?.disclaimer || 'Kết quả quá khứ, không đảm bảo tương lai';
  }

  countsLine(s: SignalSetup): string {
    const base = `${s.signals} tín hiệu, ${s.completed} lệnh hoàn tất trong 3 năm.`;
    return s.sampleSufficient ? base : `${base} Chưa đủ mẫu để thống kê riêng mã này (cần ≥ ${s.minSample}).`;
  }

  /** "Tỉ lệ nền VN30 (28 mã, 412 lệnh, 34 tháng): thắng 43% (95% xấp xỉ: 37% – 49%), kỳ vọng +0.12R (95% xấp xỉ: -0.05R – +0.29R)". */
  baseRateLine(b: SignalBaseRate): string {
    return `${b.label} (${b.k} mã, ${b.n} lệnh, ${b.effectiveMonths} tháng): thắng ${this.pct(b.winRate)} (95% xấp xỉ: ${this.pct(b.winRateLow)} – ${this.pct(b.winRateHigh)}), ` +
      `kỳ vọng ${this.r(b.evR)} (95% xấp xỉ: ${this.r(b.evRLow)} – ${this.r(b.evRHigh)})`;
  }

  /** Phân số 0–1 → "50%"; không có thì "n/a" (không phải 0%). */
  pct(v: number | null | undefined): string {
    return v == null ? 'n/a' : `${Math.round(v * 100)}%`;
  }

  /** R có dấu, 2 chữ số: "+0.20R" / "-0.10R"; không có thì "n/a" (không phải 0R). */
  r(v: number | null | undefined): string {
    if (v == null) return 'n/a';
    return (v > 0 ? '+' : '') + v.toFixed(2) + 'R';
  }
}
