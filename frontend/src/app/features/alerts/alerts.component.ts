import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  AlertService, AlertRule, CreateAlertRuleRequest, AlertHistoryItem, AlertHistoryResult
} from '../../core/services/alert.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';

@Component({
  selector: 'app-alerts',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe, NumMaskDirective, UppercaseDirective],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-2xl font-bold text-gray-800">Hệ thống Cảnh báo</h1>
        <div class="flex items-center gap-3">
          <span *ngIf="unreadCount > 0"
            class="px-3 py-1 bg-red-500 text-white text-sm rounded-full">{{ unreadCount }} chưa đọc</span>
          <button (click)="showCreateForm = !showCreateForm"
            class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition">
            {{ showCreateForm ? 'Đóng' : '+ Tạo cảnh báo' }}
          </button>
        </div>
      </div>

      <!-- Create Form -->
      <div *ngIf="showCreateForm" class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold mb-4">Tạo quy tắc cảnh báo</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Tên cảnh báo *</label>
            <input [(ngModel)]="newRule.name" type="text"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="VD: Giá VNM vượt 80,000">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Loại cảnh báo *</label>
            <select [(ngModel)]="newRule.alertType" (ngModelChange)="onAlertTypeChange()"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn --</option>
              <option value="PriceAlert">Cảnh báo giá</option>
              <option value="DrawdownAlert">Cảnh báo drawdown</option>
              <option value="PortfolioValue">Giá trị danh mục</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Điều kiện *</label>
            <select [(ngModel)]="newRule.condition"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn --</option>
              <option value="Above">Vượt trên</option>
              <option value="Below">Dưới mức</option>
              <option value="Exceeds" *ngIf="newRule.alertType === 'DrawdownAlert' || newRule.alertType === 'PortfolioValue'">Vượt quá</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Ngưỡng *</label>
            <input [(ngModel)]="newRule.threshold" type="text" inputmode="numeric" appNumMask
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              [placeholder]="newRule.alertType === 'DrawdownAlert' ? 'VD: 10 (%)' : 'VD: 80000'">
            <p *ngIf="newRule.threshold > 0 && newRule.alertType !== 'DrawdownAlert'" class="mt-1 text-xs text-gray-500">{{ newRule.threshold | vndCurrency }}</p>
            <p *ngIf="newRule.threshold > 0 && newRule.alertType === 'DrawdownAlert'" class="mt-1 text-xs text-gray-500">{{ newRule.threshold }}%</p>
          </div>
          <div *ngIf="newRule.alertType === 'PriceAlert'">
            <label class="block text-sm font-medium text-gray-700 mb-1">Mã cổ phiếu</label>
            <input [(ngModel)]="newRule.symbol" type="text" appUppercase
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="VD: VNM">
          </div>
          <div *ngIf="newRule.alertType !== 'PriceAlert'">
            <label class="block text-sm font-medium text-gray-700 mb-1">Danh mục</label>
            <select [(ngModel)]="newRule.portfolioId"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn --</option>
              <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Kênh thông báo</label>
            <select [(ngModel)]="newRule.channel"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="InApp">Trong ứng dụng</option>
              <option value="Email">Email</option>
            </select>
          </div>
        </div>
        <div class="mt-4 flex justify-end gap-2">
          <button (click)="showCreateForm = false"
            class="px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-50 transition">Hủy</button>
          <button (click)="createRule()"
            [disabled]="!newRule.name || !newRule.alertType || !newRule.condition || !newRule.threshold"
            class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition disabled:opacity-50">
            Tạo cảnh báo
          </button>
        </div>
      </div>

      <!-- Tabs -->
      <div class="bg-white rounded-lg shadow mb-6">
        <div class="border-b border-gray-200">
          <nav class="flex space-x-4 px-4">
            <button (click)="activeTab = 'rules'"
              [class.border-blue-500]="activeTab === 'rules'"
              [class.text-blue-600]="activeTab === 'rules'"
              [class.border-transparent]="activeTab !== 'rules'"
              [class.text-gray-500]="activeTab !== 'rules'"
              class="py-3 px-1 border-b-2 font-medium text-sm">Quy tắc cảnh báo</button>
            <button (click)="activeTab = 'history'; loadHistory()"
              [class.border-blue-500]="activeTab === 'history'"
              [class.text-blue-600]="activeTab === 'history'"
              [class.border-transparent]="activeTab !== 'history'"
              [class.text-gray-500]="activeTab !== 'history'"
              class="py-3 px-1 border-b-2 font-medium text-sm">
              Lịch sử
              <span *ngIf="unreadCount > 0"
                class="ml-1 px-1.5 py-0.5 text-xs bg-red-500 text-white rounded-full">{{ unreadCount }}</span>
            </button>
          </nav>
        </div>

        <div class="p-4">
          <!-- Rules Tab -->
          <div *ngIf="activeTab === 'rules'">
            <div *ngIf="loadingRules" class="text-center py-8 text-gray-500">Đang tải...</div>
            <div *ngIf="!loadingRules && rules.length === 0" class="text-center py-8 text-gray-500">
              Chưa có quy tắc cảnh báo nào.
            </div>
            <div class="space-y-3">
              <div *ngFor="let rule of rules"
                class="border rounded-lg p-4 flex justify-between items-center"
                [class.border-green-300]="rule.isActive" [class.border-gray-200]="!rule.isActive"
                [class.bg-green-50]="rule.isActive">
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-semibold text-gray-800">{{ rule.name }}</span>
                    <span class="px-2 py-0.5 text-xs rounded-full"
                      [class.bg-green-100]="rule.isActive" [class.text-green-700]="rule.isActive"
                      [class.bg-gray-100]="!rule.isActive" [class.text-gray-500]="!rule.isActive">
                      {{ rule.isActive ? 'Active' : 'Inactive' }}
                    </span>
                    <span class="px-2 py-0.5 text-xs rounded-full bg-blue-100 text-blue-700">
                      {{ getAlertTypeLabel(rule.alertType) }}
                    </span>
                  </div>
                  <div class="text-sm text-gray-600 mt-1">
                    {{ getConditionLabel(rule.condition) }} {{ formatThreshold(rule) }}
                    <span *ngIf="rule.symbol" class="font-medium"> ({{ rule.symbol }})</span>
                  </div>
                  <div class="text-xs text-gray-400 mt-1">
                    Kênh: {{ rule.channel === 'InApp' ? 'Trong app' : 'Email' }}
                    <span *ngIf="rule.lastTriggeredAt"> · Kích hoạt lần cuối: {{ rule.lastTriggeredAt | date:'dd/MM HH:mm' }}</span>
                  </div>
                </div>
                <div class="flex gap-2">
                  <button (click)="toggleRule(rule)"
                    class="px-3 py-1 text-sm rounded transition"
                    [class.bg-yellow-100]="rule.isActive" [class.text-yellow-700]="rule.isActive"
                    [class.bg-green-100]="!rule.isActive" [class.text-green-700]="!rule.isActive">
                    {{ rule.isActive ? 'Tắt' : 'Bật' }}
                  </button>
                  <button (click)="deleteRule(rule)"
                    class="px-3 py-1 text-sm bg-red-100 text-red-700 rounded hover:bg-red-200 transition">Xóa</button>
                </div>
              </div>
            </div>
          </div>

          <!-- History Tab -->
          <div *ngIf="activeTab === 'history'">
            <div class="flex justify-between items-center mb-4">
              <h3 class="text-lg font-semibold">Lịch sử cảnh báo</h3>
              <label class="flex items-center gap-2 text-sm">
                <input type="checkbox" [(ngModel)]="unreadOnly" (ngModelChange)="loadHistory()"
                  class="rounded border-gray-300">
                Chỉ chưa đọc
              </label>
            </div>
            <div *ngIf="loadingHistory" class="text-center py-8 text-gray-500">Đang tải...</div>
            <div *ngIf="!loadingHistory && alertHistory.length === 0" class="text-center py-8 text-gray-500">
              Chưa có cảnh báo nào.
            </div>
            <div class="space-y-3">
              <div *ngFor="let alert of alertHistory"
                class="border rounded-lg p-4 cursor-pointer transition"
                [class.bg-blue-50]="!alert.isRead" [class.border-blue-200]="!alert.isRead"
                [class.bg-white]="alert.isRead" [class.border-gray-200]="alert.isRead"
                (click)="markAsRead(alert)">
                <div class="flex justify-between items-start">
                  <div>
                    <div class="flex items-center gap-2">
                      <span *ngIf="!alert.isRead" class="w-2 h-2 bg-blue-500 rounded-full"></span>
                      <span class="font-semibold text-gray-800">{{ alert.title }}</span>
                      <span class="px-2 py-0.5 text-xs rounded-full bg-orange-100 text-orange-700">
                        {{ getAlertTypeLabel(alert.alertType) }}
                      </span>
                    </div>
                    <p class="text-sm text-gray-600 mt-1">{{ alert.message }}</p>
                    <div class="text-xs text-gray-400 mt-1 flex gap-3">
                      <span>{{ alert.triggeredAt | date:'dd/MM/yyyy HH:mm' }}</span>
                      <span *ngIf="alert.currentValue !== null">Giá trị: {{ alert.currentValue | number:'1.0-0' }}</span>
                      <span *ngIf="alert.thresholdValue !== null">Ngưỡng: {{ alert.thresholdValue | number:'1.0-0' }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class AlertsComponent implements OnInit {
  rules: AlertRule[] = [];
  alertHistory: AlertHistoryItem[] = [];
  portfolios: PortfolioSummary[] = [];
  unreadCount = 0;
  loadingRules = false;
  loadingHistory = false;
  showCreateForm = false;
  activeTab = 'rules';
  unreadOnly = false;

  newRule: CreateAlertRuleRequest = {
    name: '',
    alertType: '',
    condition: '',
    threshold: 0,
    symbol: '',
    portfolioId: '',
    channel: 'InApp'
  };

  constructor(
    private alertService: AlertService,
    private portfolioService: PortfolioService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadPortfolios();
    this.loadRules();
    this.loadHistory();
  }

  loadPortfolios(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => this.portfolios = data,
      error: () => {}
    });
  }

  loadRules(): void {
    this.loadingRules = true;
    this.alertService.getRules().subscribe({
      next: (data) => {
        this.rules = data;
        this.loadingRules = false;
      },
      error: () => {
        this.notification.error('Lỗi', 'Không thể tải quy tắc cảnh báo');
        this.loadingRules = false;
      }
    });
  }

  loadHistory(): void {
    this.loadingHistory = true;
    this.alertService.getHistory(this.unreadOnly).subscribe({
      next: (data) => {
        this.alertHistory = data.alerts;
        this.unreadCount = data.unreadCount;
        this.loadingHistory = false;
      },
      error: () => {
        this.notification.error('Lỗi', 'Không thể tải lịch sử cảnh báo');
        this.loadingHistory = false;
      }
    });
  }

  createRule(): void {
    if (!this.newRule.name || !this.newRule.alertType || !this.newRule.condition) return;
    this.alertService.createRule(this.newRule).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã tạo cảnh báo');
        this.showCreateForm = false;
        this.resetForm();
        this.loadRules();
      },
      error: () => this.notification.error('Lỗi', 'Không thể tạo cảnh báo')
    });
  }

  toggleRule(rule: AlertRule): void {
    this.alertService.updateRule(rule.id, { isActive: !rule.isActive }).subscribe({
      next: () => {
        this.notification.success('Thành công', `Đã ${rule.isActive ? 'tắt' : 'bật'} cảnh báo`);
        this.loadRules();
      },
      error: () => this.notification.error('Lỗi', 'Không thể cập nhật cảnh báo')
    });
  }

  deleteRule(rule: AlertRule): void {
    if (!confirm(`Xóa cảnh báo "${rule.name}"?`)) return;
    this.alertService.deleteRule(rule.id).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã xóa cảnh báo');
        this.loadRules();
      },
      error: () => this.notification.error('Lỗi', 'Không thể xóa cảnh báo')
    });
  }

  markAsRead(alert: AlertHistoryItem): void {
    if (alert.isRead) return;
    this.alertService.markAsRead(alert.id).subscribe({
      next: () => {
        alert.isRead = true;
        this.unreadCount = Math.max(0, this.unreadCount - 1);
      },
      error: () => {}
    });
  }

  onAlertTypeChange(): void {
    this.newRule.condition = '';
    this.newRule.symbol = '';
    this.newRule.portfolioId = '';
  }

  getAlertTypeLabel(type: string): string {
    const labels: Record<string, string> = {
      'PriceAlert': '💰 Giá', 'DrawdownAlert': '📉 Drawdown', 'PortfolioValue': '📊 Giá trị DM'
    };
    return labels[type] || type;
  }

  getConditionLabel(condition: string): string {
    const labels: Record<string, string> = {
      'Above': 'Vượt trên', 'Below': 'Dưới mức', 'Exceeds': 'Vượt quá'
    };
    return labels[condition] || condition;
  }

  formatThreshold(rule: AlertRule): string {
    if (rule.alertType === 'DrawdownAlert') return `${rule.threshold}%`;
    return new Intl.NumberFormat('vi-VN').format(rule.threshold);
  }

  private resetForm(): void {
    this.newRule = {
      name: '', alertType: '', condition: '', threshold: 0,
      symbol: '', portfolioId: '', channel: 'InApp'
    };
  }
}
