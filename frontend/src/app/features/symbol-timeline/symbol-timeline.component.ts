import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, of } from 'rxjs';
import { takeUntil, switchMap, catchError } from 'rxjs/operators';
import { createChart, IChartApi, ISeriesApi, Time, SeriesMarker, LineData, CandlestickData, LineStyle, IPriceLine } from 'lightweight-charts';
import { JournalEntryService, SymbolTimeline, TimelineItem, CreateJournalEntryRequest } from '../../core/services/journal-entry.service';
import { MarketEventService, CreateMarketEventRequest } from '../../core/services/market-event.service';
import { MarketDataService, StockPrice, TechnicalAnalysis } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';

const EMOTION_COLORS: { [key: string]: string } = {
  'Tự tin': '#22c55e',
  'Bình tĩnh': '#3b82f6',
  'Hào hứng': '#eab308',
  'Lo lắng': '#f97316',
  'Sợ hãi': '#ef4444',
  'Tham lam': '#a855f7',
  'FOMO': '#1f2937'
};

const EVENT_ICONS: { [key: string]: string } = {
  'Earnings': '📊',
  'Dividend': '💰',
  'RightsIssue': '📄',
  'ShareholderMtg': '🏛️',
  'InsiderTrade': '👤',
  'News': '📰',
  'Macro': '🏦'
};

const ENTRY_TYPE_LABELS: { [key: string]: string } = {
  'Observation': 'Quan sát',
  'PreTrade': 'Trước GD',
  'DuringTrade': 'Đang GD',
  'PostTrade': 'Sau GD',
  'Review': 'Tổng kết'
};

const EMOTIONS = ['Tự tin', 'Bình tĩnh', 'Hào hứng', 'Lo lắng', 'Sợ hãi', 'Tham lam', 'FOMO'];

@Component({
  selector: 'app-symbol-timeline',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe, UppercaseDirective, NumMaskDirective, AiChatPanelComponent],
  template: `
    <div class="max-w-7xl mx-auto px-4 py-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between mb-6 gap-3">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">
            {{ symbol }} — Dòng thời gian
          </h1>
          <p class="text-sm text-gray-500 mt-1" *ngIf="timeline">
            {{ timeline.items.length }} sự kiện
            <span *ngIf="timeline.holdingPeriods.length"> · {{ timeline.holdingPeriods.length }} đợt nắm giữ</span>
          </p>
        </div>
        <div class="flex gap-2 flex-wrap">
          <!-- Date range -->
          <div class="flex gap-1">
            <button *ngFor="let r of dateRanges" (click)="setDateRange(r.months)"
              [class]="selectedRange === r.months
                ? 'px-3 py-1.5 rounded-lg text-sm font-medium bg-indigo-600 text-white'
                : 'px-3 py-1.5 rounded-lg text-sm font-medium bg-gray-100 text-gray-700 hover:bg-gray-200'">
              {{ r.label }}
            </button>
          </div>
          <button (click)="showJournalForm = !showJournalForm"
            class="px-4 py-1.5 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700">
            + Ghi nhật ký
          </button>
          <button (click)="showEventForm = !showEventForm"
            class="px-4 py-1.5 bg-amber-600 text-white rounded-lg text-sm font-medium hover:bg-amber-700">
            + Sự kiện
          </button>
          <button (click)="crawlVietstock()" [disabled]="crawling"
            class="px-4 py-1.5 bg-teal-600 text-white rounded-lg text-sm font-medium hover:bg-teal-700 disabled:opacity-50">
            {{ crawling ? 'Đang tải...' : 'Cập nhật tin tức' }}
          </button>
          <button (click)="showAiPanel = !showAiPanel"
            class="px-4 py-1.5 bg-purple-600 text-white rounded-lg text-sm font-medium hover:bg-purple-700">
            AI Review
          </button>
          <div class="relative" *ngIf="timeline">
            <button (click)="showExportMenu = !showExportMenu"
              class="px-4 py-1.5 bg-gray-600 text-white rounded-lg text-sm font-medium hover:bg-gray-700">
              Xuất dữ liệu
            </button>
            <div *ngIf="showExportMenu"
              class="absolute right-0 top-full mt-1 bg-white rounded-lg shadow-lg border border-gray-200 py-1 z-10 w-44">
              <button (click)="exportCsv(); showExportMenu = false"
                class="w-full text-left px-4 py-2 text-sm hover:bg-gray-50">Xuất CSV</button>
              <button (click)="copySummary(); showExportMenu = false"
                class="w-full text-left px-4 py-2 text-sm hover:bg-gray-50">Sao chép tóm tắt</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Post-trade review banner -->
      <div *ngIf="reviewTrade" class="rounded-xl p-4 mb-4 border"
        [class]="reviewAlreadyDone
          ? 'bg-emerald-50 border-emerald-200'
          : 'bg-amber-50 border-amber-200'">
        <div class="flex items-start gap-3">
          <span class="text-xl flex-shrink-0">{{ reviewAlreadyDone ? '✅' : '✍️' }}</span>
          <div class="flex-1 min-w-0">
            <p class="font-semibold" [class]="reviewAlreadyDone ? 'text-emerald-800' : 'text-amber-800'">
              {{ reviewAlreadyDone ? 'Giao dịch này đã được đánh giá' : 'Đang đánh giá giao dịch' }}
            </p>
            <p class="text-sm text-gray-700 mt-0.5">
              {{ reviewTrade.tradeType === 'SELL' ? 'BÁN' : 'MUA' }}
              {{ reviewTrade.quantity | number }} cp {{ symbol }}
              &#64; {{ reviewTrade.price | vndCurrency }}
              — {{ formatDate(reviewTrade.timestamp) }}
            </p>
            <p class="text-xs text-gray-500 mt-1" *ngIf="!reviewAlreadyDone">
              Điền cảm xúc và nhận định bên dưới, sau đó bấm "Lưu đánh giá".
            </p>
          </div>
          <button (click)="cancelReviewMode()"
            class="text-gray-400 hover:text-gray-600 text-sm flex-shrink-0" title="Hủy">✕</button>
        </div>
      </div>

      <!-- Quick-add Journal Form -->
      <div *ngIf="showJournalForm" class="bg-white rounded-xl shadow-sm border border-gray-200 p-5 mb-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-4">
          {{ reviewTradeId ? 'Đánh giá sau giao dịch' : 'Ghi nhật ký' }} — {{ symbol }}
        </h3>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Loại</label>
            <select [(ngModel)]="journalForm.entryType" class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
              <option value="Observation">Quan sát</option>
              <option value="PreTrade">Trước giao dịch</option>
              <option value="DuringTrade">Đang giao dịch</option>
              <option value="PostTrade">Sau giao dịch</option>
              <option value="Review">Tổng kết</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Tiêu đề</label>
            <input type="text" [(ngModel)]="journalForm.title" placeholder="Tiêu đề ngắn gọn..."
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
          </div>
          <div class="md:col-span-2">
            <label class="block text-sm font-medium text-gray-700 mb-1">Nội dung</label>
            <textarea [(ngModel)]="journalForm.content" rows="3" placeholder="Phân tích, nhận định..."
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Cảm xúc</label>
            <select [(ngModel)]="journalForm.emotionalState" class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
              <option value="">-- Chọn --</option>
              <option *ngFor="let e of emotions" [value]="e">{{ e }}</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              Mức tự tin: {{ journalForm.confidenceLevel || '—' }}/10
            </label>
            <input type="range" [(ngModel)]="journalForm.confidenceLevel" min="1" max="10" step="1"
              class="w-full">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Giá tại thời điểm</label>
            <input type="text" inputmode="numeric" appNumMask [(ngModel)]="journalForm.priceAtTime"
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Bối cảnh thị trường</label>
            <input type="text" [(ngModel)]="journalForm.marketContext" placeholder="VNI sideway, bull..."
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Thẻ (cách nhau dấu phẩy)</label>
            <input type="text" [(ngModel)]="journalForm.tagsInput" placeholder="RSI, hỗ trợ, breakout..."
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Thời điểm</label>
            <input type="datetime-local" [(ngModel)]="journalForm.timestamp"
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
          </div>
        </div>
        <div class="flex gap-2 mt-4 justify-end">
          <button (click)="closeJournalForm()"
            class="px-5 py-2 bg-gray-100 text-gray-700 rounded-lg text-sm font-medium hover:bg-gray-200">
            Hủy
          </button>
          <button (click)="createJournalEntry()" [disabled]="creatingEntry"
            class="px-5 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50">
            {{ creatingEntry ? 'Đang lưu...' : (reviewTradeId ? 'Lưu đánh giá' : 'Lưu nhật ký') }}
          </button>
        </div>
      </div>

      <!-- Quick-add Event Form -->
      <div *ngIf="showEventForm" class="bg-white rounded-xl shadow-sm border border-gray-200 p-5 mb-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-4">Thêm sự kiện — {{ symbol }}</h3>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Loại sự kiện</label>
            <select [(ngModel)]="eventForm.eventType" class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
              <option value="Earnings">📊 KQKD</option>
              <option value="Dividend">💰 Cổ tức</option>
              <option value="RightsIssue">📄 Phát hành thêm</option>
              <option value="ShareholderMtg">🏛️ ĐHCĐ</option>
              <option value="InsiderTrade">👤 Giao dịch nội bộ</option>
              <option value="News">📰 Tin tức</option>
              <option value="Macro">🏦 Vĩ mô</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Tiêu đề</label>
            <input type="text" [(ngModel)]="eventForm.title" placeholder="Mô tả ngắn..."
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Ngày sự kiện</label>
            <input type="date" [(ngModel)]="eventForm.eventDate"
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Nguồn (URL)</label>
            <input type="text" [(ngModel)]="eventForm.source" placeholder="https://..."
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm">
          </div>
          <div class="md:col-span-2">
            <label class="block text-sm font-medium text-gray-700 mb-1">Mô tả</label>
            <textarea [(ngModel)]="eventForm.description" rows="2" placeholder="Chi tiết..."
              class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm"></textarea>
          </div>
        </div>
        <div class="flex gap-2 mt-4">
          <button (click)="createMarketEvent()" [disabled]="creatingEvent"
            class="px-5 py-2 bg-amber-600 text-white rounded-lg text-sm font-medium hover:bg-amber-700 disabled:opacity-50">
            {{ creatingEvent ? 'Đang lưu...' : 'Lưu sự kiện' }}
          </button>
          <button (click)="showEventForm = false"
            class="px-5 py-2 bg-gray-100 text-gray-700 rounded-lg text-sm font-medium hover:bg-gray-200">
            Hủy
          </button>
        </div>
      </div>

      <!-- Chart -->
      <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6 relative">
        <!-- Chart overlay toggles -->
        <div class="flex gap-2 mb-3 flex-wrap">
          <button (click)="toggleOverlay('candle')"
            [class]="showCandle
              ? 'px-3 py-1 rounded-lg text-xs font-medium bg-blue-600 text-white'
              : 'px-3 py-1 rounded-lg text-xs font-medium bg-gray-200 text-gray-600 hover:bg-gray-300'">
            Nến
          </button>
          <button (click)="toggleOverlay('ema')"
            [class]="showEma
              ? 'px-3 py-1 rounded-lg text-xs font-medium bg-blue-600 text-white'
              : 'px-3 py-1 rounded-lg text-xs font-medium bg-gray-200 text-gray-600 hover:bg-gray-300'">
            EMA
          </button>
          <button (click)="toggleOverlay('sr')"
            [class]="showSR
              ? 'px-3 py-1 rounded-lg text-xs font-medium bg-blue-600 text-white'
              : 'px-3 py-1 rounded-lg text-xs font-medium bg-gray-200 text-gray-600 hover:bg-gray-300'">
            S/R
          </button>
          <button (click)="toggleOverlay('fib')"
            [class]="showFib
              ? 'px-3 py-1 rounded-lg text-xs font-medium bg-blue-600 text-white'
              : 'px-3 py-1 rounded-lg text-xs font-medium bg-gray-200 text-gray-600 hover:bg-gray-300'">
            Fibonacci
          </button>
        </div>
        <div #chartContainer class="w-full" style="height: 420px;"></div>
        <div #chartTooltip class="absolute pointer-events-none bg-gray-900 text-white text-xs rounded-lg px-3 py-2 shadow-lg z-50 max-w-xs hidden"
          style="transition: opacity 0.15s ease;">
        </div>

        <!-- Emotion Ribbon Legend -->
        <div class="flex gap-3 mt-3 flex-wrap text-xs" *ngIf="timeline?.emotionSummary">
          <span *ngFor="let e of emotions" class="flex items-center gap-1">
            <span class="inline-block w-3 h-3 rounded-full" [style.background-color]="getEmotionColor(e)"></span>
            {{ e }}
          </span>
        </div>
      </div>

      <!-- Emotion Summary Panel (7C.2) -->
      <div *ngIf="timeline?.emotionSummary" class="bg-white rounded-xl shadow-sm border border-gray-200 p-5 mb-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-4">Phân tích cảm xúc</h3>
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div class="text-center">
            <p class="text-2xl font-bold text-gray-900">{{ timeline!.emotionSummary!.totalEntries }}</p>
            <p class="text-sm text-gray-500">Tổng nhật ký</p>
          </div>
          <div class="text-center">
            <p class="text-2xl font-bold text-indigo-600">
              {{ timeline!.emotionSummary?.averageConfidence ? (timeline!.emotionSummary!.averageConfidence | number:'1.1-1') : '—' }}
            </p>
            <p class="text-sm text-gray-500">Tự tin TB</p>
          </div>
          <div class="text-center" *ngIf="topEmotion">
            <p class="text-2xl font-bold" [style.color]="getEmotionColor(topEmotion)">{{ topEmotion }}</p>
            <p class="text-sm text-gray-500">Cảm xúc chính</p>
          </div>
          <div class="text-center">
            <p class="text-2xl font-bold text-gray-900">{{ timeline!.holdingPeriods.length }}</p>
            <p class="text-sm text-gray-500">Đợt nắm giữ</p>
          </div>
        </div>
        <!-- Emotion distribution bars -->
        <div class="mt-4 space-y-2" *ngIf="timeline!.emotionSummary?.distribution">
          <div *ngFor="let item of emotionDistribution" class="flex items-center gap-2">
            <span class="w-20 text-sm text-gray-600 text-right">{{ item.label }}</span>
            <div class="flex-1 bg-gray-100 rounded-full h-5 overflow-hidden">
              <div class="h-full rounded-full transition-all duration-300"
                [style.width.%]="item.percent"
                [style.background-color]="getEmotionColor(item.label)"></div>
            </div>
            <span class="text-sm text-gray-500 w-12">{{ item.count }}</span>
          </div>
        </div>
      </div>

      <!-- P7.1: Emotion ↔ P&L Correlation -->
      <div *ngIf="timeline?.emotionSummary?.correlations?.length" class="bg-white rounded-xl shadow-sm border border-gray-200 p-5 mb-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-4">Cảm xúc ↔ Kết quả giao dịch</h3>
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="border-b border-gray-200">
                <th class="text-left py-2 px-3 font-medium text-gray-600">Cảm xúc</th>
                <th class="text-right py-2 px-3 font-medium text-gray-600">Số GD</th>
                <th class="text-right py-2 px-3 font-medium text-gray-600">Win Rate</th>
                <th class="text-right py-2 px-3 font-medium text-gray-600">P&L TB (%)</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let c of timeline!.emotionSummary!.correlations" class="border-b border-gray-100">
                <td class="py-2 px-3 font-medium" [style.color]="getEmotionColor(c.emotion)">{{ c.emotion }}</td>
                <td class="text-right py-2 px-3">{{ c.tradeCount }}</td>
                <td class="text-right py-2 px-3 font-medium"
                  [class]="c.winRate >= 60 ? 'text-green-600' : c.winRate < 40 ? 'text-red-600' : 'text-gray-700'">
                  {{ c.winRate | number:'1.0-1' }}%
                </td>
                <td class="text-right py-2 px-3 font-medium"
                  [class]="c.averagePnlPercent >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ c.averagePnlPercent >= 0 ? '+' : '' }}{{ c.averagePnlPercent | number:'1.1-1' }}%
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <!-- Top insight -->
        <div class="mt-3 text-sm" *ngIf="correlationInsight">
          <p class="text-gray-600" [innerHTML]="correlationInsight"></p>
        </div>
      </div>

      <!-- P7.2: Confidence Calibration -->
      <div *ngIf="timeline?.emotionSummary?.confidenceCalibration?.length" class="bg-white rounded-xl shadow-sm border border-gray-200 p-5 mb-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-4">Hiệu chuẩn mức tự tin</h3>
        <div class="space-y-3">
          <div *ngFor="let cal of timeline!.emotionSummary!.confidenceCalibration" class="flex items-center gap-3">
            <span class="w-28 text-sm text-gray-600 text-right flex-shrink-0">{{ cal.range }}</span>
            <div class="flex-1 bg-gray-100 rounded-full h-6 overflow-hidden relative">
              <div class="h-full rounded-full transition-all duration-300"
                [style.width.%]="cal.winRate"
                [class]="cal.isCalibrated ? 'bg-green-500' : 'bg-amber-500'"></div>
              <span class="absolute inset-0 flex items-center justify-center text-xs font-medium">
                {{ cal.winRate | number:'1.0-0' }}% win
              </span>
            </div>
            <span class="text-xs w-20 flex-shrink-0"
              [class]="cal.isCalibrated ? 'text-green-600' : 'text-amber-600'">
              {{ cal.isCalibrated ? 'Phù hợp' : (cal.winRate < 50 ? 'Quá tự tin' : 'Chưa tự tin') }}
            </span>
          </div>
        </div>
      </div>

      <!-- P7.3: Behavioral Pattern Detection -->
      <div *ngIf="timeline?.behavioralPatterns?.length" class="bg-white rounded-xl shadow-sm border border-gray-200 p-5 mb-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-3">Mẫu hành vi phát hiện</h3>
        <div class="flex gap-3 mb-3 text-sm text-gray-500">
          <span *ngIf="patternCount('FOMO')">FOMO: {{ patternCount('FOMO') }}</span>
          <span *ngIf="patternCount('PanicSell')">Bán panic: {{ patternCount('PanicSell') }}</span>
          <span *ngIf="patternCount('RevengeTrading')">Revenge: {{ patternCount('RevengeTrading') }}</span>
          <span *ngIf="patternCount('Overtrading')">Overtrading: {{ patternCount('Overtrading') }}</span>
        </div>
        <div class="space-y-2">
          <div *ngFor="let p of timeline!.behavioralPatterns" class="flex items-start gap-3 p-3 rounded-lg"
            [class]="p.severity === 'Critical' ? 'bg-red-50 border border-red-200' : p.severity === 'Warning' ? 'bg-amber-50 border border-amber-200' : 'bg-blue-50 border border-blue-200'">
            <span class="text-lg flex-shrink-0">{{ p.severity === 'Critical' ? '\uD83D\uDD34' : p.severity === 'Warning' ? '\u26A0\uFE0F' : '\u2139\uFE0F' }}</span>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <span class="text-xs font-medium px-2 py-0.5 rounded-full"
                  [class]="p.severity === 'Critical' ? 'bg-red-100 text-red-800' : 'bg-amber-100 text-amber-800'">
                  {{ getPatternLabel(p.patternType) }}
                </span>
                <span class="text-xs text-gray-500">{{ formatDate(p.occurredAt) }}</span>
              </div>
              <p class="text-sm text-gray-700 mt-1">{{ p.description }}</p>
            </div>
          </div>
        </div>
      </div>

      <!-- P7.6: Emotion Trend Over Time -->
      <div *ngIf="timeline?.emotionSummary?.trends?.length" class="bg-white rounded-xl shadow-sm border border-gray-200 p-5 mb-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-4">Xu hướng cảm xúc theo tháng</h3>
        <div class="space-y-3">
          <div *ngFor="let trend of timeline!.emotionSummary!.trends" class="border-b border-gray-100 pb-3">
            <div class="flex items-center justify-between mb-1">
              <span class="text-sm font-medium text-gray-700">{{ trend.period }}</span>
              <div class="flex items-center gap-2 text-sm">
                <span class="font-medium" [style.color]="getEmotionColor(trend.dominantEmotion)">
                  {{ trend.dominantEmotion }}
                </span>
                <span class="text-gray-400">·</span>
                <span class="text-gray-500">{{ trend.entryCount }} nhật ký</span>
                <span class="text-gray-400">·</span>
                <span class="text-indigo-600">Tự tin: {{ trend.averageConfidence | number:'1.1-1' }}</span>
              </div>
            </div>
            <!-- Stacked bar for emotion distribution -->
            <div class="flex h-4 rounded-full overflow-hidden bg-gray-100">
              <div *ngFor="let item of getTrendDistribution(trend)"
                [style.width.%]="item.percent"
                [style.background-color]="getEmotionColor(item.label)"
                class="transition-all duration-300"
                [title]="item.label + ': ' + item.count">
              </div>
            </div>
          </div>
        </div>
        <!-- Trend insight -->
        <div class="mt-3 text-sm text-gray-600" *ngIf="trendInsight">
          {{ trendInsight }}
        </div>
      </div>

      <!-- Event filter toggles (7B.3) -->
      <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6" *ngIf="timeline && timeline.items.length > 0">
        <div class="flex gap-3 flex-wrap items-center">
          <span class="text-sm font-medium text-gray-700">Lọc:</span>
          <label *ngFor="let f of filterOptions" class="flex items-center gap-1.5 text-sm cursor-pointer">
            <input type="checkbox" [(ngModel)]="f.checked" (change)="applyFilters()" class="rounded">
            <span>{{ f.label }}</span>
          </label>
        </div>
      </div>

      <!-- Timeline Detail List -->
      <div class="space-y-3" *ngIf="filteredItems.length > 0">
        <div *ngFor="let item of filteredItems; let i = index" [id]="'timeline-item-' + i"
          class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 hover:border-indigo-300 transition-colors cursor-pointer"
          [class.border-indigo-400]="selectedItemIndex === i"
          (click)="selectTimelineItem(i)">

          <!-- Journal entry -->
          <div *ngIf="item.type === 'journal'" class="flex gap-3">
            <div class="w-10 h-10 rounded-full bg-indigo-100 flex items-center justify-center text-lg flex-shrink-0">📓</div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <span class="text-xs font-medium px-2 py-0.5 rounded-full bg-indigo-100 text-indigo-800">
                  {{ getEntryTypeLabel(item.data.entryType) }}
                </span>
                <span class="text-xs text-gray-500">{{ formatDate(item.timestamp) }}</span>
                <span *ngIf="item.data.emotionalState"
                  class="text-xs font-medium px-2 py-0.5 rounded-full"
                  [style.background-color]="getEmotionColor(item.data.emotionalState) + '20'"
                  [style.color]="getEmotionColor(item.data.emotionalState)">
                  {{ item.data.emotionalState }}
                  <span *ngIf="item.data.confidenceLevel">
                    ({{ item.data.confidenceLevel }}/10)
                  </span>
                </span>
              </div>
              <p class="font-medium text-gray-900 mt-1">{{ item.data.title }}</p>
              <p class="text-sm text-gray-600 mt-0.5 line-clamp-2">{{ item.data.content }}</p>
              <div class="flex gap-2 mt-2 flex-wrap" *ngIf="(item.data.tags)?.length">
                <span *ngFor="let tag of (item.data.tags)"
                  class="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">#{{ tag }}</span>
              </div>
              <p class="text-xs text-gray-400 mt-1" *ngIf="item.data.priceAtTime">
                Giá: {{ (item.data.priceAtTime) | vndCurrency }}
              </p>
            </div>
            <button (click)="deleteJournalEntry(item.data.id, $event)"
              class="text-gray-400 hover:text-red-500 flex-shrink-0 self-start text-sm">✕</button>
          </div>

          <!-- Trade -->
          <div *ngIf="item.type === 'trade'" class="flex gap-3">
            <div class="w-10 h-10 rounded-full flex items-center justify-center text-lg flex-shrink-0"
              [class]="(item.data.tradeType) === 'BUY' ? 'bg-green-100' : 'bg-red-100'">
              {{ (item.data.tradeType) === 'BUY' ? '▲' : '▼' }}
            </div>
            <div class="flex-1">
              <div class="flex items-center gap-2">
                <span class="text-xs font-medium px-2 py-0.5 rounded-full"
                  [class]="(item.data.tradeType) === 'BUY'
                    ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'">
                  {{ (item.data.tradeType) === 'BUY' ? 'MUA' : 'BÁN' }}
                </span>
                <span class="text-xs text-gray-500">{{ formatDate(item.timestamp) }}</span>
              </div>
              <p class="font-medium text-gray-900 mt-1">
                {{ (item.data.quantity) | number }} cp &#64; {{ (item.data.price) | vndCurrency }}
              </p>
            </div>
          </div>

          <!-- Market Event -->
          <div *ngIf="item.type === 'event'" class="flex gap-3">
            <div class="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center text-lg flex-shrink-0">
              {{ getEventIcon(item.data.eventType) }}
            </div>
            <div class="flex-1">
              <div class="flex items-center gap-2">
                <span class="text-xs font-medium px-2 py-0.5 rounded-full bg-amber-100 text-amber-800">
                  {{ item.data.eventType }}
                </span>
                <span class="text-xs text-gray-500">{{ formatDate(item.timestamp) }}</span>
              </div>
              <p class="font-medium text-gray-900 mt-1">{{ item.data.title }}</p>
              <p class="text-sm text-gray-600 mt-0.5" *ngIf="item.data.description">
                {{ item.data.description }}
              </p>
              <a *ngIf="item.data.source" [href]="item.data.source"
                target="_blank" rel="noopener noreferrer" class="text-xs text-indigo-600 hover:underline mt-1 inline-block">Nguồn →</a>
            </div>
          </div>

          <!-- Alert -->
          <div *ngIf="item.type === 'alert'" class="flex gap-3">
            <div class="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center text-lg flex-shrink-0">⚠️</div>
            <div class="flex-1">
              <div class="flex items-center gap-2">
                <span class="text-xs font-medium px-2 py-0.5 rounded-full bg-orange-100 text-orange-800">Cảnh báo</span>
                <span class="text-xs text-gray-500">{{ formatDate(item.timestamp) }}</span>
              </div>
              <p class="font-medium text-gray-900 mt-1">{{ item.data.message }}</p>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty state -->
      <div *ngIf="!loading && filteredItems.length === 0"
        class="bg-white rounded-xl shadow-sm border border-gray-200 p-12 text-center">
        <p class="text-gray-500 text-lg">Chưa có dữ liệu timeline cho {{ symbol }}</p>
        <p class="text-gray-400 text-sm mt-2">Hãy ghi nhật ký đầu tiên hoặc thêm sự kiện thị trường.</p>
      </div>

      <!-- Loading -->
      <div *ngIf="loading" class="flex justify-center py-12">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-indigo-600"></div>
      </div>

      <!-- AI Chat Panel (7C.3) -->
      <app-ai-chat-panel
        [(isOpen)]="showAiPanel"
        [useCase]="'timeline-review'"
        [contextData]="aiContext"
        [title]="'AI Timeline Review — ' + symbol">
      </app-ai-chat-panel>
    </div>
  `
})
export class SymbolTimelineComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('chartContainer') chartContainer!: ElementRef;
  @ViewChild('chartTooltip') chartTooltip!: ElementRef;

  symbol = '';
  timeline: SymbolTimeline | null = null;
  priceHistory: StockPrice[] = [];
  currentPrice: StockPrice | null = null;
  private tooltipItemsByDate = new Map<string, TimelineItem[]>();
  loading = false;
  selectedRange = 3; // months
  selectedItemIndex = -1;

  // Forms
  showJournalForm = false;
  showEventForm = false;
  showAiPanel = false;
  creatingEntry = false;
  creatingEvent = false;
  crawling = false;
  showExportMenu = false;

  emotions = EMOTIONS;

  journalForm = {
    entryType: 'Observation',
    title: '',
    content: '',
    emotionalState: '',
    confidenceLevel: 5 as number | undefined,
    priceAtTime: undefined as number | undefined,
    marketContext: '',
    tagsInput: '',
    timestamp: ''
  };

  eventForm = {
    eventType: 'News',
    title: '',
    eventDate: '',
    description: '',
    source: ''
  };

  // Filters (7B.3)
  filterOptions = [
    { type: 'journal', label: '📓 Nhật ký', checked: true },
    { type: 'trade', label: '💹 Giao dịch', checked: true },
    { type: 'event', label: '📰 Sự kiện', checked: true },
    { type: 'alert', label: '⚠️ Cảnh báo', checked: true }
  ];
  filteredItems: TimelineItem[] = [];

  dateRanges = [
    { label: '1T', months: 1 },
    { label: '3T', months: 3 },
    { label: '6T', months: 6 },
    { label: '1N', months: 12 }
  ];

  // Lifecycle
  private destroy$ = new Subject<void>();
  private resizeObserver: ResizeObserver | null = null;

  // Chart
  private chart: IChartApi | null = null;
  private candleSeries: ISeriesApi<'Candlestick'> | null = null;
  private lineSeries: ISeriesApi<'Line'> | null = null;
  private emotionSeries: ISeriesApi<'Histogram'> | null = null;
  private technicalAnalysis: TechnicalAnalysis | null = null;

  // Chart overlay toggles
  showCandle = true;
  showEma = true;
  showSR = true;
  showFib = false;

  // Overlay references
  private emaPriceLines: IPriceLine[] = [];
  private srPriceLines: IPriceLine[] = [];
  private fibPriceLines: IPriceLine[] = [];

  // Derived
  topEmotion = '';
  emotionDistribution: { label: string; count: number; percent: number }[] = [];
  correlationInsight = '';
  trendInsight = '';

  // AI context (7C.3)
  aiContext = '';

  // Post-trade review mode (driven by ?tradeId query param)
  reviewTradeId: string | null = null;
  reviewTrade: { tradeType: string; quantity: number; price: number; timestamp: string } | null = null;
  reviewAlreadyDone = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private journalEntryService: JournalEntryService,
    private marketEventService: MarketEventService,
    private marketDataService: MarketDataService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    this.symbol = (this.route.snapshot.paramMap.get('symbol') || this.route.snapshot.queryParams['symbol'] || '').toUpperCase();
    this.reviewTradeId = this.route.snapshot.queryParams['tradeId'] || null;
    // When coming in to review a specific trade, widen the default range so
    // older unreviewed sells (surfaced from the dashboard's pending-review list)
    // are still inside the fetched timeline window.
    if (this.reviewTradeId) this.selectedRange = 12;
    if (!this.symbol) return;
    this.loadData();
  }

  ngAfterViewInit() {
    this.initChart();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.resizeObserver) {
      this.resizeObserver.disconnect();
      this.resizeObserver = null;
    }
    if (this.chart) {
      this.chart.remove();
      this.chart = null;
    }
  }

  setDateRange(months: number) {
    this.selectedRange = months;
    this.loadData();
  }

  loadData() {
    this.loading = true;
    const to = new Date();
    const from = new Date();
    from.setMonth(from.getMonth() - this.selectedRange);
    const fromStr = from.toISOString().split('T')[0];
    const toStr = to.toISOString().split('T')[0];

    // Load timeline + price history in parallel
    this.journalEntryService.getTimeline(this.symbol, fromStr, toStr)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (timeline) => {
          this.timeline = this.normalizeTimeline(timeline);
          this.filteredItems = [...this.timeline!.items];
          this.applyFilters();
          this.computeEmotionStats();
          this.buildAiContext();
          this.updateChartMarkers();
          this.tryBeginReviewMode();
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.error('Lỗi', 'Không thể tải dữ liệu timeline');
        }
      });

    this.marketDataService.getPriceHistory(this.symbol, fromStr, toStr)
      .pipe(
        takeUntil(this.destroy$),
        switchMap(prices => {
          this.priceHistory = prices;
          return this.marketDataService.getCurrentPrice(this.symbol)
            .pipe(catchError(() => of(null)));
        })
      )
      .subscribe({
        next: (current) => { this.currentPrice = current; this.updateChartData(); },
        error: () => {
          this.notificationService.error('Lỗi', 'Không thể tải dữ liệu giá');
        }
      });

    // Fetch technical analysis for overlays
    this.marketDataService.getTechnicalAnalysis(this.symbol)
      .pipe(takeUntil(this.destroy$), catchError(() => of(null)))
      .subscribe(ta => {
        this.technicalAnalysis = ta;
        this.updateOverlays();
      });
  }

  // ===== Chart =====

  private initChart() {
    if (!this.chartContainer?.nativeElement) return;
    this.chart = createChart(this.chartContainer.nativeElement, {
      layout: {
        background: { color: '#ffffff' },
        textColor: '#374151',
        fontFamily: 'system-ui, sans-serif'
      },
      grid: {
        vertLines: { color: '#f3f4f6' },
        horzLines: { color: '#f3f4f6' }
      },
      crosshair: { mode: 0 },
      rightPriceScale: { borderColor: '#e5e7eb' },
      timeScale: {
        borderColor: '#e5e7eb',
        timeVisible: false
      },
      width: this.chartContainer.nativeElement.clientWidth,
      height: 380
    });

    // Candlestick series (primary)
    this.candleSeries = this.chart.addCandlestickSeries({
      upColor: '#22c55e',
      downColor: '#ef4444',
      borderUpColor: '#16a34a',
      borderDownColor: '#dc2626',
      wickUpColor: '#16a34a',
      wickDownColor: '#dc2626',
      priceLineVisible: true,
      lastValueVisible: true
    });

    // Line series fallback (shown when candle is toggled off)
    this.lineSeries = this.chart.addLineSeries({
      color: '#6366f1',
      lineWidth: 2,
      priceLineVisible: true,
      lastValueVisible: true,
      visible: false
    });

    // Emotion histogram (7C.1)
    this.emotionSeries = this.chart.addHistogramSeries({
      priceScaleId: 'emotion',
      priceFormat: { type: 'volume' }
    });
    this.chart.priceScale('emotion').applyOptions({
      scaleMargins: { top: 0.85, bottom: 0 }
    });

    // Tooltip on crosshair move
    this.chart.subscribeCrosshairMove((param) => {
      const tooltip = this.chartTooltip?.nativeElement;
      if (!tooltip) return;

      if (!param.time || !param.point) {
        tooltip.classList.add('hidden');
        return;
      }

      const day = param.time as string;
      const items = this.tooltipItemsByDate.get(day);
      if (!items || items.length === 0) {
        tooltip.classList.add('hidden');
        return;
      }

      // Build tooltip content — escape dynamic text to prevent XSS
      const lines = items.map(item => {
        if (item.type === 'trade') {
          const side = item.data.tradeType === 'BUY' ? 'MUA' : 'BÁN';
          return `<div class="flex items-center gap-1.5"><span class="font-bold ${item.data.tradeType === 'BUY' ? 'text-green-400' : 'text-red-400'}">${side}</span> ${item.data.quantity} × ${(item.data.price ?? 0).toLocaleString()}đ</div>`;
        }
        if (item.type === 'journal')
          return `<div class="flex items-center gap-1.5"><span class="text-indigo-400 font-bold">J</span> ${this.escapeHtml(this.truncate(item.data.title, 40))}</div>`;
        if (item.type === 'event')
          return `<div class="flex items-center gap-1.5"><span class="text-amber-400 font-bold">E</span> ${this.escapeHtml(this.truncate(item.data.title, 40))}</div>`;
        return `<div class="flex items-center gap-1.5"><span class="text-orange-400 font-bold">A</span> ${this.escapeHtml(this.truncate(item.data.title ?? 'Cảnh báo', 40))}</div>`;
      });

      tooltip.innerHTML = `<div class="font-medium text-gray-300 mb-1">${day}</div>${lines.join('')}`;
      tooltip.classList.remove('hidden');

      // Position tooltip near crosshair
      const chartRect = this.chartContainer.nativeElement.getBoundingClientRect();
      const x = param.point.x;
      const y = param.point.y;
      const tooltipW = tooltip.offsetWidth;
      const left = x + tooltipW + 20 > chartRect.width ? x - tooltipW - 10 : x + 10;
      const top = Math.max(0, Math.min(y - 10, chartRect.height - tooltip.offsetHeight));
      tooltip.style.left = left + 'px';
      tooltip.style.top = top + 'px';
    });

    // Responsive
    this.resizeObserver = new ResizeObserver(() => {
      if (this.chart && this.chartContainer?.nativeElement) {
        this.chart.applyOptions({ width: this.chartContainer.nativeElement.clientWidth });
      }
    });
    this.resizeObserver.observe(this.chartContainer.nativeElement);

    this.updateChartData();
  }

  private updateChartData() {
    if ((!this.candleSeries && !this.lineSeries) || this.priceHistory.length === 0) return;

    const sorted = this.priceHistory
      .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime());

    // Candlestick data (OHLC)
    const candleData: CandlestickData[] = sorted.map(p => ({
      time: p.date.split('T')[0] as Time,
      open: p.open,
      high: p.high,
      low: p.low,
      close: p.close
    }));

    // Line data (close only, for fallback)
    const lineData: LineData[] = sorted.map(p => ({
      time: p.date.split('T')[0] as Time,
      value: p.close
    }));

    // Extend chart to today using current market price
    const today = new Date().toISOString().split('T')[0];
    const lastCandle = candleData.length > 0 ? candleData[candleData.length - 1] : null;
    const lastDay = lastCandle ? lastCandle.time as string : '';
    if (lastDay && lastDay < today) {
      const todayPrice = this.currentPrice?.close ?? lastCandle!.close;
      candleData.push({
        time: today as Time,
        open: todayPrice,
        high: todayPrice,
        low: todayPrice,
        close: todayPrice
      });
      lineData.push({ time: today as Time, value: todayPrice });
    }

    if (this.candleSeries) this.candleSeries.setData(candleData);
    if (this.lineSeries) this.lineSeries.setData(lineData);

    this.chart?.timeScale().fitContent();
    this.updateChartMarkers();
    this.updateEmotionRibbon();
    this.updateOverlays();
  }

  private updateChartMarkers() {
    const activeSeries = this.showCandle ? this.candleSeries : this.lineSeries;
    if (!activeSeries || !this.timeline) return;

    // Group items by date
    const byDate = new Map<string, TimelineItem[]>();
    for (const item of this.timeline.items) {
      const day = item.timestamp.split('T')[0];
      if (!byDate.has(day)) byDate.set(day, []);
      byDate.get(day)!.push(item);
    }
    this.tooltipItemsByDate = byDate;

    const markers: SeriesMarker<Time>[] = [];

    for (const [day, items] of byDate) {
      const time = day as Time;

      // Trades — T marker (BUY below green, SELL above red)
      const trades = items.filter(i => i.type === 'trade');
      for (const t of trades) {
        const isBuy = t.data.tradeType === 'BUY';
        markers.push({
          time,
          position: isBuy ? 'belowBar' : 'aboveBar',
          color: isBuy ? '#22c55e' : '#ef4444',
          shape: isBuy ? 'arrowUp' : 'arrowDown',
          text: isBuy ? 'T' : 'T'
        });
      }

      // Journals — J marker (indigo)
      const journals = items.filter(i => i.type === 'journal');
      if (journals.length > 0) {
        markers.push({
          time,
          position: 'aboveBar',
          color: '#6366f1',
          shape: 'circle',
          text: journals.length > 1 ? `J${journals.length}` : 'J'
        });
      }

      // Events — E marker, Alerts — A marker (amber)
      const events = items.filter(i => i.type === 'event');
      if (events.length > 0) {
        markers.push({
          time,
          position: 'aboveBar',
          color: '#f59e0b',
          shape: 'square',
          text: events.length > 1 ? `E${events.length}` : 'E'
        });
      }

      const alerts = items.filter(i => i.type === 'alert');
      if (alerts.length > 0) {
        markers.push({
          time,
          position: 'aboveBar',
          color: '#f97316',
          shape: 'square',
          text: alerts.length > 1 ? `A${alerts.length}` : 'A'
        });
      }
    }

    // Sort markers by time (required by lightweight-charts)
    markers.sort((a, b) => (a.time as string).localeCompare(b.time as string));
    activeSeries.setMarkers(markers);
  }

  // 7C.1 — Emotion Ribbon
  private updateEmotionRibbon() {
    if (!this.emotionSeries || !this.timeline) return;

    const emotionData: LineData[] = [];

    for (const item of this.timeline.items) {
      if (item.type === 'journal' && (item.data.emotionalState)) {
        const emotion = item.data.emotionalState;
        const confidence = item.data.confidenceLevel ?? 5;
        emotionData.push({
          time: item.timestamp.split('T')[0] as Time,
          value: confidence,
          color: EMOTION_COLORS[emotion] || '#6b7280'
        } as any);
      }
    }

    if (emotionData.length > 0) {
      emotionData.sort((a, b) => (a.time as string).localeCompare(b.time as string));
      this.emotionSeries.setData(emotionData as any);
    }
  }

  // ===== Chart Overlays =====

  toggleOverlay(overlay: 'candle' | 'ema' | 'sr' | 'fib') {
    switch (overlay) {
      case 'candle':
        // Remove overlays from current series before switching
        this.clearAllOverlayLines();
        // Clear markers from old active series
        const oldSeries = this.getOverlaySeries();
        if (oldSeries) oldSeries.setMarkers([]);
        this.showCandle = !this.showCandle;
        if (this.candleSeries) this.candleSeries.applyOptions({ visible: this.showCandle });
        if (this.lineSeries) this.lineSeries.applyOptions({ visible: !this.showCandle });
        // Re-create overlays on new active series
        this.updateOverlays();
        this.updateChartMarkers();
        break;
      case 'ema':
        this.showEma = !this.showEma;
        this.updateEmaOverlay();
        break;
      case 'sr':
        this.showSR = !this.showSR;
        this.updateSROverlay();
        break;
      case 'fib':
        this.showFib = !this.showFib;
        this.updateFibOverlay();
        break;
    }
  }

  private updateOverlays() {
    this.updateEmaOverlay();
    this.updateSROverlay();
    this.updateFibOverlay();
  }

  private getOverlaySeries(): ISeriesApi<'Candlestick'> | ISeriesApi<'Line'> | null {
    return this.showCandle ? this.candleSeries : this.lineSeries;
  }

  private clearAllOverlayLines() {
    const series = this.getOverlaySeries();
    if (!series) return;
    for (const line of this.emaPriceLines) { series.removePriceLine(line); }
    for (const line of this.srPriceLines) { series.removePriceLine(line); }
    for (const line of this.fibPriceLines) { series.removePriceLine(line); }
    this.emaPriceLines = [];
    this.srPriceLines = [];
    this.fibPriceLines = [];
  }

  private updateEmaOverlay() {
    const series = this.getOverlaySeries();
    if (!series) return;

    // Remove existing EMA lines
    for (const line of this.emaPriceLines) {
      series.removePriceLine(line);
    }
    this.emaPriceLines = [];

    if (!this.showEma || !this.technicalAnalysis) return;

    const ta = this.technicalAnalysis;
    const emaLevels: { value: number | undefined; label: string; color: string }[] = [
      { value: ta.ema20, label: 'EMA 20', color: '#3b82f6' },
      { value: ta.ema50, label: 'EMA 50', color: '#f97316' },
      { value: ta.ema200, label: 'EMA 200', color: '#a855f7' }
    ];

    for (const ema of emaLevels) {
      if (ema.value != null) {
        const line = series.createPriceLine({
          price: ema.value,
          color: ema.color,
          lineWidth: 1,
          lineStyle: LineStyle.Dotted,
          axisLabelVisible: true,
          title: ema.label
        });
        this.emaPriceLines.push(line);
      }
    }
  }

  private updateSROverlay() {
    const series = this.getOverlaySeries();
    if (!series) return;

    // Remove existing S/R lines
    for (const line of this.srPriceLines) {
      series.removePriceLine(line);
    }
    this.srPriceLines = [];

    if (!this.showSR || !this.technicalAnalysis) return;

    const ta = this.technicalAnalysis;

    // Support levels (green dashed)
    for (let i = 0; i < (ta.supportLevels?.length ?? 0); i++) {
      const line = series.createPriceLine({
        price: ta.supportLevels[i],
        color: '#22c55e',
        lineWidth: 1,
        lineStyle: LineStyle.Dashed,
        axisLabelVisible: true,
        title: `S${i + 1}`
      });
      this.srPriceLines.push(line);
    }

    // Resistance levels (red dashed)
    for (let i = 0; i < (ta.resistanceLevels?.length ?? 0); i++) {
      const line = series.createPriceLine({
        price: ta.resistanceLevels[i],
        color: '#ef4444',
        lineWidth: 1,
        lineStyle: LineStyle.Dashed,
        axisLabelVisible: true,
        title: `R${i + 1}`
      });
      this.srPriceLines.push(line);
    }
  }

  private updateFibOverlay() {
    const series = this.getOverlaySeries();
    if (!series) return;

    // Remove existing Fibonacci lines
    for (const line of this.fibPriceLines) {
      series.removePriceLine(line);
    }
    this.fibPriceLines = [];

    if (!this.showFib || !this.technicalAnalysis?.fibonacci) return;

    const fib = this.technicalAnalysis.fibonacci;
    const fibLevels: { value: number; label: string; color: string }[] = [
      { value: fib.retracement236, label: 'Fib 23.6%', color: '#d97706' },
      { value: fib.retracement382, label: 'Fib 38.2%', color: '#b45309' },
      { value: fib.retracement500, label: 'Fib 50%', color: '#92400e' },
      { value: fib.retracement618, label: 'Fib 61.8%', color: '#78350f' },
      { value: fib.retracement786, label: 'Fib 78.6%', color: '#451a03' },
      { value: fib.extension1272, label: 'Ext 127.2%', color: '#ca8a04' },
      { value: fib.extension1618, label: 'Ext 161.8%', color: '#eab308' }
    ];

    for (const level of fibLevels) {
      if (level.value != null && level.value > 0) {
        const line = series.createPriceLine({
          price: level.value,
          color: level.color,
          lineWidth: 1,
          lineStyle: LineStyle.SparseDotted,
          axisLabelVisible: true,
          title: level.label
        });
        this.fibPriceLines.push(line);
      }
    }
  }

  // ===== Filters =====

  applyFilters() {
    if (!this.timeline) return;
    const activeTypes = this.filterOptions.filter(f => f.checked).map(f => f.type);
    this.filteredItems = this.timeline.items
      .filter(item => activeTypes.includes(item.type))
      .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
  }

  // ===== Journal Entry CRUD =====

  createJournalEntry() {
    if (!this.journalForm.title.trim()) {
      this.notificationService.warning('Thiếu thông tin', 'Vui lòng nhập tiêu đề');
      return;
    }
    this.creatingEntry = true;
    const tags = this.journalForm.tagsInput
      ? this.journalForm.tagsInput.split(',').map(t => t.trim()).filter(t => t)
      : undefined;

    const req: CreateJournalEntryRequest = {
      symbol: this.symbol,
      entryType: this.journalForm.entryType,
      title: this.journalForm.title,
      content: this.journalForm.content,
      emotionalState: this.journalForm.emotionalState || undefined,
      confidenceLevel: this.journalForm.confidenceLevel,
      priceAtTime: this.journalForm.priceAtTime,
      marketContext: this.journalForm.marketContext || undefined,
      tags,
      timestamp: this.journalForm.timestamp || undefined,
      tradeId: this.reviewTradeId || undefined
    };

    this.journalEntryService.create(req).subscribe({
      next: () => {
        const wasReview = !!this.reviewTradeId;
        this.notificationService.success('Thành công', wasReview ? 'Đã đánh giá giao dịch' : 'Đã lưu nhật ký');
        this.showJournalForm = false;
        this.resetJournalForm();
        this.exitReviewMode();
        this.loadData();
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không thể lưu nhật ký');
        this.creatingEntry = false;
      }
    });
  }

  // Post-trade review mode helpers
  private tryBeginReviewMode() {
    if (!this.reviewTradeId || !this.timeline) return;

    const tradeItem = this.timeline.items.find(
      i => i.type === 'trade' && i.data?.id === this.reviewTradeId
    );
    if (!tradeItem) {
      this.notificationService.warning(
        'Không thấy giao dịch',
        'Giao dịch cần đánh giá nằm ngoài khoảng thời gian. Hãy mở rộng phạm vi (6T hoặc 1N).'
      );
      this.reviewTradeId = null;
      return;
    }
    if (tradeItem.data?.tradeType !== 'SELL') {
      // Post-trade review is only meaningful for SELL trades (closes a position).
      this.reviewTradeId = null;
      return;
    }

    const alreadyReviewed = this.timeline.items.some(
      i => i.type === 'journal'
        && i.data?.entryType === 'PostTrade'
        && i.data?.tradeId === this.reviewTradeId
    );

    this.reviewTrade = {
      tradeType: tradeItem.data.tradeType,
      quantity: tradeItem.data.quantity,
      price: tradeItem.data.price,
      timestamp: tradeItem.timestamp
    };
    this.reviewAlreadyDone = alreadyReviewed;

    if (alreadyReviewed) return; // just show the banner, don't re-open the form

    this.journalForm = {
      entryType: 'PostTrade',
      title: `Đánh giá giao dịch ${tradeItem.data.tradeType === 'SELL' ? 'BÁN' : 'MUA'} ${this.symbol} — ${this.formatDate(tradeItem.timestamp)}`,
      content: '',
      emotionalState: '',
      confidenceLevel: 5,
      priceAtTime: tradeItem.data.price,
      marketContext: '',
      tagsInput: '',
      timestamp: this.toDatetimeLocal(tradeItem.timestamp)
    };
    this.showJournalForm = true;
  }

  cancelReviewMode() {
    this.showJournalForm = false;
    this.resetJournalForm();
    this.exitReviewMode();
  }

  closeJournalForm() {
    if (this.reviewTradeId) {
      this.cancelReviewMode();
    } else {
      this.showJournalForm = false;
    }
  }

  private exitReviewMode() {
    if (!this.reviewTradeId) return;
    this.reviewTradeId = null;
    this.reviewTrade = null;
    this.reviewAlreadyDone = false;
    // Remove tradeId from URL so a refresh doesn't re-open the form
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tradeId: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private toDatetimeLocal(iso: string): string {
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  deleteJournalEntry(id: string, event: Event) {
    event.stopPropagation();
    if (!confirm('Xóa nhật ký này?')) return;
    this.journalEntryService.delete(id).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Đã xóa nhật ký');
        this.loadData();
      },
      error: () => this.notificationService.error('Lỗi', 'Không thể xóa')
    });
  }

  // ===== Market Event =====

  createMarketEvent() {
    if (!this.eventForm.title.trim()) {
      this.notificationService.warning('Thiếu thông tin', 'Vui lòng nhập tiêu đề');
      return;
    }
    this.creatingEvent = true;
    const req: CreateMarketEventRequest = {
      symbol: this.symbol,
      eventType: this.eventForm.eventType,
      title: this.eventForm.title,
      eventDate: this.eventForm.eventDate || new Date().toISOString(),
      description: this.eventForm.description || undefined,
      source: this.eventForm.source || undefined
    };

    this.marketEventService.create(req).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Đã lưu sự kiện');
        this.showEventForm = false;
        this.resetEventForm();
        this.loadData();
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không thể lưu sự kiện');
        this.creatingEvent = false;
      }
    });
  }

  // ===== Export (P7.7) =====

  exportCsv() {
    if (!this.timeline) return;
    const headers = ['Ngày', 'Loại', 'Tiêu đề', 'Cảm xúc', 'Confidence', 'Giá', 'Ghi chú'];
    const rows = this.filteredItems.map(item => {
      const d = item.data;
      switch (item.type) {
        case 'journal':
          return [this.formatDate(item.timestamp), this.getEntryTypeLabel(d.entryType), d.title,
            d.emotionalState || '', d.confidenceLevel || '', d.priceAtTime || '', d.content?.substring(0, 100) || ''];
        case 'trade':
          return [this.formatDate(item.timestamp), d.tradeType === 'BUY' ? 'MUA' : 'BÁN',
            `${d.quantity} cp @ ${d.price}`, '', '', d.price, ''];
        case 'event':
          return [this.formatDate(item.timestamp), d.eventType, d.title, '', '', '', d.description || ''];
        case 'alert':
          return [this.formatDate(item.timestamp), 'Cảnh báo', d.message, '', '', '', ''];
        default:
          return [];
      }
    });

    const csv = [headers, ...rows].map(r => r.map((c: any) => `"${String(c).replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob(['\ufeff' + csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `timeline-${this.symbol}-${new Date().toISOString().split('T')[0]}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    this.notificationService.success('Xuất CSV', 'Đã tải file CSV');
  }

  copySummary() {
    if (!this.timeline) return;
    const parts: string[] = [
      `Timeline ${this.symbol}`,
      `Tổng sự kiện: ${this.timeline.items.length}`,
      `Đợt nắm giữ: ${this.timeline.holdingPeriods.length}`
    ];
    if (this.timeline.emotionSummary) {
      parts.push(`Cảm xúc chính: ${this.topEmotion}`);
      parts.push(`Tự tin TB: ${this.timeline.emotionSummary.averageConfidence?.toFixed(1) || 'N/A'}`);
    }
    if (this.timeline.behavioralPatterns?.length) {
      parts.push(`Cảnh báo hành vi: ${this.timeline.behavioralPatterns.length}`);
    }
    navigator.clipboard.writeText(parts.join('\n')).then(() => {
      this.notificationService.success('Đã sao chép', 'Tóm tắt đã được sao chép vào clipboard');
    });
  }

  // ===== Vietstock Crawl (P7.8) =====

  crawlVietstock() {
    this.crawling = true;
    this.marketEventService.crawl(this.symbol).subscribe({
      next: (result) => {
        const parts: string[] = [];
        if (result.newsAdded > 0) parts.push(`${result.newsAdded} tin tức`);
        if (result.eventsAdded > 0) parts.push(`${result.eventsAdded} sự kiện`);
        if (result.duplicatesSkipped > 0) parts.push(`bỏ qua ${result.duplicatesSkipped} trùng lặp`);
        const msg = parts.length > 0 ? `Đã thêm ${parts.join(', ')}.` : 'Không có tin tức mới.';
        this.notificationService.success('Cập nhật tin tức', msg);
        this.crawling = false;
        this.loadData();
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không thể tải tin tức từ Vietstock');
        this.crawling = false;
      }
    });
  }

  // ===== Helpers =====

  private truncate(text: string | undefined, max: number): string {
    if (!text) return '(không có tiêu đề)';
    return text.length > max ? text.substring(0, max) + '…' : text;
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  selectTimelineItem(index: number) {
    this.selectedItemIndex = index;
  }

  getEntryTypeLabel(type: string): string {
    return ENTRY_TYPE_LABELS[type] || type;
  }

  getEventIcon(type: string): string {
    return EVENT_ICONS[type] || '📰';
  }

  // P7.3: Pattern helpers
  patternCount(type: string): number {
    return this.timeline?.behavioralPatterns?.filter(p => p.patternType === type).length ?? 0;
  }

  getPatternLabel(type: string): string {
    const labels: { [key: string]: string } = {
      'FOMO': 'FOMO',
      'PanicSell': 'Bán panic',
      'RevengeTrading': 'Giao dịch trả thù',
      'Overtrading': 'Giao dịch quá mức'
    };
    return labels[type] || type;
  }

  getEmotionColor(emotion: string): string {
    return EMOTION_COLORS[emotion] || '#6b7280';
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }

  private computeEmotionStats() {
    if (!this.timeline?.emotionSummary) return;
    const dist = this.timeline.emotionSummary.distribution;
    const total = Object.values(dist).reduce((s, v) => s + v, 0);

    this.emotionDistribution = Object.entries(dist)
      .sort((a, b) => b[1] - a[1])
      .map(([label, count]) => ({
        label,
        count,
        percent: total > 0 ? (count / total) * 100 : 0
      }));

    this.topEmotion = this.emotionDistribution.length > 0 ? this.emotionDistribution[0].label : '';
    this.computeCorrelationInsight();
    this.computeTrendInsight();
  }

  // P7.1: Generate correlation insight text
  private computeCorrelationInsight() {
    const corr = this.timeline?.emotionSummary?.correlations;
    if (!corr?.length) { this.correlationInsight = ''; return; }

    const best = corr.reduce((a, b) => a.winRate > b.winRate ? a : b);
    const worst = corr.reduce((a, b) => a.winRate < b.winRate ? a : b);

    const parts: string[] = [];
    if (best.winRate > 50) {
      parts.push(`Khi <strong style="color:${this.getEmotionColor(best.emotion)}">${best.emotion}</strong> → Win rate ${best.winRate.toFixed(0)}%, P&L TB ${best.averagePnlPercent >= 0 ? '+' : ''}${best.averagePnlPercent.toFixed(1)}%`);
    }
    if (worst.emotion !== best.emotion && worst.winRate < 50) {
      parts.push(`Khi <strong style="color:${this.getEmotionColor(worst.emotion)}">${worst.emotion}</strong> → Win rate chỉ ${worst.winRate.toFixed(0)}%, P&L TB ${worst.averagePnlPercent >= 0 ? '+' : ''}${worst.averagePnlPercent.toFixed(1)}%`);
    }
    this.correlationInsight = parts.join(' · ');
  }

  // P7.6: Generate trend insight text
  private computeTrendInsight() {
    const trends = this.timeline?.emotionSummary?.trends;
    if (!trends || trends.length < 2) { this.trendInsight = ''; return; }

    const latest = trends[trends.length - 1];
    const prev = trends[trends.length - 2];
    const parts: string[] = [];

    if (latest.averageConfidence > prev.averageConfidence) {
      parts.push(`Tự tin tăng từ ${prev.averageConfidence.toFixed(1)} → ${latest.averageConfidence.toFixed(1)}`);
    } else if (latest.averageConfidence < prev.averageConfidence) {
      parts.push(`Tự tin giảm từ ${prev.averageConfidence.toFixed(1)} → ${latest.averageConfidence.toFixed(1)}`);
    }
    if (latest.dominantEmotion !== prev.dominantEmotion) {
      parts.push(`Cảm xúc chính: ${prev.dominantEmotion} → ${latest.dominantEmotion}`);
    }
    this.trendInsight = parts.length > 0 ? `Xu hướng: ${parts.join(' · ')}` : '';
  }

  // P7.6: Convert trend distribution to array for stacked bar
  getTrendDistribution(trend: any): { label: string; count: number; percent: number }[] {
    if (!trend.distribution) return [];
    const total = Object.values(trend.distribution as { [k: string]: number }).reduce((s: number, v) => s + (v as number), 0);
    if (total === 0) return [];
    return Object.entries(trend.distribution)
      .sort((a, b) => (b[1] as number) - (a[1] as number))
      .map(([label, count]) => ({
        label,
        count: count as number,
        percent: ((count as number) / total) * 100
      }));
  }

  // P7.5 — Build rich AI context for timeline review
  private buildAiContext() {
    if (!this.timeline) return;
    const parts: string[] = [
      `Bạn là huấn luyện viên tâm lý giao dịch (trading psychology coach).`,
      `Phân tích timeline giao dịch của nhà đầu tư cho mã ${this.symbol}:\n`,
      `## Tổng quan`,
      `- Tổng sự kiện: ${this.timeline.items.length}`,
      `- Đợt nắm giữ: ${this.timeline.holdingPeriods.length}`
    ];

    // Emotion summary
    if (this.timeline.emotionSummary) {
      parts.push(`- Tự tin TB: ${this.timeline.emotionSummary.averageConfidence?.toFixed(1) || 'N/A'}`);
      parts.push(`- Phân bố cảm xúc: ${JSON.stringify(this.timeline.emotionSummary.distribution)}`);

      // P7.1: Correlation data
      const corr = this.timeline.emotionSummary.correlations;
      if (corr?.length) {
        parts.push('\n## Dữ liệu cảm xúc ↔ P&L');
        parts.push('| Cảm xúc | Số GD | Win Rate | P&L TB (%) |');
        parts.push('|---------|-------|----------|------------|');
        for (const c of corr) {
          parts.push(`| ${c.emotion} | ${c.tradeCount} | ${c.winRate.toFixed(0)}% | ${c.averagePnlPercent >= 0 ? '+' : ''}${c.averagePnlPercent.toFixed(1)}% |`);
        }
      }

      // P7.2: Calibration data
      const cal = this.timeline.emotionSummary.confidenceCalibration;
      if (cal?.length) {
        parts.push('\n## Confidence Calibration');
        for (const c of cal) {
          parts.push(`- ${c.range}: Win rate ${c.winRate.toFixed(0)}%, ${c.isCalibrated ? 'Phù hợp' : 'Lệch'}`);
        }
      }
    }

    // P7.3: Behavioral patterns
    if (this.timeline.behavioralPatterns?.length) {
      parts.push('\n## Mẫu hành vi phát hiện');
      for (const p of this.timeline.behavioralPatterns) {
        parts.push(`- [${p.severity}] ${this.getPatternLabel(p.patternType)}: ${p.description} (${this.formatDate(p.occurredAt)})`);
      }
    }

    // Journal entries (all, not just last 10)
    const journals = this.timeline.items.filter(i => i.type === 'journal');
    if (journals.length > 0) {
      parts.push('\n## Timeline chi tiết — Nhật ký');
      for (const j of journals) {
        const d = j.data;
        parts.push(
          `[${this.formatDate(j.timestamp)}] ${d.entryType}: ${d.title}` +
          (d.emotionalState ? ` | Cảm xúc: ${d.emotionalState}` : '') +
          (d.confidenceLevel ? ` (${d.confidenceLevel}/10)` : '')
        );
      }
    }

    // Trades
    const trades = this.timeline.items.filter(i => i.type === 'trade');
    if (trades.length > 0) {
      parts.push('\n## Giao dịch');
      for (const t of trades) {
        const d = t.data;
        parts.push(`[${this.formatDate(t.timestamp)}] ${d.tradeType} ${d.quantity} cp @ ${d.price}`);
      }
    }

    parts.push('\n## Yêu cầu phân tích');
    parts.push('1. Mẫu cảm xúc lặp lại và ảnh hưởng đến P&L');
    parts.push('2. Bias hành vi cần khắc phục (ưu tiên severity cao)');
    parts.push('3. Điểm mạnh cần phát huy');
    parts.push('4. 3 hành động cụ thể để cải thiện kỷ luật giao dịch');
    parts.push('5. Đánh giá tổng thể: điểm kỷ luật (0-10), xu hướng cải thiện hay xấu đi');

    this.aiContext = parts.join('\n');
  }

  /** Normalize PascalCase keys from .NET backend to camelCase */
  private normalizeTimeline(timeline: SymbolTimeline): SymbolTimeline {
    timeline.items = timeline.items.map(item => ({
      ...item,
      data: this.toCamelCase(item.data)
    }));
    return timeline;
  }

  private toCamelCase(obj: any): any {
    if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return obj;
    const result: any = {};
    for (const key of Object.keys(obj)) {
      const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
      result[camelKey] = obj[key];
    }
    return result;
  }

  private resetJournalForm() {
    this.journalForm = {
      entryType: 'Observation', title: '', content: '', emotionalState: '',
      confidenceLevel: 5, priceAtTime: undefined, marketContext: '', tagsInput: '', timestamp: ''
    };
    this.creatingEntry = false;
  }

  private resetEventForm() {
    this.eventForm = { eventType: 'News', title: '', eventDate: '', description: '', source: '' };
    this.creatingEvent = false;
  }
}
