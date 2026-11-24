import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExpectancyStatsDto, PlanExpectationDto, TradePlanService } from '../../../core/services/trade-plan.service';
import {
  computeExpectation, defaultScenarioRows, ExitTargetLike, ExpectationResult, ScenarioRow
} from './expectation-math';

/** Dưới ngưỡng này, % thắng lịch sử dao động quá mạnh để tin. */
const MIN_RELIABLE_SAMPLE = 10;

/**
 * Khối "Kỳ vọng" của kế hoạch Mua: bảng kịch bản kết quả của CẢ vị thế (nhãn, xác suất %, giá thoát) → EV theo R và VND,
 * % thắng ngầm định; bên cạnh là tỉ lệ nền lịch sử của chiến lược (kèm số mẫu và khoảng tin cậy). Ô xác suất KHÔNG điền sẵn
 * từ tỉ lệ nền: người dùng ước lượng trước rồi mới so (chống neo). EV ở đây chưa trừ phí; số đã trừ phí do server tính khi lưu.
 */
@Component({
  selector: 'app-plan-expectation',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="direction === 'Buy'" class="bg-white rounded-lg shadow p-6" data-testid="plan-expectation">
      <h2 class="text-lg font-semibold mb-1">Kỳ vọng của kế hoạch</h2>
      <p class="text-xs text-gray-500 mb-3">
        Mỗi dòng là một kết quả có thể xảy ra của <strong>cả vị thế</strong>. Nhập xác suất bạn tin (tổng 100%), không điền theo tỉ lệ nền
        để tránh bị neo. 1R = rủi ro ban đầu = |giá vào − cắt lỗ| × số lượng.
      </p>

      <div *ngIf="locked" class="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded px-3 py-2 mb-3" data-testid="estimate-locked">
        Ước lượng đã khoá lúc khớp lệnh đầu tiên: đối chiếu với kết quả thực tế sau này dùng bản đã khoá. Bạn vẫn sửa được bản hiện hành.
      </div>

      <div class="overflow-x-auto">
        <table class="min-w-full text-sm">
          <thead>
            <tr class="text-left text-xs text-gray-500 border-b">
              <th class="py-1 pr-2">Kịch bản</th>
              <th class="py-1 pr-2 w-24">Xác suất %</th>
              <th class="py-1 pr-2 w-32">Giá thoát</th>
              <th class="py-1 pr-2 w-20 text-right">R</th>
              <th class="py-1 w-8"></th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let r of rows; let i = index" data-testid="scenario-row" class="border-b last:border-0">
              <td class="py-1 pr-2">
                <input type="text" [value]="r.label" (input)="onLabel(i, $any($event.target).value)"
                  class="w-full px-2 py-1 border border-gray-300 rounded text-sm" aria-label="Tên kịch bản">
              </td>
              <td class="py-1 pr-2">
                <input type="number" min="0" max="100" step="any" inputmode="decimal" data-testid="prob-input"
                  [value]="r.probabilityPercent === null ? '' : r.probabilityPercent"
                  (input)="onProbability(i, $any($event.target).value)"
                  class="w-full px-2 py-1 border border-gray-300 rounded text-sm" aria-label="Xác suất phần trăm">
              </td>
              <td class="py-1 pr-2">
                <input type="number" min="0" step="any" inputmode="decimal" data-testid="exit-input"
                  [value]="r.exitPrice" (input)="onExit(i, $any($event.target).value)"
                  class="w-full px-2 py-1 border border-gray-300 rounded text-sm" aria-label="Giá thoát">
              </td>
              <td class="py-1 pr-2 text-right font-medium"
                [class.text-green-600]="(result.rowR[i] ?? 0) > 0" [class.text-red-600]="(result.rowR[i] ?? 0) < 0">
                {{ formatR(result.rowR[i]) }}
              </td>
              <td class="py-1 text-right">
                <button type="button" data-testid="remove-row" (click)="removeRow(i)" [disabled]="rows.length <= 1"
                  class="text-gray-400 hover:text-red-600 disabled:opacity-30" title="Xoá dòng" aria-label="Xoá dòng">✕</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="flex items-center justify-between mt-2">
        <button type="button" data-testid="add-row" (click)="addRow()"
          class="text-sm text-blue-600 hover:text-blue-800">+ Thêm kịch bản</button>
        <span class="text-sm" [class.text-red-600]="result.sum > 0 && !result.sumOk" [class.text-gray-700]="!(result.sum > 0 && !result.sumOk)">
          Tổng xác suất: {{ result.sum | number:'1.0-1' }}%
        </span>
      </div>
      <p *ngIf="result.sum > 0 && !result.sumOk" role="alert" class="text-xs text-red-600 mt-1">
        Tổng xác suất phải là 100% (đang là {{ result.sum | number:'1.0-1' }}%).
      </p>

      <div class="mt-4 rounded-lg bg-gray-50 border border-gray-200 px-4 py-3" data-testid="ev-summary">
        <ng-container *ngIf="result.evR !== null; else noEv">
          <div class="text-base font-semibold"
            [class.text-green-600]="result.evR > 0" [class.text-red-600]="result.evR <= 0">
            EV: {{ formatR(result.evR) }}
            <span class="text-sm font-normal text-gray-600">(≈ {{ result.evAmount | number:'1.0-0' }} đ)</span>
          </div>
          <div class="text-sm text-gray-700">Xác suất thắng ngầm định: {{ result.impliedWinPercent | number:'1.0-1' }}%
            <span class="text-xs text-gray-500">(R &gt; +0,1)</span></div>
          <div *ngIf="result.evR <= 0" class="text-xs text-amber-700 mt-1">
            Kỳ vọng không dương: chuyển sang Sẵn sàng sẽ cần ghi lý do.
          </div>
        </ng-container>
        <ng-template #noEv>
          <div class="text-sm text-gray-600">
            Chưa tính được kỳ vọng. {{ noEvReason }}
            Chuyển sang Sẵn sàng khi chưa ước lượng sẽ cần ghi lý do.
          </div>
        </ng-template>
        <div *ngIf="serverExpectation?.evR != null" class="text-xs text-gray-500 mt-1" data-testid="server-ev">
          Đã trừ phí ước tính (lúc lưu): EV {{ formatR(serverExpectation!.evR) }}
        </div>
        <div class="text-xs text-gray-400 mt-1">EV ở trên chưa trừ phí giao dịch.</div>
      </div>

      <div class="mt-4 text-sm" data-testid="base-rate">
        <div class="font-medium text-gray-700 mb-0.5">Tỉ lệ nền (chiến dịch đã đóng)</div>
        <div *ngIf="baseError" class="text-gray-500">Không tải được tỉ lệ nền.</div>
        <div *ngIf="!baseError && !base" class="text-gray-500">Chưa có chiến dịch đã đóng để so sánh.</div>
        <div *ngIf="base">
          {{ base.strategyName }}: thắng {{ formatPct(base.winRate) }}
          ({{ base.n }} lệnh<span *ngIf="base.winRateLow95 != null && base.winRateHigh95 != null">, 95%: {{ formatPct(base.winRateLow95) }} – {{ formatPct(base.winRateHigh95) }}</span>)
          · EV {{ formatR(base.expectancyR) }}
          <div *ngIf="base.n < minSample" class="text-xs text-amber-600">chưa đủ mẫu để tin</div>
        </div>
      </div>
    </div>
  `
})
export class PlanExpectationComponent implements OnChanges {
  @Input() direction = 'Buy';
  @Input() entry = 0;
  @Input() stopLoss = 0;
  @Input() target = 0;
  @Input() quantity = 0;
  @Input() exitTargets: ExitTargetLike[] = [];
  @Input() strategyId = '';
  /** Ảnh chụp ước lượng đã khoá (đã khớp lệnh đầu). */
  @Input() locked = false;
  /** Kỳ vọng do server tính (đã trừ phí ước tính) cho kế hoạch đã lưu. */
  @Input() serverExpectation: PlanExpectationDto | null = null;
  /** Bảng đã lưu của kế hoạch (rỗng = chưa có, dựng ba dòng mặc định). */
  @Input() scenarios: ScenarioRow[] = [];
  @Output() scenariosChange = new EventEmitter<ScenarioRow[]>();

  readonly minSample = MIN_RELIABLE_SAMPLE;
  rows: ScenarioRow[] = [];
  result: ExpectationResult = computeExpectation([], 0, 0, 0);
  noEvReason = '';

  base: ExpectancyStatsDto | null = null;
  baseError = false;
  private expectancy: { total: ExpectancyStatsDto; strategies: ExpectancyStatsDto[] } | null = null;
  private baseRequested = false;
  /** Các dòng mặc định chưa bị người dùng sửa giá thoát: giá thoát đi theo giá của kế hoạch. */
  private autoRows = new Set<ScenarioRow>();

  constructor(private planService: TradePlanService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (this.direction !== 'Buy') return;

    if (changes['scenarios'] && this.scenarios.length > 0 && this.scenarios !== this.rows) {
      this.rows = this.scenarios.map(s => ({ ...s }));
      this.autoRows.clear();   // bảng đã lưu: không tự đổi giá thoát
    } else if (this.rows.length === 0) {
      this.rows = defaultScenarioRows(this.direction, this.entry, this.stopLoss, this.target, this.exitTargets);
      this.rows.forEach(r => this.autoRows.add(r));
    } else {
      this.syncAutoRows();
    }

    this.recompute();
    this.loadBaseRate();
    if (changes['strategyId']) this.pickBase();
  }

  onLabel(i: number, value: string): void {
    this.rows[i].label = value;
    this.emit();
  }

  onProbability(i: number, value: string): void {
    this.rows[i].probabilityPercent = value === '' ? null : Number(value);
    this.recompute();
    this.emit();
  }

  onExit(i: number, value: string): void {
    this.rows[i].exitPrice = value === '' ? 0 : Number(value);
    this.autoRows.delete(this.rows[i]);
    this.recompute();
    this.emit();
  }

  addRow(): void {
    this.rows = [...this.rows, { label: 'Kịch bản mới', probabilityPercent: null, exitPrice: this.target }];
    this.recompute();
    this.emit();
  }

  removeRow(i: number): void {
    if (this.rows.length <= 1) return;
    this.autoRows.delete(this.rows[i]);
    this.rows = this.rows.filter((_, idx) => idx !== i);
    this.recompute();
    this.emit();
  }

  formatR(r: number | null | undefined): string {
    if (r == null) return 'n/a';
    return `${r > 0 ? '+' : ''}${r.toFixed(2)}R`;
  }

  formatPct(p: number | null | undefined): string {
    return p == null ? 'n/a' : `${Math.round(p * 100)}%`;
  }

  private syncAutoRows(): void {
    const fresh = defaultScenarioRows(this.direction, this.entry, this.stopLoss, this.target, this.exitTargets);
    this.rows.forEach(r => {
      if (!this.autoRows.has(r)) return;
      const match = fresh.find(f => f.label === r.label);
      if (match) r.exitPrice = match.exitPrice;
    });
  }

  private recompute(): void {
    this.result = computeExpectation(this.rows, this.entry, this.stopLoss, this.quantity);
    if (this.result.evR !== null) {
      this.noEvReason = '';
    } else if (this.result.riskAmount === null) {
      this.noEvReason = 'Cần giá vào lớn hơn giá cắt lỗ và có số lượng.';
    } else if (this.result.sum === 0) {
      this.noEvReason = 'Chưa nhập xác suất.';
    } else if (!this.result.sumOk) {
      this.noEvReason = 'Tổng xác suất phải là 100%.';
    } else {
      this.noEvReason = 'Có giá thoát không hợp lệ.';
    }
  }

  private emit(): void {
    this.scenariosChange.emit(this.rows.map(r => ({ ...r })));
  }

  private loadBaseRate(): void {
    if (this.baseRequested) return;
    this.baseRequested = true;
    this.planService.getExpectancy().subscribe({
      next: result => { this.expectancy = result; this.baseError = false; this.pickBase(); },
      error: () => { this.baseError = true; this.base = null; }
    });
  }

  /** Chiến lược của kế hoạch nếu đã có chiến dịch đóng, không thì dòng tổng; không có mẫu nào thì null. */
  private pickBase(): void {
    if (!this.expectancy) return;
    const own = this.strategyId ? this.expectancy.strategies.find(s => s.strategyId === this.strategyId && s.n > 0) : undefined;
    const chosen = own ?? this.expectancy.total;
    this.base = chosen && chosen.n > 0 ? chosen : null;
  }
}
