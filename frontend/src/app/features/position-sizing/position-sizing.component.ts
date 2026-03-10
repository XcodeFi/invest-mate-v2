import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { RiskService, RiskProfile } from '../../core/services/risk.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

interface PositionSizeResult {
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
  selector: 'app-position-sizing',
  standalone: true,
  imports: [CommonModule, FormsModule, VndCurrencyPipe],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Tinh toan vi the (Position Sizing)</h1>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Input Form -->
        <div class="bg-white rounded-lg shadow p-6">
          <h2 class="text-lg font-semibold mb-4">Thong tin giao dich</h2>

          <!-- Portfolio selector -->
          <div class="mb-4">
            <label class="block text-sm font-medium text-gray-700 mb-1">Danh muc</label>
            <select [(ngModel)]="selectedPortfolioId" (ngModelChange)="onPortfolioChange()"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chon danh muc --</option>
              <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }} ({{ p.initialCapital | vndCurrency }})</option>
            </select>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Tong gia tri danh muc (VND)</label>
              <input [(ngModel)]="input.accountBalance" type="number"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">% Rui ro toi da / GD</label>
              <input [(ngModel)]="input.riskPercent" type="number" step="0.5" min="0.5" max="10"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Gia vao lenh (VND)</label>
              <input [(ngModel)]="input.entryPrice" type="number"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="VD: 25000">
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Gia stop-loss (VND)</label>
              <input [(ngModel)]="input.stopLossPrice" type="number"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="VD: 23000">
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Gia chot loi (VND)</label>
              <input [(ngModel)]="input.targetPrice" type="number"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="VD: 30000">
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">% Vi the toi da</label>
              <input [(ngModel)]="input.maxPositionPercent" type="number" step="1" min="1" max="100"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
            </div>
          </div>

          <!-- Risk profile auto-fill -->
          <div *ngIf="riskProfile" class="mt-4 bg-blue-50 rounded-lg p-3 text-sm">
            <div class="font-medium text-blue-700 mb-1">Risk Profile tu danh muc</div>
            <div class="text-blue-600 grid grid-cols-2 gap-1">
              <span>Max Position: {{ riskProfile.maxPositionSizePercent }}%</span>
              <span>Max Risk: {{ riskProfile.maxPortfolioRiskPercent }}%</span>
              <span>R:R muc tieu: {{ riskProfile.defaultRiskRewardRatio }}</span>
              <span>Max Drawdown Alert: {{ riskProfile.maxDrawdownAlertPercent }}%</span>
            </div>
          </div>

          <button (click)="calculate()"
            [disabled]="!input.entryPrice || !input.stopLossPrice"
            class="mt-4 w-full px-4 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 font-medium">
            Tinh toan
          </button>
        </div>

        <!-- Result -->
        <div class="bg-white rounded-lg shadow p-6">
          <h2 class="text-lg font-semibold mb-4">Ket qua</h2>

          <div *ngIf="!result" class="text-center py-12 text-gray-400">
            Nhap thong tin va nhan "Tinh toan"
          </div>

          <div *ngIf="result">
            <!-- Warning -->
            <div *ngIf="result.warning" class="mb-4 bg-yellow-50 border border-yellow-200 rounded-lg p-3">
              <div class="text-yellow-700 text-sm font-medium">Canh bao</div>
              <div class="text-yellow-600 text-sm">{{ result.warning }}</div>
            </div>

            <!-- Main result -->
            <div class="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-lg p-6 mb-4 text-center">
              <div class="text-sm text-blue-600 font-medium">So luong co phieu toi uu</div>
              <div class="text-4xl font-bold text-blue-800 my-2">{{ result.optimalShares | number }}</div>
              <div class="text-sm text-blue-500">Lo {{ getLotCount() }} lo (x100 cp)</div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div class="border rounded-lg p-3">
                <div class="text-xs text-gray-500">Gia tri vi the</div>
                <div class="font-bold text-gray-800">{{ result.positionValue | vndCurrency }}</div>
                <div class="text-xs text-gray-400">{{ result.positionPercent | number:'1.1-1' }}% danh muc</div>
              </div>
              <div class="border rounded-lg p-3">
                <div class="text-xs text-gray-500">Tien rui ro toi da</div>
                <div class="font-bold text-red-600">{{ result.maxRiskAmount | vndCurrency }}</div>
                <div class="text-xs text-gray-400">{{ input.riskPercent }}% cua {{ input.accountBalance | vndCurrency }}</div>
              </div>
              <div class="border rounded-lg p-3">
                <div class="text-xs text-gray-500">Rui ro / co phieu</div>
                <div class="font-bold text-red-600">{{ result.riskPerShare | vndCurrency }}</div>
              </div>
              <div class="border rounded-lg p-3">
                <div class="text-xs text-gray-500">R:R Ratio</div>
                <div class="font-bold" [class.text-green-600]="result.riskRewardRatio >= 2"
                  [class.text-yellow-600]="result.riskRewardRatio >= 1 && result.riskRewardRatio < 2"
                  [class.text-red-600]="result.riskRewardRatio < 1">
                  1 : {{ result.riskRewardRatio | number:'1.2-2' }}
                </div>
              </div>
              <div class="border rounded-lg p-3 border-green-200 bg-green-50">
                <div class="text-xs text-green-600">Loi nhuan tiem nang</div>
                <div class="font-bold text-green-700">+{{ result.potentialProfit | vndCurrency }}</div>
              </div>
              <div class="border rounded-lg p-3 border-red-200 bg-red-50">
                <div class="text-xs text-red-600">Lo tiem nang</div>
                <div class="font-bold text-red-700">-{{ result.potentialLoss | vndCurrency }}</div>
              </div>
            </div>

            <!-- Status -->
            <div class="mt-4 p-3 rounded-lg text-center text-sm font-medium"
              [class.bg-green-100]="result.withinLimit" [class.text-green-700]="result.withinLimit"
              [class.bg-red-100]="!result.withinLimit" [class.text-red-700]="!result.withinLimit">
              {{ result.withinLimit ? 'Vi the nam trong gioi han rui ro cho phep' : 'Vi the VUOT gioi han rui ro!' }}
            </div>
          </div>
        </div>
      </div>

      <!-- Quick Reference Table -->
      <div class="mt-6 bg-white rounded-lg shadow p-6">
        <h2 class="text-lg font-semibold mb-4">Bang tham chieu nhanh</h2>
        <p class="text-sm text-gray-500 mb-3">So co phieu toi da theo cac muc rui ro khac nhau (voi gia hien tai)</p>
        <div *ngIf="input.entryPrice && input.stopLossPrice" class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-2 text-left text-xs text-gray-500">% Rui ro</th>
                <th class="px-4 py-2 text-right text-xs text-gray-500">Tien rui ro</th>
                <th class="px-4 py-2 text-right text-xs text-gray-500">So CP</th>
                <th class="px-4 py-2 text-right text-xs text-gray-500">Gia tri vi the</th>
                <th class="px-4 py-2 text-right text-xs text-gray-500">% Danh muc</th>
              </tr>
            </thead>
            <tbody class="divide-y">
              <tr *ngFor="let row of quickRefTable" class="hover:bg-gray-50"
                [class.bg-blue-50]="row.riskPercent === input.riskPercent">
                <td class="px-4 py-2 font-medium">{{ row.riskPercent }}%</td>
                <td class="px-4 py-2 text-right">{{ row.riskAmount | vndCurrency }}</td>
                <td class="px-4 py-2 text-right font-bold">{{ row.shares | number }}</td>
                <td class="px-4 py-2 text-right">{{ row.value | vndCurrency }}</td>
                <td class="px-4 py-2 text-right">{{ row.portfolioPercent | number:'1.1-1' }}%</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="!input.entryPrice || !input.stopLossPrice" class="text-center py-4 text-gray-400">
          Nhap gia vao va stop-loss de xem bang tham chieu
        </div>
      </div>
    </div>
  `
})
export class PositionSizingComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  riskProfile: RiskProfile | null = null;
  result: PositionSizeResult | null = null;

  input = {
    accountBalance: 100000000,
    riskPercent: 2,
    entryPrice: 0,
    stopLossPrice: 0,
    targetPrice: 0,
    maxPositionPercent: 20
  };

  quickRefTable: { riskPercent: number; riskAmount: number; shares: number; value: number; portfolioPercent: number }[] = [];

  constructor(
    private portfolioService: PortfolioService,
    private riskService: RiskService
  ) {}

  ngOnInit(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => this.portfolios = data
    });
  }

  onPortfolioChange(): void {
    if (!this.selectedPortfolioId) { this.riskProfile = null; return; }
    const portfolio = this.portfolios.find(p => p.id === this.selectedPortfolioId);
    if (portfolio) this.input.accountBalance = portfolio.initialCapital;

    this.riskService.getRiskProfile(this.selectedPortfolioId).subscribe({
      next: (profile) => {
        this.riskProfile = profile;
        this.input.maxPositionPercent = profile.maxPositionSizePercent;
        this.input.riskPercent = profile.maxPortfolioRiskPercent;
      },
      error: () => this.riskProfile = null
    });
  }

  calculate(): void {
    const { accountBalance, riskPercent, entryPrice, stopLossPrice, targetPrice, maxPositionPercent } = this.input;
    if (!entryPrice || !stopLossPrice || entryPrice <= 0) return;

    const riskPerShare = Math.abs(entryPrice - stopLossPrice);
    if (riskPerShare === 0) return;

    const maxRiskAmount = accountBalance * (riskPercent / 100);
    let optimalShares = Math.floor(maxRiskAmount / riskPerShare);

    // Round down to nearest lot (100 shares)
    optimalShares = Math.floor(optimalShares / 100) * 100;
    if (optimalShares < 100) optimalShares = 100;

    const positionValue = optimalShares * entryPrice;
    const positionPercent = (positionValue / accountBalance) * 100;
    const maxPositionValue = accountBalance * (maxPositionPercent / 100);

    let warning = '';
    if (positionValue > maxPositionValue) {
      const cappedShares = Math.floor(maxPositionValue / entryPrice / 100) * 100;
      warning = `Vi the (${positionPercent.toFixed(1)}%) vuot gioi han ${maxPositionPercent}%. Nen giam xuong ${cappedShares} co phieu.`;
    }

    const riskRewardRatio = targetPrice > 0 ? (targetPrice - entryPrice) / riskPerShare : 0;
    const potentialProfit = targetPrice > 0 ? optimalShares * (targetPrice - entryPrice) : 0;
    const potentialLoss = optimalShares * riskPerShare;

    this.result = {
      maxRiskAmount, riskPerShare, optimalShares, positionValue, positionPercent,
      riskRewardRatio, potentialProfit, potentialLoss,
      withinLimit: positionPercent <= maxPositionPercent,
      warning
    };

    // Build quick ref table
    this.quickRefTable = [0.5, 1, 1.5, 2, 3, 5].map(pct => {
      const risk = accountBalance * (pct / 100);
      let shares = Math.floor(risk / riskPerShare / 100) * 100;
      if (shares < 100) shares = 100;
      const value = shares * entryPrice;
      return { riskPercent: pct, riskAmount: risk, shares, value, portfolioPercent: (value / accountBalance) * 100 };
    });
  }

  getLotCount(): number {
    return this.result ? Math.floor(this.result.optimalShares / 100) : 0;
  }
}
