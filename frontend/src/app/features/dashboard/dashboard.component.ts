import { Component, OnInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { AuthService, User } from '../../core/services/auth.service';
import { PnlService, OverallPnLSummary, PortfolioPnL, PositionPnL } from '../../core/services/pnl.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { RiskService, PortfolioRiskSummary, PositionRiskItem, RiskProfile } from '../../core/services/risk.service';
import { AdvancedAnalyticsService, EquityCurveData } from '../../core/services/advanced-analytics.service';
import { MarketDataService, MarketOverview, BatchPrice } from '../../core/services/market-data.service';
import { PositionsService, ActivePosition } from '../../core/services/positions.service';
import { NotificationService } from '../../core/services/notification.service';
import { DailyRoutineService, DailyRoutine, RoutineTemplate } from '../../core/services/daily-routine.service';
import { WatchlistService } from '../../core/services/watchlist.service';
import { CapitalFlowService, AdjustedReturn, CapitalFlowItem } from '../../core/services/capital-flow.service';
import { JournalEntryService, PendingReviewTrade } from '../../core/services/journal-entry.service';
import { TradePlanService, ScenarioAdvisoryDto } from '../../core/services/trade-plan.service';
import { PersonalFinanceService, NetWorthSummaryDto } from '../../core/services/personal-finance.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';
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
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe, UppercaseDirective, AiChatPanelComponent],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 py-6">
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
              <button (click)="showAiPanel = true"
                class="bg-purple-600 hover:bg-purple-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200 flex items-center gap-1">
                🤖 AI Bản tin Hôm nay
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content -->
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

        <!-- Market Overview Strip -->
        <div *ngIf="marketOverview.length > 0" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
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
            <a routerLink="/risk-dashboard"
              class="inline-block mt-2 text-sm font-medium transition-colors"
              [class.text-red-700]="hasDangerAlert" [class.hover:text-red-900]="hasDangerAlert"
              [class.text-amber-700]="!hasDangerAlert" [class.hover:text-amber-900]="!hasDangerAlert">
              Quản lý Rủi ro →
            </a>
          </div>
        </div>

        <!-- Advisory Widget (P0.5) — Gợi ý hành động -->
        @if (advisories.length > 0) {
        <div class="mb-6 bg-amber-50 rounded-xl shadow-sm border border-amber-300 p-4">
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-sm font-semibold text-amber-900 flex items-center gap-2">
              <svg class="w-4 h-4 text-amber-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"></path>
              </svg>
              Gợi ý hành động
              <span class="bg-amber-200 text-amber-800 text-xs font-bold px-2 py-0.5 rounded-full">{{ advisories.length }}</span>
            </h2>
          </div>
          <div class="space-y-2">
            @for (adv of advisories; track adv.nodeId) {
            <div class="bg-white rounded-lg border border-amber-200 p-3 flex items-center justify-between gap-3">
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2 mb-0.5">
                  <span class="font-bold text-sm text-gray-900">{{ adv.symbol }}</span>
                  <span class="text-xs text-blue-700 font-medium">{{ adv.currentPrice | vndCurrency }}</span>
                </div>
                <div class="text-xs text-amber-800 font-medium">{{ adv.nodeLabel }}</div>
                <div class="text-xs text-gray-600">{{ adv.conditionDescription }}</div>
                <div *ngIf="adv.message" class="text-xs text-gray-500 italic mt-0.5">{{ adv.message }}</div>
              </div>
              <a [routerLink]="['/trade-plan']" [queryParams]="{ loadPlan: adv.tradePlanId }"
                class="flex-shrink-0 px-3 py-1.5 text-xs bg-amber-600 hover:bg-amber-700 text-white rounded-lg font-medium transition-colors whitespace-nowrap">
                Xem KH →
              </a>
            </div>
            }
          </div>
        </div>
        }

        <!-- Smart Nudge: Capital Flow -->
        <div *ngIf="capitalFlowNudge.show" class="mb-6 bg-blue-50 rounded-xl p-4 border border-blue-200 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <span class="text-2xl">💰</span>
            <div>
              <p class="text-sm font-medium text-blue-800">{{ capitalFlowNudge.message }}</p>
              <a routerLink="/capital-flows" class="text-xs text-blue-600 hover:text-blue-800 font-medium">Ghi nhận dòng vốn →</a>
            </div>
          </div>
          <button (click)="dismissNudge()" class="text-gray-400 hover:text-gray-600 ml-4">
            <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>

        <!-- Watchlist + Daily Routine side-by-side on desktop -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 mb-6">

        <!-- Watchlist Widget -->
        <div *ngIf="watchlistTopMovers.length > 0" class="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
          <div class="flex items-center justify-between mb-3">
            <div class="flex items-center gap-2">
              <span class="text-lg">⭐</span>
              <span class="font-bold text-sm text-gray-900">Watchlist</span>
              <span class="text-xs text-gray-500">{{ watchlistTopMovers.length }} mã</span>
            </div>
            <a routerLink="/watchlist" class="text-xs text-blue-600 hover:text-blue-800 font-medium">Xem tất cả →</a>
          </div>
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-2">
            <a *ngFor="let item of watchlistTopMovers.slice(0, 6)"
              [routerLink]="'/market-data'" [queryParams]="{ symbol: item.symbol }"
              class="p-2 rounded-lg border border-gray-100 hover:border-blue-200 hover:bg-blue-50/30 transition-colors text-center">
              <div class="font-semibold text-sm text-gray-900">{{ item.symbol }}</div>
              <div class="text-sm font-mono mt-0.5">{{ item.close | vndCurrency }}</div>
            </a>
          </div>
        </div>

        <!-- Daily Routine Widget -->
        <div *ngIf="todayRoutine || suggestedRoutineTemplate" class="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
          <!-- Has routine -->
          <div *ngIf="todayRoutine">
            <div class="flex items-center justify-between mb-3">
              <div class="flex items-center gap-2">
                <span class="text-lg">📋</span>
                <span class="font-bold text-sm text-gray-900">Nhiệm vụ hôm nay</span>
                <span class="text-xs text-gray-500">{{ todayRoutine.templateName }}</span>
              </div>
              <div class="flex items-center gap-2">
                <div *ngIf="todayRoutine.currentStreak >= 3"
                  class="flex items-center gap-1 bg-orange-50 text-orange-600 text-xs font-bold px-2 py-1 rounded-full">
                  <span>🔥</span> {{ todayRoutine.currentStreak }}
                </div>
                <a routerLink="/daily-routine" class="text-xs text-blue-600 hover:text-blue-800 font-medium">Xem chi tiết →</a>
              </div>
            </div>
            <div class="w-full bg-gray-200 rounded-full h-2 mb-3">
              <div class="h-2 rounded-full transition-all duration-500"
                [class.bg-emerald-500]="todayRoutine.progressPercent >= 50"
                [class.bg-amber-500]="todayRoutine.progressPercent > 0 && todayRoutine.progressPercent < 50"
                [style.width.%]="todayRoutine.progressPercent"></div>
            </div>
            <div class="flex items-center justify-between text-xs text-gray-500 mb-3">
              <span>{{ todayRoutine.completedCount }}/{{ todayRoutine.totalCount }} bước</span>
              <span *ngIf="todayRoutine.isFullyCompleted" class="text-emerald-600 font-medium">✅ Hoàn thành!</span>
            </div>
            <!-- Show next uncompleted items (max 3) -->
            <div class="space-y-1.5">
              <div *ngFor="let item of getNextRoutineItems()" class="flex items-center gap-2">
                <button (click)="toggleRoutineItem(item)"
                  class="flex-shrink-0 w-5 h-5 rounded-full border-2 flex items-center justify-center transition-all"
                  [class.bg-emerald-500]="item.isCompleted" [class.border-emerald-500]="item.isCompleted"
                  [class.border-gray-300]="!item.isCompleted">
                  <svg *ngIf="item.isCompleted" class="w-3 h-3 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7"/>
                  </svg>
                </button>
                <span *ngIf="item.emoji" class="text-sm">{{ item.emoji }}</span>
                <span class="text-sm" [class.line-through]="item.isCompleted" [class.text-gray-400]="item.isCompleted"
                  [class.text-gray-700]="!item.isCompleted">{{ item.label }}</span>
                <a *ngIf="item.link && !item.isCompleted" [routerLink]="item.link"
                  class="text-gray-400 hover:text-blue-600 ml-auto">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/>
                  </svg>
                </a>
              </div>
            </div>
          </div>
          <!-- No routine yet — suggestion -->
          <div *ngIf="!todayRoutine && suggestedRoutineTemplate" class="flex items-center justify-between">
            <div class="flex items-center gap-3">
              <span class="text-2xl">{{ suggestedRoutineTemplate.emoji }}</span>
              <div>
                <div class="text-sm font-bold text-gray-900">{{ suggestedRoutineTemplate.name }}</div>
                <div class="text-xs text-gray-500">~{{ suggestedRoutineTemplate.estimatedMinutes }} phút · {{ suggestedRoutineTemplate.items.length }} bước</div>
              </div>
            </div>
            <button (click)="startDailyRoutine(suggestedRoutineTemplate.id)"
              class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors">
              Bắt đầu
            </button>
          </div>
        </div>

        </div> <!-- end Watchlist + Daily Routine grid -->

        <!-- Personal Finance Widget -->
        <a *ngIf="netWorthSummary && netWorthSummary.hasProfile" routerLink="/personal-finance"
           class="block bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6 hover:border-blue-300 transition-colors">
          <div class="flex items-center justify-between mb-2">
            <div class="flex items-center gap-2">
              <span class="text-lg">💰</span>
              <span class="font-bold text-sm text-gray-900">Tài chính cá nhân</span>
            </div>
            <span class="text-xs text-blue-600 font-medium">Xem chi tiết →</span>
          </div>
          <div class="flex items-baseline justify-between mb-1">
            <span class="text-xs text-gray-500">💎 Net Worth</span>
            <span class="text-lg font-bold"
                  [class.text-emerald-700]="netWorthSummary.netWorth >= 0"
                  [class.text-red-700]="netWorthSummary.netWorth < 0">
              {{ netWorthSummary.netWorth | vndCurrency }}
            </span>
          </div>
          <div class="flex items-baseline justify-between text-[11px] text-gray-500 mb-2">
            <span>Tổng tài sản {{ netWorthSummary.totalAssets | vndCurrency }}</span>
            <span *ngIf="netWorthSummary.totalDebt > 0">− Nợ {{ netWorthSummary.totalDebt | vndCurrency }}</span>
          </div>
          <div *ngIf="netWorthSummary.hasHighInterestConsumerDebt"
               class="bg-red-50 border border-red-200 rounded-lg px-2 py-1.5 text-[11px] text-red-700 mb-2">
            ⚠️ Bạn có nợ tiêu dùng lãi &gt; 20%/năm. Cân nhắc trả nợ trước khi đầu tư thêm.
          </div>
          <div class="w-full bg-gray-200 rounded-full h-2 mb-2 overflow-hidden">
            <div class="h-2 transition-all duration-500"
                 [class.bg-emerald-500]="netWorthSummary.healthScore >= 80"
                 [class.bg-amber-500]="netWorthSummary.healthScore >= 50 && netWorthSummary.healthScore < 80"
                 [class.bg-red-500]="netWorthSummary.healthScore < 50"
                 [style.width.%]="netWorthSummary.healthScore"></div>
          </div>
          <div class="flex items-center justify-between text-[11px] text-gray-600 mb-2">
            <span>Sức khỏe tài chính</span>
            <span class="font-semibold"
                  [class.text-emerald-600]="netWorthSummary.healthScore >= 80"
                  [class.text-amber-600]="netWorthSummary.healthScore >= 50 && netWorthSummary.healthScore < 80"
                  [class.text-red-600]="netWorthSummary.healthScore < 50">
              {{ netWorthSummary.healthScore }}/100
            </span>
          </div>
          <div class="flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-gray-600">
            <span>📈 CK: <span class="font-semibold">{{ netWorthSummary.securitiesValue | vndCurrency }}</span></span>
            <span *ngIf="netWorthSummary.goldTotal > 0">🪙 Vàng: <span class="font-semibold">{{ netWorthSummary.goldTotal | vndCurrency }}</span></span>
            <span>🏦 TK: <span class="font-semibold">{{ netWorthSummary.savingsTotal | vndCurrency }}</span></span>
            <span>🛡️ DP: <span class="font-semibold">{{ netWorthSummary.emergencyTotal | vndCurrency }}</span></span>
            <span>💵 NR: <span class="font-semibold">{{ netWorthSummary.idleCashTotal | vndCurrency }}</span></span>
          </div>
        </a>

        <!-- Personal Finance onboarding (chưa thiết lập) -->
        <a *ngIf="netWorthSummary && !netWorthSummary.hasProfile" routerLink="/personal-finance"
           class="block bg-gradient-to-r from-blue-50 to-indigo-50 rounded-xl border border-blue-200 p-4 mb-6 hover:border-blue-400 transition-colors">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-3">
              <span class="text-2xl">💰</span>
              <div>
                <div class="text-sm font-bold text-gray-900">Thiết lập tài chính cá nhân</div>
                <div class="text-xs text-gray-600">Theo dõi tổng tài sản + quỹ dự phòng + vàng tích trữ</div>
              </div>
            </div>
            <span class="text-xs text-blue-600 font-medium">Bắt đầu →</span>
          </div>
        </a>

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

        <!-- Hero: Tổng quan tài sản (matches /capital-flows layout) -->
        <div *ngIf="summary" class="bg-gradient-to-br from-emerald-50 via-white to-blue-50 rounded-xl shadow-sm border border-emerald-100 p-6 mb-8">
          <!-- Header + CAGR + TWR chips -->
          <div class="flex items-start justify-between flex-wrap gap-2 mb-2">
            <div>
              <h2 class="text-sm font-semibold text-emerald-900">Tổng tài sản</h2>
              <span class="text-xs text-gray-600">So với vốn hiện tại {{ totalCurrentCapital | vndCurrency }}</span>
            </div>
            <div class="flex items-center gap-2 flex-wrap">
              <span *ngIf="cagrValue !== 0"
                class="px-2.5 py-1 rounded-full text-xs font-semibold"
                [class.bg-violet-100]="cagrValue >= 0" [class.text-violet-700]="cagrValue >= 0"
                [class.bg-red-100]="cagrValue < 0" [class.text-red-700]="cagrValue < 0">
                CAGR {{ cagrValue > 0 ? '+' : '' }}{{ cagrValue.toFixed(1) }}%
              </span>
              <span *ngIf="adjustedReturn"
                class="px-2.5 py-1 rounded-full text-xs font-semibold"
                [class.bg-blue-100]="adjustedReturn.timeWeightedReturn >= 0" [class.text-blue-700]="adjustedReturn.timeWeightedReturn >= 0"
                [class.bg-red-100]="adjustedReturn.timeWeightedReturn < 0" [class.text-red-700]="adjustedReturn.timeWeightedReturn < 0"
                title="Time-Weighted Return — loại bỏ ảnh hưởng của dòng vốn vào/ra">
                TWR {{ adjustedReturn.timeWeightedReturn >= 0 ? '+' : '' }}{{ adjustedReturn.timeWeightedReturn.toFixed(2) }}%
              </span>
              <a routerLink="/capital-flows" class="text-xs font-medium text-blue-600 hover:text-blue-700">
                Quản lý dòng vốn →
              </a>
            </div>
          </div>

          <!-- Total assets + return -->
          <div class="flex items-baseline gap-3 mb-4 flex-wrap">
            <span class="text-3xl font-bold text-gray-900">{{ totalAssets | vndCurrency }}</span>
            <span *ngIf="totalCurrentCapital > 0" class="text-sm font-semibold"
              [class.text-emerald-600]="totalReturn >= 0" [class.text-red-600]="totalReturn < 0">
              {{ totalReturn >= 0 ? '↗ +' : '↘ −' }}{{ (totalReturn >= 0 ? totalReturn : -totalReturn) | vndCurrency }}
              ({{ totalReturn >= 0 ? '+' : '−' }}{{ (totalReturnPercent >= 0 ? totalReturnPercent : -totalReturnPercent).toFixed(2) }}%)
            </span>
          </div>

          <!-- Allocation bar -->
          <div class="flex h-3 rounded-full overflow-hidden bg-white mb-3">
            <div class="bg-blue-500 transition-all" [style.width.%]="marketBarWidth"></div>
            <div class="bg-cyan-400 transition-all" [style.width.%]="cashBarWidth"></div>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm mb-4">
            <div>
              <div class="flex items-center justify-between gap-2">
                <div class="flex items-center gap-2 min-w-0">
                  <span class="w-2 h-2 rounded-full bg-blue-500 shrink-0"></span>
                  <span class="text-gray-700 truncate">Giá trị thị trường</span>
                </div>
                <span class="text-sm font-bold text-blue-700 tabular-nums shrink-0">{{ marketBarWidth.toFixed(1) }}%</span>
              </div>
              <div class="font-semibold text-gray-900 mt-0.5">{{ marketValue | vndCurrency }}</div>
            </div>
            <div>
              <div class="flex items-center justify-between gap-2">
                <div class="flex items-center gap-2 min-w-0">
                  <span class="w-2 h-2 rounded-full bg-cyan-400 shrink-0"></span>
                  <span class="text-gray-700 truncate">Tiền mặt khả dụng</span>
                </div>
                <span class="text-sm font-bold text-cyan-700 tabular-nums shrink-0">{{ cashBarWidth.toFixed(1) }}%</span>
              </div>
              <div class="font-semibold mt-0.5" [ngClass]="cashBalance >= 0 ? 'text-gray-900' : 'text-red-600'">{{ cashBalance | vndCurrency }}</div>
            </div>
          </div>

          <!-- Breakdown -->
          <div class="pt-4 border-t border-emerald-200/60 grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
            <div>
              <div class="text-gray-600">Vốn ban đầu</div>
              <div class="font-medium text-gray-800 mt-0.5">{{ initialCapitalTotal | vndCurrency }}</div>
            </div>
            <div>
              <div class="text-gray-600">Dòng vốn ròng</div>
              <div class="font-medium mt-0.5" [ngClass]="netCashFlowTotal >= 0 ? 'text-green-600' : 'text-red-600'">
                {{ netCashFlowTotal >= 0 ? '+' : '' }}{{ netCashFlowTotal | vndCurrency }}
              </div>
            </div>
            <div>
              <div class="text-gray-600">Lãi/lỗ chưa TH</div>
              <div class="font-medium mt-0.5" [ngClass]="unrealizedPnLTotal >= 0 ? 'text-green-600' : 'text-red-600'">
                {{ unrealizedPnLTotal >= 0 ? '+' : '' }}{{ unrealizedPnLTotal | vndCurrency }}
              </div>
            </div>
            <div>
              <div class="text-gray-600">Lãi/lỗ đã TH</div>
              <div class="font-medium mt-0.5" [ngClass]="realizedPnLTotal >= 0 ? 'text-green-600' : 'text-red-600'">
                {{ realizedPnLTotal >= 0 ? '+' : '' }}{{ realizedPnLTotal | vndCurrency }}
              </div>
            </div>
          </div>
        </div>

        <!-- Compound Growth Tracker -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-8">
          <div class="flex items-center justify-between mb-4">
            <h2 class="tooltip-trigger text-lg font-semibold text-gray-900">Lãi kép (Compound Growth)
              <svg xmlns="http://www.w3.org/2000/svg" class="w-4 h-4 text-blue-400 inline-block ml-1" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd"/></svg>
              <div class="tooltip-box"><strong>CAGR — Tăng trưởng kép hàng năm:</strong> Mức lợi nhuận trung bình mỗi năm nếu danh mục tăng trưởng đều đặn. VD: CAGR 15% = vốn nhân đôi sau ~5 năm. Dùng CAGR để đặt mục tiêu dài hạn và so sánh với lãi suất tiết kiệm (~6%) hay VN-Index (~12%/năm).</div>
            </h2>
            <button (click)="showTargetEditor = !showTargetEditor"
              class="text-xs text-blue-600 hover:text-blue-800 font-medium">
              {{ showTargetEditor ? 'Đóng' : 'Đặt mục tiêu' }}
            </button>
          </div>

          <!-- Target Editor -->
          <div *ngIf="showTargetEditor" class="mb-4 bg-blue-50 rounded-lg p-4 border border-blue-200">
            <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
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

        <!-- Row 2: Allocation + Top Positions -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
          <!-- Danh mục của bạn -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Danh mục của bạn</h2>
            <div *ngIf="portfolios.length === 0" class="flex flex-col items-center justify-center py-8 text-gray-400">
              <p class="text-sm mb-3">Chưa có danh mục nào</p>
              <button routerLink="/portfolios/create"
                class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium text-sm transition-colors">
                Tạo danh mục đầu tiên
              </button>
            </div>
            <div *ngIf="portfolios.length > 0" class="space-y-3">
              <div *ngFor="let portfolio of portfolios"
                class="p-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition-colors cursor-pointer"
                [routerLink]="['/portfolios', portfolio.portfolioId]">
                <div class="flex items-center justify-between mb-1">
                  <h3 class="text-sm font-semibold text-gray-900 truncate mr-2">{{ portfolio.portfolioName }}</h3>
                  <span class="text-sm font-bold whitespace-nowrap"
                    [class.text-emerald-600]="safeNumber(portfolio.totalPnL) >= 0"
                    [class.text-red-600]="safeNumber(portfolio.totalPnL) < 0">
                    {{ safeNumber(portfolio.totalPnL) >= 0 ? '+' : '' }}{{ safeNumber(portfolio.totalPnLPercent).toFixed(2) }}%
                  </span>
                </div>
                <div class="flex items-center justify-between text-xs text-gray-500">
                  <span>{{ safeNumber(portfolio.totalMarketValue) | vndCurrency }}</span>
                  <span>Vốn: {{ portfolio.currentCapital | vndCurrency }}</span>
                </div>
                <div class="flex items-center gap-2 mt-2">
                  <span class="text-xs text-gray-400 whitespace-nowrap">{{ getAllocationPercent(portfolio).toFixed(1) }}%</span>
                  <div class="flex-1 bg-gray-100 rounded-full h-1.5">
                    <div class="h-1.5 rounded-full transition-all duration-500"
                      [style.width.%]="getAllocationPercent(portfolio)"
                      [style.background-color]="allocationColors[portfolios.indexOf(portfolio) % allocationColors.length]">
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Vị thế nổi bật -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div class="flex items-center justify-between mb-4">
              <div class="flex items-center gap-2">
                <h2 class="text-lg font-semibold text-gray-900">Vị thế nổi bật</h2>
                <span *ngIf="topPositions.length > 0" class="text-xs text-gray-400">(Top {{ topPositions.length }})</span>
              </div>
              <a routerLink="/positions" class="text-sm text-blue-600 hover:text-blue-800 font-medium">Xem tất cả</a>
            </div>
            <div *ngIf="topPositions.length === 0" class="text-center py-8 text-gray-400">
              Chưa có vị thế nào
            </div>
            <div *ngIf="topPositions.length > 0" class="space-y-3">
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
        </div>

        <!-- Mini Equity Curve -->
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
            <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 mb-4">
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

        <!-- Row 2.5: Chờ đánh giá -->
        @if (pendingReviewTrades.length > 0) {
        <div class="bg-white rounded-xl shadow-sm border border-amber-200 p-6 mb-8">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-gray-900 flex items-center gap-2">
              <svg class="w-5 h-5 text-amber-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
              Chờ đánh giá
              <span class="bg-amber-100 text-amber-700 text-xs font-bold px-2 py-0.5 rounded-full">{{ pendingReviewTrades.length }}</span>
            </h2>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            @for (trade of pendingReviewTrades.slice(0, 6); track trade.tradeId) {
            <a [routerLink]="['/symbol-timeline']" [queryParams]="{symbol: trade.symbol, tradeId: trade.tradeId}"
               class="flex items-center justify-between p-3 rounded-lg border border-gray-100 hover:border-amber-300 hover:bg-amber-50 transition-all cursor-pointer">
              <div>
                <span class="font-semibold text-gray-900">{{ trade.symbol }}</span>
                <span class="text-xs text-gray-500 ml-2">{{ trade.tradeDate | date:'dd/MM/yyyy' }}</span>
              </div>
              <div class="text-xs text-amber-600 font-medium">Đánh giá</div>
            </a>
            }
          </div>
        </div>
        }

        <!-- Row 3: Quick Actions -->
        <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-8">
          <h2 class="text-lg font-semibold text-gray-900 mb-4">Thao tác nhanh</h2>
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
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

      </div>
    </div>

    <app-ai-chat-panel
      [(isOpen)]="showAiPanel"
      title="Bản tin Đầu tư Hôm nay"
      useCase="daily-briefing"
      [contextData]="emptyContext">
    </app-ai-chat-panel>
  `,
  styles: []
})
export class DashboardComponent implements OnInit, OnDestroy {
  @ViewChild('miniEquityCanvas') miniEquityCanvas!: ElementRef<HTMLCanvasElement>;
  private miniEquityChart: Chart | null = null;

  showAiPanel = false;
  readonly emptyContext = {};
  currentUser: User | null = null;
  summary: OverallPnLSummary | null = null;
  portfolioSummaries: PortfolioSummary[] = [];
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

  // ─── Daily Routine Widget ──────────────────────────────────────────────────
  todayRoutine: DailyRoutine | null = null;
  suggestedRoutineTemplate: RoutineTemplate | null = null;

  // ─── Watchlist Widget ──────────────────────────────────────────────────
  watchlistTopMovers: BatchPrice[] = [];

  // ─── Pending Review Widget ──────────────────────────────────────────
  pendingReviewTrades: PendingReviewTrade[] = [];

  // ─── Advisory Widget (P0.5) ──────────────────────────────────────────
  advisories: ScenarioAdvisoryDto[] = [];

  // ─── Capital Flow Visibility ──────────────────────────────────────────
  // cashBalance / totalAssets derived via getters from portfolioSummaries + summary.
  // Using PortfolioSummary.totalInvested/totalSold (gross with fees) rather than
  // OverallPnLSummary.totalInvested (which is PnLService open-position cost without
  // fees) — otherwise dashboard diverges from /capital-flows by the total fees paid.

  get totalGrossInvested(): number {
    return this.portfolioSummaries.reduce((s, p) => s + p.totalInvested, 0);
  }

  get totalGrossSold(): number {
    return this.portfolioSummaries.reduce((s, p) => s + p.totalSold, 0);
  }

  get cashBalance(): number {
    const cap = this.summary?.totalCurrentCapital || 0;
    return cap - this.totalGrossInvested + this.totalGrossSold;
  }

  get marketValue(): number {
    return this.summary?.totalMarketValue || 0;
  }

  get totalAssets(): number {
    return this.cashBalance + this.marketValue;
  }

  get totalCurrentCapital(): number {
    return this.summary?.totalCurrentCapital || 0;
  }

  get totalReturn(): number {
    return this.totalAssets - this.totalCurrentCapital;
  }

  get totalReturnPercent(): number {
    return this.totalCurrentCapital > 0 ? (this.totalReturn / this.totalCurrentCapital) * 100 : 0;
  }

  get marketAllocationPercent(): number {
    return this.totalAssets > 0 ? (this.marketValue / this.totalAssets) * 100 : 0;
  }

  // Clamped widths for allocation bar — avoid overflow when cashBalance < 0.
  get marketBarWidth(): number {
    return Math.max(0, Math.min(100, this.marketAllocationPercent));
  }

  get cashBarWidth(): number {
    return 100 - this.marketBarWidth;
  }

  get initialCapitalTotal(): number {
    return this.summary?.totalInitialCapital || 0;
  }

  get netCashFlowTotal(): number {
    return this.summary?.totalNetCashFlow || 0;
  }

  get unrealizedPnLTotal(): number {
    return this.summary?.totalUnrealizedPnL || 0;
  }

  get realizedPnLTotal(): number {
    return this.summary?.totalRealizedPnL || 0;
  }
  adjustedReturn: AdjustedReturn | null = null;
  capitalFlowNudge: { show: boolean; message: string } = { show: false, message: '' };
  flowHistory: CapitalFlowItem[] = [];

  // ─── Personal Finance Widget ───────────────────────────────────────────────
  netWorthSummary: NetWorthSummaryDto | null = null;

  constructor(
    private authService: AuthService,
    private pnlService: PnlService,
    private riskService: RiskService,
    private advancedAnalyticsService: AdvancedAnalyticsService,
    private positionsService: PositionsService,
    private notificationService: NotificationService,
    private marketDataService: MarketDataService,
    private dailyRoutineService: DailyRoutineService,
    private watchlistService: WatchlistService,
    private capitalFlowService: CapitalFlowService,
    private journalEntryService: JournalEntryService,
    private tradePlanService: TradePlanService,
    private portfolioService: PortfolioService,
    private router: Router,
    private personalFinanceService: PersonalFinanceService
  ) {}

  ngOnInit(): void {
    this.authService.getCurrentUser().subscribe(user => {
      this.currentUser = user;
    });
    this.loadDashboardData();
    this.loadTopPositions();
    this.loadMarketOverview();
    this.loadDailyRoutine();
    this.loadWatchlistWidget();
    this.loadPendingReview();
    this.loadAdvisories();
    this.loadNetWorth();
  }

  private loadNetWorth(): void {
    this.personalFinanceService.getSummary().subscribe({
      next: (s) => { this.netWorthSummary = s; },
      error: (err) => {
        // Silent UI (widget ẩn) nhưng log console để dev diagnose khi /summary regression.
        console.error('[dashboard] loadNetWorth failed', err);
      },
    });
  }

  // ─── Daily Routine Widget ──────────────────────────────────────────────────
  private loadDailyRoutine(): void {
    forkJoin({
      routine: this.dailyRoutineService.getToday().pipe(catchError(() => of(null))),
      suggested: this.dailyRoutineService.getSuggestedTemplate().pipe(catchError(() => of(null)))
    }).subscribe(({ routine, suggested }) => {
      this.todayRoutine = routine;
      this.suggestedRoutineTemplate = suggested;
    });
  }

  getNextRoutineItems(): any[] {
    if (!this.todayRoutine) return [];
    // Show up to 4 items: first uncompleted from each group, then fill remaining
    const uncompleted = this.todayRoutine.items.filter(i => !i.isCompleted);
    const completed = this.todayRoutine.items.filter(i => i.isCompleted).slice(-1);
    return [...uncompleted.slice(0, 3), ...completed].slice(0, 4);
  }

  toggleRoutineItem(item: any): void {
    if (!this.todayRoutine) return;
    const newState = !item.isCompleted;
    item.isCompleted = newState; // optimistic
    this.dailyRoutineService.completeItem(this.todayRoutine.id, item.index, newState).subscribe({
      next: (r) => { this.todayRoutine = r; },
      error: () => { item.isCompleted = !newState; }
    });
  }

  startDailyRoutine(templateId: string): void {
    this.dailyRoutineService.getOrCreateToday(templateId).subscribe({
      next: (r) => { this.todayRoutine = r; },
      error: () => this.notificationService.error('Lỗi', 'Không thể tạo nhiệm vụ')
    });
  }

  // ─── Watchlist Widget ──────────────────────────────────────────────────
  private loadWatchlistWidget(): void {
    this.watchlistService.getAll().pipe(catchError(() => of([]))).subscribe(lists => {
      if (lists.length === 0) return;
      // Load detail of first watchlist
      const first = lists[0];
      this.watchlistService.getDetail(first.id).pipe(catchError(() => of(null))).subscribe(detail => {
        if (!detail || detail.items.length === 0) return;
        const symbols = detail.items.map(i => i.symbol).slice(0, 10);
        this.marketDataService.getBatchPrices(symbols).pipe(catchError(() => of([]))).subscribe(prices => {
          this.watchlistTopMovers = prices;
        });
      });
    });
  }

  private loadPendingReview(): void {
    this.journalEntryService.getPendingReview().subscribe({
      next: (trades) => this.pendingReviewTrades = trades,
      error: () => this.pendingReviewTrades = []
    });
  }

  // ─── Advisory Widget (P0.5) ──────────────────────────────────────────
  private loadAdvisories(): void {
    this.tradePlanService.getAdvisories().pipe(catchError(() => of([]))).subscribe(advisories => {
      this.advisories = advisories;
    });
  }

  // ─── Capital Flow Visibility ──────────────────────────────────────────
  private loadCapitalFlowData(): void {
    if (!this.summary?.portfolios?.length) return;
    const firstPortfolio = this.summary.portfolios[0];
    const portfolioId = firstPortfolio.portfolioId;

    // Load TWR/MWR
    this.capitalFlowService.getTimeWeightedReturn(portfolioId).pipe(catchError(() => of(null))).subscribe(data => {
      this.adjustedReturn = data;
      // TWR is flow-adjusted — prefer it over raw endpoint ratio for CAGR.
      if (this.equityCurveData) this.calculateCagrFromCurve(this.equityCurveData);
    });

    // Load flow history for chart markers + smart nudge
    this.capitalFlowService.getFlowHistory(portfolioId).pipe(catchError(() => of(null))).subscribe(history => {
      if (history) {
        this.flowHistory = history.flows || [];
        // Re-render chart with flow markers
        if (this.equityCurveData) {
          setTimeout(() => this.renderMiniEquityChart());
        }
        // Smart nudge: check if large equity change without recent flow
        this.checkSmartNudge(history.flows || []);
      }
    });
  }

  private checkSmartNudge(flows: CapitalFlowItem[]): void {
    if (!this.equityCurveData?.points?.length || this.equityCurveData.points.length < 2) return;
    const points = this.equityCurveData.points;
    const last = points[points.length - 1];
    const prev = points[points.length - 2];
    if (!prev.portfolioValue || prev.portfolioValue <= 0) return;

    const changePct = Math.abs((last.portfolioValue - prev.portfolioValue) / prev.portfolioValue) * 100;
    if (changePct < 20) return; // Only nudge for >20% daily change

    // Check if there's a recent flow (within 3 days)
    const lastDate = new Date(last.date);
    const hasRecentFlow = flows.some(f => {
      const fd = new Date(f.flowDate);
      return Math.abs(lastDate.getTime() - fd.getTime()) < 3 * 86400000;
    });

    if (!hasRecentFlow) {
      const direction = last.portfolioValue > prev.portfolioValue ? 'tăng' : 'giảm';
      this.capitalFlowNudge = {
        show: true,
        message: `Giá trị danh mục ${direction} ${changePct.toFixed(1)}% trong ngày gần nhất. Bạn có nạp/rút tiền không?`
      };
    }
  }

  dismissNudge(): void {
    this.capitalFlowNudge = { show: false, message: '' };
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
    if (portfolio.currentCapital === 0) return 0;
    return (this.safeNumber(portfolio.totalMarketValue) / portfolio.currentCapital) * 100;
  }

  getClampedPerformance(portfolio: PortfolioPnL): number {
    const perf = this.getPerformancePercent(portfolio);
    return Math.min(Math.max(perf, 0), 100);
  }

  private loadDashboardData(): void {
    this.isLoading = true;
    // Fetch gross totalInvested/totalSold (with fees) from PortfolioService so
    // cashBalance matches /capital-flows. OverallPnLSummary.totalInvested omits
    // fees (PnLService bug) so can't be used as-is.
    this.portfolioService.getAll().pipe(catchError(() => of([] as PortfolioSummary[]))).subscribe(ps => {
      this.portfolioSummaries = ps;
    });
    this.pnlService.getSummary().subscribe({
      next: (data) => {
        this.summary = data;
        this.isLoading = false;
        this.cagrValue = 0; // Will be set by equity curve or backend CAGR
        this.loadRiskAlerts(data);
        this.loadEquityCurve();
        this.loadCapitalFlowData();
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
        } else {
          // Fallback: tính CAGR đơn giản từ P&L khi không có snapshots
          this.calculateCagr();
        }
      },
      error: () => {
        // Fallback: tính CAGR đơn giản từ P&L
        this.calculateCagr();
      }
    });
  }

  // Derive CAGR by annualizing TWR (flow-adjusted) over the curve's span.
  // Raw endpoint ratio is wrong when flows exist: a portfolio that net-added
  // 150M and ended up 50% higher shows a huge fake CAGR; one that net-withdrew
  // shows a fake loss. TWR already nets out flows — annualize once.
  private calculateCagrFromCurve(data: EquityCurveData): void {
    if (!data.points.length) return;
    const first = data.points[0];
    const last = data.points[data.points.length - 1];

    const startDate = new Date(first.date);
    const endDate = new Date(last.date);
    const diffDays = (endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24);
    const years = diffDays / 365.25;

    if (years < 0.08) return; // ~30 days minimum for meaningful CAGR

    if (this.adjustedReturn && isFinite(this.adjustedReturn.timeWeightedReturn)) {
      const twrFraction = this.adjustedReturn.timeWeightedReturn / 100;
      if (twrFraction > -1) {
        const cagr = (Math.pow(1 + twrFraction, 1 / years) - 1) * 100;
        if (isFinite(cagr)) {
          this.cagrValue = Math.max(-99.99, Math.min(9999.99, cagr));
          this.calculateProjections();
          return;
        }
      }
    }

    // TWR unavailable — fallback to endpoint ratio. Accurate only if there
    // were no flows between first and last point.
    if (!first.portfolioValue || first.portfolioValue <= 0) return;
    if (!last.portfolioValue || last.portfolioValue <= 0) return;
    const totalReturn = last.portfolioValue / first.portfolioValue;
    const cagr = (Math.pow(totalReturn, 1 / years) - 1) * 100;
    if (isFinite(cagr)) {
      this.cagrValue = Math.max(-99.99, Math.min(9999.99, cagr));
      this.calculateProjections();
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

    // Build capital flow scatter data
    const datasets: any[] = [{
      data: values,
      borderColor: isPositive ? '#10b981' : '#ef4444',
      backgroundColor: isPositive ? 'rgba(16,185,129,0.1)' : 'rgba(239,68,68,0.1)',
      fill: true,
      tension: 0.3,
      pointRadius: 0,
      pointHoverRadius: 4,
      borderWidth: 2
    }];

    if (this.flowHistory.length > 0) {
      const depositData: (number | null)[] = new Array(labels.length).fill(null);
      const withdrawData: (number | null)[] = new Array(labels.length).fill(null);
      for (const flow of this.flowHistory) {
        const flowLabel = new Date(flow.flowDate).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' });
        const idx = labels.indexOf(flowLabel);
        if (idx === -1) continue;
        const isInflow = ['Deposit', 'Dividend', 'Interest'].includes(flow.type);
        if (isInflow) depositData[idx] = values[idx];
        else withdrawData[idx] = values[idx];
      }
      if (depositData.some(d => d !== null)) {
        datasets.push({
          label: 'Nạp tiền',
          data: depositData,
          pointRadius: 6, pointHoverRadius: 8,
          pointStyle: 'triangle',
          backgroundColor: '#10b981', borderColor: '#10b981',
          showLine: false, borderWidth: 0
        });
      }
      if (withdrawData.some(d => d !== null)) {
        datasets.push({
          label: 'Rút tiền',
          data: withdrawData,
          pointRadius: 6, pointHoverRadius: 8,
          pointStyle: 'triangle', rotation: 180,
          backgroundColor: '#ef4444', borderColor: '#ef4444',
          showLine: false, borderWidth: 0
        });
      }
    }

    this.miniEquityChart = new Chart(this.miniEquityCanvas.nativeElement, {
      type: 'line',
      data: {
        labels,
        datasets
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
                const prefix = ctx.dataset.label ? ctx.dataset.label + ': ' : '';
                if (Math.abs(v) >= 1e9) return prefix + (v / 1e9).toFixed(1) + ' tỷ';
                if (Math.abs(v) >= 1e6) return prefix + (v / 1e6).toFixed(1) + ' tr';
                return prefix + v.toLocaleString('vi-VN') + ' đ';
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
