import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import {
  DisciplineService,
  PendingThesisReviewDto,
} from '../../core/services/discipline.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

/**
 * Trang /pending-reviews (§D5 V2 plan Vin-discipline).
 * List plan đang active (Ready/InProgress) cần review thesis — InvalidationRule.CheckDate sắp tới
 * hoặc ExpectedReviewDate đã qua.
 */
@Component({
  selector: 'app-pending-reviews',
  standalone: true,
  imports: [CommonModule, RouterModule, VndCurrencyPipe],
  template: `
    <div class="min-h-screen bg-gray-50">
      <div class="max-w-5xl mx-auto p-4 lg:p-6">
        <header class="mb-6">
          <div class="flex items-center gap-2 text-sm text-gray-500 mb-2">
            <a routerLink="/dashboard" class="hover:text-indigo-600">Dashboard</a>
            <span>/</span>
            <span>Thesis cần review</span>
          </div>
          <h1 class="text-2xl font-bold text-gray-900 flex items-center gap-2">
            <span>🔔</span> Thesis cần review
          </h1>
          <p class="text-sm text-gray-600 mt-1">
            Plan đang active có <strong>điều kiện thesis sai</strong> tới ngày kiểm chứng
            (±2 ngày), hoặc đã quá <strong>ngày review định kỳ</strong>. Review thesis
            ngay để giữ kỷ luật — cắt nếu thesis đã sai.
          </p>
        </header>

        <div *ngIf="loading" class="bg-white rounded-xl shadow-sm border p-6 text-center text-gray-400">
          Đang tải...
        </div>

        <div *ngIf="error" class="bg-red-50 border border-red-200 rounded-xl p-4 text-red-700">
          {{ error }}
        </div>

        <div
          *ngIf="!loading && !error && reviews.length === 0"
          class="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center"
        >
          <div class="text-4xl mb-2">🟢</div>
          <div class="text-sm font-medium text-gray-700">Không có plan nào cần review</div>
          <div class="text-xs text-gray-500 mt-1">
            Mọi plan active đều đang trong vùng an toàn. Giữ kỷ luật!
          </div>
        </div>

        <div *ngIf="!loading && reviews.length > 0" class="space-y-3">
          <div
            *ngFor="let r of reviews"
            class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 hover:shadow-md transition-shadow"
            [class.border-red-300]="r.daysOverdue >= 3"
            [class.border-amber-300]="r.daysOverdue >= 0 && r.daysOverdue < 3"
          >
            <div class="flex items-start justify-between gap-3 mb-3">
              <div class="flex items-center gap-2">
                <span class="text-lg font-bold text-gray-900">{{ r.symbol }}</span>
                <span
                  class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase"
                  [ngClass]="statusBadgeClass(r.status)"
                >{{ r.status }}</span>
                <span
                  class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase"
                  [ngClass]="directionBadgeClass(r.direction)"
                >{{ r.direction === 'Buy' ? 'Long' : 'Short' }}</span>
              </div>

              <div class="text-right">
                <div
                  class="text-xs font-bold"
                  [ngClass]="urgencyTextClass(r.daysOverdue)"
                >
                  {{ r.daysOverdue >= 0 ? 'Quá hạn ' + r.daysOverdue + ' ngày' : 'Trong ' + (-r.daysOverdue) + ' ngày' }}
                </div>
                <div class="text-[10px] text-gray-400">{{ r.reasons.length }} lý do</div>
              </div>
            </div>

            <div *ngIf="r.thesis" class="bg-indigo-50/60 border border-indigo-100 rounded p-2 mb-3">
              <div class="text-[10px] uppercase text-indigo-700 font-semibold mb-0.5">Thesis gốc</div>
              <div class="text-xs text-gray-800">{{ r.thesis }}</div>
            </div>

            <div class="space-y-1.5 mb-3">
              <div *ngFor="let reason of r.reasons" class="flex items-start gap-2 text-xs">
                <span
                  class="px-1.5 py-0.5 rounded text-[10px] font-semibold uppercase whitespace-nowrap"
                  [ngClass]="reasonBadgeClass(reason.kind)"
                >{{ reasonLabel(reason.kind, reason.triggerType) }}</span>
                <div class="flex-1">
                  <div class="text-gray-700">{{ reason.detail }}</div>
                  <div class="text-[10px] text-gray-400">
                    Due: {{ reason.dueDate | date : 'yyyy-MM-dd' }}
                    <span *ngIf="reason.daysOverdue > 0" class="text-red-600 font-medium">
                      ({{ reason.daysOverdue }} ngày quá hạn)
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <div class="flex items-center justify-between text-xs pt-3 border-t border-gray-100">
              <div class="text-gray-500">
                {{ r.quantity }} × {{ r.entryPrice | vndCurrency }} ·
                SL {{ r.stopLoss | vndCurrency }} ·
                TP {{ r.target | vndCurrency }}
              </div>
              <a
                [routerLink]="['/trade-plan']"
                [queryParams]="{ loadPlan: r.planId }"
                class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-medium text-xs"
              >
                Mở plan →
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class PendingReviewsComponent implements OnInit {
  private disciplineService = inject(DisciplineService);
  private router = inject(Router);

  reviews: PendingThesisReviewDto[] = [];
  loading = false;
  error: string | null = null;

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading = true;
    this.error = null;
    this.disciplineService.getPendingReviews().subscribe({
      next: (data) => {
        this.reviews = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Pending reviews load failed', err);
        this.error = 'Không tải được danh sách review';
        this.loading = false;
      },
    });
  }

  statusBadgeClass(status: string): string {
    return status === 'Ready' ? 'bg-blue-100 text-blue-700' : 'bg-amber-100 text-amber-700';
  }

  directionBadgeClass(direction: string): string {
    return direction === 'Buy' ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700';
  }

  urgencyTextClass(daysOverdue: number): string {
    if (daysOverdue >= 3) return 'text-red-600';
    if (daysOverdue >= 0) return 'text-amber-600';
    return 'text-gray-500';
  }

  reasonBadgeClass(kind: string): string {
    return kind === 'InvalidationCheck'
      ? 'bg-red-50 text-red-700 border border-red-200'
      : 'bg-indigo-50 text-indigo-700 border border-indigo-200';
  }

  reasonLabel(kind: string, triggerType: string | null): string {
    if (kind === 'PeriodicReview') return 'Review định kỳ';
    return triggerType || 'Invalidation';
  }
}
