import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PortfolioService, TradeResponseItem } from '../../../core/services/portfolio.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TradeService } from '../../../core/services/trade.service';
import { getTradeTypeDisplay, getTradeTypeClass, TRADE_TYPE_FILTER_OPTIONS } from '../../../shared/constants/trade-types';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';

@Component({
  selector: 'app-portfolio-trades',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex justify-between items-center py-6">
            <div class="flex items-center">
              <button [routerLink]="['/portfolios', portfolioId]" class="mr-4 text-gray-500 hover:text-gray-700">
                <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
                </svg>
              </button>
              <div>
                <h1 class="text-3xl font-bold text-gray-900">Giao dịch của Danh mục</h1>
                <p class="text-gray-600 mt-1">Tổng cộng {{ totalCount }} giao dịch</p>
              </div>
            </div>
            <button routerLink="/trades/create" class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium">
              + Thêm giao dịch
            </button>
          </div>
        </div>
      </div>

      <!-- Filters -->
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Mã chứng khoán</label>
              <input type="text" placeholder="VD: AAPL" class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                [(ngModel)]="filters.symbol" (input)="loadTrades()" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Loại giao dịch</label>
              <select class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                [(ngModel)]="filters.tradeType" (change)="loadTrades()">
                <option value="">Tất cả</option>
                <option *ngFor="let option of TRADE_TYPE_FILTER_OPTIONS" [value]="option.value">
                  {{ option.label }}
                </option>
              </select>
            </div>
            <div class="flex items-end">
              <button (click)="resetFilters()" class="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50">
                Xóa bộ lọc
              </button>
            </div>
          </div>
        </div>

        <!-- Loading -->
        <div *ngIf="isLoading" class="text-center py-12">
          <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
        </div>

        <!-- Table -->
        <div *ngIf="!isLoading" class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-gray-200">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mã CK</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Loại</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Số lượng</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Giá</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Tổng giá trị</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Phí</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Thuế</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ngày GD</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Thao tác</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200">
                <tr *ngFor="let trade of trades" class="hover:bg-gray-50">
                  <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ trade.symbol }}</td>
                  <td class="px-6 py-4">
                    <span class="inline-flex px-2 py-1 text-xs font-semibold rounded-full"
                      [class]="getTradeTypeClass(trade.tradeType)">
                      {{ getTradeTypeDisplay(trade.tradeType) }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ trade.quantity }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ trade.price | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ trade.totalValue | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ trade.fee | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ trade.tax | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ formatDate(trade.tradeDate) }}</td>
                  <td class="px-6 py-4 text-sm">
                    <button (click)="deleteTrade(trade.id)" class="text-red-600 hover:text-red-900">Xóa</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Empty -->
          <div *ngIf="trades.length === 0" class="text-center py-12">
            <h3 class="text-sm font-medium text-gray-900">Chưa có giao dịch nào</h3>
            <p class="mt-1 text-sm text-gray-500">Thêm giao dịch mới để bắt đầu theo dõi.</p>
          </div>
        </div>

        <!-- Pagination -->
        <div *ngIf="totalPages > 1" class="bg-white px-4 py-3 border-t border-gray-200 sm:px-6 mt-4 rounded-lg">
          <div class="flex items-center justify-between">
            <div class="text-sm text-gray-700">
              Trang {{ currentPage }} / {{ totalPages }} ({{ totalCount }} giao dịch)
            </div>
            <div class="flex space-x-2">
              <button (click)="changePage(currentPage - 1)" [disabled]="currentPage === 1"
                class="px-3 py-1 text-sm border border-gray-300 rounded-md disabled:opacity-50 hover:bg-gray-50">
                Trước
              </button>
              <button (click)="changePage(currentPage + 1)" [disabled]="currentPage === totalPages"
                class="px-3 py-1 text-sm border border-gray-300 rounded-md disabled:opacity-50 hover:bg-gray-50">
                Sau
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class PortfolioTradesComponent implements OnInit {
  portfolioId = '';
  trades: TradeResponseItem[] = [];
  isLoading = true;
  currentPage = 1;
  pageSize = 10;
  totalCount = 0;
  totalPages = 0;
  filters = { symbol: '', tradeType: '' };

  constructor(
    private route: ActivatedRoute,
    private portfolioService: PortfolioService,
    private tradeService: TradeService,
    private notificationService: NotificationService
  ) {}

  // Trade type utility functions
  getTradeTypeDisplay = getTradeTypeDisplay;
  getTradeTypeClass = getTradeTypeClass;
  TRADE_TYPE_FILTER_OPTIONS = TRADE_TYPE_FILTER_OPTIONS;

  ngOnInit(): void {
    this.portfolioId = this.route.snapshot.paramMap.get('id') || '';
    this.loadTrades();
  }

  loadTrades(): void {
    this.isLoading = true;
    this.portfolioService.getTrades(this.portfolioId, {
      symbol: this.filters.symbol || undefined,
      tradeType: this.filters.tradeType || undefined,
      page: this.currentPage,
      pageSize: this.pageSize
    }).subscribe({
      next: (data) => {
        this.trades = data.items;
        this.totalCount = data.totalCount;
        this.totalPages = data.totalPages;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notificationService.error('Lỗi', 'Không thể tải giao dịch');
      }
    });
  }

  changePage(page: number): void {
    this.currentPage = page;
    this.loadTrades();
  }

  resetFilters(): void {
    this.filters = { symbol: '', tradeType: '' };
    this.currentPage = 1;
    this.loadTrades();
  }

  deleteTrade(id: string): void {
    if (!confirm('Bạn có chắc chắn muốn xóa giao dịch này?')) return;
    this.tradeService.delete(id).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Giao dịch đã được xóa');
        this.loadTrades();
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không thể xóa giao dịch');
      }
    });
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('vi-VN');
  }
}