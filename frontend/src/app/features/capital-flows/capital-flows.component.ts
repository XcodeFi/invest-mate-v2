import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { CapitalFlowService, CapitalFlowItem, CapitalFlowHistory, AdjustedReturn } from '../../core/services/capital-flow.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { PnlService, PortfolioPnL, OverallPnLSummary } from '../../core/services/pnl.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';

interface CapitalView {
  initialCapital: number;
  netCashFlow: number;
  currentCapital: number;
  marketValue: number;
  cashBalance: number;
  totalAssets: number;
  totalReturn: number;
  totalReturnPercent: number;
  absReturn: number;
  absReturnPercent: number;
  marketAllocationPercent: number;
  marketBarWidth: number;
  cashBarWidth: number;
  unrealizedPnL: number;
  realizedPnL: number;
}

@Component({
  selector: 'app-capital-flows',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe, NumMaskDirective],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Quản lý Dòng vốn</h1>

      <!-- Aggregate Hero: Tổng quan toàn bộ danh mục -->
      <div *ngIf="overallView" class="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-lg shadow border border-blue-100 p-6 mb-6">
        <div class="flex items-baseline justify-between flex-wrap gap-2 mb-1">
          <h2 class="text-sm font-semibold text-blue-900">Tổng quan ({{ portfolios.length }} danh mục)</h2>
          <span class="text-xs text-gray-600">So với vốn hiện tại {{ overallView.currentCapital | vndCurrency }}</span>
        </div>
        <div class="flex items-baseline gap-3 mb-4 flex-wrap">
          <span class="text-3xl font-bold text-gray-900">{{ overallView.totalAssets | vndCurrency }}</span>
          <span class="text-sm font-semibold" [ngClass]="overallView.totalReturn >= 0 ? 'text-green-600' : 'text-red-600'">
            {{ overallView.totalReturn >= 0 ? '↗ +' : '↘ −' }}{{ overallView.absReturn | vndCurrency }}
            ({{ overallView.totalReturn >= 0 ? '+' : '−' }}{{ overallView.absReturnPercent.toFixed(2) }}%)
          </span>
        </div>

        <div class="flex h-3 rounded-full overflow-hidden bg-white mb-3">
          <div class="bg-blue-500 transition-all" [style.width.%]="overallView.marketBarWidth"></div>
          <div class="bg-cyan-400 transition-all" [style.width.%]="overallView.cashBarWidth"></div>
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm mb-4">
          <div>
            <div class="flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-blue-500"></span>
              <span class="text-gray-700">Giá trị thị trường</span>
            </div>
            <div class="font-semibold text-gray-900 mt-0.5">
              {{ overallView.marketValue | vndCurrency }}
              <span class="text-xs text-gray-500 font-normal">({{ overallView.marketBarWidth.toFixed(1) }}%)</span>
            </div>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-cyan-400"></span>
              <span class="text-gray-700">Tiền mặt khả dụng</span>
            </div>
            <div class="font-semibold mt-0.5" [ngClass]="overallView.cashBalance >= 0 ? 'text-gray-900' : 'text-red-600'">
              {{ overallView.cashBalance | vndCurrency }}
              <span class="text-xs text-gray-500 font-normal">({{ overallView.cashBarWidth.toFixed(1) }}%)</span>
            </div>
          </div>
        </div>

        <div class="pt-4 border-t border-blue-200/60 grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
          <div>
            <div class="text-gray-600">Vốn ban đầu</div>
            <div class="font-medium text-gray-800 mt-0.5">{{ overallView.initialCapital | vndCurrency }}</div>
          </div>
          <div>
            <div class="text-gray-600">Dòng vốn ròng</div>
            <div class="font-medium mt-0.5" [ngClass]="overallView.netCashFlow >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ overallView.netCashFlow >= 0 ? '+' : '' }}{{ overallView.netCashFlow | vndCurrency }}
            </div>
          </div>
          <div>
            <div class="text-gray-600">Lãi/lỗ chưa TH</div>
            <div class="font-medium mt-0.5" [ngClass]="overallView.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ overallView.unrealizedPnL >= 0 ? '+' : '' }}{{ overallView.unrealizedPnL | vndCurrency }}
            </div>
          </div>
          <div>
            <div class="text-gray-600">Lãi/lỗ đã TH</div>
            <div class="font-medium mt-0.5" [ngClass]="overallView.realizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ overallView.realizedPnL >= 0 ? '+' : '' }}{{ overallView.realizedPnL | vndCurrency }}
            </div>
          </div>
        </div>
      </div>

      <!-- Portfolio Selector -->
      <div class="bg-white rounded-lg shadow p-4 mb-6">
        <div class="flex flex-wrap items-center gap-4">
          <label class="text-sm font-medium text-gray-700">Danh mục:</label>
          <select
            [(ngModel)]="selectedPortfolioId"
            (ngModelChange)="onPortfolioChange()"
            class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 min-w-[200px]">
            <option value="">-- Chọn danh mục --</option>
            <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }} ({{ p.currentCapital | vndCurrency }})</option>
          </select>
          <button
            *ngIf="selectedPortfolioId"
            (click)="showRecordForm = !showRecordForm"
            class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg font-medium transition-colors ml-auto">
            {{ showRecordForm ? 'Đóng' : '+ Ghi nhận dòng vốn' }}
          </button>
        </div>
      </div>

      <!-- Record Capital Flow Form -->
      <div *ngIf="showRecordForm && selectedPortfolioId" class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-4">Ghi nhận dòng vốn mới</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Loại</label>
            <select
              [(ngModel)]="newFlow.type"
              class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="Deposit">Nạp tiền</option>
              <option value="Withdraw">Rút tiền</option>
              <option value="Dividend">Cổ tức</option>
              <option value="Interest">Lãi suất</option>
              <option value="Fee">Phí</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Số tiền</label>
            <input
              type="text" inputmode="numeric" appNumMask
              [(ngModel)]="newFlow.amount"
              placeholder="0"
              class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Ngày</label>
            <input
              type="date"
              [(ngModel)]="newFlow.flowDate"
              class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Tiền tệ</label>
            <select
              [(ngModel)]="newFlow.currency"
              class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="VND">VND</option>
              <option value="USD">USD</option>
            </select>
          </div>
          <div class="md:col-span-2">
            <label class="block text-sm font-medium text-gray-700 mb-1">Ghi chú</label>
            <input
              type="text"
              [(ngModel)]="newFlow.note"
              placeholder="Ghi chú (tuỳ chọn)"
              class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          </div>
        </div>
        <div class="mt-4 flex justify-end">
          <button
            (click)="recordFlow()"
            [disabled]="saving"
            class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
            {{ saving ? 'Đang lưu...' : 'Lưu' }}
          </button>
        </div>
      </div>

      <!-- Per-portfolio Hero: Chi tiết danh mục đang chọn -->
      <div *ngIf="selectedPortfolioId && selectedPortfolio" class="bg-white rounded-lg shadow p-6 mb-6">
        <div class="flex items-baseline justify-between flex-wrap gap-2 mb-1">
          <h2 class="text-sm font-semibold text-gray-700">Chi tiết: {{ selectedPortfolio.name }}</h2>
          <span class="text-xs text-gray-500">So với vốn hiện tại {{ currentCapital | vndCurrency }}</span>
        </div>
        <div class="flex items-baseline gap-3 mb-4 flex-wrap">
          <span class="text-3xl font-bold text-gray-900">{{ totalAssets | vndCurrency }}</span>
          <span class="text-sm font-semibold" [ngClass]="totalReturn >= 0 ? 'text-green-600' : 'text-red-600'">
            {{ totalReturn >= 0 ? '↗ +' : '↘ −' }}{{ absTotalReturn | vndCurrency }}
            ({{ totalReturn >= 0 ? '+' : '−' }}{{ absTotalReturnPercent.toFixed(2) }}%)
          </span>
        </div>

        <!-- Allocation bar -->
        <div class="flex h-3 rounded-full overflow-hidden bg-gray-100 mb-3">
          <div class="bg-blue-500 transition-all" [style.width.%]="marketBarWidth"></div>
          <div class="bg-cyan-400 transition-all" [style.width.%]="cashBarWidth"></div>
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm mb-4">
          <div>
            <div class="flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-blue-500"></span>
              <span class="text-gray-600">Giá trị thị trường</span>
            </div>
            <div class="font-semibold text-gray-900 mt-0.5">
              {{ marketValue | vndCurrency }}
              <span class="text-xs text-gray-500 font-normal">({{ marketBarWidth.toFixed(1) }}%)</span>
            </div>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-cyan-400"></span>
              <span class="text-gray-600">Tiền mặt khả dụng</span>
            </div>
            <div class="font-semibold mt-0.5" [ngClass]="cashBalance >= 0 ? 'text-gray-900' : 'text-red-600'">
              {{ cashBalance | vndCurrency }}
              <span class="text-xs text-gray-500 font-normal">({{ cashBarWidth.toFixed(1) }}%)</span>
            </div>
          </div>
        </div>

        <!-- Breakdown -->
        <div class="pt-4 border-t border-gray-100 grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
          <div>
            <div class="text-gray-500">Vốn ban đầu</div>
            <div class="font-medium text-gray-700 mt-0.5">{{ selectedPortfolio.initialCapital | vndCurrency }}</div>
          </div>
          <div>
            <div class="text-gray-500">Dòng vốn ròng</div>
            <div class="font-medium mt-0.5" [ngClass]="selectedPortfolio.netCashFlow >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ selectedPortfolio.netCashFlow >= 0 ? '+' : '' }}{{ selectedPortfolio.netCashFlow | vndCurrency }}
            </div>
          </div>
          <div>
            <div class="text-gray-500">Lãi/lỗ chưa TH</div>
            <div class="font-medium mt-0.5" [ngClass]="(portfolioPnL?.totalUnrealizedPnL || 0) >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ (portfolioPnL?.totalUnrealizedPnL || 0) >= 0 ? '+' : '' }}{{ (portfolioPnL?.totalUnrealizedPnL || 0) | vndCurrency }}
            </div>
          </div>
          <div>
            <div class="text-gray-500">Lãi/lỗ đã TH</div>
            <div class="font-medium mt-0.5" [ngClass]="(portfolioPnL?.totalRealizedPnL || 0) >= 0 ? 'text-green-600' : 'text-red-600'">
              {{ (portfolioPnL?.totalRealizedPnL || 0) >= 0 ? '+' : '' }}{{ (portfolioPnL?.totalRealizedPnL || 0) | vndCurrency }}
            </div>
          </div>
        </div>
      </div>

      <!-- Summary & Return Cards -->
      <div *ngIf="selectedPortfolioId" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-sm text-gray-500">Tổng nạp</div>
          <div class="text-xl font-bold text-green-600">{{ (flowHistory?.totalDeposits || 0) | vndCurrency }}</div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-sm text-gray-500">Tổng rút</div>
          <div class="text-xl font-bold text-red-600">{{ (flowHistory?.totalWithdrawals || 0) | vndCurrency }}</div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-sm text-gray-500">Cổ tức nhận</div>
          <div class="text-xl font-bold text-blue-600">{{ (flowHistory?.totalDividends || 0) | vndCurrency }}</div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-sm text-gray-500">Dòng vốn ròng</div>
          <div class="text-xl font-bold" [ngClass]="(flowHistory?.netCashFlow || 0) >= 0 ? 'text-green-600' : 'text-red-600'">
            {{ (flowHistory?.netCashFlow || 0) | vndCurrency }}
          </div>
        </div>
      </div>

      <!-- TWR / MWR Cards -->
      <div *ngIf="adjustedReturn" class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-sm text-gray-500 mb-1">Lợi suất theo thời gian (TWR)</div>
          <div class="text-2xl font-bold" [ngClass]="adjustedReturn.timeWeightedReturn >= 0 ? 'text-green-600' : 'text-red-600'">
            {{ (adjustedReturn.timeWeightedReturn * 100).toFixed(2) }}%
          </div>
          <div class="text-xs text-gray-400 mt-1">Loại bỏ ảnh hưởng của dòng vốn vào/ra</div>
        </div>
        <div class="bg-white rounded-lg shadow p-4">
          <div class="text-sm text-gray-500 mb-1">Lợi suất theo tiền (MWR / IRR)</div>
          <div class="text-2xl font-bold" [ngClass]="adjustedReturn.moneyWeightedReturn >= 0 ? 'text-green-600' : 'text-red-600'">
            {{ (adjustedReturn.moneyWeightedReturn * 100).toFixed(2) }}%
          </div>
          <div class="text-xs text-gray-400 mt-1">Phản ánh thời điểm và quy mô dòng vốn</div>
        </div>
      </div>

      <!-- Flow History Table -->
      <div *ngIf="selectedPortfolioId" class="bg-white rounded-lg shadow p-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-4">Lịch sử dòng vốn</h2>

        <div *ngIf="loading" class="text-center text-gray-500 py-8">Đang tải...</div>

        <div *ngIf="!loading && flowHistory?.flows?.length === 0" class="text-center text-gray-500 py-8">
          Chưa có dòng vốn nào được ghi nhận
        </div>

        <div *ngIf="!loading && flowHistory && flowHistory.flows.length > 0">
          <div class="overflow-x-auto hidden md:block">
            <table class="min-w-full table-auto">
              <thead>
                <tr class="bg-gray-50 border-b">
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ngày</th>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Loại</th>
                  <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Số tiền</th>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Tiền tệ</th>
                  <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ghi chú</th>
                  <th class="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase">Thao tác</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let flow of flowHistory.flows" class="border-b hover:bg-gray-50" [class.bg-blue-50]="flow.isSeedDeposit">
                  <td class="px-4 py-3 text-sm">{{ flow.flowDate | date:'dd/MM/yyyy' }}</td>
                  <td class="px-4 py-3 text-sm">
                    <span class="px-2 py-1 rounded text-xs font-medium"
                      [ngClass]="flow.isSeedDeposit ? 'bg-blue-100 text-blue-800' : getFlowTypeBadge(flow.type)">
                      {{ flow.isSeedDeposit ? 'Vốn ban đầu' : getFlowTypeLabel(flow.type) }}
                    </span>
                  </td>
                  <td class="px-4 py-3 text-sm text-right font-semibold"
                    [ngClass]="isInflow(flow.type) ? 'text-green-600' : 'text-red-600'">
                    {{ isInflow(flow.type) ? '+' : '-' }}{{ flow.amount | vndCurrency }}
                  </td>
                  <td class="px-4 py-3 text-sm">{{ flow.currency }}</td>
                  <td class="px-4 py-3 text-sm text-gray-500">{{ flow.note || '-' }}</td>
                  <td class="px-4 py-3 text-center">
                    <button *ngIf="!flow.isSeedDeposit"
                      (click)="deleteFlow(flow.id)"
                      class="text-red-500 hover:text-red-700 text-sm font-medium">
                      Xoá
                    </button>
                    <span *ngIf="flow.isSeedDeposit" class="text-xs text-gray-400">Khoá</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Mobile Cards -->
          <div class="md:hidden divide-y divide-gray-200">
            <div *ngFor="let flow of flowHistory.flows" class="p-4 space-y-2" [class.bg-blue-50]="flow.isSeedDeposit">
              <div class="flex items-center justify-between">
                <span class="px-2 py-1 rounded text-xs font-medium"
                  [ngClass]="flow.isSeedDeposit ? 'bg-blue-100 text-blue-800' : getFlowTypeBadge(flow.type)">
                  {{ flow.isSeedDeposit ? 'Vốn ban đầu' : getFlowTypeLabel(flow.type) }}
                </span>
                <span class="text-sm text-gray-500">{{ flow.flowDate | date:'dd/MM/yyyy' }}</span>
              </div>
              <div class="flex items-center justify-between">
                <span class="text-base font-semibold"
                  [ngClass]="isInflow(flow.type) ? 'text-green-600' : 'text-red-600'">
                  {{ isInflow(flow.type) ? '+' : '-' }}{{ flow.amount | vndCurrency }}
                </span>
                <span class="text-sm text-gray-500">{{ flow.currency }}</span>
              </div>
              <div *ngIf="flow.note" class="text-sm text-gray-500">{{ flow.note }}</div>
              <div class="flex justify-end pt-1">
                <button *ngIf="!flow.isSeedDeposit"
                  (click)="deleteFlow(flow.id)"
                  class="text-red-500 hover:text-red-700 text-sm font-medium">
                  Xoá
                </button>
                <span *ngIf="flow.isSeedDeposit" class="text-xs text-gray-400">Khoá</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div *ngIf="!selectedPortfolioId" class="bg-white rounded-lg shadow p-12 text-center">
        <div class="text-gray-400 text-lg mb-2">Vui lòng chọn danh mục đầu tư</div>
        <div class="text-gray-400 text-sm">Chọn một danh mục ở phía trên để xem và quản lý dòng vốn</div>
      </div>
    </div>
  `
})
export class CapitalFlowsComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  flowHistory: CapitalFlowHistory | null = null;
  adjustedReturn: AdjustedReturn | null = null;
  portfolioPnL: PortfolioPnL | null = null;
  overallSummary: OverallPnLSummary | null = null;
  loading = false;
  saving = false;
  showRecordForm = false;

  get selectedPortfolio(): PortfolioSummary | undefined {
    return this.portfolios.find(p => p.id === this.selectedPortfolioId);
  }

  get currentCapital(): number {
    return this.selectedPortfolio?.currentCapital || 0;
  }

  get marketValue(): number {
    return this.portfolioPnL?.totalMarketValue || 0;
  }

  get cashBalance(): number {
    const p = this.selectedPortfolio;
    if (!p) return 0;
    return p.currentCapital - p.totalInvested + p.totalSold;
  }

  get totalAssets(): number {
    return this.cashBalance + this.marketValue;
  }

  get totalReturn(): number {
    return this.totalAssets - this.currentCapital;
  }

  get totalReturnPercent(): number {
    return this.currentCapital > 0 ? (this.totalReturn / this.currentCapital) * 100 : 0;
  }

  get marketAllocationPercent(): number {
    return this.totalAssets > 0 ? (this.marketValue / this.totalAssets) * 100 : 0;
  }

  // Clamped widths for allocation bar — avoid overflow / negative when
  // cashBalance is negative (marketAllocation can exceed 100%).
  get marketBarWidth(): number {
    return Math.max(0, Math.min(100, this.marketAllocationPercent));
  }

  get cashBarWidth(): number {
    return 100 - this.marketBarWidth;
  }

  get absTotalReturn(): number {
    return Math.abs(this.totalReturn);
  }

  get absTotalReturnPercent(): number {
    return Math.abs(this.totalReturnPercent);
  }

  get overallView(): CapitalView | null {
    const s = this.overallSummary;
    if (!s || this.portfolios.length === 0) return null;
    // Sum historical gross buys/sells from PortfolioSummary (not s.totalInvested,
    // which is cost basis of currently open positions — diverges from gross once
    // any position is closed).
    const totalSold = this.portfolios.reduce((sum, p) => sum + p.totalSold, 0);
    const totalInvested = this.portfolios.reduce((sum, p) => sum + p.totalInvested, 0);
    const currentCapital = s.totalCurrentCapital;
    const marketValue = s.totalMarketValue;
    const cashBalance = currentCapital - totalInvested + totalSold;
    const totalAssets = cashBalance + marketValue;
    const totalReturn = totalAssets - currentCapital;
    const totalReturnPercent = currentCapital > 0 ? (totalReturn / currentCapital) * 100 : 0;
    const marketPct = totalAssets > 0 ? (marketValue / totalAssets) * 100 : 0;
    const marketBar = Math.max(0, Math.min(100, marketPct));
    return {
      initialCapital: s.totalInitialCapital,
      netCashFlow: s.totalNetCashFlow,
      currentCapital,
      marketValue,
      cashBalance,
      totalAssets,
      totalReturn,
      totalReturnPercent,
      absReturn: Math.abs(totalReturn),
      absReturnPercent: Math.abs(totalReturnPercent),
      marketAllocationPercent: marketPct,
      marketBarWidth: marketBar,
      cashBarWidth: 100 - marketBar,
      unrealizedPnL: s.totalUnrealizedPnL,
      realizedPnL: s.totalRealizedPnL
    };
  }

  newFlow = {
    type: 'Deposit',
    amount: 0,
    currency: 'VND',
    note: '',
    flowDate: new Date().toISOString().split('T')[0]
  };

  constructor(
    private capitalFlowService: CapitalFlowService,
    private portfolioService: PortfolioService,
    private pnlService: PnlService,
    private notificationService: NotificationService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.loadPortfolios();
    this.loadOverallSummary();
  }

  loadOverallSummary(): void {
    this.pnlService.getSummary().subscribe({
      next: data => this.overallSummary = data,
      error: () => this.overallSummary = null
    });
  }

  loadPortfolios(): void {
    this.portfolioService.getAll().subscribe({
      next: data => {
        this.portfolios = data;
        // Only auto-select from query param on initial load. On refreshes
        // after flow mutations, skip — portfolio is already selected and we
        // don't want to re-trigger loadFlowData() (already done by caller).
        if (this.selectedPortfolioId) return;
        const queryPortfolioId = this.route.snapshot.queryParamMap.get('portfolioId');
        if (queryPortfolioId && data.some(p => p.id === queryPortfolioId)) {
          this.selectedPortfolioId = queryPortfolioId;
          this.onPortfolioChange();
        }
      },
      error: () => this.notificationService.error('Lỗi', 'Lỗi khi tải danh sách danh mục')
    });
  }

  onPortfolioChange(): void {
    if (this.selectedPortfolioId) {
      // Clear stale data from previous portfolio so hero card doesn't mix
      // new portfolio's currentCapital with old portfolio's marketValue.
      this.flowHistory = null;
      this.adjustedReturn = null;
      this.portfolioPnL = null;
      this.loadFlowData();
    } else {
      this.flowHistory = null;
      this.adjustedReturn = null;
      this.portfolioPnL = null;
    }
  }

  loadFlowData(): void {
    this.loading = true;
    this.capitalFlowService.getFlowHistory(this.selectedPortfolioId).subscribe({
      next: data => {
        this.flowHistory = data;
        this.loading = false;
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi tải lịch sử dòng vốn');
        this.loading = false;
      }
    });

    this.capitalFlowService.getTimeWeightedReturn(this.selectedPortfolioId).subscribe({
      next: data => this.adjustedReturn = data,
      error: () => {} // Silently fail
    });

    this.pnlService.getPortfolioPnL(this.selectedPortfolioId).subscribe({
      next: data => this.portfolioPnL = data,
      error: () => this.portfolioPnL = null // portfolio with no trades throws; treat as zero
    });
  }

  recordFlow(): void {
    if (this.newFlow.amount <= 0) {
      this.notificationService.error('Lỗi', 'Số tiền phải lớn hơn 0');
      return;
    }

    this.saving = true;
    this.capitalFlowService.recordFlow({
      portfolioId: this.selectedPortfolioId,
      type: this.newFlow.type,
      amount: this.newFlow.amount,
      currency: this.newFlow.currency,
      note: this.newFlow.note || undefined,
      flowDate: this.newFlow.flowDate || undefined
    }).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Đã ghi nhận dòng vốn thành công');
        this.saving = false;
        this.showRecordForm = false;
        this.resetForm();
        this.loadFlowData();
        this.loadPortfolios();
        this.loadOverallSummary();
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi ghi nhận dòng vốn');
        this.saving = false;
      }
    });
  }

  deleteFlow(id: string): void {
    if (!confirm('Bạn có chắc muốn xoá dòng vốn này?')) return;
    this.capitalFlowService.deleteFlow(id).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Đã xoá dòng vốn');
        this.loadFlowData();
        this.loadPortfolios();
        this.loadOverallSummary();
      },
      error: () => this.notificationService.error('Lỗi', 'Lỗi khi xoá dòng vốn')
    });
  }

  resetForm(): void {
    this.newFlow = {
      type: 'Deposit',
      amount: 0,
      currency: 'VND',
      note: '',
      flowDate: new Date().toISOString().split('T')[0]
    };
  }

  getFlowTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      'Deposit': 'Nạp tiền',
      'Withdraw': 'Rút tiền',
      'Dividend': 'Cổ tức',
      'Interest': 'Lãi suất',
      'Fee': 'Phí'
    };
    return labels[type] || type;
  }

  getFlowTypeBadge(type: string): string {
    const badges: Record<string, string> = {
      'Deposit': 'bg-green-100 text-green-800',
      'Withdraw': 'bg-red-100 text-red-800',
      'Dividend': 'bg-blue-100 text-blue-800',
      'Interest': 'bg-yellow-100 text-yellow-800',
      'Fee': 'bg-gray-100 text-gray-800'
    };
    return badges[type] || 'bg-gray-100 text-gray-800';
  }

  isInflow(type: string): boolean {
    return ['Deposit', 'Dividend', 'Interest'].includes(type);
  }

}
