import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  StrategyService, Strategy, CreateStrategyRequest, UpdateStrategyRequest,
  StrategyPerformance
} from '../../core/services/strategy.service';
import { TemplateService, StrategyTemplate } from '../../core/services/template.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';

@Component({
  selector: 'app-strategies',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe, NumMaskDirective],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-2xl font-bold text-gray-800">Quản lý Chiến lược</h1>
        <button (click)="openCreateForm()"
          class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition">
          {{ showCreateForm ? 'Đóng' : '+ Tạo chiến lược' }}
        </button>
      </div>

      <!-- Template Picker -->
      <div *ngIf="showTemplatePicker" class="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-lg shadow p-6 mb-6">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-lg font-semibold text-gray-800">Chọn chiến lược mẫu</h2>
          <button (click)="skipTemplate()" class="text-sm text-blue-600 hover:text-blue-800 underline">
            Bỏ qua, tạo từ đầu
          </button>
        </div>

        <!-- Category Filter -->
        <div class="flex flex-wrap gap-2 mb-4">
          <button (click)="filterCategory = ''" [class.bg-blue-600]="filterCategory === ''"
            [class.text-white]="filterCategory === ''"
            [class.bg-white]="filterCategory !== ''"
            class="px-3 py-1.5 rounded-full text-sm font-medium border transition">Tất cả</button>
          <button (click)="filterCategory = 'ValueInvesting'" [class.bg-blue-600]="filterCategory === 'ValueInvesting'"
            [class.text-white]="filterCategory === 'ValueInvesting'"
            [class.bg-white]="filterCategory !== 'ValueInvesting'"
            class="px-3 py-1.5 rounded-full text-sm font-medium border transition">Đầu tư giá trị</button>
          <button (click)="filterCategory = 'Technical'" [class.bg-blue-600]="filterCategory === 'Technical'"
            [class.text-white]="filterCategory === 'Technical'"
            [class.bg-white]="filterCategory !== 'Technical'"
            class="px-3 py-1.5 rounded-full text-sm font-medium border transition">Phân tích kỹ thuật</button>
          <button (click)="filterCategory = 'PortfolioManagement'" [class.bg-blue-600]="filterCategory === 'PortfolioManagement'"
            [class.text-white]="filterCategory === 'PortfolioManagement'"
            [class.bg-white]="filterCategory !== 'PortfolioManagement'"
            class="px-3 py-1.5 rounded-full text-sm font-medium border transition">Quản lý danh mục</button>
        </div>

        <!-- Difficulty Filter -->
        <div class="flex flex-wrap gap-2 mb-4">
          <span class="text-sm text-gray-500 mr-2 self-center">Trình độ:</span>
          <button (click)="filterDifficulty = ''" [class.bg-green-600]="filterDifficulty === ''"
            [class.text-white]="filterDifficulty === ''"
            [class.bg-white]="filterDifficulty !== ''"
            class="px-3 py-1 rounded-full text-xs font-medium border transition">Tất cả</button>
          <button (click)="filterDifficulty = 'Beginner'" [class.bg-green-600]="filterDifficulty === 'Beginner'"
            [class.text-white]="filterDifficulty === 'Beginner'"
            [class.bg-white]="filterDifficulty !== 'Beginner'"
            class="px-3 py-1 rounded-full text-xs font-medium border transition">Mới bắt đầu</button>
          <button (click)="filterDifficulty = 'Intermediate'" [class.bg-yellow-500]="filterDifficulty === 'Intermediate'"
            [class.text-white]="filterDifficulty === 'Intermediate'"
            [class.bg-white]="filterDifficulty !== 'Intermediate'"
            class="px-3 py-1 rounded-full text-xs font-medium border transition">Trung bình</button>
          <button (click)="filterDifficulty = 'Advanced'" [class.bg-red-500]="filterDifficulty === 'Advanced'"
            [class.text-white]="filterDifficulty === 'Advanced'"
            [class.bg-white]="filterDifficulty !== 'Advanced'"
            class="px-3 py-1 rounded-full text-xs font-medium border transition">Nâng cao</button>
        </div>

        <!-- Template Cards -->
        <div *ngIf="loadingTemplates" class="text-center py-8 text-gray-500">Đang tải chiến lược mẫu...</div>
        <div *ngIf="!loadingTemplates && templateError" class="text-center py-8 text-red-500">
          Không thể tải chiến lược mẫu. <button (click)="loadTemplates()" class="underline text-blue-600 hover:text-blue-800">Thử lại</button>
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div *ngFor="let tpl of filteredTemplates"
            (click)="selectTemplate(tpl)"
            class="bg-white rounded-lg border-2 p-4 cursor-pointer hover:shadow-lg hover:border-blue-400 transition"
            [class.border-blue-500]="selectedTemplate?.id === tpl.id"
            [class.border-gray-200]="selectedTemplate?.id !== tpl.id">
            <div class="flex justify-between items-start mb-2">
              <h3 class="font-semibold text-gray-800 text-sm leading-tight flex-1">{{ tpl.name }}</h3>
              <span class="ml-2 px-2 py-0.5 rounded text-xs font-medium shrink-0"
                [class.bg-green-100]="tpl.difficultyLevel === 'Beginner'"
                [class.text-green-700]="tpl.difficultyLevel === 'Beginner'"
                [class.bg-yellow-100]="tpl.difficultyLevel === 'Intermediate'"
                [class.text-yellow-700]="tpl.difficultyLevel === 'Intermediate'"
                [class.bg-red-100]="tpl.difficultyLevel === 'Advanced'"
                [class.text-red-700]="tpl.difficultyLevel === 'Advanced'">
                {{ getDifficultyLabel(tpl.difficultyLevel) }}
              </span>
            </div>
            <p class="text-xs text-gray-500 mb-2 line-clamp-2">{{ tpl.description }}</p>
            <div class="flex flex-wrap gap-1 mb-2">
              <span class="px-1.5 py-0.5 bg-gray-100 text-gray-600 rounded text-xs">{{ getTimeFrameLabel(tpl.timeFrame) }}</span>
              <span class="px-1.5 py-0.5 bg-gray-100 text-gray-600 rounded text-xs">{{ getMarketLabel(tpl.marketCondition) }}</span>
            </div>
            <div class="flex flex-wrap gap-1">
              <span *ngFor="let indicator of tpl.keyIndicators.slice(0, 3)"
                class="px-1.5 py-0.5 bg-blue-50 text-blue-600 rounded text-xs">{{ indicator }}</span>
              <span *ngIf="tpl.keyIndicators.length > 3" class="text-xs text-gray-400">+{{ tpl.keyIndicators.length - 3 }}</span>
            </div>
          </div>
        </div>

        <!-- Template Detail -->
        <div *ngIf="selectedTemplate" class="mt-4 bg-white rounded-lg border border-blue-200 p-4">
          <div class="flex justify-between items-start mb-3">
            <h3 class="font-bold text-gray-800">{{ selectedTemplate.name }}</h3>
            <button (click)="applyTemplate()" class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 text-sm font-medium">
              Áp dụng chiến lược này
            </button>
          </div>
          <p class="text-sm text-gray-600 mb-3">{{ selectedTemplate.description }}</p>
          <div class="bg-blue-50 rounded p-3 mb-3">
            <div class="text-xs font-medium text-blue-700 mb-1">Gợi ý sử dụng</div>
            <p class="text-xs text-blue-600">{{ selectedTemplate.suggestion }}</p>
          </div>
          <div class="flex flex-wrap gap-1 mb-3">
            <span class="text-xs text-gray-500 mr-1">Phù hợp:</span>
            <span *ngFor="let who of selectedTemplate.suitableFor"
              class="px-2 py-0.5 bg-purple-50 text-purple-600 rounded-full text-xs">{{ who }}</span>
          </div>
          @if (selectedTemplate.suggestedRrRatio || selectedTemplate.suggestedSlMethod) {
            <div class="flex flex-wrap gap-2 mb-3">
              @if (selectedTemplate.suggestedRrRatio) {
                <span class="px-2 py-1 bg-emerald-50 text-emerald-700 rounded text-xs font-medium">R:R ≥ 1:{{ selectedTemplate.suggestedRrRatio }}</span>
              }
              @if (selectedTemplate.suggestedSlPercent) {
                <span class="px-2 py-1 bg-red-50 text-red-700 rounded text-xs font-medium">SL {{ selectedTemplate.suggestedSlPercent }}%</span>
              }
              @if (selectedTemplate.suggestedSlMethod && selectedTemplate.suggestedSlMethod !== 'manual') {
                <span class="px-2 py-1 bg-blue-50 text-blue-700 rounded text-xs font-medium">
                  SL: {{ selectedTemplate.suggestedSlMethod === 'atr' ? 'ATR ×' + (selectedTemplate.suggestedAtrMultiplier || 2) :
                         selectedTemplate.suggestedSlMethod === 'chandelier' ? 'Chandelier Exit' :
                         selectedTemplate.suggestedSlMethod === 'ma_trailing' ? 'MA Trailing' :
                         selectedTemplate.suggestedSlMethod === 'support' ? 'Hỗ trợ' : selectedTemplate.suggestedSlMethod }}
                </span>
              }
              @if (selectedTemplate.suggestedSizingModel && selectedTemplate.suggestedSizingModel !== 'fixed_risk') {
                <span class="px-2 py-1 bg-indigo-50 text-indigo-700 rounded text-xs font-medium">
                  Size: {{ selectedTemplate.suggestedSizingModel === 'atr_based' ? 'Theo ATR' :
                           selectedTemplate.suggestedSizingModel === 'turtle' ? 'Turtle' :
                           selectedTemplate.suggestedSizingModel === 'volatility_adjusted' ? 'Điều chỉnh BĐ' : selectedTemplate.suggestedSizingModel }}
                </span>
              }
            </div>
          }
          <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
            <div>
              <div class="font-medium text-green-700 mb-1">Quy tắc vào lệnh</div>
              <div class="text-gray-600 whitespace-pre-wrap bg-green-50 rounded p-2 max-h-40 overflow-y-auto">{{ selectedTemplate.entryRules }}</div>
            </div>
            <div>
              <div class="font-medium text-red-700 mb-1">Quy tắc thoát lệnh</div>
              <div class="text-gray-600 whitespace-pre-wrap bg-red-50 rounded p-2 max-h-40 overflow-y-auto">{{ selectedTemplate.exitRules }}</div>
            </div>
            <div>
              <div class="font-medium text-orange-700 mb-1">Quản lý rủi ro</div>
              <div class="text-gray-600 whitespace-pre-wrap bg-orange-50 rounded p-2 max-h-40 overflow-y-auto">{{ selectedTemplate.riskRules }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Create Form -->
      <div *ngIf="showCreateForm && !showTemplatePicker" class="bg-white rounded-lg shadow p-6 mb-6">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-lg font-semibold">
            {{ appliedTemplateName ? 'Tạo chiến lược từ: ' + appliedTemplateName : 'Tạo chiến lược mới' }}
          </h2>
          <button *ngIf="appliedTemplateName" (click)="showTemplatePicker = true"
            class="text-sm text-blue-600 hover:text-blue-800 underline">Chọn lại mẫu</button>
        </div>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Tên chiến lược *</label>
            <input [(ngModel)]="newStrategy.name" type="text"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="VD: Breakout Trading">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Khung thời gian <sup class="text-gray-400 font-bold">*</sup></label>
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
            <textarea [(ngModel)]="newStrategy.entryRules" rows="4"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Điều kiện mở vị thế..."></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Quy tắc thoát lệnh</label>
            <textarea [(ngModel)]="newStrategy.exitRules" rows="4"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Điều kiện đóng vị thế..."></textarea>
          </div>
          <div class="md:col-span-2">
            <label class="block text-sm font-medium text-gray-700 mb-1">Quy tắc quản lý rủi ro</label>
            <textarea [(ngModel)]="newStrategy.riskRules" rows="4"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Stop loss, position sizing..."></textarea>
          </div>
          <!-- Auto-fill hints for Trade Plan -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              SL gợi ý <sup class="text-blue-500">1</sup>
              <span class="text-xs font-normal text-gray-400 ml-1">(% dưới giá vào)</span>
            </label>
            <div class="flex items-center gap-2">
              <input [(ngModel)]="newStrategy.suggestedSlPercent" type="text" inputmode="numeric" appNumMask [decimals]="1"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="VD: 5 → SL = giá vào - 5%">
              <span class="text-gray-400 text-sm whitespace-nowrap">%</span>
            </div>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">
              R:R gợi ý <sup class="text-blue-500">2</sup>
              <span class="text-xs font-normal text-gray-400 ml-1">(lợi nhuận / rủi ro)</span>
            </label>
            <div class="flex items-center gap-2">
              <input [(ngModel)]="newStrategy.suggestedRrRatio" type="text" inputmode="numeric" appNumMask [decimals]="1"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="VD: 2 → TP = entry + 2×risk">
              <span class="text-gray-400 text-sm">R</span>
            </div>
          </div>
          <div class="md:col-span-2 text-xs text-gray-400 bg-blue-50 rounded p-2 space-y-1">
            <div><sup>*</sup> <strong>Khung thời gian:</strong> <strong>Scalping</strong> = giao dịch trong vài phút; <strong>Day Trading</strong> = mở/đóng trong ngày; <strong>Swing</strong> = giữ vài ngày đến vài tuần; <strong>Position</strong> = giữ vài tuần đến vài tháng.</div>
            <div><sup>1</sup> <strong>SL gợi ý:</strong> Khi chọn chiến lược này trong Trade Plan, Stop-Loss sẽ tự động điền = Giá vào × (1 - SL%).</div>
            <div><sup>2</sup> <strong>R:R gợi ý:</strong> Take-Profit tự động = Giá vào + (Risk × R:R). R:R = 2 → TP = Entry + 2 × (Entry - SL).</div>
          </div>
        </div>
        <div class="mt-4 flex justify-end gap-2">
          <button (click)="showCreateForm = false; showTemplatePicker = false; appliedTemplateName = ''"
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
                    <p *ngIf="s.description" class="text-sm text-gray-600 mt-1 line-clamp-2">{{ s.description }}</p>
                    <div class="flex gap-4 mt-2 text-xs text-gray-500">
                      <span *ngIf="s.timeFrame">{{ getTimeFrameLabel(s.timeFrame) }}</span>
                      <span *ngIf="s.marketCondition">{{ getMarketLabel(s.marketCondition) }}</span>
                      <span>{{ s.createdAt | date:'dd/MM/yyyy' }}</span>
                    </div>
                  </div>
                  <div class="flex gap-2 ml-4">
                    <button (click)="viewPerformance(s)"
                      class="px-3 py-1 text-sm bg-purple-100 text-purple-700 rounded hover:bg-purple-200 transition">
                      Hiệu suất
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
                  <div class="text-sm text-gray-500">Tỷ lệ thắng <sup class="text-emerald-500 font-bold">1</sup></div>
                  <div class="text-2xl font-bold" [class.text-green-600]="performance.winRate >= 50"
                    [class.text-red-600]="performance.winRate < 50">
                    {{ performance.winRate | number:'1.1-1' }}%
                  </div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4 text-center">
                  <div class="text-sm text-gray-500">Tổng P&L <sup class="text-blue-400 font-bold">2</sup></div>
                  <div class="text-2xl font-bold" [class.text-green-600]="performance.totalPnL >= 0"
                    [class.text-red-600]="performance.totalPnL < 0">
                    {{ performance.totalPnL | vndCurrency }}
                  </div>
                </div>
                <div class="bg-gray-50 rounded-lg p-4 text-center">
                  <div class="text-sm text-gray-500">Profit Factor <sup class="text-violet-400 font-bold">3</sup></div>
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

            <!-- Glossary -->
            <div *ngIf="performance" class="mt-4 rounded-lg bg-gray-50 border border-gray-200 px-4 py-3 text-xs text-gray-500 space-y-1">
              <div><sup class="text-emerald-500 font-bold">1</sup> <strong>Tỷ lệ thắng (Win Rate):</strong> % số lệnh có lãi trên tổng số lệnh. VD: 55% = 55/100 lệnh thắng. Cần kết hợp với Profit Factor để đánh giá toàn diện.</div>
              <div><sup class="text-blue-400 font-bold">2</sup> <strong>P&L (Profit & Loss):</strong> Tổng lãi/lỗ ròng của tất cả giao dịch sử dụng chiến lược này (chưa tính phí giao dịch).</div>
              <div><sup class="text-violet-400 font-bold">3</sup> <strong>Profit Factor:</strong> Tổng lãi gộp ÷ Tổng lỗ gộp. PF > 1.5 = tốt; PF = 1 = hòa vốn; PF &lt; 1 = chiến lược đang âm vốn tổng thể.</div>
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
  showTemplatePicker = false;
  activeTab = 'list';

  // Template picker
  templates: StrategyTemplate[] = [];
  loadingTemplates = false;
  templateError = false;
  selectedTemplate: StrategyTemplate | null = null;
  filterCategory = '';
  filterDifficulty = '';
  appliedTemplateName = '';

  newStrategy: CreateStrategyRequest = {
    name: '', description: '', entryRules: '', exitRules: '',
    riskRules: '', timeFrame: '', marketCondition: ''
  };

  constructor(
    private strategyService: StrategyService,
    private templateService: TemplateService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadStrategies();
  }

  loadStrategies(): void {
    this.loading = true;
    this.strategyService.getAll().subscribe({
      next: (data) => { this.strategies = data; this.loading = false; },
      error: () => { this.notification.error('Lỗi', 'Không thể tải danh sách chiến lược'); this.loading = false; }
    });
  }

  openCreateForm(): void {
    if (this.showCreateForm) {
      this.showCreateForm = false;
      this.showTemplatePicker = false;
      this.appliedTemplateName = '';
      return;
    }
    this.showCreateForm = true;
    this.showTemplatePicker = true;
    this.selectedTemplate = null;
    this.resetForm();
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.loadingTemplates = true;
    this.templateError = false;
    this.templateService.getStrategyTemplates().subscribe({
      next: (data) => { this.templates = data; this.loadingTemplates = false; },
      error: () => { this.loadingTemplates = false; this.templateError = true; }
    });
  }

  get filteredTemplates(): StrategyTemplate[] {
    return this.templates.filter(t => {
      if (this.filterCategory && t.category !== this.filterCategory) return false;
      if (this.filterDifficulty && t.difficultyLevel !== this.filterDifficulty) return false;
      return true;
    });
  }

  selectTemplate(tpl: StrategyTemplate): void {
    this.selectedTemplate = this.selectedTemplate?.id === tpl.id ? null : tpl;
  }

  applyTemplate(): void {
    if (!this.selectedTemplate) return;
    const tpl = this.selectedTemplate;
    this.newStrategy = {
      name: tpl.name, description: tpl.description,
      entryRules: tpl.entryRules, exitRules: tpl.exitRules, riskRules: tpl.riskRules,
      timeFrame: tpl.timeFrame, marketCondition: tpl.marketCondition,
      suggestedSlPercent: tpl.suggestedSlPercent,
      suggestedRrRatio: tpl.suggestedRrRatio,
      suggestedSlMethod: tpl.suggestedSlMethod
    };
    this.appliedTemplateName = tpl.name;
    this.showTemplatePicker = false;
  }

  skipTemplate(): void {
    this.showTemplatePicker = false;
    this.selectedTemplate = null;
    this.appliedTemplateName = '';
    this.resetForm();
  }

  createStrategy(): void {
    if (!this.newStrategy.name) return;
    this.strategyService.create(this.newStrategy).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã tạo chiến lược');
        this.showCreateForm = false; this.showTemplatePicker = false; this.appliedTemplateName = '';
        this.resetForm(); this.loadStrategies();
      },
      error: () => this.notification.error('Lỗi', 'Không thể tạo chiến lược')
    });
  }

  selectStrategy(s: Strategy): void {
    this.selectedStrategy = this.selectedStrategy?.id === s.id ? null : s;
    this.performance = null;
  }

  viewPerformance(s: Strategy): void {
    this.selectedStrategy = s; this.activeTab = 'performance'; this.loadPerformance(s.id);
  }

  loadPerformance(id: string): void {
    this.loadingPerformance = true;
    this.strategyService.getPerformance(id).subscribe({
      next: (data) => { this.performance = data; this.loadingPerformance = false; },
      error: () => { this.notification.error('Lỗi', 'Không thể tải hiệu suất'); this.loadingPerformance = false; }
    });
  }

  toggleActive(s: Strategy): void {
    this.strategyService.update(s.id, { isActive: !s.isActive }).subscribe({
      next: () => { this.notification.success('Thành công', `Đã ${s.isActive ? 'tắt' : 'bật'} chiến lược`); this.loadStrategies(); },
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

  getDifficultyLabel(level: string): string {
    const map: Record<string, string> = { 'Beginner': 'Mới', 'Intermediate': 'TB', 'Advanced': 'Nâng cao' };
    return map[level] || level;
  }

  getTimeFrameLabel(tf: string): string {
    const map: Record<string, string> = { 'Scalping': 'Scalping', 'DayTrading': 'Day Trading', 'Swing': 'Swing', 'Position': 'Dài hạn' };
    return map[tf] || tf;
  }

  getMarketLabel(mc: string): string {
    const map: Record<string, string> = { 'Trending': 'Xu hướng', 'Ranging': 'Sideway', 'Volatile': 'Biến động', 'All': 'Mọi TT' };
    return map[mc] || mc;
  }

  private resetForm(): void {
    this.newStrategy = { name: '', description: '', entryRules: '', exitRules: '', riskRules: '', timeFrame: '', marketCondition: '' };
  }
}
