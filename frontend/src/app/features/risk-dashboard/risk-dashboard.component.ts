import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { RiskService, RiskProfile, PortfolioRiskSummary, DrawdownResult, CorrelationPair, StopLossTargetItem } from '../../core/services/risk.service';
import { StrategyService, Strategy, StrategyPerformance } from '../../core/services/strategy.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

interface RiskOverview {
  totalValue: number;
  totalPositions: number;
  maxDrawdown: number;
  currentDrawdown: number;
  valueAtRisk: number;
  largestPosition: number;
  activeSLCount: number;
  triggeredSLCount: number;
  highCorrelationPairs: number;
}

interface StrategyScore {
  id: string;
  name: string;
  grade: string;
  score: number;
  winRate: number;
  profitFactor: number;
  totalTrades: number;
}

@Component({
  selector: 'app-risk-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-2xl font-bold text-gray-800">Risk Dashboard</h1>
        <select [(ngModel)]="selectedPortfolioId" (ngModelChange)="loadDashboard()"
          class="px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          <option value="">-- Chọn danh mục --</option>
          <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
        </select>
      </div>

      <div *ngIf="!selectedPortfolioId" class="text-center py-16 text-gray-400">
        Chọn danh mục để xem tổng quan rủi ro
      </div>

      <div *ngIf="selectedPortfolioId && loading" class="text-center py-16 text-gray-400">Đang tải...</div>

      <div *ngIf="selectedPortfolioId && !loading">
        <!-- Top Row: Risk Overview Cards -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-xs text-gray-500 mb-1">Tổng giá trị</div>
            <div class="text-xl font-bold text-gray-800">{{ overview.totalValue | vndCurrency }}</div>
            <div class="text-xs text-gray-400">{{ overview.totalPositions }} vị thế</div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-xs text-gray-500 mb-1">VaR (95%) <sup class="text-red-400 font-bold">¹</sup></div>
            <div class="text-xl font-bold text-red-600">{{ overview.valueAtRisk | vndCurrency }}</div>
            <div class="text-xs text-gray-400">Mức lỗ tối đa 1 ngày</div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-xs text-gray-500 mb-1">Max Drawdown <sup class="text-orange-400 font-bold">²</sup></div>
            <div class="text-xl font-bold text-red-600">{{ overview.maxDrawdown | number:'1.2-2' }}%</div>
            <div class="text-xs text-gray-400">Hiện tại: {{ overview.currentDrawdown | number:'1.2-2' }}%</div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-xs text-gray-500 mb-1">Vị thế lớn nhất</div>
            <div class="text-xl font-bold" [class.text-red-600]="overview.largestPosition > 25"
              [class.text-yellow-600]="overview.largestPosition > 15 && overview.largestPosition <= 25"
              [class.text-green-600]="overview.largestPosition <= 15">
              {{ overview.largestPosition | number:'1.1-1' }}%
            </div>
            <div class="text-xs text-gray-400">của danh mục</div>
          </div>
        </div>

        <!-- Risk Health Bar -->
        <div class="bg-white rounded-lg shadow p-6 mb-6">
          <div class="flex justify-between items-center mb-3">
            <h2 class="text-lg font-semibold">Sức khỏe rủi ro</h2>
            <span class="px-3 py-1 rounded-full text-sm font-bold"
              [class.bg-green-100]="riskHealthScore >= 70" [class.text-green-700]="riskHealthScore >= 70"
              [class.bg-yellow-100]="riskHealthScore >= 40 && riskHealthScore < 70"
              [class.text-yellow-700]="riskHealthScore >= 40 && riskHealthScore < 70"
              [class.bg-red-100]="riskHealthScore < 40" [class.text-red-700]="riskHealthScore < 40">
              {{ riskHealthScore }}/100
            </span>
          </div>
          <div class="w-full bg-gray-200 rounded-full h-4">
            <div class="h-4 rounded-full transition-all duration-500"
              [style.width.%]="riskHealthScore"
              [class.bg-green-500]="riskHealthScore >= 70"
              [class.bg-yellow-500]="riskHealthScore >= 40 && riskHealthScore < 70"
              [class.bg-red-500]="riskHealthScore < 40"></div>
          </div>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mt-4">
            <div *ngFor="let item of healthItems" class="flex items-center gap-2 text-sm">
              <div class="w-3 h-3 rounded-full"
                [class.bg-green-500]="item.status === 'good'"
                [class.bg-yellow-500]="item.status === 'warning'"
                [class.bg-red-500]="item.status === 'danger'"></div>
              <span class="text-gray-600">{{ item.label }}</span>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
          <!-- Risk Profile Compliance -->
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Tuân thủ Risk Profile</h2>
            <div *ngIf="!riskProfile" class="text-center py-4 text-gray-400">Chưa thiết lập risk profile</div>
            <div *ngIf="riskProfile" class="space-y-3">
              <div *ngFor="let item of complianceItems" class="flex items-center justify-between">
                <div class="flex-1">
                  <div class="flex justify-between text-sm mb-1">
                    <span class="text-gray-600">{{ item.label }}</span>
                    <span class="font-medium" [class.text-green-600]="item.ok" [class.text-red-600]="!item.ok">
                      {{ item.current | number:'1.1-1' }}% / {{ item.limit | number:'1.1-1' }}%
                    </span>
                  </div>
                  <div class="w-full bg-gray-200 rounded-full h-2">
                    <div class="h-2 rounded-full"
                      [style.width.%]="Math.min((item.current / item.limit) * 100, 100)"
                      [class.bg-green-500]="item.ok" [class.bg-red-500]="!item.ok"></div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Stop-Loss Status -->
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Trạng thái Stop-Loss</h2>
            <div class="grid grid-cols-3 gap-4 mb-4">
              <div class="text-center bg-blue-50 rounded-lg p-3">
                <div class="text-2xl font-bold text-blue-700">{{ overview.activeSLCount }}</div>
                <div class="text-xs text-blue-500">Đang hoạt động</div>
              </div>
              <div class="text-center bg-red-50 rounded-lg p-3">
                <div class="text-2xl font-bold text-red-700">{{ overview.triggeredSLCount }}</div>
                <div class="text-xs text-red-500">Đã kích hoạt</div>
              </div>
              <div class="text-center bg-yellow-50 rounded-lg p-3">
                <div class="text-2xl font-bold text-yellow-700">{{ closestSLPercent | number:'1.1-1' }}%</div>
                <div class="text-xs text-yellow-500">SL gần nhất</div>
              </div>
            </div>
            <div *ngIf="nearestSLItems.length > 0" class="space-y-2">
              <div class="text-xs font-medium text-gray-500 uppercase">Vị thế gần SL nhất</div>
              <div *ngFor="let item of nearestSLItems" class="flex justify-between items-center text-sm bg-gray-50 rounded p-2">
                <span class="font-medium">{{ item.symbol }}</span>
                <span class="text-red-600">{{ item.distancePercent | number:'1.1-1' }}% tới SL</span>
              </div>
            </div>
          </div>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <!-- Correlation Warnings -->
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Cảnh báo tương quan <sup class="text-amber-400 font-bold">⁶</sup></h2>
            <div *ngIf="highCorrelationPairs.length === 0" class="text-center py-4 text-gray-400">
              Không có cặp tương quan cao
            </div>
            <div class="space-y-2">
              <div *ngFor="let pair of highCorrelationPairs"
                class="flex justify-between items-center bg-red-50 rounded-lg p-3">
                <span class="font-medium text-sm">{{ pair.symbol1 }} - {{ pair.symbol2 }}</span>
                <span class="text-red-700 font-bold">{{ pair.correlation | number:'1.2-2' }}</span>
              </div>
            </div>
            <div *ngIf="highCorrelationPairs.length > 0" class="mt-3 text-xs text-gray-500">
              Cặp có tương quan > 0.7 làm tăng rủi ro tập trung
            </div>
          </div>

          <!-- Strategy Scorecard -->
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Điểm chiến lược</h2>
            <div *ngIf="strategyScores.length === 0" class="text-center py-4 text-gray-400">
              Chưa có dữ liệu chiến lược
            </div>
            <div class="space-y-3">
              <div *ngFor="let s of strategyScores" class="flex items-center gap-3 bg-gray-50 rounded-lg p-3">
                <div class="w-10 h-10 rounded-full flex items-center justify-center font-bold text-lg"
                  [class.bg-green-100]="s.grade === 'A' || s.grade === 'B'"
                  [class.text-green-700]="s.grade === 'A' || s.grade === 'B'"
                  [class.bg-yellow-100]="s.grade === 'C'"
                  [class.text-yellow-700]="s.grade === 'C'"
                  [class.bg-red-100]="s.grade === 'D' || s.grade === 'F'"
                  [class.text-red-700]="s.grade === 'D' || s.grade === 'F'">
                  {{ s.grade }}
                </div>
                <div class="flex-1">
                  <div class="font-medium text-sm">{{ s.name }}</div>
                  <div class="text-xs text-gray-500">
                    Win<sup class="text-emerald-500 font-bold">³</sup>: {{ s.winRate | number:'1.0-0' }}% | PF<sup class="text-blue-400 font-bold">⁴</sup>: {{ s.profitFactor | number:'1.1-1' }} | {{ s.totalTrades }} GD
                  </div>
                </div>
                <div class="text-sm font-bold">{{ s.score }}/100</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Stress Test / What-If -->
        <div class="bg-white rounded-lg shadow p-6 mt-6">
          <h2 class="text-lg font-semibold mb-4">Stress Test — Mô phỏng kịch bản</h2>
          <p class="text-sm text-gray-500 mb-4">Ước tính ảnh hưởng lên danh mục nếu VNINDEX biến động mạnh (dựa trên beta ước tính)</p>

          <div class="grid grid-cols-2 md:grid-cols-5 gap-3 mb-6">
            <button *ngFor="let scenario of stressScenarios"
              (click)="runStressTest(scenario.marketChange)"
              class="px-3 py-3 rounded-lg border-2 text-center transition-all text-sm font-medium"
              [class.border-blue-500]="selectedScenario === scenario.marketChange"
              [class.bg-blue-50]="selectedScenario === scenario.marketChange"
              [class.border-gray-200]="selectedScenario !== scenario.marketChange"
              [class.hover:border-gray-400]="selectedScenario !== scenario.marketChange">
              <div class="font-bold" [class.text-red-600]="scenario.marketChange < 0" [class.text-green-600]="scenario.marketChange > 0">
                {{ scenario.marketChange > 0 ? '+' : '' }}{{ scenario.marketChange }}%
              </div>
              <div class="text-xs text-gray-500">{{ scenario.label }}</div>
            </button>
          </div>

          <!-- Custom scenario -->
          <div class="flex items-center gap-3 mb-6">
            <label class="text-sm text-gray-600 whitespace-nowrap">Hoặc nhập tùy chỉnh:</label>
            <input [(ngModel)]="customScenarioChange" type="number" step="1" placeholder="-10"
              class="w-24 px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
            <span class="text-sm text-gray-500">%</span>
            <button (click)="runStressTest(customScenarioChange)"
              class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-medium transition-colors">
              Mô phỏng
            </button>
          </div>

          <!-- Stress test results -->
          <div *ngIf="stressResults.length > 0">
            <div class="mb-4 p-4 rounded-lg"
              [class.bg-red-50]="stressTotalImpact < 0" [class.bg-green-50]="stressTotalImpact >= 0">
              <div class="flex justify-between items-center">
                <span class="text-sm font-medium text-gray-700">Ảnh hưởng tổng danh mục</span>
                <span class="text-xl font-bold" [class.text-red-700]="stressTotalImpact < 0" [class.text-green-700]="stressTotalImpact >= 0">
                  {{ stressTotalImpact >= 0 ? '+' : '' }}{{ stressTotalImpact | number:'1.0-0' }} VND
                  ({{ stressTotalImpactPercent >= 0 ? '+' : '' }}{{ stressTotalImpactPercent | number:'1.2-2' }}%)
                </span>
              </div>
            </div>

            <table class="w-full text-sm">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-4 py-2 text-left text-xs text-gray-500">Mã CP</th>
                  <th class="px-4 py-2 text-right text-xs text-gray-500">Giá trị hiện tại</th>
                  <th class="px-4 py-2 text-right text-xs text-gray-500">Beta ước tính <sup class="text-violet-400 font-bold">⁵</sup></th>
                  <th class="px-4 py-2 text-right text-xs text-gray-500">Ảnh hưởng</th>
                  <th class="px-4 py-2 text-right text-xs text-gray-500">Giá trị sau</th>
                </tr>
              </thead>
              <tbody class="divide-y">
                <tr *ngFor="let r of stressResults" class="hover:bg-gray-50">
                  <td class="px-4 py-2 font-medium">{{ r.symbol }}</td>
                  <td class="px-4 py-2 text-right">{{ r.currentValue | number:'1.0-0' }}</td>
                  <td class="px-4 py-2 text-right">{{ r.beta | number:'1.2-2' }}</td>
                  <td class="px-4 py-2 text-right font-medium" [class.text-red-600]="r.impact < 0" [class.text-green-600]="r.impact >= 0">
                    {{ r.impact >= 0 ? '+' : '' }}{{ r.impact | number:'1.0-0' }}
                  </td>
                  <td class="px-4 py-2 text-right">{{ r.afterValue | number:'1.0-0' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div *ngIf="stressResults.length === 0 && selectedScenario !== null" class="text-center py-4 text-gray-400">
            Không có vị thế nào để mô phỏng
          </div>
        </div>

        <!-- Risk Profile Setup -->
        <div class="bg-white rounded-lg shadow p-6 mt-6">
          <h2 class="text-lg font-semibold mb-4">Thiết lập Risk Profile</h2>
          <p class="text-sm text-gray-500 mb-4">Đặt quy tắc quản lý rủi ro cứng — Trade Plan sẽ cảnh báo khi vi phạm</p>

          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Max vị thế (%)</label>
              <input [(ngModel)]="riskProfileForm.maxPositionSizePercent" type="number" step="1" min="1" max="100"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <div class="text-xs text-gray-400 mt-1">Tỷ trọng tối đa 1 CP</div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Max rủi ro/lệnh (%)</label>
              <input [(ngModel)]="riskProfileForm.maxPortfolioRiskPercent" type="number" step="0.5" min="0.5" max="10"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <div class="text-xs text-gray-400 mt-1">% vốn rủi ro mỗi lệnh</div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Max Drawdown Alert (%)</label>
              <input [(ngModel)]="riskProfileForm.maxDrawdownAlertPercent" type="number" step="1" min="5" max="50"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <div class="text-xs text-gray-400 mt-1">Ngưỡng cảnh báo drawdown</div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">R:R tối thiểu</label>
              <input [(ngModel)]="riskProfileForm.defaultRiskRewardRatio" type="number" step="0.5" min="1" max="10"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <div class="text-xs text-gray-400 mt-1">Tỷ lệ Risk/Reward tối thiểu</div>
            </div>
          </div>

          <div class="mt-4 flex items-center gap-3">
            <button (click)="saveRiskProfile()"
              class="px-6 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-medium transition-colors"
              [disabled]="savingProfile">
              {{ savingProfile ? 'Đang lưu...' : 'Lưu Risk Profile' }}
            </button>
            <span *ngIf="profileSaved" class="text-sm text-green-600 font-medium">Đã lưu thành công!</span>
          </div>
        </div>

        <!-- Glossary -->
        <div class="mt-6 rounded-lg bg-gray-50 border border-gray-200 px-4 py-3 text-xs text-gray-500 space-y-1">
          <div class="font-medium text-gray-600 mb-1">Giải thích thuật ngữ</div>
          <div><sup class="text-red-400 font-bold">¹</sup> <strong>VaR 95% (Value at Risk):</strong> Mức lỗ tối đa dự kiến trong 1 ngày với xác suất 95% — chỉ có 5% khả năng thua lỗ vượt mức này trong điều kiện thị trường thông thường.</div>
          <div><sup class="text-orange-400 font-bold">²</sup> <strong>Max Drawdown — Sụt giảm tối đa:</strong> Khoảng cách từ đỉnh cao nhất xuống đáy thấp nhất trong lịch sử danh mục (VD: -25% = có lúc danh mục giảm 25% từ đỉnh).</div>
          <div><sup class="text-emerald-500 font-bold">³</sup> <strong>Win Rate (Tỷ lệ thắng):</strong> % số lệnh có lãi trên tổng số lệnh. VD: 60% = 6/10 lệnh thắng. Cần kết hợp với Profit Factor để đánh giá toàn diện.</div>
          <div><sup class="text-blue-400 font-bold">⁴</sup> <strong>Profit Factor (PF):</strong> Tổng lãi gộp ÷ Tổng lỗ gộp. PF > 1.5 là tốt; PF = 1 = hòa vốn; PF &lt; 1 = chiến lược thua lỗ tổng thể.</div>
          <div><sup class="text-violet-400 font-bold">⁵</sup> <strong>Beta:</strong> Hệ số đo độ nhạy cảm của cổ phiếu so với VN-Index. Beta = 1.5 → CP tăng/giảm ~1.5% khi VNIndex thay đổi 1%.</div>
          <div><sup class="text-amber-400 font-bold">⁶</sup> <strong>Tương quan (Correlation):</strong> Mức độ hai CP biến động cùng chiều. Từ -1 (hoàn toàn ngược chiều) đến +1 (hoàn toàn cùng chiều). Correlation &gt; 0.7 = rủi ro tập trung, nếu một cổ giảm thì cổ kia cũng giảm theo.</div>
        </div>
      </div>
    </div>
  `
})
export class RiskDashboardComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  loading = false;
  riskProfile: RiskProfile | null = null;
  Math = Math;

  overview: RiskOverview = {
    totalValue: 0, totalPositions: 0, maxDrawdown: 0, currentDrawdown: 0,
    valueAtRisk: 0, largestPosition: 0, activeSLCount: 0, triggeredSLCount: 0,
    highCorrelationPairs: 0
  };

  riskHealthScore = 0;
  healthItems: { label: string; status: string }[] = [];
  complianceItems: { label: string; current: number; limit: number; ok: boolean }[] = [];
  highCorrelationPairs: CorrelationPair[] = [];
  nearestSLItems: { symbol: string; distancePercent: number }[] = [];
  closestSLPercent = 0;
  strategyScores: StrategyScore[] = [];

  // Stress Test
  stressScenarios = [
    { label: 'Crash nặng', marketChange: -20 },
    { label: 'Suy giảm', marketChange: -10 },
    { label: 'Điều chỉnh', marketChange: -5 },
    { label: 'Hồi phục', marketChange: 5 },
    { label: 'Tăng mạnh', marketChange: 15 },
  ];
  selectedScenario: number | null = null;
  customScenarioChange = -10;
  stressResults: { symbol: string; currentValue: number; beta: number; impact: number; afterValue: number }[] = [];
  stressTotalImpact = 0;
  stressTotalImpactPercent = 0;
  private lastRiskSummary: PortfolioRiskSummary | null = null;

  // Risk Profile Form
  riskProfileForm = { maxPositionSizePercent: 20, maxPortfolioRiskPercent: 2, maxDrawdownAlertPercent: 15, defaultRiskRewardRatio: 2 };
  savingProfile = false;
  profileSaved = false;

  constructor(
    private portfolioService: PortfolioService,
    private riskService: RiskService,
    private strategyService: StrategyService
  ) {}

  ngOnInit(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => {
        this.portfolios = data;
        if (data.length > 0) {
          this.selectedPortfolioId = data[0].id;
          this.loadDashboard();
        }
      }
    });
  }

  loadDashboard(): void {
    if (!this.selectedPortfolioId) return;
    this.loading = true;

    forkJoin({
      risk: this.riskService.getPortfolioRiskSummary(this.selectedPortfolioId),
      drawdown: this.riskService.getDrawdown(this.selectedPortfolioId),
      correlation: this.riskService.getCorrelation(this.selectedPortfolioId),
      stopLoss: this.riskService.getStopLossTargets(this.selectedPortfolioId),
    }).subscribe({
      next: ({ risk, drawdown, correlation, stopLoss }) => {
        this.lastRiskSummary = risk;
        // Overview
        this.overview.totalValue = risk.totalValue;
        this.overview.totalPositions = risk.positionCount;
        this.overview.valueAtRisk = risk.valueAtRisk95;
        this.overview.largestPosition = risk.largestPositionPercent;
        this.overview.maxDrawdown = drawdown.maxDrawdownPercent;
        this.overview.currentDrawdown = drawdown.currentDrawdownPercent;

        // Correlation
        this.highCorrelationPairs = correlation.pairs.filter(p => Math.abs(p.correlation) > 0.7);
        this.overview.highCorrelationPairs = this.highCorrelationPairs.length;

        // Stop-loss
        const slItems = stopLoss.items || [];
        this.overview.activeSLCount = slItems.filter(s => !s.isStopLossTriggered && !s.isTargetTriggered).length;
        this.overview.triggeredSLCount = slItems.filter(s => s.isStopLossTriggered).length;

        // Nearest SL items from position risk
        this.nearestSLItems = risk.positions
          .filter(p => p.distanceToStopLossPercent > 0)
          .sort((a, b) => a.distanceToStopLossPercent - b.distanceToStopLossPercent)
          .slice(0, 5)
          .map(p => ({ symbol: p.symbol, distancePercent: p.distanceToStopLossPercent }));
        this.closestSLPercent = this.nearestSLItems.length > 0 ? this.nearestSLItems[0].distancePercent : 0;

        // Load risk profile
        this.riskService.getRiskProfile(this.selectedPortfolioId).subscribe({
          next: (profile) => {
            this.riskProfile = profile;
            this.riskProfileForm = {
              maxPositionSizePercent: profile.maxPositionSizePercent,
              maxPortfolioRiskPercent: profile.maxPortfolioRiskPercent,
              maxDrawdownAlertPercent: profile.maxDrawdownAlertPercent,
              defaultRiskRewardRatio: profile.defaultRiskRewardRatio,
            };
            this.buildCompliance(risk, profile);
            this.calculateHealthScore(risk, drawdown, profile);
          },
          error: () => {
            this.riskProfile = null;
            this.calculateHealthScore(risk, drawdown, null);
          }
        });

        this.loading = false;
      },
      error: () => this.loading = false
    });

    // Load strategy scores
    this.loadStrategyScores();
  }

  buildCompliance(risk: PortfolioRiskSummary, profile: RiskProfile): void {
    this.complianceItems = [
      {
        label: 'Vị thế lớn nhất',
        current: risk.largestPositionPercent,
        limit: profile.maxPositionSizePercent,
        ok: risk.largestPositionPercent <= profile.maxPositionSizePercent
      },
      {
        label: 'Drawdown hiện tại',
        current: Math.abs(this.overview.currentDrawdown),
        limit: profile.maxDrawdownAlertPercent,
        ok: Math.abs(this.overview.currentDrawdown) <= profile.maxDrawdownAlertPercent
      }
    ];
  }

  calculateHealthScore(risk: PortfolioRiskSummary, drawdown: DrawdownResult, profile: RiskProfile | null): void {
    let score = 100;
    this.healthItems = [];

    // Drawdown check
    if (Math.abs(drawdown.currentDrawdownPercent) > 15) {
      score -= 30;
      this.healthItems.push({ label: 'Drawdown cao', status: 'danger' });
    } else if (Math.abs(drawdown.currentDrawdownPercent) > 8) {
      score -= 15;
      this.healthItems.push({ label: 'Drawdown trung bình', status: 'warning' });
    } else {
      this.healthItems.push({ label: 'Drawdown thấp', status: 'good' });
    }

    // Concentration check
    if (risk.largestPositionPercent > 30) {
      score -= 20;
      this.healthItems.push({ label: 'Tập trung cao', status: 'danger' });
    } else if (risk.largestPositionPercent > 20) {
      score -= 10;
      this.healthItems.push({ label: 'Tập trung vừa', status: 'warning' });
    } else {
      this.healthItems.push({ label: 'Đa dạng hóa tốt', status: 'good' });
    }

    // Correlation check
    if (this.highCorrelationPairs.length > 3) {
      score -= 15;
      this.healthItems.push({ label: 'Tương quan cao', status: 'danger' });
    } else if (this.highCorrelationPairs.length > 0) {
      score -= 5;
      this.healthItems.push({ label: 'Tương quan vừa', status: 'warning' });
    } else {
      this.healthItems.push({ label: 'Tương quan thấp', status: 'good' });
    }

    // Stop-loss coverage
    const hasSL = this.overview.activeSLCount > 0;
    if (!hasSL && risk.positionCount > 0) {
      score -= 20;
      this.healthItems.push({ label: 'Thiếu stop-loss', status: 'danger' });
    } else {
      this.healthItems.push({ label: 'Có stop-loss', status: 'good' });
    }

    this.riskHealthScore = Math.max(0, Math.min(100, score));
  }

  loadStrategyScores(): void {
    this.strategyService.getAll().subscribe({
      next: (strategies) => {
        const active = strategies.filter(s => s.isActive);
        if (active.length === 0) { this.strategyScores = []; return; }

        const scores: StrategyScore[] = [];
        let loaded = 0;
        active.forEach(s => {
          this.strategyService.getPerformance(s.id).subscribe({
            next: (perf) => {
              scores.push(this.scoreStrategy(s, perf));
              loaded++;
              if (loaded === active.length) {
                this.strategyScores = scores.sort((a, b) => b.score - a.score);
              }
            },
            error: () => {
              loaded++;
              if (loaded === active.length) {
                this.strategyScores = scores.sort((a, b) => b.score - a.score);
              }
            }
          });
        });
      }
    });
  }

  scoreStrategy(s: Strategy, perf: StrategyPerformance): StrategyScore {
    let score = 0;
    // Win rate: 0-30 points
    score += Math.min(30, perf.winRate * 0.6);
    // Profit factor: 0-30 points
    score += Math.min(30, perf.profitFactor * 15);
    // Enough trades: 0-20 points
    score += Math.min(20, perf.totalTrades * 2);
    // Consistency (avg win vs avg loss): 0-20 points
    if (perf.averageLoss !== 0) {
      const ratio = Math.abs(perf.averageWin / perf.averageLoss);
      score += Math.min(20, ratio * 10);
    }
    score = Math.round(Math.min(100, score));

    let grade = 'F';
    if (score >= 85) grade = 'A';
    else if (score >= 70) grade = 'B';
    else if (score >= 55) grade = 'C';
    else if (score >= 40) grade = 'D';

    return {
      id: s.id, name: s.name, grade, score,
      winRate: perf.winRate, profitFactor: perf.profitFactor, totalTrades: perf.totalTrades
    };
  }

  // --- Stress Test ---
  // Estimated betas for common VN stocks (simplified — in production, calculate from price history)
  private estimatedBetas: Record<string, number> = {
    VIC: 1.2, VNM: 0.7, FPT: 1.1, VCB: 0.9, HPG: 1.4,
    MWG: 1.3, TCB: 1.1, VHM: 1.3, MSN: 1.0, VRE: 0.8,
    SSI: 1.5, ACB: 1.0, MBB: 1.1, BID: 0.9, CTG: 0.9,
    GAS: 0.8, PLX: 0.7, SAB: 0.6, PNJ: 0.9, REE: 1.0,
  };

  runStressTest(marketChangePercent: number): void {
    this.selectedScenario = marketChangePercent;
    if (!this.lastRiskSummary?.positions?.length) {
      this.stressResults = [];
      return;
    }

    const change = marketChangePercent / 100;
    this.stressResults = this.lastRiskSummary.positions.map(pos => {
      const beta = this.estimatedBetas[pos.symbol.toUpperCase()] ?? 1.0;
      const positionChange = change * beta;
      const impact = pos.marketValue * positionChange;
      return {
        symbol: pos.symbol,
        currentValue: pos.marketValue,
        beta,
        impact,
        afterValue: pos.marketValue + impact,
      };
    });

    this.stressTotalImpact = this.stressResults.reduce((sum, r) => sum + r.impact, 0);
    this.stressTotalImpactPercent = this.lastRiskSummary.totalValue > 0
      ? (this.stressTotalImpact / this.lastRiskSummary.totalValue) * 100 : 0;
  }

  // --- Risk Profile Save ---
  saveRiskProfile(): void {
    if (!this.selectedPortfolioId) return;
    this.savingProfile = true;
    this.profileSaved = false;

    this.riskService.setRiskProfile(this.selectedPortfolioId, this.riskProfileForm).subscribe({
      next: () => {
        this.savingProfile = false;
        this.profileSaved = true;
        // Reload dashboard to reflect new profile
        this.loadDashboard();
        setTimeout(() => this.profileSaved = false, 3000);
      },
      error: () => this.savingProfile = false
    });
  }
}
