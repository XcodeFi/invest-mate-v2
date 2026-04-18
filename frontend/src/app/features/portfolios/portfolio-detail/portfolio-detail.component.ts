import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { PortfolioService, PortfolioDetail } from '../../../core/services/portfolio.service';
import { PnlService, PortfolioPnL } from '../../../core/services/pnl.service';
import { NotificationService } from '../../../core/services/notification.service';
import { getTradeTypeDisplay, getTradeTypeClass } from '../../../shared/constants/trade-types';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';
import { AiChatPanelComponent } from '../../../shared/components/ai-chat-panel/ai-chat-panel.component';

@Component({
  selector: 'app-portfolio-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe, AiChatPanelComponent],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 py-6">
            <div class="flex items-center">
              <button routerLink="/portfolios" class="mr-4 text-gray-500 hover:text-gray-700">
                <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
                </svg>
              </button>
              <div>
                <h1 class="text-3xl font-bold text-gray-900">{{ portfolio?.name || 'Đang tải...' }}</h1>
                <p class="text-gray-600 mt-1" *ngIf="portfolio">Tạo ngày: {{ formatDate(portfolio.createdAt) }}</p>
              </div>
            </div>
            <div class="flex space-x-3" *ngIf="portfolio">
              <button (click)="showAiPanel = true"
                class="bg-purple-600 hover:bg-purple-700 text-white text-sm font-medium rounded-lg px-3 py-1.5 transition-colors flex items-center gap-1">
                🤖 AI Đánh giá
              </button>
              <button [routerLink]="['/portfolios', portfolio.id, 'trades']" class="bg-gray-100 hover:bg-gray-200 text-gray-700 px-4 py-2 rounded-lg font-medium">
                Giao dịch
              </button>
              <button [routerLink]="['/portfolios', portfolio.id, 'edit']" class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium">
                Chỉnh sửa
              </button>
              <button (click)="deletePortfolio()" class="bg-red-600 hover:bg-red-700 text-white px-4 py-2 rounded-lg font-medium">
                Xóa
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Loading -->
      <div *ngIf="isLoading" class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="text-center py-12">
          <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p class="mt-4 text-gray-600">Đang tải dữ liệu...</p>
        </div>
      </div>

      <!-- Content -->
      <div *ngIf="!isLoading && portfolio" class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Summary Cards -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <p class="text-sm font-medium text-gray-600">Vốn hiện tại</p>
            <p class="text-2xl font-bold text-gray-900 mt-1">{{ portfolio.currentCapital | vndCurrency }}</p>
            <p class="text-xs text-gray-500 mt-1">Vốn ban đầu: {{ portfolio.initialCapital | vndCurrency }}</p>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <p class="text-sm font-medium text-gray-600">Giá trị thị trường</p>
            <p class="text-2xl font-bold text-gray-900 mt-1">{{ (pnl?.totalMarketValue || 0) | vndCurrency }}</p>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <p class="text-sm font-medium text-gray-600">Lãi/Lỗ chưa thực hiện</p>
            <p class="text-2xl font-bold mt-1" [class]="(pnl?.totalUnrealizedPnL || 0) >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ (pnl?.totalUnrealizedPnL || 0) | vndCurrency }}
            </p>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <p class="text-sm font-medium text-gray-600">Lãi/Lỗ đã thực hiện</p>
            <p class="text-2xl font-bold mt-1" [class]="(pnl?.totalRealizedPnL || 0) >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ (pnl?.totalRealizedPnL || 0) | vndCurrency }}
            </p>
          </div>
        </div>

        <!-- Positions Table -->
        <div *ngIf="pnl && pnl.positions && pnl.positions.length > 0" class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden mb-8">
          <div class="px-6 py-4 border-b border-gray-200">
            <h2 class="text-lg font-semibold text-gray-900">Vị thế đang nắm giữ</h2>
          </div>
          <div class="overflow-x-auto hidden md:block">
            <table class="min-w-full divide-y divide-gray-200">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mã CK</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Số lượng</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Giá TB</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Giá hiện tại</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Giá trị</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Lãi/Lỗ</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">%</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200">
                <tr *ngFor="let pos of pnl.positions" class="hover:bg-gray-50">
                  <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ pos.symbol }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.quantity }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.averageCost | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.currentPrice | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.marketValue | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm" [class]="pos.totalPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ pos.totalPnL | vndCurrency }}
                  </td>
                  <td class="px-6 py-4 text-sm" [class]="(pos.totalPnLPercent ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ (pos.totalPnLPercent ?? 0).toFixed(2) }}%
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Mobile Cards -->
          <div class="md:hidden divide-y divide-gray-200">
            <div *ngFor="let pos of pnl.positions" class="p-4 space-y-2">
              <div class="flex items-center justify-between">
                <span class="font-bold text-gray-900">{{ pos.symbol }}</span>
                <span class="text-sm font-bold" [class]="(pos.totalPnLPercent ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ (pos.totalPnLPercent ?? 0).toFixed(2) }}%
                </span>
              </div>
              <div class="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
                <div><span class="text-gray-500">Số lượng:</span> <span class="font-medium">{{ pos.quantity }}</span></div>
                <div><span class="text-gray-500">Giá TB:</span> <span class="font-medium">{{ pos.averageCost | vndCurrency }}</span></div>
                <div><span class="text-gray-500">Giá hiện tại:</span> <span class="font-medium">{{ pos.currentPrice | vndCurrency }}</span></div>
                <div>
                  <span class="text-gray-500">Lãi/Lỗ:</span>
                  <span class="font-medium" [class]="pos.totalPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ pos.totalPnL | vndCurrency }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Recent Trades -->
        <div *ngIf="portfolio.trades && portfolio.trades.length > 0" class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
          <div class="px-6 py-4 border-b border-gray-200 flex justify-between items-center">
            <h2 class="text-lg font-semibold text-gray-900">Giao dịch gần đây</h2>
            <button [routerLink]="['/portfolios', portfolio.id, 'trades']" class="text-blue-600 hover:text-blue-800 text-sm font-medium">
              Xem tất cả →
            </button>
          </div>
          <div class="overflow-x-auto hidden md:block">
            <table class="min-w-full divide-y divide-gray-200">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mã CK</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Loại</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Số lượng</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Giá</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ngày</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200">
                <tr *ngFor="let trade of portfolio.trades.slice(0, 5)" class="hover:bg-gray-50">
                  <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ trade.symbol }}</td>
                  <td class="px-6 py-4">
                    <span class="inline-flex px-2 py-1 text-xs font-semibold rounded-full"
                      [class]="getTradeTypeClass(trade.tradeType)">
                      {{ getTradeTypeDisplay(trade.tradeType) }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ trade.quantity }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ trade.price | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ formatDate(trade.tradeDate) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Mobile Cards -->
          <div class="md:hidden divide-y divide-gray-200">
            <div *ngFor="let trade of portfolio.trades.slice(0, 5)" class="p-4 space-y-2">
              <div class="flex items-center justify-between">
                <span class="font-bold text-gray-900">{{ trade.symbol }}</span>
                <span class="inline-flex px-2 py-1 text-xs font-semibold rounded-full"
                  [class]="getTradeTypeClass(trade.tradeType)">
                  {{ getTradeTypeDisplay(trade.tradeType) }}
                </span>
              </div>
              <div class="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
                <div><span class="text-gray-500">Số lượng:</span> <span class="font-medium">{{ trade.quantity }}</span></div>
                <div><span class="text-gray-500">Giá:</span> <span class="font-medium">{{ trade.price | vndCurrency }}</span></div>
                <div><span class="text-gray-500">Ngày:</span> <span class="font-medium">{{ formatDate(trade.tradeDate) }}</span></div>
              </div>
            </div>
          </div>
        </div>

        <!-- No trades -->
        <div *ngIf="!portfolio.trades || portfolio.trades.length === 0" class="bg-white rounded-lg shadow-sm border border-gray-200 p-12 text-center">
          <svg class="mx-auto h-12 w-12 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"></path>
          </svg>
          <h3 class="mt-2 text-sm font-medium text-gray-900">Chưa có giao dịch</h3>
          <p class="mt-1 text-sm text-gray-500">Thêm giao dịch đầu tiên để bắt đầu theo dõi danh mục.</p>
          <button routerLink="/trades/create" class="mt-4 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium">
            Thêm giao dịch
          </button>
        </div>
      </div>
    </div>
    <app-ai-chat-panel [(isOpen)]="showAiPanel" title="AI Đánh giá Danh mục" useCase="portfolio-review" [contextData]="{ portfolioId: portfolioId }"></app-ai-chat-panel>
  `,
  styles: []
})
export class PortfolioDetailComponent implements OnInit {
  portfolio: PortfolioDetail | null = null;
  pnl: PortfolioPnL | null = null;
  isLoading = true;
  showAiPanel = false;
  portfolioId = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private portfolioService: PortfolioService,
    private pnlService: PnlService,
    private notificationService: NotificationService
  ) {}

  // Trade type utility functions
  getTradeTypeDisplay = getTradeTypeDisplay;
  getTradeTypeClass = getTradeTypeClass;

  ngOnInit(): void {
    this.portfolioId = this.route.snapshot.paramMap.get('id') || '';
    this.loadPortfolio();
  }

  private loadPortfolio(): void {
    this.isLoading = true;
    this.portfolioService.getById(this.portfolioId).subscribe({
      next: (data) => {
        this.portfolio = data;
        this.loadPnL();
      },
      error: (err) => {
        this.isLoading = false;
        this.notificationService.error('Lỗi', 'Không thể tải dữ liệu danh mục');
      }
    });
  }

  private loadPnL(): void {
    this.pnlService.getPortfolioPnL(this.portfolioId).subscribe({
      next: (data) => {
        this.pnl = data;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  deletePortfolio(): void {
    if (!confirm('Bạn có chắc chắn muốn xóa danh mục này?')) return;
    this.portfolioService.delete(this.portfolioId).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Danh mục đã được xóa');
        this.router.navigate(['/portfolios']);
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không thể xóa danh mục');
      }
    });
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('vi-VN');
  }
}