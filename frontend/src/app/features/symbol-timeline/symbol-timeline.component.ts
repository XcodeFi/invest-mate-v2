import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { ThemeService } from '../../core/services/theme.service';
import { ensureContrastOnDark } from '../../shared/theme/contrast';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, Subscription, forkJoin, of } from 'rxjs';
import { takeUntil, catchError } from 'rxjs/operators';
import { JournalEntryService, SymbolTimeline, TimelineItem, CreateJournalEntryRequest } from '../../core/services/journal-entry.service';
import { MarketEventService, CreateMarketEventRequest } from '../../core/services/market-event.service';
import { MarketDataService, PatternCandidate, TechnicalAnalysis, TechnicalSeries } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';
import { IndicatorChartComponent } from '../../shared/components/indicator-chart/indicator-chart.component';
import { GroupLayers, IndicatorChartCandles, IndicatorChartMarker } from '../../shared/components/indicator-chart/indicator-chart.types';
import { firstStarredGroup } from '../../shared/indicators/ta-group-stars';
import { IndicatorGroupBarComponent } from '../../shared/components/indicator-group-bar/indicator-group-bar.component';
import { IndicatorGuideComponent } from '../../shared/components/indicator-guide/indicator-guide.component';
import { TrendTemplateCardComponent } from '../../shared/components/trend-template-card/trend-template-card.component';
import { PatternCardComponent } from '../../shared/patterns/pattern-card.component';
import { withPattern } from '../../shared/patterns/pattern-layers';
import { SeriesAdjustedNotesComponent } from '../../shared/components/series-adjusted-notes/series-adjusted-notes.component';
import { buildGroupLayers, TaGroup } from '../../shared/indicators/ta-group-layers';
import { buildTimelineQuery, parseTimelineQuery, TimelineUrlState } from './timeline-url-state';
import { readNow } from '../../shared/indicators/indicator-guides';
import { sliceSeriesFrom } from '../../shared/indicators/slice-series';
import { dossierBadge, DossierAtTradeDto, DossierBadge } from './dossier-at-trade';
import { ThesisCardComponent } from './thesis-card.component';
import { vnDay } from '../../shared/indicators/vn-date';
import { buildEmotionPane, buildTimelineMarkers } from './timeline-markers';

/**
 * Cửa sổ lịch sử cố định (tháng) để tính chỉ báo, KHÔNG phụ thuộc khoảng đang xem: EMA 200 và các chỉ báo đệ quy khác phụ thuộc điểm
 * bắt đầu, nên cửa sổ đổi theo khoảng thì giá trị phiên cuối cũng đổi theo. Là mức tối đa backend nhận và đủ dài cho mọi khoảng của trang.
 */
const INDICATOR_WINDOW_MONTHS = 36;

/** Nền thẻ ở chế độ tối (--dm-surface). */
const DARK_CARD_BG = '#111827';

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
  imports: [CommonModule, FormsModule, RouterModule, ThesisCardComponent, VndCurrencyPipe, NumMaskDirective, AiChatPanelComponent,
    IndicatorChartComponent, IndicatorGroupBarComponent, IndicatorGuideComponent, SeriesAdjustedNotesComponent, PatternCardComponent, TrendTemplateCardComponent],
  template: `
    <div class="max-w-7xl mx-auto px-4 py-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between mb-6 gap-3">
        <div>
          <div class="flex items-center gap-3 flex-wrap">
            <h1 class="text-2xl font-bold text-gray-900">
              {{ symbol }} — Dòng thời gian
            </h1>
            <!-- Đường sang hồ sơ ở CẤP TRANG. Link trong feed chỉ hiện khi hồ sơ đã có mốc ký hoặc
                 mốc agent, nên mã chưa có hồ sơ — đúng lúc cần viết nhất — lại không có đường nào. -->
            <a [routerLink]="['/company-dossier', symbol]" data-testid="header-dossier-link"
              class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg border border-indigo-200 bg-indigo-50 text-indigo-700 text-xs font-medium hover:bg-indigo-100">
              🏢 Hồ sơ công ty
            </a>
          </div>
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

      <!-- Biểu đồ: dùng lại biểu đồ nhiều tầng của trang Thị trường, thêm các mốc của dòng thời gian (không dính: trang dài, có feed bên dưới) -->
      <!-- Thẻ Luận điểm đứng cạnh biểu đồ khi màn rộng hơn 1024px, xếp dưới biểu đồ khi từ 1024px trở xuống -->
      <div class="flex flex-col min-[1025px]:flex-row gap-4 mb-4 items-start" data-testid="timeline-chart-row">
      <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 w-full min-w-0 min-[1025px]:flex-1" data-testid="timeline-chart-card">
        <app-indicator-group-bar [selected]="selectedGroup" (groupToggle)="toggleGroup($event)"></app-indicator-group-bar>
        @if (analysisFailed) {
          <p class="mb-2 text-xs text-amber-700" data-testid="timeline-analysis-error">Không tải được chỉ báo, thử lại sau</p>
        }
        @if (chartError) {
          <div role="alert" class="text-sm text-red-600 bg-red-50 rounded-lg px-3 py-2" data-testid="timeline-chart-error">{{ chartError }}</div>
        } @else if (series && series.dates.length === 0) {
          <p class="text-sm text-gray-500 py-8 text-center" data-testid="timeline-chart-empty">Không có dữ liệu giá trong khoảng này</p>
        } @else if (series) {
          <app-indicator-chart [dates]="series.dates" [candles]="chartCandles" [layers]="groupLayers" [markers]="chartMarkers"></app-indicator-chart>
        } @else {
          <p class="text-sm text-gray-500 py-8 text-center" data-testid="timeline-chart-loading">Đang tải biểu đồ...</p>
        }
        <p class="mt-2 text-xs text-gray-500" data-testid="timeline-marker-legend">
          Mốc trên biểu đồ: T = giao dịch (mũi tên lên là mua, xuống là bán), J = nhật ký, E = sự kiện, A = cảnh báo, H = hồ sơ công ty. Trỏ chuột vào phiên có mốc để xem chi tiết.
        </p>

        <!-- Emotion Ribbon Legend -->
        <div class="flex gap-3 mt-3 flex-wrap text-xs" *ngIf="timeline?.emotionSummary">
          <span *ngFor="let e of emotions" class="flex items-center gap-1">
            <span class="inline-block w-3 h-3 rounded-full" [style.background-color]="getEmotionColor(e)"></span>
            {{ e }}
          </span>
        </div>
      </div>
      <app-thesis-card class="w-full min-[1025px]:w-80 min-[1025px]:flex-shrink-0" [symbol]="symbol"></app-thesis-card>
      </div>

      <!-- Ghi chú chất lượng giá và khối Cách đọc của nhóm đang chọn -->
      <app-series-adjusted-notes [series]="series"></app-series-adjusted-notes>
      <div class="mb-6">
        <app-indicator-guide [group]="selectedGroup" [readNowLines]="readNowLines"></app-indicator-guide>
        <app-trend-template-card *ngIf="selectedGroup === 'xu-huong'" [template]="analysis?.trendTemplate"></app-trend-template-card>
        <app-pattern-card [candidate]="patternCandidate" [showOnChart]="showPattern" [chartReady]="!!series" (showOnChartChange)="setShowPattern($event)"></app-pattern-card>
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
              <span *ngIf="dossierBadgeOf(item.data) as badge" data-testid="dossier-at-trade"
                class="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full mt-1" [ngClass]="badge.cls" [title]="badge.tooltip">
                <span aria-hidden="true">{{ badge.icon }}</span>{{ badge.text }}
              </span>
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

          <div *ngIf="item.type === 'dossier'" class="flex gap-3">
            <div class="w-10 h-10 rounded-full bg-indigo-100 flex items-center justify-center text-lg flex-shrink-0">📋</div>
            <div class="flex-1">
              <div class="flex items-center gap-2">
                <span class="text-xs font-medium px-2 py-0.5 rounded-full bg-indigo-100 text-indigo-800">Hồ sơ công ty</span>
                <span class="text-xs text-gray-500">{{ formatDate(item.timestamp) }}</span>
              </div>
              <p class="font-medium text-gray-900 mt-1">{{ dossierMarkerLabel(item.data?.action) }}</p>
              <a [routerLink]="['/company-dossier', symbol]"
                class="text-xs text-indigo-600 hover:underline">→ Xem hồ sơ</a>
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
export class SymbolTimelineComponent implements OnInit, OnDestroy {
  symbol = '';
  timeline: SymbolTimeline | null = null;
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
    { type: 'alert', label: '⚠️ Cảnh báo', checked: true },
    { type: 'dossier', label: '📋 Hồ sơ công ty', checked: true }
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
  /** Phát mỗi lần bắt đầu tải mới để huỷ các request của lần tải trước (đổi khoảng nhanh thì câu trả lời cũ đến muộn không ghi đè). */
  private cancelLoad$ = new Subject<void>();

  // Biểu đồ chỉ báo theo nhóm: chuỗi tải ngay khi vào trang (cần cho nến), nhóm chỉ đổi lớp vẽ
  selectedGroup: TaGroup | null = null;
  series: TechnicalSeries | null = null;
  private analysis: TechnicalAnalysis | null = null;
  chartError: string | null = null;
  /** Mã mà `fullSeries` thuộc về; null khi chưa tải hoặc lần tải lỗi (lần loadData sau sẽ tải lại). */
  private indicatorSymbol: string | null = null;
  private fullSeries: TechnicalSeries | null = null;
  /** Lần tải chỉ báo đang chạy: lần tải mới huỷ lần cũ để câu trả lời hoặc lỗi đến muộn của lần cũ không ghi đè lần mới. */
  private indicatorSub: Subscription | null = null;
  /** Phân tích (các lớp của nhóm chỉ báo) không tải được: hiện một dòng báo thay vì để các nhóm trống lặng lẽ. */
  analysisFailed = false;
  chartCandles: IndicatorChartCandles = { open: [], high: [], low: [], close: [] };
  groupLayers: GroupLayers = { overlays: [], panes: [], priceLines: [], notes: [] };

  /** Vẽ cốc, pivot, cắt lỗ, mục tiêu của ứng viên mô hình lên biểu đồ (bật sẵn; thẻ ứng viên có ô tắt). */
  showPattern = true;
  get patternCandidate(): PatternCandidate | null { return this.analysis?.patternCandidates?.[0] ?? null; }
  setShowPattern(on: boolean): void {
    this.showPattern = on;
    this.refreshChart();
  }
  chartMarkers: IndicatorChartMarker[] = [];
  readNowLines: string[] = [];

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

  private themeService = inject(ThemeService);

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
    // Trạng thái trang (range, grp, focus) nằm trên URL: đọc qua queryParamMap (không phải snapshot) để Back và link dán lại đúng trạng thái.
    // Lần phát đầu tiên áp dụng trạng thái ban đầu và tải dữ liệu; các lần sau chỉ áp dụng khoá nào ĐỔI.
    this.route.queryParamMap.pipe(takeUntil(this.destroy$)).subscribe(q => this.onUrlState(parseTimelineQuery(q)));
  }

  // ===== Trạng thái trên URL =====

  /** Những gì URL đang nói (hoặc ta vừa ghi lên đó), để lần phát lại của chính ta và các khoá không liên quan (tradeId) là no-op. */
  private urlState: TimelineUrlState = {};
  private firstUrlState = true;
  /** Ngày của mục đang chọn (YYYY-MM-DD theo giờ VN); null khi chưa chọn. */
  private focusDate: string | null = null;
  /** Cuộn tới mục focus ở lần áp dụng kế tiếp (chỉ khi focus tới từ URL, không phải từ cú bấm của chính người dùng). */
  private scrollToFocus = false;

  private onUrlState(next: TimelineUrlState): void {
    const prev = this.urlState;
    this.urlState = next;

    if (this.firstUrlState) {
      this.firstUrlState = false;
      // Luồng review (tradeId) cần khoảng 12 tháng để thấy lệnh cũ: tradeId thắng range trên URL
      if (next.range !== undefined && !this.reviewTradeId) this.selectedRange = next.range;
      // URL thắng; không có thì mở sẵn nhóm gắn sao đầu tiên (chỉ ở lần tải đầu, không ghi ngược lên URL)
      this.selectedGroup = next.group ?? firstStarredGroup();
      this.focusDate = next.focus ?? null;
      this.scrollToFocus = this.focusDate !== null;
      this.loadData();
      return;
    }

    let reload = false;
    if (next.range !== prev.range) {
      this.selectedRange = next.range ?? (this.reviewTradeId ? 12 : 3);
      reload = true;
    }
    if (next.group !== prev.group) {
      this.selectedGroup = next.group ?? null;
      if (!reload) this.refreshChart();
    }
    if (next.focus !== prev.focus) {
      this.focusDate = next.focus ?? null;
      this.scrollToFocus = this.focusDate !== null;
      if (!reload) this.applyFocus();
    }
    if (reload) this.loadData();   // loadData áp dụng lại focus sau khi tải xong
  }

  /** Ghi range/grp/focus lên URL: merge để không đụng symbol và tradeId, replaceUrl để cú bấm trên biểu đồ không chất thêm mục lịch sử. */
  private writeUrlState(): void {
    this.urlState = {
      range: this.selectedRange,
      group: this.selectedGroup ?? undefined,
      focus: this.focusDate ?? undefined
    };
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: buildTimelineQuery({ range: this.selectedRange, group: this.selectedGroup, focus: this.focusDate }),
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  /** Chọn mục có ngày (giờ VN) trùng focus; nếu focus tới từ URL thì cuộn tới mục đó. Không có mục nào thì không chọn gì. */
  private applyFocus(): void {
    if (!this.focusDate) return;
    const index = this.filteredItems.findIndex(i => vnDay(i.timestamp) === this.focusDate);
    this.selectedItemIndex = index;
    if (this.scrollToFocus && index >= 0) {
      setTimeout(() => document.getElementById('timeline-item-' + index)?.scrollIntoView({ block: 'center', behavior: 'smooth' }));
    }
    this.scrollToFocus = false;
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  setDateRange(months: number) {
    this.selectedRange = months;
    this.writeUrlState();
    this.loadData();
  }

  loadData() {
    this.cancelLoad$.next();
    this.loading = true;
    const to = new Date();
    const from = new Date();
    from.setMonth(from.getMonth() - this.selectedRange);
    // Ngày theo giờ Việt Nam: toISOString() là ngày UTC, lệch một ngày trong 7 giờ đầu mỗi ngày
    const fromStr = vnDay(from);
    const toStr = vnDay(to);

    // Load timeline + price history in parallel
    this.journalEntryService.getTimeline(this.symbol, fromStr, toStr)
      .pipe(takeUntil(this.destroy$), takeUntil(this.cancelLoad$))
      .subscribe({
        next: (timeline) => {
          this.timeline = this.normalizeTimeline(timeline);
          this.filteredItems = [...this.timeline!.items];
          this.applyFilters();
          this.applyFocus();
          this.computeEmotionStats();
          this.buildAiContext();
          this.refreshChart();
          this.tryBeginReviewMode();
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.error('Lỗi', 'Không thể tải dữ liệu timeline');
        }
      });

    // Chỉ báo tính trên cùng một cửa sổ cố định nên chỉ tải MỘT lần cho mỗi mã; đổi khoảng chỉ cắt lại chuỗi đã có.
    // Chỉ dòng thời gian phụ thuộc khoảng (ở trên).
    if (this.indicatorSymbol === this.symbol && !this.analysisFailed) {
      this.applyRange();   // đã có (hoặc đang tải: câu trả lời sẽ được cắt theo khoảng lúc nó đến)
    } else {   // chưa tải, tải lỗi, hoặc phân tích lỗi: tải lại khi người dùng đổi khoảng
      this.loadIndicators();
    }
  }

  private loadIndicators(): void {
    this.indicatorSub?.unsubscribe();
    this.indicatorSymbol = this.symbol;
    this.fullSeries = null;
    this.chartError = null;
    this.analysisFailed = false;
    this.indicatorSub = forkJoin([
      this.marketDataService.getTechnicalSeries(this.symbol, INDICATOR_WINDOW_MONTHS),
      this.marketDataService.getTechnicalAnalysis(this.symbol).pipe(catchError(() => {
        this.analysisFailed = true;
        return of(null);
      }))
    ]).pipe(takeUntil(this.destroy$)).subscribe({
      next: ([series, analysis]) => {
        this.fullSeries = series;
        this.analysis = analysis;
        this.applyRange();
      },
      error: () => {
        // Lỗi thì bỏ hẳn chuỗi cũ (biểu đồ và các ghi chú chỉ vẽ khi còn chuỗi) và cho phép tải lại ở lần đổi khoảng sau
        this.indicatorSymbol = null;
        this.fullSeries = null;
        this.analysis = null;
        this.series = null;
        this.chartError = 'Không tải được dữ liệu biểu đồ';
        this.notificationService.error('Lỗi', 'Không thể tải dữ liệu giá');
      }
    });
  }

  /** Cắt chuỗi đã tải theo khoảng đang chọn (tính theo ngày Việt Nam) rồi dựng lại biểu đồ. */
  private applyRange(): void {
    if (!this.fullSeries) return;
    const from = new Date();
    from.setMonth(from.getMonth() - this.selectedRange);
    this.series = sliceSeriesFrom(this.fullSeries, vnDay(from));
    this.chartCandles = { open: this.series.open, high: this.series.high, low: this.series.low, close: this.series.close };
    this.refreshChart();
  }

  // ===== Biểu đồ =====

  /** Bấm nhóm đang chọn thì bỏ chọn (chỉ còn nến và các mốc); bấm nhóm khác thì vẽ nhóm đó. */
  toggleGroup(group: TaGroup): void {
    this.selectedGroup = this.selectedGroup === group ? null : group;
    this.refreshChart();
    this.writeUrlState();
  }

  /** Dựng lại lớp vẽ, các mốc và các câu đọc nhanh từ chuỗi, phân tích và dòng thời gian hiện có. */
  private refreshChart(): void {
    const series = this.series;
    if (!series) return;

    const base = withPattern(
      this.analysis
        ? buildGroupLayers(this.selectedGroup, series, this.analysis)
        : { overlays: [], panes: [], priceLines: [], notes: [] } as GroupLayers,
      this.patternCandidate, series, this.showPattern);
    const items = this.timeline?.items ?? [];
    const emotionPane = buildEmotionPane(series.dates, items, EMOTION_COLORS, '#6b7280');
    this.groupLayers = emotionPane ? { ...base, panes: [...base.panes, emotionPane] } : base;
    this.chartMarkers = buildTimelineMarkers(items, {
      adjusted: series.adjusted, bars: { dates: series.dates, low: series.low, high: series.high },
      dossierLabel: a => this.dossierMarkerLabel(a)
    });
    this.readNowLines = this.selectedGroup && this.analysis ? readNow(this.selectedGroup, series, this.analysis) : [];
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

  /** Nhãn tiếng Việt cho mốc hồ sơ. Action lạ thì vẫn nói được điều gì đó, không trả rỗng. */
  dossierMarkerLabel(action: string | undefined): string {
    switch (action) {
      case 'signed': return 'Ký hồ sơ công ty';
      case 'agent-drafted': return 'Trợ lý AI sửa hồ sơ — chờ bạn ký lại';
      default: return 'Cập nhật hồ sơ công ty';
    }
  }

  /** Tách khỏi exportCsv để test được từng loại — nhánh default trả [] là một dòng trống giữa file. */
  private csvRow(item: TimelineItem): any[] {
    const d: any = item.data;
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
      case 'dossier':
        return [this.formatDate(item.timestamp), 'Hồ sơ công ty', this.dossierMarkerLabel(d?.action),
          '', '', '', `phiên bản ${d?.version ?? ''}`];
      default:
        return [];
    }
  }

  exportCsv() {
    if (!this.timeline) return;
    const headers = ['Ngày', 'Loại', 'Tiêu đề', 'Cảm xúc', 'Confidence', 'Giá', 'Ghi chú'];
    const rows = this.filteredItems.map(item => this.csvRow(item));

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

  selectTimelineItem(index: number) {
    this.selectedItemIndex = index;
    const item = this.filteredItems[index];
    this.focusDate = item ? vnDay(item.timestamp) : null;
    this.writeUrlState();
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

  dossierBadgeOf(data: { dossierAtTrade?: DossierAtTradeDto | null }): DossierBadge | null {
    return dossierBadge(data?.dossierAtTrade);
  }

  /** Màu cảm xúc; ở chế độ tối, màu quá tối so với nền thẻ (FOMO #1f2937) được làm sáng, màu đủ tương phản giữ nguyên. */
  getEmotionColor(emotion: string): string {
    const color = EMOTION_COLORS[emotion] || '#6b7280';
    return this.themeService.theme() === 'dark' ? ensureContrastOnDark(color, DARK_CARD_BG, 4.5) : color;
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

    // Pre-compute distribution array per trend so the template *ngFor doesn't
    // allocate a fresh sorted array on every change-detection pass.
    const trends = this.timeline.emotionSummary.trends;
    if (Array.isArray(trends)) {
      for (const t of trends) {
        (t as any)._distributionArray = this.computeTrendDistribution(t);
      }
    }

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

  // P7.6: Convert trend distribution to array for stacked bar.
  // Returns the trend's pre-computed `_distributionArray` (set during
  // computeEmotionStats) so the *ngFor in the template gets a stable array
  // reference per CD pass instead of allocating a fresh sorted array each time.
  private static readonly EMPTY_TREND_DIST: ReadonlyArray<{ label: string; count: number; percent: number }> = [];

  getTrendDistribution(trend: any): ReadonlyArray<{ label: string; count: number; percent: number }> {
    return trend?._distributionArray ?? SymbolTimelineComponent.EMPTY_TREND_DIST;
  }

  private computeTrendDistribution(trend: any): { label: string; count: number; percent: number }[] {
    if (!trend?.distribution) return [];
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
