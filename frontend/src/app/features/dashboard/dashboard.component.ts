import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, User } from '../../core/services/auth.service';
import { PnlService, OverallPnLSummary, PortfolioPnL, PositionPnL } from '../../core/services/pnl.service';
import { RiskService, PortfolioRiskSummary, PositionRiskItem } from '../../core/services/risk.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { forkJoin } from 'rxjs';

interface RiskAlert {
  symbol: string;
  portfolioName: string;
  type: 'stop-loss' | 'drawdown';
  message: string;
  severity: 'warning' | 'danger';
  value: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex justify-between items-center py-6">
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Investor Cockpit</h1>
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

        <!-- Row 1: Summary Cards -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <!-- Tổng Giá trị -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm font-medium text-gray-500">Tổng Giá trị</p>
              <div class="w-8 h-8 bg-emerald-100 rounded-lg flex items-center justify-center">
                <svg class="w-5 h-5 text-emerald-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6"></path>
                </svg>
              </div>
            </div>
            <p class="text-2xl font-bold text-gray-900">{{ pnlSummary.totalPortfolioValue | vndCurrency }}</p>
            <div class="mt-2 flex items-center text-sm" *ngIf="pnlSummary.totalInvested > 0">
              <span
                class="font-medium"
                [class.text-emerald-600]="totalChangePercent >= 0"
                [class.text-red-600]="totalChangePercent < 0"
              >
                {{ totalChangePercent >= 0 ? '+' : '' }}{{ totalChangePercent.toFixed(2) }}%
              </span>
              <span class="text-gray-400 ml-1">so với vốn</span>
            </div>
          </div>

          <!-- Đã Đầu tư -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm font-medium text-gray-500">Đã Đầu tư</p>
              <div class="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center">
                <svg class="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1"></path>
                </svg>
              </div>
            </div>
            <p class="text-2xl font-bold text-gray-900">{{ pnlSummary.totalInvested | vndCurrency }}</p>
            <div class="mt-2 text-sm text-gray-400">
              {{ portfolios.length }} danh mục
            </div>
          </div>

          <!-- Tổng Lãi/Lỗ -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm font-medium text-gray-500">Tổng Lãi/Lỗ</p>
              <div
                class="w-8 h-8 rounded-lg flex items-center justify-center"
                [class.bg-emerald-100]="totalPnL >= 0"
                [class.bg-red-100]="totalPnL < 0"
              >
                <svg
                  class="w-5 h-5"
                  [class.text-emerald-600]="totalPnL >= 0"
                  [class.text-red-600]="totalPnL < 0"
                  fill="none" stroke="currentColor" viewBox="0 0 24 24"
                >
                  <path *ngIf="totalPnL >= 0" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 10l7-7m0 0l7 7m-7-7v18"></path>
                  <path *ngIf="totalPnL < 0" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 14l-7 7m0 0l-7-7m7 7V3"></path>
                </svg>
              </div>
            </div>
            <p
              class="text-2xl font-bold"
              [class.text-emerald-600]="totalPnL >= 0"
              [class.text-red-600]="totalPnL < 0"
            >
              {{ totalPnL | vndCurrency }}
            </p>
            <div class="mt-2 text-sm text-gray-400">
              Thực hiện: {{ pnlSummary.totalRealizedPnL | vndCurrency }}
            </div>
          </div>

          <!-- CAGR -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div class="flex items-center justify-between mb-2">
              <p class="text-sm font-medium text-gray-500">CAGR</p>
              <div class="w-8 h-8 bg-violet-100 rounded-lg flex items-center justify-center">
                <svg class="w-5 h-5 text-violet-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"></path>
                </svg>
              </div>
            </div>
            <p class="text-2xl font-bold text-gray-900">--</p>
            <div class="mt-2 text-sm text-gray-400">
              Chưa đủ dữ liệu
            </div>
          </div>
        </div>

        <!-- Row 2: Allocation + Risk Alerts -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
          <!-- Phân bổ Danh mục -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Phân bổ Danh mục</h2>
            <div *ngIf="portfolios.length === 0" class="text-center py-8 text-gray-400">
              Chưa có danh mục nào
            </div>
            <div *ngIf="portfolios.length > 0" class="space-y-4">
              <div *ngFor="let p of portfolios; let i = index">
                <div class="flex items-center justify-between mb-1">
                  <span class="text-sm font-medium text-gray-700 truncate mr-2">{{ p.portfolioName }}</span>
                  <span class="text-sm text-gray-500 whitespace-nowrap">{{ getAllocationPercent(p).toFixed(1) }}%</span>
                </div>
                <div class="w-full bg-gray-100 rounded-full h-3">
                  <div
                    class="h-3 rounded-full transition-all duration-500"
                    [style.width.%]="getAllocationPercent(p)"
                    [style.background-color]="allocationColors[i % allocationColors.length]"
                  ></div>
                </div>
                <div class="flex items-center justify-between mt-1">
                  <span class="text-xs text-gray-400">{{ safeNumber(p.totalMarketValue) | vndCurrency }}</span>
                  <span
                    class="text-xs font-medium"
                    [class.text-emerald-600]="safeNumber(p.totalPnL) >= 0"
                    [class.text-red-600]="safeNumber(p.totalPnL) < 0"
                  >
                    {{ safeNumber(p.totalPnL) >= 0 ? '+' : '' }}{{ safeNumber(p.totalPnLPercent).toFixed(2) }}%
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- Cảnh báo Rủi ro -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Cảnh báo Rủi ro</h2>
            <div *ngIf="riskAlerts.length === 0" class="flex flex-col items-center justify-center py-8 text-gray-400">
              <svg class="w-12 h-12 mb-3 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"></path>
              </svg>
              <p class="text-sm">Không có cảnh báo</p>
            </div>
            <div *ngIf="riskAlerts.length > 0" class="space-y-3">
              <div
                *ngFor="let alert of riskAlerts"
                class="flex items-start p-3 rounded-lg"
                [class.bg-red-50]="alert.severity === 'danger'"
                [class.border-red-200]="alert.severity === 'danger'"
                [class.bg-amber-50]="alert.severity === 'warning'"
                [class.border-amber-200]="alert.severity === 'warning'"
                [class.border]="true"
              >
                <svg
                  class="w-5 h-5 mt-0.5 flex-shrink-0"
                  [class.text-red-500]="alert.severity === 'danger'"
                  [class.text-amber-500]="alert.severity === 'warning'"
                  fill="none" stroke="currentColor" viewBox="0 0 24 24"
                >
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z"></path>
                </svg>
                <div class="ml-3">
                  <p class="text-sm font-semibold" [class.text-red-800]="alert.severity === 'danger'" [class.text-amber-800]="alert.severity === 'warning'">
                    {{ alert.symbol }} <span class="font-normal text-gray-500">- {{ alert.portfolioName }}</span>
                  </p>
                  <p class="text-sm mt-0.5" [class.text-red-700]="alert.severity === 'danger'" [class.text-amber-700]="alert.severity === 'warning'">
                    {{ alert.message }}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Row 3: Quick Actions -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-8">
          <h2 class="text-lg font-semibold text-gray-900 mb-4">Thao tác nhanh</h2>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
            <a
              routerLink="/trade-plan"
              class="flex flex-col items-center p-4 rounded-xl border-2 border-gray-100 hover:border-blue-200 hover:bg-blue-50 transition-all duration-200 group cursor-pointer"
            >
              <div class="w-12 h-12 bg-blue-100 group-hover:bg-blue-200 rounded-xl flex items-center justify-center mb-3 transition-colors">
                <svg class="w-6 h-6 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"></path>
                </svg>
              </div>
              <span class="text-sm font-medium text-gray-700 group-hover:text-blue-700">Lập kế hoạch GD</span>
            </a>

            <a
              routerLink="/market-data"
              class="flex flex-col items-center p-4 rounded-xl border-2 border-gray-100 hover:border-emerald-200 hover:bg-emerald-50 transition-all duration-200 group cursor-pointer"
            >
              <div class="w-12 h-12 bg-emerald-100 group-hover:bg-emerald-200 rounded-xl flex items-center justify-center mb-3 transition-colors">
                <svg class="w-6 h-6 text-emerald-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z"></path>
                </svg>
              </div>
              <span class="text-sm font-medium text-gray-700 group-hover:text-emerald-700">Xem Thị trường</span>
            </a>

            <a
              routerLink="/journals"
              class="flex flex-col items-center p-4 rounded-xl border-2 border-gray-100 hover:border-violet-200 hover:bg-violet-50 transition-all duration-200 group cursor-pointer"
            >
              <div class="w-12 h-12 bg-violet-100 group-hover:bg-violet-200 rounded-xl flex items-center justify-center mb-3 transition-colors">
                <svg class="w-6 h-6 text-violet-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"></path>
                </svg>
              </div>
              <span class="text-sm font-medium text-gray-700 group-hover:text-violet-700">Ghi Nhật ký</span>
            </a>

            <a
              routerLink="/risk-dashboard"
              class="flex flex-col items-center p-4 rounded-xl border-2 border-gray-100 hover:border-amber-200 hover:bg-amber-50 transition-all duration-200 group cursor-pointer"
            >
              <div class="w-12 h-12 bg-amber-100 group-hover:bg-amber-200 rounded-xl flex items-center justify-center mb-3 transition-colors">
                <svg class="w-6 h-6 text-amber-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"></path>
                </svg>
              </div>
              <span class="text-sm font-medium text-gray-700 group-hover:text-amber-700">Quản lý Rủi ro</span>
            </a>
          </div>
        </div>

        <!-- Row 4: Portfolio List -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-200">
          <div class="px-6 py-4 border-b border-gray-200">
            <h2 class="text-lg font-semibold text-gray-900">Danh mục của bạn</h2>
          </div>
          <div class="divide-y divide-gray-200">
            <div *ngFor="let portfolio of portfolios" class="px-6 py-5 hover:bg-gray-50 transition-colors duration-200">
              <div class="flex items-center justify-between">
                <div class="flex-1 min-w-0">
                  <h3 class="text-lg font-medium text-gray-900">{{ portfolio.portfolioName }}</h3>
                  <p class="text-sm text-gray-500 mt-0.5">Vốn ban đầu: {{ portfolio.initialCapital | vndCurrency }}</p>
                </div>
                <div class="text-right mx-4">
                  <p class="text-xl font-bold text-gray-900">{{ safeNumber(portfolio.totalMarketValue) | vndCurrency }}</p>
                  <p
                    class="text-sm font-medium"
                    [class.text-emerald-600]="safeNumber(portfolio.totalPnL) >= 0"
                    [class.text-red-600]="safeNumber(portfolio.totalPnL) < 0"
                  >
                    {{ safeNumber(portfolio.totalPnL) >= 0 ? '+' : '' }}{{ safeNumber(portfolio.totalPnL) | vndCurrency }}
                    ({{ safeNumber(portfolio.totalPnLPercent).toFixed(2) }}%)
                  </p>
                </div>
                <div class="ml-4">
                  <button
                    [routerLink]="['/portfolios', portfolio.portfolioId]"
                    class="bg-gray-100 hover:bg-gray-200 text-gray-700 px-4 py-2 rounded-lg font-medium transition-colors duration-200 text-sm"
                  >
                    Xem chi tiết
                  </button>
                </div>
              </div>
              <!-- Performance progress bar -->
              <div class="mt-3" *ngIf="portfolio.initialCapital > 0">
                <div class="flex items-center justify-between text-xs text-gray-400 mb-1">
                  <span>Hiệu suất so với vốn</span>
                  <span>{{ getPerformancePercent(portfolio).toFixed(1) }}%</span>
                </div>
                <div class="w-full bg-gray-100 rounded-full h-2">
                  <div
                    class="h-2 rounded-full transition-all duration-500"
                    [style.width.%]="getClampedPerformance(portfolio)"
                    [class.bg-emerald-500]="safeNumber(portfolio.totalMarketValue) >= portfolio.initialCapital"
                    [class.bg-red-400]="safeNumber(portfolio.totalMarketValue) < portfolio.initialCapital"
                  ></div>
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
  riskAlerts: RiskAlert[] = [];

  allocationColors = [
    '#3b82f6', '#10b981', '#8b5cf6', '#f59e0b',
    '#ef4444', '#06b6d4', '#ec4899', '#14b8a6'
  ];

  get pnlSummary() {
    return {
      totalRealizedPnL: this.safeNumber(this.summary?.totalRealizedPnL),
      totalUnrealizedPnL: this.safeNumber(this.summary?.totalUnrealizedPnL),
      totalPortfolioValue: this.safeNumber(this.summary?.totalMarketValue),
      totalInvested: this.safeNumber(this.summary?.totalInvested)
    };
  }

  get totalPnL(): number {
    return this.pnlSummary.totalRealizedPnL + this.pnlSummary.totalUnrealizedPnL;
  }

  get totalChangePercent(): number {
    const invested = this.pnlSummary.totalInvested;
    if (invested === 0) return 0;
    return ((this.pnlSummary.totalPortfolioValue - invested) / invested) * 100;
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
    private riskService: RiskService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.authService.getCurrentUser().subscribe(user => {
      this.currentUser = user;
    });
    this.loadDashboardData();
  }

  getAllocationPercent(portfolio: PortfolioPnL): number {
    const totalValue = this.pnlSummary.totalPortfolioValue;
    if (totalValue === 0) return 0;
    return (this.safeNumber(portfolio.totalMarketValue) / totalValue) * 100;
  }

  getPerformancePercent(portfolio: PortfolioPnL): number {
    if (portfolio.initialCapital === 0) return 0;
    return (this.safeNumber(portfolio.totalMarketValue) / portfolio.initialCapital) * 100;
  }

  getClampedPerformance(portfolio: PortfolioPnL): number {
    const perf = this.getPerformancePercent(portfolio);
    return Math.min(Math.max(perf, 0), 100);
  }

  private loadDashboardData(): void {
    this.isLoading = true;
    this.pnlService.getSummary().subscribe({
      next: (data) => {
        this.summary = data;
        this.isLoading = false;
        this.loadRiskAlerts(data);
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  private loadRiskAlerts(summary: OverallPnLSummary): void {
    if (!summary.portfolios || summary.portfolios.length === 0) return;

    const riskRequests = summary.portfolios.map(p =>
      this.riskService.getPortfolioRiskSummary(p.portfolioId)
    );

    forkJoin(riskRequests).subscribe({
      next: (riskSummaries) => {
        const alerts: RiskAlert[] = [];

        riskSummaries.forEach((risk, index) => {
          const portfolio = summary.portfolios[index];

          risk.positions.forEach((pos: PositionRiskItem) => {
            if (pos.stopLossPrice != null && pos.distanceToStopLossPercent <= 5) {
              alerts.push({
                symbol: pos.symbol,
                portfolioName: portfolio.portfolioName,
                type: 'stop-loss',
                message: `Cách stop-loss ${pos.distanceToStopLossPercent.toFixed(1)}% (${pos.stopLossPrice.toLocaleString('vi-VN')} VND)`,
                severity: pos.distanceToStopLossPercent <= 2 ? 'danger' : 'warning',
                value: pos.distanceToStopLossPercent
              });
            }
          });

          if (risk.maxDrawdown > 10) {
            alerts.push({
              symbol: portfolio.portfolioName,
              portfolioName: portfolio.portfolioName,
              type: 'drawdown',
              message: `Drawdown hiện tại: ${risk.maxDrawdown.toFixed(1)}%`,
              severity: risk.maxDrawdown > 20 ? 'danger' : 'warning',
              value: risk.maxDrawdown
            });
          }
        });

        alerts.sort((a, b) => a.value - b.value);
        this.riskAlerts = alerts.slice(0, 5);
      },
      error: () => {
        // Risk data unavailable — leave alerts empty
      }
    });
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
