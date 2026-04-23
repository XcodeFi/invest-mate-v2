import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DisciplinePeriod,
  DisciplineScoreDto,
  DisciplineService,
} from '../../../core/services/discipline.service';

/**
 * Widget "Kỷ luật Thesis" — §D6 plan Vin-discipline (Hybrid formula).
 * Hiển thị composite 0-100 + 3 sub-bars + Stop-Honor Rate primitive + sample size.
 * Mount cạnh Risk Alert trong Dashboard Cockpit.
 */
@Component({
  selector: 'app-discipline-score-widget',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
      <div class="flex items-center justify-between mb-3">
        <h2 class="text-sm font-semibold text-gray-900 flex items-center gap-2">
          <svg class="w-4 h-4 text-indigo-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"></path>
          </svg>
          Kỷ luật Thesis
        </h2>
        <select
          [(ngModel)]="days"
          (change)="onPeriodChange()"
          class="text-xs border border-gray-300 rounded-md px-2 py-1 focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          <option [ngValue]="7">7 ngày</option>
          <option [ngValue]="30">30 ngày</option>
          <option [ngValue]="90">90 ngày</option>
          <option [ngValue]="365">365 ngày</option>
        </select>
      </div>

      <ng-container *ngIf="!loading && score">
        <!-- Composite + label -->
        <div class="text-center mb-3">
          <div class="text-4xl font-extrabold" [ngClass]="overallColorClass()">
            {{ score.overall !== null ? score.overall : '—' }}
            <span class="text-xl text-gray-400">/ 100</span>
          </div>
          <div class="text-xs font-medium mt-1" [ngClass]="overallColorClass()">
            <span class="mr-1">{{ overallEmoji() }}</span>{{ score.label }}
          </div>
        </div>

        <!-- Sub-bars: 3 components -->
        <div class="space-y-2 mb-3">
          <div *ngFor="let c of componentBars()" class="flex items-center gap-2 text-xs">
            <span class="w-32 text-gray-600 truncate" [title]="c.label + ' (trọng số ' + c.weight + '%)'">
              {{ c.label }}
            </span>
            <div class="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
              <div
                class="h-full rounded-full transition-all duration-500"
                [style.width.%]="c.value !== null ? c.value : 0"
                [ngClass]="componentBarColor(c.value)"
              ></div>
            </div>
            <span class="w-10 text-right font-semibold" [ngClass]="componentTextColor(c.value)">
              {{ c.value !== null ? c.value : '—' }}
            </span>
          </div>
        </div>

        <!-- Primitive: Stop-Honor Rate -->
        <div class="pt-3 border-t border-gray-100">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2 text-xs">
              <span class="text-gray-500">🎯</span>
              <span class="text-gray-700 font-medium">Stop-Honor Rate</span>
            </div>
            <div class="text-xs">
              <span *ngIf="score.primitives.stopHonorRate.total > 0" class="font-bold text-gray-900">
                {{ (score.primitives.stopHonorRate.value * 100) | number : '1.0-0' }}%
                <span class="text-gray-500 font-normal">
                  ({{ score.primitives.stopHonorRate.hit }}/{{ score.primitives.stopHonorRate.total }} lệnh)
                </span>
              </span>
              <span *ngIf="score.primitives.stopHonorRate.total === 0" class="text-gray-400">—</span>
            </div>
          </div>
          <div class="text-[10px] text-gray-400 mt-1">
            Mẫu {{ score.sampleSize.closedLossTrades }} lệnh lỗ · {{ score.sampleSize.totalPlans }} plan trong {{ score.sampleSize.daysObserved }} ngày
          </div>
        </div>

        <!-- Warning banner khi trôi dạt -->
        <div
          *ngIf="score.overall !== null && score.overall < 60"
          class="mt-3 bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700"
        >
          ⚠ Kỷ luật trôi dạt — review lại các plan đang InProgress ngay.
        </div>

        <!-- Pending reviews link -->
        <div class="mt-3 pt-3 border-t border-gray-100 text-xs">
          <a routerLink="/pending-reviews" class="text-indigo-600 hover:text-indigo-800 font-medium flex items-center gap-1">
            🔔 Plan cần review thesis →
          </a>
        </div>
      </ng-container>

      <div *ngIf="loading" class="text-center py-4 text-sm text-gray-400">Đang tải...</div>
      <div *ngIf="error" class="text-center py-4 text-sm text-red-500">{{ error }}</div>
    </div>
  `,
})
export class DisciplineScoreWidgetComponent implements OnInit {
  private disciplineService = inject(DisciplineService);

  score: DisciplineScoreDto | null = null;
  days: DisciplinePeriod = 90;
  loading = false;
  error: string | null = null;

  ngOnInit(): void {
    this.load();
  }

  onPeriodChange(): void {
    this.load();
  }

  private load(): void {
    this.loading = true;
    this.error = null;
    this.disciplineService.getScore(this.days).subscribe({
      next: (s) => {
        this.score = s;
        this.loading = false;
      },
      error: (err) => {
        console.error('Discipline score load failed', err);
        this.error = 'Không tải được điểm kỷ luật';
        this.loading = false;
      },
    });
  }

  overallColorClass(): string {
    const v = this.score?.overall;
    if (v === null || v === undefined) return 'text-gray-400';
    if (v >= 80) return 'text-green-600';
    if (v >= 60) return 'text-amber-600';
    return 'text-red-600';
  }

  overallEmoji(): string {
    const v = this.score?.overall;
    if (v === null || v === undefined) return '⏳';
    if (v >= 80) return '🟢';
    if (v >= 60) return '🟡';
    return '🔴';
  }

  componentBars() {
    const c = this.score?.components;
    return [
      { label: 'SL-Integrity', value: c?.slIntegrity ?? null, weight: 50 },
      { label: 'Plan Quality', value: c?.planQuality ?? null, weight: 30 },
      { label: 'Review Timeliness', value: c?.reviewTimeliness ?? null, weight: 20 },
    ];
  }

  componentBarColor(v: number | null): string {
    if (v === null) return 'bg-gray-300';
    if (v >= 80) return 'bg-green-500';
    if (v >= 60) return 'bg-amber-500';
    return 'bg-red-500';
  }

  componentTextColor(v: number | null): string {
    if (v === null) return 'text-gray-400';
    if (v >= 80) return 'text-green-700';
    if (v >= 60) return 'text-amber-700';
    return 'text-red-700';
  }
}
