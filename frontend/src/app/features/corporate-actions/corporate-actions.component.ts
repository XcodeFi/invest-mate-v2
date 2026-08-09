import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import {
  CorporateActionService,
  CorporateAction,
  CorporateActionType
} from '../../core/services/corporate-action.service';
import { PortfolioService } from '../../core/services/portfolio.service';
import { PositionsService, ActivePosition } from '../../core/services/positions.service';

export const PAR_VALUE = 10_000;
export const DEFAULT_TAX_PERCENT = 5;

export interface AdjustmentPreview {
  quantityAfter: number;
  averageCostAfter: number;
  totalCostAfter: number;
  cashGross: number;
  cashNet: number;
}

/**
 * Xem trước tác động của sự kiện quyền lên một vị thế.
 * Hàm thuần, tách khỏi component để test được độc lập.
 */
export function previewAdjustment(
  quantity: number,
  totalCost: number,
  type: CorporateActionType,
  percentOfPar: number | null,
  ratioOld: number | null,
  ratioNew: number | null,
  taxRatePercent = DEFAULT_TAX_PERCENT
): AdjustmentPreview {
  if (type === 'CashDividend') {
    // "5%" là 5% của mệnh giá 10.000đ, không phải 5% giá thị trường
    const perShare = ((percentOfPar ?? 0) / 100) * PAR_VALUE;
    const gross = quantity * perShare;
    return {
      quantityAfter: quantity,
      averageCostAfter: quantity > 0 ? totalCost / quantity : 0,
      totalCostAfter: totalCost,
      cashGross: gross,
      cashNet: gross * (1 - taxRatePercent / 100)
    };
  }

  const multiplier = ratioOld && ratioNew && ratioOld > 0 ? ratioNew / ratioOld : 1;
  const quantityAfter = Math.floor(quantity * multiplier);
  return {
    quantityAfter,
    averageCostAfter: quantityAfter > 0 ? totalCost / quantityAfter : 0,
    totalCostAfter: totalCost,
    cashGross: 0,
    cashNet: 0
  };
}

const TYPE_LABELS: Record<CorporateActionType, string> = {
  CashDividend: 'Cổ tức tiền mặt',
  StockDividend: 'Cổ tức cổ phiếu',
  StockSplit: 'Chia tách cổ phiếu'
};

@Component({
  selector: 'app-corporate-actions',
  standalone: true,
  imports: [CommonModule, FormsModule, UppercaseDirective, VndCurrencyPipe],
  template: `
    <div class="p-4 max-w-6xl mx-auto">
      <div class="flex items-center justify-between mb-4">
        <div>
          <h1 class="text-xl font-semibold">Sự kiện quyền</h1>
          <p class="text-sm text-gray-500">
            Cổ tức tiền mặt, cổ tức cổ phiếu và chia tách — dùng để tính lại giá vốn và lãi/lỗ.
          </p>
        </div>
        <button type="button"
                class="rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700"
                (click)="openForm()">
          Thêm sự kiện quyền
        </button>
      </div>

      <div class="mb-4">
        <label class="text-sm font-medium text-gray-700 mr-2">Danh mục</label>
        <select class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 min-w-[200px]"
                [(ngModel)]="selectedPortfolioId" (ngModelChange)="reload()">
          @for (p of portfolios(); track p.id) {
            <option [value]="p.id">{{ p.name }}</option>
          }
        </select>
      </div>

      @if (loading()) {
        <p class="text-sm text-gray-500">Đang tải…</p>
      } @else if (actions().length === 0) {
        <p class="rounded border border-dashed p-6 text-center text-sm text-gray-500">
          Chưa có sự kiện quyền nào. Bấm “Thêm sự kiện quyền” để nhập.
        </p>
      } @else {
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="border-b text-left text-gray-500">
              <tr>
                <th class="px-3 py-2">Mã</th>
                <th class="px-3 py-2">Loại</th>
                <th class="px-3 py-2">Ngày GDKHQ</th>
                <th class="px-3 py-2">Ngày về</th>
                <th class="px-3 py-2 text-right">Tỷ lệ</th>
                <th class="px-3 py-2">Trạng thái</th>
                <th class="px-3 py-2"></th>
              </tr>
            </thead>
            <tbody>
              @for (a of actions(); track a.id) {
                <tr class="border-b">
                  <td class="px-3 py-2 font-medium">{{ a.symbol }}</td>
                  <td class="px-3 py-2">{{ typeLabel(a.type) }}</td>
                  <td class="px-3 py-2">{{ a.exDate | date: 'dd/MM/yyyy' }}</td>
                  <td class="px-3 py-2">
                    {{ a.settledAt ? (a.settledAt | date: 'dd/MM/yyyy') : (a.settlementDate ? (a.settlementDate | date: 'dd/MM/yyyy') : '—') }}
                  </td>
                  <td class="px-3 py-2 text-right">
                    {{ a.declaredText }}
                    @if (a.amountPerShare) {
                      <span class="block text-xs text-gray-500">{{ a.amountPerShare | vndCurrency }}/CP</span>
                    }
                  </td>
                  <td class="px-3 py-2">
                    @if (a.settledAt) {
                      <span class="rounded bg-green-100 px-2 py-0.5 text-xs text-green-700">Đã về</span>
                    } @else {
                      <span class="rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-700">Chờ về</span>
                    }
                  </td>
                  <td class="px-3 py-2 text-right whitespace-nowrap">
                    @if (!a.settledAt) {
                      <button type="button" class="mr-2 text-sm text-blue-600 hover:underline"
                              (click)="settle(a)">Xác nhận đã về</button>
                    }
                    <button type="button" class="text-sm text-red-600 hover:underline"
                            (click)="remove(a)">Xoá</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (error()) {
        <p class="mt-3 rounded bg-red-50 px-3 py-2 text-sm text-red-700">{{ error() }}</p>
      }

      @if (formOpen()) {
        <div class="fixed inset-0 z-[60] flex items-center justify-center bg-black/50 p-4">
          <div class="w-full max-w-lg rounded-lg bg-white p-5 shadow-xl">
            <h2 class="mb-4 text-lg font-semibold">Thêm sự kiện quyền</h2>

            <div class="space-y-3">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Mã chứng khoán</label>
                <input appUppercase [(ngModel)]="form.symbol"
                       class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                       placeholder="VD: HPG" />
              </div>

              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Loại sự kiện</label>
                <select [(ngModel)]="form.type" class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
                  <option value="CashDividend">Cổ tức tiền mặt</option>
                  <option value="StockDividend">Cổ tức cổ phiếu</option>
                  <option value="StockSplit">Chia tách cổ phiếu</option>
                </select>
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Ngày GDKHQ</label>
                  <input type="date" [(ngModel)]="form.exDate"
                         class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500" />
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Ngày về (dự kiến)</label>
                  <input type="date" [(ngModel)]="form.settlementDate"
                         class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500" />
                </div>
              </div>

              @if (form.type === 'CashDividend') {
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Tỷ lệ (% mệnh giá)</label>
                    <input type="number" [(ngModel)]="form.percentOfPar" min="0" step="0.01"
                           class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500" placeholder="VD: 5" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Thuế TNCN (%)</label>
                    <input type="number" [(ngModel)]="form.taxRatePercent" min="0" max="99" step="0.1"
                           class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500" />
                  </div>
                </div>
                <p class="text-xs text-gray-500">
                  “5%” nghĩa là 5% của mệnh giá 10.000đ = 500đ mỗi cổ phiếu, không phải 5% giá thị trường.
                </p>
              } @else {
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Cứ mỗi (CP cũ)</label>
                    <input type="number" [(ngModel)]="form.ratioOld" min="1" step="1"
                           class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500" placeholder="VD: 10" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Nhận thêm (CP)</label>
                    <input type="number" [(ngModel)]="form.bonusShares" min="1" step="1"
                           class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500" placeholder="VD: 3" />
                  </div>
                </div>
                <p class="text-xs text-gray-500">
                  Cổ tức cổ phiếu 30% tương đương “cứ 10 CP nhận thêm 3 CP”. Chia tách 1:2 là “cứ 1 CP nhận thêm 1 CP”.
                </p>
              }

              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Ghi chú</label>
                <input [(ngModel)]="form.note" class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500" />
              </div>

              @if (matchedPosition(); as pos) {
                <div class="rounded-lg bg-blue-50 border border-blue-100 p-3 text-sm">
                  <p class="font-medium">Xem trước tác động lên {{ pos.symbol }}</p>
                  @if (preview(); as pv) {
                    <p>
                      {{ pos.quantity | number }} CP → <strong>{{ pv.quantityAfter | number }} CP</strong>
                      · giá vốn {{ pos.averageCost | vndCurrency }} →
                      <strong>{{ pv.averageCostAfter | vndCurrency }}</strong>
                    </p>
                    @if (pv.cashGross > 0) {
                      <p>
                        Cổ tức {{ pv.cashGross | vndCurrency }} — sau thuế còn
                        <strong>{{ pv.cashNet | vndCurrency }}</strong>
                      </p>
                    }
                    <p class="text-xs text-gray-500">Tổng vốn không đổi: {{ pv.totalCostAfter | vndCurrency }}</p>
                  }
                </div>
              } @else if (form.symbol) {
                <p class="text-xs text-gray-500">
                  Không tìm thấy vị thế {{ form.symbol }} trong danh mục — vẫn lưu được, nhưng chưa xem trước được.
                </p>
              }

              @if (formError()) {
                <p class="rounded bg-red-50 px-3 py-2 text-sm text-red-700">{{ formError() }}</p>
              }
            </div>

            <div class="mt-5 flex justify-end gap-2">
              <button type="button" class="rounded border px-3 py-2 text-sm" (click)="closeForm()">Hủy</button>
              <button type="button"
                      class="rounded bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                      [disabled]="saving()" (click)="save()">
                {{ saving() ? 'Đang lưu…' : 'Lưu' }}
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class CorporateActionsComponent implements OnInit {
  private readonly service = inject(CorporateActionService);
  private readonly portfolioService = inject(PortfolioService);
  private readonly positionsService = inject(PositionsService);

  portfolios = signal<{ id: string; name: string }[]>([]);
  actions = signal<CorporateAction[]>([]);
  positions = signal<ActivePosition[]>([]);
  loading = signal(false);
  saving = signal(false);
  error = signal<string | null>(null);
  formError = signal<string | null>(null);
  formOpen = signal(false);

  selectedPortfolioId = '';

  form = this.emptyForm();

  // Method chứ KHÔNG phải computed(): `form` là object thường nên computed sẽ không
  // đăng ký dependency lên các field của nó và ô xem trước đứng im khi người dùng gõ.
  // ngModel kích hoạt change detection nên method chạy lại mỗi cycle.
  matchedPosition(): ActivePosition | null {
    const symbol = (this.form.symbol ?? '').toUpperCase().trim();
    if (!symbol) return null;
    return this.positions().find(p => p.symbol === symbol) ?? null;
  }

  preview(): AdjustmentPreview | null {
    const pos = this.matchedPosition();
    if (!pos) return null;
    const totalCost = pos.quantity * pos.averageCost;
    const ratioNew = this.form.ratioOld && this.form.bonusShares
      ? Number(this.form.ratioOld) + Number(this.form.bonusShares)
      : null;
    return previewAdjustment(
      pos.quantity, totalCost, this.form.type,
      this.form.percentOfPar, this.form.ratioOld, ratioNew,
      this.form.taxRatePercent ?? DEFAULT_TAX_PERCENT
    );
  }

  ngOnInit(): void {
    this.portfolioService.getAll().subscribe({
      next: list => {
        this.portfolios.set(list.map(p => ({ id: p.id, name: p.name })));
        if (list.length > 0) {
          this.selectedPortfolioId = list[0].id;
          this.reload();
        }
      },
      error: () => this.error.set('Không tải được danh sách danh mục.')
    });
  }

  typeLabel(type: CorporateActionType): string {
    return TYPE_LABELS[type];
  }

  reload(): void {
    if (!this.selectedPortfolioId) return;
    this.loading.set(true);
    this.error.set(null);

    this.service.getByPortfolio(this.selectedPortfolioId).subscribe({
      next: list => {
        this.actions.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Không tải được danh sách sự kiện quyền.');
        this.loading.set(false);
      }
    });

    this.positionsService.getAll(this.selectedPortfolioId).subscribe({
      next: list => this.positions.set(list),
      error: () => this.positions.set([])
    });
  }

  openForm(): void {
    this.form = this.emptyForm();
    this.formError.set(null);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  save(): void {
    this.formError.set(null);

    if (!this.form.symbol?.trim()) {
      this.formError.set('Chưa nhập mã chứng khoán.');
      return;
    }
    if (!this.form.exDate) {
      this.formError.set('Chưa chọn ngày giao dịch không hưởng quyền.');
      return;
    }
    if (this.form.type === 'CashDividend' && !this.form.percentOfPar) {
      this.formError.set('Chưa nhập tỷ lệ cổ tức tiền mặt.');
      return;
    }
    if (this.form.type !== 'CashDividend' && (!this.form.ratioOld || !this.form.bonusShares)) {
      this.formError.set('Chưa nhập đủ tỷ lệ cổ phiếu nhận thêm.');
      return;
    }

    this.saving.set(true);
    const isCash = this.form.type === 'CashDividend';

    this.service.create({
      PortfolioId: this.selectedPortfolioId,
      Symbol: this.form.symbol.trim().toUpperCase(),
      Type: this.form.type,
      ExDate: this.form.exDate,
      SettlementDate: this.form.settlementDate || null,
      PercentOfPar: isCash ? Number(this.form.percentOfPar) : null,
      TaxRatePercent: isCash ? Number(this.form.taxRatePercent ?? DEFAULT_TAX_PERCENT) : null,
      RatioOld: isCash ? null : Number(this.form.ratioOld),
      RatioNew: isCash ? null : Number(this.form.ratioOld) + Number(this.form.bonusShares),
      Note: this.form.note || null
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.formOpen.set(false);
        this.reload();
      },
      error: () => {
        this.saving.set(false);
        this.formError.set('Lưu không thành công. Kiểm tra lại thông tin đã nhập.');
      }
    });
  }

  settle(action: CorporateAction): void {
    const today = new Date().toISOString().slice(0, 10);
    if (!confirm(`Xác nhận ${TYPE_LABELS[action.type].toLowerCase()} ${action.symbol} đã về tài khoản hôm nay?`)) return;

    this.service.settle(action.id, today).subscribe({
      next: () => this.reload(),
      error: () => this.error.set('Xác nhận không thành công.')
    });
  }

  remove(action: CorporateAction): void {
    if (!confirm(`Xoá sự kiện ${TYPE_LABELS[action.type].toLowerCase()} của ${action.symbol}?`)) return;

    this.service.delete(action.id).subscribe({
      next: () => this.reload(),
      error: () => this.error.set('Không xoá được — sự kiện này có thể đã sinh dòng tiền cổ tức.')
    });
  }

  private emptyForm() {
    return {
      symbol: '',
      type: 'StockDividend' as CorporateActionType,
      exDate: '',
      settlementDate: '',
      percentOfPar: null as number | null,
      taxRatePercent: DEFAULT_TAX_PERCENT as number | null,
      ratioOld: null as number | null,
      bonusShares: null as number | null,
      note: ''
    };
  }
}
