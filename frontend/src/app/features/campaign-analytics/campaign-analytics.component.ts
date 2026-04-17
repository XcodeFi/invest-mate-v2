import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TradePlanService, CampaignAnalyticsDto, TradePlan } from '../../core/services/trade-plan.service';
import { TIME_HORIZON_OPTIONS } from '../../shared/constants/time-horizon';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-campaign-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, VndCurrencyPipe],
  template: `
    <div class="max-w-7xl mx-auto px-4 py-6 space-y-6">
      <div class="flex items-center justify-between">
        <h1 class="text-2xl font-bold text-gray-800">Đánh giá Chiến dịch</h1>
        <select [(ngModel)]="selectedHorizon" (ngModelChange)="loadAnalytics()" class="border rounded-lg px-3 py-2 text-sm">
          <option value="">Tất cả</option>
          <option *ngFor="let th of timeHorizonOptions" [value]="th.value">{{ th.label }}</option>
        </select>
      </div>

      <!-- Pending Review Alert -->
      @if (pendingPlans.length > 0) {
        <div class="bg-amber-50 border border-amber-200 rounded-lg p-4">
          <h3 class="font-semibold text-amber-800 mb-2">{{pendingPlans.length}} kế hoạch chờ đánh giá</h3>
          <div class="space-y-1">
            @for (plan of pendingPlans; track plan.id) {
              <div class="flex items-center justify-between text-sm">
                <span class="font-medium">{{plan.symbol}}</span>
                <span class="text-gray-500">{{plan.executedAt | date:'dd/MM/yyyy'}}</span>
                <a [routerLink]="['/trade-plan']" [queryParams]="{id: plan.id}" class="text-blue-600 hover:underline">Đánh giá</a>
              </div>
            }
          </div>
        </div>
      }

      <!-- Summary Cards -->
      @if (analytics && analytics.totalCampaigns > 0) {
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-sm text-gray-500">Tổng chiến dịch</div>
            <div class="text-2xl font-bold">{{analytics.totalCampaigns}}</div>
            <div class="text-xs text-gray-400">{{analytics.winningCampaigns}} lãi / {{analytics.losingCampaigns}} lỗ</div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-sm text-gray-500">Win Rate</div>
            <div class="text-2xl font-bold" [class.text-green-600]="analytics.winRate >= 50" [class.text-red-600]="analytics.winRate < 50">
              {{analytics.winRate | number:'1.1-1'}}%
            </div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-sm text-gray-500">TB Lãi/Lỗ</div>
            <div class="text-2xl font-bold" [class.text-green-600]="analytics.averagePnLPercent > 0" [class.text-red-600]="analytics.averagePnLPercent < 0">
              {{analytics.averagePnLPercent > 0 ? '+' : ''}}{{analytics.averagePnLPercent | number:'1.2-2'}}%
            </div>
          </div>
          <div class="bg-white rounded-lg shadow p-4">
            <div class="text-sm text-gray-500">Tổng tích lũy</div>
            <div class="text-2xl font-bold" [class.text-green-600]="analytics.totalAccumulatedPnL > 0" [class.text-red-600]="analytics.totalAccumulatedPnL < 0">
              {{analytics.totalAccumulatedPnL | vndCurrency}}
            </div>
          </div>
        </div>

        <!-- Best / Worst -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          @if (analytics.bestCampaign) {
            <div class="bg-green-50 border border-green-200 rounded-lg p-4">
              <div class="text-sm text-green-700 font-semibold mb-1">Chiến dịch tốt nhất (VND/ngày)</div>
              <div class="text-lg font-bold text-green-800">{{analytics.bestCampaign.symbol}}</div>
              <div class="text-sm text-green-700">
                +{{analytics.bestCampaign.pnLPercent | number:'1.2-2'}}% · {{analytics.bestCampaign.pnLPerDay | vndCurrency}}/ngày · {{analytics.bestCampaign.holdingDays}} ngày
              </div>
            </div>
          }
          @if (analytics.worstCampaign) {
            <div class="bg-red-50 border border-red-200 rounded-lg p-4">
              <div class="text-sm text-red-700 font-semibold mb-1">Chiến dịch tệ nhất (VND/ngày)</div>
              <div class="text-lg font-bold text-red-800">{{analytics.worstCampaign.symbol}}</div>
              <div class="text-sm text-red-700">
                {{analytics.worstCampaign.pnLPercent | number:'1.2-2'}}% · {{analytics.worstCampaign.pnLPerDay | vndCurrency}}/ngày · {{analytics.worstCampaign.holdingDays}} ngày
              </div>
            </div>
          }
        </div>

        <!-- Campaigns Table -->
        <div class="bg-white rounded-lg shadow overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead class="bg-gray-50">
              <tr>
                <th class="px-4 py-3 text-left font-semibold text-gray-600">Mã</th>
                <th class="px-4 py-3 text-left font-semibold text-gray-600">Tầm nhìn</th>
                <th class="px-4 py-3 text-right font-semibold text-gray-600">Lãi/Lỗ</th>
                <th class="px-4 py-3 text-right font-semibold text-gray-600">%</th>
                <th class="px-4 py-3 text-right font-semibold text-gray-600">Số ngày</th>
                <th class="px-4 py-3 text-right font-semibold text-gray-600">VND/ngày</th>
                <th class="px-4 py-3 text-right font-semibold text-gray-600">% đạt target</th>
                <th class="px-4 py-3 text-left font-semibold text-gray-600">Ngày đánh giá</th>
              </tr>
            </thead>
            <tbody>
              @for (item of analytics.trend; track item.planId) {
                <tr class="border-t hover:bg-gray-50">
                  <td class="px-4 py-3 font-medium">{{item.symbol}}</td>
                  <td class="px-4 py-3 text-gray-500">{{getHorizonLabel(item)}}</td>
                  <td class="px-4 py-3 text-right" [class.text-green-600]="item.cumulativePnL > 0" [class.text-red-600]="item.cumulativePnL < 0">
                    {{getCampaignPnLAmount(item.planId) | vndCurrency}}
                  </td>
                  <td class="px-4 py-3 text-right" [class.text-green-600]="item.pnLPercent > 0" [class.text-red-600]="item.pnLPercent < 0">
                    {{item.pnLPercent > 0 ? '+' : ''}}{{item.pnLPercent | number:'1.2-2'}}%
                  </td>
                  <td class="px-4 py-3 text-right">{{getCampaignDays(item.planId)}}</td>
                  <td class="px-4 py-3 text-right" [class.text-green-600]="getCampaignPnLPerDay(item.planId) > 0" [class.text-red-600]="getCampaignPnLPerDay(item.planId) < 0">
                    {{getCampaignPnLPerDay(item.planId) | vndCurrency}}
                  </td>
                  <td class="px-4 py-3 text-right">{{getCampaignTargetAchievement(item.planId) | number:'1.0-0'}}%</td>
                  <td class="px-4 py-3 text-gray-500">{{item.reviewedAt | date:'dd/MM/yyyy'}}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <!-- Lessons Learned -->
        @if (lessonsData.length > 0) {
          <div class="bg-white rounded-lg shadow p-4">
            <h3 class="font-semibold text-gray-800 mb-3">Bài học rút kinh nghiệm</h3>
            <div class="space-y-3">
              @for (lesson of lessonsData; track lesson.planId) {
                <div class="border-l-4 border-blue-400 pl-3 py-1">
                  <div class="text-sm font-medium text-gray-700">{{lesson.symbol}} · {{lesson.reviewedAt | date:'dd/MM/yyyy'}}</div>
                  <div class="text-sm text-gray-600 mt-1">{{lesson.lessons}}</div>
                </div>
              }
            </div>
          </div>
        }
      } @else if (!loading) {
        <div class="text-center py-12 text-gray-500">
          Chưa có chiến dịch nào được đánh giá. Hãy đóng chiến dịch từ trang Kế hoạch.
        </div>
      }

      @if (loading) {
        <div class="text-center py-12 text-gray-500">Đang tải...</div>
      }
    </div>
  `
})
export class CampaignAnalyticsComponent implements OnInit {
  analytics: CampaignAnalyticsDto | null = null;
  pendingPlans: TradePlan[] = [];
  reviewedPlans: TradePlan[] = [];
  lessonsData: { planId: string; symbol: string; reviewedAt: string; lessons: string }[] = [];
  timeHorizonOptions = TIME_HORIZON_OPTIONS;
  selectedHorizon = '';
  loading = false;

  constructor(private tradePlanService: TradePlanService) {}

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading = true;
    forkJoin({
      analytics: this.tradePlanService.getCampaignAnalytics(this.selectedHorizon || undefined),
      pending: this.tradePlanService.getPendingReview(),
      all: this.tradePlanService.getAll()
    }).subscribe({
      next: (result) => {
        this.analytics = result.analytics;
        this.pendingPlans = result.pending;
        this.reviewedPlans = result.all.filter(p => p.status === 'Reviewed' && p.reviewData);
        this.lessonsData = this.reviewedPlans
          .filter(p => p.reviewData?.lessonsLearned)
          .map(p => ({
            planId: p.id,
            symbol: p.symbol,
            reviewedAt: p.reviewData!.reviewedAt,
            lessons: p.reviewData!.lessonsLearned!
          }))
          .sort((a, b) => new Date(b.reviewedAt).getTime() - new Date(a.reviewedAt).getTime());
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  loadAnalytics(): void {
    this.loading = true;
    this.tradePlanService.getCampaignAnalytics(this.selectedHorizon || undefined).subscribe({
      next: (data) => { this.analytics = data; this.loading = false; },
      error: () => this.loading = false
    });
  }

  getHorizonLabel(item: any): string {
    const plan = this.reviewedPlans.find(p => p.id === item.planId);
    const h = plan?.timeHorizon;
    if (h === 'ShortTerm') return 'Ngắn hạn';
    if (h === 'MediumTerm') return 'Trung hạn';
    if (h === 'LongTerm') return 'Dài hạn';
    return '—';
  }

  getCampaignPnLAmount(planId: string): number {
    return this.reviewedPlans.find(p => p.id === planId)?.reviewData?.pnLAmount ?? 0;
  }

  getCampaignDays(planId: string): number {
    return this.reviewedPlans.find(p => p.id === planId)?.reviewData?.holdingDays ?? 0;
  }

  getCampaignPnLPerDay(planId: string): number {
    return this.reviewedPlans.find(p => p.id === planId)?.reviewData?.pnLPerDay ?? 0;
  }

  getCampaignTargetAchievement(planId: string): number {
    return this.reviewedPlans.find(p => p.id === planId)?.reviewData?.targetAchievementPercent ?? 0;
  }
}
