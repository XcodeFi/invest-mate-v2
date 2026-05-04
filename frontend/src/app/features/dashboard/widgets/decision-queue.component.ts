import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { catchError, of } from 'rxjs';
import {
  DecisionItemDto,
  DecisionService,
  DecisionSeverity,
  DecisionType,
} from '../../../core/services/decision.service';
import { DisciplineService } from '../../../core/services/discipline.service';

/**
 * Decision Queue — vị trí #1 trên Home (P3 Decision Engine v1.1).
 * Gộp 3 nguồn alert (StopLoss + Scenario trigger + Thesis review) thành 1 widget duy nhất.
 *
 * Empty state positive (v1.1): khi 0 alert → "✅ Hôm nay đang kỷ luật + 🔥 streak X ngày"
 * thay vì widget biến mất hoàn toàn (fix Critical risk #4 từ Product/UX agent review).
 *
 * Inline action (BÁN/GIỮ) thuộc PR-3 (P4) — PR-2 chỉ render link "Xử lý →" tới page detail.
 */
@Component({
  selector: 'app-decision-queue',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <!-- Loading skeleton -->
    <div *ngIf="loading" data-test="decision-queue-loading"
         class="bg-white rounded-xl border border-gray-200 shadow-sm p-4 mb-6 animate-pulse">
      <div class="h-4 w-40 bg-gray-200 rounded mb-3"></div>
      <div class="h-3 w-64 bg-gray-100 rounded"></div>
    </div>

    <!-- Empty state — kể câu chuyện thắng (v1.1) -->
    <div *ngIf="!loading && items.length === 0"
         data-test="decision-queue-empty"
         class="bg-emerald-50 rounded-xl border-2 border-emerald-200 shadow-sm p-4 mb-6">
      <div class="flex items-center justify-between">
        <div>
          <h2 class="text-sm font-bold text-emerald-900 flex items-center gap-2">
            ✅ Hôm nay đang kỷ luật
          </h2>
          <p class="text-xs text-emerald-700 mt-1">
            Không có SL bị chạm, không có thesis quá hạn review.
          </p>
        </div>
        <div *ngIf="streakDays !== null && streakDays > 0"
             data-test="streak-badge"
             class="text-right">
          <div class="text-2xl">🔥</div>
          <div class="text-xs font-semibold text-emerald-700">{{ streakDays }} ngày</div>
        </div>
      </div>
    </div>

    <!-- Active queue — có alert -->
    <div *ngIf="!loading && items.length > 0"
         data-test="decision-queue-active"
         class="bg-white rounded-xl border-2 border-red-200 shadow-md mb-6">
      <div class="px-4 py-3 bg-red-50 border-b border-red-200 rounded-t-xl flex items-center justify-between">
        <h2 class="text-base font-bold text-red-900 flex items-center gap-2">
          🚨 Việc cần xử lý hôm nay
          <span class="bg-red-600 text-white text-xs font-bold px-2 py-0.5 rounded-full"
                data-test="decision-queue-count">
            {{ items.length }}
          </span>
        </h2>
      </div>
      <div class="divide-y divide-gray-100">
        <div *ngFor="let item of visibleItems; trackBy: trackById"
             data-test="decision-item"
             class="p-4 hover:bg-gray-50 transition-colors"
             [class.bg-red-50]="item.severity === 'Critical'">
          <div class="flex items-start justify-between gap-3 mb-1">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2 mb-1 flex-wrap">
                <span class="font-bold text-gray-900">{{ item.symbol }}</span>
                <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                      [ngClass]="severityBadgeClass(item.severity)">
                  {{ severityLabel(item.severity) }}
                </span>
                <span class="text-xs text-gray-500">{{ typeLabel(item.type) }}</span>
                <span *ngIf="item.portfolioName" class="text-xs text-gray-400">· {{ item.portfolioName }}</span>
              </div>
              <div class="text-sm text-gray-800">{{ item.headline }}</div>
              <div *ngIf="item.thesisOrReason" class="text-xs text-gray-500 italic mt-1">
                Lý do gốc: {{ item.thesisOrReason }}
              </div>
            </div>
          </div>
          <div class="flex justify-end mt-2">
            <a [routerLink]="getActionRoute(item)" [queryParams]="getActionParams(item)"
               class="px-3 py-1.5 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded-md font-medium transition-colors">
              Xử lý →
            </a>
          </div>
        </div>
      </div>
      <div *ngIf="items.length > maxVisible"
           data-test="overflow-link"
           class="px-4 py-2 text-xs text-center text-gray-500 border-t">
        Hiển thị {{ maxVisible }}/{{ items.length }} ·
        <a routerLink="/risk-dashboard" class="text-blue-600 hover:text-blue-800 font-medium">
          Xem tất cả →
        </a>
      </div>
    </div>
  `,
})
export class DecisionQueueComponent implements OnInit {
  private decisionService = inject(DecisionService);
  private disciplineService = inject(DisciplineService);

  items: DecisionItemDto[] = [];
  streakDays: number | null = null;
  loading = true;
  readonly maxVisible = 5;

  ngOnInit(): void {
    this.loadQueue();
    this.loadStreak();
  }

  get visibleItems(): DecisionItemDto[] {
    return this.items.slice(0, this.maxVisible);
  }

  trackById(_: number, item: DecisionItemDto): string {
    return item.id;
  }

  severityBadgeClass(s: DecisionSeverity): Record<string, boolean> {
    return {
      'bg-red-100 text-red-800': s === 'Critical',
      'bg-amber-100 text-amber-800': s === 'Warning',
      'bg-blue-100 text-blue-800': s === 'Info',
    };
  }

  severityLabel(s: DecisionSeverity): string {
    if (s === 'Critical') return 'Khẩn cấp';
    if (s === 'Warning') return 'Lưu ý';
    return 'Thông tin';
  }

  typeLabel(t: DecisionType): string {
    if (t === 'StopLossHit') return 'Stop-loss';
    if (t === 'ScenarioTrigger') return 'Kịch bản';
    return 'Review thesis';
  }

  getActionRoute(item: DecisionItemDto): string[] {
    if (item.type === 'StopLossHit') return ['/risk-dashboard'];
    if (item.type === 'ScenarioTrigger') return ['/trade-plan'];
    return ['/symbol-timeline'];
  }

  getActionParams(item: DecisionItemDto): Record<string, string> {
    if (item.type === 'ScenarioTrigger' && item.tradePlanId) {
      return { loadPlan: item.tradePlanId };
    }
    if (item.type === 'ThesisReviewDue' && item.tradePlanId) {
      return { symbol: item.symbol, planId: item.tradePlanId };
    }
    if (item.type === 'StopLossHit') {
      return { symbol: item.symbol };
    }
    return {};
  }

  private loadQueue(): void {
    this.decisionService
      .getQueue()
      .pipe(catchError(() => of({ items: [], totalCount: 0 })))
      .subscribe((q) => {
        this.items = q.items ?? [];
        this.loading = false;
      });
  }

  private loadStreak(): void {
    this.disciplineService
      .getStreak()
      .pipe(catchError(() => of({ daysWithoutViolation: 0, hasData: false })))
      .subscribe((s) => {
        this.streakDays = s.hasData ? s.daysWithoutViolation : null;
      });
  }
}
