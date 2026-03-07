import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PnlService, OverallPnLSummary, PositionPnL } from '../../core/services/pnl.service';
import { AnalyticsService, PerformanceSummary, PortfolioRiskSummary } from '../../core/services/analytics.service';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex justify-between items-center py-6">
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Phân tích Đầu tư</h1>
              <p class="text-gray-600 mt-1">Phân tích hiệu suất và phân bổ danh mục</p>
            </div>
            <div class="flex space-x-3" *ngIf="portfolioIds.length > 1">
              <select class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                (change)="onPortfolioChange($event)">
                <option value="">Tất cả danh mục</option>
                <option *ngFor="let p of portfolioOptions" [value]="p.id">{{ p.name }}</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content -->
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Performance Overview -->
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
                <p class="text-sm font-medium text-gray-600">Tổng lợi nhuận</p>
                <p class="text-2xl font-bold" [class]="totalPnLPercent >= 0 ? 'text-green-600' : 'text-red-600'">{{ totalPnLPercent >= 0 ? '+' : '' }}{{ totalPnLPercent.toFixed(1) }}%</p>
                <p class="text-sm text-gray-600">{{ formatCurrency(totalPnL) }}</p>
              </div>
            </div>
          </div>

          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div class="flex items-center">
              <div class="flex-shrink-0">
                <div class="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center">
                  <svg class="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"></path>
                  </svg>
                </div>
              </div>
              <div class="ml-4">
                <p class="text-sm font-medium text-gray-600">Sharpe Ratio</p>
                <p class="text-2xl font-bold text-gray-900">{{ performanceData ? performanceData.sharpeRatio.toFixed(2) : '--' }}</p>
                <p class="text-sm text-gray-600">Hiệu suất rủi ro</p>
              </div>
            </div>
          </div>

          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <div class="flex items-center">
              <div class="flex-shrink-0">
                <div class="w-8 h-8 bg-yellow-100 rounded-lg flex items-center justify-center">
                  <svg class="w-5 h-5 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1"></path>
                  </svg>
                </div>
              </div>
              <div class="ml-4">
                <p class="text-sm font-medium text-gray-600">Sortino Ratio</p>
                <p class="text-2xl font-bold text-gray-900">{{ performanceData ? performanceData.sortinoRatio.toFixed(2) : '--' }}</p>
                <p class="text-sm text-gray-600">Rủi ro giảm giá</p>
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
                <p class="text-sm font-medium text-gray-600">Max Drawdown</p>
                <p class="text-2xl font-bold text-red-600">{{ performanceData ? performanceData.maxDrawdown.toFixed(1) + '%' : '--' }}</p>
                <p class="text-sm text-gray-600">Mất mát tối đa</p>
              </div>
            </div>
          </div>
        </div>

        <!-- Charts Row -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-8">
          <!-- Performance Chart -->
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 class="text-lg font-semibold text-gray-900 mb-4">Biểu đồ hiệu suất danh mục</h3>
            <div class="h-64 flex items-center justify-center bg-gray-50 rounded-lg">
              <div class="text-center">
                <svg class="mx-auto h-12 w-12 text-gray-400 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"></path>
                </svg>
                <p class="text-gray-500">Biểu đồ hiệu suất sẽ hiển thị ở đây</p>
                <p class="text-sm text-gray-400 mt-1">Tích hợp với Chart.js hoặc D3.js</p>
              </div>
            </div>
          </div>

          <!-- Position Allocation -->
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 class="text-lg font-semibold text-gray-900 mb-4">Phân bổ theo cổ phiếu</h3>
            <div class="space-y-4">
              <div *ngFor="let holding of topHoldings; let i = index" class="flex items-center justify-between">
                <div class="flex items-center">
                  <div class="w-4 h-4 rounded-full mr-3" [style.background-color]="getPositionColor(i)"></div>
                  <span class="text-sm font-medium text-gray-900">{{ holding.symbol }}</span>
                </div>
                <div class="text-right">
                  <span class="text-sm font-medium text-gray-900">{{ getHoldingPercent(holding.marketValue) }}%</span>
                  <p class="text-xs text-gray-500">{{ formatCurrency(holding.marketValue) }}</p>
                </div>
              </div>
              <div *ngIf="topHoldings.length === 0" class="text-center py-4 text-gray-500">
                Chưa có vị thế nào
              </div>
            </div>
          </div>
        </div>

        <!-- Top Holdings Table -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
          <div class="px-6 py-4 border-b border-gray-200">
            <h3 class="text-lg font-semibold text-gray-900">Cổ phiếu nắm giữ nhiều nhất</h3>
          </div>
          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-gray-200">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Mã CK</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Số lượng</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Giá trung bình</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Giá hiện tại</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Giá trị thị trường</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Lãi/Lỗ</th>
                  <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">% Lãi/Lỗ</th>
                </tr>
              </thead>
              <tbody class="bg-white divide-y divide-gray-200">
                <tr *ngFor="let holding of topHoldings" class="hover:bg-gray-50">
                  <td class="px-6 py-4 whitespace-nowrap">
                    <div class="text-sm font-medium text-gray-900">{{ holding.symbol }}</div>
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ holding.quantity }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ formatCurrency(holding.averageCost) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ formatCurrency(holding.currentPrice) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {{ formatCurrency(holding.marketValue) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm" [class]="(holding.totalPnL ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ formatCurrency(holding.totalPnL ?? 0) }}
                  </td>
                  <td class="px-6 py-4 whitespace-nowrap text-sm" [class]="(holding.totalPnLPercent ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'">
                    {{ (holding.totalPnLPercent ?? 0).toFixed(2) }}%
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- Risk Metrics -->
        <div class="mt-8 bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h3 class="text-lg font-semibold text-gray-900 mb-4">Chỉ số rủi ro</h3>
          <div class="grid grid-cols-1 md:grid-cols-4 gap-6">
            <div class="text-center">
              <div class="text-2xl font-bold text-gray-900">{{ performanceData ? performanceData.winRate.toFixed(1) + '%' : '--' }}</div>
              <div class="text-sm text-gray-600">Win Rate</div>
            </div>
            <div class="text-center">
              <div class="text-2xl font-bold text-gray-900">{{ performanceData ? performanceData.profitFactor.toFixed(2) : '--' }}</div>
              <div class="text-sm text-gray-600">Profit Factor</div>
            </div>
            <div class="text-center">
              <div class="text-2xl font-bold text-gray-900">{{ riskData ? riskData.valueAtRisk95.toFixed(1) + '%' : '--' }}</div>
              <div class="text-sm text-gray-600">Value at Risk (95%)</div>
            </div>
            <div class="text-center">
              <div class="text-2xl font-bold text-gray-900">{{ performanceData ? formatCurrency(performanceData.expectancy) : '--' }}</div>
              <div class="text-sm text-gray-600">Expectancy</div>
            </div>
          </div>
        </div>

        <!-- Trade Stats -->
        <div *ngIf="performanceData" class="mt-8 bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h3 class="text-lg font-semibold text-gray-900 mb-4">Thống kê giao dịch</h3>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-6">
            <div class="text-center">
              <div class="text-2xl font-bold text-gray-900">{{ performanceData.totalTrades }}</div>
              <div class="text-sm text-gray-600">Tổng GD</div>
            </div>
            <div class="text-center">
              <div class="text-2xl font-bold text-green-600">{{ performanceData.winningTrades }}</div>
              <div class="text-sm text-gray-600">Lãi</div>
            </div>
            <div class="text-center">
              <div class="text-2xl font-bold text-red-600">{{ performanceData.losingTrades }}</div>
              <div class="text-sm text-gray-600">Lỗ</div>
            </div>
            <div class="text-center">
              <div class="text-2xl font-bold text-gray-900">{{ performanceData.cagr.toFixed(1) }}%</div>
              <div class="text-sm text-gray-600">CAGR</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class AnalyticsComponent implements OnInit {
  summary: OverallPnLSummary | null = null;
  topHoldings: PositionPnL[] = [];
  performanceData: PerformanceSummary | null = null;
  riskData: PortfolioRiskSummary | null = null;
  isLoading = true;
  portfolioIds: string[] = [];
  portfolioOptions: { id: string; name: string }[] = [];
  selectedPortfolioId = '';

  constructor(
    private pnlService: PnlService,
    private analyticsService: AnalyticsService
  ) {}

  ngOnInit(): void {
    this.loadAnalyticsData();
  }

  private loadAnalyticsData(): void {
    this.isLoading = true;
    this.pnlService.getSummary().subscribe({
      next: (data) => {
        this.summary = data;
        this.topHoldings = data.portfolios
          .flatMap(p => p.positions ?? [])
          .filter(pos => pos && pos.symbol)
          .sort((a, b) => (b.marketValue ?? 0) - (a.marketValue ?? 0));

        this.portfolioIds = data.portfolios.map(p => p.portfolioId);
        this.portfolioOptions = data.portfolios.map(p => ({ id: p.portfolioId, name: p.portfolioName }));

        // Load performance metrics for the first portfolio (or selected)
        if (this.portfolioIds.length > 0) {
          const targetId = this.selectedPortfolioId || this.portfolioIds[0];
          this.loadMetrics(targetId);
        }
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  private loadMetrics(portfolioId: string): void {
    this.analyticsService.getPerformance(portfolioId).subscribe({
      next: (data) => this.performanceData = data,
      error: () => this.performanceData = null
    });

    this.analyticsService.getRiskSummary(portfolioId).subscribe({
      next: (data) => this.riskData = data,
      error: () => this.riskData = null
    });
  }

  onPortfolioChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.selectedPortfolioId = value;
    if (value) {
      this.loadMetrics(value);
      // Filter holdings for selected portfolio
      const portfolio = this.summary?.portfolios.find(p => p.portfolioId === value);
      this.topHoldings = (portfolio?.positions ?? [])
        .filter(pos => pos && pos.symbol)
        .sort((a, b) => (b.marketValue ?? 0) - (a.marketValue ?? 0));
    } else {
      // Show all
      this.topHoldings = (this.summary?.portfolios ?? [])
        .flatMap(p => p.positions ?? [])
        .filter(pos => pos && pos.symbol)
        .sort((a, b) => (b.marketValue ?? 0) - (a.marketValue ?? 0));
      if (this.portfolioIds.length > 0) {
        this.loadMetrics(this.portfolioIds[0]);
      }
    }
  }

  get totalPnLPercent(): number {
    return this.summary?.totalPnLPercent || 0;
  }

  get totalPnL(): number {
    return this.summary?.totalPnL || 0;
  }

  private positionColors = ['#3B82F6', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6', '#EC4899', '#06B6D4', '#84CC16'];

  getPositionColor(index: number): string {
    return this.positionColors[index % this.positionColors.length];
  }

  getHoldingPercent(marketValue: number): string {
    const total = this.summary?.totalMarketValue || 0;
    if (total === 0) return '0.00';
    return ((marketValue / total) * 100).toFixed(2);
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND'
    }).format(amount);
  }
}
