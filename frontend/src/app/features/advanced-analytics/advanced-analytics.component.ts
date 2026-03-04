import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  AdvancedAnalyticsService, PerformanceSummary, EquityCurveData, MonthlyReturnsData
} from '../../core/services/advanced-analytics.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-advanced-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Phân tích Nâng cao</h1>

      <!-- Portfolio Selector -->
      <div class="bg-white rounded-lg shadow p-4 mb-6">
        <div class="flex flex-wrap items-center gap-4">
          <label class="text-sm font-medium text-gray-700">Danh mục:</label>
          <select
            [(ngModel)]="selectedPortfolioId"
            (ngModelChange)="onPortfolioChange()"
            class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 min-w-[200px]">
            <option value="">-- Chọn danh mục --</option>
            <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
          </select>
        </div>
      </div>

      <div *ngIf="selectedPortfolioId && performance">
        <!-- Performance Metric Cards -->
        <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-6">
          <div class="bg-white rounded-lg shadow p-4 text-center">
            <div class="text-xs text-gray-500 uppercase tracking-wide">CAGR</div>
            <div class="text-xl font-bold mt-1" [class.text-green-600]="performance.cagr >= 0" [class.text-red-600]="performance.cagr < 0">
              {{ performance.cagr | number:'1.2-2' }}%
            </div>
          </div>
          <div class="bg-white rounded-lg shadow p-4 text-center">
            <div class="text-xs text-gray-500 uppercase tracking-wide">Sharpe</div>
            <div class="text-xl font-bold mt-1" [class.text-green-600]="performance.sharpeRatio >= 1" [class.text-yellow-600]="performance.sharpeRatio >= 0 && performance.sharpeRatio < 1" [class.text-red-600]="performance.sharpeRatio < 0">
              {{ performance.sharpeRatio | number:'1.2-2' }}
            </div>
          </div>
          <div class="bg-white rounded-lg shadow p-4 text-center">
            <div class="text-xs text-gray-500 uppercase tracking-wide">Sortino</div>
            <div class="text-xl font-bold mt-1" [class.text-green-600]="performance.sortinoRatio >= 1" [class.text-red-600]="performance.sortinoRatio < 1">
              {{ performance.sortinoRatio | number:'1.2-2' }}
            </div>
          </div>
          <div class="bg-white rounded-lg shadow p-4 text-center">
            <div class="text-xs text-gray-500 uppercase tracking-wide">Win Rate</div>
            <div class="text-xl font-bold mt-1" [class.text-green-600]="performance.winRate >= 50" [class.text-red-600]="performance.winRate < 50">
              {{ performance.winRate | number:'1.1-1' }}%
            </div>
          </div>
          <div class="bg-white rounded-lg shadow p-4 text-center">
            <div class="text-xs text-gray-500 uppercase tracking-wide">Profit Factor</div>
            <div class="text-xl font-bold mt-1" [class.text-green-600]="performance.profitFactor >= 1.5" [class.text-yellow-600]="performance.profitFactor >= 1 && performance.profitFactor < 1.5" [class.text-red-600]="performance.profitFactor < 1">
              {{ performance.profitFactor | number:'1.2-2' }}
            </div>
          </div>
          <div class="bg-white rounded-lg shadow p-4 text-center">
            <div class="text-xs text-gray-500 uppercase tracking-wide">Max DD</div>
            <div class="text-xl font-bold mt-1 text-red-600">
              {{ performance.maxDrawdown | number:'1.2-2' }}%
            </div>
          </div>
        </div>

        <!-- Tabs -->
        <div class="bg-white rounded-lg shadow mb-6">
          <div class="border-b border-gray-200">
            <nav class="flex space-x-4 px-4">
              <button *ngFor="let tab of tabs" (click)="activeTab = tab.key"
                [class.border-blue-500]="activeTab === tab.key"
                [class.text-blue-600]="activeTab === tab.key"
                [class.border-transparent]="activeTab !== tab.key"
                [class.text-gray-500]="activeTab !== tab.key"
                class="py-3 px-1 border-b-2 font-medium text-sm whitespace-nowrap">
                {{ tab.label }}
              </button>
            </nav>
          </div>

          <div class="p-6">
            <!-- Trade Statistics -->
            <div *ngIf="activeTab === 'trades'">
              <h3 class="text-lg font-semibold mb-4">Thống kê Giao dịch</h3>
              <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                <div class="bg-gray-50 rounded-lg p-4">
                  <div class="text-sm text-gray-500">Tổng giao dịch</div>
                  <div class="text-2xl font-bold text-gray-800">{{ performance.totalTrades }}</div>
                  <div class="flex justify-between mt-2 text-sm">
                    <span class="text-green-600">Thắng: {{ performance.winningTrades }}</span>
                    <span class="text-red-600">Thua: {{ performance.losingTrades }}</span>
                  </div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4">
                  <div class="text-sm text-gray-500">Expectancy</div>
                  <div class="text-2xl font-bold" [class.text-green-600]="performance.expectancy > 0" [class.text-red-600]="performance.expectancy <= 0">
                    {{ formatCurrency(performance.expectancy) }}
                  </div>
                  <div class="text-xs text-gray-500 mt-1">Kỳ vọng lợi nhuận trung bình / giao dịch</div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4">
                  <div class="text-sm text-gray-500">Total Return</div>
                  <div class="text-2xl font-bold" [class.text-green-600]="performance.totalReturn > 0" [class.text-red-600]="performance.totalReturn <= 0">
                    {{ performance.totalReturn | number:'1.2-2' }}%
                  </div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4">
                  <div class="text-sm text-gray-500">Trung bình thắng</div>
                  <div class="text-xl font-bold text-green-600">{{ formatCurrency(performance.averageWin) }}</div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4">
                  <div class="text-sm text-gray-500">Trung bình thua</div>
                  <div class="text-xl font-bold text-red-600">{{ formatCurrency(performance.averageLoss) }}</div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4">
                  <div class="text-sm text-gray-500">Gross P/L</div>
                  <div class="flex justify-between">
                    <span class="text-green-600 font-medium">+{{ formatCurrency(performance.grossProfit) }}</span>
                    <span class="text-red-600 font-medium">{{ formatCurrency(performance.grossLoss) }}</span>
                  </div>
                </div>
              </div>

              <!-- Win Rate Bar -->
              <div class="mt-6">
                <div class="text-sm font-medium text-gray-700 mb-2">Win Rate: {{ performance.winRate | number:'1.1-1' }}%</div>
                <div class="w-full bg-gray-200 rounded-full h-4">
                  <div class="bg-green-500 h-4 rounded-full transition-all duration-500"
                    [style.width.%]="performance.winRate"></div>
                </div>
              </div>
            </div>

            <!-- Equity Curve -->
            <div *ngIf="activeTab === 'equity'">
              <h3 class="text-lg font-semibold mb-4">Equity Curve</h3>
              <div *ngIf="equityCurve && equityCurve.points.length > 0">
                <div class="overflow-x-auto max-h-96 overflow-y-auto">
                  <table class="min-w-full divide-y divide-gray-200">
                    <thead class="bg-gray-50 sticky top-0">
                      <tr>
                        <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ngày</th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Giá trị DM</th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Lợi nhuận ngày</th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Lợi nhuận tích luỹ</th>
                      </tr>
                    </thead>
                    <tbody class="bg-white divide-y divide-gray-200">
                      <tr *ngFor="let point of equityCurve.points">
                        <td class="px-4 py-2 text-sm">{{ point.date | date:'dd/MM/yyyy' }}</td>
                        <td class="px-4 py-2 text-right text-sm font-medium">{{ formatCurrency(point.portfolioValue) }}</td>
                        <td class="px-4 py-2 text-right text-sm"
                          [class.text-green-600]="point.dailyReturn > 0"
                          [class.text-red-600]="point.dailyReturn < 0">
                          {{ point.dailyReturn > 0 ? '+' : '' }}{{ point.dailyReturn | number:'1.2-2' }}%
                        </td>
                        <td class="px-4 py-2 text-right text-sm font-medium"
                          [class.text-green-600]="point.cumulativeReturn > 0"
                          [class.text-red-600]="point.cumulativeReturn < 0">
                          {{ point.cumulativeReturn > 0 ? '+' : '' }}{{ point.cumulativeReturn | number:'1.2-2' }}%
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
              <div *ngIf="!equityCurve || equityCurve.points.length === 0" class="text-center py-8 text-gray-500">
                Chưa có dữ liệu equity curve. Hãy chụp snapshot hàng ngày để tạo dữ liệu.
              </div>
            </div>

            <!-- Monthly Returns -->
            <div *ngIf="activeTab === 'monthly'">
              <h3 class="text-lg font-semibold mb-4">Lợi nhuận theo Tháng</h3>
              <div *ngIf="monthlyReturns && monthlyReturns.returns.length > 0">
                <div class="overflow-x-auto">
                  <table class="min-w-full divide-y divide-gray-200">
                    <thead class="bg-gray-50">
                      <tr>
                        <th class="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase">Năm</th>
                        <th *ngFor="let m of months" class="px-3 py-3 text-center text-xs font-medium text-gray-500 uppercase">{{ m }}</th>
                      </tr>
                    </thead>
                    <tbody class="bg-white divide-y divide-gray-200">
                      <tr *ngFor="let year of monthlyReturns.years">
                        <td class="px-3 py-2 font-medium text-sm">{{ year }}</td>
                        <td *ngFor="let monthNum of monthNumbers" class="px-3 py-2 text-center text-sm">
                          <span *ngIf="getMonthlyReturn(year, monthNum) !== null"
                            class="px-2 py-1 rounded text-xs font-medium"
                            [class.bg-green-100]="(getMonthlyReturn(year, monthNum) || 0) > 0"
                            [class.text-green-700]="(getMonthlyReturn(year, monthNum) || 0) > 0"
                            [class.bg-red-100]="(getMonthlyReturn(year, monthNum) || 0) < 0"
                            [class.text-red-700]="(getMonthlyReturn(year, monthNum) || 0) < 0"
                            [class.bg-gray-100]="(getMonthlyReturn(year, monthNum) || 0) === 0"
                            [class.text-gray-700]="(getMonthlyReturn(year, monthNum) || 0) === 0">
                            {{ getMonthlyReturn(year, monthNum) | number:'1.1-1' }}%
                          </span>
                          <span *ngIf="getMonthlyReturn(year, monthNum) === null" class="text-gray-300">-</span>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
              <div *ngIf="!monthlyReturns || monthlyReturns.returns.length === 0" class="text-center py-8 text-gray-500">
                Chưa đủ dữ liệu. Cần ít nhất 2 tháng snapshot để hiển thị.
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div *ngIf="!selectedPortfolioId" class="bg-white rounded-lg shadow p-8 text-center text-gray-500">
        Chọn danh mục để xem phân tích nâng cao
      </div>
    </div>
  `
})
export class AdvancedAnalyticsComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  activeTab = 'trades';

  performance: PerformanceSummary | null = null;
  equityCurve: EquityCurveData | null = null;
  monthlyReturns: MonthlyReturnsData | null = null;

  tabs = [
    { key: 'trades', label: 'Thống kê GD' },
    { key: 'equity', label: 'Equity Curve' },
    { key: 'monthly', label: 'Theo tháng' }
  ];

  months = ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12'];
  monthNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

  constructor(
    private analyticsService: AdvancedAnalyticsService,
    private portfolioService: PortfolioService,
    private notification: NotificationService
  ) {}

  ngOnInit() {
    this.portfolioService.getAll().subscribe({
      next: (data) => {
        this.portfolios = data;
      },
      error: () => this.notification.error('Lỗi', 'Không thể tải danh sách danh mục')
    });
  }

  onPortfolioChange() {
    if (!this.selectedPortfolioId) return;
    this.loadData();
  }

  loadData() {
    const id = this.selectedPortfolioId;

    this.analyticsService.getPerformance(id).subscribe({
      next: (data) => this.performance = data,
      error: () => {
        this.performance = null;
        this.notification.error('Lỗi', 'Không thể tải dữ liệu performance');
      }
    });

    this.analyticsService.getEquityCurve(id).subscribe({
      next: (data) => this.equityCurve = data,
      error: () => this.equityCurve = null
    });

    this.analyticsService.getMonthlyReturns(id).subscribe({
      next: (data) => this.monthlyReturns = data,
      error: () => this.monthlyReturns = null
    });
  }

  getMonthlyReturn(year: number, month: number): number | null {
    if (!this.monthlyReturns) return null;
    const item = this.monthlyReturns.returns.find(r => r.year === year && r.month === month);
    return item ? item.returnPercent : null;
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value);
  }
}
