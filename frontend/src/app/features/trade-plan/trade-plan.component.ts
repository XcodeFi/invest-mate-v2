import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, takeUntil, catchError } from 'rxjs';
import { StrategyService, Strategy } from '../../core/services/strategy.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { RiskService, RiskProfile, PortfolioRiskSummary, PositionSizingRequest, PositionSizingResult, SizingModelResult } from '../../core/services/risk.service';
import { MarketDataService, StockPrice, TechnicalAnalysis } from '../../core/services/market-data.service';
import { TradePlanTemplateService, TradePlanTemplate } from '../../core/services/trade-plan-template.service';
import { TradePlanService, TradePlan as TradePlanDto, ScenarioNodeDto, ScenarioPreset, TrailingStopConfigDto, ScenarioHistoryDto, ScenarioSuggestionDto, SuggestedNodeDto, ScenarioAdvisoryDto, CampaignReviewDto } from '../../core/services/trade-plan.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { isBuyTrade, getTradeTypeDisplay, getTradeTypeClass } from '../../shared/constants/trade-types';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';

interface ChecklistItem {
  label: string;
  category: string;
  checked: boolean;
  critical: boolean;
  hint: string;
  weight: number; // 1=nice-to-have, 2=important, 3=critical
}

interface PlanLotForm {
  lotNumber: number;
  plannedPrice: number;
  plannedQuantity: number;
  allocationPercent: number;
  label: string;
}

interface ExitTargetForm {
  level: number;
  actionType: string;
  price: number;
  percentOfPosition: number;
  label: string;
}

interface ScenarioNodeForm {
  nodeId: string;
  parentId: string | null;
  order: number;
  label: string;
  conditionType: string;
  conditionValue: number;
  conditionNote: string;
  actionType: string;
  actionValue: number;
  trailingStopConfig: { method: string; trailValue: number; activationPrice: number; stepSize: number };
  status: string;
}

interface TradePlanForm {
  symbol: string;
  direction: string;
  entryPrice: number;
  stopLoss: number;
  target: number;
  quantity: number;
  strategyId: string;
  portfolioId: string;
  reason: string;
  marketCondition: string;
  timeHorizon: string;
  confidenceLevel: number;
  checklist: ChecklistItem[];
  notes: string;
  entryMode: string;
  lots: PlanLotForm[];
  exitTargets: ExitTargetForm[];
}

@Component({
  selector: 'app-trade-plan',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe, NumMaskDirective, UppercaseDirective, AiChatPanelComponent],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-2xl font-bold text-gray-800">Kế hoạch giao dịch (Trade Plan)</h1>
        <button *ngIf="selectedPlanId" (click)="resetForm()"
          class="px-4 py-2 border border-gray-300 hover:bg-gray-50 text-gray-700 rounded-lg text-sm font-medium transition-colors">
          + Tạo mới
        </button>
      </div>

      <!-- Saved Plans Panel -->
      <div class="bg-white rounded-lg shadow mb-6">
        <div class="px-4 py-3 border-b flex items-center justify-between">
          <h2 class="text-sm font-semibold text-gray-700">Kế hoạch đã lưu ({{ filteredSavedPlans.length }})</h2>
          <div class="flex gap-1">
            <button *ngFor="let tab of planFilterTabs" (click)="planFilterTab = tab.key; filterSavedPlans()"
              class="px-3 py-1 rounded-full text-xs font-medium transition-colors"
              [class.bg-blue-100]="planFilterTab === tab.key" [class.text-blue-700]="planFilterTab === tab.key"
              [class.bg-gray-100]="planFilterTab !== tab.key" [class.text-gray-600]="planFilterTab !== tab.key">
              {{ tab.label }}
            </button>
          </div>
        </div>
        <div *ngIf="savedPlansLoading" class="px-4 py-6 text-center text-gray-400 text-sm">Đang tải...</div>
        <div *ngIf="!savedPlansLoading && filteredSavedPlans.length === 0" class="px-4 py-6 text-center text-gray-400 text-sm">
          Chưa có kế hoạch nào{{ planFilterTab !== 'all' ? ' ở trạng thái này' : '' }}
        </div>
        <div *ngIf="!savedPlansLoading && filteredSavedPlans.length > 0">
          <!-- Desktop table -->
          <div class="hidden md:block overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="bg-gray-50 text-xs text-gray-500 uppercase">
                <tr>
                  <th class="px-4 py-2 text-left">Mã CK</th>
                  <th class="px-4 py-2 text-left">Hướng</th>
                  <th class="px-4 py-2 text-right">Giá vào</th>
                  <th class="px-4 py-2 text-right">SL / TP</th>
                  <th class="px-4 py-2 text-right">R:R</th>
                  <th class="px-4 py-2 text-center">Trạng thái</th>
                  <th class="px-4 py-2 text-left">Ngày tạo</th>
                  <th class="px-4 py-2 text-center">Thao tác</th>
                </tr>
              </thead>
              <tbody class="divide-y">
                <tr *ngFor="let sp of filteredSavedPlans" class="hover:bg-gray-50 cursor-pointer"
                  [class.bg-blue-50]="sp.id === selectedPlanId" (click)="loadPlan(sp)">
                  <td class="px-4 py-2 font-bold">{{ sp.symbol }}</td>
                  <td class="px-4 py-2">
                    <span class="px-2 py-0.5 rounded-full text-xs font-medium" [ngClass]="getTradeTypeClass(sp.direction)">
                      {{ getTradeTypeDisplay(sp.direction) }}
                    </span>
                  </td>
                  <td class="px-4 py-2 text-right">{{ sp.entryPrice | number:'1.0-0' }}</td>
                  <td class="px-4 py-2 text-right">
                    <span class="text-red-500">{{ sp.stopLoss | number:'1.0-0' }}</span> /
                    <span class="text-green-500">{{ sp.target | number:'1.0-0' }}</span>
                  </td>
                  <td class="px-4 py-2 text-right font-medium"
                    [class.text-green-600]="getRR(sp) >= 2" [class.text-red-600]="getRR(sp) < 1">
                    1:{{ getRR(sp) | number:'1.1-1' }}
                  </td>
                  <td class="px-4 py-2 text-center">
                    <span class="px-2 py-0.5 rounded-full text-xs font-medium" [ngClass]="getStatusClass(sp.status)">
                      {{ getStatusLabel(sp.status) }}
                    </span>
                    <div *ngIf="sp.lots && sp.lots.length > 0" class="mt-1">
                      <div class="w-full bg-gray-200 rounded-full h-1.5">
                        <div class="bg-amber-500 h-1.5 rounded-full" [style.width.%]="getLotProgress(sp)"></div>
                      </div>
                      <span class="text-xs text-amber-600">{{ getExecutedLotCount(sp) }}/{{ sp.lots.length }} lô</span>
                    </div>
                  </td>
                  <td class="px-4 py-2 text-gray-500">{{ sp.createdAt | date:'dd/MM/yy HH:mm' }}</td>
                  <td class="px-4 py-2 text-center" (click)="$event.stopPropagation()">
                    <div class="flex items-center justify-center gap-1">
                      <button (click)="aiTradePlanId = sp.id; showAiPanel = true" title="AI Tư vấn"
                        class="bg-purple-600 hover:bg-purple-700 text-white text-sm font-medium rounded-lg px-3 py-1.5 transition-colors flex items-center gap-1">
                        🤖 AI Tư vấn
                      </button>
                      <button *ngIf="sp.status === 'Draft' || sp.status === 'Ready'"
                        (click)="deletePlan(sp)" title="Xoá"
                        class="p-1 text-red-400 hover:text-red-600 hover:bg-red-50 rounded transition-colors">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"></path>
                        </svg>
                      </button>
                      <button *ngIf="sp.status === 'Draft'" (click)="markReady(sp)" title="Sẵn sàng"
                        class="p-1 text-emerald-400 hover:text-emerald-600 hover:bg-emerald-50 rounded transition-colors">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                        </svg>
                      </button>
                      <a *ngIf="sp.status === 'Executed' || sp.status === 'Reviewed'"
                        [routerLink]="['/trade-replay', sp.id]" title="Xem replay"
                        class="p-1 text-indigo-400 hover:text-indigo-600 hover:bg-indigo-50 rounded transition-colors">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 4v16M17 4v16M3 8h4m10 0h4M3 12h18M3 16h4m10 0h4M4 20h16a1 1 0 001-1V5a1 1 0 00-1-1H4a1 1 0 00-1 1v14a1 1 0 001 1z"></path>
                        </svg>
                      </a>
                      <button *ngIf="sp.status === 'Executed'" (click)="openCampaignReview(sp)" title="Đóng chiến dịch"
                        class="px-2 py-1 text-xs bg-amber-500 hover:bg-amber-600 text-white rounded-lg transition-colors font-medium">
                        Đóng chiến dịch
                      </button>
                      <button *ngIf="sp.status !== 'Cancelled' && sp.status !== 'Executed' && sp.status !== 'Reviewed'"
                        (click)="cancelPlan(sp)" title="Huỷ"
                        class="p-1 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded transition-colors">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                        </svg>
                      </button>
                    </div>
                    <!-- Review data badge for Reviewed plans -->
                    <div *ngIf="sp.status === 'Reviewed' && sp.reviewData" class="mt-1 text-xs">
                      <span class="font-medium" [class.text-green-600]="sp.reviewData.pnLAmount >= 0" [class.text-red-600]="sp.reviewData.pnLAmount < 0">
                        P&L: {{ sp.reviewData.pnLAmount | vndCurrency }} ({{ sp.reviewData.pnLPercent | number:'1.1-1' }}%)
                      </span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <!-- Mobile cards -->
          <div class="md:hidden divide-y divide-gray-200">
            <div *ngFor="let sp of filteredSavedPlans" class="p-3 space-y-2 cursor-pointer"
              [class.bg-blue-50]="sp.id === selectedPlanId" (click)="loadPlan(sp)">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="font-bold text-gray-900">{{ sp.symbol }}</span>
                  <span class="px-2 py-0.5 rounded-full text-xs font-medium" [ngClass]="getTradeTypeClass(sp.direction)">
                    {{ getTradeTypeDisplay(sp.direction) }}
                  </span>
                </div>
                <span class="px-2 py-0.5 rounded-full text-xs font-medium" [ngClass]="getStatusClass(sp.status)">
                  {{ getStatusLabel(sp.status) }}
                </span>
              </div>
              <div class="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
                <div><span class="text-gray-500">Giá vào:</span> <span class="font-medium">{{ sp.entryPrice | number:'1.0-0' }}</span></div>
                <div>
                  <span class="font-medium" [class.text-green-600]="getRR(sp) >= 2" [class.text-red-600]="getRR(sp) < 1">
                    R:R 1:{{ getRR(sp) | number:'1.1-1' }}
                  </span>
                </div>
                <div><span class="text-red-500">SL:</span> <span class="font-medium">{{ sp.stopLoss | number:'1.0-0' }}</span></div>
                <div><span class="text-green-500">TP:</span> <span class="font-medium">{{ sp.target | number:'1.0-0' }}</span></div>
              </div>
              <div *ngIf="sp.lots && sp.lots.length > 0" class="mt-1">
                <div class="w-full bg-gray-200 rounded-full h-1.5">
                  <div class="bg-amber-500 h-1.5 rounded-full" [style.width.%]="getLotProgress(sp)"></div>
                </div>
                <span class="text-xs text-amber-600">{{ getExecutedLotCount(sp) }}/{{ sp.lots.length }} lô</span>
              </div>
              <div class="flex items-center justify-between text-xs text-gray-500 pt-1 border-t border-gray-100" (click)="$event.stopPropagation()">
                <span>{{ sp.createdAt | date:'dd/MM/yy HH:mm' }}</span>
                <div class="flex items-center gap-1">
                  <button *ngIf="sp.status === 'Draft' || sp.status === 'Ready'"
                    (click)="deletePlan(sp)" title="Xoá"
                    class="p-1 text-red-400 hover:text-red-600 rounded">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"></path>
                    </svg>
                  </button>
                  <a *ngIf="sp.status === 'Executed' || sp.status === 'Reviewed'"
                    [routerLink]="['/trade-replay', sp.id]" title="Xem replay"
                    class="p-1 text-indigo-400 hover:text-indigo-600 rounded">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 4v16M17 4v16M3 8h4m10 0h4M3 12h18M3 16h4m10 0h4M4 20h16a1 1 0 001-1V5a1 1 0 00-1-1H4a1 1 0 00-1 1v14a1 1 0 001 1z"></path>
                    </svg>
                  </a>
                  <button *ngIf="sp.status === 'Draft'" (click)="markReady(sp)" title="Sẵn sàng"
                    class="p-1 text-emerald-400 hover:text-emerald-600 rounded">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                    </svg>
                  </button>
                  <button *ngIf="sp.status === 'Executed'" (click)="openCampaignReview(sp)" title="Đóng chiến dịch"
                    class="px-2 py-1 text-xs bg-amber-500 hover:bg-amber-600 text-white rounded-lg transition-colors font-medium">
                    Đóng
                  </button>
                  <button *ngIf="sp.status !== 'Cancelled' && sp.status !== 'Executed' && sp.status !== 'Reviewed'"
                    (click)="cancelPlan(sp)" title="Huỷ"
                    class="p-1 text-gray-400 hover:text-gray-600 rounded">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                    </svg>
                  </button>
                </div>
              </div>
              <!-- Review data for Reviewed plans (mobile) -->
              <div *ngIf="sp.status === 'Reviewed' && sp.reviewData" class="text-xs px-1">
                <span class="font-medium" [class.text-green-600]="sp.reviewData.pnLAmount >= 0" [class.text-red-600]="sp.reviewData.pnLAmount < 0">
                  P&L: {{ sp.reviewData.pnLAmount | vndCurrency }} ({{ sp.reviewData.pnLPercent | number:'1.1-1' }}%)
                </span>
                <span class="text-gray-500 ml-1">{{ sp.reviewData.pnLPerDay | vndCurrency }}/ngày</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Editing indicator -->
      <div *ngIf="selectedPlanId" class="mb-4 bg-blue-50 border border-blue-200 rounded-lg px-4 py-2 flex items-center justify-between">
        <span class="text-sm text-blue-700">Đang chỉnh sửa kế hoạch: <strong>{{ plan.symbol }}</strong> ({{ getStatusLabel(selectedPlanStatus) }})</span>
        <button (click)="resetForm()" class="text-xs text-blue-600 hover:text-blue-800 underline">Huỷ chỉnh sửa</button>
      </div>

      <!-- Template Panel -->
      <div class="bg-white rounded-lg shadow p-4 mb-6">
        <div class="flex flex-wrap items-center gap-4">
          <!-- Load from template -->
          <div class="flex items-center gap-2 flex-1 min-w-0">
            <span class="text-sm font-medium text-gray-600 whitespace-nowrap">Tải template:</span>
            <select [(ngModel)]="selectedTemplateId"
              class="flex-1 min-w-0 px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn template --</option>
              <option *ngFor="let t of templates" [value]="t.id">
                {{ t.name }}{{ t.symbol ? ' (' + t.symbol + ')' : '' }}
              </option>
            </select>
            <button (click)="applyTemplate()" [disabled]="!selectedTemplateId"
              class="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 text-white rounded-lg text-sm font-medium transition-colors whitespace-nowrap">
              Tải
            </button>
            <button *ngIf="selectedTemplateId" (click)="deleteTemplate(selectedTemplateId)"
              class="px-3 py-2 border border-red-300 hover:bg-red-50 text-red-600 rounded-lg text-sm transition-colors">
              Xoá
            </button>
          </div>

          <!-- Save as template -->
          <div class="flex items-center gap-2">
            <div *ngIf="!showSaveTemplate">
              <button (click)="showSaveTemplate = true"
                class="px-4 py-2 border border-gray-300 hover:bg-gray-50 text-gray-700 rounded-lg text-sm font-medium transition-colors whitespace-nowrap">
                + Lưu làm template
              </button>
            </div>
            <div *ngIf="showSaveTemplate" class="flex items-center gap-2">
              <input [(ngModel)]="newTemplateName" type="text" placeholder="Tên template..."
                class="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 w-48">
              <button (click)="saveAsTemplate()" [disabled]="!newTemplateName.trim() || savingTemplate"
                class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-gray-300 text-white rounded-lg text-sm font-medium transition-colors whitespace-nowrap">
                {{ savingTemplate ? 'Đang lưu...' : 'Lưu' }}
              </button>
              <button (click)="showSaveTemplate = false; newTemplateName = ''"
                class="px-3 py-2 text-gray-500 hover:text-gray-700 text-sm">✕</button>
            </div>
          </div>

          <div *ngIf="templates.length === 0" class="text-xs text-gray-400 italic">
            Chưa có template nào
          </div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Trade Setup -->
        <div class="lg:col-span-2 space-y-6">
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Thiết lập giao dịch</h2>
            <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
              <div class="relative">
                <label class="block text-sm font-medium text-gray-700 mb-1">Mã cổ phiếu *</label>
                <input [(ngModel)]="plan.symbol" type="text" appUppercase
                  (ngModelChange)="onSymbolInput()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  placeholder="VD: VNM">
                <div *ngIf="stockLoading" class="absolute right-2 top-8">
                  <div class="w-4 h-4 border-2 border-blue-500 border-t-transparent rounded-full animate-spin"></div>
                </div>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Hướng *</label>
                <select [(ngModel)]="plan.direction"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
                  <option value="Buy">Mua (Long)</option>
                  <option value="Sell">Bán (Short)</option>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Danh mục</label>
                <select [(ngModel)]="plan.portfolioId" (ngModelChange)="onPortfolioChange()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
                  <option value="">-- Chọn --</option>
                  <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }} ({{ p.initialCapital | vndCurrency }})</option>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">
                  Giá vào lệnh *
                  <span class="text-xs text-gray-400 font-normal ml-1">(giá bạn dự kiến mua)</span>
                </label>
                <input [(ngModel)]="plan.entryPrice" type="text" inputmode="numeric" appNumMask [emptyWhenZero]="true" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  placeholder="Nhập giá dự kiến">
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">
                  Stop-Loss <sup class="text-red-400 font-bold cursor-default" title="Giải thích 1">1</sup> *
                </label>
                <input [(ngModel)]="plan.stopLoss" type="text" inputmode="numeric" appNumMask [emptyWhenZero]="true" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  [placeholder]="suggestedSlHint || 'Mức cắt lỗ'">
                <p *ngIf="slAutoFilled" class="text-xs text-blue-500 mt-0.5">Tự điền từ chiến lược</p>
                <!-- SL Method Selector (P4) -->
                @if (slMethods.length > 1) {
                  <div class="mt-2 space-y-1">
                    <div class="flex items-center gap-2 flex-wrap">
                      @for (m of slMethods; track m.value) {
                        <button (click)="applySlMethod(m.value)"
                          class="text-xs px-2 py-1 rounded-full border transition-colors hover:bg-gray-100"
                          [class.bg-blue-100]="slMethod === m.value" [class.border-blue-400]="slMethod === m.value"
                          [class.text-blue-700]="slMethod === m.value"
                          [class.bg-gray-50]="slMethod !== m.value" [class.border-gray-200]="slMethod !== m.value"
                          [class.text-gray-600]="slMethod !== m.value">
                          {{ m.label }}
                          @if (m.price) {
                            <span class="font-mono ml-1">{{ m.price | number:'1.0-0' }}</span>
                          }
                        </button>
                      }
                    </div>
                    @if (slMethod === 'atr') {
                      <div class="flex items-center gap-2 text-xs text-gray-500">
                        <span>Hệ số k:</span>
                        @for (k of [1.5, 2, 3]; track k) {
                          <button (click)="slAtrMultiplier = k; onSlAtrMultiplierChange()"
                            class="px-1.5 py-0.5 rounded border text-xs"
                            [class.bg-blue-100]="slAtrMultiplier === k"
                            [class.border-blue-400]="slAtrMultiplier === k"
                            [class.bg-gray-50]="slAtrMultiplier !== k">
                            {{ k }}×
                          </button>
                        }
                        <span class="text-gray-400">
                          {{ slAtrMultiplier === 1.5 ? '(ngắn hạn)' : slAtrMultiplier === 2 ? '(trung hạn)' : '(dài hạn)' }}
                        </span>
                      </div>
                    }
                    @if (slMethod !== 'manual') {
                      <div class="text-xs text-gray-400">
                        {{ slMethods.find(m => m.value === slMethod)?.note }}
                      </div>
                    }
                  </div>
                }
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">
                  Take-Profit <sup class="text-emerald-500 font-bold cursor-default" title="Giải thích 2">2</sup> *
                </label>
                <input [(ngModel)]="plan.target" type="text" inputmode="numeric" appNumMask [emptyWhenZero]="true" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  [placeholder]="suggestedTpHint || 'Mức chốt lời'">
                <p *ngIf="tpAutoFilled" class="text-xs text-blue-500 mt-0.5">Tự điền từ chiến lược</p>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">
                  Số lượng (CP) <sup class="text-violet-400 font-bold cursor-default" title="Giải thích 3">3</sup>
                </label>
                <input [(ngModel)]="plan.quantity" type="text" inputmode="numeric" appNumMask [emptyWhenZero]="true" step="100" (ngModelChange)="onQuantityManualChange()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  [placeholder]="optimalShares > 0 ? 'Tự động: ' + optimalShares : '0'">
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Chế độ vào lệnh</label>
                <select [(ngModel)]="plan.entryMode" (ngModelChange)="onEntryModeChange()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
                  <option value="Single">Một lần</option>
                  <option value="ScalingIn">Chia lô</option>
                  <option value="DCA">DCA</option>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Chiến lược</label>
                <select [(ngModel)]="plan.strategyId" (ngModelChange)="onStrategyChange()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
                  <option value="">-- Chọn --</option>
                  <option *ngFor="let s of strategies" [value]="s.id">{{ s.name }}</option>
                </select>
                <p *ngIf="selectedStrategy?.suggestedSlPercent" class="text-xs text-blue-500 mt-0.5">
                  Gợi ý SL: -{{ selectedStrategy!.suggestedSlPercent }}%
                  <span *ngIf="selectedStrategy?.suggestedRrRatio">, R:R {{ selectedStrategy!.suggestedRrRatio }}:1</span>
                  — tự điền khi nhập giá vào
                </p>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Điều kiện thị trường</label>
                <select [(ngModel)]="plan.marketCondition"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
                  <option value="Trending">Xu hướng</option>
                  <option value="Ranging">Sideway</option>
                  <option value="Volatile">Biến động</option>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Khung thời gian</label>
                <select [(ngModel)]="plan.timeHorizon"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
                  <option value="ShortTerm">Ngắn hạn</option>
                  <option value="MediumTerm">Trung hạn</option>
                  <option value="LongTerm">Dài hạn</option>
                </select>
              </div>
            </div>

            <!-- Lot Editor (Chia lô) -->
            <div *ngIf="plan.entryMode === 'ScalingIn'" class="mt-4 bg-amber-50 border border-amber-200 rounded-lg p-4">
              <div class="flex items-center justify-between mb-3">
                <h3 class="text-sm font-semibold text-amber-800">
                  Chia lô mua ({{ plan.lots.length }} lô — Tổng: {{ getLotsTotalQty() | number:'1.0-0' }} CP)
                </h3>
                <div class="flex gap-1">
                  <button (click)="applyLotPreset('40-30-30')" class="px-2 py-0.5 text-xs bg-amber-200 hover:bg-amber-300 rounded">40/30/30</button>
                  <button (click)="applyLotPreset('50-50')" class="px-2 py-0.5 text-xs bg-amber-200 hover:bg-amber-300 rounded">50/50</button>
                  <button (click)="applyLotPreset('equal')" class="px-2 py-0.5 text-xs bg-amber-200 hover:bg-amber-300 rounded">Đều</button>
                  <button (click)="addLot()" class="px-2 py-0.5 text-xs bg-amber-600 text-white hover:bg-amber-700 rounded">+ Thêm lô</button>
                </div>
              </div>
              <table *ngIf="plan.lots.length > 0" class="w-full text-sm">
                <thead class="text-xs text-amber-700">
                  <tr>
                    <th class="pb-1 text-left">Lô</th>
                    <th class="pb-1 text-right">Giá mua</th>
                    <th class="pb-1 text-right">Số lượng</th>
                    <th class="pb-1 text-right">% vốn</th>
                    <th class="pb-1 text-center w-8"></th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let lot of plan.lots; let i = index" class="border-t border-amber-200">
                    <td class="py-1 font-medium">Lô {{ lot.lotNumber }}</td>
                    <td class="py-1">
                      <input [(ngModel)]="lot.plannedPrice" type="text" inputmode="numeric" appNumMask
                        (ngModelChange)="recalculateLots()" class="w-full px-2 py-1 border border-amber-300 rounded text-right text-sm">
                    </td>
                    <td class="py-1">
                      <input [(ngModel)]="lot.plannedQuantity" type="text" inputmode="numeric" appNumMask
                        (ngModelChange)="recalculateLots()" class="w-full px-2 py-1 border border-amber-300 rounded text-right text-sm">
                    </td>
                    <td class="py-1 text-right text-amber-700">{{ lot.allocationPercent | number:'1.0-0' }}%</td>
                    <td class="py-1 text-center">
                      <button (click)="removeLot(i)" class="text-red-400 hover:text-red-600">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                        </svg>
                      </button>
                    </td>
                  </tr>
                </tbody>
              </table>
              <div *ngIf="plan.lots.length > 1" class="mt-2 text-xs text-amber-700">
                Giá TB dự kiến: {{ getLotsWeightedAvg() | number:'1.0-0' }}đ
              </div>
            </div>

            <!-- DCA Editor -->
            <div *ngIf="plan.entryMode === 'DCA'" class="mt-4 bg-teal-50 border border-teal-200 rounded-lg p-4">
              <h3 class="text-sm font-semibold text-teal-800 mb-3">
                DCA — Trung bình giá (Dollar Cost Averaging)
              </h3>
              <p class="text-xs text-teal-600 mb-3">Mua đều đặn theo lịch, không cần dự đoán giá — phù hợp tích luỹ dài hạn</p>
              <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3">
                <div>
                  <label class="block text-xs font-medium text-teal-700 mb-1">Số tiền mỗi lần</label>
                  <input [(ngModel)]="dcaForm.amountPerPeriod" type="text" inputmode="numeric" appNumMask [emptyWhenZero]="true"
                    (ngModelChange)="buildDcaSchedule()"
                    class="w-full px-2 py-1.5 border border-teal-300 rounded text-sm"
                    placeholder="VD: 5.000.000">
                </div>
                <div>
                  <label class="block text-xs font-medium text-teal-700 mb-1">Tần suất</label>
                  <select [(ngModel)]="dcaForm.frequency" (ngModelChange)="buildDcaSchedule()"
                    class="w-full px-2 py-1.5 border border-teal-300 rounded text-sm">
                    <option value="weekly">Hàng tuần</option>
                    <option value="biweekly">2 tuần / lần</option>
                    <option value="monthly">Hàng tháng</option>
                  </select>
                </div>
                <div>
                  <label class="block text-xs font-medium text-teal-700 mb-1">Số kỳ</label>
                  <input [(ngModel)]="dcaForm.numberOfPeriods" type="number" min="1" max="52"
                    (ngModelChange)="buildDcaSchedule()"
                    class="w-full px-2 py-1.5 border border-teal-300 rounded text-sm"
                    placeholder="VD: 12">
                </div>
                <div>
                  <label class="block text-xs font-medium text-teal-700 mb-1">Ngày bắt đầu</label>
                  <input [(ngModel)]="dcaForm.startDate" type="date"
                    (ngModelChange)="buildDcaSchedule()"
                    class="w-full px-2 py-1.5 border border-teal-300 rounded text-sm">
                </div>
              </div>
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-3">
                <div>
                  <label class="block text-xs font-medium text-teal-700 mb-1">Giá sàn (không mua nếu cao hơn)</label>
                  <input [(ngModel)]="dcaForm.maxPrice" type="text" inputmode="numeric" appNumMask [emptyWhenZero]="true"
                    class="w-full px-2 py-1.5 border border-teal-300 rounded text-sm"
                    placeholder="Để trống = không giới hạn">
                </div>
                <div>
                  <label class="block text-xs font-medium text-teal-700 mb-1">Giá trần (mua thêm nếu dưới)</label>
                  <input [(ngModel)]="dcaForm.minPrice" type="text" inputmode="numeric" appNumMask [emptyWhenZero]="true"
                    class="w-full px-2 py-1.5 border border-teal-300 rounded text-sm"
                    placeholder="Để trống = không giới hạn">
                </div>
              </div>
              <!-- DCA Summary -->
              <div *ngIf="dcaForm.amountPerPeriod > 0 && dcaForm.numberOfPeriods > 0" class="mt-3 bg-teal-100 rounded p-3">
                <div class="grid grid-cols-3 gap-4 text-center text-sm">
                  <div>
                    <div class="text-xs text-teal-600">Tổng vốn DCA</div>
                    <div class="font-bold text-teal-800">{{ dcaForm.amountPerPeriod * dcaForm.numberOfPeriods | vndCurrency }}</div>
                  </div>
                  <div>
                    <div class="text-xs text-teal-600">Thời gian</div>
                    <div class="font-bold text-teal-800">{{ getDcaDuration() }}</div>
                  </div>
                  <div>
                    <div class="text-xs text-teal-600">Lịch mua</div>
                    <div class="font-bold text-teal-800">{{ getDcaFrequencyLabel() }}</div>
                  </div>
                </div>
              </div>
              <!-- DCA Schedule Table -->
              <div *ngIf="dcaSchedule.length > 0" class="mt-3">
                <div class="text-xs font-medium text-teal-700 mb-1">Lịch mua dự kiến ({{ dcaSchedule.length }} kỳ)</div>
                <div class="max-h-40 overflow-y-auto">
                  <table class="w-full text-xs">
                    <thead class="text-teal-600 bg-teal-100 sticky top-0">
                      <tr>
                        <th class="px-2 py-1 text-left">Kỳ</th>
                        <th class="px-2 py-1 text-left">Ngày mua</th>
                        <th class="px-2 py-1 text-right">Số tiền</th>
                        <th class="px-2 py-1 text-right">Tích luỹ</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-teal-100">
                      <tr *ngFor="let row of dcaSchedule">
                        <td class="px-2 py-1">{{ row.period }}</td>
                        <td class="px-2 py-1">{{ row.date | date:'dd/MM/yyyy' }}</td>
                        <td class="px-2 py-1 text-right">{{ row.amount | vndCurrency }}</td>
                        <td class="px-2 py-1 text-right font-medium">{{ row.cumulative | vndCurrency }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            <!-- Exit Strategy -->
            <div class="mt-4 bg-violet-50 border border-violet-200 rounded-lg p-4">
              <div class="flex items-center justify-between mb-3">
                <h3 class="text-sm font-semibold text-violet-800">Chiến lược thoát lệnh</h3>
                <div class="flex items-center gap-1">
                  <button (click)="exitStrategyMode = 'Simple'"
                    class="px-3 py-1 rounded-l-lg text-xs font-medium transition-colors"
                    [class.bg-violet-600]="exitStrategyMode === 'Simple'" [class.text-white]="exitStrategyMode === 'Simple'"
                    [class.bg-violet-100]="exitStrategyMode !== 'Simple'" [class.text-violet-600]="exitStrategyMode !== 'Simple'">
                    Cơ bản
                  </button>
                  <button (click)="exitStrategyMode = 'Advanced'; loadScenarioPresets()"
                    class="px-3 py-1 rounded-r-lg text-xs font-medium transition-colors"
                    [class.bg-violet-600]="exitStrategyMode === 'Advanced'" [class.text-white]="exitStrategyMode === 'Advanced'"
                    [class.bg-violet-100]="exitStrategyMode !== 'Advanced'" [class.text-violet-600]="exitStrategyMode !== 'Advanced'">
                    Nâng cao
                  </button>
                </div>
              </div>

              <!-- SIMPLE MODE: flat exit targets -->
              <div *ngIf="exitStrategyMode === 'Simple'">
                <div class="flex items-center justify-between mb-2">
                  <span class="text-xs text-violet-600">{{ plan.exitTargets.length }} mục tiêu</span>
                  <button (click)="addExitTarget()" class="px-2 py-0.5 text-xs bg-violet-600 text-white hover:bg-violet-700 rounded">+ Thêm</button>
                </div>
                <div *ngFor="let et of plan.exitTargets; let i = index" class="flex items-center gap-2 mb-2">
                  <select [(ngModel)]="et.actionType" class="px-2 py-1 border border-violet-300 rounded text-sm w-32">
                    <option value="TakeProfit">Chốt lời</option>
                    <option value="CutLoss">Cắt lỗ</option>
                    <option value="TrailingStop">Trailing Stop</option>
                    <option value="PartialExit">Bán một phần</option>
                  </select>
                  <input [(ngModel)]="et.price" type="text" inputmode="numeric" appNumMask placeholder="Giá"
                    class="px-2 py-1 border border-violet-300 rounded text-sm text-right w-28">
                  <input [(ngModel)]="et.percentOfPosition" type="number" placeholder="% vị thế" min="1" max="100"
                    class="px-2 py-1 border border-violet-300 rounded text-sm text-right w-20">
                  <span class="text-xs text-violet-500">%</span>
                  <input [(ngModel)]="et.label" type="text" placeholder="Ghi chú" class="px-2 py-1 border border-violet-300 rounded text-sm flex-1">
                  <button (click)="removeExitTarget(i)" class="text-red-400 hover:text-red-600">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                    </svg>
                  </button>
                </div>
                <div *ngIf="plan.exitTargets.length === 0" class="text-xs text-violet-400">Chưa có mục tiêu thoát lệnh. TP hiện tại: {{ plan.target | number:'1.0-0' }}đ</div>
              </div>

              <!-- ADVANCED MODE: Scenario Playbook -->
              <div *ngIf="exitStrategyMode === 'Advanced'">
                <!-- Time Horizon + AI Suggestion Row -->
                <div class="flex items-center gap-2 mb-3 flex-wrap">
                  <label class="text-xs text-violet-700 font-medium whitespace-nowrap">Kỳ vọng:</label>
                  <select [(ngModel)]="selectedTimeHorizon" class="px-2 py-1 border border-violet-300 rounded text-xs">
                    <option value="Short">Ngắn hạn</option>
                    <option value="Medium">Trung hạn</option>
                    <option value="Long">Dài hạn</option>
                  </select>
                  <button (click)="fetchScenarioSuggestion()"
                    [disabled]="!plan.symbol || !plan.entryPrice || loadingSuggestion"
                    class="px-3 py-1 text-xs bg-indigo-600 text-white hover:bg-indigo-700 rounded disabled:opacity-50 flex items-center gap-1 font-medium">
                    <svg *ngIf="!loadingSuggestion" class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.347.346a2 2 0 01-2.829 0l-.346-.346a5 5 0 010-7.072z"/>
                    </svg>
                    <svg *ngIf="loadingSuggestion" class="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24">
                      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                      <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                    </svg>
                    {{ loadingSuggestion ? 'Đang phân tích...' : 'Gợi ý kịch bản' }}
                  </button>
                </div>

                <!-- Scenario Suggestion Panel -->
                <div *ngIf="scenarioSuggestion" class="mb-4 border border-indigo-200 rounded-lg bg-indigo-50 p-3">
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-xs font-semibold text-indigo-800">Phân tích kỹ thuật — {{ scenarioSuggestion.symbol }}</span>
                    <button (click)="scenarioSuggestion = null" class="text-gray-400 hover:text-gray-600 text-xs">&#10005;</button>
                  </div>
                  <!-- Technical basis summary -->
                  <div class="flex flex-wrap gap-2 mb-3 text-xs">
                    <span *ngIf="scenarioSuggestion.technicalBasis.ema20" class="bg-white border border-indigo-200 rounded px-2 py-0.5 text-indigo-700">
                      EMA20: {{ scenarioSuggestion.technicalBasis.ema20 | number:'1.0-0' }}
                    </span>
                    <span *ngIf="scenarioSuggestion.technicalBasis.ema50" class="bg-white border border-indigo-200 rounded px-2 py-0.5 text-indigo-700">
                      EMA50: {{ scenarioSuggestion.technicalBasis.ema50 | number:'1.0-0' }}
                    </span>
                    <span *ngIf="scenarioSuggestion.technicalBasis.rsi14" class="bg-white border border-indigo-200 rounded px-2 py-0.5"
                      [class.text-red-600]="scenarioSuggestion.technicalBasis.rsi14 > 70"
                      [class.text-green-600]="scenarioSuggestion.technicalBasis.rsi14 < 30"
                      [class.text-indigo-700]="scenarioSuggestion.technicalBasis.rsi14 >= 30 && scenarioSuggestion.technicalBasis.rsi14 <= 70">
                      RSI: {{ scenarioSuggestion.technicalBasis.rsi14 | number:'1.0-1' }}
                    </span>
                    <span *ngIf="scenarioSuggestion.technicalBasis.atr14" class="bg-white border border-indigo-200 rounded px-2 py-0.5 text-indigo-700">
                      ATR: {{ scenarioSuggestion.technicalBasis.atr14 | number:'1.0-0' }}
                    </span>
                    <span *ngIf="(scenarioSuggestion.technicalBasis.supportLevels || []).length > 0" class="bg-green-50 border border-green-200 rounded px-2 py-0.5 text-green-700">
                      Hỗ trợ: {{ (scenarioSuggestion.technicalBasis.supportLevels || [])[0] | number:'1.0-0' }}
                    </span>
                    <span *ngIf="(scenarioSuggestion.technicalBasis.resistanceLevels || []).length > 0" class="bg-red-50 border border-red-200 rounded px-2 py-0.5 text-red-700">
                      Kháng cự: {{ (scenarioSuggestion.technicalBasis.resistanceLevels || [])[0] | number:'1.0-0' }}
                    </span>
                  </div>
                  <!-- Suggested nodes list -->
                  <div class="space-y-2 mb-3">
                    <div *ngFor="let sn of scenarioSuggestion.nodes; trackBy: trackBySuggestionNodeId"
                      class="bg-white border rounded-lg p-2.5 flex items-start gap-2"
                      [class.border-indigo-200]="selectedSuggestionNodes.has(sn.nodeId)"
                      [class.border-gray-200]="!selectedSuggestionNodes.has(sn.nodeId)">
                      <input type="checkbox" [checked]="selectedSuggestionNodes.has(sn.nodeId)"
                        (change)="toggleSuggestionNode(sn.nodeId)"
                        class="mt-0.5 rounded border-gray-300 text-indigo-600">
                      <div class="flex-1 min-w-0">
                        <div class="flex items-center gap-1.5 mb-1">
                          <span class="text-xs font-semibold text-gray-800">{{ sn.label }}</span>
                          <span class="px-1.5 py-0.5 rounded text-xs font-medium"
                            [class.bg-green-100]="sn.category === 'TakeProfit'"
                            [class.text-green-700]="sn.category === 'TakeProfit'"
                            [class.bg-red-100]="sn.category === 'StopLoss'"
                            [class.text-red-700]="sn.category === 'StopLoss'"
                            [class.bg-blue-100]="sn.category === 'AddPosition'"
                            [class.text-blue-700]="sn.category === 'AddPosition'"
                            [class.bg-amber-100]="sn.category === 'Sideway'"
                            [class.text-amber-700]="sn.category === 'Sideway'">
                            {{ getCategoryLabel(sn.category) }}
                          </span>
                        </div>
                        <div class="text-xs text-gray-600">
                          Điều kiện: <span class="font-medium">{{ sn.conditionType }}</span>
                          <span *ngIf="sn.conditionValue"> &#64; {{ sn.conditionValue | number:'1.0-0' }}</span>
                          → Hành động: <span class="font-medium">{{ sn.actionType }}</span>
                          <span *ngIf="sn.actionValue"> {{ sn.actionValue }}</span>
                        </div>
                        <div class="text-xs text-gray-400 mt-1 italic">{{ sn.reasoning }}</div>
                      </div>
                    </div>
                  </div>
                  <!-- Apply buttons -->
                  <div class="flex items-center gap-2">
                    <button (click)="applySelectedSuggestions()"
                      [disabled]="selectedSuggestionNodes.size === 0"
                      class="px-3 py-1.5 text-xs bg-indigo-600 text-white hover:bg-indigo-700 rounded-lg font-medium disabled:opacity-50">
                      Áp dụng gợi ý ({{ selectedSuggestionNodes.size }})
                    </button>
                    <button (click)="applyAllSuggestions()"
                      class="px-3 py-1.5 text-xs bg-indigo-500 text-white hover:bg-indigo-600 rounded-lg font-medium">
                      Tạo kế hoạch từ gợi ý
                    </button>
                  </div>
                </div>

                <!-- Preset row -->
                <div class="flex items-center gap-2 mb-3">
                  <select [(ngModel)]="selectedPresetId" class="px-2 py-1 border border-violet-300 rounded text-xs flex-1">
                    <option value="">-- Chọn mẫu kịch bản --</option>
                    <optgroup label="Mẫu hệ thống">
                      <option *ngFor="let p of getSystemPresets()" [value]="p.id">{{ p.nameVi }} — {{ p.description }}</option>
                    </optgroup>
                    <optgroup *ngIf="getUserPresets().length > 0" label="Mẫu của tôi">
                      <option *ngFor="let p of getUserPresets()" [value]="p.id">{{ p.nameVi }} — {{ p.description }}</option>
                    </optgroup>
                  </select>
                  <button (click)="applyScenarioPreset()" [disabled]="!selectedPresetId"
                    class="px-3 py-1 text-xs bg-violet-600 text-white hover:bg-violet-700 rounded disabled:opacity-50">Áp dụng</button>
                  <button *ngIf="selectedPresetId && !isPresetSelected()"
                    (click)="deleteScenarioTemplate(selectedPresetId)"
                    class="px-2 py-1 text-xs border border-red-300 hover:bg-red-50 text-red-500 rounded"
                    title="Xoá mẫu">
                    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"></path>
                    </svg>
                  </button>
                </div>

                <!-- Save as scenario template -->
                <div *ngIf="scenarioNodes.length > 0" class="mb-3">
                  <div *ngIf="!showSaveScenarioTemplate">
                    <button (click)="showSaveScenarioTemplate = true"
                      class="px-3 py-1 text-xs border border-violet-300 hover:bg-violet-50 text-violet-600 rounded font-medium transition-colors">
                      + Lưu mẫu kịch bản
                    </button>
                  </div>
                  <div *ngIf="showSaveScenarioTemplate" class="flex items-center gap-2">
                    <input [(ngModel)]="newScenarioTemplateName" type="text" placeholder="Tên mẫu..."
                      class="px-2 py-1 border border-violet-300 rounded text-xs focus:ring-2 focus:ring-violet-500 w-36">
                    <input [(ngModel)]="newScenarioTemplateDesc" type="text" placeholder="Mô tả ngắn..."
                      class="px-2 py-1 border border-violet-300 rounded text-xs focus:ring-2 focus:ring-violet-500 w-48">
                    <button (click)="saveScenarioTemplate()" [disabled]="!newScenarioTemplateName.trim() || savingScenarioTemplate"
                      class="px-3 py-1 text-xs bg-violet-600 hover:bg-violet-700 disabled:bg-gray-300 text-white rounded font-medium transition-colors">
                      {{ savingScenarioTemplate ? 'Đang lưu...' : 'Lưu' }}
                    </button>
                    <button (click)="showSaveScenarioTemplate = false; newScenarioTemplateName = ''; newScenarioTemplateDesc = ''"
                      class="px-2 py-1 text-gray-500 hover:text-gray-700 text-xs">&#10005;</button>
                  </div>
                </div>

                <!-- Visual Flowchart Scenario Tree -->
                <div class="scenario-tree">
                  <ng-container *ngFor="let node of getScenarioRootNodes(); let isLast = last">
                    <ng-container *ngTemplateOutlet="scenarioNodeTpl; context: { $implicit: node, depth: 0, isLast: isLast }"></ng-container>
                  </ng-container>
                </div>

                <button (click)="addScenarioNode(null)" class="mt-3 px-3 py-1.5 text-xs bg-violet-600 text-white hover:bg-violet-700 rounded-lg flex items-center gap-1">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path></svg>
                  Thêm kịch bản gốc
                </button>

                <div *ngIf="scenarioNodes.length === 0" class="text-xs text-violet-400 mt-2 text-center py-4">
                  Chưa có kịch bản nào. Chọn mẫu hoặc thêm kịch bản gốc.
                </div>
              </div>

              <!-- Scenario History Panel -->
              <div *ngIf="exitStrategyMode === 'Advanced' && selectedPlanId && scenarioHistory.length > 0" class="mt-4">
                <h4 class="text-sm font-semibold text-violet-700 mb-2">Lịch sử kịch bản</h4>
                <div class="space-y-2">
                  <div *ngFor="let item of scenarioHistory"
                    class="flex items-center gap-2 px-3 py-2 rounded-lg text-xs"
                    [ngClass]="{
                      'bg-green-50 border border-green-200': item.status === 'Triggered',
                      'bg-yellow-50 border border-yellow-200': item.status === 'Pending',
                      'bg-gray-50 border border-gray-200': item.status === 'Skipped'
                    }">
                    <span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium"
                      [ngClass]="{
                        'bg-green-100 text-green-800': item.status === 'Triggered',
                        'bg-yellow-100 text-yellow-800': item.status === 'Pending',
                        'bg-gray-100 text-gray-600': item.status === 'Skipped'
                      }">
                      {{ item.status === 'Triggered' ? 'Đã kích hoạt' : item.status === 'Pending' ? 'Chờ' : 'Bỏ qua' }}
                    </span>
                    <span class="font-medium text-gray-800">{{ item.label || '(Chưa đặt tên)' }}</span>
                    <span class="text-gray-500">&#8594; {{ getActionLabel(item.actionType, item.actionValue) }}</span>
                    <span *ngIf="item.triggeredAt" class="text-green-600 ml-auto whitespace-nowrap">
                      {{ formatTriggerTime(item.triggeredAt) }}
                      <span *ngIf="item.priceAtTrigger" class="ml-1">— Giá: {{ item.priceAtTrigger | number:'1.0-0' }}đ</span>
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Scenario Node Template (recursive visual flowchart) -->
            <ng-template #scenarioNodeTpl let-node let-depth="depth" let-isLast="isLast">
              <div class="scenario-node-wrapper" [class.is-last]="isLast">
                <!-- Horizontal connector line (not for root nodes) -->
                <div *ngIf="depth > 0" class="scenario-connector">
                  <div class="connector-horizontal"></div>
                </div>

                <!-- Node card -->
                <div class="scenario-card"
                  [class.border-green-500]="node.status === 'Triggered'"
                  [class.bg-green-50]="node.status === 'Triggered'"
                  [class.border-yellow-400]="node.status === 'Pending'"
                  [class.bg-yellow-50]="node.status === 'Pending'"
                  [class.border-gray-300]="node.status === 'Skipped'"
                  [class.bg-gray-50]="node.status === 'Skipped'">

                  <!-- Status badge + collapse toggle -->
                  <div class="flex items-center justify-between mb-2">
                    <div class="flex items-center gap-1.5">
                      <span *ngIf="node.status === 'Triggered'" class="inline-flex items-center gap-1 px-1.5 py-0.5 bg-green-100 text-green-700 rounded-full text-xs font-medium">
                        <svg class="w-3 h-3" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd"/></svg>
                        Đã kích hoạt
                      </span>
                      <span *ngIf="node.status === 'Pending'" class="inline-flex items-center gap-1 px-1.5 py-0.5 bg-yellow-100 text-yellow-700 rounded-full text-xs font-medium">
                        <svg class="w-3 h-3" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm1-12a1 1 0 10-2 0v4a1 1 0 00.293.707l2.828 2.829a1 1 0 101.415-1.415L11 9.586V6z" clip-rule="evenodd"/></svg>
                        Chờ
                      </span>
                      <span *ngIf="node.status === 'Skipped'" class="inline-flex items-center gap-1 px-1.5 py-0.5 bg-gray-100 text-gray-500 rounded-full text-xs font-medium">
                        Bỏ qua
                      </span>
                    </div>
                    <div class="flex items-center gap-1">
                      <button *ngIf="hasScenarioChildren(node.nodeId)" (click)="toggleScenarioCollapse(node.nodeId)"
                        class="p-0.5 text-violet-400 hover:text-violet-600 rounded transition-colors" [title]="collapsedNodes.has(node.nodeId) ? 'Mở rộng nhánh' : 'Thu gọn nhánh'">
                        <svg class="w-4 h-4 transition-transform" [class.rotate-180]="collapsedNodes.has(node.nodeId)" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
                        </svg>
                      </button>
                      <button (click)="addScenarioNode(node.nodeId)" class="p-0.5 text-indigo-400 hover:text-indigo-600 rounded transition-colors" title="Thêm kịch bản con">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path></svg>
                      </button>
                      <button (click)="removeScenarioNode(node.nodeId)" class="p-0.5 text-red-400 hover:text-red-600 rounded transition-colors" title="Xoá kịch bản">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                        </svg>
                      </button>
                    </div>
                  </div>

                  <!-- Condition row -->
                  <div class="flex items-center gap-1.5 flex-wrap">
                    <span class="text-xs font-bold text-indigo-600 bg-indigo-50 px-1.5 py-0.5 rounded">NẾU</span>
                    <select [(ngModel)]="node.conditionType" class="px-1.5 py-1 border border-indigo-200 rounded text-xs bg-white focus:ring-1 focus:ring-indigo-400">
                      <option value="PriceAbove">Giá >=</option>
                      <option value="PriceBelow">Giá <=</option>
                      <option value="PricePercentChange">Thay đổi %</option>
                      <option value="TrailingStopHit">Chạm trailing</option>
                      <option value="TimeElapsed">Sau N ngày</option>
                    </select>
                    <input *ngIf="node.conditionType !== 'TrailingStopHit'" [(ngModel)]="node.conditionValue" type="text" inputmode="numeric" appNumMask
                      class="w-24 px-1.5 py-1 border border-indigo-200 rounded text-xs text-right bg-white focus:ring-1 focus:ring-indigo-400"
                      [placeholder]="node.conditionType === 'PricePercentChange' ? '%' : node.conditionType === 'TimeElapsed' ? 'ngày' : 'đ'">
                  </div>

                  <!-- Action row -->
                  <div class="flex items-center gap-1.5 flex-wrap mt-1.5">
                    <span class="text-xs font-bold text-violet-600 bg-violet-50 px-1.5 py-0.5 rounded">&#8594;</span>
                    <select [(ngModel)]="node.actionType" class="px-1.5 py-1 border border-violet-200 rounded text-xs bg-white focus:ring-1 focus:ring-violet-400">
                      <option value="SellPercent">Bán % vị thế</option>
                      <option value="SellAll">Bán tất cả</option>
                      <option value="MoveStopLoss">Dời SL</option>
                      <option value="MoveStopToBreakeven">SL về hòa vốn</option>
                      <option value="ActivateTrailingStop">Bật trailing stop</option>
                      <option value="AddPosition">Thêm vị thế</option>
                      <option value="SendNotification">Chỉ thông báo</option>
                    </select>
                    <input *ngIf="node.actionType === 'SellPercent' || node.actionType === 'AddPosition' || node.actionType === 'MoveStopLoss'"
                      [(ngModel)]="node.actionValue" type="text" inputmode="numeric" appNumMask
                      class="w-16 px-1.5 py-1 border border-violet-200 rounded text-xs text-right bg-white focus:ring-1 focus:ring-violet-400"
                      [placeholder]="node.actionType === 'MoveStopLoss' ? 'đ' : '%'">
                  </div>

                  <!-- Trailing Stop Config (inline) -->
                  <div *ngIf="node.actionType === 'ActivateTrailingStop'" class="mt-2 pl-2 flex items-center gap-2 text-xs border-l-2 border-violet-200">
                    <select [(ngModel)]="node.trailingStopConfig.method" class="px-1 py-0.5 border rounded text-xs bg-white">
                      <option value="Percentage">%</option>
                      <option value="ATR">ATR (ước tính)</option>
                      <option value="FixedAmount">Cố định (VNĐ)</option>
                    </select>
                    <input [(ngModel)]="node.trailingStopConfig.trailValue" type="number" placeholder="Giá trị" class="w-16 px-1 py-0.5 border rounded text-xs text-right bg-white">
                    <span class="text-indigo-400">Kích hoạt:</span>
                    <input [(ngModel)]="node.trailingStopConfig.activationPrice" type="text" inputmode="numeric" appNumMask placeholder="Giá" class="w-24 px-1 py-0.5 border rounded text-xs text-right bg-white">
                  </div>

                  <!-- Label / note -->
                  <input [(ngModel)]="node.label" type="text" placeholder="Ghi chú kịch bản..."
                    class="mt-2 w-full px-2 py-1 border border-gray-200 rounded text-xs bg-white/70 focus:ring-1 focus:ring-indigo-400">
                </div>

                <!-- Children (collapsible subtree with connector lines) -->
                <div *ngIf="hasScenarioChildren(node.nodeId) && !collapsedNodes.has(node.nodeId)" class="scenario-children">
                  <ng-container *ngFor="let child of getScenarioChildNodes(node.nodeId); let childIsLast = last">
                    <ng-container *ngTemplateOutlet="scenarioNodeTpl; context: { $implicit: child, depth: depth + 1, isLast: childIsLast }"></ng-container>
                  </ng-container>
                </div>

                <!-- Collapsed indicator -->
                <div *ngIf="hasScenarioChildren(node.nodeId) && collapsedNodes.has(node.nodeId)" class="mt-1 ml-6">
                  <button (click)="toggleScenarioCollapse(node.nodeId)" class="text-xs text-violet-400 hover:text-violet-600 flex items-center gap-1">
                    <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"></path></svg>
                    {{ getScenarioChildNodes(node.nodeId).length }} kịch bản con (đã thu gọn)
                  </button>
                </div>
              </div>
            </ng-template>

            <div class="mt-4">
              <label class="block text-sm font-medium text-gray-700 mb-1">Lý do vào lệnh</label>
              <textarea [(ngModel)]="plan.reason" rows="2"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="Lý do cụ thể để vào lệnh này..."></textarea>
            </div>

            <div class="mt-4">
              <label class="block text-sm font-medium text-gray-700 mb-2">
                Mức độ tự tin: {{ plan.confidenceLevel }}/10
                <sup class="text-amber-400 font-bold cursor-default" title="Giải thích 4">4</sup>
              </label>
              <input [(ngModel)]="plan.confidenceLevel" type="range" min="1" max="10"
                class="w-full h-2 bg-gray-200 rounded-lg cursor-pointer">
            </div>

            <!-- Glossary footnotes -->
            <div class="mt-4 rounded-lg bg-gray-50 border border-gray-200 px-4 py-3 text-xs text-gray-500 space-y-1">
              <div><sup class="text-red-400 font-bold">1</sup> <strong>Stop-Loss (SL) — Cắt lỗ:</strong> Mức giá mà bạn chấp nhận bán lỗ để giới hạn thiệt hại. VD: Mua ở 50,000 đ, SL = 47,500 đ → thua tối đa 5%.</div>
              <div><sup class="text-emerald-500 font-bold">2</sup> <strong>Take-Profit (TP) — Chốt lời:</strong> Mức giá mục tiêu để hiện thực hóa lợi nhuận. VD: TP = 57,500 đ → lãi 15% nếu chạm mức này.</div>
              <div><sup class="text-violet-400 font-bold">3</sup> <strong>Số lượng CP — Position Size:</strong> Số cổ phiếu nên mua để rủi ro không vượt % vốn cho phép. Tự tính nếu chọn Danh mục có Risk Profile.</div>
              <div><sup class="text-amber-400 font-bold">4</sup> <strong>Mức độ tự tin:</strong> Điểm 1–10 đánh giá mức chắc chắn của tín hiệu. &lt;5 = tín hiệu yếu nên bỏ qua; ≥8 = tín hiệu mạnh.</div>
              <div><sup class="text-blue-400 font-bold">5</sup> <strong>R:R Ratio (Risk:Reward):</strong> Tỷ lệ lợi nhuận/rủi ro. R:R = 1:2 nghĩa là rủi ro 1đ để kiếm 2đ. Nên giao dịch khi R:R ≥ 1:2 (màu xanh).</div>
            </div>

            <!-- Mini Stock Info Card -->
            <div *ngIf="stockPrice" class="mt-4 bg-gradient-to-r from-indigo-50 to-blue-50 rounded-lg p-4 border border-indigo-100">
              <div class="flex items-center justify-between mb-2">
                <span class="font-bold text-indigo-800 text-lg">{{ stockPrice.symbol }}</span>
                <button (click)="applyStockPrice()" class="text-xs bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-1 rounded-full transition-colors">
                  Áp dụng giá vào lệnh
                </button>
              </div>
              <div class="grid grid-cols-4 gap-3 text-sm">
                <div>
                  <div class="text-xs text-gray-500">Giá hiện tại</div>
                  <div class="font-bold text-gray-800">{{ stockPrice.close | number:'1.0-0' }}</div>
                </div>
                <div>
                  <div class="text-xs text-gray-500">Mở cửa</div>
                  <div class="font-medium text-gray-700">{{ stockPrice.open | number:'1.0-0' }}</div>
                </div>
                <div>
                  <div class="text-xs text-gray-500">Cao / Thấp</div>
                  <div class="font-medium text-gray-700">{{ stockPrice.high | number:'1.0-0' }} / {{ stockPrice.low | number:'1.0-0' }}</div>
                </div>
                <div>
                  <div class="text-xs text-gray-500">KL giao dịch</div>
                  <div class="font-medium text-gray-700">{{ stockPrice.volume | number:'1.0-0' }}</div>
                </div>
              </div>
              <div class="mt-2 text-xs" [class.text-green-600]="stockPrice.close >= stockPrice.open" [class.text-red-600]="stockPrice.close < stockPrice.open">
                {{ stockPrice.close >= stockPrice.open ? '+' : '' }}{{ ((stockPrice.close - stockPrice.open) / stockPrice.open * 100).toFixed(2) }}% so với giá mở cửa
              </div>
            </div>
            <div *ngIf="stockError" class="mt-4 bg-yellow-50 rounded-lg p-3 text-sm text-yellow-700 border border-yellow-200">
              {{ stockError }}
            </div>

            <!-- Risk profile auto-fill -->
            <div *ngIf="riskProfile" class="mt-4 bg-blue-50 rounded-lg p-3 text-sm">
              <div class="font-medium text-blue-700 mb-1">Risk Profile từ danh mục</div>
              <div class="text-blue-600 grid grid-cols-2 gap-1">
                <span>Max Position: {{ riskProfile.maxPositionSizePercent }}%</span>
                <span>Max Risk: {{ riskProfile.maxPortfolioRiskPercent }}%</span>
                <span>R:R mục tiêu: {{ riskProfile.defaultRiskRewardRatio }}</span>
                <span>Max Drawdown Alert: {{ riskProfile.maxDrawdownAlertPercent }}%</span>
              </div>
            </div>

            <!-- Risk Profile Enforcement Warnings -->
            <div *ngIf="riskViolations.length > 0" class="mt-4 bg-red-50 border-2 border-red-300 rounded-lg p-4">
              <div class="font-bold text-red-700 mb-2 flex items-center gap-2">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z"></path>
                </svg>
                Vi phạm Risk Profile ({{ riskViolations.length }})
              </div>
              <ul class="space-y-1">
                <li *ngFor="let v of riskViolations" class="text-sm text-red-600 flex items-start gap-1">
                  <span class="mt-0.5">•</span> {{ v }}
                </li>
              </ul>
              <div *ngIf="!riskOverrideConfirmed" class="mt-3">
                <button (click)="riskOverrideConfirmed = true"
                  class="text-xs bg-red-600 hover:bg-red-700 text-white px-3 py-1.5 rounded-lg transition-colors">
                  Tôi biết và chấp nhận rủi ro này
                </button>
              </div>
              <div *ngIf="riskOverrideConfirmed" class="mt-2 text-xs text-red-500 italic">
                Đã xác nhận vi phạm — hãy cẩn trọng!
              </div>
            </div>
          </div>

          <!-- Strategy Rules Reference -->
          <div *ngIf="selectedStrategy" class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-3">Quy tắc chiến lược: {{ selectedStrategy.name }}</h2>
            <div class="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
              <div class="bg-green-50 rounded-lg p-3">
                <div class="font-medium text-green-700 mb-1">Vào lệnh</div>
                <div class="text-green-600 whitespace-pre-wrap">{{ selectedStrategy.entryRules || 'Chưa thiết lập' }}</div>
              </div>
              <div class="bg-red-50 rounded-lg p-3">
                <div class="font-medium text-red-700 mb-1">Thoát lệnh</div>
                <div class="text-red-600 whitespace-pre-wrap">{{ selectedStrategy.exitRules || 'Chưa thiết lập' }}</div>
              </div>
              <div class="bg-orange-50 rounded-lg p-3">
                <div class="font-medium text-orange-700 mb-1">Quản lý rủi ro</div>
                <div class="text-orange-600 whitespace-pre-wrap">{{ selectedStrategy.riskRules || 'Chưa thiết lập' }}</div>
              </div>
            </div>
          </div>

          <!-- Notes -->
          <div class="bg-white rounded-lg shadow p-6">
            <label class="block text-sm font-medium text-gray-700 mb-1">Ghi chú thêm</label>
            <textarea [(ngModel)]="plan.notes" rows="3"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Ghi chú thêm về giao dịch này..."></textarea>
          </div>
        </div>

        <!-- Right sidebar: Position Sizing + Metrics + Checklist -->
        <div class="space-y-6">
          <!-- Position Sizing Results -->
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Tính toán vị thế</h2>

            <div *ngIf="optimalShares === 0 && !plan.entryPrice" class="text-center py-4 text-gray-400 text-sm">
              Nhập giá vào lệnh và stop-loss để tính vị thế
            </div>

            <div *ngIf="optimalShares > 0">
              <!-- Warning -->
              <div *ngIf="positionWarning" class="mb-3 bg-yellow-50 border border-yellow-200 rounded-lg p-3">
                <div class="text-yellow-700 text-sm font-medium">Cảnh báo</div>
                <div class="text-yellow-600 text-sm">{{ positionWarning }}</div>
              </div>

              <!-- Main result -->
              <div class="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-lg p-4 mb-3 text-center">
                <div class="text-sm text-blue-600 font-medium">Số lượng cổ phiếu tối ưu</div>
                <div class="text-3xl font-bold text-blue-800 my-1">{{ optimalShares | number }}</div>
                <div class="text-sm text-blue-500">Lô {{ getLotCount() }} lô (x100 cp)</div>
              </div>

              <div class="grid grid-cols-2 gap-2">
                <div class="border rounded-lg p-2">
                  <div class="text-xs text-gray-500">Giá trị vị thế</div>
                  <div class="font-bold text-gray-800 text-sm">{{ optimalPositionValue | vndCurrency }}</div>
                  <div class="text-xs text-gray-400">{{ positionPercent | number:'1.1-1' }}% danh mục</div>
                </div>
                <div class="border rounded-lg p-2">
                  <div class="text-xs text-gray-500">Tiền rủi ro tối đa</div>
                  <div class="font-bold text-red-600 text-sm">{{ maxRiskAmount | vndCurrency }}</div>
                  <div class="text-xs text-gray-400">{{ riskPercent }}% của {{ accountBalance | vndCurrency }}</div>
                </div>
              </div>

              <!-- Status -->
              <div class="mt-3 p-2 rounded-lg text-center text-sm font-medium"
                [class.bg-green-100]="withinLimit" [class.text-green-700]="withinLimit"
                [class.bg-red-100]="!withinLimit" [class.text-red-700]="!withinLimit">
                {{ withinLimit ? 'Vị thế nằm trong giới hạn rủi ro cho phép' : 'Vị thế VƯỢT giới hạn rủi ro!' }}
              </div>
            </div>
          </div>

          <!-- Advanced Sizing Models (P3) -->
          @if (sizingModels.length > 0) {
            <div class="bg-white rounded-lg shadow p-6">
              <h2 class="text-lg font-semibold mb-3">So sánh mô hình</h2>
              <div class="overflow-x-auto">
                <table class="w-full text-sm">
                  <thead>
                    <tr class="border-b text-left text-xs text-gray-500 uppercase">
                      <th class="pb-2 pr-2">Mô hình</th>
                      <th class="pb-2 pr-2 text-right">CP</th>
                      <th class="pb-2 pr-2 text-right">%DM</th>
                      <th class="pb-2"></th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (m of sizingModels; track m.model) {
                      <tr class="border-b last:border-0 hover:bg-gray-50 cursor-pointer"
                        [class.bg-blue-50]="m.model === selectedSizingModel"
                        (click)="applySizingModel(m)">
                        <td class="py-2 pr-2">
                          <div class="font-medium text-gray-800">{{ m.modelVi }}</div>
                          @if (m.note) {
                            <div class="text-xs text-gray-400 truncate max-w-[160px]">{{ m.note }}</div>
                          }
                        </td>
                        <td class="py-2 pr-2 text-right font-mono font-bold">{{ m.shares | number }}</td>
                        <td class="py-2 pr-2 text-right">
                          <span class="text-xs font-medium px-1.5 py-0.5 rounded-full"
                            [class.bg-green-100]="m.withinLimit" [class.text-green-700]="m.withinLimit"
                            [class.bg-red-100]="!m.withinLimit" [class.text-red-700]="!m.withinLimit">
                            {{ m.positionPercent | number:'1.1-1' }}%
                          </span>
                        </td>
                        <td class="py-2">
                          @if (m.model === recommendedModel) {
                            <span class="text-xs text-blue-600 font-medium">Gợi ý</span>
                          }
                          @if (m.model === selectedSizingModel) {
                            <span class="text-xs text-emerald-600 font-medium">Đang chọn</span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              @if (loadingSizingModels) {
                <div class="text-center text-gray-400 text-xs mt-2">Đang tính...</div>
              }
              @if (sizingModels.length === 1 && !loadingSizingModels) {
                <div class="text-center text-gray-400 text-xs mt-2">Tra cứu mã CP để xem thêm mô hình (ATR, Turtle, Volatility)</div>
              }
            </div>
          }

          <!-- Quick Metrics -->
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Chỉ số giao dịch</h2>
            <div class="space-y-3">
              <div class="flex justify-between items-center">
                <span class="text-sm text-gray-600">
                  R:R Ratio <sup class="text-blue-400 font-bold cursor-default" title="Giải thích 5">5</sup>
                </span>
                <span class="font-bold text-lg" [class.text-green-600]="rr >= 2"
                  [class.text-yellow-600]="rr >= 1 && rr < 2" [class.text-red-600]="rr < 1">
                  1 : {{ rr | number:'1.2-2' }}
                </span>
              </div>
              <div class="flex justify-between items-center">
                <span class="text-sm text-gray-600">Rủi ro / CP</span>
                <span class="font-bold text-red-600">{{ riskPerShare | vndCurrency }}</span>
              </div>
              <div class="flex justify-between items-center">
                <span class="text-sm text-gray-600">Giá trị vị thế</span>
                <span class="font-bold">{{ positionValue | vndCurrency }}</span>
              </div>
              <div class="flex justify-between items-center border-t pt-2">
                <span class="text-sm text-green-600">Lời tiềm năng</span>
                <span class="font-bold text-green-600">+{{ potentialProfit | vndCurrency }}</span>
              </div>
              <div class="flex justify-between items-center">
                <span class="text-sm text-red-600">Lỗ tiềm năng</span>
                <span class="font-bold text-red-600">-{{ potentialLoss | vndCurrency }}</span>
              </div>
              <div class="flex justify-between items-center border-t pt-2">
                <span class="text-sm text-gray-600">SL với stop%</span>
                <span class="font-bold">{{ stopPercent | number:'1.2-2' }}%</span>
              </div>
            </div>
          </div>

          <!-- Pre-trade Checklist (P6: Dynamic) -->
          <div class="bg-white rounded-lg shadow p-6">
            <div class="flex justify-between items-center mb-2">
              <h2 class="text-lg font-semibold">Checklist trước giao dịch</h2>
              <span class="text-sm font-medium px-2 py-1 rounded-full"
                [class.bg-green-100]="checklistScore >= 70"
                [class.text-green-700]="checklistScore >= 70"
                [class.bg-yellow-100]="checklistScore >= 50 && checklistScore < 70"
                [class.text-yellow-700]="checklistScore >= 50 && checklistScore < 70"
                [class.bg-red-100]="checklistScore < 50"
                [class.text-red-700]="checklistScore < 50">
                {{ checklistScore }}%
              </span>
            </div>
            <div class="flex items-center gap-2 mb-4 text-xs text-gray-400">
              <span>Tối thiểu 70% để giao dịch</span>
              <span>·</span>
              <span class="text-red-400">●3</span> bắt buộc
              <span class="text-amber-400">●2</span> quan trọng
              <span class="text-gray-300">●1</span> tham khảo
            </div>

            @for (category of checklistCategories; track category) {
              @if (getChecklistByCategory(category).length > 0) {
                <div class="mb-4">
                  <div class="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2">{{ category }}</div>
                  @for (item of getChecklistByCategory(category); track item.label) {
                    <div class="flex items-start gap-2 mb-2">
                      <input type="checkbox" [(ngModel)]="item.checked" (ngModelChange)="updateChecklistScore()"
                        class="mt-1 h-4 w-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500">
                      <div class="flex-1">
                        <div class="text-sm" [class.text-gray-800]="!item.checked" [class.text-gray-400]="item.checked"
                          [class.line-through]="item.checked">
                          {{ item.label }}
                          @if (item.weight === 3) { <span class="text-red-400 text-xs ml-1">●3</span> }
                          @else if (item.weight === 2) { <span class="text-amber-400 text-xs ml-1">●2</span> }
                        </div>
                        <div class="text-xs text-gray-400">{{ item.hint }}</div>
                      </div>
                    </div>
                  }
                </div>
              }
            }

            <!-- Progress bar -->
            <div class="w-full bg-gray-200 rounded-full h-2 mb-3">
              <div class="h-2 rounded-full transition-all"
                [style.width.%]="checklistScore"
                [class.bg-green-500]="checklistScore >= 70"
                [class.bg-yellow-400]="checklistScore >= 50 && checklistScore < 70"
                [class.bg-red-400]="checklistScore < 50">
              </div>
            </div>

            <!-- Go/No-Go -->
            <div class="p-4 rounded-lg text-center font-bold"
              [class.bg-green-100]="canTrade" [class.text-green-700]="canTrade"
              [class.bg-red-100]="!canTrade" [class.text-red-700]="!canTrade">
              {{ canTrade ? 'SẴN SÀNG GIAO DỊCH' : 'CHƯA ĐỦ ĐIỀU KIỆN' }}
            </div>
            <div *ngIf="!canTrade" class="mt-2 text-xs text-red-500 text-center">
              {{ getMissingCritical() }}
            </div>

            <!-- Save buttons -->
            <div class="mt-4 space-y-2">
              <button (click)="saveDraft()" [disabled]="saving || !plan.symbol"
                class="block w-full text-center border-2 border-blue-400 hover:bg-blue-50 text-blue-700 font-medium py-2.5 px-4 rounded-lg transition-colors disabled:opacity-50">
                {{ saving ? 'Đang lưu...' : (selectedPlanId ? 'Cập nhật nháp' : 'Lưu nháp') }}
              </button>
              <button (click)="saveAndReady()" [disabled]="saving || !plan.symbol || !canTrade"
                class="block w-full text-center bg-emerald-600 hover:bg-emerald-700 disabled:bg-gray-300 text-white font-medium py-2.5 px-4 rounded-lg transition-colors">
                {{ saving ? 'Đang lưu...' : 'Lưu & Sẵn sàng' }}
              </button>
            </div>

            <!-- Divider -->
            <div class="mt-4 pt-4 border-t border-gray-200">
              <p class="text-xs text-gray-400 text-center mb-2">Đã có KH? Thực hiện ngay</p>
            </div>

            <!-- Action buttons -->
            <div class="space-y-2">
              <a [routerLink]="['/trade-wizard']"
                [queryParams]="getWizardParams()"
                class="block w-full text-center bg-blue-600 hover:bg-blue-700 text-white font-medium py-3 px-4 rounded-lg transition-colors"
                [class.opacity-50]="!canTrade" [class.pointer-events-none]="!canTrade">
                Thực hiện qua Wizard
              </a>
              <a [routerLink]="['/trades/create']"
                [queryParams]="getTradeCreateParams()"
                class="block w-full text-center bg-emerald-600 hover:bg-emerald-700 text-white font-medium py-2 px-4 rounded-lg transition-colors text-sm">
                Thực hiện ngay
              </a>
              <button (click)="showOrderSheet = !showOrderSheet"
                class="block w-full text-center border border-indigo-300 hover:bg-indigo-50 text-indigo-700 font-medium py-2 px-4 rounded-lg transition-colors text-sm">
                {{ showOrderSheet ? 'Ẩn phiếu lệnh' : 'Xem phiếu lệnh' }}
              </button>
            </div>

            <!-- Order Sheet (Phieu Lenh) -->
            <div *ngIf="showOrderSheet" class="mt-4 bg-indigo-50 border-2 border-indigo-200 rounded-lg p-4 print:border-black print:bg-white" id="orderSheet">
              <div class="flex items-center justify-between mb-3">
                <h3 class="text-sm font-bold text-indigo-800">Phiếu lệnh</h3>
                <div class="flex gap-2">
                  <button (click)="copyOrderSheet()" class="px-3 py-1 text-xs bg-indigo-600 text-white hover:bg-indigo-700 rounded print:hidden">
                    Copy
                  </button>
                  <button (click)="printOrderSheet()" class="px-3 py-1 text-xs bg-gray-600 text-white hover:bg-gray-700 rounded print:hidden">
                    In
                  </button>
                </div>
              </div>
              <div class="font-mono text-sm text-indigo-900 whitespace-pre-line">{{ getOrderSheetText() }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Campaign Review Panel -->
      <div *ngIf="showReviewPanel && reviewPlanTarget" class="mt-6 bg-amber-50 border-2 border-amber-300 rounded-lg shadow p-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-amber-800">Đóng chiến dịch — {{ reviewPlanTarget.symbol }}</h2>
          <button (click)="closeCampaignReviewPanel()" class="text-gray-500 hover:text-gray-700 text-lg">&times;</button>
        </div>

        <div *ngIf="loadingReviewPreview" class="text-center py-4 text-gray-500">Đang tính toán...</div>

        <div *ngIf="!loadingReviewPreview && reviewPreview">
          <!-- Preview metrics -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-4">
            <div class="bg-white rounded-lg p-3 text-center">
              <div class="text-xs text-gray-500 mb-1">P&L</div>
              <div class="text-lg font-bold" [class.text-green-600]="reviewPreview.pnLAmount >= 0" [class.text-red-600]="reviewPreview.pnLAmount < 0">
                {{ reviewPreview.pnLAmount | vndCurrency }}
              </div>
            </div>
            <div class="bg-white rounded-lg p-3 text-center">
              <div class="text-xs text-gray-500 mb-1">% Lãi/Lỗ</div>
              <div class="text-lg font-bold" [class.text-green-600]="reviewPreview.pnLPercent >= 0" [class.text-red-600]="reviewPreview.pnLPercent < 0">
                {{ reviewPreview.pnLPercent | number:'1.2-2' }}%
              </div>
            </div>
            <div class="bg-white rounded-lg p-3 text-center">
              <div class="text-xs text-gray-500 mb-1">VND/ngày</div>
              <div class="text-lg font-bold" [class.text-green-600]="reviewPreview.pnLPerDay >= 0" [class.text-red-600]="reviewPreview.pnLPerDay < 0">
                {{ reviewPreview.pnLPerDay | vndCurrency }}
              </div>
            </div>
            <div class="bg-white rounded-lg p-3 text-center">
              <div class="text-xs text-gray-500 mb-1">Số ngày nắm giữ</div>
              <div class="text-lg font-bold text-gray-700">{{ reviewPreview.holdingDays }}</div>
            </div>
          </div>

          <!-- Additional metrics -->
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-3 mb-4 text-sm">
            <div class="bg-white rounded-lg p-2">
              <span class="text-gray-500">Tổng đầu tư:</span>
              <span class="font-medium ml-1">{{ reviewPreview.totalInvested | vndCurrency }}</span>
            </div>
            <div class="bg-white rounded-lg p-2">
              <span class="text-gray-500">Tổng thu về:</span>
              <span class="font-medium ml-1">{{ reviewPreview.totalReturned | vndCurrency }}</span>
            </div>
            <div class="bg-white rounded-lg p-2">
              <span class="text-gray-500">Đạt mục tiêu:</span>
              <span class="font-medium ml-1">{{ reviewPreview.targetAchievementPercent | number:'1.0-0' }}%</span>
            </div>
          </div>

          <!-- Lessons learned -->
          <div class="mb-4">
            <label class="block text-sm font-medium text-amber-800 mb-1">Bài học rút ra</label>
            <textarea [(ngModel)]="reviewLessonsLearned" rows="3"
              class="w-full px-3 py-2 border border-amber-300 rounded-lg focus:ring-2 focus:ring-amber-500 text-sm"
              placeholder="Ghi lại bài học, sai lầm, điểm tốt của chiến dịch này..."></textarea>
          </div>

          <!-- Submit button -->
          <div class="flex justify-end gap-2">
            <button (click)="closeCampaignReviewPanel()"
              class="px-4 py-2 border border-gray-300 hover:bg-gray-50 text-gray-700 rounded-lg text-sm font-medium transition-colors">
              Huỷ
            </button>
            <button (click)="submitCampaignReview()" [disabled]="submittingReview"
              class="px-6 py-2 bg-amber-600 hover:bg-amber-700 disabled:bg-gray-300 text-white rounded-lg text-sm font-medium transition-colors">
              {{ submittingReview ? 'Đang xử lý...' : 'Xác nhận đóng chiến dịch' }}
            </button>
          </div>
        </div>

        <div *ngIf="!loadingReviewPreview && !reviewPreview" class="text-center py-4 text-red-500 text-sm">
          Không thể tải dữ liệu review. Vui lòng thử lại.
        </div>
      </div>

      <!-- Review Data Display for loaded Reviewed plan -->
      <div *ngIf="selectedPlanId && selectedPlanStatus === 'Reviewed' && loadedReviewData" class="mt-6 bg-violet-50 border border-violet-200 rounded-lg shadow p-6">
        <h2 class="text-lg font-semibold text-violet-800 mb-4">Kết quả chiến dịch</h2>
        <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-4">
          <div class="bg-white rounded-lg p-3 text-center">
            <div class="text-xs text-gray-500 mb-1">P&L</div>
            <div class="text-lg font-bold" [class.text-green-600]="loadedReviewData.pnLAmount >= 0" [class.text-red-600]="loadedReviewData.pnLAmount < 0">
              {{ loadedReviewData.pnLAmount | vndCurrency }}
            </div>
          </div>
          <div class="bg-white rounded-lg p-3 text-center">
            <div class="text-xs text-gray-500 mb-1">% Lãi/Lỗ</div>
            <div class="text-lg font-bold" [class.text-green-600]="loadedReviewData.pnLPercent >= 0" [class.text-red-600]="loadedReviewData.pnLPercent < 0">
              {{ loadedReviewData.pnLPercent | number:'1.2-2' }}%
            </div>
          </div>
          <div class="bg-white rounded-lg p-3 text-center">
            <div class="text-xs text-gray-500 mb-1">VND/ngày</div>
            <div class="text-lg font-bold" [class.text-green-600]="loadedReviewData.pnLPerDay >= 0" [class.text-red-600]="loadedReviewData.pnLPerDay < 0">
              {{ loadedReviewData.pnLPerDay | vndCurrency }}
            </div>
          </div>
          <div class="bg-white rounded-lg p-3 text-center">
            <div class="text-xs text-gray-500 mb-1">Số ngày nắm giữ</div>
            <div class="text-lg font-bold text-gray-700">{{ loadedReviewData.holdingDays }}</div>
          </div>
        </div>
        <div *ngIf="loadedReviewData.lessonsLearned" class="bg-white rounded-lg p-3">
          <div class="text-xs text-gray-500 mb-1">Bài học rút ra</div>
          <p class="text-sm text-gray-700 whitespace-pre-line">{{ loadedReviewData.lessonsLearned }}</p>
        </div>
      </div>

      <!-- Quick Reference Table -->
      <div class="mt-6 bg-white rounded-lg shadow p-6">
        <h2 class="text-lg font-semibold mb-4">Bảng tham chiếu nhanh</h2>
        <p class="text-sm text-gray-500 mb-3">Số cổ phiếu tối đa theo các mức rủi ro khác nhau (với giá hiện tại)</p>
        <div *ngIf="plan.entryPrice && plan.stopLoss" class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-2 text-left text-xs text-gray-500">% Rủi ro</th>
                <th class="px-4 py-2 text-right text-xs text-gray-500">Tiền rủi ro</th>
                <th class="px-4 py-2 text-right text-xs text-gray-500">Số CP</th>
                <th class="px-4 py-2 text-right text-xs text-gray-500">Giá trị vị thế</th>
                <th class="px-4 py-2 text-right text-xs text-gray-500">% Danh mục</th>
              </tr>
            </thead>
            <tbody class="divide-y">
              <tr *ngFor="let row of quickRefTable" class="hover:bg-gray-50"
                [class.bg-blue-50]="row.riskPercent === riskPercent">
                <td class="px-4 py-2 font-medium">{{ row.riskPercent }}%</td>
                <td class="px-4 py-2 text-right">{{ row.riskAmount | vndCurrency }}</td>
                <td class="px-4 py-2 text-right font-bold">{{ row.shares | number }}</td>
                <td class="px-4 py-2 text-right">{{ row.value | vndCurrency }}</td>
                <td class="px-4 py-2 text-right">{{ row.portfolioPercent | number:'1.1-1' }}%</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="!plan.entryPrice || !plan.stopLoss" class="text-center py-4 text-gray-400">
          Nhập giá vào và stop-loss để xem bảng tham chiếu
        </div>
      </div>
    </div>

    <app-ai-chat-panel [(isOpen)]="showAiPanel" title="AI Tư vấn Kế hoạch" useCase="trade-plan-advisor" [contextData]="{ tradePlanId: aiTradePlanId }"></app-ai-chat-panel>
  `,
  styles: [`
    /* Scenario tree visual flowchart */
    .scenario-tree {
      position: relative;
      padding-left: 0;
    }

    .scenario-node-wrapper {
      position: relative;
      padding-left: 20px;
      padding-bottom: 8px;
    }

    /* Root nodes have no left padding */
    .scenario-tree > .scenario-node-wrapper {
      padding-left: 0;
    }

    /* Vertical line connecting siblings — runs along left edge of children container */
    .scenario-children {
      position: relative;
      margin-left: 16px;
      padding-top: 4px;
    }

    .scenario-children::before {
      content: '';
      position: absolute;
      left: 0;
      top: 0;
      bottom: 16px;
      width: 2px;
      background-color: #c4b5fd; /* violet-300 */
      border-radius: 1px;
    }

    /* Hide the vertical line extension for the last child */
    .scenario-children > .scenario-node-wrapper.is-last::after {
      content: '';
      position: absolute;
      left: -20px;
      top: 20px;
      bottom: 0;
      width: 2px;
      background-color: white;
    }

    /* Horizontal connector from vertical line to card */
    .connector-horizontal {
      position: absolute;
      left: -20px;
      top: 18px;
      width: 18px;
      height: 2px;
      background-color: #c4b5fd; /* violet-300 */
    }

    /* Small dot at the junction */
    .connector-horizontal::before {
      content: '';
      position: absolute;
      right: -3px;
      top: -3px;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background-color: #8b5cf6; /* violet-500 */
      border: 2px solid white;
    }

    /* Node card styling */
    .scenario-card {
      border-width: 2px;
      border-style: solid;
      border-radius: 0.5rem;
      padding: 0.625rem;
      transition: box-shadow 0.15s ease-in-out, border-color 0.15s ease-in-out;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
    }

    .scenario-card:hover {
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }

    /* Collapse toggle rotation */
    .rotate-180 {
      transform: rotate(180deg);
    }
  `]
})
export class TradePlanComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private symbolSubject = new Subject<string>();
  private sizingSubject = new Subject<void>();

  showAiPanel = false;
  aiTradePlanId = '';
  strategies: Strategy[] = [];
  portfolios: PortfolioSummary[] = [];
  selectedStrategy: Strategy | null = null;
  riskProfile: RiskProfile | null = null;

  // Shared trade type utilities
  isBuyTrade = isBuyTrade;
  getTradeTypeDisplay = getTradeTypeDisplay;
  getTradeTypeClass = getTradeTypeClass;

  // Auto-fill stock price
  stockPrice: StockPrice | null = null;
  stockLoading = false;
  stockError = '';

  // SL Method Selector (P4)
  slMethod: 'manual' | 'atr' | 'chandelier' | 'ma_trailing' | 'support' = 'manual';
  slAtrMultiplier = 2;
  slMethods: { value: string; label: string; price: number | null; note: string }[] = [];
  stockAnalysis: TechnicalAnalysis | null = null;

  // Risk enforcement
  riskViolations: string[] = [];
  riskOverrideConfirmed = false;
  portfolioRiskSummary: PortfolioRiskSummary | null = null;

  // Saved plans
  savedPlans: TradePlanDto[] = [];
  filteredSavedPlans: TradePlanDto[] = [];
  savedPlansLoading = false;
  selectedPlanId: string | null = null;
  selectedPlanStatus = '';
  saving = false;
  planFilterTab = 'all';
  planFilterTabs = [
    { key: 'all', label: 'Tất cả' },
    { key: 'Draft', label: 'Nháp' },
    { key: 'Ready', label: 'Sẵn sàng' },
    { key: 'InProgress', label: 'Đang chờ' },
    { key: 'Executed', label: 'Đã thực hiện' },
    { key: 'Reviewed', label: 'Đã review' },
  ];

  plan: TradePlanForm = {
    symbol: '', direction: 'Buy', entryPrice: 0, stopLoss: 0, target: 0,
    quantity: 0, strategyId: '', portfolioId: '', reason: '',
    marketCondition: 'Trending', timeHorizon: 'MediumTerm', confidenceLevel: 5, checklist: [], notes: '',
    entryMode: 'Single', lots: [], exitTargets: []
  };

  rr = 0;
  riskPerShare = 0;
  positionValue = 0;
  potentialProfit = 0;
  potentialLoss = 0;
  stopPercent = 0;
  checklistScore = 0;

  // Position sizing properties
  accountBalance = 100000000;
  riskPercent = 2;
  maxPositionPercent = 20;
  optimalShares = 0;
  optimalPositionValue = 0;
  positionPercent = 0;
  maxRiskAmount = 0;
  withinLimit = true;
  positionWarning = '';
  manualQuantity = false;
  quickRefTable: { riskPercent: number; riskAmount: number; shares: number; value: number; portfolioPercent: number }[] = [];

  // Advanced Position Sizing (P3)
  sizingModels: SizingModelResult[] = [];
  selectedSizingModel = 'fixed_risk';
  recommendedModel = 'fixed_risk';
  loadingSizingModels = false;
  stockAtr: number | null = null;
  stockAtrPercent: number | null = null;

  checklistCategories = ['Phân tích', 'Đa khung thời gian', 'Quản lý rủi ro', 'Tâm lý', 'Xác nhận'];

  // Template management
  templates: TradePlanTemplate[] = [];
  selectedTemplateId = '';
  showSaveTemplate = false;
  newTemplateName = '';
  savingTemplate = false;

  // Scenario Playbook
  exitStrategyMode: 'Simple' | 'Advanced' = 'Simple';
  scenarioNodes: ScenarioNodeForm[] = [];
  scenarioPresets: ScenarioPreset[] = [];
  selectedPresetId = '';
  scenarioHistory: ScenarioHistoryDto[] = [];
  collapsedNodes = new Set<string>();
  private _cachedRootNodes: ScenarioNodeForm[] = [];
  private _cachedChildMap = new Map<string, ScenarioNodeForm[]>();
  private _scenarioVersion = 0;

  // Scenario template save/load
  showSaveScenarioTemplate = false;
  newScenarioTemplateName = '';
  newScenarioTemplateDesc = '';
  savingScenarioTemplate = false;

  // Scenario Suggestion (P0.6)
  selectedTimeHorizon: string = 'Medium';
  loadingSuggestion = false;
  scenarioSuggestion: ScenarioSuggestionDto | null = null;
  selectedSuggestionNodes: Set<string> = new Set();

  // Campaign Review
  showReviewPanel = false;
  reviewPlanTarget: TradePlanDto | null = null;
  reviewPreview: CampaignReviewDto | null = null;
  loadingReviewPreview = false;
  submittingReview = false;
  reviewLessonsLearned = '';
  loadedReviewData: TradePlanDto['reviewData'] | null = null;

  // Order sheet
  showOrderSheet = false;

  // DCA form
  dcaForm = {
    amountPerPeriod: 0,
    frequency: 'monthly' as 'weekly' | 'biweekly' | 'monthly',
    numberOfPeriods: 12,
    startDate: new Date().toISOString().substring(0, 10),
    maxPrice: 0,
    minPrice: 0
  };
  dcaSchedule: { period: number; date: Date; amount: number; cumulative: number }[] = [];

  // Strategy auto-fill hints
  suggestedSlHint = '';
  suggestedTpHint = '';
  slAutoFilled = false;
  tpAutoFilled = false;

  constructor(
    private strategyService: StrategyService,
    private portfolioService: PortfolioService,
    private riskService: RiskService,
    private marketDataService: MarketDataService,
    private templateService: TradePlanTemplateService,
    private tradePlanService: TradePlanService,
    private notification: NotificationService,
    private route: ActivatedRoute
  ) {
    this.initChecklist();
  }

  ngOnInit(): void {
    this.strategyService.getAll().subscribe({ next: d => this.strategies = d });
    this.portfolioService.getAll().subscribe({ next: d => this.portfolios = d });
    this.templateService.getAll().subscribe({ next: d => this.templates = d, error: () => {} });
    this.loadSavedPlans();

    // Pre-fill from query params (e.g. navigated from market-data analysis)
    const qp = this.route.snapshot.queryParams;
    if (qp['symbol']) {
      this.plan.symbol = qp['symbol'].toUpperCase().trim();
      this.onSymbolInput();
    }
    if (qp['entry']) this.plan.entryPrice = +qp['entry'];
    if (qp['sl']) this.plan.stopLoss = +qp['sl'];
    if (qp['tp']) this.plan.target = +qp['tp'];
    if (qp['entry'] || qp['sl'] || qp['tp']) this.recalculate();

    // Auto-fill: debounced symbol lookup
    this.symbolSubject.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(symbol => {
        if (!symbol || symbol.length < 2) {
          this.stockPrice = null;
          this.stockError = '';
          return of(null);
        }
        this.stockLoading = true;
        this.stockError = '';
        return this.marketDataService.getCurrentPrice(symbol).pipe(
          catchError(() => {
            this.stockError = `Không tìm thấy giá cho mã "${symbol}"`;
            this.stockPrice = null;
            this.stockLoading = false;
            return of(null);
          })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(price => {
      this.stockLoading = false;
      if (price) {
        this.stockPrice = price;
        this.stockError = '';
        // Auto-fill entry price if empty
        if (!this.plan.entryPrice || this.plan.entryPrice === 0) {
          this.plan.entryPrice = price.close;
          this.recalculate();
        }
        // Fetch technical analysis for advanced sizing + SL methods
        this.marketDataService.getTechnicalAnalysis(this.plan.symbol).pipe(
          takeUntil(this.destroy$)
        ).subscribe({
          next: (analysis) => {
            this.stockAnalysis = analysis;
            this.stockAtr = analysis.atr14 ?? null;
            this.stockAtrPercent = analysis.atrPercent ?? null;
            this.fetchSizingModels();
            this.calculateSlMethods();
          },
          error: () => {}
        });
      }
    });

    // Debounced refresh of advanced sizing models on price/SL changes
    this.sizingSubject.pipe(
      debounceTime(500),
      takeUntil(this.destroy$)
    ).subscribe(() => this.fetchSizingModels());
  }

  applyTemplate(): void {
    const t = this.templates.find(x => x.id === this.selectedTemplateId);
    if (!t) return;
    if (t.symbol) { this.plan.symbol = t.symbol; this.onSymbolInput(); }
    if (t.direction) this.plan.direction = t.direction;
    if (t.entryPrice) this.plan.entryPrice = t.entryPrice;
    if (t.stopLoss) this.plan.stopLoss = t.stopLoss;
    if (t.target) this.plan.target = t.target;
    if (t.strategyId) { this.plan.strategyId = t.strategyId; this.onStrategyChange(); }
    if (t.marketCondition) this.plan.marketCondition = t.marketCondition;
    if (t.reason) this.plan.reason = t.reason;
    if (t.notes) this.plan.notes = t.notes;
    if (t.positionSize) { this.plan.quantity = t.positionSize; this.manualQuantity = true; }
    this.recalculate();
    this.notification.success('Template', `Đã tải "${t.name}"`);
  }

  saveAsTemplate(): void {
    if (!this.newTemplateName.trim()) return;
    this.savingTemplate = true;
    this.templateService.create({
      name: this.newTemplateName.trim(),
      symbol: this.plan.symbol || undefined,
      direction: this.plan.direction,
      entryPrice: this.plan.entryPrice || undefined,
      stopLoss: this.plan.stopLoss || undefined,
      target: this.plan.target || undefined,
      strategyId: this.plan.strategyId || undefined,
      marketCondition: this.plan.marketCondition,
      reason: this.plan.reason || undefined,
      notes: this.plan.notes || undefined,
      positionSize: this.plan.quantity || this.optimalShares || undefined,
    }).subscribe({
      next: (saved) => {
        this.templates = [saved, ...this.templates];
        this.savingTemplate = false;
        this.showSaveTemplate = false;
        this.newTemplateName = '';
        this.notification.success('Template', `Đã lưu "${saved.name}"`);
      },
      error: () => {
        this.savingTemplate = false;
        this.notification.error('Lỗi', 'Không thể lưu template');
      }
    });
  }

  deleteTemplate(id: string): void {
    this.templateService.delete(id).subscribe({
      next: () => {
        this.templates = this.templates.filter(t => t.id !== id);
        if (this.selectedTemplateId === id) this.selectedTemplateId = '';
        this.notification.success('Template', 'Đã xoá template');
      },
      error: () => this.notification.error('Lỗi', 'Không thể xoá template')
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSymbolInput(): void {
    this.symbolSubject.next(this.plan.symbol);
    this.riskOverrideConfirmed = false;
  }

  applyStockPrice(): void {
    if (this.stockPrice) {
      this.plan.entryPrice = this.stockPrice.close;
      this.recalculate();
    }
  }

  initChecklist(): void {
    const tf = this.selectedStrategy?.timeFrame || 'Swing';
    this.plan.checklist = this.generateDynamicChecklist(tf);
    this.updateChecklistScore();
  }

  private generateDynamicChecklist(timeFrame: string): ChecklistItem[] {
    const items: ChecklistItem[] = [];

    // ─── Phân tích (strategy-specific indicators) ─────────────
    if (timeFrame === 'Scalping') {
      items.push(
        { label: 'VWAP xác nhận hướng giao dịch', category: 'Phân tích', checked: false, critical: true, hint: 'Giá trên VWAP = mua, dưới VWAP = bán', weight: 3 },
        { label: 'Stochastic < 20 hoặc > 80 (timing)', category: 'Phân tích', checked: false, critical: false, hint: 'Quá bán/quá mua cho điểm vào', weight: 2 },
        { label: 'Volume đột biến xác nhận', category: 'Phân tích', checked: false, critical: true, hint: 'Volume > 2x trung bình', weight: 3 },
        { label: 'Spread mua-bán đủ nhỏ (< 0.3%)', category: 'Phân tích', checked: false, critical: false, hint: 'Thanh khoản cao', weight: 1 },
      );
    } else if (timeFrame === 'DayTrading') {
      items.push(
        { label: 'EMA(20) > EMA(50) xác nhận xu hướng', category: 'Phân tích', checked: false, critical: true, hint: 'Xu hướng ngắn hạn rõ ràng', weight: 3 },
        { label: 'RSI pullback về 40-50 (uptrend) hoặc 50-60 (downtrend)', category: 'Phân tích', checked: false, critical: false, hint: 'Timing vào lệnh', weight: 2 },
        { label: 'MACD cắt signal line đúng hướng', category: 'Phân tích', checked: false, critical: false, hint: 'Xác nhận momentum', weight: 2 },
        { label: 'Bollinger %B xác nhận vùng vào', category: 'Phân tích', checked: false, critical: false, hint: '%B < 0.2 (mua) hoặc > 0.8 (bán)', weight: 1 },
        { label: 'Volume phiên > 1.3x trung bình', category: 'Phân tích', checked: false, critical: true, hint: 'Dòng tiền ủng hộ', weight: 2 },
      );
    } else if (timeFrame === 'Swing') {
      items.push(
        { label: 'ADX > 25 — thị trường có xu hướng', category: 'Phân tích', checked: false, critical: true, hint: 'Không giao dịch khi sideway', weight: 3 },
        { label: 'Fibonacci retracement xác nhận vùng vào', category: 'Phân tích', checked: false, critical: false, hint: 'Giá tại 38.2%, 50%, hoặc 61.8%', weight: 2 },
        { label: 'RSI/MACD xác nhận momentum', category: 'Phân tích', checked: false, critical: false, hint: 'RSI chưa quá mua, MACD đúng hướng', weight: 2 },
        { label: 'OBV rising — dòng tiền ủng hộ', category: 'Phân tích', checked: false, critical: true, hint: 'Smart money cùng hướng', weight: 2 },
      );
    } else { // Position
      items.push(
        { label: 'SMA(50) trên SMA(200) trên khung tuần', category: 'Phân tích', checked: false, critical: true, hint: 'Golden Cross weekly', weight: 3 },
        { label: 'ADX > 25 trên khung tuần', category: 'Phân tích', checked: false, critical: true, hint: 'Xu hướng dài hạn đủ mạnh', weight: 3 },
        { label: 'MACD weekly cắt signal đúng hướng', category: 'Phân tích', checked: false, critical: false, hint: 'Momentum dài hạn', weight: 2 },
        { label: 'Cơ bản tốt: ROE > 15%, EPS tăng trưởng', category: 'Phân tích', checked: false, critical: false, hint: 'Kết hợp phân tích cơ bản', weight: 2 },
      );
    }

    // ─── Multi-Timeframe Gate (strategy-dependent) ────────────
    if (timeFrame === 'DayTrading') {
      items.push(
        { label: '⏱ Xu hướng Daily ủng hộ hướng giao dịch', category: 'Đa khung thời gian', checked: false, critical: true, hint: 'Khung lớn hơn phải xác nhận', weight: 3 },
      );
    } else if (timeFrame === 'Swing') {
      items.push(
        { label: '⏱ Xu hướng Weekly ủng hộ hướng giao dịch', category: 'Đa khung thời gian', checked: false, critical: true, hint: 'Weekly trend phải cùng hướng', weight: 3 },
      );
    } else if (timeFrame === 'Position') {
      items.push(
        { label: '⏱ Xu hướng Monthly ủng hộ hướng giao dịch', category: 'Đa khung thời gian', checked: false, critical: true, hint: 'Monthly trend phải cùng hướng', weight: 3 },
      );
    }
    // Scalping: no multi-TF gate (too fast)

    // ─── Quản lý rủi ro (common + strategy-specific) ─────────
    items.push(
      { label: 'Stop-loss đã được đặt', category: 'Quản lý rủi ro', checked: false, critical: true, hint: 'Biết chính xác điểm cắt lỗ', weight: 3 },
      { label: 'R:R ratio >= 2:1', category: 'Quản lý rủi ro', checked: false, critical: true, hint: 'Lời tiềm năng gấp 2 lần rủi ro', weight: 3 },
      { label: 'Vị thế trong giới hạn position sizing', category: 'Quản lý rủi ro', checked: false, critical: true, hint: 'Không vượt % tối đa danh mục', weight: 2 },
    );

    if (timeFrame === 'Scalping') {
      items.push(
        { label: 'Tổng loss tối đa/ngày chưa vượt 2%', category: 'Quản lý rủi ro', checked: false, critical: true, hint: 'Dừng giao dịch nếu vượt', weight: 3 },
      );
    } else {
      items.push(
        { label: 'Tổng rủi ro danh mục chưa vượt giới hạn', category: 'Quản lý rủi ro', checked: false, critical: false, hint: 'Tính cả vị thế mới', weight: 1 },
      );
    }

    // ─── Tâm lý (common + strategy-specific) ─────────────────
    items.push(
      { label: 'Không đang FOMO hoặc sợ hãi', category: 'Tâm lý', checked: false, critical: false, hint: 'Bình tĩnh, có kế hoạch rõ', weight: 1 },
      { label: 'Chấp nhận mất số tiền rủi ro này', category: 'Tâm lý', checked: false, critical: true, hint: 'Thoải mái với mức lỗ tối đa', weight: 2 },
      { label: 'Không revenge trading', category: 'Tâm lý', checked: false, critical: false, hint: 'Không có giao dịch lỗ gần đây', weight: 1 },
    );

    if (timeFrame === 'Swing' || timeFrame === 'Position') {
      items.push(
        { label: 'Kiên nhẫn chờ pullback, không vào đuổi giá', category: 'Tâm lý', checked: false, critical: false, hint: 'Chờ giá về vùng vào, không FOMO', weight: 1 },
      );
    }

    // ─── Xác nhận (common) ───────────────────────────────────
    items.push(
      { label: 'Đã ghi nhật ký giao dịch', category: 'Xác nhận', checked: false, critical: false, hint: 'Entry reason, market context', weight: 1 },
      { label: 'Đã xác nhận lại giá vào/SL/TP', category: 'Xác nhận', checked: false, critical: true, hint: 'Double check các mức giá', weight: 2 },
    );

    return items;
  }

  onStrategyChange(): void {
    this.selectedStrategy = this.strategies.find(s => s.id === this.plan.strategyId) || null;
    this.slAutoFilled = false;
    this.tpAutoFilled = false;
    this.suggestedSlHint = '';
    this.suggestedTpHint = '';

    const s = this.selectedStrategy;
    if (!s) return;

    // Regenerate checklist based on strategy timeFrame (P6)
    this.plan.checklist = this.generateDynamicChecklist(s.timeFrame);
    this.updateChecklistScore();

    const entry = this.plan.entryPrice;
    const isBuy = isBuyTrade(this.plan.direction);

    if (s.suggestedSlPercent) {
      const slValue = isBuy
        ? entry * (1 - s.suggestedSlPercent / 100)
        : entry * (1 + s.suggestedSlPercent / 100);
      this.suggestedSlHint = entry > 0 ? `Gợi ý: ${Math.round(slValue).toLocaleString('vi-VN')}` : `Gợi ý: -${s.suggestedSlPercent}%`;
      if (entry > 0 && !this.plan.stopLoss) {
        this.plan.stopLoss = Math.round(slValue);
        this.slAutoFilled = true;
      }
    }

    if (s.suggestedRrRatio && this.plan.stopLoss) {
      const risk = Math.abs(entry - this.plan.stopLoss);
      const tpValue = isBuy
        ? entry + risk * s.suggestedRrRatio
        : entry - risk * s.suggestedRrRatio;
      this.suggestedTpHint = entry > 0 ? `Gợi ý: ${Math.round(tpValue).toLocaleString('vi-VN')}` : `Gợi ý: R:R ${s.suggestedRrRatio}:1`;
      if (entry > 0 && !this.plan.target) {
        this.plan.target = Math.round(tpValue);
        this.tpAutoFilled = true;
      }
    }

    // Auto-select SL method from strategy (P5)
    if (s.suggestedSlMethod && s.suggestedSlMethod !== 'manual') {
      this.slMethod = s.suggestedSlMethod as any;
      // If strategy specifies ATR multiplier and analysis is loaded, apply it
      if (s.suggestedSlMethod === 'atr' && this.stockAnalysis?.atr14 && entry > 0) {
        const atrSl = isBuy
          ? Math.round(entry - (this.slAtrMultiplier) * this.stockAnalysis.atr14)
          : Math.round(entry + (this.slAtrMultiplier) * this.stockAnalysis.atr14);
        if (atrSl > 0) {
          this.plan.stopLoss = atrSl;
          this.slAutoFilled = true;
        }
      }
    }

    if (this.slAutoFilled || this.tpAutoFilled) this.recalculate();
  }

  onPortfolioChange(): void {
    if (!this.plan.portfolioId) { this.riskProfile = null; this.portfolioRiskSummary = null; return; }
    const portfolio = this.portfolios.find(p => p.id === this.plan.portfolioId);
    if (portfolio) this.accountBalance = portfolio.initialCapital;

    this.riskService.getRiskProfile(this.plan.portfolioId).subscribe({
      next: (profile) => {
        this.riskProfile = profile;
        this.maxPositionPercent = profile.maxPositionSizePercent;
        this.riskPercent = profile.maxPortfolioRiskPercent;
        this.recalculate();
      },
      error: () => this.riskProfile = null
    });

    this.riskService.getPortfolioRiskSummary(this.plan.portfolioId).subscribe({
      next: (summary) => { this.portfolioRiskSummary = summary; this.checkRiskViolations(); },
      error: () => this.portfolioRiskSummary = null
    });
  }

  recalculate(): void {
    const { entryPrice, stopLoss, target } = this.plan;
    this.riskPerShare = Math.abs(entryPrice - stopLoss);
    this.rr = this.riskPerShare > 0 && target > 0 ? Math.abs(target - entryPrice) / this.riskPerShare : 0;
    this.stopPercent = entryPrice > 0 ? (this.riskPerShare / entryPrice) * 100 : 0;

    // Keep SL method pills in sync with entry price changes
    this.calculateSlMethods();

    // Position sizing calculation
    this.calculatePositionSizing();
    this.sizingSubject.next();

    // Use optimal shares when quantity is not manually set
    const effectiveQuantity = this.manualQuantity && this.plan.quantity > 0
      ? this.plan.quantity : this.optimalShares;

    this.positionValue = effectiveQuantity * entryPrice;
    this.potentialProfit = target > 0 ? effectiveQuantity * Math.abs(target - entryPrice) : 0;
    this.potentialLoss = effectiveQuantity * this.riskPerShare;

    // Auto-check R:R item
    const rrItem = this.plan.checklist.find(c => c.label.includes('R:R ratio'));
    if (rrItem) rrItem.checked = this.rr >= 2;

    // Auto-check stop-loss item
    const slItem = this.plan.checklist.find(c => c.label.includes('Stop-loss'));
    if (slItem) slItem.checked = stopLoss > 0 && stopLoss !== entryPrice;

    // Auto-check position sizing limit
    const posItem = this.plan.checklist.find(c => c.label.includes('position sizing'));
    if (posItem) posItem.checked = this.withinLimit && this.optimalShares > 0;

    this.checkRiskViolations();
    this.updateChecklistScore();
  }

  calculatePositionSizing(): void {
    const { entryPrice, stopLoss } = this.plan;
    if (!entryPrice || !stopLoss || entryPrice <= 0 || this.riskPerShare === 0) {
      this.optimalShares = 0;
      this.optimalPositionValue = 0;
      this.positionPercent = 0;
      this.maxRiskAmount = 0;
      this.withinLimit = true;
      this.positionWarning = '';
      this.quickRefTable = [];
      return;
    }

    this.maxRiskAmount = this.accountBalance * (this.riskPercent / 100);
    let optimal = Math.floor(this.maxRiskAmount / this.riskPerShare);
    optimal = Math.floor(optimal / 100) * 100;
    if (optimal < 100) optimal = 100;
    this.optimalShares = optimal;

    this.optimalPositionValue = this.optimalShares * entryPrice;
    this.positionPercent = (this.optimalPositionValue / this.accountBalance) * 100;
    const maxPositionValue = this.accountBalance * (this.maxPositionPercent / 100);

    this.positionWarning = '';
    if (this.optimalPositionValue > maxPositionValue) {
      const cappedShares = Math.floor(maxPositionValue / entryPrice / 100) * 100;
      this.positionWarning = `Vị thế (${this.positionPercent.toFixed(1)}%) vượt giới hạn ${this.maxPositionPercent}%. Nên giảm xuống ${cappedShares} cổ phiếu.`;
    }

    this.withinLimit = this.positionPercent <= this.maxPositionPercent;

    // Build quick ref table
    this.quickRefTable = [0.5, 1, 1.5, 2, 3, 5].map(pct => {
      const risk = this.accountBalance * (pct / 100);
      let shares = Math.floor(risk / this.riskPerShare / 100) * 100;
      if (shares < 100) shares = 100;
      const value = shares * entryPrice;
      return { riskPercent: pct, riskAmount: risk, shares, value, portfolioPercent: (value / this.accountBalance) * 100 };
    });
  }

  fetchSizingModels(): void {
    const { entryPrice, stopLoss } = this.plan;
    if (!entryPrice || !stopLoss || entryPrice <= 0 || this.riskPerShare === 0) {
      this.sizingModels = [];
      return;
    }

    this.loadingSizingModels = true;
    const request: PositionSizingRequest = {
      accountBalance: this.accountBalance,
      entryPrice,
      stopLoss,
      riskPercent: this.riskPercent,
      maxPositionPercent: this.maxPositionPercent,
      atr: this.stockAtr ?? undefined,
      atrMultiplier: 2,
      atrPercent: this.stockAtrPercent ?? undefined
    };

    this.riskService.calculatePositionSizing(request).subscribe({
      next: (result) => {
        this.sizingModels = result.models;
        this.recommendedModel = result.recommendedModel;
        this.loadingSizingModels = false;
      },
      error: () => {
        this.sizingModels = [];
        this.loadingSizingModels = false;
      }
    });
  }

  calculateSlMethods(): void {
    const entry = this.plan.entryPrice;
    const a = this.stockAnalysis;
    if (!entry || entry <= 0) {
      this.slMethods = [];
      return;
    }

    const isBuy = isBuyTrade(this.plan.direction);
    const methods: { value: string; label: string; price: number | null; note: string }[] = [
      { value: 'manual', label: 'Cố định (nhập tay)', price: this.plan.stopLoss || null, note: '' }
    ];

    // ATR Stop Loss: Entry ∓ k × ATR (buy: below, sell: above)
    if (a?.atr14) {
      const atrSl = isBuy
        ? Math.round(entry - this.slAtrMultiplier * a.atr14)
        : Math.round(entry + this.slAtrMultiplier * a.atr14);
      methods.push({
        value: 'atr', label: `ATR (${this.slAtrMultiplier}×)`,
        price: atrSl,
        note: `Entry ${isBuy ? '-' : '+'} ${this.slAtrMultiplier}×${a.atr14.toLocaleString()}đ`
      });
    }

    // Chandelier Exit: HH22 - 3×ATR (buy) or LL22 + 3×ATR (sell)
    if (a?.atr14) {
      if (isBuy && a?.highestHigh22) {
        const chandelier = Math.round(a.highestHigh22 - 3 * a.atr14);
        methods.push({
          value: 'chandelier', label: 'Chandelier Exit',
          price: chandelier,
          note: `HH22(${a.highestHigh22.toLocaleString()}) - 3×ATR`
        });
      } else if (!isBuy && a?.lowestLow22) {
        const chandelier = Math.round(a.lowestLow22 + 3 * a.atr14);
        methods.push({
          value: 'chandelier', label: 'Chandelier Exit',
          price: chandelier,
          note: `LL22(${a.lowestLow22.toLocaleString()}) + 3×ATR`
        });
      }
    }

    // MA Trailing: EMA(21) as SL floor
    if (a?.ema21) {
      methods.push({
        value: 'ma_trailing', label: 'MA Trailing (EMA21)',
        price: Math.round(a.ema21),
        note: `EMA(21) = ${a.ema21.toLocaleString()}đ`
      });
    }

    // Nearest support (for buy) or nearest resistance (for sell)
    if (isBuy && a?.supportLevels?.length) {
      methods.push({
        value: 'support', label: 'Hỗ trợ gần nhất',
        price: Math.round(a.supportLevels[0]),
        note: `Swing low = ${a.supportLevels[0].toLocaleString()}đ`
      });
    } else if (!isBuy && a?.resistanceLevels?.length) {
      methods.push({
        value: 'support', label: 'Kháng cự gần nhất',
        price: Math.round(a.resistanceLevels[0]),
        note: `Swing high = ${a.resistanceLevels[0].toLocaleString()}đ`
      });
    }

    this.slMethods = methods;
  }

  applySlMethod(method: string): void {
    this.slMethod = method as any;
    const m = this.slMethods.find(s => s.value === method);
    if (method !== 'manual' && m?.price && m.price > 0) {
      this.plan.stopLoss = m.price;
      this.slAutoFilled = true;
    } else {
      this.slAutoFilled = false;
    }
    this.recalculate();
  }

  onSlAtrMultiplierChange(): void {
    this.calculateSlMethods();
    if (this.slMethod === 'atr') {
      this.applySlMethod('atr');
    }
  }

  applySizingModel(model: SizingModelResult): void {
    this.selectedSizingModel = model.model;
    this.plan.quantity = model.shares;
    this.manualQuantity = true;
    this.recalculate();
  }

  onQuantityManualChange(): void {
    this.manualQuantity = this.plan.quantity > 0;
    this.recalculate();
  }

  getLotCount(): number {
    return Math.floor(this.optimalShares / 100);
  }

  getChecklistByCategory(cat: string): ChecklistItem[] {
    return this.plan.checklist.filter(c => c.category === cat);
  }

  updateChecklistScore(): void {
    const totalWeight = this.plan.checklist.reduce((sum, c) => sum + c.weight, 0);
    const checkedWeight = this.plan.checklist.filter(c => c.checked).reduce((sum, c) => sum + c.weight, 0);
    this.checklistScore = totalWeight > 0 ? Math.round((checkedWeight / totalWeight) * 100) : 0;
  }

  get canTrade(): boolean {
    const criticalItems = this.plan.checklist.filter(c => c.critical);
    const criticalOk = criticalItems.every(c => c.checked);
    const scoreOk = this.checklistScore >= 70;
    const riskOk = this.riskViolations.length === 0 || this.riskOverrideConfirmed;
    return criticalOk && scoreOk && riskOk;
  }

  getMissingCritical(): string {
    const missing = this.plan.checklist.filter(c => c.critical && !c.checked);
    const parts: string[] = [];
    if (missing.length > 0) parts.push(`Còn ${missing.length} điều kiện bắt buộc (●3)`);
    if (this.checklistScore < 70) parts.push(`Điểm ${this.checklistScore}% < 70% tối thiểu`);
    if (this.riskViolations.length > 0 && !this.riskOverrideConfirmed) {
      parts.push(`${this.riskViolations.length} vi phạm Risk Profile`);
    }
    return parts.join(' · ');
  }

  checkRiskViolations(): void {
    const violations: string[] = [];
    const { entryPrice, stopLoss, target } = this.plan;

    if (this.riskProfile && entryPrice > 0) {
      // Check R:R ratio
      if (this.rr > 0 && this.rr < this.riskProfile.defaultRiskRewardRatio) {
        violations.push(`R:R (1:${this.rr.toFixed(1)}) thấp hơn mức yêu cầu (1:${this.riskProfile.defaultRiskRewardRatio})`);
      }

      // Check position size vs max position
      if (this.positionPercent > this.riskProfile.maxPositionSizePercent) {
        violations.push(`Vị thế (${this.positionPercent.toFixed(1)}%) vượt giới hạn ${this.riskProfile.maxPositionSizePercent}% danh mục`);
      }

      // Check stop loss distance vs max risk
      if (stopLoss > 0 && this.stopPercent > this.riskProfile.maxPortfolioRiskPercent * 2) {
        violations.push(`Khoảng cách SL (${this.stopPercent.toFixed(1)}%) quá lớn so với khẩu vị rủi ro`);
      }
    }

    // Check concentration risk from existing portfolio
    if (this.portfolioRiskSummary && this.plan.symbol) {
      const existingPos = this.portfolioRiskSummary.positions.find(
        p => p.symbol.toUpperCase() === this.plan.symbol.toUpperCase()
      );
      if (existingPos && existingPos.positionSizePercent > 30) {
        violations.push(`${this.plan.symbol} đã chiếm ${existingPos.positionSizePercent.toFixed(1)}% danh mục — rủi ro tập trung cao`);
      }

      // Check if largest position > 30%
      if (this.portfolioRiskSummary.largestPositionPercent > 30 && !existingPos) {
        violations.push(`Danh mục đã có vị thế chiếm ${this.portfolioRiskSummary.largestPositionPercent.toFixed(1)}% — cân nhắc đa dạng hóa`);
      }
    }

    this.riskViolations = violations;
    if (violations.length === 0) this.riskOverrideConfirmed = false;
  }

  // --- Saved Plans ---

  loadSavedPlans(): void {
    this.savedPlansLoading = true;
    this.tradePlanService.getAll().subscribe({
      next: (plans) => {
        this.savedPlans = plans.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        this.filterSavedPlans();
        this.savedPlansLoading = false;
      },
      error: () => { this.savedPlansLoading = false; }
    });
  }

  filterSavedPlans(): void {
    this.filteredSavedPlans = this.planFilterTab === 'all'
      ? this.savedPlans
      : this.savedPlans.filter(p => p.status === this.planFilterTab);
  }

  loadPlan(sp: TradePlanDto): void {
    this.selectedPlanId = sp.id;
    this.selectedPlanStatus = sp.status;
    this.plan.symbol = sp.symbol;
    this.plan.direction = sp.direction;
    this.plan.entryPrice = sp.entryPrice;
    this.plan.stopLoss = sp.stopLoss;
    this.plan.target = sp.target;
    this.plan.quantity = sp.quantity;
    this.plan.strategyId = sp.strategyId || '';
    this.plan.portfolioId = sp.portfolioId || '';
    this.plan.reason = sp.reason || '';
    this.plan.marketCondition = sp.marketCondition || 'Trending';
    this.plan.timeHorizon = sp.timeHorizon || 'MediumTerm';
    this.plan.confidenceLevel = sp.confidenceLevel;
    this.loadedReviewData = sp.reviewData || null;
    this.plan.notes = sp.notes || '';
    this.plan.entryMode = sp.entryMode || 'Single';
    this.plan.lots = (sp.lots || []).map((l, i) => ({
      lotNumber: l.lotNumber ?? (i + 1),
      plannedPrice: l.plannedPrice,
      plannedQuantity: l.plannedQuantity,
      allocationPercent: l.allocationPercent ?? 0,
      label: l.label ?? ''
    }));
    this.plan.exitTargets = (sp.exitTargets || []).map((e, i) => ({
      level: e.level ?? (i + 1),
      actionType: e.actionType,
      price: e.price,
      percentOfPosition: e.percentOfPosition ?? 0,
      label: e.label ?? ''
    }));
    // Scenario Playbook
    this.exitStrategyMode = (sp.exitStrategyMode === 'Advanced') ? 'Advanced' : 'Simple';
    this.scenarioNodes = (sp.scenarioNodes || []).map(n => ({
      nodeId: n.nodeId,
      parentId: n.parentId,
      order: n.order,
      label: n.label,
      conditionType: n.conditionType,
      conditionValue: n.conditionValue || 0,
      conditionNote: n.conditionNote || '',
      actionType: n.actionType,
      actionValue: n.actionValue || 0,
      trailingStopConfig: n.trailingStopConfig
        ? { method: n.trailingStopConfig.method, trailValue: n.trailingStopConfig.trailValue, activationPrice: n.trailingStopConfig.activationPrice || 0, stepSize: n.trailingStopConfig.stepSize || 0 }
        : { method: 'Percentage', trailValue: 5, activationPrice: 0, stepSize: 0 },
      status: n.status
    }));
    this.invalidateScenarioCache();
    // Load scenario history for Advanced in-progress plans
    if (this.exitStrategyMode === 'Advanced' && sp.id && (sp.status === 'InProgress' || sp.status === 'Executed')) {
      this.loadScenarioHistory(sp.id);
    } else {
      this.scenarioHistory = [];
    }
    if (sp.checklist && sp.checklist.length > 0) {
      this.plan.checklist = sp.checklist.map(c => ({
        label: c.label, category: c.category, checked: c.checked, critical: c.critical, hint: c.hint
      }));
    }
    this.manualQuantity = sp.quantity > 0;
    if (sp.strategyId) this.onStrategyChange();
    if (sp.portfolioId) this.onPortfolioChange();
    if (sp.symbol) this.onSymbolInput();
    this.recalculate();
    this.updateChecklistScore();
  }

  resetForm(): void {
    this.selectedPlanId = null;
    this.selectedPlanStatus = '';
    this.plan = {
      symbol: '', direction: 'Buy', entryPrice: 0, stopLoss: 0, target: 0,
      quantity: 0, strategyId: '', portfolioId: '', reason: '',
      marketCondition: 'Trending', timeHorizon: 'MediumTerm', confidenceLevel: 5, checklist: [], notes: '',
      entryMode: 'Single', lots: [], exitTargets: []
    };
    this.showOrderSheet = false;
    this.showReviewPanel = false;
    this.reviewPlanTarget = null;
    this.reviewPreview = null;
    this.reviewLessonsLearned = '';
    this.loadedReviewData = null;
    this.manualQuantity = false;
    this.exitStrategyMode = 'Simple';
    this.scenarioNodes = [];
    this.selectedPresetId = '';
    this.scenarioHistory = [];
    this.scenarioSuggestion = null;
    this.selectedSuggestionNodes = new Set();
    this.loadingSuggestion = false;
    this.invalidateScenarioCache();
    this.selectedStrategy = null;
    this.riskProfile = null;
    this.stockPrice = null;
    this.stockError = '';
    this.initChecklist();
    this.recalculate();
  }

  saveDraft(): void {
    this.savePlan('Draft');
  }

  saveAndReady(): void {
    this.savePlan('Ready');
  }

  private savePlan(status: string): void {
    this.saving = true;
    const checklist = this.plan.checklist.map(c => ({
      label: c.label, category: c.category, checked: c.checked, critical: c.critical, hint: c.hint
    }));

    if (this.selectedPlanId) {
      // Update existing plan
      this.tradePlanService.update(this.selectedPlanId, {
        portfolioId: this.plan.portfolioId || undefined,
        symbol: this.plan.symbol.toUpperCase().trim(),
        direction: this.plan.direction,
        entryPrice: this.plan.entryPrice,
        stopLoss: this.plan.stopLoss,
        target: this.plan.target,
        quantity: this.plan.quantity || this.optimalShares,
        strategyId: this.plan.strategyId || undefined,
        marketCondition: this.plan.marketCondition,
        timeHorizon: this.plan.timeHorizon || undefined,
        reason: this.plan.reason || undefined,
        notes: this.plan.notes || undefined,
        riskPercent: this.riskPercent,
        accountBalance: this.accountBalance,
        riskRewardRatio: this.rr,
        confidenceLevel: this.plan.confidenceLevel,
        checklist,
        entryMode: this.plan.entryMode !== 'Single' ? this.plan.entryMode : undefined,
        lots: this.plan.lots.length > 0 ? this.plan.lots.map(l => ({
          lotNumber: l.lotNumber, plannedPrice: l.plannedPrice,
          plannedQuantity: l.plannedQuantity, allocationPercent: l.allocationPercent,
          label: l.label, status: 'Pending'
        })) : undefined,
        exitTargets: this.plan.exitTargets.length > 0 ? this.plan.exitTargets.map(e => ({
          level: e.level, actionType: e.actionType, price: e.price,
          percentOfPosition: e.percentOfPosition, label: e.label, isTriggered: false
        })) : undefined,
        exitStrategyMode: this.exitStrategyMode,
        scenarioNodes: this.buildScenarioPayload()
      }).subscribe({
        next: () => {
          // If status changed (e.g., Draft → Ready), update status separately
          if (status !== this.selectedPlanStatus && status === 'Ready') {
            this.tradePlanService.updateStatus(this.selectedPlanId!, { status: 'ready' }).subscribe({
              next: () => {
                this.saving = false;
                this.notification.success('Kế hoạch', 'Đã cập nhật & sẵn sàng');
                this.loadSavedPlans();
              },
              error: () => {
                this.saving = false;
                this.notification.error('Lỗi', 'Cập nhật thành công nhưng không chuyển trạng thái được');
                this.loadSavedPlans();
              }
            });
          } else {
            this.saving = false;
            this.notification.success('Kế hoạch', 'Đã cập nhật');
            this.loadSavedPlans();
          }
        },
        error: () => {
          this.saving = false;
          this.notification.error('Lỗi', 'Không thể cập nhật kế hoạch');
        }
      });
    } else {
      // Create new plan
      this.tradePlanService.create({
        portfolioId: this.plan.portfolioId || undefined,
        symbol: this.plan.symbol.toUpperCase().trim(),
        direction: this.plan.direction,
        entryPrice: this.plan.entryPrice,
        stopLoss: this.plan.stopLoss,
        target: this.plan.target,
        quantity: this.plan.quantity || this.optimalShares,
        strategyId: this.plan.strategyId || undefined,
        marketCondition: this.plan.marketCondition,
        timeHorizon: this.plan.timeHorizon || undefined,
        reason: this.plan.reason || undefined,
        notes: this.plan.notes || undefined,
        riskPercent: this.riskPercent,
        accountBalance: this.accountBalance,
        riskRewardRatio: this.rr,
        confidenceLevel: this.plan.confidenceLevel,
        checklist,
        status: status === 'Ready' ? 'Ready' : undefined,
        entryMode: this.plan.entryMode !== 'Single' ? this.plan.entryMode : undefined,
        lots: this.plan.lots.length > 0 ? this.plan.lots.map(l => ({
          lotNumber: l.lotNumber, plannedPrice: l.plannedPrice,
          plannedQuantity: l.plannedQuantity, allocationPercent: l.allocationPercent,
          label: l.label, status: 'Pending'
        })) : undefined,
        exitTargets: this.plan.exitTargets.length > 0 ? this.plan.exitTargets.map(e => ({
          level: e.level, actionType: e.actionType, price: e.price,
          percentOfPosition: e.percentOfPosition, label: e.label, isTriggered: false
        })) : undefined,
        exitStrategyMode: this.exitStrategyMode,
        scenarioNodes: this.buildScenarioPayload()
      }).subscribe({
        next: (res) => {
          this.saving = false;
          this.selectedPlanId = res.id;
          this.selectedPlanStatus = status === 'Ready' ? 'Ready' : 'Draft';
          this.notification.success('Kế hoạch', status === 'Ready' ? 'Đã tạo & sẵn sàng' : 'Đã lưu nháp');
          this.loadSavedPlans();
        },
        error: () => {
          this.saving = false;
          this.notification.error('Lỗi', 'Không thể lưu kế hoạch');
        }
      });
    }
  }

  deletePlan(sp: TradePlanDto): void {
    if (!confirm(`Xoá kế hoạch ${sp.symbol}?`)) return;
    this.tradePlanService.delete(sp.id).subscribe({
      next: () => {
        if (this.selectedPlanId === sp.id) this.resetForm();
        this.notification.success('Kế hoạch', 'Đã xoá');
        this.loadSavedPlans();
      },
      error: () => this.notification.error('Lỗi', 'Không thể xoá')
    });
  }

  markReady(sp: TradePlanDto): void {
    this.tradePlanService.updateStatus(sp.id, { status: 'ready' }).subscribe({
      next: () => { this.notification.success('Kế hoạch', `${sp.symbol} sẵn sàng`); this.loadSavedPlans(); },
      error: () => this.notification.error('Lỗi', 'Không thể chuyển trạng thái')
    });
  }

  openCampaignReview(sp: TradePlanDto): void {
    this.reviewPlanTarget = sp;
    this.showReviewPanel = true;
    this.reviewPreview = null;
    this.reviewLessonsLearned = '';
    this.loadingReviewPreview = true;
    this.tradePlanService.previewReview(sp.id).subscribe({
      next: (preview) => {
        this.reviewPreview = preview;
        this.loadingReviewPreview = false;
      },
      error: () => {
        this.loadingReviewPreview = false;
        this.notification.error('Lỗi', 'Không thể tải dữ liệu review');
      }
    });
  }

  closeCampaignReviewPanel(): void {
    this.showReviewPanel = false;
    this.reviewPlanTarget = null;
    this.reviewPreview = null;
    this.reviewLessonsLearned = '';
  }

  submitCampaignReview(): void {
    if (!this.reviewPlanTarget) return;
    this.submittingReview = true;
    this.tradePlanService.submitReview(this.reviewPlanTarget.id, {
      lessonsLearned: this.reviewLessonsLearned.trim() || undefined
    }).subscribe({
      next: () => {
        this.submittingReview = false;
        this.notification.success('Chiến dịch', `${this.reviewPlanTarget!.symbol} đã đóng thành công`);
        this.closeCampaignReviewPanel();
        this.loadSavedPlans();
      },
      error: () => {
        this.submittingReview = false;
        this.notification.error('Lỗi', 'Không thể đóng chiến dịch');
      }
    });
  }

  cancelPlan(sp: TradePlanDto): void {
    if (!confirm(`Huỷ kế hoạch ${sp.symbol}?`)) return;
    this.tradePlanService.updateStatus(sp.id, { status: 'cancelled' }).subscribe({
      next: () => {
        if (this.selectedPlanId === sp.id) this.resetForm();
        this.notification.success('Kế hoạch', `${sp.symbol} đã huỷ`);
        this.loadSavedPlans();
      },
      error: () => this.notification.error('Lỗi', 'Không thể huỷ')
    });
  }

  getRR(sp: TradePlanDto): number {
    const risk = Math.abs(sp.entryPrice - sp.stopLoss);
    return risk > 0 ? Math.abs(sp.target - sp.entryPrice) / risk : 0;
  }

  getStatusClass(status: string): Record<string, boolean> {
    return {
      'bg-gray-100 text-gray-600': status === 'Draft',
      'bg-emerald-100 text-emerald-700': status === 'Ready',
      'bg-blue-100 text-blue-700': status === 'InProgress',
      'bg-violet-100 text-violet-700': status === 'Executed',
      'bg-amber-100 text-amber-700': status === 'Reviewed',
      'bg-red-100 text-red-600': status === 'Cancelled',
    };
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'Draft': 'Nháp', 'Ready': 'Sẵn sàng', 'InProgress': 'Đang chờ',
      'Executed': 'Đã thực hiện', 'Reviewed': 'Đã review', 'Cancelled': 'Đã huỷ'
    };
    return labels[status] || status;
  }

  getWizardParams(): Record<string, string> {
    const params: Record<string, string> = {};
    if (this.selectedPlanId) params['planId'] = this.selectedPlanId;
    if (this.plan.symbol) params['symbol'] = this.plan.symbol;
    if (this.plan.direction) params['direction'] = this.plan.direction;
    if (this.plan.entryPrice) params['price'] = String(this.plan.entryPrice);
    if (this.plan.quantity || this.optimalShares) params['quantity'] = String(this.plan.quantity || this.optimalShares);
    if (this.plan.portfolioId) params['portfolioId'] = this.plan.portfolioId;
    if (this.plan.stopLoss) params['stopLoss'] = String(this.plan.stopLoss);
    if (this.plan.target) params['takeProfit'] = String(this.plan.target);
    if (this.plan.strategyId) params['strategyId'] = this.plan.strategyId;
    return params;
  }

  getTradeCreateParams(): Record<string, string> {
    const params: Record<string, string> = {};
    if (this.selectedPlanId) params['planId'] = this.selectedPlanId;
    if (this.plan.symbol) params['symbol'] = this.plan.symbol;
    if (this.plan.direction) params['direction'] = this.plan.direction;
    if (this.plan.entryPrice) params['price'] = String(this.plan.entryPrice);
    if (this.plan.quantity || this.optimalShares) params['quantity'] = String(this.plan.quantity || this.optimalShares);
    if (this.plan.portfolioId) params['portfolioId'] = this.plan.portfolioId;
    if (this.plan.stopLoss) params['stopLoss'] = String(this.plan.stopLoss);
    if (this.plan.target) params['takeProfit'] = String(this.plan.target);
    return params;
  }

  // --- Saved Plan Lot Helpers ---

  getExecutedLotCount(sp: TradePlanDto): number {
    return (sp.lots || []).filter(l => l.status === 'Executed').length;
  }

  getLotProgress(sp: TradePlanDto): number {
    const lots = sp.lots || [];
    if (lots.length === 0) return 0;
    return Math.round((this.getExecutedLotCount(sp) / lots.length) * 100);
  }

  // --- Multi-lot & Exit Targets ---

  onEntryModeChange(): void {
    if (this.plan.entryMode === 'Single') {
      this.plan.lots = [];
      this.dcaSchedule = [];
    } else if (this.plan.entryMode === 'DCA') {
      this.plan.lots = [];
      this.buildDcaSchedule();
    } else if (this.plan.lots.length === 0) {
      // Default 2 lots for ScalingIn
      this.plan.lots = [
        { lotNumber: 1, plannedPrice: this.plan.entryPrice, plannedQuantity: 0, allocationPercent: 50, label: '' },
        { lotNumber: 2, plannedPrice: 0, plannedQuantity: 0, allocationPercent: 50, label: '' }
      ];
      this.dcaSchedule = [];
      this.recalculateLots();
    }
  }

  addLot(): void {
    const nextNum = this.plan.lots.length + 1;
    this.plan.lots.push({
      lotNumber: nextNum, plannedPrice: 0, plannedQuantity: 0, allocationPercent: 0, label: ''
    });
  }

  removeLot(index: number): void {
    this.plan.lots.splice(index, 1);
    this.plan.lots.forEach((l, i) => l.lotNumber = i + 1);
    this.recalculateLots();
  }

  recalculateLots(): void {
    const totalQty = this.plan.quantity || this.optimalShares;
    if (totalQty <= 0) return;
    const totalPlanned = this.plan.lots.reduce((s, l) => s + (l.plannedQuantity || 0), 0);
    if (totalPlanned > 0) {
      this.plan.lots.forEach(l => {
        l.allocationPercent = totalPlanned > 0 ? Math.round((l.plannedQuantity / totalPlanned) * 100) : 0;
      });
    }
  }

  applyLotPreset(preset: string): void {
    const totalQty = this.plan.quantity || this.optimalShares || 0;
    const entryPrice = this.plan.entryPrice || 0;
    let splits: number[];
    switch (preset) {
      case '40-30-30': splits = [40, 30, 30]; break;
      case '50-50': splits = [50, 50]; break;
      case 'equal':
        const n = this.plan.lots.length || 3;
        splits = Array(n).fill(Math.floor(100 / n));
        splits[0] += 100 - splits.reduce((a, b) => a + b, 0);
        break;
      default: return;
    }
    this.plan.lots = splits.map((pct, i) => ({
      lotNumber: i + 1,
      plannedPrice: entryPrice,
      plannedQuantity: Math.floor(totalQty * pct / 100 / 100) * 100,
      allocationPercent: pct,
      label: ''
    }));
  }

  getLotsTotalQty(): number {
    return this.plan.lots.reduce((s, l) => s + (l.plannedQuantity || 0), 0);
  }

  getLotsWeightedAvg(): number {
    const totalQty = this.getLotsTotalQty();
    if (totalQty <= 0) return 0;
    const totalValue = this.plan.lots.reduce((s, l) => s + (l.plannedPrice || 0) * (l.plannedQuantity || 0), 0);
    return totalValue / totalQty;
  }

  // --- DCA Methods ---

  getDcaDuration(): string {
    const n = this.dcaForm.numberOfPeriods;
    switch (this.dcaForm.frequency) {
      case 'weekly': return n >= 4 ? `~${(n / 4.3).toFixed(0)} tháng` : `${n} tuần`;
      case 'biweekly': return n >= 2 ? `~${(n / 2.15).toFixed(0)} tháng` : `${n * 2} tuần`;
      case 'monthly': return n >= 12 ? `${(n / 12).toFixed(1)} năm` : `${n} tháng`;
      default: return '';
    }
  }

  getDcaFrequencyLabel(): string {
    switch (this.dcaForm.frequency) {
      case 'weekly': return 'Hàng tuần';
      case 'biweekly': return '2 tuần/lần';
      case 'monthly': return 'Hàng tháng';
      default: return '';
    }
  }

  buildDcaSchedule(): void {
    if (!this.dcaForm.amountPerPeriod || !this.dcaForm.numberOfPeriods) {
      this.dcaSchedule = [];
      return;
    }
    const schedule: typeof this.dcaSchedule = [];
    let currentDate = new Date(this.dcaForm.startDate || Date.now());
    let cumulative = 0;

    for (let i = 1; i <= this.dcaForm.numberOfPeriods; i++) {
      cumulative += this.dcaForm.amountPerPeriod;
      schedule.push({
        period: i,
        date: new Date(currentDate),
        amount: this.dcaForm.amountPerPeriod,
        cumulative
      });
      // Advance date
      switch (this.dcaForm.frequency) {
        case 'weekly': currentDate.setDate(currentDate.getDate() + 7); break;
        case 'biweekly': currentDate.setDate(currentDate.getDate() + 14); break;
        case 'monthly': currentDate.setMonth(currentDate.getMonth() + 1); break;
      }
    }
    this.dcaSchedule = schedule;
  }

  addExitTarget(): void {
    const nextLevel = this.plan.exitTargets.length + 1;
    this.plan.exitTargets.push({
      level: nextLevel, actionType: 'TakeProfit', price: 0, percentOfPosition: 0, label: ''
    });
  }

  removeExitTarget(index: number): void {
    this.plan.exitTargets.splice(index, 1);
    this.plan.exitTargets.forEach((e, i) => e.level = i + 1);
  }

  // --- Scenario Suggestion Methods (P0.6) ---

  fetchScenarioSuggestion(): void {
    if (!this.plan.symbol || !this.plan.entryPrice) return;
    this.loadingSuggestion = true;
    this.scenarioSuggestion = null;
    this.tradePlanService.getScenarioSuggestion(this.plan.symbol, this.plan.entryPrice, this.selectedTimeHorizon).subscribe({
      next: (suggestion) => {
        this.scenarioSuggestion = suggestion;
        // Default: all nodes selected
        this.selectedSuggestionNodes = new Set(suggestion.nodes.map(n => n.nodeId));
        this.loadingSuggestion = false;
      },
      error: () => {
        this.loadingSuggestion = false;
        this.notification.error('Lỗi', 'Không thể lấy gợi ý kịch bản');
      }
    });
  }

  toggleSuggestionNode(nodeId: string): void {
    if (this.selectedSuggestionNodes.has(nodeId)) {
      this.selectedSuggestionNodes.delete(nodeId);
    } else {
      this.selectedSuggestionNodes.add(nodeId);
    }
  }

  trackBySuggestionNodeId = (_: number, sn: any) => sn.nodeId;

  getCategoryLabel(category: string): string {
    const map: Record<string, string> = {
      'TakeProfit': 'Chốt lời',
      'StopLoss': 'Cắt lỗ',
      'AddPosition': 'Mua thêm',
      'Sideway': 'Sideway'
    };
    return map[category] || category;
  }

  private mapSuggestedNode(sn: SuggestedNodeDto, idMap: Map<string, string>): ScenarioNodeForm {
    return {
      nodeId: idMap.get(sn.nodeId)!,
      parentId: sn.parentId ? (idMap.get(sn.parentId) ?? null) : null,
      order: sn.order,
      label: sn.label,
      conditionType: sn.conditionType,
      conditionValue: sn.conditionValue ?? 0,
      conditionNote: sn.reasoning,
      actionType: sn.actionType,
      actionValue: sn.actionValue ?? 0,
      trailingStopConfig: { method: 'Percentage', trailValue: 5, activationPrice: 0, stepSize: 0 },
      status: 'Pending'
    };
  }

  applySelectedSuggestions(): void {
    if (!this.scenarioSuggestion || this.selectedSuggestionNodes.size === 0) return;
    const nodes = this.scenarioSuggestion.nodes.filter(n => this.selectedSuggestionNodes.has(n.nodeId));
    const idMap = new Map<string, string>();
    nodes.forEach(n => idMap.set(n.nodeId, crypto.randomUUID()));
    const mapped = nodes.map(n => this.mapSuggestedNode(n, idMap));
    this.scenarioNodes = [...this.scenarioNodes, ...mapped];
    this.invalidateScenarioCache();
    this.scenarioSuggestion = null;
    this.selectedSuggestionNodes = new Set();
    this.notification.success('Kịch bản', `Đã áp dụng ${mapped.length} kịch bản gợi ý`);
  }

  applyAllSuggestions(): void {
    if (!this.scenarioSuggestion) return;
    const nodes = this.scenarioSuggestion.nodes;
    const idMap = new Map<string, string>();
    nodes.forEach(n => idMap.set(n.nodeId, crypto.randomUUID()));
    this.scenarioNodes = nodes.map(n => this.mapSuggestedNode(n, idMap));
    this.invalidateScenarioCache();
    this.scenarioSuggestion = null;
    this.selectedSuggestionNodes = new Set();
    this.notification.success('Kịch bản', `Đã tạo kế hoạch với ${nodes.length} kịch bản từ gợi ý AI`);
  }

  // --- Scenario Playbook Methods ---

  loadScenarioPresets(): void {
    if (this.scenarioPresets.length > 0) return;
    this.tradePlanService.getScenarioTemplates().subscribe({
      next: (presets) => this.scenarioPresets = presets,
      error: () => this.notification.error('Lỗi', 'Không thể tải mẫu kịch bản')
    });
  }

  private invalidateScenarioCache(): void {
    this._scenarioVersion++;
    this._cachedRootNodes = this.scenarioNodes.filter(n => !n.parentId).sort((a, b) => a.order - b.order);
    this._cachedChildMap.clear();
    for (const node of this.scenarioNodes) {
      if (node.parentId) {
        const siblings = this._cachedChildMap.get(node.parentId) || [];
        siblings.push(node);
        this._cachedChildMap.set(node.parentId, siblings);
      }
    }
    this._cachedChildMap.forEach(arr => arr.sort((a, b) => a.order - b.order));
  }

  getScenarioRootNodes(): ScenarioNodeForm[] {
    return this._cachedRootNodes;
  }

  getScenarioChildNodes(parentId: string): ScenarioNodeForm[] {
    return this._cachedChildMap.get(parentId) || [];
  }

  hasScenarioChildren(nodeId: string): boolean {
    return (this._cachedChildMap.get(nodeId) || []).length > 0;
  }

  toggleScenarioCollapse(nodeId: string): void {
    if (this.collapsedNodes.has(nodeId)) {
      this.collapsedNodes.delete(nodeId);
    } else {
      this.collapsedNodes.add(nodeId);
    }
  }

  addScenarioNode(parentId: string | null): void {
    this.scenarioNodes.push({
      nodeId: crypto.randomUUID(),
      parentId,
      order: this.scenarioNodes.filter(n => n.parentId === parentId).length,
      label: '',
      conditionType: parentId ? 'PriceAbove' : 'PriceAbove',
      conditionValue: 0,
      conditionNote: '',
      actionType: 'SellPercent',
      actionValue: 30,
      trailingStopConfig: { method: 'Percentage', trailValue: 5, activationPrice: 0, stepSize: 0 },
      status: 'Pending'
    });
    this.invalidateScenarioCache();
  }

  removeScenarioNode(nodeId: string): void {
    const toRemove = new Set<string>();
    const collect = (id: string) => {
      toRemove.add(id);
      this.scenarioNodes.filter(n => n.parentId === id).forEach(c => collect(c.nodeId));
    };
    collect(nodeId);
    this.scenarioNodes = this.scenarioNodes.filter(n => !toRemove.has(n.nodeId));
    this.invalidateScenarioCache();
  }

  applyScenarioPreset(): void {
    const preset = this.scenarioPresets.find(p => p.id === this.selectedPresetId);
    if (!preset) return;
    // Build old->new ID map for parent references
    const idMap = new Map<string, string>();
    preset.nodes.forEach(n => idMap.set(n.nodeId, crypto.randomUUID()));

    this.scenarioNodes = preset.nodes.map(n => ({
      nodeId: idMap.get(n.nodeId)!,
      parentId: n.parentId ? (idMap.get(n.parentId) ?? null) : null,
      order: n.order,
      label: n.label,
      conditionType: n.conditionType,
      conditionValue: this.substitutePresetValue(n.conditionValue, n.conditionType),
      conditionNote: n.conditionNote || '',
      actionType: n.actionType,
      actionValue: n.actionValue || 0,
      trailingStopConfig: n.trailingStopConfig
        ? { method: n.trailingStopConfig.method, trailValue: n.trailingStopConfig.trailValue, activationPrice: n.trailingStopConfig.activationPrice || 0, stepSize: n.trailingStopConfig.stepSize || 0 }
        : { method: 'Percentage', trailValue: 5, activationPrice: 0, stepSize: 0 },
      status: 'Pending'
    }));
    this.invalidateScenarioCache();
    this.notification.success('Kịch bản', `Đã áp dụng mẫu "${preset.nameVi}"`);
  }

  getSystemPresets(): ScenarioPreset[] {
    return this.scenarioPresets.filter(p => p.isPreset);
  }

  getUserPresets(): ScenarioPreset[] {
    return this.scenarioPresets.filter(p => !p.isPreset);
  }

  isPresetSelected(): boolean {
    const selected = this.scenarioPresets.find(p => p.id === this.selectedPresetId);
    return !!selected?.isPreset;
  }

  saveScenarioTemplate(): void {
    if (!this.newScenarioTemplateName.trim() || this.scenarioNodes.length === 0) return;
    this.savingScenarioTemplate = true;
    const payload = this.buildScenarioPayload();
    if (!payload) {
      this.savingScenarioTemplate = false;
      return;
    }
    this.tradePlanService.saveScenarioTemplate({
      name: this.newScenarioTemplateName.trim(),
      description: this.newScenarioTemplateDesc.trim(),
      nodes: payload
    }).subscribe({
      next: (res) => {
        // Add to local list as a user template
        this.scenarioPresets.push({
          id: res.id,
          name: this.newScenarioTemplateName.trim(),
          nameVi: this.newScenarioTemplateName.trim(),
          description: this.newScenarioTemplateDesc.trim(),
          nodes: payload,
          isPreset: false
        });
        this.savingScenarioTemplate = false;
        this.showSaveScenarioTemplate = false;
        this.newScenarioTemplateName = '';
        this.newScenarioTemplateDesc = '';
        this.notification.success('Mẫu kịch bản', 'Đã lưu mẫu thành công');
      },
      error: () => {
        this.savingScenarioTemplate = false;
        this.notification.error('Lỗi', 'Không thể lưu mẫu kịch bản');
      }
    });
  }

  deleteScenarioTemplate(id: string): void {
    if (!confirm('Bạn có chắc muốn xoá mẫu kịch bản này?')) return;
    this.tradePlanService.deleteScenarioTemplate(id).subscribe({
      next: () => {
        this.scenarioPresets = this.scenarioPresets.filter(p => p.id !== id);
        if (this.selectedPresetId === id) this.selectedPresetId = '';
        this.notification.success('Mẫu kịch bản', 'Đã xoá mẫu');
      },
      error: () => this.notification.error('Lỗi', 'Không thể xoá mẫu kịch bản')
    });
  }

  private substitutePresetValue(value: number | null, conditionType: string): number {
    if (value === null || value === 0) {
      // For PriceBelow with 0 value → use stopLoss
      if (conditionType === 'PriceBelow') return this.plan.stopLoss || 0;
      return 0;
    }
    // PricePercentChange values are relative percentages already
    if (conditionType === 'PricePercentChange') return value;
    // For PriceAbove/PriceBelow, values might be absolute
    return value;
  }

  private buildScenarioPayload(): ScenarioNodeDto[] | undefined {
    if (this.exitStrategyMode !== 'Advanced' || this.scenarioNodes.length === 0) return undefined;
    return this.scenarioNodes.map(n => ({
      nodeId: n.nodeId, parentId: n.parentId, order: n.order, label: n.label,
      conditionType: n.conditionType, conditionValue: n.conditionValue || null,
      conditionNote: n.conditionNote || null,
      actionType: n.actionType, actionValue: n.actionValue || null,
      trailingStopConfig: n.actionType === 'ActivateTrailingStop' ? {
        method: n.trailingStopConfig.method, trailValue: n.trailingStopConfig.trailValue,
        activationPrice: n.trailingStopConfig.activationPrice || undefined,
        stepSize: n.trailingStopConfig.stepSize || undefined,
        currentTrailingStop: undefined, highestPrice: undefined
      } as TrailingStopConfigDto : null,
      status: 'Pending', triggeredAt: null, tradeId: null
    }) as ScenarioNodeDto);
  }

  // --- Scenario History ---

  loadScenarioHistory(planId: string): void {
    this.tradePlanService.getScenarioHistory(planId).subscribe({
      next: (history) => this.scenarioHistory = history,
      error: () => this.scenarioHistory = []
    });
  }

  getActionLabel(actionType: string, actionValue: number | null): string {
    switch (actionType) {
      case 'SellPercent': return `Bán ${actionValue || 0}% vị thế`;
      case 'SellAll': return 'Bán toàn bộ';
      case 'MoveStopLoss': return `Dời SL đến ${(actionValue || 0).toLocaleString('vi-VN')}đ`;
      case 'MoveStopToBreakeven': return 'Dời SL về hòa vốn';
      case 'ActivateTrailingStop': return 'Bật trailing stop';
      case 'AddPosition': return `Thêm ${actionValue || 0}% vị thế`;
      case 'SendNotification': return 'Thông báo';
      default: return actionType;
    }
  }

  formatTriggerTime(isoString: string): string {
    const d = new Date(isoString);
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const hh = String(d.getHours()).padStart(2, '0');
    const mi = String(d.getMinutes()).padStart(2, '0');
    return `${dd}/${mm} ${hh}:${mi}`;
  }

  // --- Order Sheet ---

  copyOrderSheet(): void {
    const text = this.getOrderSheetText();
    navigator.clipboard.writeText(text).then(
      () => this.notification.success('Phiếu lệnh', 'Đã copy vào clipboard'),
      () => this.notification.error('Lỗi', 'Không thể copy')
    );
  }

  printOrderSheet(): void {
    const text = this.getOrderSheetText();
    const win = window.open('', '_blank', 'width=400,height=600');
    if (win) {
      win.document.write(`<html><head><title>Phiếu lệnh - ${this.plan.symbol}</title>
        <style>body{font-family:monospace;white-space:pre-line;padding:20px;font-size:14px;}</style>
        </head><body>${text}</body></html>`);
      win.document.close();
      win.print();
    }
  }

  getOrderSheetText(): string {
    const p = this.plan;
    const qty = p.quantity || this.optimalShares;
    const portfolioName = this.portfolios.find(pt => pt.id === p.portfolioId)?.name || '';
    const totalValue = qty * (p.entryPrice || 0);
    const lines: string[] = [
      `=== PHIẾU LỆNH ===`,
      `Mã: ${p.symbol}  |  ${getTradeTypeDisplay(p.direction).toUpperCase()}`,
      portfolioName ? `Danh mục: ${portfolioName}` : '',
      `Giá vào: ${(p.entryPrice || 0).toLocaleString('vi-VN')}đ`,
      `Số lượng: ${qty.toLocaleString('vi-VN')} CP`,
      `Giá trị: ${totalValue.toLocaleString('vi-VN')}đ`,
      `Stop-Loss: ${(p.stopLoss || 0).toLocaleString('vi-VN')}đ`,
      `Take-Profit: ${(p.target || 0).toLocaleString('vi-VN')}đ`,
      `R:R = 1:${this.rr.toFixed(1)}`,
    ].filter(l => l);

    if (p.entryMode !== 'Single' && p.lots.length > 0) {
      lines.push('', `--- Chia lô (${p.entryMode}) ---`);
      p.lots.forEach(l => {
        lines.push(`  Lô ${l.lotNumber}: ${(l.plannedQuantity || 0).toLocaleString('vi-VN')} CP @ ${(l.plannedPrice || 0).toLocaleString('vi-VN')}đ (${l.allocationPercent}%)`);
      });
    }

    if (p.exitTargets.length > 0) {
      lines.push('', '--- Thoát lệnh ---');
      p.exitTargets.forEach(e => {
        const typeLabel: Record<string, string> = {
          TakeProfit: 'Chốt lời', CutLoss: 'Cắt lỗ', TrailingStop: 'Trailing', PartialExit: 'Bán 1 phần'
        };
        lines.push(`  ${typeLabel[e.actionType] || e.actionType}: ${(e.price || 0).toLocaleString('vi-VN')}đ${e.percentOfPosition ? ` (${e.percentOfPosition}%)` : ''}`);
      });
    }

    if (p.reason) lines.push('', `Lý do: ${p.reason}`);
    lines.push('', `Ngày: ${new Date().toLocaleDateString('vi-VN')}`);
    return lines.join('\n');
  }
}
