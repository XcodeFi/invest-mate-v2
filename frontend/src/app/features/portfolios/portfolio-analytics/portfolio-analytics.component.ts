import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { PnlService, PortfolioPnL } from '../../../core/services/pnl.service';
import { NotificationService } from '../../../core/services/notification.service';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';

@Component({
  selector: 'app-portfolio-analytics',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex items-center py-6">
            <button [routerLink]="['/portfolios', portfolioId]" class="mr-4 text-gray-500 hover:text-gray-700">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
              </svg>
            </button>
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Phân tích Danh mục</h1>
              <p class="text-gray-600 mt-1">{{ pnl?.portfolioName || 'Đang tải...' }}</p>
            </div>
          </div>
        </div>
      </div>

      <!-- Loading -->
      <div *ngIf="isLoading" class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="text-center py-12">
          <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
        </div>
      </div>

      <!-- Content -->
      <div *ngIf="!isLoading && pnl" class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Summary Cards -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <p class="text-sm font-medium text-gray-600">Tổng đầu tư</p>
            <p class="text-2xl font-bold text-gray-900 mt-1">{{ pnl.totalInvested | vndCurrency }}</p>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <p class="text-sm font-medium text-gray-600">Giá trị thị trường</p>
            <p class="text-2xl font-bold text-gray-900 mt-1">{{ pnl.totalMarketValue | vndCurrency }}</p>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <p class="text-sm font-medium text-gray-600">Tổng P&L</p>
            <p class="text-2xl font-bold mt-1" [class]="pnl.totalPnL >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ pnl.totalPnL | vndCurrency }}
            </p>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <p class="text-sm font-medium text-gray-600">% P&L</p>
            <p class="text-2xl font-bold mt-1" [class]="(pnl.totalPnLPercent ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ (pnl.totalPnLPercent ?? 0).toFixed(2) }}%
            </p>
          </div>
        </div>

        <!-- P&L Breakdown -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-8">
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 class="text-lg font-semibold text-gray-900 mb-4">Phân tích Lãi/Lỗ</h3>
            <div class="space-y-4">
              <div class="flex justify-between items-center">
                <span class="text-gray-600">Lãi/Lỗ đã thực hiện</span>
                <span class="font-bold" [class]="pnl.totalRealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ pnl.totalRealizedPnL | vndCurrency }}
                </span>
              </div>
              <div class="flex justify-between items-center">
                <span class="text-gray-600">Lãi/Lỗ chưa thực hiện</span>
                <span class="font-bold" [class]="pnl.totalUnrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ pnl.totalUnrealizedPnL | vndCurrency }}
                </span>
              </div>
              <hr />
              <div class="flex justify-between items-center">
                <span class="text-gray-900 font-medium">Tổng P&L</span>
                <span class="text-xl font-bold" [class]="pnl.totalPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ pnl.totalPnL | vndCurrency }}
                </span>
              </div>
            </div>
          </div>

          <!-- Position Allocation -->
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 class="text-lg font-semibold text-gray-900 mb-4">Phân bổ theo cổ phiếu</h3>
            <div class="space-y-3" *ngIf="pnl.positions.length > 0">
              <div *ngFor="let pos of pnl.positions; let i = index" class="flex items-center justify-between">
                <div class="flex items-center">
                  <div class="w-4 h-4 rounded-full mr-3" [style.background-color]="getColor(i)"></div>
                  <span class="text-sm font-medium text-gray-900">{{ pos.symbol }}</span>
                </div>
                <div class="text-right">
                  <span class="text-sm font-medium text-gray-900">{{ getPositionPercent(pos.marketValue) }}%</span>
                  <p class="text-xs text-gray-500">{{ pos.marketValue | vndCurrency }}</p>
                </div>
              </div>
            </div>
            <p *ngIf="pnl.positions.length === 0" class="text-gray-500 text-center py-4">Chưa có vị thế nào</p>
          </div>
        </div>

        <!-- Positions Detail Table -->
        <div *ngIf="pnl.positions.length > 0" class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
          <div class="px-6 py-4 border-b border-gray-200">
            <h3 class="text-lg font-semibold text-gray-900">Chi tiết từng cổ phiếu</h3>
          </div>
          <div class="overflow-x-auto hidden md:block">
            <table class="min-w-full divide-y divide-gray-200">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mã CK</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">SL</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Giá TB</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Giá hiện tại</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Tổng chi phí</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Giá trị TT</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Unrealized</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Realized</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Total P&L</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200">
                <tr *ngFor="let pos of pnl.positions" class="hover:bg-gray-50">
                  <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ pos.symbol }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.quantity }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.averageCost | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.currentPrice | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.totalCost | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm text-gray-900">{{ pos.marketValue | vndCurrency }}</td>
                  <td class="px-6 py-4 text-sm" [class]="pos.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ pos.unrealizedPnL | vndCurrency }}
                  </td>
                  <td class="px-6 py-4 text-sm" [class]="pos.realizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ pos.realizedPnL | vndCurrency }}
                  </td>
                  <td class="px-6 py-4 text-sm font-bold" [class]="pos.totalPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ pos.totalPnL | vndCurrency }} ({{ (pos.totalPnLPercent ?? 0).toFixed(2) }}%)
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
                <span class="text-sm font-bold" [class]="pos.totalPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ pos.totalPnL | vndCurrency }} ({{ (pos.totalPnLPercent ?? 0).toFixed(2) }}%)
                </span>
              </div>
              <div class="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
                <div><span class="text-gray-500">Số lượng:</span> <span class="font-medium">{{ pos.quantity }}</span></div>
                <div><span class="text-gray-500">Giá TB:</span> <span class="font-medium">{{ pos.averageCost | vndCurrency }}</span></div>
                <div><span class="text-gray-500">Giá hiện tại:</span> <span class="font-medium">{{ pos.currentPrice | vndCurrency }}</span></div>
                <div><span class="text-gray-500">Giá trị TT:</span> <span class="font-medium">{{ pos.marketValue | vndCurrency }}</span></div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class PortfolioAnalyticsComponent implements OnInit {
  portfolioId = '';
  pnl: PortfolioPnL | null = null;
  isLoading = true;
  private colors = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6', '#EC4899', '#06B6D4', '#84CC16'];

  constructor(
    private route: ActivatedRoute,
    private pnlService: PnlService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.portfolioId = this.route.snapshot.paramMap.get('id') || '';
    this.loadPnL();
  }

  private loadPnL(): void {
    this.pnlService.getPortfolioPnL(this.portfolioId).subscribe({
      next: (data) => {
        this.pnl = data;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notificationService.error('Lỗi', 'Không thể tải dữ liệu phân tích');
      }
    });
  }

  getColor(index: number): string {
    return this.colors[index % this.colors.length];
  }

  getPositionPercent(marketValue: number): string {
    if (!this.pnl || !this.pnl.totalMarketValue) return '0.00';
    return ((marketValue / this.pnl.totalMarketValue) * 100).toFixed(2);
  }

}