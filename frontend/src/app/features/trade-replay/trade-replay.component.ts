import { Component, OnInit, OnDestroy, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

import {
  TradePlanService, TradePlan, PlanLotDto, ExitTargetDto, StopLossHistoryDto
} from '../../core/services/trade-plan.service';
import { MarketDataService, StockPrice } from '../../core/services/market-data.service';
import { PortfolioService, TradeResponseItem, TradeListResponse } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

interface TimelineEvent {
  date: Date;
  type: 'plan_created' | 'lot_executed' | 'stop_loss_changed' | 'exit_triggered' | 'plan_completed';
  title: string;
  description: string;
  color: string;
  icon: string;
}

@Component({
  selector: 'app-trade-replay',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex items-center py-6">
            <button routerLink="/trade-plan" class="mr-4 text-gray-500 hover:text-gray-700">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
              </svg>
            </button>
            <div class="flex-1">
              <div class="flex items-center gap-3">
                <h1 class="text-2xl font-bold text-gray-900">Replay: {{ plan?.symbol }}</h1>
                <span *ngIf="plan" class="text-sm px-2 py-0.5 rounded-full font-medium"
                  [class]="plan.direction === 'Buy' ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'">
                  {{ plan.direction === 'Buy' ? 'LONG' : 'SHORT' }}
                </span>
                <span *ngIf="plan" class="text-sm px-2 py-0.5 rounded-full font-medium"
                  [class]="plan.status === 'Reviewed' ? 'bg-purple-100 text-purple-700' : 'bg-blue-100 text-blue-700'">
                  {{ plan.status === 'Reviewed' ? 'Đã review' : 'Đã thực hiện' }}
                </span>
              </div>
              <p class="text-gray-500 text-sm mt-1" *ngIf="plan">
                Tạo ngày {{ plan.createdAt | date:'dd/MM/yyyy HH:mm' }}
                <span *ngIf="plan.marketCondition"> · {{ plan.marketCondition }}</span>
              </p>
            </div>
          </div>
        </div>
      </div>

      <!-- Loading -->
      <div *ngIf="isLoading" class="max-w-7xl mx-auto py-16 text-center">
        <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
        <p class="mt-4 text-gray-500">Đang tải dữ liệu replay...</p>
      </div>

      <!-- Error -->
      <div *ngIf="error" class="max-w-7xl mx-auto px-4 py-12 text-center">
        <p class="text-red-600 mb-4">{{ error }}</p>
        <button routerLink="/trade-plan" class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700">Quay lại</button>
      </div>

      <!-- Content -->
      <div *ngIf="!isLoading && !error && plan" class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-8">

        <!-- Summary Cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
            <div class="text-xs text-gray-500 uppercase tracking-wide mb-1">Giá vào lệnh</div>
            <div class="text-lg font-bold text-gray-900">{{ actualAvgEntry | number:'1.0-0' }}đ</div>
            <div class="text-xs mt-1" [class]="entryDiffPercent >= 0 ? 'text-red-500' : 'text-green-500'">
              KH: {{ plan.entryPrice | number:'1.0-0' }}đ
              ({{ entryDiffPercent >= 0 ? '+' : '' }}{{ entryDiffPercent | number:'1.1-1' }}%)
            </div>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
            <div class="text-xs text-gray-500 uppercase tracking-wide mb-1">Lãi/Lỗ</div>
            <div class="text-lg font-bold" [class]="totalPnL >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ totalPnL >= 0 ? '+' : '' }}{{ totalPnL | vndCurrency }}
            </div>
            <div class="text-xs mt-1" [class]="totalPnLPercent >= 0 ? 'text-green-500' : 'text-red-500'">
              {{ totalPnLPercent >= 0 ? '+' : '' }}{{ totalPnLPercent | number:'1.2-2' }}%
            </div>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
            <div class="text-xs text-gray-500 uppercase tracking-wide mb-1">R:R (KH / TT)</div>
            <div class="text-lg font-bold text-gray-900">
              1:{{ plannedRR | number:'1.1-1' }} / 1:{{ actualRR | number:'1.1-1' }}
            </div>
            <div class="text-xs text-gray-400 mt-1">Rủi ro / Lợi nhuận</div>
          </div>
          <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
            <div class="text-xs text-gray-500 uppercase tracking-wide mb-1">Phí giao dịch</div>
            <div class="text-lg font-bold text-gray-900">{{ totalFees | vndCurrency }}</div>
            <div class="text-xs text-gray-400 mt-1">{{ linkedTrades.length }} giao dịch</div>
          </div>
        </div>

        <!-- Plan Info -->
        <div *ngIf="plan.thesis || plan.notes" class="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
          <h3 class="text-sm font-semibold text-gray-700 mb-2">Thesis & Ghi chú</h3>
          <p *ngIf="plan.thesis" class="text-sm text-gray-600 mb-1"><strong>Thesis:</strong> {{ plan.thesis }}</p>
          <p *ngIf="plan.notes" class="text-sm text-gray-600"><strong>Ghi chú:</strong> {{ plan.notes }}</p>
        </div>

        <!-- Chart -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
          <h3 class="text-sm font-semibold text-gray-700 mb-3">Biểu đồ giá & Sự kiện giao dịch</h3>
          <div *ngIf="priceData.length > 0" class="h-96">
            <canvas #replayChart></canvas>
          </div>
          <div *ngIf="priceData.length === 0" class="h-96 flex items-center justify-center text-gray-400">
            Chưa có dữ liệu giá cho {{ plan.symbol }}
          </div>
          <!-- Legend -->
          <div *ngIf="priceData.length > 0" class="flex flex-wrap gap-4 mt-3 text-xs text-gray-500">
            <span class="flex items-center gap-1"><span class="w-3 h-3 rounded-full bg-gray-500 inline-block"></span> Giá đóng cửa</span>
            <span class="flex items-center gap-1"><span class="w-3 h-3 inline-block" style="width:0;height:0;border-left:6px solid transparent;border-right:6px solid transparent;border-bottom:10px solid #10B981"></span> Vào lệnh</span>
            <span class="flex items-center gap-1"><span class="w-3 h-3 inline-block" style="width:0;height:0;border-left:6px solid transparent;border-right:6px solid transparent;border-top:10px solid #EF4444"></span> Thoát lệnh</span>
            <span class="flex items-center gap-1"><span class="w-8 border-t-2 border-dashed border-red-400 inline-block"></span> Stop-Loss</span>
            <span class="flex items-center gap-1"><span class="w-8 border-t-2 border-dashed border-green-400 inline-block"></span> Mục tiêu</span>
          </div>
        </div>

        <!-- Timeline -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-4">
          <h3 class="text-sm font-semibold text-gray-700 mb-4">Dòng thời gian sự kiện</h3>
          <div *ngIf="timeline.length === 0" class="text-sm text-gray-400 text-center py-4">Chưa có sự kiện nào</div>
          <div class="relative">
            <div *ngIf="timeline.length > 0" class="absolute left-4 top-0 bottom-0 w-0.5 bg-gray-200"></div>
            <div *ngFor="let event of timeline; let last = last" class="relative pl-10 pb-6" [class.pb-0]="last">
              <div class="absolute left-2.5 w-3 h-3 rounded-full border-2 border-white shadow-sm" [style.background-color]="event.color"></div>
              <div class="text-xs text-gray-400 mb-0.5">{{ event.date | date:'dd/MM/yyyy HH:mm' }}</div>
              <div class="text-sm font-medium text-gray-800">{{ event.title }}</div>
              <div *ngIf="event.description" class="text-xs text-gray-500 mt-0.5">{{ event.description }}</div>
            </div>
          </div>
        </div>

      </div>
    </div>
  `,
  styles: []
})
export class TradeReplayComponent implements OnInit, OnDestroy, AfterViewInit {
  @ViewChild('replayChart') replayCanvas!: ElementRef<HTMLCanvasElement>;
  private chart: Chart | null = null;

  plan: TradePlan | null = null;
  priceData: StockPrice[] = [];
  linkedTrades: TradeResponseItem[] = [];
  timeline: TimelineEvent[] = [];
  isLoading = true;
  error: string | null = null;

  // Summary
  actualAvgEntry = 0;
  entryDiffPercent = 0;
  totalPnL = 0;
  totalPnLPercent = 0;
  plannedRR = 0;
  actualRR = 0;
  totalFees = 0;

  private dateLabels: string[] = [];

  constructor(
    private route: ActivatedRoute,
    private tradePlanService: TradePlanService,
    private marketDataService: MarketDataService,
    private portfolioService: PortfolioService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error = 'Không tìm thấy ID kế hoạch';
      this.isLoading = false;
      return;
    }

    this.tradePlanService.getById(id).pipe(
      switchMap(plan => {
        this.plan = plan;
        const { from, to } = this.computeDateRange(plan);
        const trades$ = plan.portfolioId
          ? this.portfolioService.getTrades(plan.portfolioId, { symbol: plan.symbol, pageSize: 100 })
          : of(null);
        return forkJoin([
          this.marketDataService.getPriceHistory(plan.symbol, from, to),
          trades$
        ]);
      })
    ).subscribe({
      next: ([prices, tradesResp]) => {
        this.priceData = prices || [];
        this.dateLabels = this.priceData.map(p => p.date.split('T')[0]);
        if (tradesResp) {
          const planTradeIds = new Set(this.plan!.tradeIds || []);
          if (this.plan!.tradeId) planTradeIds.add(this.plan!.tradeId);
          (this.plan!.lots || []).forEach(l => { if (l.tradeId) planTradeIds.add(l.tradeId); });
          (this.plan!.exitTargets || []).forEach(t => { if (t.tradeId) planTradeIds.add(t.tradeId); });
          this.linkedTrades = tradesResp.items.filter(t => planTradeIds.has(t.id));
        }
        this.buildTimeline();
        this.computeSummary();
        this.isLoading = false;
        setTimeout(() => this.renderChart(), 50);
      },
      error: () => {
        this.error = 'Không thể tải dữ liệu. Vui lòng thử lại.';
        this.isLoading = false;
      }
    });
  }

  ngAfterViewInit(): void {
    if (!this.isLoading && this.priceData.length > 0) {
      this.renderChart();
    }
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  // --- Date Range ---

  private computeDateRange(plan: TradePlan): { from: string; to: string } {
    const dates: Date[] = [new Date(plan.createdAt)];
    (plan.lots || []).forEach(l => { if (l.executedAt) dates.push(new Date(l.executedAt)); });
    (plan.exitTargets || []).forEach(t => { if (t.triggeredAt) dates.push(new Date(t.triggeredAt)); });
    (plan.stopLossHistory || []).forEach(h => dates.push(new Date(h.changedAt)));
    if (plan.executedAt) dates.push(new Date(plan.executedAt));

    const earliest = new Date(Math.min(...dates.map(d => d.getTime())));
    const latest = new Date(Math.max(...dates.map(d => d.getTime())));

    const from = new Date(earliest);
    from.setDate(from.getDate() - 14);

    const hasOpenPosition = !plan.exitTargets?.some(t => t.isTriggered) && plan.status !== 'Reviewed';
    const to = hasOpenPosition ? new Date() : new Date(latest);
    to.setDate(to.getDate() + 14);

    return { from: from.toISOString().split('T')[0], to: to.toISOString().split('T')[0] };
  }

  // --- Timeline ---

  private buildTimeline(): void {
    const events: TimelineEvent[] = [];
    const plan = this.plan!;

    events.push({
      date: new Date(plan.createdAt),
      type: 'plan_created',
      title: 'Tạo kế hoạch giao dịch',
      description: `${plan.symbol} ${plan.direction} @ ${plan.entryPrice.toLocaleString('vi-VN')}đ | SL: ${plan.stopLoss.toLocaleString('vi-VN')}đ | TP: ${plan.target.toLocaleString('vi-VN')}đ`,
      color: '#3B82F6',
      icon: 'star'
    });

    (plan.lots || []).filter(l => l.status === 'Executed' && l.executedAt).forEach(l => {
      events.push({
        date: new Date(l.executedAt!),
        type: 'lot_executed',
        title: `Thực hiện Lô ${l.lotNumber}${l.label ? ' — ' + l.label : ''}`,
        description: `${l.plannedQuantity.toLocaleString('vi-VN')} CP @ ${(l.actualPrice || l.plannedPrice).toLocaleString('vi-VN')}đ`,
        color: '#10B981',
        icon: 'arrow-up'
      });
    });

    (plan.stopLossHistory || []).forEach(h => {
      events.push({
        date: new Date(h.changedAt),
        type: 'stop_loss_changed',
        title: 'Điều chỉnh Stop-Loss',
        description: `${h.oldPrice.toLocaleString('vi-VN')}đ → ${h.newPrice.toLocaleString('vi-VN')}đ${h.reason ? ' — ' + h.reason : ''}`,
        color: '#F59E0B',
        icon: 'shield'
      });
    });

    (plan.exitTargets || []).filter(t => t.isTriggered && t.triggeredAt).forEach(t => {
      const actionLabels: Record<string, string> = {
        'TakeProfit': 'Chốt lời',
        'CutLoss': 'Cắt lỗ',
        'TrailingStop': 'Trailing Stop',
        'PartialExit': 'Thoát một phần'
      };
      events.push({
        date: new Date(t.triggeredAt!),
        type: 'exit_triggered',
        title: `${actionLabels[t.actionType] || t.actionType} Mức ${t.level}${t.label ? ' — ' + t.label : ''}`,
        description: `@ ${t.price.toLocaleString('vi-VN')}đ${t.quantity ? ' · ' + t.quantity.toLocaleString('vi-VN') + ' CP' : ''}${t.percentOfPosition ? ' · ' + t.percentOfPosition + '% vị thế' : ''}`,
        color: '#EF4444',
        icon: 'arrow-down'
      });
    });

    if (plan.executedAt) {
      events.push({
        date: new Date(plan.executedAt),
        type: 'plan_completed',
        title: 'Hoàn thành kế hoạch',
        description: plan.status === 'Reviewed' ? 'Đã review' : 'Tất cả lệnh đã thực hiện',
        color: '#8B5CF6',
        icon: 'check'
      });
    }

    this.timeline = events.sort((a, b) => a.date.getTime() - b.date.getTime());
  }

  // --- Summary ---

  private computeSummary(): void {
    const plan = this.plan!;

    // Actual average entry
    const executedLots = (plan.lots || []).filter(l => l.status === 'Executed' && l.actualPrice);
    if (executedLots.length > 0) {
      const totalQty = executedLots.reduce((s, l) => s + l.plannedQuantity, 0);
      this.actualAvgEntry = totalQty > 0
        ? executedLots.reduce((s, l) => s + (l.actualPrice! * l.plannedQuantity), 0) / totalQty
        : plan.entryPrice;
    } else {
      this.actualAvgEntry = plan.entryPrice;
    }

    this.entryDiffPercent = plan.entryPrice > 0
      ? ((this.actualAvgEntry - plan.entryPrice) / plan.entryPrice) * 100
      : 0;

    // Planned R:R
    const plannedRisk = Math.abs(plan.entryPrice - plan.stopLoss);
    this.plannedRR = plannedRisk > 0 ? Math.abs(plan.target - plan.entryPrice) / plannedRisk : 0;

    // P&L from linked trades
    const buyTrades = this.linkedTrades.filter(t => t.tradeType === 'BUY');
    const sellTrades = this.linkedTrades.filter(t => t.tradeType === 'SELL');
    const totalBuyCost = buyTrades.reduce((s, t) => s + t.totalValue + t.fee + t.tax, 0);
    const totalSellRevenue = sellTrades.reduce((s, t) => s + t.totalValue - t.fee - t.tax, 0);

    if (sellTrades.length > 0 && buyTrades.length > 0) {
      this.totalPnL = totalSellRevenue - totalBuyCost;
      this.totalPnLPercent = totalBuyCost > 0 ? (this.totalPnL / totalBuyCost) * 100 : 0;
    } else {
      // Estimate from exit targets if no linked trades
      const triggeredExits = (plan.exitTargets || []).filter(t => t.isTriggered);
      if (triggeredExits.length > 0) {
        const avgExitPrice = triggeredExits.reduce((s, t) => s + t.price, 0) / triggeredExits.length;
        this.totalPnL = (avgExitPrice - this.actualAvgEntry) * plan.quantity;
        this.totalPnLPercent = this.actualAvgEntry > 0
          ? ((avgExitPrice - this.actualAvgEntry) / this.actualAvgEntry) * 100
          : 0;
      }
    }

    // Actual R:R
    const actualRisk = Math.abs(this.actualAvgEntry - plan.stopLoss);
    const actualReward = Math.abs(this.totalPnL) / (plan.quantity || 1);
    this.actualRR = actualRisk > 0 ? actualReward / actualRisk : 0;

    // Fees
    this.totalFees = this.linkedTrades.reduce((s, t) => s + t.fee + t.tax, 0);
  }

  // --- Chart ---

  private renderChart(): void {
    if (!this.replayCanvas?.nativeElement || this.priceData.length === 0) return;
    this.chart?.destroy();

    const plan = this.plan!;
    const labels = this.priceData.map(p =>
      new Date(p.date).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' })
    );
    const datasets: any[] = [];

    // 1. Price line
    datasets.push({
      label: 'Giá đóng cửa',
      data: this.priceData.map(p => p.close),
      type: 'line' as const,
      borderColor: '#6B7280',
      backgroundColor: 'rgba(107, 114, 128, 0.05)',
      fill: true,
      tension: 0.2,
      pointRadius: 0,
      pointHoverRadius: 4,
      borderWidth: 1.5,
      order: 2
    });

    // 2. Plan creation marker
    const createdIdx = this.findDateIndex(plan.createdAt);
    if (createdIdx >= 0 && this.priceData[createdIdx]) {
      datasets.push({
        label: 'Tạo kế hoạch',
        data: this.sparseData(labels.length, [{ idx: createdIdx, val: this.priceData[createdIdx].close }]),
        type: 'line' as const,
        pointStyle: 'star',
        pointRadius: 10,
        pointBackgroundColor: '#3B82F6',
        borderColor: 'transparent',
        backgroundColor: '#3B82F6',
        showLine: false,
        order: 1
      });
    }

    // 3. Entry lots (green triangles up)
    const entryPoints = (plan.lots || [])
      .filter(l => l.status === 'Executed' && l.executedAt && l.actualPrice)
      .map(l => ({ idx: this.findDateIndex(l.executedAt!), val: l.actualPrice! }))
      .filter(p => p.idx >= 0);

    if (entryPoints.length > 0) {
      datasets.push({
        label: 'Vào lệnh',
        data: this.sparseData(labels.length, entryPoints),
        type: 'line' as const,
        pointStyle: 'triangle',
        rotation: 0,
        pointRadius: 10,
        pointBackgroundColor: '#10B981',
        borderColor: 'transparent',
        backgroundColor: '#10B981',
        showLine: false,
        order: 1
      });
    }

    // 4. Exit triggers (red triangles down)
    const exitPoints = (plan.exitTargets || [])
      .filter(t => t.isTriggered && t.triggeredAt)
      .map(t => ({ idx: this.findDateIndex(t.triggeredAt!), val: t.price }))
      .filter(p => p.idx >= 0);

    if (exitPoints.length > 0) {
      datasets.push({
        label: 'Thoát lệnh',
        data: this.sparseData(labels.length, exitPoints),
        type: 'line' as const,
        pointStyle: 'triangle',
        rotation: 180,
        pointRadius: 10,
        pointBackgroundColor: '#EF4444',
        borderColor: 'transparent',
        backgroundColor: '#EF4444',
        showLine: false,
        order: 1
      });
    }

    // 5. Stop-loss horizontal lines
    this.addStopLossDatasets(datasets, labels.length);

    // 6. Target horizontal line
    datasets.push({
      label: 'Mục tiêu',
      data: new Array(labels.length).fill(plan.target),
      type: 'line' as const,
      borderColor: 'rgba(16, 185, 129, 0.5)',
      borderDash: [6, 3],
      borderWidth: 1.5,
      pointRadius: 0,
      fill: false,
      order: 3
    });

    this.chart = new Chart(this.replayCanvas.nativeElement, {
      type: 'line',
      data: { labels, datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { display: false },
          tooltip: {
            filter: (item) => item.parsed.y != null,
            callbacks: {
              label: (ctx) => {
                const label = ctx.dataset.label || '';
                const val = ctx.parsed.y;
                if (val == null) return '';
                return `${label}: ${val.toLocaleString('vi-VN')}đ`;
              }
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
            ticks: { maxTicksLimit: 12 }
          }
        }
      }
    });
  }

  private addStopLossDatasets(datasets: any[], length: number): void {
    const plan = this.plan!;
    const history = plan.stopLossHistory || [];

    interface SLLevel { price: number; fromDate: string; toDate: string }
    const levels: SLLevel[] = [];

    if (history.length > 0) {
      // Initial SL from plan creation to first change
      levels.push({
        price: history[0].oldPrice,
        fromDate: plan.createdAt,
        toDate: history[0].changedAt
      });
      // Each subsequent SL
      history.forEach((h, i) => {
        levels.push({
          price: h.newPrice,
          fromDate: h.changedAt,
          toDate: i < history.length - 1 ? history[i + 1].changedAt : this.priceData[this.priceData.length - 1]?.date || plan.updatedAt
        });
      });
    } else {
      // Single SL for entire range
      levels.push({
        price: plan.stopLoss,
        fromDate: plan.createdAt,
        toDate: this.priceData[this.priceData.length - 1]?.date || plan.updatedAt
      });
    }

    levels.forEach((level, i) => {
      const fromIdx = Math.max(0, this.findDateIndex(level.fromDate));
      const toIdx = Math.min(length - 1, this.findDateIndex(level.toDate));
      const data = new Array(length).fill(null);
      for (let j = fromIdx; j <= toIdx; j++) data[j] = level.price;

      datasets.push({
        label: i === 0 ? 'Stop-Loss' : '',
        data,
        type: 'line' as const,
        borderColor: 'rgba(239, 68, 68, 0.5)',
        borderDash: [6, 3],
        borderWidth: 1.5,
        pointRadius: 0,
        fill: false,
        spanGaps: false,
        order: 3
      });
    });
  }

  // --- Helpers ---

  private sparseData(length: number, points: { idx: number; val: number }[]): (number | null)[] {
    const data = new Array(length).fill(null);
    points.forEach(p => { if (p.idx >= 0 && p.idx < length) data[p.idx] = p.val; });
    return data;
  }

  private findDateIndex(dateStr: string): number {
    const target = dateStr.split('T')[0];
    let idx = this.dateLabels.indexOf(target);
    if (idx >= 0) return idx;

    // Find nearest
    const targetTime = new Date(dateStr).getTime();
    let bestIdx = 0;
    let bestDiff = Infinity;
    for (let i = 0; i < this.dateLabels.length; i++) {
      const diff = Math.abs(new Date(this.dateLabels[i]).getTime() - targetTime);
      if (diff < bestDiff) { bestDiff = diff; bestIdx = i; }
    }
    return bestIdx;
  }

  private formatVndShort(value: number): string {
    if (Math.abs(value) >= 1e6) return (value / 1e6).toFixed(0) + 'M';
    if (Math.abs(value) >= 1e3) return (value / 1e3).toFixed(0) + 'K';
    return value.toString();
  }
}
