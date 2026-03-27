import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PositionsService, ActivePosition } from '../../core/services/positions.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { TradePlanService } from '../../core/services/trade-plan.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';
import { isBuyTrade, getTradeTypeDisplay, getTradeTypeClass } from '../../shared/constants/trade-types';

interface PortfolioGroup {
  portfolioId: string;
  portfolioName: string;
  positions: ActivePosition[];
  totalValue: number;
  totalPnL: number;
  totalPnLPercent: number;
}

@Component({
  selector: 'app-positions',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe, AiChatPanelComponent],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <div>
          <h1 class="text-2xl font-bold text-gray-800">Vị thế đang mở</h1>
          <p class="text-sm text-gray-500 mt-1">Theo dõi các vị thế cổ phiếu đang nắm giữ</p>
        </div>
        <div class="flex items-center gap-3">
          <select [(ngModel)]="sortBy" (ngModelChange)="sortPositions()"
            class="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
            <option value="value">Sắp xếp: Giá trị</option>
            <option value="pnl">Sắp xếp: Lãi/Lỗ</option>
            <option value="pnlPercent">Sắp xếp: % Lãi/Lỗ</option>
            <option value="symbol">Sắp xếp: Mã CK</option>
          </select>
          <select [(ngModel)]="selectedPortfolioId" (ngModelChange)="loadPositions()"
            class="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
            <option value="">Tất cả danh mục</option>
            <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
          </select>
          <button (click)="showAiPanel = true"
            class="px-3 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg text-sm transition-colors flex items-center gap-1">
            🤖 AI Tư vấn
          </button>
          <button (click)="loadPositions()" class="px-3 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm transition-colors">
            Làm mới
          </button>
        </div>
      </div>

      <!-- Loading -->
      <div *ngIf="loading" class="text-center py-12">
        <div class="inline-block w-8 h-8 border-4 border-blue-500 border-t-transparent rounded-full animate-spin"></div>
        <p class="text-gray-500 mt-2">Đang tải vị thế...</p>
      </div>

      <!-- Empty state -->
      <div *ngIf="!loading && positions.length === 0" class="text-center py-12 bg-white rounded-lg shadow">
        <svg class="mx-auto w-16 h-16 text-gray-300 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"></path>
        </svg>
        <p class="text-gray-500 text-lg">Không có vị thế đang mở</p>
        <p class="text-gray-400 text-sm mt-1">Hãy tạo giao dịch mua để bắt đầu</p>
      </div>

      <!-- Summary bar -->
      <div *ngIf="!loading && positions.length > 0" class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-xs text-gray-500">Tổng vị thế</div>
          <div class="text-xl font-bold text-gray-800">{{ positions.length }}</div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-xs text-gray-500">Tổng giá trị</div>
          <div class="text-xl font-bold text-gray-800">{{ getTotalMarketValue() | vndCurrency }}</div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-xs text-gray-500">Tổng lãi/lỗ chưa hiện thực</div>
          <div class="text-xl font-bold" [class.text-green-600]="getTotalUnrealizedPnL() >= 0" [class.text-red-600]="getTotalUnrealizedPnL() < 0">
            {{ getTotalUnrealizedPnL() >= 0 ? '+' : '' }}{{ getTotalUnrealizedPnL() | vndCurrency }}
          </div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-xs text-gray-500">Có kế hoạch</div>
          <div class="text-xl font-bold text-blue-600">{{ getLinkedCount() }}/{{ positions.length }}</div>
        </div>
      </div>

      <!-- Grouped by portfolio -->
      <div *ngIf="!loading && positions.length > 0" class="space-y-6">
        <div *ngFor="let group of groupedPositions">
          <!-- Portfolio group header -->
          <div class="flex items-center justify-between mb-3">
            <div class="flex items-center gap-2">
              <div class="w-1 h-6 bg-blue-500 rounded-full"></div>
              <h2 class="text-lg font-semibold text-gray-800">{{ group.portfolioName }}</h2>
              <span class="text-xs text-gray-400">({{ group.positions.length }} vị thế)</span>
            </div>
            <div class="flex items-center gap-4 text-sm">
              <span class="text-gray-500">Giá trị: <span class="font-semibold text-gray-800">{{ group.totalValue | vndCurrency }}</span></span>
              <span [class.text-green-600]="group.totalPnL >= 0" [class.text-red-600]="group.totalPnL < 0">
                {{ group.totalPnL >= 0 ? '+' : '' }}{{ group.totalPnL | vndCurrency }}
                ({{ group.totalPnLPercent >= 0 ? '+' : '' }}{{ group.totalPnLPercent | number:'1.2-2' }}%)
              </span>
            </div>
          </div>

          <!-- Position cards in group -->
          <div class="space-y-3">
            <div *ngFor="let pos of group.positions" class="bg-white rounded-lg shadow overflow-hidden">
              <!-- Card header -->
              <div class="px-4 py-3 flex items-center justify-between border-b"
                [class.border-l-4]="true"
                [class.border-l-green-500]="pos.unrealizedPnL >= 0"
                [class.border-l-red-500]="pos.unrealizedPnL < 0">
                <div class="flex items-center gap-3">
                  <span class="text-lg font-bold text-gray-800">{{ pos.symbol }}</span>
                  <a [routerLink]="['/symbol-timeline', pos.symbol]"
                    class="text-indigo-600 hover:text-indigo-800 text-xs" title="Xem timeline">📊</a>
                  <span *ngIf="pos.linkedPlan" class="px-2 py-0.5 bg-blue-100 text-blue-700 text-xs rounded-full font-medium">
                    Có KH
                  </span>
                </div>
                <div class="text-right">
                  <div class="text-lg font-bold" [class.text-green-600]="pos.unrealizedPnL >= 0" [class.text-red-600]="pos.unrealizedPnL < 0">
                    {{ pos.unrealizedPnL >= 0 ? '+' : '' }}{{ pos.unrealizedPnL | vndCurrency }}
                  </div>
                  <div class="text-xs" [class.text-green-500]="pos.unrealizedPnLPercent >= 0" [class.text-red-500]="pos.unrealizedPnLPercent < 0">
                    {{ pos.unrealizedPnLPercent >= 0 ? '+' : '' }}{{ pos.unrealizedPnLPercent | number:'1.2-2' }}%
                  </div>
                </div>
              </div>

              <!-- Card body -->
              <div class="px-4 py-3">
                <div class="grid grid-cols-2 md:grid-cols-5 gap-3 text-sm">
                  <div>
                    <div class="text-xs text-gray-500">Số lượng</div>
                    <div class="font-medium">{{ pos.quantity | number:'1.0-0' }} CP</div>
                  </div>
                  <div>
                    <div class="text-xs text-gray-500">Giá TB</div>
                    <div class="font-medium">{{ pos.averageCost | number:'1.0-0' }}</div>
                  </div>
                  <div>
                    <div class="text-xs text-gray-500">Giá hiện tại</div>
                    <div class="font-medium">{{ pos.currentPrice | number:'1.0-0' }}</div>
                  </div>
                  <div>
                    <div class="text-xs text-gray-500">Giá trị</div>
                    <div class="font-medium">{{ pos.marketValue | vndCurrency }}</div>
                  </div>
                  <div>
                    <div class="text-xs text-gray-500">Lãi/lỗ đã hiện thực</div>
                    <div class="font-medium" [class.text-green-600]="pos.realizedPnL >= 0" [class.text-red-600]="pos.realizedPnL < 0">
                      {{ pos.realizedPnL | vndCurrency }}
                    </div>
                  </div>
                </div>

                <!-- SL/TP Distance Bar -->
                <div *ngIf="pos.linkedPlan && pos.linkedPlan.stopLoss && pos.linkedPlan.target" class="mt-3">
                  <div class="flex items-center gap-2 text-xs text-gray-500 mb-1">
                    <span class="text-red-500 font-medium">SL {{ pos.linkedPlan.stopLoss | number:'1.0-0' }}</span>
                    <div class="flex-1 relative h-2 bg-gray-200 rounded-full overflow-hidden">
                      <div class="absolute inset-y-0 left-0 bg-gradient-to-r from-red-400 via-yellow-300 to-green-400 rounded-full"
                        [style.width.%]="100"></div>
                      <div class="absolute inset-y-0 w-1 bg-gray-800 rounded"
                        [style.left.%]="getPricePosition(pos)"></div>
                    </div>
                    <span class="text-green-500 font-medium">TP {{ pos.linkedPlan.target | number:'1.0-0' }}</span>
                  </div>
                  <div class="text-xs text-center"
                    [class.text-red-600]="getDistanceToSL(pos) < 3"
                    [class.text-amber-600]="getDistanceToSL(pos) >= 3 && getDistanceToSL(pos) < 5"
                    [class.text-gray-500]="getDistanceToSL(pos) >= 5">
                    Cách SL {{ getDistanceToSL(pos) | number:'1.1-1' }}% · Cách TP {{ getDistanceToTP(pos) | number:'1.1-1' }}%
                  </div>
                </div>

                <!-- Linked Plan Info -->
                <div *ngIf="pos.linkedPlan" class="mt-3 bg-blue-50 rounded-lg p-3">
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-xs font-semibold text-blue-700">Kế hoạch liên kết</span>
                    <a [routerLink]="['/trade-plan']" [queryParams]="{planId: pos.linkedPlan.id}"
                      class="text-xs text-blue-600 hover:text-blue-800 underline">Xem KH</a>
                  </div>
                  <div class="grid grid-cols-3 gap-2 text-xs">
                    <div>
                      <span class="text-blue-500">SL:</span>
                      <span class="font-medium text-red-600">{{ pos.linkedPlan.stopLoss | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-blue-500">TP:</span>
                      <span class="font-medium text-green-600">{{ pos.linkedPlan.target | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-blue-500">R:R:</span>
                      <span class="font-medium">1:{{ getRR(pos.linkedPlan) | number:'1.1-1' }}</span>
                    </div>
                  </div>
                  <!-- Lot progress -->
                  <div *ngIf="pos.linkedPlan.lots && pos.linkedPlan.lots.length > 0" class="mt-2">
                    <div class="flex items-center gap-2">
                      <div class="flex-1 bg-blue-200 rounded-full h-1.5">
                        <div class="bg-blue-600 h-1.5 rounded-full" [style.width.%]="getLotProgress(pos.linkedPlan)"></div>
                      </div>
                      <span class="text-xs text-blue-600">{{ getExecutedLots(pos.linkedPlan) }}/{{ pos.linkedPlan.lots.length }} lô</span>
                    </div>
                  </div>
                  <!-- Exit targets -->
                  <div *ngIf="pos.linkedPlan.exitTargets && pos.linkedPlan.exitTargets.length > 0" class="mt-2 flex gap-2 flex-wrap">
                    <span *ngFor="let et of pos.linkedPlan.exitTargets" class="px-2 py-0.5 rounded text-xs"
                      [class.bg-green-100]="et.actionType === 'TakeProfit'" [class.text-green-700]="et.actionType === 'TakeProfit'"
                      [class.bg-red-100]="et.actionType === 'CutLoss'" [class.text-red-700]="et.actionType === 'CutLoss'"
                      [class.bg-amber-100]="et.actionType === 'TrailingStop' || et.actionType === 'PartialExit'"
                      [class.text-amber-700]="et.actionType === 'TrailingStop' || et.actionType === 'PartialExit'"
                      [class.line-through]="et.isTriggered" [class.opacity-50]="et.isTriggered">
                      {{ getExitLabel(et.actionType) }}: {{ et.price | number:'1.0-0' }}
                    </span>
                  </div>
                </div>

                <!-- Next action suggestion -->
                <div *ngIf="pos.nextAction" class="mt-3 bg-amber-50 border border-amber-200 rounded-lg p-2 flex items-center gap-2">
                  <svg class="w-4 h-4 text-amber-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                  </svg>
                  <span class="text-sm text-amber-800">{{ pos.nextAction }}</span>
                </div>

                <!-- Recent trades (expandable) -->
                <div *ngIf="pos.recentTrades && pos.recentTrades.length > 0" class="mt-3">
                  <button (click)="toggleTrades(pos.symbol + pos.portfolioId)" class="text-xs text-gray-500 hover:text-gray-700 flex items-center gap-1">
                    <svg class="w-3 h-3 transition-transform" [class.rotate-90]="expandedSymbol === pos.symbol + pos.portfolioId" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"></path>
                    </svg>
                    {{ pos.recentTrades.length }} giao dịch gần đây
                  </button>
                  <div *ngIf="expandedSymbol === pos.symbol + pos.portfolioId" class="mt-2">
                    <table class="w-full text-xs">
                      <thead class="bg-gray-50">
                        <tr>
                          <th class="px-2 py-1 text-left">Loại</th>
                          <th class="px-2 py-1 text-right">SL</th>
                          <th class="px-2 py-1 text-right">Giá</th>
                          <th class="px-2 py-1 text-left">Ngày</th>
                        </tr>
                      </thead>
                      <tbody class="divide-y">
                        <tr *ngFor="let t of pos.recentTrades">
                          <td class="px-2 py-1">
                            <span class="px-1.5 py-0.5 rounded text-xs" [ngClass]="getTradeTypeClass(t.tradeType)">
                              {{ getTradeTypeDisplay(t.tradeType) }}
                            </span>
                          </td>
                          <td class="px-2 py-1 text-right">{{ t.quantity | number:'1.0-0' }}</td>
                          <td class="px-2 py-1 text-right">{{ t.price | number:'1.0-0' }}</td>
                          <td class="px-2 py-1 text-gray-500">{{ t.tradeDate | date:'dd/MM/yy' }}</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>

              <!-- Card actions -->
              <div class="px-4 py-2 bg-gray-50 border-t flex items-center gap-2">
                <a [routerLink]="['/trades/create']"
                  [queryParams]="{symbol: pos.symbol, portfolioId: pos.portfolioId, direction: 'Sell', planId: pos.linkedPlan?.id}"
                  class="px-3 py-1.5 text-xs bg-red-600 hover:bg-red-700 text-white rounded transition-colors">
                  Ghi nhận bán
                </a>
                <a *ngIf="pos.linkedPlan" [routerLink]="['/trade-plan']"
                  class="px-3 py-1.5 text-xs border border-blue-300 hover:bg-blue-50 text-blue-700 rounded transition-colors">
                  Xem KH
                </a>
                <a [routerLink]="['/trades']" [queryParams]="{symbol: pos.symbol}"
                  class="px-3 py-1.5 text-xs border border-gray-300 hover:bg-gray-100 text-gray-600 rounded transition-colors">
                  Lịch sử GD
                </a>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Error -->
      <div *ngIf="error" class="mt-4 bg-red-50 border border-red-200 rounded-lg p-4 text-red-700 text-sm">
        {{ error }}
      </div>
    </div>

    <app-ai-chat-panel
      [(isOpen)]="showAiPanel"
      title="AI Tư vấn Vị thế"
      useCase="position-advisor"
      [contextData]="{ portfolioId: selectedPortfolioId }">
    </app-ai-chat-panel>
  `
})
export class PositionsComponent implements OnInit {
  positions: ActivePosition[] = [];
  groupedPositions: PortfolioGroup[] = [];
  showAiPanel = false;
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  sortBy = 'value';
  loading = false;
  error = '';
  expandedSymbol: string | null = null;

  constructor(
    private positionsService: PositionsService,
    private portfolioService: PortfolioService,
    private tradePlanService: TradePlanService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.portfolioService.getAll().subscribe({ next: d => this.portfolios = d });
    this.loadPositions();
  }

  loadPositions(): void {
    this.loading = true;
    this.error = '';
    this.positionsService.getAll(this.selectedPortfolioId || undefined).subscribe({
      next: (data) => {
        this.positions = data;
        this.buildGroups();
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Không thể tải vị thế. Vui lòng thử lại.';
        this.loading = false;
      }
    });
  }

  getTotalMarketValue(): number {
    return this.positions.reduce((s, p) => s + p.marketValue, 0);
  }

  getTotalUnrealizedPnL(): number {
    return this.positions.reduce((s, p) => s + p.unrealizedPnL, 0);
  }

  getLinkedCount(): number {
    return this.positions.filter(p => p.linkedPlan).length;
  }

  getRR(plan: any): number {
    const risk = Math.abs(plan.entryPrice - plan.stopLoss);
    return risk > 0 ? Math.abs(plan.target - plan.entryPrice) / risk : 0;
  }

  getLotProgress(plan: any): number {
    const lots = plan.lots || [];
    if (lots.length === 0) return 0;
    const executed = lots.filter((l: any) => l.status === 'Executed').length;
    return Math.round((executed / lots.length) * 100);
  }

  getExecutedLots(plan: any): number {
    return (plan.lots || []).filter((l: any) => l.status === 'Executed').length;
  }

  getExitLabel(actionType: string): string {
    const labels: Record<string, string> = {
      TakeProfit: 'TP', CutLoss: 'CL', TrailingStop: 'Trailing', PartialExit: 'Bán 1 phần'
    };
    return labels[actionType] || actionType;
  }

  private buildGroups(): void {
    const map = new Map<string, PortfolioGroup>();
    for (const pos of this.positions) {
      let group = map.get(pos.portfolioId);
      if (!group) {
        group = { portfolioId: pos.portfolioId, portfolioName: pos.portfolioName, positions: [], totalValue: 0, totalPnL: 0, totalPnLPercent: 0 };
        map.set(pos.portfolioId, group);
      }
      group.positions.push(pos);
      group.totalValue += pos.marketValue;
      group.totalPnL += pos.unrealizedPnL;
    }
    for (const group of map.values()) {
      const totalCost = group.positions.reduce((s, p) => s + p.averageCost * p.quantity, 0);
      group.totalPnLPercent = totalCost > 0 ? (group.totalPnL / totalCost) * 100 : 0;
    }
    this.groupedPositions = Array.from(map.values()).sort((a, b) => b.totalValue - a.totalValue);
  }

  toggleTrades(symbol: string): void {
    this.expandedSymbol = this.expandedSymbol === symbol ? null : symbol;
  }

  // SL/TP distance helpers
  getPricePosition(pos: ActivePosition): number {
    const plan = pos.linkedPlan;
    if (!plan || !plan.stopLoss || !plan.target) return 50;
    const range = plan.target - plan.stopLoss;
    if (range <= 0) return 50;
    return Math.max(0, Math.min(100, ((pos.currentPrice - plan.stopLoss) / range) * 100));
  }

  getDistanceToSL(pos: ActivePosition): number {
    if (!pos.linkedPlan?.stopLoss || pos.currentPrice <= 0) return 0;
    return Math.abs((pos.currentPrice - pos.linkedPlan.stopLoss) / pos.currentPrice) * 100;
  }

  getDistanceToTP(pos: ActivePosition): number {
    if (!pos.linkedPlan?.target || pos.currentPrice <= 0) return 0;
    return Math.abs((pos.linkedPlan.target - pos.currentPrice) / pos.currentPrice) * 100;
  }

  sortPositions(): void {
    for (const group of this.groupedPositions) {
      group.positions.sort((a, b) => {
        switch (this.sortBy) {
          case 'pnl': return b.unrealizedPnL - a.unrealizedPnL;
          case 'pnlPercent': return b.unrealizedPnLPercent - a.unrealizedPnLPercent;
          case 'symbol': return a.symbol.localeCompare(b.symbol);
          default: return b.marketValue - a.marketValue;
        }
      });
    }
  }

  isBuyTrade = isBuyTrade;
  getTradeTypeDisplay = getTradeTypeDisplay;
  getTradeTypeClass = getTradeTypeClass;
}
