import { Component, OnInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { AuthService, User } from '../../core/services/auth.service';
import { PnlService, OverallPnLSummary, PortfolioPnL, PositionPnL } from '../../core/services/pnl.service';
import { RiskService, PortfolioRiskSummary, PositionRiskItem, RiskProfile } from '../../core/services/risk.service';
import { AdvancedAnalyticsService, EquityCurveData } from '../../core/services/advanced-analytics.service';
import { MarketDataService, MarketOverview } from '../../core/services/market-data.service';
import { PositionsService, ActivePosition } from '../../core/services/positions.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { isBuyTrade } from '../../shared/constants/trade-types';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

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
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe, UppercaseDirective],
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

        <!-- Market Overview Strip -->
        <div *ngIf="marketOverview.length > 0" class="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
          <div *ngFor="let idx of marketOverview"
            class="bg-white rounded-lg border px-4 py-3 flex items-center justify-between"
            [class.border-l-green-500]="idx.change >= 0" [class.border-l-red-500]="idx.change < 0"
            style="border-left-width: 3px;">
            <div>
              <div class="text-xs font-medium text-gray-500">{{ idx.symbol }}</div>
              <div class="text-lg font-bold" [class.text-green-600]="idx.change >= 0" [class.text-red-600]="idx.change < 0">
                {{ idx.price.toLocaleString('vi-VN', {maximumFractionDigits: 2}) }}
              </div>
            </div>
            <div class="text-right">
              <div class="text-sm font-medium" [class.text-green-600]="idx.change >= 0" [class.text-red-600]="idx.change < 0">
                {{ idx.changePercent >= 0 ? '+' : '' }}{{ idx.changePercent.toFixed(2) }}%
              </div>
              <div class="text-xs text-gray-400">KL: {{ idx.totalVolume >= 1000000 ? (idx.totalVolume / 1000000).toFixed(0) + 'M' : (idx.totalVolume / 1000).toFixed(0) + 'K' }}</div>
            </div>
          </div>
        </div>

        <!-- Risk Alert Banner (persistent top) -->
        <div *ngIf="riskAlerts.length > 0" class="mb-6 bg-gradient-to-r rounded-xl p-4 border-2 shadow-sm"
          [class.from-red-50]="hasDangerAlert" [class.to-orange-50]="hasDangerAlert" [class.border-red-300]="hasDangerAlert"
          [class.from-amber-50]="!hasDangerAlert" [class.to-yellow-50]="!hasDangerAlert" [class.border-amber-300]="!hasDangerAlert">
          <div class="flex items-center justify-between mb-2">
            <div class="flex items-center gap-2">
              <svg class="w-5 h-5" [class.text-red-600]="hasDangerAlert" [class.text-amber-600]="!hasDangerAlert"
                fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z"></path>
              </svg>
              <span class="font-bold text-sm" [class.text-red-800]="hasDangerAlert" [class.text-amber-800]="!hasDangerAlert">
                {{ riskAlerts.length }} cảnh báo rủi ro
              </span>
            </div>
            <button (click)="bannerDismissed = true" *ngIf="!bannerDismissed" class="text-xs text-gray-500 hover:text-gray-700">Ẩn</button>
          </div>
          <div *ngIf="!bannerDismissed" class="space-y-1">
            <div *ngFor="let alert of riskAlerts.slice(0, 3)" class="flex items-center gap-2 text-sm"
              [class.text-red-700]="alert.severity === 'danger'" [class.text-amber-700]="alert.severity === 'warning'">
              <span class="font-medium">{{ alert.symbol }}:</span> {{ alert.message }}
            </div>
            <a *ngIf="riskAlerts.length > 3" routerLink="/risk-dashboard"
              class="text-xs font-medium underline" [class.text-red-600]="hasDangerAlert" [class.text-amber-600]="!hasDangerAlert">
              Xem tất cả {{ riskAlerts.length }} cảnh báo →
            </a>
          </div>
        </div>

        <!-- Timeframe Switcher -->
        <div class="flex items-center gap-2 mb-6 flex-wrap">
          <span class="text-xs font-medium text-gray-500 mr-1">Xem theo:</span>
          <button *ngFor="let tf of timeframes"
            (click)="setTimeframe(tf.key)"
            class="px-3 py-1.5 rounded-full text-xs font-medium transition-colors"
            [class.bg-blue-600]="selectedTimeframe === tf.key"
            [class.text-white]="selectedTimeframe === tf.key"
            [class.bg-white]="selectedTimeframe !== tf.key"
            [class.text-gray-600]="selectedTimeframe !== tf.key"
            [class.border]="selectedTimeframe !== tf.key"
            [class.border-gray-200]="selectedTimeframe !== tf.key">
            {{ tf.label }}
          </button>

          <!-- Period stats badge (shown when not "all") -->
          <div *ngIf="selectedTimeframe !== 'all' && equityCurveData"
            class="ml-auto flex items-center gap-3 bg-white border border-gray-200 rounded-xl px-4 py-2 text-sm">
            <span class="text-gray-500">{{ getTimeframeLabel() }}:</span>
            <span class="font-bold"
              [class.text-emerald-600]="periodReturn >= 0"
              [class.text-red-600]="periodReturn < 0">
              {{ periodReturn >= 0 ? '+' : '' }}{{ periodReturn.toFixed(2) }}%
            </span>
            <span class="text-gray-400">|</span>
            <span class="font-semibold"
              [class.text-emerald-600]="periodPnL >= 0"
              [class.text-red-600]="periodPnL < 0">
              {{ periodPnL >= 0 ? '+' : '' }}{{ formatProjection(periodPnL) }}
            </span>
          </div>
          <div *ngIf="selectedTimeframe !== 'all' && !equityCurveData"
            class="ml-auto text-xs text-gray-400 italic">Chưa có dữ liệu equity curve</div>
        </div>

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
            <p class="text-2xl font-bold"
              [class.text-emerald-600]="cagrValue > 0"
              [class.text-red-600]="cagrValue < 0"
              [class.text-gray-900]="cagrValue === 0">
              {{ cagrValue !== 0 ? (cagrValue > 0 ? '+' : '') + cagrValue.toFixed(1) + '%' : '--' }}
            </p>
            <div class="mt-2 text-sm text-gray-400">
              {{ cagrValue !== 0 ? 'Lãi kép hàng năm' : 'Chưa đủ dữ liệu' }}
            </div>
          </div>
        </div>

        <!-- Compound Growth Tracker -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-8">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-gray-900">Lãi kép (Compound Growth)</h2>
            <button (click)="showTargetEditor = !showTargetEditor"
              class="text-xs text-blue-600 hover:text-blue-800 font-medium">
              {{ showTargetEditor ? 'Đóng' : 'Đặt mục tiêu' }}
            </button>
          </div>

          <!-- Target Editor -->
          <div *ngIf="showTargetEditor" class="mb-4 bg-blue-50 rounded-lg p-4 border border-blue-200">
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div>
                <label class="block text-xs font-medium text-gray-600 mb-1">CAGR mục tiêu (%/năm)</label>
                <input [(ngModel)]="cagrTarget" type="number" step="1" min="1" max="50"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
              </div>
              <div>
                <label class="block text-xs font-medium text-gray-600 mb-1">Kỳ hạn (năm)</label>
                <input [(ngModel)]="targetYears" type="number" step="1" min="1" max="30"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
              </div>
              <div class="col-span-2 flex items-end">
                <button (click)="applyTarget()" class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-medium transition-colors">
                  Áp dụng mục tiêu
                </button>
              </div>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
            <!-- Current CAGR vs Target -->
            <div class="text-center">
              <div class="text-xs text-gray-500 mb-1">CAGR hiện tại</div>
              <div class="text-3xl font-bold"
                [class.text-emerald-600]="cagrValue > 0"
                [class.text-red-600]="cagrValue < 0"
                [class.text-gray-400]="cagrValue === 0">
                {{ cagrValue !== 0 ? (cagrValue > 0 ? '+' : '') + cagrValue.toFixed(1) + '%' : '--' }}
              </div>
              <div *ngIf="cagrTargetSet" class="mt-1 text-sm">
                <span class="text-gray-500">Mục tiêu: </span>
                <span class="font-medium text-blue-600">{{ cagrTarget }}%</span>
              </div>
              <div *ngIf="cagrTargetSet && cagrValue !== 0" class="mt-2">
                <div class="w-full bg-gray-200 rounded-full h-2">
                  <div class="h-2 rounded-full transition-all duration-500"
                    [style.width.%]="getTargetProgress()"
                    [class.bg-emerald-500]="getTargetProgress() >= 100"
                    [class.bg-blue-500]="getTargetProgress() >= 50 && getTargetProgress() < 100"
                    [class.bg-amber-500]="getTargetProgress() < 50"></div>
                </div>
                <div class="text-xs text-gray-500 mt-1">{{ getTargetProgress().toFixed(0) }}% mục tiêu</div>
              </div>
            </div>

            <!-- Projected Values -->
            <div>
              <div class="text-xs text-gray-500 mb-2">Ước tính giá trị vốn (CAGR hiện tại)</div>
              <div class="space-y-2">
                <div *ngFor="let proj of projections" class="flex justify-between items-center text-sm">
                  <span class="text-gray-600">{{ proj.label }}</span>
                  <span class="font-bold text-gray-800">{{ formatProjection(proj.value) }}</span>
                </div>
              </div>
            </div>

            <!-- Target vs Actual -->
            <div *ngIf="cagrTargetSet">
              <div class="text-xs text-gray-500 mb-2">Mục tiêu vs Thực tế ({{ targetYears }} năm)</div>
              <div class="space-y-2">
                <div class="flex justify-between items-center text-sm">
                  <span class="text-gray-600">Vốn hiện tại</span>
                  <span class="font-bold">{{ formatProjection(pnlSummary.totalPortfolioValue || pnlSummary.totalInvested) }}</span>
                </div>
                <div class="flex justify-between items-center text-sm">
                  <span class="text-blue-600">Mục tiêu</span>
                  <span class="font-bold text-blue-600">{{ formatProjection(targetValue) }}</span>
                </div>
                <div class="flex justify-between items-center text-sm">
                  <span class="text-emerald-600">Ước tính thực tế</span>
                  <span class="font-bold text-emerald-600">{{ formatProjection(actualProjection) }}</span>
                </div>
              </div>
            </div>
            <div *ngIf="!cagrTargetSet">
              <div class="flex flex-col items-center justify-center h-full text-gray-400 text-sm">
                <p>Đặt mục tiêu CAGR để so sánh</p>
                <p class="text-xs">VD: 15%/năm trong 10 năm</p>
              </div>
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

        <!-- Row 2.5: Mini Equity Curve -->
        <div *ngIf="equityCurveData && equityCurveData.points.length > 1" class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-8">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-gray-900">Equity Curve</h2>
            <div class="flex space-x-2">
              <button *ngFor="let range of equityRanges"
                (click)="setEquityRange(range.days)"
                class="px-3 py-1 text-xs font-medium rounded-full transition-colors"
                [class.bg-blue-600]="selectedRange === range.days"
                [class.text-white]="selectedRange === range.days"
                [class.bg-gray-100]="selectedRange !== range.days"
                [class.text-gray-600]="selectedRange !== range.days">
                {{ range.label }}
              </button>
            </div>
          </div>
          <div class="h-48">
            <canvas #miniEquityCanvas></canvas>
          </div>
        </div>

        <!-- Top Positions Widget -->
        <div *ngIf="topPositions.length > 0" class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-8">
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-2">
              <h2 class="text-base font-semibold text-gray-900">Vị thế nổi bật</h2>
              <span class="text-xs text-gray-400">(Top {{ topPositions.length }})</span>
            </div>
            <a routerLink="/positions" class="text-sm text-blue-600 hover:text-blue-800 font-medium">Xem tất cả</a>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            <div *ngFor="let pos of topPositions"
              class="border rounded-lg p-3 hover:shadow-sm transition-shadow"
              [class.border-green-200]="pos.unrealizedPnL >= 0"
              [class.border-red-200]="pos.unrealizedPnL < 0">
              <div class="flex items-center justify-between mb-1">
                <span class="font-bold text-sm text-gray-800">{{ pos.symbol }}</span>
                <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                  [class.bg-green-100]="pos.unrealizedPnL >= 0" [class.text-green-700]="pos.unrealizedPnL >= 0"
                  [class.bg-red-100]="pos.unrealizedPnL < 0" [class.text-red-700]="pos.unrealizedPnL < 0">
                  {{ pos.unrealizedPnL >= 0 ? '+' : '' }}{{ pos.unrealizedPnLPercent | number:'1.1-1' }}%
                </span>
              </div>
              <div class="text-xs text-gray-500">{{ pos.quantity | number:'1.0-0' }} CP &#64; {{ pos.averageCost | number:'1.0-0' }}</div>
              <div class="text-xs mt-1">
                <span class="text-gray-500">Giá trị:</span>
                <span class="font-medium ml-1">{{ pos.marketValue | vndCurrency }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Quick Trade Widget -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-200 mb-8">
          <button (click)="qtExpanded = !qtExpanded"
            class="w-full flex items-center justify-between px-6 py-4 hover:bg-gray-50 transition-colors rounded-xl">
            <div class="flex items-center gap-2">
              <span class="text-lg">⚡</span>
              <h2 class="text-base font-semibold text-gray-900">Giao dịch nhanh</h2>
              <span class="text-xs text-gray-400">Tính position size tại chỗ → mở Trade Plan</span>
            </div>
            <svg class="w-4 h-4 text-gray-400 transition-transform" [class.rotate-180]="qtExpanded"
              fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
            </svg>
          </button>

          <div *ngIf="qtExpanded" class="px-6 pb-6 border-t border-gray-100 pt-4">
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
              <!-- Symbol -->
              <div>
                <label class="block text-xs font-medium text-gray-600 mb-1">Mã CP</label>
                <div class="relative">
                  <input [(ngModel)]="qt.symbol" (blur)="onQtSymbolBlur()" appUppercase
                    type="text" placeholder="VNM, VIC..."
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
                  <span *ngIf="qtLoading" class="absolute right-2 top-2.5 text-xs text-gray-400">...</span>
                  <span *ngIf="qtFetchedPrice && !qtLoading"
                    class="absolute right-2 top-2 text-xs font-medium text-emerald-600">
                    {{ qtFetchedPrice.toLocaleString('vi-VN') }}
                  </span>
                </div>
              </div>

              <!-- Direction -->
              <div>
                <label class="block text-xs font-medium text-gray-600 mb-1">Chiều</label>
                <div class="flex rounded-lg overflow-hidden border border-gray-300">
                  <button (click)="qt.direction='Buy'"
                    class="flex-1 py-2 text-sm font-medium transition-colors"
                    [class.bg-emerald-500]="isBuyTrade(qt.direction)" [class.text-white]="isBuyTrade(qt.direction)"
                    [class.text-gray-600]="!isBuyTrade(qt.direction)">Mua</button>
                  <button (click)="qt.direction='Sell'"
                    class="flex-1 py-2 text-sm font-medium transition-colors"
                    [class.bg-red-500]="!isBuyTrade(qt.direction)" [class.text-white]="!isBuyTrade(qt.direction)"
                    [class.text-gray-600]="isBuyTrade(qt.direction)">Bán</button>
                </div>
              </div>

              <!-- Entry -->
              <div>
                <label class="block text-xs font-medium text-gray-600 mb-1">Giá vào</label>
                <input [(ngModel)]="qt.entryPrice" (ngModelChange)="calcQtStats()" type="number"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
              </div>

              <!-- Stop-loss -->
              <div>
                <label class="block text-xs font-medium text-gray-600 mb-1">Stop-loss</label>
                <input [(ngModel)]="qt.stopLoss" (ngModelChange)="calcQtStats()" type="number"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
              </div>
            </div>

            <!-- Stats row -->
            <div *ngIf="qt.entryPrice && qt.stopLoss" class="flex flex-wrap items-center gap-4 mb-4 p-3 bg-gray-50 rounded-lg text-sm">
              <div>
                <span class="text-gray-500">Rủi ro/CP: </span>
                <span class="font-semibold text-red-600">{{ (qt.entryPrice - qt.stopLoss).toLocaleString('vi-VN') }} đ</span>
              </div>
              <div *ngIf="qtOptimalShares > 0">
                <span class="text-gray-500">Số CP đề xuất: </span>
                <span class="font-bold text-blue-600">{{ qtOptimalShares | number }}</span>
              </div>
              <div *ngIf="qtOptimalShares > 0">
                <span class="text-gray-500">Giá trị vị thế: </span>
                <span class="font-semibold">{{ (qtOptimalShares * qt.entryPrice) | vndCurrency }}</span>
              </div>
              <div *ngIf="!qtRiskProfile" class="text-amber-600 text-xs">
                Chưa có Risk Profile → không tính được số CP tối ưu
              </div>
            </div>

            <!-- Portfolio selector + action -->
            <div class="flex flex-wrap items-center gap-3">
              <select [(ngModel)]="qt.portfolioId"
                class="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
                <option value="">-- Chọn danh mục --</option>
                <option *ngFor="let p of portfolios" [value]="p.portfolioId">{{ p.portfolioName }}</option>
              </select>
              <button (click)="openInTradePlan()" [disabled]="!qt.symbol"
                class="px-5 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 text-white rounded-lg text-sm font-medium transition-colors">
                Mở trong Trade Plan →
              </button>
            </div>
          </div>
        </div>

        <!-- Row 3: Quick Actions -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-8">
          <h2 class="text-lg font-semibold text-gray-900 mb-4">Thao tác nhanh</h2>
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
            <a
              routerLink="/trade-wizard"
              class="flex flex-col items-center p-4 rounded-xl border-2 border-gray-100 hover:border-blue-200 hover:bg-blue-50 transition-all duration-200 group cursor-pointer"
            >
              <div class="w-12 h-12 bg-blue-100 group-hover:bg-blue-200 rounded-xl flex items-center justify-center mb-3 transition-colors">
                <svg class="w-6 h-6 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"></path>
                </svg>
              </div>
              <span class="text-sm font-medium text-gray-700 group-hover:text-blue-700">Wizard Giao dịch</span>
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
export class DashboardComponent implements OnInit, OnDestroy {
  @ViewChild('miniEquityCanvas') miniEquityCanvas!: ElementRef<HTMLCanvasElement>;
  private miniEquityChart: Chart | null = null;

  currentUser: User | null = null;
  summary: OverallPnLSummary | null = null;
  isLoading = true;
  riskAlerts: RiskAlert[] = [];
  bannerDismissed = false;
  cagrValue = 0;
  cagrTarget = 15; // default CAGR target %
  cagrTargetSet = false;
  targetYears = 10;
  showTargetEditor = false;
  targetValue = 0;
  actualProjection = 0;
  projections: { label: string; value: number }[] = [];
  equityCurveData: EquityCurveData | null = null;
  selectedRange = 90;
  equityRanges = [
    { label: '30D', days: 30 },
    { label: '90D', days: 90 },
    { label: '1Y', days: 365 },
    { label: 'All', days: 0 }
  ];

  allocationColors = [
    '#3b82f6', '#10b981', '#8b5cf6', '#f59e0b',
    '#ef4444', '#06b6d4', '#ec4899', '#14b8a6'
  ];

  get hasDangerAlert(): boolean {
    return this.riskAlerts.some(a => a.severity === 'danger');
  }

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

  // ─── Multi-timeframe ──────────────────────────────────────────────────────
  selectedTimeframe = 'all';
  timeframes = [
    { key: 'today', label: 'Hôm nay' },
    { key: 'week',  label: 'Tuần này' },
    { key: 'month', label: 'Tháng này' },
    { key: 'year',  label: 'Năm nay' },
    { key: 'all',   label: 'Toàn bộ' },
  ];
  periodReturn = 0;
  periodPnL = 0;

  // ─── Shared utilities ────────────────────────────────────────────────────
  isBuyTrade = isBuyTrade;

  // ─── Market Overview ─────────────────────────────────────────────────────
  marketOverview: MarketOverview[] = [];

  // ─── Positions Widget ────────────────────────────────────────────────────
  topPositions: ActivePosition[] = [];

  // ─── Quick Trade Widget ───────────────────────────────────────────────────
  qtExpanded = false;
  qtLoading = false;
  qt = { symbol: '', direction: 'Buy', entryPrice: 0, stopLoss: 0, portfolioId: '' };
  qtFetchedPrice: number | null = null;
  qtRR = 0;
  qtOptimalShares = 0;
  qtRiskProfile: RiskProfile | null = null;

  constructor(
    private authService: AuthService,
    private pnlService: PnlService,
    private riskService: RiskService,
    private advancedAnalyticsService: AdvancedAnalyticsService,
    private positionsService: PositionsService,
    private notificationService: NotificationService,
    private marketDataService: MarketDataService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.authService.getCurrentUser().subscribe(user => {
      this.currentUser = user;
    });
    this.loadDashboardData();
    this.loadTopPositions();
    this.loadMarketOverview();
  }

  private loadMarketOverview(): void {
    this.marketDataService.getMarketOverview().subscribe({
      next: data => this.marketOverview = data,
      error: () => {}
    });
  }

  private loadTopPositions(): void {
    this.positionsService.getAll().subscribe({
      next: (positions) => {
        // Sort by market value descending, take top 6
        this.topPositions = positions
          .sort((a, b) => b.marketValue - a.marketValue)
          .slice(0, 6);
      },
      error: () => {}
    });
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
        this.cagrValue = 0; // Will be set by equity curve or backend CAGR
        this.loadRiskAlerts(data);
        this.loadEquityCurve();
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
    const profileRequests = summary.portfolios.map(p =>
      this.riskService.getRiskProfile(p.portfolioId).pipe(catchError(() => of(null)))
    );

    forkJoin([forkJoin(riskRequests), forkJoin(profileRequests)]).subscribe({
      next: ([riskSummaries, profiles]) => {
        const alerts: RiskAlert[] = [];

        riskSummaries.forEach((risk, index) => {
          const portfolio = summary.portfolios[index];
          const profile = profiles[index];

          risk.positions.forEach((pos: PositionRiskItem) => {
            // Stop-loss proximity alert
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

            // Concentration alert: position > maxPositionSizePercent from risk profile
            if (profile && pos.positionSizePercent > profile.maxPositionSizePercent) {
              alerts.push({
                symbol: pos.symbol,
                portfolioName: portfolio.portfolioName,
                type: 'stop-loss',
                message: `Tập trung quá mức: ${pos.positionSizePercent.toFixed(1)}% danh mục (giới hạn ${profile.maxPositionSizePercent}%)`,
                severity: pos.positionSizePercent > profile.maxPositionSizePercent * 1.5 ? 'danger' : 'warning',
                value: pos.positionSizePercent
              });
            }
          });

          // Drawdown alert
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

        alerts.sort((a, b) => b.value - a.value);
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

  applyTarget(): void {
    this.cagrTargetSet = true;
    this.showTargetEditor = false;
    this.calculateProjections();
  }

  getTargetProgress(): number {
    if (!this.cagrTargetSet || this.cagrTarget <= 0) return 0;
    return Math.min(200, (this.cagrValue / this.cagrTarget) * 100);
  }

  formatProjection(value: number): string {
    if (value >= 1e9) return (value / 1e9).toFixed(1) + ' tỷ';
    if (value >= 1e6) return (value / 1e6).toFixed(1) + ' triệu';
    return value.toLocaleString('vi-VN') + ' đ';
  }

  calculateProjections(): void {
    const currentValue = this.pnlSummary.totalPortfolioValue || this.pnlSummary.totalInvested;
    if (currentValue <= 0 || !isFinite(this.cagrValue) || this.cagrValue === 0) {
      this.projections = [];
      this.targetValue = 0;
      this.actualProjection = 0;
      return;
    }

    const rate = this.cagrValue / 100;
    const cap = 1e15; // Cap projections at 1 quadrillion
    this.projections = [
      { label: 'Sau 5 năm', value: Math.min(cap, currentValue * Math.pow(1 + rate, 5)) },
      { label: 'Sau 10 năm', value: Math.min(cap, currentValue * Math.pow(1 + rate, 10)) },
      { label: 'Sau 20 năm', value: Math.min(cap, currentValue * Math.pow(1 + rate, 20)) },
    ];

    // Target vs actual
    const targetRate = this.cagrTarget / 100;
    this.targetValue = Math.min(cap, currentValue * Math.pow(1 + targetRate, this.targetYears));
    this.actualProjection = Math.min(cap, currentValue * Math.pow(1 + rate, this.targetYears));
  }

  ngOnDestroy(): void {
    this.miniEquityChart?.destroy();
  }

  private calculateCagr(): void {
    const invested = this.pnlSummary.totalInvested;
    const current = this.pnlSummary.totalPortfolioValue;
    if (invested <= 0 || current <= 0) { this.cagrValue = 0; return; }

    const totalReturn = current / invested;
    const years = 1;
    const cagr = (Math.pow(totalReturn, 1 / years) - 1) * 100;
    this.cagrValue = isFinite(cagr) ? Math.max(-99.99, Math.min(9999.99, cagr)) : 0;
    this.calculateProjections();
  }

  private loadEquityCurve(): void {
    if (!this.summary?.portfolios?.length) return;
    const firstPortfolioId = this.summary.portfolios[0].portfolioId;

    this.advancedAnalyticsService.getEquityCurve(firstPortfolioId).subscribe({
      next: (data) => {
        this.equityCurveData = data;
        if (data.points.length > 1) {
          this.calculateCagrFromCurve(data);
          this.computePeriodStats();
          setTimeout(() => this.renderMiniEquityChart());
          // If curve didn't produce CAGR (< 30 days), still try backend
          if (this.cagrValue === 0) {
            this.loadBackendCagr(firstPortfolioId);
          }
        } else {
          // No equity curve snapshots — use backend-calculated CAGR
          this.loadBackendCagr(firstPortfolioId);
        }
      },
      error: () => {
        this.equityCurveData = null;
        this.loadBackendCagr(firstPortfolioId);
      }
    });
  }

  private loadBackendCagr(portfolioId: string): void {
    this.advancedAnalyticsService.getPerformance(portfolioId).subscribe({
      next: (perf) => {
        if (perf.cagr !== 0 && isFinite(perf.cagr)) {
          this.cagrValue = Math.max(-99.99, Math.min(9999.99, perf.cagr));
          this.calculateProjections();
        }
      },
      error: () => {}
    });
  }

  private calculateCagrFromCurve(data: EquityCurveData): void {
    if (!data.points.length) return;
    const first = data.points[0];
    const last = data.points[data.points.length - 1];
    if (!first.portfolioValue || first.portfolioValue <= 0) return;
    if (!last.portfolioValue || last.portfolioValue <= 0) return;

    const startDate = new Date(first.date);
    const endDate = new Date(last.date);
    const diffDays = (endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24);
    const years = diffDays / 365.25;

    if (years >= 0.08) { // ~30 days minimum for meaningful CAGR
      const totalReturn = last.portfolioValue / first.portfolioValue;
      const cagr = (Math.pow(totalReturn, 1 / years) - 1) * 100;
      if (isFinite(cagr)) {
        this.cagrValue = Math.max(-99.99, Math.min(9999.99, cagr));
        this.calculateProjections();
      }
    }
  }

  setEquityRange(days: number): void {
    this.selectedRange = days;
    setTimeout(() => this.renderMiniEquityChart());
  }

  // ─── Multi-timeframe ──────────────────────────────────────────────────────
  getTimeframeLabel(): string {
    return this.timeframes.find(t => t.key === this.selectedTimeframe)?.label ?? '';
  }

  setTimeframe(key: string): void {
    this.selectedTimeframe = key;
    this.computePeriodStats();
  }

  private computePeriodStats(): void {
    if (!this.equityCurveData?.points?.length) { this.periodReturn = 0; this.periodPnL = 0; return; }
    const points = this.equityCurveData.points;
    const now = new Date();
    let cutoff: Date;
    switch (this.selectedTimeframe) {
      case 'today': cutoff = new Date(now.getFullYear(), now.getMonth(), now.getDate()); break;
      case 'week':  cutoff = new Date(now); cutoff.setDate(now.getDate() - 7); break;
      case 'month': cutoff = new Date(now.getFullYear(), now.getMonth(), 1); break;
      case 'year':  cutoff = new Date(now.getFullYear(), 0, 1); break;
      default:      this.periodReturn = this.cagrValue; this.periodPnL = this.totalPnL; return;
    }
    const filtered = points.filter(p => new Date(p.date) >= cutoff);
    if (filtered.length < 1) { this.periodReturn = 0; this.periodPnL = 0; return; }
    const first = filtered[0].portfolioValue;
    const last  = filtered[filtered.length - 1].portfolioValue;
    this.periodPnL    = last - first;
    this.periodReturn = first > 0 ? ((last - first) / first) * 100 : 0;
  }

  // ─── Quick Trade Widget ───────────────────────────────────────────────────
  onQtSymbolBlur(): void {
    const sym = this.qt.symbol?.trim().toUpperCase();
    if (!sym) return;
    this.qt.symbol = sym;
    this.qtLoading = true;
    this.qtFetchedPrice = null;
    this.marketDataService.getCurrentPrice(sym).subscribe({
      next: (data) => {
        this.qtLoading = false;
        this.qtFetchedPrice = data.close;
        if (!this.qt.entryPrice) this.qt.entryPrice = data.close;
        this.calcQtStats();
      },
      error: () => { this.qtLoading = false; }
    });
    // Load risk profile for first portfolio if not yet loaded
    if (!this.qtRiskProfile && this.portfolios.length > 0) {
      this.riskService.getRiskProfile(this.portfolios[0].portfolioId).pipe(catchError(() => of(null)))
        .subscribe(p => { this.qtRiskProfile = p; this.calcQtStats(); });
    }
  }

  calcQtStats(): void {
    const { entryPrice, stopLoss } = this.qt;
    if (!entryPrice || !stopLoss || entryPrice <= stopLoss) { this.qtRR = 0; this.qtOptimalShares = 0; return; }
    const riskPerShare = entryPrice - stopLoss;
    const totalValue = this.pnlSummary.totalPortfolioValue || this.pnlSummary.totalInvested;
    if (this.qtRiskProfile && totalValue > 0) {
      const maxRisk = totalValue * (this.qtRiskProfile.maxPortfolioRiskPercent / 100);
      this.qtOptimalShares = Math.floor(maxRisk / riskPerShare);
    }
    this.qtRR = 0; // R:R requires target — shown as 0 when no target
  }

  openInTradePlan(): void {
    this.router.navigate(['/trade-plan'], { queryParams: {
      symbol:    this.qt.symbol,
      direction: this.qt.direction,
      entry:     this.qt.entryPrice,
      sl:        this.qt.stopLoss,
      portfolio: this.qt.portfolioId || (this.portfolios[0]?.portfolioId ?? ''),
    }});
  }

  private renderMiniEquityChart(): void {
    if (!this.miniEquityCanvas?.nativeElement || !this.equityCurveData?.points?.length) return;
    this.miniEquityChart?.destroy();

    let points = this.equityCurveData.points;
    if (this.selectedRange > 0) {
      const cutoff = new Date();
      cutoff.setDate(cutoff.getDate() - this.selectedRange);
      points = points.filter(p => new Date(p.date) >= cutoff);
    }
    if (points.length < 2) return;

    const labels = points.map(p => new Date(p.date).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' }));
    const values = points.map(p => p.portfolioValue);
    const isPositive = values[values.length - 1] >= values[0];

    this.miniEquityChart = new Chart(this.miniEquityCanvas.nativeElement, {
      type: 'line',
      data: {
        labels,
        datasets: [{
          data: values,
          borderColor: isPositive ? '#10b981' : '#ef4444',
          backgroundColor: isPositive ? 'rgba(16,185,129,0.1)' : 'rgba(239,68,68,0.1)',
          fill: true,
          tension: 0.3,
          pointRadius: 0,
          pointHoverRadius: 4,
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
              label: (ctx) => {
                const v = ctx.parsed.y ?? 0;
                if (Math.abs(v) >= 1e9) return (v / 1e9).toFixed(1) + ' tỷ';
                if (Math.abs(v) >= 1e6) return (v / 1e6).toFixed(1) + ' tr';
                return v.toLocaleString('vi-VN') + ' đ';
              }
            }
          }
        },
        scales: {
          y: {
            ticks: {
              callback: (v) => {
                const n = Number(v);
                if (Math.abs(n) >= 1e9) return (n / 1e9).toFixed(0) + 'B';
                if (Math.abs(n) >= 1e6) return (n / 1e6).toFixed(0) + 'M';
                return n.toLocaleString('vi-VN');
              }
            },
            grid: { color: 'rgba(0,0,0,0.04)' }
          },
          x: {
            grid: { display: false },
            ticks: { maxTicksLimit: 8 }
          }
        }
      }
    });
  }
}
