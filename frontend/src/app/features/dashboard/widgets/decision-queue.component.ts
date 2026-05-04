import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import {
  DecisionAction,
  DecisionItemDto,
  DecisionService,
  DecisionSeverity,
  DecisionType,
} from '../../../core/services/decision.service';
import { DisciplineService } from '../../../core/services/discipline.service';

/**
 * Decision Queue — vị trí #1 trên Home (P3 + P4 Decision Engine v1.1).
 * Gộp 3 nguồn alert (StopLoss + Scenario trigger + Thesis review) thành 1 widget duy nhất.
 *
 * Empty state positive (v1.1): khi 0 alert → "✅ Hôm nay đang kỷ luật + 🔥 streak X ngày"
 * thay vì widget biến mất hoàn toàn.
 *
 * Inline action (P4): mỗi item có 2 button BÁN THEO KẾ HOẠCH / GIỮ + GHI LÝ DO.
 *  - BÁN: chỉ enable khi item có `tradePlanId` → confirm dialog → POST /resolve {action: ExecuteSell}.
 *  - GIỮ: expand inline note form → ≥ 20 chars → POST /resolve {action: HoldWithJournal}.
 *  - Optimistic remove item khỏi list sau khi resolve thành công.
 */
const MIN_HOLD_NOTE_LENGTH = 20;

@Component({
  selector: 'app-decision-queue',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
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

          <!-- Inline note form (GIỮ + GHI LÝ DO expanded) -->
          <div *ngIf="expandedNoteFor === item.id" class="mt-3 space-y-2"
               data-test="note-form">
            <textarea [(ngModel)]="noteDraft"
                      data-test="note-textarea"
                      placeholder="Vì sao giữ? Ít nhất 20 ký tự — buộc bạn nghĩ kỹ trước khi bỏ qua tín hiệu."
                      rows="3"
                      [disabled]="resolving"
                      class="w-full text-sm border border-amber-300 rounded-md px-3 py-2 focus:ring-2 focus:ring-amber-400 disabled:bg-gray-50"></textarea>
            <div class="text-xs text-gray-500">
              {{ (noteDraft || '').trim().length }}/{{ minNoteLength }} ký tự
            </div>
            <div class="flex justify-end gap-2">
              <button (click)="cancelNote()"
                      [disabled]="resolving"
                      class="px-3 py-1.5 text-xs text-gray-600 hover:text-gray-900 disabled:opacity-50">
                Hủy
              </button>
              <button (click)="submitHold(item)"
                      [disabled]="!canSubmitHold || resolving"
                      data-test="btn-submit-hold"
                      class="px-3 py-1.5 text-xs bg-amber-600 hover:bg-amber-700 disabled:bg-gray-300 disabled:cursor-not-allowed text-white rounded-md font-medium">
                {{ resolving ? 'Đang lưu...' : 'Lưu lý do + Giữ' }}
              </button>
            </div>
          </div>

          <!-- Action buttons (default) -->
          <div *ngIf="expandedNoteFor !== item.id" class="flex flex-wrap justify-end gap-2 mt-2">
            <button *ngIf="canExecuteSell(item)"
                    (click)="onExecuteSell(item)"
                    [disabled]="resolving"
                    data-test="btn-sell"
                    class="px-3 py-1.5 text-xs bg-red-600 hover:bg-red-700 disabled:bg-gray-300 text-white rounded-md font-bold transition-colors">
              🔪 BÁN THEO KẾ HOẠCH
            </button>
            <button (click)="expandNote(item)"
                    [disabled]="resolving"
                    data-test="btn-hold"
                    class="px-3 py-1.5 text-xs bg-amber-100 hover:bg-amber-200 disabled:opacity-50 text-amber-900 border border-amber-300 rounded-md font-medium transition-colors">
              ✋ GIỮ + GHI LÝ DO
            </button>
            <a [routerLink]="getActionRoute(item)" [queryParams]="getActionParams(item)"
               class="px-3 py-1.5 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded-md font-medium transition-colors">
              Xử lý →
            </a>
          </div>

          <!-- Resolve error — hiện cho cả BÁN lẫn GIỮ flow, gắn theo item.id -->
          <div *ngIf="errorFor(item.id)"
               data-test="resolve-error"
               class="mt-2 text-xs text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">
            ⚠️ {{ errorFor(item.id) }}
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
  readonly minNoteLength = MIN_HOLD_NOTE_LENGTH;

  expandedNoteFor: string | null = null;
  noteDraft = '';
  resolving = false;
  /** Error map by item.id — surface BÁN errors at item-level (BÁN không expand note form). */
  resolveErrors: Record<string, string> = {};

  errorFor(itemId: string): string | null {
    return this.resolveErrors[itemId] ?? null;
  }

  ngOnInit(): void {
    this.loadQueue();
    this.loadStreak();
  }

  get visibleItems(): DecisionItemDto[] {
    return this.items.slice(0, this.maxVisible);
  }

  get canSubmitHold(): boolean {
    return (this.noteDraft || '').trim().length >= this.minNoteLength;
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

  /** BÁN chỉ áp được khi item có tradePlanId — backend cần plan để tính quantity. */
  canExecuteSell(item: DecisionItemDto): boolean {
    return !!item.tradePlanId;
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

  expandNote(item: DecisionItemDto): void {
    this.expandedNoteFor = item.id;
    this.noteDraft = '';
    delete this.resolveErrors[item.id];
  }

  cancelNote(): void {
    if (this.expandedNoteFor) delete this.resolveErrors[this.expandedNoteFor];
    this.expandedNoteFor = null;
    this.noteDraft = '';
  }

  onExecuteSell(item: DecisionItemDto): void {
    if (!this.canExecuteSell(item) || this.resolving) return;
    const confirmed = window.confirm(
      `Xác nhận BÁN ${item.symbol} theo plan?\n\n` +
      `Hệ thống sẽ tạo lệnh bán với giá hiện tại + quantity tính từ TradePlan ${item.tradePlanId}.`
    );
    if (!confirmed) return;
    this.runResolve(item, { action: 'ExecuteSell' as DecisionAction, tradePlanId: item.tradePlanId });
  }

  submitHold(item: DecisionItemDto): void {
    if (!this.canSubmitHold || this.resolving) return;
    this.runResolve(item, {
      action: 'HoldWithJournal' as DecisionAction,
      tradePlanId: item.tradePlanId,
      symbol: item.symbol,
      note: this.noteDraft.trim(),
    });
  }

  private runResolve(item: DecisionItemDto, request: {
    action: DecisionAction;
    tradePlanId?: string | null;
    symbol?: string | null;
    note?: string | null;
  }): void {
    this.resolving = true;
    delete this.resolveErrors[item.id];
    this.decisionService
      .resolve(item.id, request)
      .pipe(
        catchError((err) => {
          this.resolveErrors[item.id] = err?.error?.message ?? 'Không thể xử lý — thử lại sau.';
          return of(null);
        }),
        finalize(() => { this.resolving = false; })
      )
      .subscribe((result) => {
        if (!result) return;
        // Optimistic remove khỏi list — server-side đã persist trade/journal entry.
        this.items = this.items.filter((i) => i.id !== item.id);
        delete this.resolveErrors[item.id];
        this.expandedNoteFor = null;
        this.noteDraft = '';
      });
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
