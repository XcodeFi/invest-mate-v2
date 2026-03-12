import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { StrategyService, Strategy } from '../../core/services/strategy.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { RiskService, RiskProfile } from '../../core/services/risk.service';
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

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Trade Setup -->
        <div class="lg:col-span-2 space-y-6">
          <div class="bg-white rounded-lg shadow p-6">
            <h2 class="text-lg font-semibold mb-4">Thiết lập giao dịch</h2>
            <div class="grid grid-cols-2 md:grid-cols-3 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Mã cổ phiếu *</label>
                <input [(ngModel)]="plan.symbol" type="text" (input)="plan.symbol = plan.symbol.toUpperCase()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                  placeholder="VD: VNM">
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
                  <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
                  <option value="">-- Chọn --</option>
                  <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }} ({{ p.initialCapital | vndCurrency }})</option>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Giá vào lệnh *</label>
                <input [(ngModel)]="plan.entryPrice" type="number" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Stop-Loss *</label>
                <input [(ngModel)]="plan.stopLoss" type="number" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Take-Profit *</label>
                <input [(ngModel)]="plan.target" type="number" (ngModelChange)="recalculate()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Số lượng (CP)</label>
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
              <label class="block text-sm font-medium text-gray-700 mb-2">Mức độ tự tin: {{ plan.confidenceLevel }}/10</label>
              <input [(ngModel)]="plan.confidenceLevel" type="range" min="1" max="10"
                class="w-full h-2 bg-gray-200 rounded-lg cursor-pointer">
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
                <span class="text-sm text-gray-600">R:R Ratio</span>
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
export class TradePlanComponent implements OnInit {
  strategies: Strategy[] = [];
  portfolios: PortfolioSummary[] = [];
  selectedStrategy: Strategy | null = null;
  riskProfile: RiskProfile | null = null;

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

  constructor(
    private strategyService: StrategyService,
    private portfolioService: PortfolioService,
    private riskService: RiskService,
    private notification: NotificationService
  ) {
    this.initChecklist();
  }

  ngOnInit(): void {
    this.strategyService.getAll().subscribe({ next: d => this.strategies = d });
    this.portfolioService.getAll().subscribe({ next: d => this.portfolios = d });
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
  }

  onPortfolioChange(): void {
    if (!this.plan.portfolioId) { this.riskProfile = null; return; }
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
    return criticalItems.every(c => c.checked);
  }

  getMissingCritical(): string {
    const missing = this.plan.checklist.filter(c => c.critical && !c.checked);
    if (missing.length === 0) return '';
    return `Còn ${missing.length} điều kiện bắt buộc chưa hoàn thành`;
  }
}
