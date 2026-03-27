import { Component, OnInit, OnDestroy, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { createChart, IChartApi, ISeriesApi, CandlestickData, Time, SeriesMarker, LineData } from 'lightweight-charts';
import { JournalEntryService, SymbolTimeline, TimelineItem, CreateJournalEntryRequest } from '../../core/services/journal-entry.service';
import { MarketEventService, CreateMarketEventRequest } from '../../core/services/market-event.service';
import { MarketDataService, StockPrice } from '../../core/services/market-data.service';
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
          <button (click)="showAiPanel = !showAiPanel"
            class="px-4 py-1.5 bg-purple-600 text-white rounded-lg text-sm font-medium hover:bg-purple-700">
            AI Review
          </button>
        </div>
      </div>

      <!-- Quick-add Journal Form -->
      <div *ngIf="showJournalForm" class="bg-white rounded-xl shadow-sm border border-gray-200 p-5 mb-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-4">Ghi nhật ký — {{ symbol }}</h3>
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
        <div class="flex gap-2 mt-4">
          <button (click)="createJournalEntry()" [disabled]="creatingEntry"
            class="px-5 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50">
            {{ creatingEntry ? 'Đang lưu...' : 'Lưu nhật ký' }}
          </button>
          <button (click)="showJournalForm = false"
            class="px-5 py-2 bg-gray-100 text-gray-700 rounded-lg text-sm font-medium hover:bg-gray-200">
            Hủy
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
      <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6">
        <div #chartContainer class="w-full" style="height: 420px;"></div>

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

  symbol = '';
  timeline: SymbolTimeline | null = null;
  priceHistory: StockPrice[] = [];
  loading = false;
  selectedRange = 3; // months
  selectedItemIndex = -1;

  // Forms
  showJournalForm = false;
  showEventForm = false;
  showAiPanel = false;
  creatingEntry = false;
  creatingEvent = false;

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
  private emotionSeries: ISeriesApi<'Histogram'> | null = null;

  // Derived
  topEmotion = '';
  emotionDistribution: { label: string; count: number; percent: number }[] = [];

  // AI context (7C.3)
  aiContext = '';

  constructor(
    private route: ActivatedRoute,
    private journalEntryService: JournalEntryService,
    private marketEventService: MarketEventService,
    private marketDataService: MarketDataService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    this.symbol = (this.route.snapshot.paramMap.get('symbol') || '').toUpperCase();
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
          this.loading = false;
        },
        error: () => {
          this.loading = false;
          this.notificationService.error('Lỗi', 'Không thể tải dữ liệu timeline');
        }
      });

    this.marketDataService.getPriceHistory(this.symbol, fromStr, toStr)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (prices) => {
          this.priceHistory = prices;
          this.updateChartData();
        },
        error: () => {
          this.notificationService.error('Lỗi', 'Không thể tải dữ liệu giá');
        }
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

    this.candleSeries = this.chart.addCandlestickSeries({
      upColor: '#22c55e',
      downColor: '#ef4444',
      borderUpColor: '#16a34a',
      borderDownColor: '#dc2626',
      wickUpColor: '#16a34a',
      wickDownColor: '#dc2626'
    });

    // Emotion histogram (7C.1)
    this.emotionSeries = this.chart.addHistogramSeries({
      priceScaleId: 'emotion',
      priceFormat: { type: 'volume' }
    });
    this.chart.priceScale('emotion').applyOptions({
      scaleMargins: { top: 0.85, bottom: 0 }
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
    if (!this.candleSeries || this.priceHistory.length === 0) return;

    const candles: CandlestickData[] = this.priceHistory
      .sort((a, b) => new Date(a.date).getTime() - new Date(b.date).getTime())
      .map(p => ({
        time: p.date.split('T')[0] as Time,
        open: p.open,
        high: p.high,
        low: p.low,
        close: p.close
      }));

    this.candleSeries.setData(candles);
    this.chart?.timeScale().fitContent();
    this.updateChartMarkers();
    this.updateEmotionRibbon();
  }

  private updateChartMarkers() {
    if (!this.candleSeries || !this.timeline) return;

    const markers: SeriesMarker<Time>[] = [];

    for (const item of this.timeline.items) {
      const time = item.timestamp.split('T')[0] as Time;

      if (item.type === 'journal') {
        markers.push({
          time,
          position: 'aboveBar',
          color: '#6366f1',
          shape: 'circle',
          text: '📓'
        });
      } else if (item.type === 'trade') {
        const isBuy = (item.data.tradeType) === 'BUY';
        markers.push({
          time,
          position: isBuy ? 'belowBar' : 'aboveBar',
          color: isBuy ? '#22c55e' : '#ef4444',
          shape: isBuy ? 'arrowUp' : 'arrowDown',
          text: isBuy
            ? `MUA ${item.data.quantity}`
            : `BÁN ${item.data.quantity}`
        });
      } else if (item.type === 'event') {
        markers.push({
          time,
          position: 'aboveBar',
          color: '#f59e0b',
          shape: 'square',
          text: this.getEventIcon(item.data.eventType)
        });
      } else if (item.type === 'alert') {
        markers.push({
          time,
          position: 'aboveBar',
          color: '#f97316',
          shape: 'square',
          text: '⚠️'
        });
      }
    }

    // Sort markers by time (required by lightweight-charts)
    markers.sort((a, b) => (a.time as string).localeCompare(b.time as string));
    this.candleSeries.setMarkers(markers);
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

  // ===== Filters =====

  applyFilters() {
    if (!this.timeline) return;
    const activeTypes = this.filterOptions.filter(f => f.checked).map(f => f.type);
    this.filteredItems = this.timeline.items.filter(item => activeTypes.includes(item.type));
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
      timestamp: this.journalForm.timestamp || undefined
    };

    this.journalEntryService.create(req).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Đã lưu nhật ký');
        this.showJournalForm = false;
        this.resetJournalForm();
        this.loadData();
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không thể lưu nhật ký');
        this.creatingEntry = false;
      }
    });
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

  // ===== Helpers =====

  selectTimelineItem(index: number) {
    this.selectedItemIndex = index;
  }

  getEntryTypeLabel(type: string): string {
    return ENTRY_TYPE_LABELS[type] || type;
  }

  getEventIcon(type: string): string {
    return EVENT_ICONS[type] || '📰';
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
  }

  // 7C.3 — Build AI context
  private buildAiContext() {
    if (!this.timeline) return;
    const parts: string[] = [
      `Symbol: ${this.symbol}`,
      `Tổng sự kiện: ${this.timeline.items.length}`,
      `Đợt nắm giữ: ${this.timeline.holdingPeriods.length}`
    ];

    if (this.timeline.emotionSummary) {
      parts.push(`Tự tin TB: ${this.timeline.emotionSummary.averageConfidence?.toFixed(1) || 'N/A'}`);
      parts.push(`Phân bố cảm xúc: ${JSON.stringify(this.timeline.emotionSummary.distribution)}`);
    }

    // Include journal entries for AI
    const journals = this.timeline.items.filter(i => i.type === 'journal');
    if (journals.length > 0) {
      parts.push('\n--- Nhật ký gần nhất ---');
      for (const j of journals.slice(-10)) {
        const d = j.data;
        parts.push(
          `[${this.formatDate(j.timestamp)}] ${d.entryType}: ${d.title}` +
          (d.emotionalState ? ` | Cảm xúc: ${d.emotionalState}` : '') +
          (d.confidenceLevel ? ` (${d.confidenceLevel}/10)` : '')
        );
      }
    }

    // Include trades for AI
    const trades = this.timeline.items.filter(i => i.type === 'trade');
    if (trades.length > 0) {
      parts.push('\n--- Giao dịch ---');
      for (const t of trades) {
        const d = t.data;
        parts.push(
          `[${this.formatDate(t.timestamp)}] ${d.tradeType} ${d.quantity} cp @ ${d.price}`
        );
      }
    }

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
