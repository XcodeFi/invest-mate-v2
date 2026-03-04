import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { SnapshotService, Snapshot, SnapshotComparison } from '../../core/services/snapshot.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-snapshots',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Lịch sử & Time Travel</h1>

      <!-- Portfolio Selector -->
      <div class="bg-white rounded-lg shadow p-4 mb-6">
        <div class="flex flex-wrap items-center gap-4">
          <label class="text-sm font-medium text-gray-700">Danh mục:</label>
          <select
            [(ngModel)]="selectedPortfolioId"
            (ngModelChange)="onPortfolioChange()"
            class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 min-w-[200px]">
            <option value="">-- Chọn danh mục --</option>
            <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
          </select>
          <button
            *ngIf="selectedPortfolioId"
            (click)="takeSnapshot()"
            [disabled]="takingSnapshot"
            class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg font-medium transition-colors disabled:opacity-50 ml-auto">
            {{ takingSnapshot ? 'Đang chụp...' : '📸 Chụp Snapshot' }}
          </button>
        </div>
      </div>

      <!-- Tab Navigation -->
      <div *ngIf="selectedPortfolioId" class="bg-white rounded-lg shadow mb-6">
        <div class="border-b border-gray-200">
          <nav class="flex -mb-px">
            <button
              (click)="activeTab = 'timeline'"
              [ngClass]="activeTab === 'timeline' ? 'border-blue-500 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'"
              class="px-6 py-3 border-b-2 font-medium text-sm transition-colors">
              📈 Timeline
            </button>
            <button
              (click)="activeTab = 'lookup'"
              [ngClass]="activeTab === 'lookup' ? 'border-blue-500 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'"
              class="px-6 py-3 border-b-2 font-medium text-sm transition-colors">
              🔍 Tra cứu ngày
            </button>
            <button
              (click)="activeTab = 'compare'"
              [ngClass]="activeTab === 'compare' ? 'border-blue-500 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'"
              class="px-6 py-3 border-b-2 font-medium text-sm transition-colors">
              ⚖️ So sánh
            </button>
          </nav>
        </div>

        <!-- Timeline Tab -->
        <div *ngIf="activeTab === 'timeline'" class="p-6">
          <div *ngIf="loadingTimeline" class="text-center text-gray-500 py-8">Đang tải...</div>

          <div *ngIf="!loadingTimeline && timeline.length === 0" class="text-center text-gray-500 py-8">
            Chưa có snapshot nào. Nhấn "Chụp Snapshot" để bắt đầu theo dõi.
          </div>

          <!-- Timeline Chart (simplified table view) -->
          <div *ngIf="!loadingTimeline && timeline.length > 0">
            <!-- Summary Cards -->
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
              <div class="border rounded-lg p-3">
                <div class="text-xs text-gray-500">Giá trị hiện tại</div>
                <div class="text-lg font-bold text-gray-800">{{ formatCurrency(latestSnapshot?.totalValue || 0) }}</div>
              </div>
              <div class="border rounded-lg p-3">
                <div class="text-xs text-gray-500">Lãi/Lỗ chưa thực hiện</div>
                <div class="text-lg font-bold" [ngClass]="(latestSnapshot?.unrealizedPnL || 0) >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ formatCurrency(latestSnapshot?.unrealizedPnL || 0) }}
                </div>
              </div>
              <div class="border rounded-lg p-3">
                <div class="text-xs text-gray-500">Lãi/Lỗ thực hiện</div>
                <div class="text-lg font-bold" [ngClass]="(latestSnapshot?.realizedPnL || 0) >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ formatCurrency(latestSnapshot?.realizedPnL || 0) }}
                </div>
              </div>
              <div class="border rounded-lg p-3">
                <div class="text-xs text-gray-500">Lợi suất tích luỹ</div>
                <div class="text-lg font-bold" [ngClass]="(latestSnapshot?.cumulativeReturn || 0) >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ ((latestSnapshot?.cumulativeReturn || 0) * 100).toFixed(2) }}%
                </div>
              </div>
            </div>

            <!-- Timeline Table -->
            <div class="overflow-x-auto">
              <table class="min-w-full table-auto">
                <thead>
                  <tr class="bg-gray-50 border-b">
                    <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ngày</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Tổng GT</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Tiền mặt</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Đầu tư</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Unrealized P&L</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Lợi suất ngày</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Lợi suất tích luỹ</th>
                    <th class="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase">Vị thế</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let snap of timeline" class="border-b hover:bg-gray-50 cursor-pointer" (click)="viewSnapshot(snap)">
                    <td class="px-4 py-3 text-sm font-medium">{{ snap.snapshotDate | date:'dd/MM/yyyy' }}</td>
                    <td class="px-4 py-3 text-sm text-right font-semibold">{{ formatCurrency(snap.totalValue) }}</td>
                    <td class="px-4 py-3 text-sm text-right">{{ formatCurrency(snap.cashBalance) }}</td>
                    <td class="px-4 py-3 text-sm text-right">{{ formatCurrency(snap.investedValue) }}</td>
                    <td class="px-4 py-3 text-sm text-right font-medium" [ngClass]="snap.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                      {{ formatCurrency(snap.unrealizedPnL) }}
                    </td>
                    <td class="px-4 py-3 text-sm text-right" [ngClass]="snap.dailyReturn >= 0 ? 'text-green-600' : 'text-red-600'">
                      {{ (snap.dailyReturn * 100).toFixed(2) }}%
                    </td>
                    <td class="px-4 py-3 text-sm text-right font-medium" [ngClass]="snap.cumulativeReturn >= 0 ? 'text-green-600' : 'text-red-600'">
                      {{ (snap.cumulativeReturn * 100).toFixed(2) }}%
                    </td>
                    <td class="px-4 py-3 text-sm text-center">{{ snap.positions?.length || 0 }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- Lookup Tab -->
        <div *ngIf="activeTab === 'lookup'" class="p-6">
          <div class="flex gap-3 mb-6">
            <input
              type="date"
              [(ngModel)]="lookupDate"
              class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
            <button
              (click)="lookupSnapshot()"
              [disabled]="loadingLookup"
              class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
              {{ loadingLookup ? 'Đang tải...' : 'Tra cứu' }}
            </button>
          </div>

          <div *ngIf="lookupSnapshot_result" class="space-y-6">
            <!-- Snapshot Overview -->
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div class="border rounded-lg p-4">
                <div class="text-xs text-gray-500">Tổng giá trị</div>
                <div class="text-xl font-bold">{{ formatCurrency(lookupSnapshot_result.totalValue) }}</div>
              </div>
              <div class="border rounded-lg p-4">
                <div class="text-xs text-gray-500">Tiền mặt</div>
                <div class="text-xl font-bold">{{ formatCurrency(lookupSnapshot_result.cashBalance) }}</div>
              </div>
              <div class="border rounded-lg p-4">
                <div class="text-xs text-gray-500">Unrealized P&L</div>
                <div class="text-xl font-bold" [ngClass]="lookupSnapshot_result.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ formatCurrency(lookupSnapshot_result.unrealizedPnL) }}
                </div>
              </div>
              <div class="border rounded-lg p-4">
                <div class="text-xs text-gray-500">Realized P&L</div>
                <div class="text-xl font-bold" [ngClass]="lookupSnapshot_result.realizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ formatCurrency(lookupSnapshot_result.realizedPnL) }}
                </div>
              </div>
            </div>

            <!-- Positions Table -->
            <div *ngIf="lookupSnapshot_result.positions.length > 0" class="overflow-x-auto">
              <h3 class="text-md font-semibold text-gray-700 mb-3">Danh sách vị thế</h3>
              <table class="min-w-full table-auto">
                <thead>
                  <tr class="bg-gray-50 border-b">
                    <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mã CP</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">SL</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Giá TB</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Giá TT</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">GT thị trường</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Lãi/Lỗ</th>
                    <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Tỷ trọng</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let pos of lookupSnapshot_result.positions" class="border-b hover:bg-gray-50">
                    <td class="px-4 py-3 text-sm font-bold">{{ pos.symbol }}</td>
                    <td class="px-4 py-3 text-sm text-right">{{ pos.quantity | number }}</td>
                    <td class="px-4 py-3 text-sm text-right">{{ formatCurrency(pos.averageCost) }}</td>
                    <td class="px-4 py-3 text-sm text-right">{{ formatCurrency(pos.marketPrice) }}</td>
                    <td class="px-4 py-3 text-sm text-right font-semibold">{{ formatCurrency(pos.marketValue) }}</td>
                    <td class="px-4 py-3 text-sm text-right font-medium" [ngClass]="pos.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                      {{ formatCurrency(pos.unrealizedPnL) }}
                    </td>
                    <td class="px-4 py-3 text-sm text-right">{{ (pos.weight * 100).toFixed(1) }}%</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div *ngIf="lookupNotFound" class="text-center text-gray-500 py-8">
            Không tìm thấy snapshot cho ngày đã chọn
          </div>
        </div>

        <!-- Compare Tab -->
        <div *ngIf="activeTab === 'compare'" class="p-6">
          <div class="flex flex-wrap gap-3 mb-6">
            <div>
              <label class="block text-xs text-gray-500 mb-1">Ngày 1</label>
              <input
                type="date"
                [(ngModel)]="compareDate1"
                class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
            </div>
            <div>
              <label class="block text-xs text-gray-500 mb-1">Ngày 2</label>
              <input
                type="date"
                [(ngModel)]="compareDate2"
                class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
            </div>
            <div class="flex items-end">
              <button
                (click)="compareSnapshots()"
                [disabled]="loadingCompare"
                class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
                {{ loadingCompare ? 'Đang so sánh...' : 'So sánh' }}
              </button>
            </div>
          </div>

          <div *ngIf="comparison" class="space-y-6">
            <!-- Comparison Summary -->
            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div class="border-2 rounded-lg p-4" [ngClass]="comparison.valueChange >= 0 ? 'border-green-200 bg-green-50' : 'border-red-200 bg-red-50'">
                <div class="text-sm text-gray-600">Thay đổi giá trị</div>
                <div class="text-2xl font-bold" [ngClass]="comparison.valueChange >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ comparison.valueChange >= 0 ? '+' : '' }}{{ formatCurrency(comparison.valueChange) }}
                </div>
              </div>
              <div class="border-2 rounded-lg p-4" [ngClass]="comparison.valueChangePercent >= 0 ? 'border-green-200 bg-green-50' : 'border-red-200 bg-red-50'">
                <div class="text-sm text-gray-600">Thay đổi %</div>
                <div class="text-2xl font-bold" [ngClass]="comparison.valueChangePercent >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ comparison.valueChangePercent >= 0 ? '+' : '' }}{{ (comparison.valueChangePercent * 100).toFixed(2) }}%
                </div>
              </div>
              <div class="border-2 rounded-lg p-4" [ngClass]="comparison.returnDifference >= 0 ? 'border-green-200 bg-green-50' : 'border-red-200 bg-red-50'">
                <div class="text-sm text-gray-600">Chênh lệch lợi suất</div>
                <div class="text-2xl font-bold" [ngClass]="comparison.returnDifference >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ comparison.returnDifference >= 0 ? '+' : '' }}{{ (comparison.returnDifference * 100).toFixed(2) }}%
                </div>
              </div>
            </div>

            <!-- Side by Side Comparison -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
              <!-- Snapshot 1 -->
              <div class="border rounded-lg p-4">
                <h3 class="font-semibold text-gray-700 mb-3">
                  📅 {{ comparison.snapshot1?.snapshotDate | date:'dd/MM/yyyy' }}
                </h3>
                <div *ngIf="comparison.snapshot1" class="space-y-2">
                  <div class="flex justify-between"><span class="text-gray-500">Tổng GT:</span><span class="font-semibold">{{ formatCurrency(comparison.snapshot1.totalValue) }}</span></div>
                  <div class="flex justify-between"><span class="text-gray-500">Tiền mặt:</span><span>{{ formatCurrency(comparison.snapshot1.cashBalance) }}</span></div>
                  <div class="flex justify-between"><span class="text-gray-500">Đầu tư:</span><span>{{ formatCurrency(comparison.snapshot1.investedValue) }}</span></div>
                  <div class="flex justify-between"><span class="text-gray-500">Unrealized P&L:</span>
                    <span [ngClass]="comparison.snapshot1.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'" class="font-semibold">
                      {{ formatCurrency(comparison.snapshot1.unrealizedPnL) }}
                    </span>
                  </div>
                  <div class="flex justify-between"><span class="text-gray-500">Lợi suất:</span>
                    <span [ngClass]="comparison.snapshot1.cumulativeReturn >= 0 ? 'text-green-600' : 'text-red-600'" class="font-semibold">
                      {{ (comparison.snapshot1.cumulativeReturn * 100).toFixed(2) }}%
                    </span>
                  </div>
                  <div class="text-xs text-gray-400">{{ comparison.snapshot1.positions?.length || 0 }} vị thế</div>
                </div>
                <div *ngIf="!comparison.snapshot1" class="text-center text-gray-400 py-4">Không có dữ liệu</div>
              </div>

              <!-- Snapshot 2 -->
              <div class="border rounded-lg p-4">
                <h3 class="font-semibold text-gray-700 mb-3">
                  📅 {{ comparison.snapshot2?.snapshotDate | date:'dd/MM/yyyy' }}
                </h3>
                <div *ngIf="comparison.snapshot2" class="space-y-2">
                  <div class="flex justify-between"><span class="text-gray-500">Tổng GT:</span><span class="font-semibold">{{ formatCurrency(comparison.snapshot2.totalValue) }}</span></div>
                  <div class="flex justify-between"><span class="text-gray-500">Tiền mặt:</span><span>{{ formatCurrency(comparison.snapshot2.cashBalance) }}</span></div>
                  <div class="flex justify-between"><span class="text-gray-500">Đầu tư:</span><span>{{ formatCurrency(comparison.snapshot2.investedValue) }}</span></div>
                  <div class="flex justify-between"><span class="text-gray-500">Unrealized P&L:</span>
                    <span [ngClass]="comparison.snapshot2.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'" class="font-semibold">
                      {{ formatCurrency(comparison.snapshot2.unrealizedPnL) }}
                    </span>
                  </div>
                  <div class="flex justify-between"><span class="text-gray-500">Lợi suất:</span>
                    <span [ngClass]="comparison.snapshot2.cumulativeReturn >= 0 ? 'text-green-600' : 'text-red-600'" class="font-semibold">
                      {{ (comparison.snapshot2.cumulativeReturn * 100).toFixed(2) }}%
                    </span>
                  </div>
                  <div class="text-xs text-gray-400">{{ comparison.snapshot2.positions?.length || 0 }} vị thế</div>
                </div>
                <div *ngIf="!comparison.snapshot2" class="text-center text-gray-400 py-4">Không có dữ liệu</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Snapshot Detail Modal -->
      <div *ngIf="selectedSnapshot" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
        <div class="bg-white rounded-lg shadow-xl max-w-4xl w-full max-h-[90vh] overflow-auto">
          <div class="p-6">
            <div class="flex justify-between items-center mb-4">
              <h2 class="text-xl font-bold text-gray-800">
                Snapshot {{ selectedSnapshot.snapshotDate | date:'dd/MM/yyyy' }}
              </h2>
              <button (click)="selectedSnapshot = null" class="text-gray-400 hover:text-gray-600 text-2xl">&times;</button>
            </div>

            <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
              <div class="border rounded p-3">
                <div class="text-xs text-gray-500">Tổng GT</div>
                <div class="font-bold">{{ formatCurrency(selectedSnapshot.totalValue) }}</div>
              </div>
              <div class="border rounded p-3">
                <div class="text-xs text-gray-500">Tiền mặt</div>
                <div class="font-bold">{{ formatCurrency(selectedSnapshot.cashBalance) }}</div>
              </div>
              <div class="border rounded p-3">
                <div class="text-xs text-gray-500">Unrealized P&L</div>
                <div class="font-bold" [ngClass]="selectedSnapshot.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ formatCurrency(selectedSnapshot.unrealizedPnL) }}
                </div>
              </div>
              <div class="border rounded p-3">
                <div class="text-xs text-gray-500">Lợi suất</div>
                <div class="font-bold" [ngClass]="selectedSnapshot.cumulativeReturn >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ (selectedSnapshot.cumulativeReturn * 100).toFixed(2) }}%
                </div>
              </div>
            </div>

            <div *ngIf="selectedSnapshot.positions.length > 0" class="overflow-x-auto">
              <table class="min-w-full table-auto">
                <thead>
                  <tr class="bg-gray-50 border-b">
                    <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">Mã CP</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">SL</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Giá TB</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Giá TT</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">GT TT</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Lãi/Lỗ</th>
                    <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Tỷ trọng</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let pos of selectedSnapshot.positions" class="border-b">
                    <td class="px-3 py-2 text-sm font-bold">{{ pos.symbol }}</td>
                    <td class="px-3 py-2 text-sm text-right">{{ pos.quantity | number }}</td>
                    <td class="px-3 py-2 text-sm text-right">{{ formatCurrency(pos.averageCost) }}</td>
                    <td class="px-3 py-2 text-sm text-right">{{ formatCurrency(pos.marketPrice) }}</td>
                    <td class="px-3 py-2 text-sm text-right font-semibold">{{ formatCurrency(pos.marketValue) }}</td>
                    <td class="px-3 py-2 text-sm text-right" [ngClass]="pos.unrealizedPnL >= 0 ? 'text-green-600' : 'text-red-600'">
                      {{ formatCurrency(pos.unrealizedPnL) }}
                    </td>
                    <td class="px-3 py-2 text-sm text-right">{{ (pos.weight * 100).toFixed(1) }}%</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div *ngIf="!selectedPortfolioId" class="bg-white rounded-lg shadow p-12 text-center">
        <div class="text-gray-400 text-lg mb-2">Vui lòng chọn danh mục đầu tư</div>
        <div class="text-gray-400 text-sm">Chọn một danh mục ở phía trên để xem lịch sử và so sánh</div>
      </div>
    </div>
  `
})
export class SnapshotsComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  activeTab: 'timeline' | 'lookup' | 'compare' = 'timeline';

  // Timeline
  timeline: Snapshot[] = [];
  loadingTimeline = false;
  latestSnapshot: Snapshot | null = null;

  // Lookup
  lookupDate = '';
  lookupSnapshot_result: Snapshot | null = null;
  loadingLookup = false;
  lookupNotFound = false;

  // Compare
  compareDate1 = '';
  compareDate2 = '';
  comparison: SnapshotComparison | null = null;
  loadingCompare = false;

  // Snapshot detail
  selectedSnapshot: Snapshot | null = null;

  // Take snapshot
  takingSnapshot = false;

  constructor(
    private snapshotService: SnapshotService,
    private portfolioService: PortfolioService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadPortfolios();

    const today = new Date();
    this.lookupDate = today.toISOString().split('T')[0];

    const thirtyDaysAgo = new Date(today);
    thirtyDaysAgo.setDate(today.getDate() - 30);
    this.compareDate1 = thirtyDaysAgo.toISOString().split('T')[0];
    this.compareDate2 = today.toISOString().split('T')[0];
  }

  loadPortfolios(): void {
    this.portfolioService.getAll().subscribe({
      next: data => this.portfolios = data,
      error: () => this.notificationService.error('Lỗi', 'Lỗi khi tải danh sách danh mục')
    });
  }

  onPortfolioChange(): void {
    if (this.selectedPortfolioId) {
      this.loadTimeline();
    } else {
      this.timeline = [];
      this.latestSnapshot = null;
      this.lookupSnapshot_result = null;
      this.comparison = null;
    }
  }

  loadTimeline(): void {
    this.loadingTimeline = true;
    this.snapshotService.getTimeline(this.selectedPortfolioId).subscribe({
      next: data => {
        this.timeline = data;
        this.latestSnapshot = data.length > 0 ? data[0] : null;
        this.loadingTimeline = false;
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi tải timeline');
        this.loadingTimeline = false;
      }
    });
  }

  takeSnapshot(): void {
    this.takingSnapshot = true;
    this.snapshotService.takeSnapshot(this.selectedPortfolioId).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Đã chụp snapshot thành công!');
        this.takingSnapshot = false;
        this.loadTimeline();
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi chụp snapshot');
        this.takingSnapshot = false;
      }
    });
  }

  lookupSnapshot(): void {
    if (!this.lookupDate) return;
    this.loadingLookup = true;
    this.lookupNotFound = false;
    this.lookupSnapshot_result = null;

    this.snapshotService.getSnapshotAtDate(this.selectedPortfolioId, this.lookupDate).subscribe({
      next: data => {
        this.lookupSnapshot_result = data;
        this.loadingLookup = false;
      },
      error: () => {
        this.lookupNotFound = true;
        this.loadingLookup = false;
      }
    });
  }

  compareSnapshots(): void {
    if (!this.compareDate1 || !this.compareDate2) return;
    this.loadingCompare = true;
    this.snapshotService.compareSnapshots(this.selectedPortfolioId, this.compareDate1, this.compareDate2).subscribe({
      next: data => {
        this.comparison = data;
        this.loadingCompare = false;
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi so sánh snapshots');
        this.loadingCompare = false;
      }
    });
  }

  viewSnapshot(snap: Snapshot): void {
    this.selectedSnapshot = snap;
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'decimal', maximumFractionDigits: 0 }).format(value) + ' đ';
  }
}
