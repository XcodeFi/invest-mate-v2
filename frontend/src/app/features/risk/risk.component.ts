import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  RiskService, RiskProfile, PortfolioRiskSummary, DrawdownResult,
  CorrelationMatrix, StopLossTargetsResponse, SetRiskProfileRequest,
  SetStopLossTargetRequest, StopLossTargetItem
} from '../../core/services/risk.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';
import { TemplateService, RiskProfileTemplate } from '../../core/services/template.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

@Component({
  selector: 'app-risk',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Quản lý Rủi ro</h1>

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
        </div>
      </div>

      <div *ngIf="selectedPortfolioId">
        <!-- Risk Summary Cards -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-sm text-gray-500">Tổng giá trị</div>
            <div class="text-xl font-bold text-blue-600">{{ (riskSummary?.totalValue || 0) | vndCurrency }}</div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-sm text-gray-500">Max Drawdown</div>
            <div class="text-xl font-bold" [class.text-red-600]="(riskSummary?.maxDrawdown || 0) > 10" [class.text-yellow-600]="(riskSummary?.maxDrawdown || 0) <= 10">
              {{ (riskSummary?.maxDrawdown || 0) | number:'1.2-2' }}%
            </div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-sm text-gray-500">VaR (95%)</div>
            <div class="text-xl font-bold text-orange-600">{{ (riskSummary?.valueAtRisk95 || 0) | number:'1.2-2' }}%</div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-sm text-gray-500">Vị thế lớn nhất</div>
            <div class="text-xl font-bold text-purple-600">{{ (riskSummary?.largestPositionPercent || 0) | number:'1.1-1' }}%</div>
          </div>
        </div>

        <!-- Tabs -->
        <div class="bg-white rounded-lg shadow mb-6">
          <div class="border-b border-gray-200">
            <nav class="flex space-x-4 px-4" aria-label="Tabs">
              <button *ngFor="let tab of tabs" (click)="activeTab = tab.key"
                [class.border-blue-500]="activeTab === tab.key"
                [class.text-blue-600]="activeTab === tab.key"
                [class.border-transparent]="activeTab !== tab.key"
                [class.text-gray-500]="activeTab !== tab.key"
                class="py-3 px-1 border-b-2 font-medium text-sm whitespace-nowrap">
                {{ tab.label }}
              </button>
            </nav>
          </div>

          <div class="p-4">
            <!-- Position Risk Tab -->
            <div *ngIf="activeTab === 'positions'">
              <h3 class="text-lg font-semibold mb-4">Rủi ro theo vị thế</h3>
              <div class="overflow-x-auto">
                <table class="min-w-full divide-y divide-gray-200">
                  <thead class="bg-gray-50">
                    <tr>
                      <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mã CK</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">KL</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Giá hiện tại</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Giá trị</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Tỷ trọng</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">SL</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">TP</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">R:R</th>
                    </tr>
                  </thead>
                  <tbody class="bg-white divide-y divide-gray-200">
                    <tr *ngFor="let pos of riskSummary?.positions || []">
                      <td class="px-4 py-3 font-medium text-blue-600">{{ pos.symbol }}</td>
                      <td class="px-4 py-3 text-right">{{ pos.quantity | number:'1.0-0' }}</td>
                      <td class="px-4 py-3 text-right">{{ pos.currentPrice | vndCurrency }}</td>
                      <td class="px-4 py-3 text-right">{{ pos.marketValue | vndCurrency }}</td>
                      <td class="px-4 py-3 text-right">
                        <span class="px-2 py-1 rounded text-xs font-medium"
                          [class.bg-red-100]="pos.positionSizePercent > (riskProfile?.maxPositionSizePercent || 20)"
                          [class.text-red-700]="pos.positionSizePercent > (riskProfile?.maxPositionSizePercent || 20)"
                          [class.bg-green-100]="pos.positionSizePercent <= (riskProfile?.maxPositionSizePercent || 20)"
                          [class.text-green-700]="pos.positionSizePercent <= (riskProfile?.maxPositionSizePercent || 20)">
                          {{ pos.positionSizePercent | number:'1.1-1' }}%
                        </span>
                      </td>
                      <td class="px-4 py-3 text-right text-red-500">{{ pos.stopLossPrice ? (pos.stopLossPrice | vndCurrency) : '-' }}</td>
                      <td class="px-4 py-3 text-right text-green-500">{{ pos.targetPrice ? (pos.targetPrice | vndCurrency) : '-' }}</td>
                      <td class="px-4 py-3 text-right">{{ pos.riskRewardRatio ? (pos.riskRewardRatio | number:'1.1-1') : '-' }}</td>
                    </tr>
                    <tr *ngIf="!riskSummary?.positions?.length">
                      <td colspan="8" class="px-4 py-8 text-center text-gray-500">Chưa có vị thế nào</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <!-- Stop-Loss Tracker Tab -->
            <div *ngIf="activeTab === 'stoploss'">
              <div class="flex justify-between items-center mb-4">
                <h3 class="text-lg font-semibold">Stop-Loss / Take-Profit</h3>
                <button (click)="showSlForm = !showSlForm"
                  class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium">
                  {{ showSlForm ? 'Đóng' : '+ Thiết lập SL/TP' }}
                </button>
              </div>

              <!-- SL/TP Form -->
              <div *ngIf="showSlForm" class="bg-gray-50 rounded-lg p-4 mb-4">
                <div class="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-4">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Trade ID</label>
                    <input type="text" [(ngModel)]="newSl.tradeId" placeholder="Trade ID"
                      class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm">
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Mã CK</label>
                    <input type="text" [(ngModel)]="newSl.symbol" placeholder="VNM"
                      class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm">
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Giá vào</label>
                    <input type="number" [(ngModel)]="newSl.entryPrice"
                      class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm">
                    <p *ngIf="newSl.entryPrice > 0" class="mt-1 text-xs text-gray-500">{{ newSl.entryPrice | vndCurrency }}</p>
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Stop-Loss</label>
                    <input type="number" [(ngModel)]="newSl.stopLossPrice"
                      class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm">
                    <p *ngIf="newSl.stopLossPrice > 0" class="mt-1 text-xs text-gray-500">{{ newSl.stopLossPrice | vndCurrency }}</p>
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Target</label>
                    <input type="number" [(ngModel)]="newSl.targetPrice"
                      class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm">
                    <p *ngIf="newSl.targetPrice > 0" class="mt-1 text-xs text-gray-500">{{ newSl.targetPrice | vndCurrency }}</p>
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 mb-1">Trailing Stop %</label>
                    <input type="number" [(ngModel)]="newSl.trailingStopPercent" placeholder="Tuỳ chọn"
                      class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm">
                  </div>
                </div>
                <div class="mt-4 flex justify-end">
                  <button (click)="saveStopLoss()" [disabled]="saving"
                    class="bg-green-600 hover:bg-green-700 text-white px-6 py-2 rounded-lg text-sm font-medium disabled:opacity-50">
                    {{ saving ? 'Đang lưu...' : 'Lưu' }}
                  </button>
                </div>
              </div>

              <!-- SL/TP Table -->
              <div class="overflow-x-auto">
                <table class="min-w-full divide-y divide-gray-200">
                  <thead class="bg-gray-50">
                    <tr>
                      <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mã CK</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Giá vào</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Stop-Loss</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Target</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">R:R</th>
                      <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Rủi ro/CP</th>
                      <th class="px-4 py-3 text-center text-xs font-medium text-gray-500 uppercase">Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody class="bg-white divide-y divide-gray-200">
                    <tr *ngFor="let sl of slTargets?.items || []">
                      <td class="px-4 py-3 font-medium text-blue-600">{{ sl.symbol }}</td>
                      <td class="px-4 py-3 text-right">{{ sl.entryPrice | vndCurrency }}</td>
                      <td class="px-4 py-3 text-right text-red-500">{{ sl.stopLossPrice | vndCurrency }}</td>
                      <td class="px-4 py-3 text-right text-green-500">{{ sl.targetPrice | vndCurrency }}</td>
                      <td class="px-4 py-3 text-right">{{ sl.riskRewardRatio | number:'1.1-1' }}</td>
                      <td class="px-4 py-3 text-right">{{ sl.riskPerShare | vndCurrency }}</td>
                      <td class="px-4 py-3 text-center">
                        <span *ngIf="sl.isStopLossTriggered" class="px-2 py-1 bg-red-100 text-red-700 rounded text-xs font-medium">SL Triggered</span>
                        <span *ngIf="sl.isTargetTriggered" class="px-2 py-1 bg-green-100 text-green-700 rounded text-xs font-medium">TP Reached</span>
                        <span *ngIf="!sl.isStopLossTriggered && !sl.isTargetTriggered" class="px-2 py-1 bg-blue-100 text-blue-700 rounded text-xs font-medium">Active</span>
                      </td>
                    </tr>
                    <tr *ngIf="!slTargets?.items?.length">
                      <td colspan="7" class="px-4 py-8 text-center text-gray-500">Chưa thiết lập SL/TP nào</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <!-- Drawdown Tab -->
            <div *ngIf="activeTab === 'drawdown'">
              <h3 class="text-lg font-semibold mb-4">Phân tích Drawdown</h3>
              <div *ngIf="drawdownResult" class="space-y-4">
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div class="bg-red-50 rounded-lg p-4">
                    <div class="text-sm text-red-600">Max Drawdown</div>
                    <div class="text-2xl font-bold text-red-700">{{ drawdownResult.maxDrawdownPercent | number:'1.2-2' }}%</div>
                    <div class="text-xs text-gray-500 mt-1" *ngIf="drawdownResult.peakDate">
                      Đỉnh: {{ drawdownResult.peakDate | date:'dd/MM/yyyy' }} ({{ (drawdownResult.peakValue || 0) | vndCurrency }})
                    </div>
                    <div class="text-xs text-gray-500" *ngIf="drawdownResult.troughDate">
                      Đáy: {{ drawdownResult.troughDate | date:'dd/MM/yyyy' }} ({{ (drawdownResult.troughValue || 0) | vndCurrency }})
                    </div>
                  </div>
                  <div class="bg-yellow-50 rounded-lg p-4">
                    <div class="text-sm text-yellow-600">Current Drawdown</div>
                    <div class="text-2xl font-bold text-yellow-700">{{ drawdownResult.currentDrawdownPercent | number:'1.2-2' }}%</div>
                  </div>
                  <div class="bg-blue-50 rounded-lg p-4">
                    <div class="text-sm text-blue-600">Số điểm dữ liệu</div>
                    <div class="text-2xl font-bold text-blue-700">{{ drawdownResult.drawdownSeries.length }}</div>
                  </div>
                </div>

                <!-- Drawdown Series Table -->
                <div *ngIf="drawdownResult.drawdownSeries.length" class="overflow-x-auto max-h-96 overflow-y-auto">
                  <table class="min-w-full divide-y divide-gray-200">
                    <thead class="bg-gray-50 sticky top-0">
                      <tr>
                        <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ngày</th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Giá trị</th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Drawdown</th>
                      </tr>
                    </thead>
                    <tbody class="bg-white divide-y divide-gray-200">
                      <tr *ngFor="let point of drawdownResult.drawdownSeries">
                        <td class="px-4 py-2 text-sm">{{ point.date | date:'dd/MM/yyyy' }}</td>
                        <td class="px-4 py-2 text-right text-sm">{{ point.value | vndCurrency }}</td>
                        <td class="px-4 py-2 text-right text-sm">
                          <span [class.text-red-600]="point.drawdownPercent > 5"
                                [class.text-yellow-600]="point.drawdownPercent > 0 && point.drawdownPercent <= 5"
                                [class.text-green-600]="point.drawdownPercent === 0">
                            -{{ point.drawdownPercent | number:'1.2-2' }}%
                          </span>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
              <div *ngIf="!drawdownResult" class="text-center py-8 text-gray-500">
                Cần ít nhất 2 snapshot để tính drawdown. Hãy chụp snapshot trước.
              </div>
            </div>

            <!-- Correlation Tab -->
            <div *ngIf="activeTab === 'correlation'">
              <h3 class="text-lg font-semibold mb-4">Ma trận tương quan</h3>
              <div *ngIf="correlationMatrix && correlationMatrix.pairs.length > 0">
                <div class="overflow-x-auto">
                  <table class="min-w-full divide-y divide-gray-200">
                    <thead class="bg-gray-50">
                      <tr>
                        <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Cặp mã</th>
                        <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Tương quan</th>
                        <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Mức độ</th>
                      </tr>
                    </thead>
                    <tbody class="bg-white divide-y divide-gray-200">
                      <tr *ngFor="let pair of correlationMatrix.pairs">
                        <td class="px-4 py-3 font-medium">{{ pair.symbol1 }} / {{ pair.symbol2 }}</td>
                        <td class="px-4 py-3 text-right">
                          <span [class.text-red-600]="pair.correlation > 0.7"
                                [class.text-yellow-600]="pair.correlation >= 0.3 && pair.correlation <= 0.7"
                                [class.text-green-600]="pair.correlation < 0.3"
                                class="font-mono font-medium">
                            {{ pair.correlation | number:'1.3-3' }}
                          </span>
                        </td>
                        <td class="px-4 py-3">
                          <span class="px-2 py-1 rounded text-xs font-medium"
                            [class.bg-red-100]="getCorrelationLevel(pair.correlation) === 'high'"
                            [class.text-red-700]="getCorrelationLevel(pair.correlation) === 'high'"
                            [class.bg-yellow-100]="getCorrelationLevel(pair.correlation) === 'medium'"
                            [class.text-yellow-700]="getCorrelationLevel(pair.correlation) === 'medium'"
                            [class.bg-green-100]="getCorrelationLevel(pair.correlation) === 'low'"
                            [class.text-green-700]="getCorrelationLevel(pair.correlation) === 'low'">
                            {{ getCorrelationLabel(pair.correlation) }}
                          </span>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
              <div *ngIf="!correlationMatrix || correlationMatrix.pairs.length === 0" class="text-center py-8 text-gray-500">
                Cần ít nhất 2 mã cổ phiếu có dữ liệu giá để tính tương quan.
              </div>
            </div>

            <!-- Risk Profile Tab -->
            <div *ngIf="activeTab === 'profile'">
              <h3 class="text-lg font-semibold mb-4">Cấu hình rủi ro</h3>

              <!-- Risk Profile Template Picker -->
              <div class="mb-6">
                <p class="text-sm text-gray-600 mb-3">Chọn mẫu cấu hình phù hợp với phong cách đầu tư:</p>
                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                  <div *ngFor="let tpl of riskTemplates"
                    (click)="applyRiskTemplate(tpl)"
                    class="cursor-pointer border-2 rounded-lg p-4 transition-all hover:shadow-md"
                    [class.border-blue-500]="selectedRiskTemplateId === tpl.id"
                    [class.bg-blue-50]="selectedRiskTemplateId === tpl.id"
                    [class.border-gray-200]="selectedRiskTemplateId !== tpl.id">
                    <div class="flex items-center gap-2 mb-2">
                      <span class="text-lg">{{ getRiskIcon(tpl.sortOrder) }}</span>
                      <h4 class="font-semibold text-gray-800">{{ tpl.name }}</h4>
                    </div>
                    <p class="text-xs text-gray-500 mb-3 line-clamp-2">{{ tpl.description }}</p>
                    <div class="space-y-1 text-xs">
                      <div class="flex justify-between">
                        <span class="text-gray-500">Vị thế tối đa:</span>
                        <span class="font-medium">{{ tpl.maxPositionSizePercent }}%</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-500">Ngành tối đa:</span>
                        <span class="font-medium">{{ tpl.maxSectorExposurePercent }}%</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-500">R:R:</span>
                        <span class="font-medium">{{ tpl.defaultRiskRewardRatio }}:1</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-gray-500">Rủi ro DM:</span>
                        <span class="font-medium">{{ tpl.maxPortfolioRiskPercent }}%</span>
                      </div>
                    </div>
                    <div class="mt-3 flex flex-wrap gap-1">
                      <span *ngFor="let tag of tpl.suitableFor"
                        class="px-2 py-0.5 bg-gray-100 text-gray-600 rounded text-[10px]">{{ tag }}</span>
                    </div>
                  </div>
                </div>

                <!-- Suggestion box -->
                <div *ngIf="selectedRiskTemplate" class="mt-4 bg-amber-50 border border-amber-200 rounded-lg p-4">
                  <div class="flex items-start gap-2">
                    <span class="text-amber-500 mt-0.5">&#9432;</span>
                    <div>
                      <h4 class="text-sm font-semibold text-amber-800">{{ selectedRiskTemplate.name }} - Gợi ý</h4>
                      <p class="text-sm text-amber-700 mt-1">{{ selectedRiskTemplate.suggestion }}</p>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Form -->
              <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Tỷ trọng vị thế tối đa (%)</label>
                  <input type="number" [(ngModel)]="profileForm.maxPositionSizePercent"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg">
                  <p class="text-xs text-gray-500 mt-1">Giới hạn % tối đa cho 1 vị thế</p>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Phơi nhiễm ngành tối đa (%)</label>
                  <input type="number" [(ngModel)]="profileForm.maxSectorExposurePercent"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg">
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Cảnh báo drawdown (%)</label>
                  <input type="number" [(ngModel)]="profileForm.maxDrawdownAlertPercent"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg">
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">R:R mặc định</label>
                  <input type="number" [(ngModel)]="profileForm.defaultRiskRewardRatio" step="0.1"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg">
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Rủi ro danh mục tối đa (%)</label>
                  <input type="number" [(ngModel)]="profileForm.maxPortfolioRiskPercent"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg">
                </div>
              </div>
              <div class="mt-6 flex justify-end">
                <button (click)="saveRiskProfile()" [disabled]="saving"
                  class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium disabled:opacity-50">
                  {{ saving ? 'Đang lưu...' : 'Lưu cấu hình' }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div *ngIf="!selectedPortfolioId" class="bg-white rounded-lg shadow p-8 text-center text-gray-500">
        Chọn danh mục để xem phân tích rủi ro
      </div>
    </div>
  `
})
export class RiskComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  activeTab = 'positions';
  saving = false;
  showSlForm = false;

  riskProfile: RiskProfile | null = null;
  riskSummary: PortfolioRiskSummary | null = null;
  drawdownResult: DrawdownResult | null = null;
  correlationMatrix: CorrelationMatrix | null = null;
  slTargets: StopLossTargetsResponse | null = null;

  riskTemplates: RiskProfileTemplate[] = [];
  selectedRiskTemplateId = '';
  selectedRiskTemplate: RiskProfileTemplate | null = null;

  tabs = [
    { key: 'positions', label: 'Vị thế' },
    { key: 'stoploss', label: 'SL/TP' },
    { key: 'drawdown', label: 'Drawdown' },
    { key: 'correlation', label: 'Tương quan' },
    { key: 'profile', label: 'Cấu hình' }
  ];

  profileForm: SetRiskProfileRequest = {
    maxPositionSizePercent: 20,
    maxSectorExposurePercent: 40,
    maxDrawdownAlertPercent: 10,
    defaultRiskRewardRatio: 2.0,
    maxPortfolioRiskPercent: 5
  };

  newSl: SetStopLossTargetRequest = {
    tradeId: '', portfolioId: '', symbol: '',
    entryPrice: 0, stopLossPrice: 0, targetPrice: 0
  };

  constructor(
    private riskService: RiskService,
    private portfolioService: PortfolioService,
    private notification: NotificationService,
    private templateService: TemplateService
  ) {}

  ngOnInit() {
    this.portfolioService.getAll().subscribe({
      next: (data) => {
        this.portfolios = data;
      },
      error: () => this.notification.error('Lỗi', 'Không thể tải danh sách danh mục')
    });

    this.templateService.getRiskProfileTemplates().subscribe({
      next: (templates) => this.riskTemplates = templates,
      error: () => {}
    });
  }

  onPortfolioChange() {
    if (!this.selectedPortfolioId) return;
    this.loadRiskData();
  }

  loadRiskData() {
    const id = this.selectedPortfolioId;

    this.riskService.getRiskProfile(id).subscribe({
      next: (profile) => {
        this.riskProfile = profile;
        this.profileForm = {
          maxPositionSizePercent: profile.maxPositionSizePercent,
          maxSectorExposurePercent: profile.maxSectorExposurePercent,
          maxDrawdownAlertPercent: profile.maxDrawdownAlertPercent,
          defaultRiskRewardRatio: profile.defaultRiskRewardRatio,
          maxPortfolioRiskPercent: profile.maxPortfolioRiskPercent
        };
      },
      error: () => { this.riskProfile = null; }
    });

    this.riskService.getPortfolioRiskSummary(id).subscribe({
      next: (summary) => this.riskSummary = summary,
      error: () => this.riskSummary = null
    });

    this.riskService.getDrawdown(id).subscribe({
      next: (result) => this.drawdownResult = result,
      error: () => this.drawdownResult = null
    });

    this.riskService.getCorrelation(id).subscribe({
      next: (matrix) => this.correlationMatrix = matrix,
      error: () => this.correlationMatrix = null
    });

    this.riskService.getStopLossTargets(id).subscribe({
      next: (targets) => this.slTargets = targets,
      error: () => this.slTargets = null
    });
  }

  saveRiskProfile() {
    this.saving = true;
    this.riskService.setRiskProfile(this.selectedPortfolioId, this.profileForm).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã lưu cấu hình rủi ro');
        this.saving = false;
        this.loadRiskData();
      },
      error: () => {
        this.notification.error('Lỗi', 'Không thể lưu cấu hình');
        this.saving = false;
      }
    });
  }

  saveStopLoss() {
    this.saving = true;
    this.newSl.portfolioId = this.selectedPortfolioId;
    this.riskService.setStopLossTarget(this.newSl).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã thiết lập SL/TP');
        this.saving = false;
        this.showSlForm = false;
        this.newSl = { tradeId: '', portfolioId: '', symbol: '', entryPrice: 0, stopLossPrice: 0, targetPrice: 0 };
        this.loadRiskData();
      },
      error: () => {
        this.notification.error('Lỗi', 'Không thể thiết lập SL/TP');
        this.saving = false;
      }
    });
  }

  applyRiskTemplate(tpl: RiskProfileTemplate) {
    this.selectedRiskTemplateId = tpl.id;
    this.selectedRiskTemplate = tpl;
    this.profileForm = {
      maxPositionSizePercent: tpl.maxPositionSizePercent,
      maxSectorExposurePercent: tpl.maxSectorExposurePercent,
      maxDrawdownAlertPercent: tpl.maxDrawdownAlertPercent,
      defaultRiskRewardRatio: tpl.defaultRiskRewardRatio,
      maxPortfolioRiskPercent: tpl.maxPortfolioRiskPercent
    };
  }

  getRiskIcon(sortOrder: number): string {
    const icons: Record<number, string> = { 1: '\u{1F6E1}', 2: '\u{2696}', 3: '\u{1F680}', 4: '\u{1F525}' };
    return icons[sortOrder] || '\u{1F4CA}';
  }

  getCorrelationLevel(corr: number): string {
    const abs = Math.abs(corr);
    if (abs > 0.7) return 'high';
    if (abs > 0.3) return 'medium';
    return 'low';
  }

  getCorrelationLabel(corr: number): string {
    const abs = Math.abs(corr);
    if (abs > 0.7) return 'Cao';
    if (abs > 0.3) return 'Trung bình';
    return 'Thấp';
  }
}
