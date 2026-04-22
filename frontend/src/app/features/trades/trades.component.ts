import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PortfolioService, PortfolioSummary, TradeResponseItem } from '../../core/services/portfolio.service';
import { TradeService } from '../../core/services/trade.service';
import { TradePlanService, TradePlan } from '../../core/services/trade-plan.service';
import { NotificationService } from '../../core/services/notification.service';
import { TradeType, getTradeTypeDisplay, getTradeTypeClass, TRADE_TYPE_FILTER_OPTIONS } from '../../shared/constants/trade-types';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';
import { JournalEntryService, PendingReviewTrade } from '../../core/services/journal-entry.service';

@Component({
  selector: 'app-trades',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe, UppercaseDirective, AiChatPanelComponent],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 py-6">
            <div>
              <h1 class="text-2xl sm:text-3xl font-bold text-gray-900">Lịch sử Giao dịch</h1>
              <p class="text-gray-600 mt-1 text-sm sm:text-base">Xem và quản lý tất cả giao dịch của bạn</p>
            </div>
            <div class="flex space-x-3">
              <button (click)="showAiPanel = true"
                class="bg-purple-600 hover:bg-purple-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200 flex items-center gap-1">
                🤖 AI Phân tích
              </button>
              <button
                routerLink="/trades/import"
                class="border border-blue-300 text-blue-700 hover:bg-blue-50 px-4 py-2 rounded-lg font-medium transition-colors duration-200 group relative"
                title="Nhập nhiều giao dịch cùng lúc từ file Excel/CSV"
              >
                📥 Nhập từ Excel
                <span class="hidden group-hover:block absolute top-full left-1/2 -translate-x-1/2 mt-1 px-2 py-1 bg-gray-800 text-white text-xs rounded whitespace-nowrap z-10">
                  Hỗ trợ file .csv — Nhập nhiều giao dịch cùng lúc
                </span>
              </button>
              <button
                routerLink="/trades/create"
                class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200"
              >
                + Thêm giao dịch mới
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content -->
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Filters -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Mã chứng khoán</label>
              <div class="relative">
                <input
                  type="text"
                  placeholder="VD: AAPL, VNM..." appUppercase
                  class="w-full px-3 py-2 pr-8 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  [(ngModel)]="filters.symbol"
                  (input)="applyFilters()"
                />
                <button *ngIf="filters.symbol"
                  (click)="clearSymbolFilter()"
                  class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-lg leading-none"
                  title="Xóa bộ lọc"
                >&times;</button>
              </div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Loại giao dịch</label>
              <select
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                [(ngModel)]="filters.type"
                (change)="applyFilters()"
              >
                <option value="">Tất cả</option>
                <option *ngFor="let option of TRADE_TYPE_FILTER_OPTIONS" [value]="option.value">
                  {{ option.label }}
                </option>
              </select>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Từ ngày</label>
              <input
                type="date"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                [(ngModel)]="filters.fromDate"
                (change)="applyFilters()"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Đến ngày</label>
              <input
                type="date"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                [(ngModel)]="filters.toDate"
                (change)="applyFilters()"
              />
            </div>
          </div>
        </div>

        <!-- Trades Table (desktop) -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
          <div class="hidden md:block overflow-x-auto">
            <table class="min-w-full divide-y divide-gray-200">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Mã CK</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Loại</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Số lượng</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Giá</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Tổng tiền</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Phí</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Thuế</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Ngày giao dịch</th>
                  <th class="px-6 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">Kế hoạch</th>
                  <th class="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Nhật ký</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Thao tác</th>
                </tr>
              </thead>
              <tbody class="bg-white divide-y divide-gray-200">
                <tr *ngFor="let trade of filteredTrades" class="hover:bg-gray-50">
                  <td class="px-6 py-4 whitespace-nowrap">
                    <button (click)="filterBySymbol(trade.symbol)"
                      class="text-sm font-medium text-blue-600 hover:text-blue-800 hover:underline cursor-pointer">
                      {{ trade.symbol }}
                    </button>
                    <a [routerLink]="['/symbol-timeline', trade.symbol]"
                      class="text-indigo-600 hover:text-indigo-800 text-xs ml-1" title="Xem timeline">📊</a>
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap">
                    <span
                      class="inline-flex px-2 py-1 text-xs font-semibold rounded-full"
                      [class]="getTradeTypeClass(trade.tradeType)"
                    >
                      {{ getTradeTypeDisplay(trade.tradeType) }}
                    </span>
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ trade.quantity }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ trade.price | vndCurrency }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ trade.totalValue | vndCurrency }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ trade.fee | vndCurrency }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ trade.tax | vndCurrency }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ formatDate(trade.tradeDate) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-center">
                    <a *ngIf="trade.tradePlanId" [routerLink]="['/trade-plan']"
                      [queryParams]="{ loadPlan: trade.tradePlanId }"
                      class="text-blue-600 hover:text-blue-800 font-medium">
                      Xem
                    </a>
                    <div *ngIf="!trade.tradePlanId" class="relative inline-block">
                      <button *ngIf="linkingTradeId !== trade.id"
                        (click)="showLinkPlanDropdown(trade)"
                        class="text-xs px-2 py-1 border border-dashed border-gray-300 text-gray-500 rounded hover:border-blue-400 hover:text-blue-600 transition-colors"
                        title="Gắn kế hoạch giao dịch">
                        + Gắn KH
                      </button>
                      <div *ngIf="linkingTradeId === trade.id" class="absolute z-20 right-0 mt-1 w-56 bg-white border border-gray-200 rounded-lg shadow-lg">
                        <div class="p-2 border-b border-gray-100 flex items-center justify-between">
                          <span class="text-xs font-medium text-gray-600">Chọn kế hoạch</span>
                          <button (click)="linkingTradeId = null" class="text-gray-400 hover:text-gray-600 text-sm">&times;</button>
                        </div>
                        <div class="max-h-40 overflow-y-auto">
                          <div *ngFor="let plan of getMatchingPlans(trade.symbol)"
                            (click)="linkTradeToPlan(trade, plan)"
                            class="px-3 py-2 text-xs hover:bg-blue-50 cursor-pointer border-b border-gray-50">
                            <div class="font-medium text-gray-800">{{ plan.symbol }} - {{ plan.direction }}</div>
                            <div class="text-gray-500">{{ plan.entryPrice | number:'1.0-0' }}đ · {{ plan.status }} · {{ formatDate(plan.createdAt) }}</div>
                          </div>
                          <div *ngIf="getMatchingPlans(trade.symbol).length === 0" class="px-3 py-3 text-center">
                            <div class="text-xs text-gray-400 mb-2">Không có KH cho {{ trade.symbol }}</div>
                            <a [routerLink]="['/trade-plan']" [queryParams]="{ symbol: trade.symbol }"
                              class="inline-block text-xs px-3 py-1.5 bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors font-medium">
                              + Tạo kế hoạch
                            </a>
                          </div>
                        </div>
                      </div>
                    </div>
                  </td>
                  <td class="px-3 py-4 whitespace-nowrap text-sm">
                    @if (hasPostTradeReview(trade)) {
                      <span class="text-emerald-600 cursor-pointer" title="Đã đánh giá" (click)="openJournal(trade)">&#10003;</span>
                    } @else if (trade.tradeType === 'SELL') {
                      <a [routerLink]="['/symbol-timeline']" [queryParams]="{symbol: trade.symbol, tradeId: trade.id}"
                         class="text-amber-500 hover:text-amber-700" title="Chưa đánh giá">&#9998;</a>
                    }
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                    <button
                      (click)="deleteTrade(trade.id)"
                      class="text-red-600 hover:text-red-900"
                    >
                      Xóa
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Mobile Cards -->
          <div class="md:hidden divide-y divide-gray-200">
            <div *ngFor="let trade of filteredTrades" class="p-4 space-y-2">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-1">
                  <button (click)="filterBySymbol(trade.symbol)"
                    class="text-sm font-bold text-blue-600 hover:text-blue-800">
                    {{ trade.symbol }}
                  </button>
                  <a [routerLink]="['/symbol-timeline', trade.symbol]"
                    class="text-indigo-600 hover:text-indigo-800 text-xs" title="Xem timeline">📊</a>
                </div>
                <span class="inline-flex px-2 py-0.5 text-xs font-semibold rounded-full"
                  [class]="getTradeTypeClass(trade.tradeType)">
                  {{ getTradeTypeDisplay(trade.tradeType) }}
                </span>
              </div>
              <div class="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
                <div><span class="text-gray-500">SL:</span> <span class="font-medium">{{ trade.quantity }}</span></div>
                <div><span class="text-gray-500">Giá:</span> <span class="font-medium">{{ trade.price | vndCurrency }}</span></div>
                <div><span class="text-gray-500">Tổng:</span> <span class="font-medium">{{ trade.totalValue | vndCurrency }}</span></div>
                <div><span class="text-gray-500">Ngày:</span> <span class="font-medium">{{ formatDate(trade.tradeDate) }}</span></div>
              </div>
              <div class="flex items-center justify-between pt-1 border-t border-gray-100 text-sm">
                <div>
                  <a *ngIf="trade.tradePlanId" [routerLink]="['/trade-plan']"
                    [queryParams]="{ loadPlan: trade.tradePlanId }"
                    class="text-blue-600 hover:text-blue-800 font-medium text-xs">
                    Xem KH
                  </a>
                  <a *ngIf="!trade.tradePlanId"
                    [routerLink]="['/trade-plan']" [queryParams]="{ symbol: trade.symbol }"
                    class="text-xs text-blue-600 hover:text-blue-800 font-medium">
                    {{ getMatchingPlans(trade.symbol).length > 0 ? 'Gắn KH' : '+ Tạo KH' }}
                  </a>
                </div>
                <button (click)="deleteTrade(trade.id)" class="text-red-600 hover:text-red-900 text-xs font-medium">Xóa</button>
              </div>
            </div>
          </div>

          <!-- Empty State -->
          <div *ngIf="filteredTrades.length === 0" class="text-center py-12">
            <svg class="mx-auto h-12 w-12 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"></path>
            </svg>
            <h3 class="mt-2 text-sm font-medium text-gray-900">Chưa có giao dịch nào</h3>
            <p class="mt-1 text-sm text-gray-500">Bắt đầu bằng cách thêm giao dịch đầu tiên của bạn.</p>
            <div class="mt-6">
              <button
                routerLink="/trades/create"
                class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200"
              >
                Thêm giao dịch đầu tiên
              </button>
            </div>
          </div>
        </div>

        <!-- Pagination -->
        <div *ngIf="filteredTrades.length > 0" class="bg-white px-4 py-3 border-t border-gray-200 sm:px-6 mt-4 rounded-lg">
          <div class="flex items-center justify-between">
            <div class="text-sm text-gray-700">
              Hiển thị {{ (currentPage - 1) * pageSize + 1 }} đến {{ min(currentPage * pageSize, totalTrades) }} trong tổng số {{ totalTrades }} giao dịch
            </div>
            <div class="flex space-x-2">
              <button
                (click)="previousPage()"
                [disabled]="currentPage === 1"
                class="px-3 py-1 text-sm border border-gray-300 rounded-md disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-50"
              >
                Trước
              </button>
              <span class="px-3 py-1 text-sm text-gray-700">
                Trang {{ currentPage }} / {{ totalPages }}
              </span>
              <button
                (click)="nextPage()"
                [disabled]="currentPage === totalPages"
                class="px-3 py-1 text-sm border border-gray-300 rounded-md disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-50"
              >
                Sau
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <app-ai-chat-panel
      [(isOpen)]="showAiPanel"
      title="AI Phân tích Giao dịch"
      useCase="trade-analysis"
      [contextData]="emptyContext">
    </app-ai-chat-panel>
  `,
  styles: []
})
export class TradesComponent implements OnInit {
  allTrades: TradeResponseItem[] = [];
  filteredTrades: TradeResponseItem[] = [];
  showAiPanel = false;
  readonly emptyContext = {};
  portfolios: PortfolioSummary[] = [];
  currentPage: number = 1;
  pageSize: number = 10;
  totalTrades: number = 0;
  totalPages: number = 0;
  isLoading = true;

  filters = {
    symbol: '',
    type: '',
    fromDate: '',
    toDate: ''
  };

  // Plan linking
  allPlans: TradePlan[] = [];
  linkingTradeId: string | null = null;

  pendingReviewTradeIds: Set<string> = new Set();
  pendingReviewLoaded = false;

  constructor(
    private portfolioService: PortfolioService,
    private tradeService: TradeService,
    private tradePlanService: TradePlanService,
    private notificationService: NotificationService,
    private route: ActivatedRoute,
    private journalEntryService: JournalEntryService,
    private router: Router
  ) {}

  // Trade type utility functions
  getTradeTypeDisplay = getTradeTypeDisplay;
  getTradeTypeClass = getTradeTypeClass;
  TRADE_TYPE_FILTER_OPTIONS = TRADE_TYPE_FILTER_OPTIONS;

  ngOnInit(): void {
    const params = this.route.snapshot.queryParams;
    if (params['symbol']) {
      this.filters.symbol = params['symbol'];
    }
    this.loadTrades();
    this.tradePlanService.getAll().subscribe({
      next: (plans) => this.allPlans = plans,
      error: () => {}
    });
  }

  private loadTrades(): void {
    this.isLoading = true;
    // Load all portfolios first, then get trades for each
    this.portfolioService.getAll().subscribe({
      next: (portfolios) => {
        this.portfolios = portfolios;
        if (portfolios.length === 0) {
          this.allTrades = [];
          this.filteredTrades = [];
          this.isLoading = false;
          return;
        }
        // Load trades from first portfolio by default (or all)
        this.loadAllTrades(portfolios);
      },
      error: () => {
        this.isLoading = false;
        this.notificationService.error('Lỗi', 'Không thể tải dữ liệu');
      }
    });
  }

  private loadAllTrades(portfolios: PortfolioSummary[]): void {
    let completed = 0;
    this.allTrades = [];

    portfolios.forEach(p => {
      this.portfolioService.getTrades(p.id, { pageSize: 100 }).subscribe({
        next: (data) => {
          this.allTrades.push(...data.items);
          completed++;
          if (completed === portfolios.length) {
            this.allTrades.sort((a, b) => new Date(b.tradeDate).getTime() - new Date(a.tradeDate).getTime());
            this.applyFilters();
            this.isLoading = false;
            this.journalEntryService.getPendingReview().subscribe({
              next: (pending) => { this.pendingReviewTradeIds = new Set(pending.map(p => p.tradeId)); this.pendingReviewLoaded = true; },
              error: () => { this.pendingReviewLoaded = true; }
            });
          }
        },
        error: () => {
          completed++;
          if (completed === portfolios.length) {
            this.applyFilters();
            this.isLoading = false;
          }
        }
      });
    });
  }

  private _filtered: TradeResponseItem[] = [];

  applyFilters(): void {
    this._filtered = this.allTrades.filter(trade => {
      const matchesSymbol = !this.filters.symbol || trade.symbol.toLowerCase().includes(this.filters.symbol.toLowerCase());
      const matchesType = !this.filters.type || trade.tradeType === this.filters.type;
      const matchesFromDate = !this.filters.fromDate || new Date(trade.tradeDate) >= new Date(this.filters.fromDate);
      const matchesToDate = !this.filters.toDate || new Date(trade.tradeDate) <= new Date(this.filters.toDate + 'T23:59:59');
      return matchesSymbol && matchesType && matchesFromDate && matchesToDate;
    });

    this.totalTrades = this._filtered.length;
    this.totalPages = Math.ceil(this.totalTrades / this.pageSize);
    this.currentPage = 1;
    this.updatePage();
  }

  private updatePage(): void {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    this.filteredTrades = this._filtered.slice(startIndex, startIndex + this.pageSize);
  }

  previousPage(): void {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.updatePage();
    }
  }

  nextPage(): void {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.updatePage();
    }
  }

  deleteTrade(tradeId: string): void {
    if (confirm('Bạn có chắc chắn muốn xóa giao dịch này?')) {
      this.tradeService.delete(tradeId).subscribe({
        next: () => {
          this.notificationService.success('Thành công', 'Giao dịch đã được xóa');
          this.allTrades = this.allTrades.filter(t => t.id !== tradeId);
          this.applyFilters();
        },
        error: () => {
          this.notificationService.error('Lỗi', 'Không thể xóa giao dịch');
        }
      });
    }
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('vi-VN', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  clearSymbolFilter(): void {
    this.filters.symbol = '';
    this.applyFilters();
  }

  filterBySymbol(symbol: string): void {
    this.filters.symbol = symbol;
    this.applyFilters();
  }

  // Plan linking
  showLinkPlanDropdown(trade: TradeResponseItem): void {
    this.linkingTradeId = trade.id;
  }

  getMatchingPlans(symbol: string): TradePlan[] {
    return this.allPlans.filter(p =>
      p.symbol.toUpperCase() === symbol.toUpperCase()
    );
  }

  linkTradeToPlan(trade: TradeResponseItem, plan: TradePlan): void {
    this.tradeService.linkToPlan(trade.id, plan.id).subscribe({
      next: () => {
        trade.tradePlanId = plan.id;
        this.linkingTradeId = null;
        this.notificationService.success('Thành công', `Đã gắn kế hoạch cho ${trade.symbol}`);
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không thể gắn kế hoạch');
      }
    });
  }

  hasPostTradeReview(trade: any): boolean {
    return this.pendingReviewLoaded && trade.tradeType === 'SELL' && !this.pendingReviewTradeIds.has(trade.id);
  }

  openJournal(trade: any): void {
    this.router.navigate(['/symbol-timeline'], { queryParams: { symbol: trade.symbol, tradeId: trade.id } });
  }

  min(a: number, b: number): number {
    return Math.min(a, b);
  }
}