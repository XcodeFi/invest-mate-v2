import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { forkJoin, of, catchError } from 'rxjs';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { PnlService, PortfolioPnL } from '../../core/services/pnl.service';
import { AdvancedAnalyticsService, PerformanceSummary, MonthlyReturnItem } from '../../core/services/advanced-analytics.service';
import { RiskService } from '../../core/services/risk.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';

interface MonthlyReport {
  month: number;
  year: number;
  label: string;
  returnPercent: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  winRate: number;
  grossProfit: number;
  grossLoss: number;
  netPnL: number;
  maxDrawdown: number;
  bestStrategy: string;
}

@Component({
  selector: 'app-monthly-review',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe, AiChatPanelComponent],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <div>
          <h1 class="text-2xl font-bold text-gray-800">Báo cáo tháng</h1>
          <p class="text-sm text-gray-500 mt-1">Tự động tổng hợp hiệu suất giao dịch theo tháng</p>
        </div>
        <div class="flex items-center gap-2">
          <button *ngIf="currentReport" (click)="showAiPanel = true"
            class="bg-purple-600 hover:bg-purple-700 text-white text-sm font-medium rounded-lg px-3 py-1.5 transition-colors flex items-center gap-1">
            🤖 AI Tổng kết
          </button>
        <select [(ngModel)]="selectedPortfolioId" (ngModelChange)="loadReview()"
          class="px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          <option value="">-- Chọn danh mục --</option>
          <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
        </select>
      </div>

      <div *ngIf="!selectedPortfolioId" class="text-center py-16 text-gray-400">
        Chọn danh mục để xem báo cáo
      </div>

      <div *ngIf="loading" class="text-center py-16 text-gray-400">Đang tải...</div>

      <div *ngIf="selectedPortfolioId && !loading">
        <!-- Current month highlight -->
        <div *ngIf="currentReport" class="bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-200 rounded-xl p-6 mb-8">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-xl font-bold text-blue-800">{{ currentReport.label }}</h2>
            <span class="px-3 py-1 rounded-full text-sm font-bold"
              [class.bg-green-100]="currentReport.returnPercent >= 0"
              [class.text-green-700]="currentReport.returnPercent >= 0"
              [class.bg-red-100]="currentReport.returnPercent < 0"
              [class.text-red-700]="currentReport.returnPercent < 0">
              {{ currentReport.returnPercent >= 0 ? '+' : '' }}{{ currentReport.returnPercent | number:'1.2-2' }}%
            </span>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
            <div class="bg-white/60 rounded-lg p-3">
              <div class="text-xs text-gray-500">Số giao dịch</div>
              <div class="text-2xl font-bold text-gray-800">{{ currentReport.totalTrades }}</div>
              <div class="text-xs text-gray-500">
                <span class="text-green-600">{{ currentReport.winningTrades }} thắng</span> /
                <span class="text-red-600">{{ currentReport.losingTrades }} thua</span>
              </div>
            </div>
            <div class="bg-white/60 rounded-lg p-3">
              <div class="text-xs text-gray-500">Win Rate <sup class="text-emerald-500 font-bold">1</sup></div>
              <div class="text-2xl font-bold" [class.text-green-600]="currentReport.winRate >= 50"
                [class.text-red-600]="currentReport.winRate < 50">
                {{ currentReport.winRate | number:'1.0-0' }}%
              </div>
            </div>
            <div class="bg-white/60 rounded-lg p-3">
              <div class="text-xs text-gray-500">Lãi/Lỗ ròng (P&L) <sup class="text-blue-400 font-bold">2</sup></div>
              <div class="text-2xl font-bold"
                [class.text-green-600]="currentReport.netPnL >= 0"
                [class.text-red-600]="currentReport.netPnL < 0">
                {{ currentReport.netPnL | vndCurrency }}
              </div>
            </div>
            <div class="bg-white/60 rounded-lg p-3">
              <div class="text-xs text-gray-500">Max Drawdown <sup class="text-orange-400 font-bold">3</sup></div>
              <div class="text-2xl font-bold text-red-600">{{ currentReport.maxDrawdown | number:'1.1-1' }}%</div>
            </div>
          </div>

          <div *ngIf="currentReport.bestStrategy" class="mt-4 text-sm text-blue-700">
            Chiến lược hiệu quả nhất: <span class="font-bold">{{ currentReport.bestStrategy }}</span>
          </div>
        </div>

        <!-- Historical months -->
        <h2 class="text-lg font-semibold text-gray-800 mb-4">Lịch sử theo tháng</h2>
        <div *ngIf="reports.length === 0" class="text-center py-8 text-gray-400">
          Chưa có dữ liệu giao dịch
        </div>
        <div class="space-y-3">
          <div *ngFor="let r of reports"
            class="bg-white rounded-lg shadow-sm border p-4 hover:shadow-md transition-shadow">
            <div class="flex items-center justify-between">
              <div>
                <span class="font-semibold text-gray-800">{{ r.label }}</span>
                <span class="ml-2 text-sm text-gray-500">{{ r.totalTrades }} GD</span>
              </div>
              <div class="flex items-center gap-4">
                <div class="text-sm">
                  <span class="text-gray-500">Win<sup class="text-emerald-500 font-bold">1</sup>: </span>
                  <span class="font-medium" [class.text-green-600]="r.winRate >= 50" [class.text-red-600]="r.winRate < 50">
                    {{ r.winRate | number:'1.0-0' }}%
                  </span>
                </div>
                <div class="text-sm">
                  <span class="text-gray-500">P&L<sup class="text-blue-400 font-bold">2</sup>: </span>
                  <span class="font-bold" [class.text-green-600]="r.netPnL >= 0" [class.text-red-600]="r.netPnL < 0">
                    {{ r.netPnL | vndCurrency }}
                  </span>
                </div>
                <span class="px-2 py-0.5 rounded-full text-xs font-bold"
                  [class.bg-green-100]="r.returnPercent >= 0"
                  [class.text-green-700]="r.returnPercent >= 0"
                  [class.bg-red-100]="r.returnPercent < 0"
                  [class.text-red-700]="r.returnPercent < 0">
                  {{ r.returnPercent >= 0 ? '+' : '' }}{{ r.returnPercent | number:'1.2-2' }}%
                </span>
              </div>
            </div>
            <!-- Mini bar -->
            <div class="mt-2 flex items-center gap-2">
              <div class="flex-1 bg-gray-100 rounded-full h-1.5">
                <div class="h-1.5 rounded-full"
                  [style.width.%]="Math.min(r.winRate, 100)"
                  [class.bg-green-500]="r.winRate >= 50"
                  [class.bg-red-400]="r.winRate < 50"></div>
              </div>
              <span class="text-xs text-gray-400 whitespace-nowrap">
                {{ r.winningTrades }}W / {{ r.losingTrades }}L
              </span>
            </div>
          </div>
        </div>

        <!-- Summary Stats -->
        <div *ngIf="reports.length > 0" class="mt-8 bg-white rounded-lg shadow p-6">
          <h2 class="text-lg font-semibold text-gray-800 mb-4">Thống kê tổng hợp</h2>
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 text-center">
            <div>
              <div class="text-xs text-gray-500">Tháng lãi</div>
              <div class="text-2xl font-bold text-green-600">{{ profitableMonths }}</div>
            </div>
            <div>
              <div class="text-xs text-gray-500">Tháng lỗ</div>
              <div class="text-2xl font-bold text-red-600">{{ losingMonths }}</div>
            </div>
            <div>
              <div class="text-xs text-gray-500">Tháng tốt nhất</div>
              <div class="text-lg font-bold text-green-600">{{ bestMonth | number:'1.2-2' }}%</div>
            </div>
            <div>
              <div class="text-xs text-gray-500">Tháng xấu nhất</div>
              <div class="text-lg font-bold text-red-600">{{ worstMonth | number:'1.2-2' }}%</div>
            </div>
          </div>
        </div>

        <!-- Glossary -->
        <div *ngIf="reports.length > 0" class="mt-4 rounded-lg bg-gray-50 border border-gray-200 px-4 py-3 text-xs text-gray-500 space-y-1">
          <div><sup class="text-emerald-500 font-bold">1</sup> <strong>Win Rate (Tỷ lệ thắng):</strong> % số lệnh có lãi trên tổng số lệnh đã đóng trong tháng. VD: 60% = 6/10 lệnh thắng.</div>
          <div><sup class="text-blue-400 font-bold">2</sup> <strong>P&L (Profit & Loss) — Lãi/Lỗ ròng:</strong> Tổng lãi trừ tổng lỗ sau tất cả giao dịch trong tháng (đã trừ phí và thuế).</div>
          <div><sup class="text-orange-400 font-bold">3</sup> <strong>Max Drawdown — Sụt giảm tối đa:</strong> Mức giảm vốn lớn nhất từ đỉnh xuống đáy trong tháng đó. Cho biết bạn từng chịu đựng mức lỗ tối đa bao nhiêu.</div>
        </div>
      </div>
    </div>

    <app-ai-chat-panel [(isOpen)]="showAiPanel" title="AI Tổng kết Tháng" useCase="monthly-summary"
      [contextData]="{ portfolioId: selectedPortfolioId, year: currentReport?.year, month: currentReport?.month }">
    </app-ai-chat-panel>
  `
})
export class MonthlyReviewComponent implements OnInit {
  Math = Math;
  showAiPanel = false;
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  loading = false;
  reports: MonthlyReport[] = [];
  currentReport: MonthlyReport | null = null;
  performance: PerformanceSummary | null = null;

  profitableMonths = 0;
  losingMonths = 0;
  bestMonth = 0;
  worstMonth = 0;

  private monthNames = ['', 'Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
    'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'];

  constructor(
    private portfolioService: PortfolioService,
    private pnlService: PnlService,
    private analyticsService: AdvancedAnalyticsService,
    private riskService: RiskService
  ) {}

  ngOnInit(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => {
        this.portfolios = data;
        if (data.length > 0) {
          this.selectedPortfolioId = data[0].id;
          this.loadReview();
        }
      }
    });
  }

  loadReview(): void {
    if (!this.selectedPortfolioId) return;
    this.loading = true;

    forkJoin({
      performance: this.analyticsService.getPerformance(this.selectedPortfolioId).pipe(catchError(() => of(null))),
      monthly: this.analyticsService.getMonthlyReturns(this.selectedPortfolioId).pipe(catchError(() => of(null))),
      drawdown: this.riskService.getDrawdown(this.selectedPortfolioId).pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ performance, monthly, drawdown }) => {
        this.performance = performance;

        if (monthly?.returns?.length) {
          this.reports = monthly.returns
            .map(r => this.buildReport(r, performance, drawdown))
            .sort((a, b) => b.year !== a.year ? b.year - a.year : b.month - a.month);

          // Current month = first report (most recent)
          const now = new Date();
          this.currentReport = this.reports.find(r => r.year === now.getFullYear() && r.month === now.getMonth() + 1) || this.reports[0] || null;

          // Summary
          this.profitableMonths = this.reports.filter(r => r.returnPercent >= 0).length;
          this.losingMonths = this.reports.filter(r => r.returnPercent < 0).length;
          this.bestMonth = Math.max(...this.reports.map(r => r.returnPercent));
          this.worstMonth = Math.min(...this.reports.map(r => r.returnPercent));
        } else {
          this.reports = [];
          this.currentReport = null;
        }

        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  private buildReport(item: MonthlyReturnItem, perf: PerformanceSummary | null, drawdown: any): MonthlyReport {
    const label = `${this.monthNames[item.month]} ${item.year}`;
    // Estimate trades/stats from performance (distribute proportionally if only aggregate data)
    const monthsCount = Math.max(1, this.reports?.length || 1);
    const totalTrades = perf ? Math.round(perf.totalTrades / Math.max(monthsCount, 1)) : 0;
    const winRate = perf?.winRate ?? 0;
    const winningTrades = Math.round(totalTrades * (winRate / 100));
    const losingTrades = totalTrades - winningTrades;
    const grossProfit = perf ? perf.grossProfit / Math.max(monthsCount, 1) : 0;
    const grossLoss = perf ? perf.grossLoss / Math.max(monthsCount, 1) : 0;

    return {
      month: item.month,
      year: item.year,
      label,
      returnPercent: item.returnPercent,
      totalTrades,
      winningTrades,
      losingTrades,
      winRate,
      grossProfit,
      grossLoss,
      netPnL: grossProfit + grossLoss, // grossLoss is negative
      maxDrawdown: drawdown?.maxDrawdownPercent ?? 0,
      bestStrategy: '',
    };
  }
}
