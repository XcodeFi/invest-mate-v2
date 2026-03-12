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
            <div class="text-xs text-gray-500 mb-1">VaR (95%)</div>
            <div class="text-xl font-bold text-red-600">{{ overview.valueAtRisk | vndCurrency }}</div>
            <div class="text-xs text-gray-400">Mức lỗ tối đa 1 ngày</div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-xs text-gray-500 mb-1">Max Drawdown</div>
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
            <h2 class="text-lg font-semibold mb-4">Cảnh báo tương quan</h2>
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
                    Win: {{ s.winRate | number:'1.0-0' }}% | PF: {{ s.profitFactor | number:'1.1-1' }} | {{ s.totalTrades }} GD
                  </div>
                </div>
                <div class="text-sm font-bold">{{ s.score }}/100</div>
              </div>
            </div>
          </div>
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
}
