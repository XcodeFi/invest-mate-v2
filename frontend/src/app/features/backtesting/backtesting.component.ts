import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BacktestService, BacktestSummary, BacktestDetail, SimulatedTrade } from '../../core/services/backtest.service';
import { StrategyService, Strategy } from '../../core/services/strategy.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';
import { getTradeTypeDisplay, getTradeTypeClass } from '../../shared/constants/trade-types';

@Component({
  selector: 'app-backtesting',
  standalone: true,
  imports: [CommonModule, FormsModule, VndCurrencyPipe, NumMaskDirective],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-2xl font-bold text-gray-800">Backtest Chiến lược</h1>
        <button (click)="showRunForm = !showRunForm"
          class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition">
          {{ showRunForm ? 'Đóng' : '+ Chạy Backtest' }}
        </button>
      </div>

      <!-- Run Backtest Form -->
      <div *ngIf="showRunForm" class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold mb-4">Chạy Backtest mới</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Chiến lược *</label>
            <select [(ngModel)]="newBacktest.strategyId"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn chiến lược --</option>
              <option *ngFor="let s of strategies" [value]="s.id">{{ s.name }}</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Tên backtest *</label>
            <input [(ngModel)]="newBacktest.name" type="text"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="VD: Test MA Crossover Q1 2025">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Vốn ban đầu (VND) *</label>
            <input [(ngModel)]="newBacktest.initialCapital" type="text" inputmode="numeric" appNumMask
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="100.000.000">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Ngày bắt đầu *</label>
            <input [(ngModel)]="newBacktest.startDate" type="date"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Ngày kết thúc *</label>
            <input [(ngModel)]="newBacktest.endDate" type="date"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          </div>
        </div>
        <div class="mt-4 flex justify-end gap-2">
          <button (click)="showRunForm = false"
            class="px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-50">Hủy</button>
          <button (click)="runBacktest()"
            [disabled]="!newBacktest.strategyId || !newBacktest.name || !newBacktest.startDate || !newBacktest.endDate || running"
            class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50">
            {{ running ? 'Đang gửi...' : 'Chạy Backtest' }}
          </button>
        </div>
      </div>

      <!-- Backtest List -->
      <div class="bg-white rounded-lg shadow">
        <div class="border-b border-gray-200 px-6 py-4">
          <h2 class="text-lg font-semibold">Lịch sử Backtest</h2>
        </div>
        <div *ngIf="loading" class="text-center py-8 text-gray-500">Đang tải...</div>
        <div *ngIf="!loading && backtests.length === 0" class="text-center py-8 text-gray-500">
          Chưa có backtest nào. Hãy chạy backtest đầu tiên!
        </div>
        <div class="divide-y divide-gray-200">
          <div *ngFor="let bt of backtests"
            class="px-6 py-4 hover:bg-gray-50 cursor-pointer transition"
            (click)="selectBacktest(bt)">
            <div class="flex justify-between items-center">
              <div>
                <div class="flex items-center gap-2">
                  <h3 class="font-semibold text-gray-800">{{ bt.name }}</h3>
                  <span class="px-2 py-0.5 text-xs rounded-full"
                    [class.bg-green-100]="bt.status === 'Completed'"
                    [class.text-green-700]="bt.status === 'Completed'"
                    [class.bg-yellow-100]="bt.status === 'Pending' || bt.status === 'Running'"
                    [class.text-yellow-700]="bt.status === 'Pending' || bt.status === 'Running'"
                    [class.bg-red-100]="bt.status === 'Failed'"
                    [class.text-red-700]="bt.status === 'Failed'">
                    {{ getStatusLabel(bt.status) }}
                  </span>
                </div>
                <div class="text-sm text-gray-500 mt-1">
                  {{ bt.startDate | date:'dd/MM/yyyy' }} - {{ bt.endDate | date:'dd/MM/yyyy' }}
                  | Von: {{ bt.initialCapital | vndCurrency }}
                </div>
              </div>
              <div class="text-sm text-gray-400">{{ bt.createdAt | date:'dd/MM/yyyy HH:mm' }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Backtest Detail -->
      <div *ngIf="selectedDetail" class="mt-6">
        <div class="bg-white rounded-lg shadow p-6">
          <div class="flex justify-between items-center mb-6">
            <h2 class="text-xl font-bold text-gray-800">{{ selectedDetail.name }}</h2>
            <button (click)="selectedDetail = null" class="text-gray-400 hover:text-gray-600">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>

          <!-- Tabs -->
          <div class="flex gap-2 mb-6 border-b">
            <button (click)="detailTab = 'metrics'" [class.border-blue-500]="detailTab === 'metrics'"
              [class.text-blue-600]="detailTab === 'metrics'" [class.border-transparent]="detailTab !== 'metrics'"
              class="py-2 px-4 border-b-2 text-sm font-medium">Kết quả</button>
            <button (click)="detailTab = 'equity'" [class.border-blue-500]="detailTab === 'equity'"
              [class.text-blue-600]="detailTab === 'equity'" [class.border-transparent]="detailTab !== 'equity'"
              class="py-2 px-4 border-b-2 text-sm font-medium">Equity Curve</button>
            <button (click)="detailTab = 'trades'" [class.border-blue-500]="detailTab === 'trades'"
              [class.text-blue-600]="detailTab === 'trades'" [class.border-transparent]="detailTab !== 'trades'"
              class="py-2 px-4 border-b-2 text-sm font-medium">Giao dịch</button>
          </div>

          <!-- Metrics Tab -->
          <div *ngIf="detailTab === 'metrics' && selectedDetail.result">
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
              <div class="bg-gradient-to-br from-blue-50 to-blue-100 rounded-lg p-4 text-center">
                <div class="text-xs text-blue-600 font-medium">Giá trị cuối</div>
                <div class="text-xl font-bold text-blue-800">{{ selectedDetail.result.finalValue | vndCurrency }}</div>
              </div>
              <div class="bg-gradient-to-br from-green-50 to-green-100 rounded-lg p-4 text-center">
                <div class="text-xs text-green-600 font-medium">Tổng lợi nhuận</div>
                <div class="text-xl font-bold" [class.text-green-800]="selectedDetail.result.totalReturn >= 0"
                  [class.text-red-800]="selectedDetail.result.totalReturn < 0">
                  {{ selectedDetail.result.totalReturn | number:'1.2-2' }}%
                </div>
              </div>
              <div class="bg-gradient-to-br from-purple-50 to-purple-100 rounded-lg p-4 text-center">
                <div class="text-xs text-purple-600 font-medium">CAGR</div>
                <div class="text-xl font-bold text-purple-800">{{ selectedDetail.result.cagr | number:'1.2-2' }}%</div>
              </div>
              <div class="bg-gradient-to-br from-orange-50 to-orange-100 rounded-lg p-4 text-center">
                <div class="text-xs text-orange-600 font-medium">Sharpe Ratio</div>
                <div class="text-xl font-bold text-orange-800">{{ selectedDetail.result.sharpeRatio | number:'1.2-2' }}</div>
              </div>
            </div>
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div class="border rounded-lg p-3 text-center">
                <div class="text-xs text-gray-500">Max Drawdown</div>
                <div class="text-lg font-bold text-red-600">{{ selectedDetail.result.maxDrawdown | number:'1.2-2' }}%</div>
              </div>
              <div class="border rounded-lg p-3 text-center">
                <div class="text-xs text-gray-500">Win Rate</div>
                <div class="text-lg font-bold" [class.text-green-600]="selectedDetail.result.winRate >= 50"
                  [class.text-red-600]="selectedDetail.result.winRate < 50">
                  {{ selectedDetail.result.winRate | number:'1.1-1' }}%
                </div>
              </div>
              <div class="border rounded-lg p-3 text-center">
                <div class="text-xs text-gray-500">Profit Factor</div>
                <div class="text-lg font-bold" [class.text-green-600]="selectedDetail.result.profitFactor >= 1"
                  [class.text-red-600]="selectedDetail.result.profitFactor < 1">
                  {{ selectedDetail.result.profitFactor | number:'1.2-2' }}
                </div>
              </div>
              <div class="border rounded-lg p-3 text-center">
                <div class="text-xs text-gray-500">Tổng GD (Thắng/Thua)</div>
                <div class="text-lg font-bold">
                  {{ selectedDetail.result.totalTrades }}
                  (<span class="text-green-600">{{ selectedDetail.result.winningTrades }}</span>/<span class="text-red-600">{{ selectedDetail.result.losingTrades }}</span>)
                </div>
              </div>
            </div>
          </div>

          <!-- Equity Curve Tab -->
          <div *ngIf="detailTab === 'equity' && selectedDetail.result">
            <div class="overflow-x-auto">
              <div class="min-w-[600px] h-64 flex items-end gap-px bg-gray-50 rounded-lg p-4">
                <div *ngFor="let point of getChartPoints()" class="flex-1 flex flex-col items-center justify-end">
                  <div class="w-full rounded-t"
                    [style.height.%]="point.heightPercent"
                    [class.bg-green-400]="point.return >= 0"
                    [class.bg-red-400]="point.return < 0">
                  </div>
                </div>
              </div>
              <div class="flex justify-between text-xs text-gray-400 mt-1 px-4">
                <span>{{ selectedDetail.result.equityCurve[0]?.date | date:'dd/MM/yy' }}</span>
                <span>{{ selectedDetail.result.equityCurve[selectedDetail.result.equityCurve.length - 1]?.date | date:'dd/MM/yy' }}</span>
              </div>
            </div>
            <!-- Equity Table -->
            <div class="mt-4 max-h-64 overflow-y-auto">
              <table class="w-full text-sm">
                <thead class="bg-gray-50 sticky top-0">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs text-gray-500">Ngày</th>
                    <th class="px-3 py-2 text-right text-xs text-gray-500">Giá trị</th>
                    <th class="px-3 py-2 text-right text-xs text-gray-500">Lợi nhuận ngày</th>
                    <th class="px-3 py-2 text-right text-xs text-gray-500">Lợi nhuận tích lũy</th>
                  </tr>
                </thead>
                <tbody class="divide-y">
                  <tr *ngFor="let p of selectedDetail.result.equityCurve" class="hover:bg-gray-50">
                    <td class="px-3 py-2">{{ p.date | date:'dd/MM/yyyy' }}</td>
                    <td class="px-3 py-2 text-right">{{ p.portfolioValue | vndCurrency }}</td>
                    <td class="px-3 py-2 text-right" [class.text-green-600]="p.dailyReturn >= 0"
                      [class.text-red-600]="p.dailyReturn < 0">{{ p.dailyReturn | number:'1.2-2' }}%</td>
                    <td class="px-3 py-2 text-right" [class.text-green-600]="p.cumulativeReturn >= 0"
                      [class.text-red-600]="p.cumulativeReturn < 0">{{ p.cumulativeReturn | number:'1.2-2' }}%</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Trades Tab -->
          <div *ngIf="detailTab === 'trades'">
            <div *ngIf="selectedDetail.simulatedTrades.length === 0" class="text-center py-8 text-gray-500">
              Không có giao dịch mô phỏng
            </div>
            <div class="overflow-x-auto">
              <table *ngIf="selectedDetail.simulatedTrades.length > 0" class="w-full text-sm">
                <thead class="bg-gray-50">
                  <tr>
                    <th class="px-3 py-2 text-left text-xs text-gray-500">Mã CK</th>
                    <th class="px-3 py-2 text-left text-xs text-gray-500">Loại</th>
                    <th class="px-3 py-2 text-right text-xs text-gray-500">Giá vào</th>
                    <th class="px-3 py-2 text-right text-xs text-gray-500">Giá ra</th>
                    <th class="px-3 py-2 text-right text-xs text-gray-500">KL</th>
                    <th class="px-3 py-2 text-left text-xs text-gray-500">Ngày vào</th>
                    <th class="px-3 py-2 text-left text-xs text-gray-500">Ngày ra</th>
                    <th class="px-3 py-2 text-right text-xs text-gray-500">P&L</th>
                    <th class="px-3 py-2 text-right text-xs text-gray-500">%</th>
                  </tr>
                </thead>
                <tbody class="divide-y">
                  <tr *ngFor="let t of selectedDetail.simulatedTrades" class="hover:bg-gray-50">
                    <td class="px-3 py-2 font-medium">{{ t.symbol }}</td>
                    <td class="px-3 py-2">
                      <span class="px-2 py-0.5 rounded text-xs" [ngClass]="getTradeTypeClass(t.type)">
                        {{ getTradeTypeDisplay(t.type) }}
                      </span>
                    </td>
                    <td class="px-3 py-2 text-right">{{ t.entryPrice | vndCurrency }}</td>
                    <td class="px-3 py-2 text-right">{{ t.exitPrice | vndCurrency }}</td>
                    <td class="px-3 py-2 text-right">{{ t.quantity | number }}</td>
                    <td class="px-3 py-2">{{ t.entryDate | date:'dd/MM/yy' }}</td>
                    <td class="px-3 py-2">{{ t.exitDate | date:'dd/MM/yy' }}</td>
                    <td class="px-3 py-2 text-right font-medium"
                      [class.text-green-600]="t.pnL >= 0" [class.text-red-600]="t.pnL < 0">
                      {{ t.pnL | vndCurrency }}
                    </td>
                    <td class="px-3 py-2 text-right"
                      [class.text-green-600]="t.returnPercent >= 0" [class.text-red-600]="t.returnPercent < 0">
                      {{ t.returnPercent | number:'1.2-2' }}%
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Error message -->
          <div *ngIf="selectedDetail.status === 'Failed'" class="bg-red-50 border border-red-200 rounded-lg p-4">
            <div class="text-red-700 font-medium">Backtest thất bại</div>
            <div class="text-red-600 text-sm mt-1">{{ selectedDetail.errorMessage }}</div>
          </div>

          <!-- Pending/Running -->
          <div *ngIf="selectedDetail.status === 'Pending' || selectedDetail.status === 'Running'"
            class="bg-yellow-50 border border-yellow-200 rounded-lg p-6 text-center">
            <div class="text-yellow-700 font-medium mb-2">{{ getStatusLabel(selectedDetail.status) }}</div>
            <div class="text-yellow-600 text-sm">Backtest đang được xử lý. Nhấn "Làm mới" để cập nhật kết quả.</div>
            <button (click)="refreshDetail()" class="mt-3 px-4 py-2 bg-yellow-500 text-white rounded-lg hover:bg-yellow-600 text-sm">
              Làm mới
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class BacktestingComponent implements OnInit {
  getTradeTypeDisplay = getTradeTypeDisplay;
  getTradeTypeClass = getTradeTypeClass;

  backtests: BacktestSummary[] = [];
  strategies: Strategy[] = [];
  selectedDetail: BacktestDetail | null = null;
  loading = false;
  running = false;
  showRunForm = false;
  detailTab = 'metrics';
  pollingInterval: any;

  newBacktest = {
    strategyId: '', name: '', startDate: '', endDate: '', initialCapital: 100000000
  };

  constructor(
    private backtestService: BacktestService,
    private strategyService: StrategyService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadBacktests();
    this.loadStrategies();
  }

  ngOnDestroy(): void {
    if (this.pollingInterval) clearInterval(this.pollingInterval);
  }

  loadBacktests(): void {
    this.loading = true;
    this.backtestService.getAll().subscribe({
      next: (data) => { this.backtests = data; this.loading = false; },
      error: () => { this.notification.error('Lỗi', 'Không thể tải danh sách backtest'); this.loading = false; }
    });
  }

  loadStrategies(): void {
    this.strategyService.getAll().subscribe({
      next: (data) => this.strategies = data,
      error: () => {}
    });
  }

  runBacktest(): void {
    if (!this.newBacktest.strategyId || !this.newBacktest.name) return;
    this.running = true;
    this.backtestService.run({
      strategyId: this.newBacktest.strategyId,
      name: this.newBacktest.name,
      startDate: this.newBacktest.startDate,
      endDate: this.newBacktest.endDate,
      initialCapital: this.newBacktest.initialCapital
    }).subscribe({
      next: (res) => {
        this.notification.success('Thành công', 'Backtest đã được gửi xử lý');
        this.showRunForm = false;
        this.running = false;
        this.newBacktest = { strategyId: '', name: '', startDate: '', endDate: '', initialCapital: 100000000 };
        this.loadBacktests();
        // Auto-poll for result
        this.startPolling(res.id);
      },
      error: () => { this.notification.error('Lỗi', 'Không thể chạy backtest'); this.running = false; }
    });
  }

  selectBacktest(bt: BacktestSummary): void {
    this.detailTab = 'metrics';
    this.backtestService.getById(bt.id).subscribe({
      next: (detail) => {
        this.selectedDetail = detail;
        if (detail.status === 'Pending' || detail.status === 'Running') {
          this.startPolling(detail.id);
        }
      },
      error: () => this.notification.error('Lỗi', 'Không thể tải chi tiết backtest')
    });
  }

  refreshDetail(): void {
    if (!this.selectedDetail) return;
    this.backtestService.getById(this.selectedDetail.id).subscribe({
      next: (detail) => {
        this.selectedDetail = detail;
        if (detail.status === 'Completed' || detail.status === 'Failed') {
          this.stopPolling();
          this.loadBacktests();
        }
      }
    });
  }

  startPolling(id: string): void {
    this.stopPolling();
    this.pollingInterval = setInterval(() => {
      this.backtestService.getById(id).subscribe({
        next: (detail) => {
          this.selectedDetail = detail;
          if (detail.status === 'Completed' || detail.status === 'Failed') {
            this.stopPolling();
            this.loadBacktests();
          }
        }
      });
    }, 5000);
  }

  stopPolling(): void {
    if (this.pollingInterval) { clearInterval(this.pollingInterval); this.pollingInterval = null; }
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'Đang chờ', 'Running': 'Đang chạy', 'Completed': 'Hoàn thành', 'Failed': 'Thất bại'
    };
    return map[status] || status;
  }

  getChartPoints(): { heightPercent: number; return: number }[] {
    if (!this.selectedDetail?.result?.equityCurve?.length) return [];
    const curve = this.selectedDetail.result.equityCurve;
    const values = curve.map(p => p.portfolioValue);
    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;
    // Sample to max 100 points for chart
    const step = Math.max(1, Math.floor(curve.length / 100));
    return curve.filter((_, i) => i % step === 0).map(p => ({
      heightPercent: ((p.portfolioValue - min) / range) * 80 + 10,
      return: p.cumulativeReturn
    }));
  }
}
