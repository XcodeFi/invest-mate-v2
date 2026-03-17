import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { StrategyService, Strategy } from '../../core/services/strategy.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { RiskService, RiskProfile } from '../../core/services/risk.service';
import { TradeService, CreateTradeRequest } from '../../core/services/trade.service';
import { JournalService, CreateJournalRequest } from '../../core/services/journal.service';
import { TradePlanService } from '../../core/services/trade-plan.service';
import { MarketDataService } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { isBuyTrade, getTradeTypeDisplay } from '../../shared/constants/trade-types';

interface ChecklistItem {
  label: string;
  checked: boolean;
  critical: boolean;
}

interface PositionCalc {
  maxRiskAmount: number;
  riskPerShare: number;
  optimalShares: number;
  positionValue: number;
  positionPercent: number;
  riskRewardRatio: number;
  potentialProfit: number;
  potentialLoss: number;
  withinLimit: boolean;
  warning: string;
}

@Component({
  selector: 'app-trade-wizard',
  standalone: true,
  imports: [CommonModule, FormsModule, VndCurrencyPipe, NumMaskDirective, UppercaseDirective],
  template: `
    <div class="container mx-auto px-4 py-6 max-w-4xl">
      <h1 class="text-2xl font-bold text-gray-800 mb-2">Wizard Giao dịch</h1>
      <p class="text-gray-500 mb-6">Hướng dẫn từng bước để thực hiện giao dịch có kỷ luật</p>

      <!-- Step Indicator -->
      <div class="flex items-center justify-center mb-8">
        <div *ngFor="let step of steps; let i = index; let last = last"
          class="flex items-center">
          <!-- Step circle -->
          <div class="flex flex-col items-center">
            <div
              [class]="getStepCircleClass(i)"
              class="w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold transition-all duration-300 cursor-pointer"
              (click)="goToStep(i)">
              <span *ngIf="i < currentStep">&#10003;</span>
              <span *ngIf="i >= currentStep">{{ i + 1 }}</span>
            </div>
            <span class="text-xs mt-1 text-center max-w-[80px] leading-tight"
              [class.text-blue-600]="i === currentStep"
              [class.text-green-600]="i < currentStep"
              [class.text-gray-400]="i > currentStep">
              {{ step }}
            </span>
          </div>
          <!-- Connector line -->
          <div *ngIf="!last"
            class="w-8 sm:w-12 h-0.5 mx-1 mb-5"
            [class.bg-green-400]="i < currentStep"
            [class.bg-gray-300]="i >= currentStep">
          </div>
        </div>
      </div>

      <!-- Step Content -->
      <div class="bg-white rounded-xl shadow-lg p-6 min-h-[400px]">

        <!-- ===== STEP 1: Chọn Chiến lược ===== -->
        <div *ngIf="currentStep === 0">
          <h2 class="text-xl font-semibold text-gray-800 mb-1">Chọn Chiến lược</h2>
          <p class="text-gray-500 text-sm mb-4">Chọn một chiến lược đã định sẵn hoặc bỏ qua bước này</p>

          <div *ngIf="loadingStrategies" class="flex justify-center py-12">
            <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
          </div>

          <div *ngIf="!loadingStrategies && strategies.length === 0"
            class="text-center py-12 text-gray-400">
            <p class="text-lg mb-2">Chưa có chiến lược nào</p>
            <p class="text-sm">Bạn có thể bỏ qua bước này và tiếp tục lập kế hoạch</p>
          </div>

          <div *ngIf="!loadingStrategies" class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div *ngFor="let s of strategies"
              (click)="selectStrategy(s)"
              class="border-2 rounded-lg p-4 cursor-pointer transition-all hover:shadow-md"
              [class.border-blue-500]="selectedStrategy?.id === s.id"
              [class.bg-blue-50]="selectedStrategy?.id === s.id"
              [class.border-gray-200]="selectedStrategy?.id !== s.id">
              <div class="flex items-start justify-between">
                <h3 class="font-semibold text-gray-800">{{ s.name }}</h3>
                <span *ngIf="s.timeFrame"
                  class="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">{{ s.timeFrame }}</span>
              </div>
              <p *ngIf="s.description" class="text-sm text-gray-500 mt-1 line-clamp-2">{{ s.description }}</p>
              <div *ngIf="s.entryRules" class="mt-2">
                <span class="text-xs font-medium text-gray-400 uppercase">Quy tắc vào lệnh</span>
                <p class="text-sm text-gray-600 line-clamp-2">{{ s.entryRules }}</p>
              </div>
              <div *ngIf="selectedStrategy?.id === s.id"
                class="mt-2 text-blue-600 text-sm font-medium flex items-center gap-1">
                &#10003; Đã chọn
              </div>
            </div>
          </div>

          <div *ngIf="!loadingStrategies" class="mt-6 flex justify-end">
            <button (click)="skipStrategy()"
              class="text-gray-500 hover:text-gray-700 text-sm font-medium mr-4">
              Bỏ qua &rarr;
            </button>
          </div>
        </div>

        <!-- ===== STEP 2: Lập Kế hoạch ===== -->
        <div *ngIf="currentStep === 1">
          <h2 class="text-xl font-semibold text-gray-800 mb-1">Lập Kế hoạch Giao dịch</h2>
          <p class="text-gray-500 text-sm mb-4">Nhập thông tin giao dịch và tính toán vị thế tối ưu</p>

          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Form -->
            <div class="space-y-4">
              <!-- Portfolio -->
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Danh mục đầu tư</label>
                <select [(ngModel)]="plan.portfolioId" (ngModelChange)="onPortfolioChange()"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                  <option value="">-- Chọn danh mục --</option>
                  <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }} ({{ p.initialCapital | vndCurrency }})</option>
                </select>
              </div>

              <!-- Symbol + Direction -->
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Mã chứng khoán</label>
                  <div class="relative">
                    <input [(ngModel)]="plan.symbol" type="text" placeholder="VD: VNM, FPT" appUppercase
                      (blur)="onSymbolBlur()"
                      class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                    <span *ngIf="loadingPrice" class="absolute right-2 top-2.5 animate-spin inline-block w-4 h-4 border-2 border-blue-500 border-t-transparent rounded-full"></span>
                  </div>
                  <p *ngIf="fetchedPrice" class="text-xs text-emerald-600 mt-1">Giá hiện tại: {{ fetchedPrice.toLocaleString('vi-VN') }} đ</p>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Hướng giao dịch</label>
                  <select [(ngModel)]="plan.direction"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                    <option value="Buy">Mua (Buy)</option>
                    <option value="Sell">Bán (Sell)</option>
                  </select>
                </div>
              </div>

              <!-- Entry, SL, TP -->
              <div class="grid grid-cols-3 gap-3">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Giá vào lệnh</label>
                  <input [(ngModel)]="plan.entryPrice" type="text" inputmode="numeric" appNumMask (ngModelChange)="calculate()"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    placeholder="VND">
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Stop-Loss <sup class="text-red-400 font-bold">1</sup></label>
                  <input [(ngModel)]="plan.stopLoss" type="text" inputmode="numeric" appNumMask (ngModelChange)="calculate()"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    placeholder="VND">
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Take-Profit <sup class="text-emerald-500 font-bold">2</sup></label>
                  <input [(ngModel)]="plan.takeProfit" type="text" inputmode="numeric" appNumMask (ngModelChange)="calculate()"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    placeholder="VND">
                </div>
              </div>

              <!-- Account balance & risk -->
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Tổng giá trị danh mục</label>
                  <input [(ngModel)]="plan.accountBalance" type="text" inputmode="numeric" appNumMask (ngModelChange)="calculate()"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">% Rủi ro / giao dịch <sup class="text-amber-400 font-bold">3</sup></label>
                  <input [(ngModel)]="plan.riskPercent" type="text" inputmode="numeric" appNumMask [decimals]="1" step="0.5" min="0.5" max="10" (ngModelChange)="calculate()"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                </div>
              </div>
            </div>

            <!-- Calculation Results -->
            <div>
              <div *ngIf="positionCalc" class="bg-gray-50 rounded-lg p-5 space-y-3">
                <h3 class="font-semibold text-gray-800 mb-3">Kết quả tính toán</h3>

                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">Mức rủi ro tối đa</span>
                  <span class="font-medium">{{ positionCalc.maxRiskAmount | vndCurrency }}</span>
                </div>
                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">Rủi ro / cổ phiếu</span>
                  <span class="font-medium">{{ positionCalc.riskPerShare | vndCurrency }}</span>
                </div>
                <hr class="border-gray-200">
                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">Số lượng tối ưu</span>
                  <span class="font-bold text-blue-600 text-lg">{{ positionCalc.optimalShares | number }}</span>
                </div>
                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">Giá trị vị thế</span>
                  <span class="font-medium">{{ positionCalc.positionValue | vndCurrency }}</span>
                </div>
                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">% danh mục</span>
                  <span class="font-medium">{{ positionCalc.positionPercent | number:'1.1-1' }}%</span>
                </div>
                <hr class="border-gray-200">
                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">Tỷ lệ R:R <sup class="text-blue-400 font-bold">4</sup></span>
                  <span class="font-bold"
                    [class.text-green-600]="positionCalc.riskRewardRatio >= 2"
                    [class.text-yellow-600]="positionCalc.riskRewardRatio >= 1 && positionCalc.riskRewardRatio < 2"
                    [class.text-red-600]="positionCalc.riskRewardRatio < 1">
                    1 : {{ positionCalc.riskRewardRatio | number:'1.1-1' }}
                  </span>
                </div>
                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">Lợi nhuận tiềm năng</span>
                  <span class="font-medium text-green-600">+{{ positionCalc.potentialProfit | vndCurrency }}</span>
                </div>
                <div class="flex justify-between text-sm">
                  <span class="text-gray-500">Thua lỗ tiềm năng</span>
                  <span class="font-medium text-red-600">-{{ positionCalc.potentialLoss | vndCurrency }}</span>
                </div>

                <!-- Warning -->
                <div *ngIf="!positionCalc.withinLimit"
                  class="mt-3 bg-red-50 border border-red-200 rounded-lg p-3 text-sm text-red-700">
                  <span class="font-semibold">Cảnh báo:</span> {{ positionCalc.warning }}
                </div>
              </div>

              <div *ngIf="!positionCalc" class="bg-gray-50 rounded-lg p-8 text-center text-gray-400">
                <p>Nhập đầy đủ thông tin để xem kết quả tính toán</p>
              </div>

              <!-- Selected strategy reminder -->
              <div *ngIf="selectedStrategy" class="mt-4 bg-blue-50 border border-blue-200 rounded-lg p-3">
                <span class="text-xs font-medium text-blue-500 uppercase">Chiến lược đang dùng</span>
                <p class="text-sm font-semibold text-blue-800">{{ selectedStrategy.name }}</p>
                <p *ngIf="selectedStrategy.entryRules" class="text-xs text-blue-600 mt-1 line-clamp-2">{{ selectedStrategy.entryRules }}</p>
              </div>
            </div>
          </div>

          <!-- Glossary -->
          <div class="mt-4 rounded-lg bg-gray-50 border border-gray-200 px-4 py-3 text-xs text-gray-500 space-y-1">
            <div><sup class="text-red-400 font-bold">1</sup> <strong>Stop-Loss (SL) — Cắt lỗ:</strong> Mức giá bạn chấp nhận bán để giới hạn thua lỗ. VD: mua 100, SL = 95 → tối đa chỉ lỗ 5%.</div>
            <div><sup class="text-emerald-500 font-bold">2</sup> <strong>Take-Profit (TP) — Chốt lời:</strong> Mức giá mục tiêu để bán lấy lãi. VD: mua 100, TP = 110 → mục tiêu lãi 10%.</div>
            <div><sup class="text-amber-400 font-bold">3</sup> <strong>% Rủi ro / giao dịch:</strong> Tỷ lệ % vốn bạn chấp nhận mất nếu lệnh chạm Stop-Loss. Thông thường 1–2% tổng danh mục mỗi lệnh.</div>
            <div><sup class="text-blue-400 font-bold">4</sup> <strong>R:R (Risk:Reward) — Tỷ lệ rủi ro/lợi nhuận:</strong> Lợi nhuận tiềm năng ÷ Rủi ro tối đa. R:R = 1:2 nghĩa là có thể lãi 2 đồng khi chấp nhận rủi ro 1 đồng. Tối thiểu nên đạt 1:2.</div>
          </div>
        </div>

        <!-- ===== STEP 3: Checklist ===== -->
        <div *ngIf="currentStep === 2">
          <h2 class="text-xl font-semibold text-gray-800 mb-1">Checklist Trước Giao dịch</h2>
          <p class="text-gray-500 text-sm mb-4">Kiểm tra tất cả các điều kiện trước khi vào lệnh</p>

          <div class="space-y-3 max-w-xl">
            <div *ngFor="let item of checklist; let i = index"
              (click)="item.checked = !item.checked"
              class="flex items-center gap-3 p-3 rounded-lg border cursor-pointer transition-colors"
              [class.bg-green-50]="item.checked"
              [class.border-green-300]="item.checked"
              [class.bg-white]="!item.checked"
              [class.border-gray-200]="!item.checked"
              [class.hover:bg-gray-50]="!item.checked">
              <div class="w-6 h-6 rounded border-2 flex items-center justify-center shrink-0 transition-colors"
                [class.bg-green-500]="item.checked"
                [class.border-green-500]="item.checked"
                [class.border-gray-300]="!item.checked">
                <span *ngIf="item.checked" class="text-white text-sm">&#10003;</span>
              </div>
              <div class="flex-1">
                <span class="text-sm font-medium text-gray-700">{{ item.label }}</span>
                <span *ngIf="item.critical" class="ml-2 text-xs bg-red-100 text-red-600 px-1.5 py-0.5 rounded">Bắt buộc</span>
              </div>
            </div>
          </div>

          <!-- Go/No-Go Status -->
          <div class="mt-6 p-4 rounded-lg text-center"
            [class.bg-green-50]="isChecklistPassed()"
            [class.bg-red-50]="!isChecklistPassed()">
            <div *ngIf="isChecklistPassed()" class="text-green-700">
              <span class="text-2xl">&#10003;</span>
              <p class="font-bold text-lg mt-1">GO - Sẵn sàng giao dịch</p>
              <p class="text-sm text-green-600">Tất cả điều kiện bắt buộc đã được đáp ứng</p>
            </div>
            <div *ngIf="!isChecklistPassed()" class="text-red-700">
              <span class="text-2xl">&#10007;</span>
              <p class="font-bold text-lg mt-1">NO-GO - Chưa đủ điều kiện</p>
              <p class="text-sm text-red-600">Vui lòng hoàn thành tất cả mục bắt buộc ({{ getCriticalRemaining() }} còn lại)</p>
            </div>
          </div>
        </div>

        <!-- ===== STEP 4: Xác nhận & Ghi giao dịch ===== -->
        <div *ngIf="currentStep === 3">
          <h2 class="text-xl font-semibold text-gray-800 mb-1">Xác nhận & Ghi Giao dịch</h2>
          <p class="text-gray-500 text-sm mb-4">Kiểm tra lại thông tin và ghi nhận giao dịch</p>

          <!-- Trade recorded success -->
          <div *ngIf="createdTradeId" class="bg-green-50 border border-green-200 rounded-lg p-6 text-center mb-6">
            <span class="text-4xl">&#10003;</span>
            <p class="text-lg font-bold text-green-800 mt-2">Giao dịch đã được ghi nhận!</p>
            <p class="text-sm text-green-600 mt-1">Mã giao dịch: <span class="font-mono">{{ createdTradeId }}</span></p>
          </div>

          <!-- Summary -->
          <div *ngIf="!createdTradeId" class="max-w-lg mx-auto">
            <div class="bg-gray-50 rounded-lg p-5 space-y-3">
              <h3 class="font-semibold text-gray-800 border-b border-gray-200 pb-2">Tóm tắt Kế hoạch</h3>

              <div *ngIf="selectedStrategy" class="flex justify-between text-sm">
                <span class="text-gray-500">Chiến lược</span>
                <span class="font-medium">{{ selectedStrategy.name }}</span>
              </div>
              <div class="flex justify-between text-sm">
                <span class="text-gray-500">Danh mục</span>
                <span class="font-medium">{{ getPortfolioName() }}</span>
              </div>
              <div class="flex justify-between text-sm">
                <span class="text-gray-500">Mã CK</span>
                <span class="font-bold text-lg">{{ plan.symbol | uppercase }}</span>
              </div>
              <div class="flex justify-between text-sm">
                <span class="text-gray-500">Hướng</span>
                <span class="font-medium"
                  [class.text-green-600]="isBuyTrade(plan.direction)"
                  [class.text-red-600]="!isBuyTrade(plan.direction)">
                  {{ getTradeTypeDisplay(plan.direction) }}
                </span>
              </div>
              <hr class="border-gray-200">
              <div class="flex justify-between text-sm">
                <span class="text-gray-500">Giá vào lệnh</span>
                <span class="font-medium">{{ plan.entryPrice | vndCurrency }}</span>
              </div>
              <div class="flex justify-between text-sm">
                <span class="text-gray-500">Stop-Loss</span>
                <span class="font-medium text-red-600">{{ plan.stopLoss | vndCurrency }}</span>
              </div>
              <div class="flex justify-between text-sm">
                <span class="text-gray-500">Take-Profit</span>
                <span class="font-medium text-green-600">{{ plan.takeProfit | vndCurrency }}</span>
              </div>
              <hr class="border-gray-200">
              <div *ngIf="positionCalc" class="flex justify-between text-sm">
                <span class="text-gray-500">Số lượng</span>
                <span class="font-bold">{{ positionCalc.optimalShares | number }}</span>
              </div>
              <div *ngIf="positionCalc" class="flex justify-between text-sm">
                <span class="text-gray-500">Tỷ lệ R:R</span>
                <span class="font-medium">1 : {{ positionCalc.riskRewardRatio | number:'1.1-1' }}</span>
              </div>
              <div class="flex justify-between text-sm">
                <span class="text-gray-500">Checklist</span>
                <span class="font-medium text-green-600">{{ getCheckedCount() }} / {{ checklist.length }} đạt</span>
              </div>
            </div>

            <div class="mt-6 text-center">
              <button (click)="recordTrade()"
                [disabled]="isRecording || !canRecordTrade()"
                class="bg-blue-600 hover:bg-blue-700 disabled:bg-gray-400 text-white px-8 py-3 rounded-lg font-semibold text-lg transition-colors">
                <span *ngIf="!isRecording">Ghi Giao dịch</span>
                <span *ngIf="isRecording" class="flex items-center gap-2">
                  <span class="animate-spin inline-block w-5 h-5 border-2 border-white border-t-transparent rounded-full"></span>
                  Đang xử lý...
                </span>
              </button>
              <p *ngIf="!canRecordTrade()" class="text-red-500 text-sm mt-2">Vui lòng điền đầy đủ thông tin ở bước trước</p>
            </div>
          </div>
        </div>

        <!-- ===== STEP 5: Nhật ký ===== -->
        <div *ngIf="currentStep === 4">
          <h2 class="text-xl font-semibold text-gray-800 mb-1">Nhật ký Giao dịch</h2>
          <p class="text-gray-500 text-sm mb-4">Ghi lại suy nghĩ và lý do vào lệnh để rút kinh nghiệm sau này</p>

          <div class="max-w-2xl mx-auto space-y-4">
            <!-- Entry Reason -->
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Lý do vào lệnh</label>
              <textarea [(ngModel)]="journal.entryReason" rows="3"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                placeholder="Tại sao bạn quyết định vào lệnh này?"></textarea>
            </div>

            <!-- Market Context -->
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Bối cảnh thị trường</label>
              <textarea [(ngModel)]="journal.marketContext" rows="2"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                placeholder="Xu hướng thị trường, tin tức, sự kiện liên quan..."></textarea>
            </div>

            <!-- Technical Setup -->
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Thiết lập kỹ thuật <sup class="text-violet-400 font-bold">5</sup></label>
              <textarea [(ngModel)]="journal.technicalSetup" rows="2"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                placeholder="Mô hình giá, chỉ báo kỹ thuật sử dụng..."></textarea>
            </div>

            <!-- Emotional State + Confidence -->
            <div class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Trạng thái cảm xúc <sup class="text-pink-400 font-bold">6</sup></label>
                <select [(ngModel)]="journal.emotionalState"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                  <option value="">-- Chọn --</option>
                  <option value="Bình tĩnh">Bình tĩnh</option>
                  <option value="Tự tin">Tự tin</option>
                  <option value="Lo lắng">Lo lắng</option>
                  <option value="Hưng phấn">Hưng phấn</option>
                  <option value="Sợ hãi">Sợ hãi</option>
                  <option value="FOMO">FOMO</option>
                </select>
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Mức độ tự tin (1-10)</label>
                <input [(ngModel)]="journal.confidenceLevel" type="number" min="1" max="10"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
              </div>
            </div>

            <!-- Glossary step 5 -->
            <div class="rounded-lg bg-gray-50 border border-gray-200 px-4 py-3 text-xs text-gray-500 space-y-1">
              <div><sup class="text-violet-400 font-bold">5</sup> <strong>Thiết lập kỹ thuật (Technical Setup):</strong> Mô tả các tín hiệu phân tích kỹ thuật đã dùng để ra quyết định: mô hình nến, đường MA, RSI, MACD, vùng hỗ trợ/kháng cự…</div>
              <div><sup class="text-pink-400 font-bold">6</sup> <strong>FOMO (Fear Of Missing Out):</strong> Tâm lý sợ bỏ lỡ — vào lệnh vì thấy giá tăng mạnh, không theo kế hoạch. Là một trong những nguyên nhân thua lỗ phổ biến nhất.</div>
            </div>

            <!-- Journal saved success -->
            <div *ngIf="journalSaved" class="bg-green-50 border border-green-200 rounded-lg p-4 text-center">
              <span class="text-green-600 font-semibold">&#10003; Nhật ký đã được lưu thành công!</span>
            </div>

            <div class="flex justify-center gap-4 mt-6">
              <button *ngIf="!journalSaved" (click)="saveJournal()"
                [disabled]="isSavingJournal || !createdTradeId"
                class="bg-blue-600 hover:bg-blue-700 disabled:bg-gray-400 text-white px-6 py-2.5 rounded-lg font-medium transition-colors">
                <span *ngIf="!isSavingJournal">Lưu Nhật ký</span>
                <span *ngIf="isSavingJournal" class="flex items-center gap-2">
                  <span class="animate-spin inline-block w-4 h-4 border-2 border-white border-t-transparent rounded-full"></span>
                  Đang lưu...
                </span>
              </button>
              <button (click)="goToDashboard()"
                class="bg-green-600 hover:bg-green-700 text-white px-6 py-2.5 rounded-lg font-medium transition-colors">
                Hoàn thành
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Navigation Buttons -->
      <div class="flex justify-between mt-6">
        <button *ngIf="currentStep > 0" (click)="prevStep()"
          class="flex items-center gap-2 px-5 py-2.5 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium transition-colors">
          &larr; Quay lại
        </button>
        <div *ngIf="currentStep === 0"></div>

        <button *ngIf="currentStep < steps.length - 1" (click)="nextStep()"
          [disabled]="!canProceed()"
          class="flex items-center gap-2 px-5 py-2.5 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-400 text-white rounded-lg font-medium transition-colors">
          Tiếp tục &rarr;
        </button>
      </div>
    </div>
  `
})
export class TradeWizardComponent implements OnInit {
  private strategyService = inject(StrategyService);
  private portfolioService = inject(PortfolioService);
  private riskService = inject(RiskService);
  private tradeService = inject(TradeService);
  private journalService = inject(JournalService);
  private marketDataService = inject(MarketDataService);
  private tradePlanService = inject(TradePlanService);
  private notificationService = inject(NotificationService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  loadingPrice = false;
  fetchedPrice: number | null = null;
  planId: string | null = null;
  lotNumber: number | null = null;

  isBuyTrade = isBuyTrade;
  getTradeTypeDisplay = getTradeTypeDisplay;

  steps =['Chiến lược', 'Kế hoạch', 'Checklist', 'Xác nhận', 'Nhật ký'];
  currentStep = 0;

  // Step 1
  strategies: Strategy[] = [];
  selectedStrategy: Strategy | null = null;
  loadingStrategies = false;

  // Step 2
  portfolios: PortfolioSummary[] = [];
  riskProfile: RiskProfile | null = null;
  plan = {
    portfolioId: '',
    symbol: '',
    direction: 'Buy' as 'Buy' | 'Sell',
    entryPrice: 0,
    stopLoss: 0,
    takeProfit: 0,
    accountBalance: 0,
    riskPercent: 2
  };
  positionCalc: PositionCalc | null = null;

  // Step 3
  checklist: ChecklistItem[] = [
    { label: 'Đã xác định xu hướng thị trường chung', checked: false, critical: true },
    { label: 'Tín hiệu vào lệnh rõ ràng theo chiến lược', checked: false, critical: true },
    { label: 'Đã đặt Stop-Loss hợp lý', checked: false, critical: true },
    { label: 'Tỷ lệ R:R tối thiểu 1:2', checked: false, critical: true },
    { label: 'Khối lượng giao dịch trong giới hạn rủi ro', checked: false, critical: true },
    { label: 'Không bị ảnh hưởng bởi cảm xúc (FOMO, sợ hãi)', checked: false, critical: false },
    { label: 'Đã kiểm tra lịch sự kiện / tin tức quan trọng', checked: false, critical: false },
    { label: 'Thanh khoản đủ cho mã chứng khoán này', checked: false, critical: false },
  ];

  // Step 4
  createdTradeId: string | null = null;
  isRecording = false;

  // Step 5
  journal = {
    entryReason: '',
    marketContext: '',
    technicalSetup: '',
    emotionalState: '',
    confidenceLevel: 5
  };
  journalSaved = false;
  isSavingJournal = false;

  ngOnInit(): void {
    this.loadStrategies();
    this.loadPortfolios();
    this.applyQueryParams();
  }

  private applyQueryParams(): void {
    const params = this.route.snapshot.queryParams;
    this.planId = params['planId'] || null;
    this.lotNumber = params['lotNumber'] ? +params['lotNumber'] : null;

    if (params['symbol']) this.plan.symbol = params['symbol'];
    if (params['direction']) this.plan.direction = params['direction'] as 'Buy' | 'Sell';
    if (params['price']) this.plan.entryPrice = +params['price'];
    if (params['quantity']) this.plan.entryPrice = this.plan.entryPrice || 0; // quantity handled via positionCalc
    if (params['portfolioId']) this.plan.portfolioId = params['portfolioId'];
    if (params['stopLoss']) this.plan.stopLoss = +params['stopLoss'];
    if (params['takeProfit']) this.plan.takeProfit = +params['takeProfit'];

    // If planId provided, load full plan data
    if (this.planId) {
      this.tradePlanService.getById(this.planId).subscribe({
        next: (tp) => {
          this.plan.symbol = tp.symbol;
          this.plan.direction = tp.direction as 'Buy' | 'Sell';
          this.plan.entryPrice = tp.entryPrice;
          this.plan.stopLoss = tp.stopLoss;
          this.plan.takeProfit = tp.target;
          this.plan.portfolioId = tp.portfolioId || '';
          this.plan.accountBalance = tp.accountBalance || 0;
          this.plan.riskPercent = tp.riskPercent || 2;
          if (tp.strategyId) {
            const strat = this.strategies.find(s => s.id === tp.strategyId);
            if (strat) this.selectStrategy(strat);
          }
          if (tp.portfolioId) this.onPortfolioChange();
          this.calculate();
          // Mark plan as InProgress
          this.tradePlanService.updateStatus(this.planId!, { status: 'inprogress' }).subscribe();
        },
        error: () => this.notificationService.error('Lỗi', 'Không thể tải kế hoạch')
      });
    }
  }

  // --- Data Loading ---

  loadStrategies(): void {
    this.loadingStrategies = true;
    this.strategyService.getAll().subscribe({
      next: (data) => {
        this.strategies = data.filter(s => s.isActive);
        this.loadingStrategies = false;
      },
      error: () => {
        this.loadingStrategies = false;
        this.notificationService.error('Lỗi', 'Không thể tải danh sách chiến lược');
      }
    });
  }

  loadPortfolios(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => {
        this.portfolios = data;
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không thể tải danh sách danh mục');
      }
    });
  }

  onPortfolioChange(): void {
    if (!this.plan.portfolioId) {
      this.riskProfile = null;
      return;
    }
    const portfolio = this.portfolios.find(p => p.id === this.plan.portfolioId);
    if (portfolio) {
      this.plan.accountBalance = portfolio.initialCapital;
    }
    this.riskService.getRiskProfile(this.plan.portfolioId).subscribe({
      next: (profile) => {
        this.riskProfile = profile;
        if (profile.maxPortfolioRiskPercent) {
          this.plan.riskPercent = profile.maxPortfolioRiskPercent;
        }
        this.calculate();
      },
      error: () => {
        this.riskProfile = null;
        this.calculate();
      }
    });
  }

  onSymbolBlur(): void {
    const symbol = this.plan.symbol?.trim().toUpperCase();
    if (!symbol) return;
    this.plan.symbol = symbol;
    this.loadingPrice = true;
    this.fetchedPrice = null;
    this.marketDataService.getCurrentPrice(symbol).subscribe({
      next: (data) => {
        this.loadingPrice = false;
        this.fetchedPrice = data.close;
        if (!this.plan.entryPrice) {
          this.plan.entryPrice = data.close;
          this.calculate();
        }
      },
      error: () => {
        this.loadingPrice = false;
      }
    });
  }

  // --- Step 1 ---

  selectStrategy(s: Strategy): void {
    this.selectedStrategy = this.selectedStrategy?.id === s.id ? null : s;
  }

  skipStrategy(): void {
    this.selectedStrategy = null;
    this.nextStep();
  }

  // --- Step 2: Calculation ---

  calculate(): void {
    const { entryPrice, stopLoss, takeProfit, accountBalance, riskPercent } = this.plan;
    if (!entryPrice || !stopLoss || !accountBalance || !riskPercent) {
      this.positionCalc = null;
      return;
    }

    const riskPerShare = Math.abs(entryPrice - stopLoss);
    if (riskPerShare === 0) {
      this.positionCalc = null;
      return;
    }

    const maxRiskAmount = accountBalance * (riskPercent / 100);
    const optimalShares = Math.floor(maxRiskAmount / riskPerShare);
    const positionValue = optimalShares * entryPrice;
    const positionPercent = (positionValue / accountBalance) * 100;

    const profitPerShare = takeProfit ? Math.abs(takeProfit - entryPrice) : 0;
    const riskRewardRatio = profitPerShare > 0 ? profitPerShare / riskPerShare : 0;
    const potentialProfit = optimalShares * profitPerShare;
    const potentialLoss = optimalShares * riskPerShare;

    let withinLimit = true;
    let warning = '';
    if (this.riskProfile && positionPercent > this.riskProfile.maxPositionSizePercent) {
      withinLimit = false;
      warning = `Vị thế chiếm ${positionPercent.toFixed(1)}% danh mục, vượt giới hạn ${this.riskProfile.maxPositionSizePercent}%`;
    }

    this.positionCalc = {
      maxRiskAmount,
      riskPerShare,
      optimalShares,
      positionValue,
      positionPercent,
      riskRewardRatio,
      potentialProfit,
      potentialLoss,
      withinLimit,
      warning
    };
  }

  // --- Step 3: Checklist ---

  isChecklistPassed(): boolean {
    return this.checklist.filter(i => i.critical).every(i => i.checked);
  }

  getCriticalRemaining(): number {
    return this.checklist.filter(i => i.critical && !i.checked).length;
  }

  getCheckedCount(): number {
    return this.checklist.filter(i => i.checked).length;
  }

  // --- Step 4: Record Trade ---

  canRecordTrade(): boolean {
    return !!(this.plan.portfolioId && this.plan.symbol && this.plan.entryPrice && this.positionCalc && this.positionCalc.optimalShares > 0);
  }

  getPortfolioName(): string {
    const p = this.portfolios.find(p => p.id === this.plan.portfolioId);
    return p ? p.name : '';
  }

  recordTrade(): void {
    if (!this.canRecordTrade() || !this.positionCalc) return;
    this.isRecording = true;

    const request: CreateTradeRequest = {
      portfolioId: this.plan.portfolioId,
      symbol: this.plan.symbol.toUpperCase(),
      tradeType: this.plan.direction,
      quantity: this.positionCalc.optimalShares,
      price: this.plan.entryPrice,
      fee: 0,
      tax: 0,
      tradeDate: new Date().toISOString()
    };

    this.tradeService.create(request).subscribe({
      next: (result) => {
        this.createdTradeId = result.id;
        this.isRecording = false;
        this.notificationService.success('Thành công', `Giao dịch ${this.plan.symbol.toUpperCase()} đã được ghi nhận`);

        // Pre-fill journal
        this.journal.entryReason = `${getTradeTypeDisplay(this.plan.direction)} ${this.plan.symbol.toUpperCase()} tại giá ${this.plan.entryPrice}`;
        if (this.selectedStrategy) {
          this.journal.technicalSetup = `Chiến lược: ${this.selectedStrategy.name}`;
        }

        // Link strategy if selected
        if (this.selectedStrategy && this.createdTradeId) {
          this.strategyService.linkTrade(this.selectedStrategy.id, this.createdTradeId).subscribe();
        }

        // Update or create trade plan with Executed status
        if (this.planId && this.createdTradeId && this.lotNumber) {
          // Execute specific lot
          this.tradePlanService.executeLot(this.planId, this.lotNumber, {
            tradeId: this.createdTradeId, actualPrice: this.plan.entryPrice
          }).subscribe();
        } else if (this.planId && this.createdTradeId) {
          this.tradePlanService.updateStatus(this.planId, { status: 'executed', tradeId: this.createdTradeId }).subscribe();
        } else if (this.createdTradeId) {
          // Auto-create plan from wizard data (fire-and-forget)
          this.tradePlanService.create({
            symbol: this.plan.symbol.toUpperCase(),
            direction: this.plan.direction,
            entryPrice: this.plan.entryPrice,
            stopLoss: this.plan.stopLoss,
            target: this.plan.takeProfit,
            quantity: this.positionCalc?.optimalShares || 0,
            portfolioId: this.plan.portfolioId || undefined,
            strategyId: this.selectedStrategy?.id || undefined,
            marketCondition: 'Trending',
            riskPercent: this.plan.riskPercent,
            accountBalance: this.plan.accountBalance,
            riskRewardRatio: this.positionCalc?.riskRewardRatio,
            confidenceLevel: 5,
            status: 'Executed',
            tradeId: this.createdTradeId
          }).subscribe();
        }

        // Set stop-loss target
        if (this.plan.stopLoss && this.plan.takeProfit && this.createdTradeId) {
          this.riskService.setStopLossTarget({
            tradeId: this.createdTradeId,
            portfolioId: this.plan.portfolioId,
            symbol: this.plan.symbol.toUpperCase(),
            entryPrice: this.plan.entryPrice,
            stopLossPrice: this.plan.stopLoss,
            targetPrice: this.plan.takeProfit
          }).subscribe();
        }

        // Auto-create journal entry with pre-filled data
        if (this.createdTradeId) {
          this.journalService.create({
            tradeId: this.createdTradeId,
            portfolioId: this.plan.portfolioId,
            entryReason: this.journal.entryReason || `${getTradeTypeDisplay(this.plan.direction)} ${this.plan.symbol.toUpperCase()} tại giá ${this.plan.entryPrice}`,
            technicalSetup: this.journal.technicalSetup || undefined,
            marketContext: this.journal.marketContext || undefined,
            emotionalState: this.journal.emotionalState || 'Calm',
            confidenceLevel: this.journal.confidenceLevel || 5,
          }).subscribe({
            next: () => {
              this.journalSaved = true;
            },
            error: () => {} // Non-blocking, user can still manually save in step 5
          });
        }
      },
      error: (err) => {
        this.isRecording = false;
        this.notificationService.error('Lỗi', 'Không thể ghi giao dịch. Vui lòng thử lại.');
      }
    });
  }

  // --- Step 5: Journal ---

  saveJournal(): void {
    if (!this.createdTradeId) return;
    this.isSavingJournal = true;

    const updateData = {
      entryReason: this.journal.entryReason || undefined,
      marketContext: this.journal.marketContext || undefined,
      technicalSetup: this.journal.technicalSetup || undefined,
      emotionalState: this.journal.emotionalState || undefined,
      confidenceLevel: this.journal.confidenceLevel || undefined,
    };

    // If journal was auto-created, update it; otherwise create new
    if (this.journalSaved) {
      this.journalService.getByTrade(this.createdTradeId).subscribe({
        next: (existing) => {
          this.journalService.update(existing.id, updateData).subscribe({
            next: () => {
              this.isSavingJournal = false;
              this.notificationService.success('Thành công', 'Nhật ký giao dịch đã được cập nhật');
            },
            error: () => {
              this.isSavingJournal = false;
              this.notificationService.error('Lỗi', 'Không thể cập nhật nhật ký');
            }
          });
        },
        error: () => {
          this.isSavingJournal = false;
        }
      });
    } else {
      this.journalService.create({
        tradeId: this.createdTradeId,
        portfolioId: this.plan.portfolioId,
        ...updateData
      }).subscribe({
        next: () => {
          this.journalSaved = true;
          this.isSavingJournal = false;
          this.notificationService.success('Thành công', 'Nhật ký giao dịch đã được lưu');
        },
        error: () => {
          this.isSavingJournal = false;
          this.notificationService.error('Lỗi', 'Không thể lưu nhật ký. Vui lòng thử lại.');
        }
      });
    }
  }

  goToDashboard(): void {
    if (this.createdTradeId && !this.journalSaved) {
      const save = confirm('Bạn chưa lưu nhật ký giao dịch. Bạn có muốn lưu trước khi rời không?');
      if (save) {
        this.isSavingJournal = true;
        const request: CreateJournalRequest = {
          tradeId: this.createdTradeId,
          portfolioId: this.plan.portfolioId,
          entryReason: this.journal.entryReason || undefined,
          marketContext: this.journal.marketContext || undefined,
          technicalSetup: this.journal.technicalSetup || undefined,
          emotionalState: this.journal.emotionalState || undefined,
          confidenceLevel: this.journal.confidenceLevel || undefined,
        };
        this.journalService.create(request).subscribe({
          next: () => {
            this.isSavingJournal = false;
            this.notificationService.success('Thành công', 'Nhật ký đã được lưu');
            this.router.navigate(['/dashboard']);
          },
          error: () => {
            this.isSavingJournal = false;
            this.notificationService.error('Lỗi', 'Không thể lưu nhật ký');
            this.router.navigate(['/dashboard']);
          }
        });
        return;
      }
    }
    this.router.navigate(['/dashboard']);
  }

  // --- Navigation ---

  canProceed(): boolean {
    switch (this.currentStep) {
      case 0: return true; // Strategy is optional
      case 1: return !!(this.plan.portfolioId && this.plan.symbol && this.plan.entryPrice && this.plan.stopLoss);
      case 2: return this.isChecklistPassed();
      case 3: return !!this.createdTradeId;
      default: return true;
    }
  }

  nextStep(): void {
    if (this.currentStep < this.steps.length - 1 && this.canProceed()) {
      this.currentStep++;
    }
  }

  prevStep(): void {
    if (this.currentStep > 0) {
      this.currentStep--;
    }
  }

  goToStep(step: number): void {
    if (step < this.currentStep) {
      this.currentStep = step;
    }
  }

  getStepCircleClass(index: number): string {
    if (index < this.currentStep) {
      return 'bg-green-500 text-white';
    }
    if (index === this.currentStep) {
      return 'bg-blue-600 text-white ring-4 ring-blue-100';
    }
    return 'bg-gray-200 text-gray-500';
  }
}
