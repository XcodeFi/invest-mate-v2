import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);
import { PnlService, OverallPnLSummary, PositionPnL } from '../../core/services/pnl.service';
import { AnalyticsService, PerformanceSummary, PortfolioRiskSummary } from '../../core/services/analytics.service';
import {
  AdvancedAnalyticsService,
  PerformanceSummary as AdvPerformanceSummary,
  EquityCurveData,
  MonthlyReturnsData
} from '../../core/services/advanced-analytics.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, VndCurrencyPipe],
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
            <div class="flex space-x-3">
              <select class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                [(ngModel)]="selectedPortfolioId"
                (ngModelChange)="onPortfolioChangeNew()">
                <option value="">Tất cả danh mục</option>
                <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content -->
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Performance Overview Cards -->
        <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-8">
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4 text-center">
            <div class="text-xs text-gray-500 uppercase tracking-wide">Tổng lợi nhuận</div>
            <div class="text-xl font-bold mt-1" [class]="totalPnLPercent >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ totalPnLPercent >= 0 ? '+' : '' }}{{ totalPnLPercent.toFixed(1) }}%
            </div>
            <div class="text-xs text-gray-500 mt-1">{{ totalPnL | vndCurrency }}</div>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4 text-center">
            <div class="tooltip-trigger text-xs text-gray-500 uppercase tracking-wide">
              CAGR <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
              <div class="tooltip-box"><strong>CAGR — Tăng trưởng kép hàng năm:</strong> Mức lợi nhuận trung bình mỗi năm nếu danh mục tăng đều đặn. VD: CAGR 12% = vốn nhân đôi sau ~6 năm. &gt; 15%/năm là tốt trên TTCK VN.</div>
            </div>
            <div class="text-xl font-bold mt-1"
              [class.text-green-600]="advPerformance && advPerformance.cagr >= 0"
              [class.text-red-600]="advPerformance && advPerformance.cagr < 0">
              {{ advPerformance ? (advPerformance.cagr | number:'1.2-2') + '%' : '--' }}
            </div>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4 text-center">
            <div class="tooltip-trigger text-xs text-gray-500 uppercase tracking-wide">
              Sharpe <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
              <div class="tooltip-box"><strong>Sharpe Ratio:</strong> Lợi nhuận vượt trội ÷ độ biến động toàn phần. ≥ 1 = tốt; ≥ 2 = rất tốt; &lt; 0 = thua lãi suất phi rủi ro (tiết kiệm ngân hàng).</div>
            </div>
            <div class="text-xl font-bold mt-1"
              [class.text-green-600]="advPerformance && advPerformance.sharpeRatio >= 1"
              [class.text-yellow-600]="advPerformance && advPerformance.sharpeRatio >= 0 && advPerformance.sharpeRatio < 1"
              [class.text-red-600]="advPerformance && advPerformance.sharpeRatio < 0">
              {{ advPerformance ? (advPerformance.sharpeRatio | number:'1.2-2') : '--' }}
            </div>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4 text-center">
            <div class="tooltip-trigger text-xs text-gray-500 uppercase tracking-wide">
              Sortino <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
              <div class="tooltip-box"><strong>Sortino Ratio:</strong> Giống Sharpe nhưng chỉ đo rủi ro sụt giảm (downside), bỏ qua biến động tăng. Phản ánh chính xác hơn mức độ kiểm soát thua lỗ. ≥ 1.5 = tốt.</div>
            </div>
            <div class="text-xl font-bold mt-1"
              [class.text-green-600]="advPerformance && advPerformance.sortinoRatio >= 1"
              [class.text-red-600]="advPerformance && advPerformance.sortinoRatio < 1">
              {{ advPerformance ? (advPerformance.sortinoRatio | number:'1.2-2') : '--' }}
            </div>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4 text-center">
            <div class="tooltip-trigger text-xs text-gray-500 uppercase tracking-wide">
              Max Drawdown <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
              <div class="tooltip-box"><strong>Max Drawdown — Sụt giảm tối đa:</strong> Mức giảm vốn lớn nhất từ đỉnh xuống đáy trong lịch sử. VD: -20% = danh mục từng mất 20% so với đỉnh cao nhất trước đó.</div>
            </div>
            <div class="text-xl font-bold mt-1 text-red-600">
              {{ advPerformance ? (advPerformance.maxDrawdown | number:'1.2-2') + '%' : '--' }}
            </div>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4 text-center">
            <div class="tooltip-trigger text-xs text-gray-500 uppercase tracking-wide">
              Win Rate <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
              <div class="tooltip-box"><strong>Win Rate — Tỷ lệ thắng:</strong> % số lệnh có lãi trên tổng lệnh đã đóng. Win Rate 50% không có nghĩa hòa vốn — cần kết hợp với Profit Factor và R:R để đánh giá toàn diện.</div>
            </div>
            <div class="text-xl font-bold mt-1"
              [class.text-green-600]="advPerformance && advPerformance.winRate >= 50"
              [class.text-red-600]="advPerformance && advPerformance.winRate < 50">
              {{ advPerformance ? (advPerformance.winRate | number:'1.1-1') + '%' : '--' }}
            </div>
          </div>
        </div>

        <!-- Tabs -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 mb-8">
          <div class="border-b border-gray-200">
            <nav class="flex space-x-4 px-4">
              <button *ngFor="let tab of tabs" (click)="onTabChange(tab.key)"
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
            <!-- Tổng quan Tab -->
            <div *ngIf="activeTab === 'overview'">
              <!-- Charts Row -->
              <div class="grid grid-cols-1 lg:grid-cols-2 gap-8 mb-8">
                <!-- P&L Bar Chart -->
                <div class="bg-gray-50 rounded-lg p-6">
                  <h3 class="text-lg font-semibold text-gray-900 mb-4">Lãi/Lỗ theo cổ phiếu</h3>
                  <div class="h-64">
                    <canvas #pnlBarCanvas></canvas>
                  </div>
                  <div *ngIf="topHoldings.length === 0" class="h-64 flex items-center justify-center text-gray-400">
                    Chưa có dữ liệu
                  </div>
                </div>

                <!-- Pie Allocation Chart -->
                <div class="bg-gray-50 rounded-lg p-6">
                  <h3 class="text-lg font-semibold text-gray-900 mb-4">Phân bổ theo cổ phiếu</h3>
                  <div class="h-64" *ngIf="topHoldings.length > 0">
                    <canvas #pieCanvas></canvas>
                  </div>
                  <div *ngIf="topHoldings.length === 0" class="h-64 flex items-center justify-center text-gray-500">
                    Chưa có vị thế nào
                  </div>
                </div>
              </div>

              <!-- Top Holdings Table -->
              <div class="border border-gray-200 rounded-lg overflow-hidden">
                <div class="px-6 py-4 border-b border-gray-200 bg-gray-50">
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
                          {{ holding.averageCost | vndCurrency }}
                        </td>
                        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                          {{ holding.currentPrice | vndCurrency }}
                        </td>
                        <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                          {{ holding.marketValue | vndCurrency }}
                        </td>
                        <td class="px-6 py-4 whitespace-nowrap text-sm" [class]="(holding.totalPnL ?? 0) >= 0 ? 'text-green-600' : 'text-red-600'">
                          {{ (holding.totalPnL ?? 0) | vndCurrency }}
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
              <div class="mt-8 bg-gray-50 rounded-lg p-6">
                <h3 class="text-lg font-semibold text-gray-900 mb-4">Chỉ số rủi ro</h3>
                <div class="grid grid-cols-1 md:grid-cols-4 gap-6">
                  <div class="text-center">
                    <div class="text-2xl font-bold text-gray-900">{{ performanceData ? performanceData.winRate.toFixed(1) + '%' : '--' }}</div>
                    <div class="tooltip-trigger text-sm text-gray-600 mt-1">
                      Win Rate <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
                      <div class="tooltip-box"><strong>Win Rate — Tỷ lệ thắng:</strong> % số lệnh có lãi trên tổng lệnh đã đóng. Cần kết hợp với Profit Factor và R:R để đánh giá toàn diện.</div>
                    </div>
                  </div>
                  <div class="text-center">
                    <div class="text-2xl font-bold text-gray-900">{{ performanceData ? performanceData.profitFactor.toFixed(2) : '--' }}</div>
                    <div class="tooltip-trigger text-sm text-gray-600 mt-1">
                      Profit Factor <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
                      <div class="tooltip-box"><strong>Profit Factor:</strong> Tổng lãi gộp ÷ Tổng lỗ gộp. PF &gt; 1.5 = tốt; PF = 1 = hòa vốn; PF &lt; 1 = chiến lược lỗ ròng tổng thể.</div>
                    </div>
                  </div>
                  <div class="text-center">
                    <div class="text-2xl font-bold text-gray-900">{{ riskData ? riskData.valueAtRisk95.toFixed(1) + '%' : '--' }}</div>
                    <div class="tooltip-trigger text-sm text-gray-600 mt-1">
                      Value at Risk (95%) <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
                      <div class="tooltip-box"><strong>VaR 95%:</strong> Mức lỗ tối đa dự kiến trong 1 ngày với xác suất 95%. VD: VaR 2% = 95% khả năng không mất quá 2% tổng danh mục trong 1 ngày.</div>
                    </div>
                  </div>
                  <div class="text-center">
                    <div class="text-2xl font-bold text-gray-900">{{ performanceData ? (performanceData.expectancy | vndCurrency) : '--' }}</div>
                    <div class="tooltip-trigger text-sm text-gray-600 mt-1">
                      Expectancy <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
                      <div class="tooltip-box"><strong>Expectancy — Kỳ vọng lợi nhuận:</strong> Lãi/lỗ trung bình mỗi giao dịch = (WinRate × AvgWin) − (LossRate × AvgLoss). Dương = chiến lược sinh lời dài hạn.</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Trade Statistics Tab -->
            <div *ngIf="activeTab === 'trades'">
              <div *ngIf="advPerformance">
                <h3 class="text-lg font-semibold mb-4">Thống kê Giao dịch</h3>
                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                  <div class="bg-gray-50 rounded-lg p-4">
                    <div class="text-sm text-gray-500">Tổng giao dịch</div>
                    <div class="text-2xl font-bold text-gray-800">{{ advPerformance.totalTrades }}</div>
                    <div class="flex justify-between mt-2 text-sm">
                      <span class="text-green-600">Thắng: {{ advPerformance.winningTrades }}</span>
                      <span class="text-red-600">Thua: {{ advPerformance.losingTrades }}</span>
                    </div>
                  </div>
                  <div class="bg-gray-50 rounded-lg p-4">
                    <div class="text-sm text-gray-500">Expectancy</div>
                    <div class="text-2xl font-bold" [class.text-green-600]="advPerformance.expectancy > 0" [class.text-red-600]="advPerformance.expectancy <= 0">
                      {{ advPerformance.expectancy | vndCurrency }}
                    </div>
                    <div class="text-xs text-gray-500 mt-1">Kỳ vọng lợi nhuận trung bình / giao dịch</div>
                  </div>
                  <div class="bg-gray-50 rounded-lg p-4">
                    <div class="text-sm text-gray-500">Total Return</div>
                    <div class="text-2xl font-bold" [class.text-green-600]="advPerformance.totalReturn > 0" [class.text-red-600]="advPerformance.totalReturn <= 0">
                      {{ advPerformance.totalReturn | number:'1.2-2' }}%
                    </div>
                  </div>
                  <div class="bg-gray-50 rounded-lg p-4">
                    <div class="text-sm text-gray-500">Trung bình thắng</div>
                    <div class="text-xl font-bold text-green-600">{{ advPerformance.averageWin | vndCurrency }}</div>
                  </div>
                  <div class="bg-gray-50 rounded-lg p-4">
                    <div class="text-sm text-gray-500">Trung bình thua</div>
                    <div class="text-xl font-bold text-red-600">{{ advPerformance.averageLoss | vndCurrency }}</div>
                  </div>
                  <div class="bg-gray-50 rounded-lg p-4">
                    <div class="text-sm text-gray-500">Gross P/L</div>
                    <div class="flex justify-between">
                      <span class="text-green-600 font-medium">+{{ advPerformance.grossProfit | vndCurrency }}</span>
                      <span class="text-red-600 font-medium">{{ advPerformance.grossLoss | vndCurrency }}</span>
                    </div>
                  </div>
                </div>

                <!-- Win Rate Bar -->
                <div class="mt-6">
                  <div class="text-sm font-medium text-gray-700 mb-2">Win Rate: {{ advPerformance.winRate | number:'1.1-1' }}%</div>
                  <div class="w-full bg-gray-200 rounded-full h-4">
                    <div class="bg-green-500 h-4 rounded-full transition-all duration-500"
                      [style.width.%]="advPerformance.winRate"></div>
                  </div>
                </div>
              </div>
              <div *ngIf="!advPerformance && selectedPortfolioId" class="text-center py-8 text-gray-500">
                Đang tải dữ liệu thống kê...
              </div>
              <div *ngIf="!selectedPortfolioId" class="text-center py-8 text-gray-500">
                Chọn danh mục để xem thống kê giao dịch
              </div>
            </div>

            <!-- Equity Curve Tab -->
            <div *ngIf="activeTab === 'equity'">
              <div class="flex items-center gap-2 mb-4">
                <h3 class="text-lg font-semibold">Equity Curve</h3>
                <span class="tooltip-trigger text-gray-400 cursor-help">
                  <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4 text-gray-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
                  <div class="tooltip-box"><strong>Equity Curve — Đường cong vốn:</strong> Biểu đồ thể hiện sự thay đổi tổng giá trị danh mục theo thời gian. Đường đi lên đều = chiến lược ổn định; đường gấp khúc mạnh = rủi ro cao, drawdown lớn.</div>
                </span>
              </div>
              <div *ngIf="equityCurve && equityCurve.points.length > 0" class="mb-6">
                <div class="bg-gray-50 rounded-lg p-4 h-72">
                  <canvas #equityCurveCanvas></canvas>
                </div>
              </div>
              <div *ngIf="equityCurve && equityCurve.points.length > 0">
                <div class="overflow-x-auto max-h-96 overflow-y-auto">
                  <table class="min-w-full divide-y divide-gray-200">
                    <thead class="bg-gray-50 sticky top-0">
                      <tr>
                        <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ngày</th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Giá trị DM</th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                          <span class="tooltip-trigger normal-case font-medium text-gray-500">
                            Lợi nhuận ngày <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
                            <div class="tooltip-box"><strong>Daily Return — Lợi nhuận ngày:</strong> % thay đổi giá trị danh mục so với ngày hôm trước. Dương = danh mục tăng; âm = danh mục giảm trong ngày đó.</div>
                          </span>
                        </th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">
                          <span class="tooltip-trigger normal-case font-medium text-gray-500">
                            Lợi nhuận tích luỹ <svg xmlns="http://www.w3.org/2000/svg" class="w-3.5 h-3.5 text-blue-400 flex-shrink-0" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
                            <div class="tooltip-box"><strong>Cumulative Return — Lợi nhuận tích luỹ:</strong> Tổng % lợi nhuận kể từ ngày đầu tiên đến ngày đó. VD: +25% = danh mục đang lãi 25% so với vốn ban đầu.</div>
                          </span>
                        </th>
                      </tr>
                    </thead>
                    <tbody class="bg-white divide-y divide-gray-200">
                      <tr *ngFor="let point of equityCurve.points">
                        <td class="px-4 py-2 text-sm">{{ point.date | date:'dd/MM/yyyy' }}</td>
                        <td class="px-4 py-2 text-right text-sm font-medium">{{ point.portfolioValue | vndCurrency }}</td>
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
                <span *ngIf="selectedPortfolioId">Chưa có dữ liệu equity curve. Hãy chụp snapshot hàng ngày để tạo dữ liệu.</span>
                <span *ngIf="!selectedPortfolioId">Chọn danh mục để xem equity curve</span>
              </div>
            </div>

            <!-- Monthly Returns Tab -->
            <div *ngIf="activeTab === 'monthly'">
              <h3 class="text-lg font-semibold mb-4">Lợi nhuận theo Tháng</h3>
              <div *ngIf="monthlyReturns && monthlyReturns.returns.length > 0" class="mb-6">
                <div class="bg-gray-50 rounded-lg p-4 h-64">
                  <canvas #monthlyBarCanvas></canvas>
                </div>
              </div>
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
                <span *ngIf="selectedPortfolioId">Chưa đủ dữ liệu. Cần ít nhất 2 tháng snapshot để hiển thị.</span>
                <span *ngIf="!selectedPortfolioId">Chọn danh mục để xem lợi nhuận theo tháng</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class AnalyticsComponent implements OnInit, OnDestroy {
  @ViewChild('pnlBarCanvas') pnlBarCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('pieCanvas') pieCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('equityCurveCanvas') equityCurveCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('monthlyBarCanvas') monthlyBarCanvas!: ElementRef<HTMLCanvasElement>;

  private pnlBarChart: Chart | null = null;
  private pieChart: Chart | null = null;
  private equityCurveChart: Chart | null = null;
  private monthlyBarChart: Chart | null = null;

  summary: OverallPnLSummary | null = null;
  topHoldings: PositionPnL[] = [];
  performanceData: PerformanceSummary | null = null;
  riskData: PortfolioRiskSummary | null = null;
  isLoading = true;
  portfolioIds: string[] = [];
  portfolioOptions: { id: string; name: string }[] = [];
  selectedPortfolioId = '';

  // Advanced analytics properties
  portfolios: PortfolioSummary[] = [];
  advPerformance: AdvPerformanceSummary | null = null;
  equityCurve: EquityCurveData | null = null;
  monthlyReturns: MonthlyReturnsData | null = null;
  activeTab = 'overview';

  tabs = [
    { key: 'overview', label: 'Tổng quan' },
    { key: 'trades', label: 'Thống kê GD' },
    { key: 'equity', label: 'Equity Curve' },
    { key: 'monthly', label: 'Theo tháng' }
  ];

  months = ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12'];
  monthNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

  constructor(
    private pnlService: PnlService,
    private analyticsService: AnalyticsService,
    private advancedAnalyticsService: AdvancedAnalyticsService,
    private portfolioService: PortfolioService
  ) {}

  ngOnInit(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => {
        this.portfolios = data;
      }
    });
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
          this.loadAdvancedData(targetId);
        }
        this.isLoading = false;
        this.renderOverviewCharts();
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

  private loadAdvancedData(portfolioId: string): void {
    this.advancedAnalyticsService.getPerformance(portfolioId).subscribe({
      next: (data) => this.advPerformance = data,
      error: () => this.advPerformance = null
    });

    this.advancedAnalyticsService.getEquityCurve(portfolioId).subscribe({
      next: (data) => {
        this.equityCurve = data;
        if (this.activeTab === 'equity') setTimeout(() => this.renderEquityCurveChart());
      },
      error: () => this.equityCurve = null
    });

    this.advancedAnalyticsService.getMonthlyReturns(portfolioId).subscribe({
      next: (data) => {
        this.monthlyReturns = data;
        if (this.activeTab === 'monthly') setTimeout(() => this.renderMonthlyBarChart());
      },
      error: () => this.monthlyReturns = null
    });
  }

  onTabChange(tabKey: string): void {
    this.activeTab = tabKey;
    setTimeout(() => {
      if (tabKey === 'overview') this.renderOverviewCharts();
      if (tabKey === 'equity') this.renderEquityCurveChart();
      if (tabKey === 'monthly') this.renderMonthlyBarChart();
    });
  }

  onPortfolioChangeNew(): void {
    if (this.selectedPortfolioId) {
      this.loadMetrics(this.selectedPortfolioId);
      this.loadAdvancedData(this.selectedPortfolioId);
      // Filter holdings for selected portfolio
      const portfolio = this.summary?.portfolios.find(p => p.portfolioId === this.selectedPortfolioId);
      this.topHoldings = (portfolio?.positions ?? [])
        .filter(pos => pos && pos.symbol)
        .sort((a, b) => (b.marketValue ?? 0) - (a.marketValue ?? 0));
    } else {
      // Show all
      this.topHoldings = (this.summary?.portfolios ?? [])
        .flatMap(p => p.positions ?? [])
        .filter(pos => pos && pos.symbol)
        .sort((a, b) => (b.marketValue ?? 0) - (a.marketValue ?? 0));
      this.advPerformance = null;
      this.equityCurve = null;
      this.monthlyReturns = null;
      if (this.portfolioIds.length > 0) {
        this.loadMetrics(this.portfolioIds[0]);
        this.loadAdvancedData(this.portfolioIds[0]);
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

  getMonthlyReturn(year: number, month: number): number | null {
    if (!this.monthlyReturns) return null;
    const item = this.monthlyReturns.returns.find(r => r.year === year && r.month === month);
    return item ? item.returnPercent : null;
  }

  ngOnDestroy(): void {
    this.destroyCharts();
  }

  private destroyCharts(): void {
    this.pnlBarChart?.destroy();
    this.pieChart?.destroy();
    this.equityCurveChart?.destroy();
    this.monthlyBarChart?.destroy();
  }

  private renderOverviewCharts(): void {
    setTimeout(() => {
      this.renderPnLBarChart();
      this.renderPieChart();
    });
  }

  private renderPnLBarChart(): void {
    if (!this.pnlBarCanvas?.nativeElement || this.topHoldings.length === 0) return;
    this.pnlBarChart?.destroy();

    const labels = this.topHoldings.slice(0, 10).map(h => h.symbol);
    const data = this.topHoldings.slice(0, 10).map(h => h.totalPnL ?? h.unrealizedPnL ?? 0);
    const colors = data.map(v => v >= 0 ? 'rgba(16, 185, 129, 0.8)' : 'rgba(239, 68, 68, 0.8)');

    this.pnlBarChart = new Chart(this.pnlBarCanvas.nativeElement, {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: 'Lãi/Lỗ (VND)',
          data,
          backgroundColor: colors,
          borderRadius: 4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (ctx) => this.formatVnd(ctx.parsed.y ?? 0)
            }
          }
        },
        scales: {
          y: {
            ticks: {
              callback: (v) => this.formatVndShort(Number(v))
            },
            grid: { color: 'rgba(0,0,0,0.05)' }
          },
          x: { grid: { display: false } }
        }
      }
    });
  }

  private renderPieChart(): void {
    if (!this.pieCanvas?.nativeElement || this.topHoldings.length === 0) return;
    this.pieChart?.destroy();

    const labels = this.topHoldings.slice(0, 8).map(h => h.symbol);
    const data = this.topHoldings.slice(0, 8).map(h => h.marketValue);

    this.pieChart = new Chart(this.pieCanvas.nativeElement, {
      type: 'doughnut',
      data: {
        labels,
        datasets: [{
          data,
          backgroundColor: this.positionColors,
          borderWidth: 2,
          borderColor: '#f9fafb'
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'right',
            labels: { padding: 12, usePointStyle: true, pointStyle: 'circle' }
          },
          tooltip: {
            callbacks: {
              label: (ctx) => {
                const total = (ctx.dataset.data as number[]).reduce((a, b) => a + b, 0);
                const pct = total > 0 ? ((ctx.parsed / total) * 100).toFixed(1) : '0';
                return `${ctx.label}: ${this.formatVnd(ctx.parsed)} (${pct}%)`;
              }
            }
          }
        }
      }
    });
  }

  renderEquityCurveChart(): void {
    if (!this.equityCurveCanvas?.nativeElement || !this.equityCurve?.points?.length) return;
    this.equityCurveChart?.destroy();

    const points = this.equityCurve.points;
    const labels = points.map(p => new Date(p.date).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' }));
    const values = points.map(p => p.portfolioValue);

    this.equityCurveChart = new Chart(this.equityCurveCanvas.nativeElement, {
      type: 'line',
      data: {
        labels,
        datasets: [{
          label: 'Giá trị danh mục',
          data: values,
          borderColor: '#3B82F6',
          backgroundColor: 'rgba(59, 130, 246, 0.1)',
          fill: true,
          tension: 0.3,
          pointRadius: points.length > 30 ? 0 : 3,
          pointHoverRadius: 5,
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (ctx) => this.formatVnd(ctx.parsed.y ?? 0)
            }
          }
        },
        scales: {
          y: {
            ticks: { callback: (v) => this.formatVndShort(Number(v)) },
            grid: { color: 'rgba(0,0,0,0.05)' }
          },
          x: {
            grid: { display: false },
            ticks: { maxTicksLimit: 10 }
          }
        }
      }
    });
  }

  renderMonthlyBarChart(): void {
    if (!this.monthlyBarCanvas?.nativeElement || !this.monthlyReturns?.returns?.length) return;
    this.monthlyBarChart?.destroy();

    const sorted = [...this.monthlyReturns.returns].sort((a, b) => {
      if (a.year !== b.year) return a.year - b.year;
      return a.month - b.month;
    });
    const labels = sorted.map(r => `T${r.month}/${r.year}`);
    const data = sorted.map(r => r.returnPercent);
    const colors = data.map(v => v >= 0 ? 'rgba(16, 185, 129, 0.8)' : 'rgba(239, 68, 68, 0.8)');

    this.monthlyBarChart = new Chart(this.monthlyBarCanvas.nativeElement, {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: 'Lợi nhuận (%)',
          data,
          backgroundColor: colors,
          borderRadius: 4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: (ctx) => { const v = ctx.parsed.y ?? 0; return `${v >= 0 ? '+' : ''}${v.toFixed(2)}%`; }
            }
          }
        },
        scales: {
          y: {
            ticks: { callback: (v) => `${v}%` },
            grid: { color: 'rgba(0,0,0,0.05)' }
          },
          x: { grid: { display: false } }
        }
      }
    });
  }

  private formatVnd(value: number): string {
    if (Math.abs(value) >= 1e9) return (value / 1e9).toFixed(1) + ' tỷ';
    if (Math.abs(value) >= 1e6) return (value / 1e6).toFixed(1) + ' tr';
    if (Math.abs(value) >= 1e3) return (value / 1e3).toFixed(0) + 'k';
    return value.toLocaleString('vi-VN');
  }

  private formatVndShort(value: number): string {
    if (Math.abs(value) >= 1e9) return (value / 1e9).toFixed(0) + 'B';
    if (Math.abs(value) >= 1e6) return (value / 1e6).toFixed(0) + 'M';
    if (Math.abs(value) >= 1e3) return (value / 1e3).toFixed(0) + 'K';
    return value.toString();
  }
}
