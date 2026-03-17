import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TradeService, CreateTradeRequest } from '../../../core/services/trade.service';
import { PortfolioService, PortfolioSummary } from '../../../core/services/portfolio.service';
import { FeeService, FeeCalculationRequest, FeeCalculationResponse } from '../../../core/services/fee.service';
import { TradePlanService } from '../../../core/services/trade-plan.service';
import { PnlService, PositionPnL } from '../../../core/services/pnl.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TradeType, isSellTrade } from '../../../shared/constants/trade-types';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../../shared/directives/num-mask.directive';
import { UppercaseDirective } from '../../../shared/directives/uppercase.directive';

interface StockSymbolEntry {
  symbol: string;
  name: string;
  exchange: string;
}

@Component({
  selector: 'app-trade-create',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe, NumMaskDirective, UppercaseDirective],
  template: `
    <div class="min-h-screen bg-gray-50">
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex items-center py-6">
            <button routerLink="/trades" class="mr-4 text-gray-500 hover:text-gray-700">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
              </svg>
            </button>
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Thêm Giao dịch Mới</h1>
              <p class="text-gray-600 mt-1">Tạo lệnh mua hoặc bán cổ phiếu</p>
            </div>
          </div>
        </div>
      </div>

      <div class="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <form (ngSubmit)="onSubmit()" #tradeForm="ngForm">
            <div class="space-y-6">
              <!-- Portfolio Selection -->
              <div>
                <label for="portfolioId" class="block text-sm font-medium text-gray-700 mb-1">Danh mục <span class="text-red-500">*</span></label>
                <select id="portfolioId" name="portfolioId" [(ngModel)]="form.portfolioId" required
                  (ngModelChange)="loadPositionInfo()"
                  class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  #portfolioInput="ngModel">
                  <option value="">-- Chọn danh mục --</option>
                  <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }} — {{ p.initialCapital | vndCurrency }}</option>
                </select>
                <p *ngIf="portfolioInput.invalid && portfolioInput.touched" class="mt-1 text-sm text-red-600">Vui lòng chọn danh mục</p>
              </div>

              <!-- Trade Type -->
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-2">Loại giao dịch <span class="text-red-500">*</span></label>
                <div class="flex space-x-4">
                  <label class="flex-1 cursor-pointer">
                    <input type="radio" name="tradeType" [value]="TradeType.BUY" [(ngModel)]="form.tradeType" required class="sr-only" (change)="onFormChange(); validateQuantity()" />
                    <div class="text-center py-3 px-4 rounded-lg border-2 transition-colors"
                      [class]="form.tradeType === TradeType.BUY ? 'border-green-500 bg-green-50 text-green-700' : 'border-gray-300 hover:border-gray-400'">
                      <span class="font-semibold text-lg">MUA</span>
                    </div>
                  </label>
                  <label class="flex-1 cursor-pointer">
                    <input type="radio" name="tradeType" [value]="TradeType.SELL" [(ngModel)]="form.tradeType" required class="sr-only" (change)="onFormChange(); validateQuantity()" />
                    <div class="text-center py-3 px-4 rounded-lg border-2 transition-colors"
                      [class]="form.tradeType === TradeType.SELL ? 'border-red-500 bg-red-50 text-red-700' : 'border-gray-300 hover:border-gray-400'">
                      <span class="font-semibold text-lg">BÁN</span>
                    </div>
                  </label>
                </div>
              </div>

              <!-- Symbol with Autocomplete -->
              <div class="relative">
                <label for="symbol" class="block text-sm font-medium text-gray-700 mb-1">Mã chứng khoán <span class="text-red-500">*</span></label>
                <input type="text" id="symbol" name="symbol" [(ngModel)]="form.symbol" required appUppercase
                  class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  placeholder="VD: VNM, VIC, FPT..."
                  (input)="onSymbolInput($event); onFormChange()"
                  (focus)="showSymbolDropdown = true"
                  (blur)="hideDropdownDelayed()"
                  autocomplete="off"
                  #symbolInput="ngModel" />
                <p *ngIf="symbolInput.invalid && symbolInput.touched" class="mt-1 text-sm text-red-600">Mã chứng khoán là bắt buộc</p>
                <!-- Autocomplete Dropdown -->
                <div *ngIf="showSymbolDropdown && filteredSymbols.length > 0"
                  class="absolute z-10 w-full mt-1 bg-white border border-gray-300 rounded-lg shadow-lg max-h-60 overflow-y-auto">
                  <div *ngFor="let s of filteredSymbols"
                    class="px-4 py-2 hover:bg-blue-50 cursor-pointer text-sm"
                    (mousedown)="selectSymbol(s.symbol)">
                    <span class="font-semibold text-gray-900">{{ s.symbol }}</span>
                    <span class="text-gray-500 ml-2">{{ s.name }}</span>
                  </div>
                </div>
              </div>

              <!-- Position Info (when selling) -->
              <div *ngIf="positionLoading" class="bg-gray-50 rounded-lg p-3 text-sm text-gray-500 text-center">
                Đang tải thông tin vị thế...
              </div>
              <div *ngIf="positionInfo && positionInfo.quantity > 0" class="bg-gradient-to-r from-blue-50 to-indigo-50 rounded-lg p-4 border border-blue-100">
                <div class="flex items-center justify-between mb-2">
                  <span class="text-sm font-semibold text-blue-800">Thông tin vị thế {{ positionInfo.symbol }}</span>
                  <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                    [class.bg-green-100]="positionInfo.unrealizedPnL >= 0" [class.text-green-700]="positionInfo.unrealizedPnL >= 0"
                    [class.bg-red-100]="positionInfo.unrealizedPnL < 0" [class.text-red-700]="positionInfo.unrealizedPnL < 0">
                    {{ positionInfo.unrealizedPnL >= 0 ? '+' : '' }}{{ positionInfo.unrealizedPnLPercent | number:'1.2-2' }}%
                  </span>
                </div>
                <div class="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
                  <div>
                    <div class="text-xs text-gray-500">Đang nắm giữ</div>
                    <div class="font-bold text-blue-800">{{ positionInfo.quantity | number:'1.0-0' }} CP</div>
                  </div>
                  <div>
                    <div class="text-xs text-gray-500">Giá TB</div>
                    <div class="font-medium">{{ positionInfo.averageCost | number:'1.0-0' }}đ</div>
                  </div>
                  <div>
                    <div class="text-xs text-gray-500">Giá hiện tại</div>
                    <div class="font-medium">{{ positionInfo.currentPrice | number:'1.0-0' }}đ</div>
                  </div>
                  <div>
                    <div class="text-xs text-gray-500">Lãi/lỗ chưa hiện thực</div>
                    <div class="font-medium" [class.text-green-600]="positionInfo.unrealizedPnL >= 0" [class.text-red-600]="positionInfo.unrealizedPnL < 0">
                      {{ positionInfo.unrealizedPnL >= 0 ? '+' : '' }}{{ positionInfo.unrealizedPnL | vndCurrency }}
                    </div>
                  </div>
                </div>
                <div *ngIf="form.tradeType === TradeType.SELL" class="mt-2 flex gap-2">
                  <button type="button" (click)="form.quantity = positionInfo!.quantity; onFormChange()"
                    class="text-xs px-2 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors">
                    Bán toàn bộ ({{ positionInfo.quantity | number:'1.0-0' }})
                  </button>
                  <button type="button" (click)="form.quantity = Math.floor(positionInfo!.quantity / 2 / 100) * 100; onFormChange()"
                    class="text-xs px-2 py-1 border border-blue-300 text-blue-700 rounded hover:bg-blue-50 transition-colors">
                    Bán 50%
                  </button>
                </div>
              </div>
              <div *ngIf="positionInfo && positionInfo.quantity === 0 && form.tradeType === TradeType.SELL"
                class="bg-yellow-50 border border-yellow-200 rounded-lg p-3 text-sm text-yellow-700">
                Không có vị thế {{ form.symbol }} trong danh mục này
              </div>

              <!-- Trade Date -->
              <div>
                <label for="tradeDate" class="block text-sm font-medium text-gray-700 mb-1">Ngày giao dịch</label>
                <input type="datetime-local" id="tradeDate" name="tradeDate" [(ngModel)]="form.tradeDate"
                  class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent" />
                <p class="mt-1 text-xs text-gray-500">Mặc định là ngày giờ hiện tại nếu để trống</p>
              </div>

              <!-- Quantity & Price -->
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label for="quantity" class="block text-sm font-medium text-gray-700 mb-1">Số lượng <span class="text-red-500">*</span></label>
                  <input type="text" inputmode="numeric" appNumMask id="quantity" name="quantity" [(ngModel)]="form.quantity" required min="100" step="100"
                    class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    placeholder="100" (input)="onFormChange()" #qtyInput="ngModel" />
                  <p *ngIf="qtyInput.invalid && qtyInput.touched" class="mt-1 text-sm text-red-600">Tối thiểu 100, bước nhảy 100</p>
                  <p *ngIf="quantityError" class="mt-1 text-sm text-red-600 font-medium">{{ quantityError }}</p>
                </div>
                <div>
                  <label for="price" class="block text-sm font-medium text-gray-700 mb-1">Giá <span class="text-red-500">*</span></label>
                  <input type="text" inputmode="numeric" appNumMask id="price" name="price" [(ngModel)]="form.price" required min="0.01" step="0.01"
                    class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    placeholder="0.00" (input)="onFormChange()" #priceInput="ngModel" />
                  <p *ngIf="priceInput.invalid && priceInput.touched" class="mt-1 text-sm text-red-600">Phải lớn hơn 0</p>
                  <p *ngIf="form.price > 0" class="mt-1 text-sm text-gray-500">{{ form.price | vndCurrency }}</p>
                </div>
              </div>

              <!-- Fee & Tax (Auto-calculated) -->
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Phí giao dịch</label>
                  <div class="w-full px-4 py-2 border border-gray-300 rounded-lg bg-gray-50 text-gray-700">
                    {{ isCalculatingFees ? 'Đang tính...' : (feeCalculation ? (feeCalculation.totalFees | vndCurrency) : '0 VND') }}
                  </div>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Thuế</label>
                  <div class="w-full px-4 py-2 border border-gray-300 rounded-lg bg-gray-50 text-gray-700">
                    {{ isCalculatingFees ? 'Đang tính...' : (feeCalculation ? (feeCalculation.breakdown.tax | vndCurrency) : '0 VND') }}
                  </div>
                </div>
              </div>

              <!-- Order Summary -->
              <div *ngIf="form.quantity > 0 && form.price > 0" class="bg-gray-50 rounded-lg p-4">
                <h4 class="text-sm font-medium text-gray-700 mb-2">Tóm tắt lệnh</h4>
                <div class="space-y-1 text-sm">
                  <div class="flex justify-between"><span class="text-gray-600">Giá trị giao dịch:</span><span class="font-medium">{{ form.quantity * form.price | vndCurrency }}</span></div>
                  <div *ngIf="feeCalculation" class="space-y-1">
                    <div class="flex justify-between"><span class="text-gray-600">Phí giao dịch:</span><span>{{ feeCalculation.breakdown.transactionFee | vndCurrency }}</span></div>
                    <div class="flex justify-between"><span class="text-gray-600">VAT:</span><span>{{ feeCalculation.breakdown.vat | vndCurrency }}</span></div>
                    <div class="flex justify-between"><span class="text-gray-600">Thuế thu nhập:</span><span>{{ feeCalculation.breakdown.tax | vndCurrency }}</span></div>
                  </div>
                  <div *ngIf="!feeCalculation && !isCalculatingFees" class="text-yellow-600 text-xs">Không thể tính phí - vui lòng kiểm tra thông tin</div>
                  <hr class="my-1" />
                  <div class="flex justify-between font-bold"><span>Tổng chi phí:</span><span>{{ feeCalculation ? (form.quantity * form.price + feeCalculation.totalFees | vndCurrency) : (form.quantity * form.price | vndCurrency) }}</span></div>
                </div>
              </div>

              <div class="flex justify-end space-x-3 pt-4 border-t border-gray-200">
                <button type="button" routerLink="/trades" class="px-6 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium">
                  Hủy
                </button>
                <button type="submit" [disabled]="tradeForm.invalid || isSubmitting || !!quantityError"
                  class="px-6 py-2 rounded-lg font-medium text-white disabled:opacity-50 disabled:cursor-not-allowed"
                  [class]="form.tradeType === TradeType.SELL ? 'bg-red-600 hover:bg-red-700' : 'bg-green-600 hover:bg-green-700'">
                  {{ isSubmitting ? 'Đang xử lý...' : (form.tradeType === TradeType.SELL ? 'Đặt lệnh BÁN' : 'Đặt lệnh MUA') }}
                </button>
              </div>
            </div>
          </form>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class TradeCreateComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  form: CreateTradeRequest = {
    portfolioId: '',
    symbol: '',
    tradeType: TradeType.BUY,
    quantity: 0,
    price: 0,
    fee: 0,
    tax: 0,
    tradeDate: ''
  };
  isSubmitting = false;
  isCalculatingFees = false;
  feeCalculation: FeeCalculationResponse | null = null;

  // Symbol autocomplete
  allSymbols: StockSymbolEntry[] = [];
  filteredSymbols: StockSymbolEntry[] = [];
  showSymbolDropdown = false;

  planId: string | null = null;
  lotNumber: number | null = null;
  exitAction: string = '';

  // Position info for sell validation
  positionInfo: PositionPnL | null = null;
  positionLoading = false;
  quantityError = '';

  constructor(
    private tradeService: TradeService,
    private portfolioService: PortfolioService,
    private feeService: FeeService,
    private tradePlanService: TradePlanService,
    private pnlService: PnlService,
    private notificationService: NotificationService,
    private router: Router,
    private route: ActivatedRoute,
    private http: HttpClient
  ) {}

  // Trade type enum
  TradeType = TradeType;
  Math = Math;

  ngOnInit(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => this.portfolios = data,
      error: () => this.notificationService.error('Lỗi', 'Không thể tải danh sách danh mục')
    });

    // Load stock symbols for autocomplete
    this.http.get<StockSymbolEntry[]>('assets/data/vn-stock-symbols.json').subscribe({
      next: (data) => this.allSymbols = data,
      error: () => console.warn('Could not load stock symbols')
    });

    // Set default trade date to now
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    this.form.tradeDate = now.toISOString().slice(0, 16);

    // Auto-fill from query params (from Trade Plan page)
    const params = this.route.snapshot.queryParams;
    this.planId = params['planId'] || null;
    this.lotNumber = params['lotNumber'] ? +params['lotNumber'] : null;
    if (params['symbol']) this.form.symbol = params['symbol'];
    if (params['direction']) this.form.tradeType = isSellTrade(params['direction']) ? TradeType.SELL : TradeType.BUY;
    if (params['price']) this.form.price = +params['price'];
    if (params['quantity']) this.form.quantity = +params['quantity'];
    if (params['portfolioId']) this.form.portfolioId = params['portfolioId'];
    if (this.form.symbol && this.form.price && this.form.quantity) {
      this.calculateFees();
    }
    // Load position info when symbol + portfolio are pre-filled
    if (this.form.symbol && this.form.portfolioId) {
      this.loadPositionInfo();
    }
  }

  onSymbolInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value.toUpperCase();
    if (value.length > 0) {
      this.filteredSymbols = this.allSymbols.filter(s =>
        s.symbol.includes(value) || s.name.toLowerCase().includes(value.toLowerCase())
      ).slice(0, 10);
      this.showSymbolDropdown = true;
    } else {
      this.filteredSymbols = [];
      this.showSymbolDropdown = false;
    }
  }

  selectSymbol(symbol: string): void {
    this.form.symbol = symbol;
    this.filteredSymbols = [];
    this.showSymbolDropdown = false;
    this.onFormChange();
    this.loadPositionInfo();
  }

  hideDropdownDelayed(): void {
    setTimeout(() => this.showSymbolDropdown = false, 200);
  }

  calculateFees(): void {
    if (!this.form.symbol || this.form.quantity <= 0 || this.form.price <= 0) {
      this.feeCalculation = null;
      return;
    }

    this.isCalculatingFees = true;
    const request: FeeCalculationRequest = {
      symbol: this.form.symbol.toUpperCase(),
      tradeType: this.form.tradeType,
      quantity: this.form.quantity,
      price: this.form.price
    };

    this.feeService.calculateFees(request).subscribe({
      next: (response) => {
        this.feeCalculation = response;
        this.isCalculatingFees = false;
      },
      error: (err) => {
        console.error('Fee calculation error:', err);
        this.feeCalculation = null;
        this.isCalculatingFees = false;
        // Don't show error notification for fee calculation failures
      }
    });
  }

  onFormChange(): void {
    // Trigger fee calculation when form changes
    setTimeout(() => this.calculateFees(), 300); // Simple debounce
    this.validateQuantity();
  }

  loadPositionInfo(): void {
    if (!this.form.portfolioId || !this.form.symbol) {
      this.positionInfo = null;
      return;
    }
    this.positionLoading = true;
    this.pnlService.getPositionPnL(this.form.portfolioId, this.form.symbol.toUpperCase()).subscribe({
      next: (pos) => {
        this.positionInfo = pos;
        this.positionLoading = false;
        this.validateQuantity();
      },
      error: () => {
        this.positionInfo = null;
        this.positionLoading = false;
      }
    });
  }

  validateQuantity(): void {
    this.quantityError = '';
    if (this.form.tradeType === TradeType.BUY && this.form.quantity > 0) {
      if (this.form.quantity % 100 !== 0) {
        this.quantityError = 'Lệnh MUA phải là lô chẵn (bội số của 100)';
      } else if (this.form.price > 0) {
        const tradeValue = this.form.quantity * this.form.price;
        const portfolio = this.portfolios.find(p => p.id === this.form.portfolioId);
        if (portfolio) {
          const remainingCash = portfolio.initialCapital - portfolio.totalInvested + portfolio.totalSold;
          if (tradeValue > remainingCash) {
            this.quantityError = `Giá trị lệnh (${tradeValue.toLocaleString('vi-VN')}đ) vượt quá tiền còn lại của danh mục (${remainingCash.toLocaleString('vi-VN')}đ)`;
          }
        }
      }
    }
    if (this.form.tradeType === TradeType.SELL && this.positionInfo && this.form.quantity > 0) {
      if (this.form.quantity > this.positionInfo.quantity) {
        this.quantityError = `Vượt quá số lượng đang nắm giữ (${this.positionInfo.quantity.toLocaleString('vi-VN')} CP)`;
      }
    }
  }

  onSubmit(): void {
    if (this.isSubmitting) return;

    if (!this.feeCalculation) {
      this.notificationService.error('Lỗi', 'Không thể tính phí giao dịch. Vui lòng kiểm tra thông tin và thử lại.');
      return;
    }

    this.isSubmitting = true;

    const payload: CreateTradeRequest = {
      portfolioId: this.form.portfolioId,
      symbol: this.form.symbol.toUpperCase(),
      tradeType: this.form.tradeType,
      quantity: this.form.quantity,
      price: this.form.price,
      fee: this.feeCalculation.totalFees,
      tax: this.feeCalculation.breakdown.tax,
      tradeDate: this.form.tradeDate || undefined
    };

    this.tradeService.create(payload).subscribe({
      next: (result) => {
        this.notificationService.success('Thành công', `Lệnh ${this.form.tradeType === TradeType.BUY ? 'MUA' : 'BÁN'} ${this.form.symbol.toUpperCase()} đã được tạo`);
        // Link trade to plan
        if (this.planId && result?.id) {
          if (this.lotNumber) {
            // Execute specific lot
            this.tradePlanService.executeLot(this.planId, this.lotNumber, {
              tradeId: result.id, actualPrice: this.form.price
            }).subscribe();
          } else if (this.exitAction && this.form.tradeType === TradeType.SELL) {
            // Trigger exit target for sell trades
            const exitLevel = +this.exitAction;
            if (exitLevel > 0) {
              this.tradePlanService.triggerExitTarget(this.planId, exitLevel, { tradeId: result.id }).subscribe();
            }
          } else {
            // Fallback: generic status update
            this.tradePlanService.updateStatus(this.planId, { status: 'executed', tradeId: result.id }).subscribe();
          }
        }
        this.router.navigate(['/trades']);
      },
      error: (err) => {
        this.isSubmitting = false;
        this.notificationService.error('Lỗi', err.error?.message || 'Không thể tạo giao dịch');
      }
    });
  }

}