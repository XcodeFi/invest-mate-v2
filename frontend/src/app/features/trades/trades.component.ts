import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PortfolioService, PortfolioSummary, TradeResponseItem } from '../../core/services/portfolio.service';
import { TradeService } from '../../core/services/trade.service';
import { NotificationService } from '../../core/services/notification.service';
import { TradeType, getTradeTypeDisplay, getTradeTypeClass, TRADE_TYPE_FILTER_OPTIONS } from '../../shared/constants/trade-types';

@Component({
  selector: 'app-trades',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex justify-between items-center py-6">
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Lịch sử Giao dịch</h1>
              <p class="text-gray-600 mt-1">Xem và quản lý tất cả giao dịch của bạn</p>
            </div>
            <div class="flex space-x-3">
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
              <input
                type="text"
                placeholder="VD: AAPL, VNM..."
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                [(ngModel)]="filters.symbol"
                (input)="applyFilters()"
              />
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

        <!-- Trades Table -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
          <div class="overflow-x-auto">
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
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Thao tác</th>
                </tr>
              </thead>
              <tbody class="bg-white divide-y divide-gray-200">
                <tr *ngFor="let trade of filteredTrades" class="hover:bg-gray-50">
                  <td class="px-6 py-4 whitespace-nowrap">
                    <div class="text-sm font-medium text-gray-900">{{ trade.symbol }}</div>
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
                    {{ formatCurrency(trade.price) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ formatCurrency(trade.totalValue) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ formatCurrency(trade.fee) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ formatCurrency(trade.tax) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ formatDate(trade.tradeDate) }}
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
  `,
  styles: []
})
export class TradesComponent implements OnInit {
  allTrades: TradeResponseItem[] = [];
  filteredTrades: TradeResponseItem[] = [];
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

  constructor(
    private portfolioService: PortfolioService,
    private tradeService: TradeService,
    private notificationService: NotificationService
  ) {}

  // Trade type utility functions
  getTradeTypeDisplay = getTradeTypeDisplay;
  getTradeTypeClass = getTradeTypeClass;
  TRADE_TYPE_FILTER_OPTIONS = TRADE_TYPE_FILTER_OPTIONS;

  ngOnInit(): void {
    this.loadTrades();
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

  applyFilters(): void {
    let filtered = this.allTrades.filter(trade => {
      const matchesSymbol = !this.filters.symbol || trade.symbol.toLowerCase().includes(this.filters.symbol.toLowerCase());
      const matchesType = !this.filters.type || trade.tradeType === this.filters.type;
      const matchesFromDate = !this.filters.fromDate || new Date(trade.tradeDate) >= new Date(this.filters.fromDate);
      const matchesToDate = !this.filters.toDate || new Date(trade.tradeDate) <= new Date(this.filters.toDate + 'T23:59:59');
      return matchesSymbol && matchesType && matchesFromDate && matchesToDate;
    });

    this.totalTrades = filtered.length;
    this.totalPages = Math.ceil(this.totalTrades / this.pageSize);
    this.currentPage = 1;

    const startIndex = (this.currentPage - 1) * this.pageSize;
    this.filteredTrades = filtered.slice(startIndex, startIndex + this.pageSize);
  }

  previousPage(): void {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.applyFilters();
    }
  }

  nextPage(): void {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.applyFilters();
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

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND'
    }).format(amount);
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

  min(a: number, b: number): number {
    return Math.min(a, b);
  }
}