import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, takeUntil, catchError } from 'rxjs';
import { StrategyService, Strategy } from '../../core/services/strategy.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { RiskService, RiskProfile, PortfolioRiskSummary } from '../../core/services/risk.service';
import { MarketDataService, StockPrice } from '../../core/services/market-data.service';
import { TradePlanTemplateService, TradePlanTemplate } from '../../core/services/trade-plan-template.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

interface ChecklistItem {
  label: string;
  category: string;
  checked: boolean;
  critical: boolean;
  hint: string;
}

interface TradePlan {
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
  confidenceLevel: number;
  checklist: ChecklistItem[];
  notes: string;
}

@Component({
  selector: 'app-trade-plan',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Kế hoạch giao dịch (Trade Plan)</h1>

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
            <div class="grid grid-cols-2 md:grid-cols-3 gap-4">
              <div class="relative">
                <label class="block text-sm font-medium text-gray-700 mb-1">Mã cổ phiếu *</label>
                <input [(ngModel)]="plan.symbol" type="text"
                  (input)="plan.symbol = plan.symbol.toUpperCase(); onSymbolInput()"
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
                <input [(ngModel)]="plan.entryPrice" type="number" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">
                  Stop-Loss <sup class="text-red-400 font-bold cursor-default" title="Giải thích ¹">¹</sup> *
                </label>
                <input [(ngModel)]="plan.stopLoss" type="number" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  [placeholder]="suggestedSlHint">
                <p *ngIf="slAutoFilled" class="text-xs text-blue-500 mt-0.5">Tự điền từ chiến lược</p>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">
                  Take-Profit <sup class="text-emerald-500 font-bold cursor-default" title="Giải thích ²">²</sup> *
                </label>
                <input [(ngModel)]="plan.target" type="number" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  [placeholder]="suggestedTpHint">
                <p *ngIf="tpAutoFilled" class="text-xs text-blue-500 mt-0.5">Tự điền từ chiến lược</p>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">
                  Số lượng (CP) <sup class="text-violet-400 font-bold cursor-default" title="Giải thích ³">³</sup>
                </label>
                <input [(ngModel)]="plan.quantity" type="number" step="100" (ngModelChange)="onQuantityManualChange()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  [placeholder]="optimalShares > 0 ? 'Tự động: ' + optimalShares : '0'">
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
            </div>

            <div class="mt-4">
              <label class="block text-sm font-medium text-gray-700 mb-1">Lý do vào lệnh</label>
              <textarea [(ngModel)]="plan.reason" rows="2"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="Lý do cụ thể để vào lệnh này..."></textarea>
            </div>

            <div class="mt-4">
              <label class="block text-sm font-medium text-gray-700 mb-2">
                Mức độ tự tin: {{ plan.confidenceLevel }}/10
                <sup class="text-amber-400 font-bold cursor-default" title="Giải thích ⁴">⁴</sup>
              </label>
              <input [(ngModel)]="plan.confidenceLevel" type="range" min="1" max="10"
                class="w-full h-2 bg-gray-200 rounded-lg cursor-pointer">
            </div>

            <!-- Glossary footnotes -->
            <div class="mt-4 rounded-lg bg-gray-50 border border-gray-200 px-4 py-3 text-xs text-gray-500 space-y-1">
              <div><sup class="text-red-400 font-bold">¹</sup> <strong>Stop-Loss (SL) — Cắt lỗ:</strong> Mức giá mà bạn chấp nhận bán lỗ để giới hạn thiệt hại. VD: Mua ở 50,000 đ, SL = 47,500 đ → thua tối đa 5%.</div>
              <div><sup class="text-emerald-500 font-bold">²</sup> <strong>Take-Profit (TP) — Chốt lời:</strong> Mức giá mục tiêu để hiện thực hóa lợi nhuận. VD: TP = 57,500 đ → lãi 15% nếu chạm mức này.</div>
              <div><sup class="text-violet-400 font-bold">³</sup> <strong>Số lượng CP — Position Size:</strong> Số cổ phiếu nên mua để rủi ro không vượt % vốn cho phép. Tự tính nếu chọn Danh mục có Risk Profile.</div>
              <div><sup class="text-amber-400 font-bold">⁴</sup> <strong>Mức độ tự tin:</strong> Điểm 1–10 đánh giá mức chắc chắn của tín hiệu. &lt;5 = tín hiệu yếu nên bỏ qua; ≥8 = tín hiệu mạnh.</div>
              <div><sup class="text-blue-400 font-bold">⁵</sup> <strong>R:R Ratio (Risk:Reward):</strong> Tỷ lệ lợi nhuận/rủi ro. R:R = 1:2 nghĩa là rủi ro 1đ để kiếm 2đ. Nên giao dịch khi R:R ≥ 1:2 (màu xanh).</div>
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

          <!-- Quick Metrics -->
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Chỉ số giao dịch</h2>
            <div class="space-y-3">
              <div class="flex justify-between items-center">
                <span class="text-sm text-gray-600">
                  R:R Ratio <sup class="text-blue-400 font-bold cursor-default" title="Giải thích ⁵">⁵</sup>
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

          <!-- Pre-trade Checklist -->
          <div class="bg-white rounded-lg shadow p-6">
            <div class="flex justify-between items-center mb-4">
              <h2 class="text-lg font-semibold">Checklist trước giao dịch</h2>
              <span class="text-sm font-medium px-2 py-1 rounded-full"
                [class.bg-green-100]="checklistScore >= 80"
                [class.text-green-700]="checklistScore >= 80"
                [class.bg-yellow-100]="checklistScore >= 50 && checklistScore < 80"
                [class.text-yellow-700]="checklistScore >= 50 && checklistScore < 80"
                [class.bg-red-100]="checklistScore < 50"
                [class.text-red-700]="checklistScore < 50">
                {{ checklistScore }}%
              </span>
            </div>

            <div *ngFor="let category of checklistCategories" class="mb-4">
              <div class="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2">{{ category }}</div>
              <div *ngFor="let item of getChecklistByCategory(category)" class="flex items-start gap-2 mb-2">
                <input type="checkbox" [(ngModel)]="item.checked" (ngModelChange)="updateChecklistScore()"
                  class="mt-1 h-4 w-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500">
                <div class="flex-1">
                  <div class="text-sm" [class.text-gray-800]="!item.checked" [class.text-gray-400]="item.checked"
                    [class.line-through]="item.checked">
                    {{ item.label }}
                    <span *ngIf="item.critical" class="text-red-500 text-xs">*bắt buộc</span>
                  </div>
                  <div class="text-xs text-gray-400">{{ item.hint }}</div>
                </div>
              </div>
            </div>

            <!-- Go/No-Go -->
            <div class="mt-4 p-4 rounded-lg text-center font-bold"
              [class.bg-green-100]="canTrade" [class.text-green-700]="canTrade"
              [class.bg-red-100]="!canTrade" [class.text-red-700]="!canTrade">
              {{ canTrade ? 'SẴN SÀNG GIAO DỊCH' : 'CHƯA ĐỦ ĐIỀU KIỆN' }}
            </div>
            <div *ngIf="!canTrade" class="mt-2 text-xs text-red-500 text-center">
              {{ getMissingCritical() }}
            </div>

            <!-- Action buttons -->
            <div class="mt-4 space-y-2">
              <a routerLink="/trade-wizard"
                class="block w-full text-center bg-blue-600 hover:bg-blue-700 text-white font-medium py-3 px-4 rounded-lg transition-colors"
                [class.opacity-50]="!canTrade" [class.pointer-events-none]="!canTrade">
                🧙 Thực hiện qua Wizard
              </a>
              <a [routerLink]="['/trades/create']"
                [queryParams]="{ symbol: plan.symbol, direction: plan.direction, price: plan.entryPrice, quantity: plan.quantity || optimalShares, portfolioId: plan.portfolioId, stopLoss: plan.stopLoss, takeProfit: plan.target }"
                class="block w-full text-center bg-emerald-600 hover:bg-emerald-700 text-white font-medium py-2 px-4 rounded-lg transition-colors text-sm">
                Thực hiện ngay →
              </a>
            </div>
          </div>
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
  `
})
export class TradePlanComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private symbolSubject = new Subject<string>();

  strategies: Strategy[] = [];
  portfolios: PortfolioSummary[] = [];
  selectedStrategy: Strategy | null = null;
  riskProfile: RiskProfile | null = null;

  // Auto-fill stock price
  stockPrice: StockPrice | null = null;
  stockLoading = false;
  stockError = '';

  // Risk enforcement
  riskViolations: string[] = [];
  riskOverrideConfirmed = false;
  portfolioRiskSummary: PortfolioRiskSummary | null = null;

  plan: TradePlan = {
    symbol: '', direction: 'Buy', entryPrice: 0, stopLoss: 0, target: 0,
    quantity: 0, strategyId: '', portfolioId: '', reason: '',
    marketCondition: 'Trending', confidenceLevel: 5, checklist: [], notes: ''
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

  checklistCategories = ['Phân tích', 'Quản lý rủi ro', 'Tâm lý', 'Xác nhận'];

  // Template management
  templates: TradePlanTemplate[] = [];
  selectedTemplateId = '';
  showSaveTemplate = false;
  newTemplateName = '';
  savingTemplate = false;

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
    private notification: NotificationService
  ) {
    this.initChecklist();
  }

  ngOnInit(): void {
    this.strategyService.getAll().subscribe({ next: d => this.strategies = d });
    this.portfolioService.getAll().subscribe({ next: d => this.portfolios = d });
    this.templateService.getAll().subscribe({ next: d => this.templates = d, error: () => {} });

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
      }
    });
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
    this.plan.checklist = [
      { label: 'Đã xác định xu hướng chính (Daily/Weekly)', category: 'Phân tích', checked: false, critical: true, hint: 'Xu hướng lớn phải rõ ràng' },
      { label: 'Setup khớp với chiến lược đã chọn', category: 'Phân tích', checked: false, critical: true, hint: 'Entry rules được thỏa mãn' },
      { label: 'Khối lượng giao dịch xác nhận', category: 'Phân tích', checked: false, critical: false, hint: 'Volume trên trung bình' },
      { label: 'Không có tin xấu (earnings, sự kiện)','category': 'Phân tích', checked: false, critical: false, hint: 'Kiểm tra lịch sự kiện' },

      { label: 'Stop-loss đã được đặt', category: 'Quản lý rủi ro', checked: false, critical: true, hint: 'Biết chính xác điểm cắt lỗ' },
      { label: 'R:R ratio >= 2:1', category: 'Quản lý rủi ro', checked: false, critical: true, hint: 'Lời tiềm năng gấp 2 lần rủi ro' },
      { label: 'Vị thế trong giới hạn position sizing', category: 'Quản lý rủi ro', checked: false, critical: true, hint: 'Không vượt % tối đa danh mục' },
      { label: 'Tổng rủi ro danh mục chưa vượt giới hạn', category: 'Quản lý rủi ro', checked: false, critical: false, hint: 'Tính cả vị thế mới' },

      { label: 'Không đang FOMO hoặc sợ hãi', category: 'Tâm lý', checked: false, critical: false, hint: 'Bình tĩnh, có kế hoạch rõ' },
      { label: 'Chấp nhận mất số tiền rủi ro này', category: 'Tâm lý', checked: false, critical: true, hint: 'Thoải mái với mức lỗ tối đa' },
      { label: 'Không revenge trading', category: 'Tâm lý', checked: false, critical: false, hint: 'Không có giao dịch lỗ trước' },

      { label: 'Đã ghi nhật ký giao dịch', category: 'Xác nhận', checked: false, critical: false, hint: 'Entry reason, market context' },
      { label: 'Đã xác nhận lại giá vào/SL/TP', category: 'Xác nhận', checked: false, critical: true, hint: 'Double check các mức giá' },
    ];
    this.updateChecklistScore();
  }

  onStrategyChange(): void {
    this.selectedStrategy = this.strategies.find(s => s.id === this.plan.strategyId) || null;
    this.slAutoFilled = false;
    this.tpAutoFilled = false;
    this.suggestedSlHint = '';
    this.suggestedTpHint = '';

    const s = this.selectedStrategy;
    if (!s) return;

    const entry = this.plan.entryPrice;
    const isBuy = this.plan.direction === 'Buy';

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

    // Position sizing calculation
    this.calculatePositionSizing();

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
    const total = this.plan.checklist.length;
    const checked = this.plan.checklist.filter(c => c.checked).length;
    this.checklistScore = total > 0 ? Math.round((checked / total) * 100) : 0;
  }

  get canTrade(): boolean {
    const criticalItems = this.plan.checklist.filter(c => c.critical);
    const criticalOk = criticalItems.every(c => c.checked);
    const riskOk = this.riskViolations.length === 0 || this.riskOverrideConfirmed;
    return criticalOk && riskOk;
  }

  getMissingCritical(): string {
    const missing = this.plan.checklist.filter(c => c.critical && !c.checked);
    const parts: string[] = [];
    if (missing.length > 0) parts.push(`Còn ${missing.length} điều kiện bắt buộc chưa hoàn thành`);
    if (this.riskViolations.length > 0 && !this.riskOverrideConfirmed) {
      parts.push(`${this.riskViolations.length} vi phạm Risk Profile chưa xác nhận`);
    }
    return parts.join('. ');
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
}
