import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  StrategyService, Strategy, CreateStrategyRequest, UpdateStrategyRequest,
  StrategyPerformance
} from '../../core/services/strategy.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

@Component({
  selector: 'app-strategies',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-2xl font-bold text-gray-800">Quản lý Chiến lược</h1>
        <button (click)="showCreateForm = !showCreateForm"
          class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition">
          {{ showCreateForm ? 'Đóng' : '+ Tạo chiến lược' }}
        </button>
      </div>

      <!-- Create Form -->
      <div *ngIf="showCreateForm" class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold mb-4">Tạo chiến lược mới</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Tên chiến lược *</label>
            <input [(ngModel)]="newStrategy.name" type="text"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="VD: Breakout Trading">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Khung thời gian</label>
            <select [(ngModel)]="newStrategy.timeFrame"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn --</option>
              <option value="Scalping">Scalping</option>
              <option value="DayTrading">Day Trading</option>
              <option value="Swing">Swing</option>
              <option value="Position">Position</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Điều kiện thị trường</label>
            <select [(ngModel)]="newStrategy.marketCondition"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn --</option>
              <option value="Trending">Trending</option>
              <option value="Ranging">Ranging</option>
              <option value="Volatile">Volatile</option>
              <option value="All">Tất cả</option>
            </select>
          </div>
          <div class="md:col-span-2">
            <label class="block text-sm font-medium text-gray-700 mb-1">Mô tả</label>
            <textarea [(ngModel)]="newStrategy.description" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Mô tả chiến lược..."></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Quy tắc vào lệnh</label>
            <textarea [(ngModel)]="newStrategy.entryRules" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Điều kiện mở vị thế..."></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Quy tắc thoát lệnh</label>
            <textarea [(ngModel)]="newStrategy.exitRules" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Điều kiện đóng vị thế..."></textarea>
          </div>
          <div class="md:col-span-2">
            <label class="block text-sm font-medium text-gray-700 mb-1">Quy tắc quản lý rủi ro</label>
            <textarea [(ngModel)]="newStrategy.riskRules" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Stop loss, position sizing..."></textarea>
          </div>
        </div>
        <div class="mt-4 flex justify-end gap-2">
          <button (click)="showCreateForm = false"
            class="px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-50 transition">Hủy</button>
          <button (click)="createStrategy()"
            [disabled]="!newStrategy.name"
            class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition disabled:opacity-50">
            Tạo chiến lược
          </button>
        </div>
      </div>

      <!-- Tabs -->
      <div class="bg-white rounded-lg shadow mb-6">
        <div class="border-b border-gray-200">
          <nav class="flex space-x-4 px-4">
            <button (click)="activeTab = 'list'"
              [class.border-blue-500]="activeTab === 'list'"
              [class.text-blue-600]="activeTab === 'list'"
              [class.border-transparent]="activeTab !== 'list'"
              [class.text-gray-500]="activeTab !== 'list'"
              class="py-3 px-1 border-b-2 font-medium text-sm">Danh sách</button>
            <button (click)="activeTab = 'performance'" *ngIf="selectedStrategy"
              [class.border-blue-500]="activeTab === 'performance'"
              [class.text-blue-600]="activeTab === 'performance'"
              [class.border-transparent]="activeTab !== 'performance'"
              [class.text-gray-500]="activeTab !== 'performance'"
              class="py-3 px-1 border-b-2 font-medium text-sm">Hiệu suất</button>
          </nav>
        </div>

        <div class="p-4">
          <!-- Strategy List -->
          <div *ngIf="activeTab === 'list'">
            <div *ngIf="loading" class="text-center py-8 text-gray-500">Đang tải...</div>
            <div *ngIf="!loading && strategies.length === 0" class="text-center py-8 text-gray-500">
              Chưa có chiến lược nào. Hãy tạo chiến lược đầu tiên!
            </div>
            <div class="space-y-4">
              <div *ngFor="let s of strategies"
                class="border rounded-lg p-4 hover:shadow-md transition cursor-pointer"
                [class.border-blue-500]="selectedStrategy?.id === s.id"
                [class.border-gray-200]="selectedStrategy?.id !== s.id">
                <div class="flex justify-between items-start">
                  <div class="flex-1" (click)="selectStrategy(s)">
                    <div class="flex items-center gap-2">
                      <h3 class="text-lg font-semibold text-gray-800">{{ s.name }}</h3>
                      <span *ngIf="s.isActive"
                        class="px-2 py-0.5 text-xs rounded-full bg-green-100 text-green-700">Active</span>
                      <span *ngIf="!s.isActive"
                        class="px-2 py-0.5 text-xs rounded-full bg-gray-100 text-gray-500">Inactive</span>
                    </div>
                    <p *ngIf="s.description" class="text-sm text-gray-600 mt-1">{{ s.description }}</p>
                    <div class="flex gap-4 mt-2 text-xs text-gray-500">
                      <span *ngIf="s.timeFrame">⏱ {{ s.timeFrame }}</span>
                      <span *ngIf="s.marketCondition">📊 {{ s.marketCondition }}</span>
                      <span>📅 {{ s.createdAt | date:'dd/MM/yyyy' }}</span>
                    </div>
                  </div>
                  <div class="flex gap-2 ml-4">
                    <button (click)="viewPerformance(s)"
                      class="px-3 py-1 text-sm bg-purple-100 text-purple-700 rounded hover:bg-purple-200 transition">
                      📈 Hiệu suất
                    </button>
                    <button (click)="toggleActive(s)"
                      class="px-3 py-1 text-sm rounded transition"
                      [class.bg-yellow-100]="s.isActive" [class.text-yellow-700]="s.isActive"
                      [class.bg-green-100]="!s.isActive" [class.text-green-700]="!s.isActive">
                      {{ s.isActive ? 'Tắt' : 'Bật' }}
                    </button>
                    <button (click)="deleteStrategy(s)"
                      class="px-3 py-1 text-sm bg-red-100 text-red-700 rounded hover:bg-red-200 transition">
                      Xóa
                    </button>
                  </div>
                </div>
                <!-- Expanded details -->
                <div *ngIf="selectedStrategy?.id === s.id" class="mt-4 pt-4 border-t border-gray-200">
                  <div class="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                    <div>
                      <div class="font-medium text-gray-700 mb-1">Quy tắc vào lệnh</div>
                      <div class="text-gray-600 whitespace-pre-wrap">{{ s.entryRules || 'Chưa thiết lập' }}</div>
                    </div>
                    <div>
                      <div class="font-medium text-gray-700 mb-1">Quy tắc thoát lệnh</div>
                      <div class="text-gray-600 whitespace-pre-wrap">{{ s.exitRules || 'Chưa thiết lập' }}</div>
                    </div>
                    <div>
                      <div class="font-medium text-gray-700 mb-1">Quản lý rủi ro</div>
                      <div class="text-gray-600 whitespace-pre-wrap">{{ s.riskRules || 'Chưa thiết lập' }}</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Performance Tab -->
          <div *ngIf="activeTab === 'performance' && selectedStrategy">
            <h3 class="text-lg font-semibold mb-4">Hiệu suất: {{ selectedStrategy.name }}</h3>
            <div *ngIf="loadingPerformance" class="text-center py-8 text-gray-500">Đang tải...</div>
            <div *ngIf="!loadingPerformance && performance">
              <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
                <div class="bg-gray-50 rounded-lg p-4 text-center">
                  <div class="text-sm text-gray-500">Tổng giao dịch</div>
                  <div class="text-2xl font-bold text-gray-800">{{ performance.totalTrades }}</div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4 text-center">
                  <div class="text-sm text-gray-500">Tỷ lệ thắng</div>
                  <div class="text-2xl font-bold" [class.text-green-600]="performance.winRate >= 50"
                    [class.text-red-600]="performance.winRate < 50">
                    {{ performance.winRate | number:'1.1-1' }}%
                  </div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4 text-center">
                  <div class="text-sm text-gray-500">Tổng P&L</div>
                  <div class="text-2xl font-bold" [class.text-green-600]="performance.totalPnL >= 0"
                    [class.text-red-600]="performance.totalPnL < 0">
                    {{ performance.totalPnL | vndCurrency }}
                  </div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4 text-center">
                  <div class="text-sm text-gray-500">Profit Factor</div>
                  <div class="text-2xl font-bold" [class.text-green-600]="performance.profitFactor >= 1"
                    [class.text-red-600]="performance.profitFactor < 1">
                    {{ performance.profitFactor | number:'1.2-2' }}
                  </div>
                </div>
              </div>
              <div class="grid grid-cols-2 md:grid-cols-3 gap-4">
                <div class="bg-white border rounded-lg p-3">
                  <div class="text-xs text-gray-500">Thắng / Thua</div>
                  <div class="font-semibold">
                    <span class="text-green-600">{{ performance.winningTrades }}</span> /
                    <span class="text-red-600">{{ performance.losingTrades }}</span>
                  </div>
                </div>
                <div class="bg-white border rounded-lg p-3">
                  <div class="text-xs text-gray-500">P&L trung bình</div>
                  <div class="font-semibold" [class.text-green-600]="performance.averagePnL >= 0"
                    [class.text-red-600]="performance.averagePnL < 0">
                    {{ performance.averagePnL | vndCurrency }}
                  </div>
                </div>
                <div class="bg-white border rounded-lg p-3">
                  <div class="text-xs text-gray-500">Lãi TB / Lỗ TB</div>
                  <div class="font-semibold">
                    <span class="text-green-600">{{ performance.averageWin | vndCurrency }}</span> /
                    <span class="text-red-600">{{ performance.averageLoss | vndCurrency }}</span>
                  </div>
                </div>
                <div class="bg-white border rounded-lg p-3">
                  <div class="text-xs text-gray-500">Lãi lớn nhất</div>
                  <div class="font-semibold text-green-600">{{ performance.largestWin | vndCurrency }}</div>
                </div>
                <div class="bg-white border rounded-lg p-3">
                  <div class="text-xs text-gray-500">Lỗ lớn nhất</div>
                  <div class="font-semibold text-red-600">{{ performance.largestLoss | vndCurrency }}</div>
                </div>
              </div>
            </div>
            <div *ngIf="!loadingPerformance && !performance" class="text-center py-8 text-gray-500">
              Chưa có dữ liệu hiệu suất
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class StrategiesComponent implements OnInit {
  strategies: Strategy[] = [];
  selectedStrategy: Strategy | null = null;
  performance: StrategyPerformance | null = null;
  loading = false;
  loadingPerformance = false;
  showCreateForm = false;
  activeTab = 'list';

  newStrategy: CreateStrategyRequest = {
    name: '',
    description: '',
    entryRules: '',
    exitRules: '',
    riskRules: '',
    timeFrame: '',
    marketCondition: ''
  };

  constructor(
    private strategyService: StrategyService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadStrategies();
  }

  loadStrategies(): void {
    this.loading = true;
    this.strategyService.getAll().subscribe({
      next: (data) => {
        this.strategies = data;
        this.loading = false;
      },
      error: () => {
        this.notification.error('Lỗi', 'Không thể tải danh sách chiến lược');
        this.loading = false;
      }
    });
  }

  createStrategy(): void {
    if (!this.newStrategy.name) return;
    this.strategyService.create(this.newStrategy).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã tạo chiến lược');
        this.showCreateForm = false;
        this.resetForm();
        this.loadStrategies();
      },
      error: () => this.notification.error('Lỗi', 'Không thể tạo chiến lược')
    });
  }

  selectStrategy(s: Strategy): void {
    this.selectedStrategy = this.selectedStrategy?.id === s.id ? null : s;
    this.performance = null;
  }

  viewPerformance(s: Strategy): void {
    this.selectedStrategy = s;
    this.activeTab = 'performance';
    this.loadPerformance(s.id);
  }

  loadPerformance(id: string): void {
    this.loadingPerformance = true;
    this.strategyService.getPerformance(id).subscribe({
      next: (data) => {
        this.performance = data;
        this.loadingPerformance = false;
      },
      error: () => {
        this.notification.error('Lỗi', 'Không thể tải hiệu suất');
        this.loadingPerformance = false;
      }
    });
  }

  toggleActive(s: Strategy): void {
    this.strategyService.update(s.id, { isActive: !s.isActive }).subscribe({
      next: () => {
        this.notification.success('Thành công', `Đã ${s.isActive ? 'tắt' : 'bật'} chiến lược`);
        this.loadStrategies();
      },
      error: () => this.notification.error('Lỗi', 'Không thể cập nhật chiến lược')
    });
  }

  deleteStrategy(s: Strategy): void {
    if (!confirm(`Xóa chiến lược "${s.name}"?`)) return;
    this.strategyService.delete(s.id).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã xóa chiến lược');
        if (this.selectedStrategy?.id === s.id) this.selectedStrategy = null;
        this.loadStrategies();
      },
      error: () => this.notification.error('Lỗi', 'Không thể xóa chiến lược')
    });
  }

  private resetForm(): void {
    this.newStrategy = {
      name: '', description: '', entryRules: '', exitRules: '',
      riskRules: '', timeFrame: '', marketCondition: ''
    };
  }
}
