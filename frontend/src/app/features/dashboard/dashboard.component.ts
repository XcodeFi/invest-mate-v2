import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, User } from '../../core/services/auth.service';
import { PnlService, OverallPnLSummary, PortfolioPnL } from '../../core/services/pnl.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex justify-between items-center py-6">
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Tổng quan Danh mục</h1>
              <p class="text-gray-600 mt-1">Chào mừng, {{ currentUser?.name }}</p>
            </div>
            <div class="flex space-x-3">
              <button
                routerLink="/portfolios/create"
                class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200"
              >
                + Tạo Danh mục mới
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content -->
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- P&L Summary Cards -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div class="flex items-center">
              <div class="flex-shrink-0">
                <div class="w-8 h-8 bg-green-100 rounded-lg flex items-center justify-center">
                  <svg class="w-5 h-5 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6"></path>
                  </svg>
                </div>
              </div>
              <div class="ml-4">
                <p class="text-sm font-medium text-gray-600">Tổng Giá trị</p>
                <p class="text-2xl font-bold text-gray-900">{{ formatCurrency(pnlSummary.totalPortfolioValue) }}</p>
              </div>
            </div>
          </div>

          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div class="flex items-center">
              <div class="flex-shrink-0">
                <div class="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center">
                  <svg class="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1"></path>
                  </svg>
                </div>
              </div>
              <div class="ml-4">
                <p class="text-sm font-medium text-gray-600">Đã Đầu tư</p>
                <p class="text-2xl font-bold text-gray-900">{{ formatCurrency(pnlSummary.totalInvested) }}</p>
              </div>
            </div>
          </div>

          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div class="flex items-center">
              <div class="flex-shrink-0">
                <div class="w-8 h-8 bg-yellow-100 rounded-lg flex items-center justify-center">
                  <svg class="w-5 h-5 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"></path>
                  </svg>
                </div>
              </div>
              <div class="ml-4">
                <p class="text-sm font-medium text-gray-600">Lãi/Lỗ Thực hiện</p>
                <p class="text-2xl font-bold" [class]="pnlSummary.totalRealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ formatCurrency(pnlSummary.totalRealizedPnL) }}
                </p>
              </div>
            </div>
          </div>

          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div class="flex items-center">
              <div class="flex-shrink-0">
                <div class="w-8 h-8 bg-purple-100 rounded-lg flex items-center justify-center">
                  <svg class="w-5 h-5 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"></path>
                  </svg>
                </div>
              </div>
              <div class="ml-4">
                <p class="text-sm font-medium text-gray-600">Lãi/Lỗ Chưa thực hiện</p>
                <p class="text-2xl font-bold" [class]="pnlSummary.totalUnrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ formatCurrency(pnlSummary.totalUnrealizedPnL) }}
                </p>
              </div>
            </div>
          </div>
        </div>

        <!-- Portfolio List -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200">
          <div class="px-6 py-4 border-b border-gray-200">
            <h2 class="text-lg font-semibold text-gray-900">Danh mục của bạn</h2>
          </div>
          <div class="divide-y divide-gray-200">
            <div *ngFor="let portfolio of portfolios" class="px-6 py-4 hover:bg-gray-50 transition-colors duration-200">
              <div class="flex items-center justify-between">
                <div class="flex-1">
                  <h3 class="text-lg font-medium text-gray-900">{{ portfolio.portfolioName }}</h3>
                  <p class="text-sm text-gray-600">Vốn: {{ formatCurrency(portfolio.initialCapital) }}</p>
                </div>
                <div class="text-right">
                  <p class="text-2xl font-bold text-gray-900">{{ formatCurrency(safeNumber(portfolio.totalMarketValue)) }}</p>
                  <p class="text-sm" [class]="safeNumber(portfolio.totalPnL) >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ formatCurrency(safeNumber(portfolio.totalPnL)) }} ({{ safeNumber(portfolio.totalPnLPercent).toFixed(2) }}%)
                  </p>
                </div>
                <div class="ml-6">
                  <button
                    [routerLink]="['/portfolios', portfolio.portfolioId]"
                    class="bg-gray-100 hover:bg-gray-200 text-gray-700 px-4 py-2 rounded-lg font-medium transition-colors duration-200"
                  >
                    Xem chi tiết
                  </button>
                </div>
              </div>
            </div>
            <div *ngIf="portfolios.length === 0" class="px-6 py-12 text-center">
              <svg class="mx-auto h-12 w-12 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"></path>
              </svg>
              <h3 class="mt-2 text-sm font-medium text-gray-900">Chưa có danh mục nào</h3>
              <p class="mt-1 text-sm text-gray-500">Bắt đầu bằng cách tạo danh mục đầu tư đầu tiên của bạn.</p>
              <div class="mt-6">
                <button
                  routerLink="/portfolios/create"
                  class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200"
                >
                  Tạo danh mục đầu tiên
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class DashboardComponent implements OnInit {
  currentUser: User | null = null;
  summary: OverallPnLSummary | null = null;
  isLoading = true;

  get pnlSummary() {
    return {
      totalRealizedPnL: this.safeNumber(this.summary?.totalRealizedPnL),
      totalUnrealizedPnL: this.safeNumber(this.summary?.totalUnrealizedPnL),
      totalPortfolioValue: this.safeNumber(this.summary?.totalMarketValue),
      totalInvested: this.safeNumber(this.summary?.totalInvested)
    };
  }

  safeNumber(value: number | undefined | null): number {
    return (value != null && isFinite(value)) ? value : 0;
  }

  get portfolios(): PortfolioPnL[] {
    return this.summary?.portfolios || [];
  }

  constructor(
    private authService: AuthService,
    private pnlService: PnlService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.authService.getCurrentUser().subscribe(user => {
      this.currentUser = user;
    });
    this.loadDashboardData();
  }

  private loadDashboardData(): void {
    this.isLoading = true;
    this.pnlService.getSummary().subscribe({
      next: (data) => {
        this.summary = data;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        // Don't show error on initial load if no portfolios exist yet
      }
    });
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
}