import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import {
  DailyRoutineService, DailyRoutine, RoutineItem, RoutineTemplate,
  RoutineHistory, RoutineItemTemplate
} from '../../core/services/daily-routine.service';
import { NotificationService } from '../../core/services/notification.service';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

@Component({
  selector: 'app-daily-routine',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
          <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div>
              <h1 class="text-2xl sm:text-3xl font-bold text-gray-900">📋 Nhiệm vụ hàng ngày</h1>
              <p class="text-gray-500 text-sm mt-1">Quy trình giao dịch có kỷ luật mỗi ngày</p>
            </div>
            <!-- Streak badge -->
            <div *ngIf="routine" class="flex items-center gap-3">
              <div class="flex items-center gap-2 bg-gradient-to-r from-orange-50 to-amber-50 border border-orange-200 rounded-xl px-4 py-2">
                <span class="text-xl" [class.animate-bounce]="routine.currentStreak >= 5">🔥</span>
                <div>
                  <div class="text-sm font-bold text-orange-700">{{ routine.currentStreak }} ngày liên tiếp</div>
                  <div class="text-xs text-orange-500">Kỷ lục: {{ routine.longestStreak }} ngày</div>
                </div>
              </div>
              <div *ngIf="cachedStreakMessage" class="hidden sm:block text-sm font-medium text-amber-600 bg-amber-50 px-3 py-1.5 rounded-lg">
                {{ cachedStreakMessage }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-6">

        <!-- Template Selector -->
        <div class="mb-6">
          <div class="flex items-center gap-2 mb-3">
            <span class="text-sm font-medium text-gray-700">Chọn mẫu:</span>
          </div>
          <div class="flex gap-2 overflow-x-auto pb-2 -mx-1 px-1">
            <button *ngFor="let t of templates"
              (click)="selectTemplate(t)"
              class="flex-shrink-0 flex items-center gap-2 px-4 py-2.5 rounded-xl border text-sm font-medium transition-all whitespace-nowrap"
              [class.bg-blue-600]="routine?.templateId === t.id"
              [class.text-white]="routine?.templateId === t.id"
              [class.border-blue-600]="routine?.templateId === t.id"
              [class.shadow-md]="routine?.templateId === t.id"
              [class.bg-white]="routine?.templateId !== t.id"
              [class.text-gray-700]="routine?.templateId !== t.id"
              [class.border-gray-200]="routine?.templateId !== t.id && !t.isSuggested"
              [class.border-amber-300]="routine?.templateId !== t.id && t.isSuggested"
              [class.bg-amber-50]="routine?.templateId !== t.id && t.isSuggested"
              [class.hover:border-blue-300]="routine?.templateId !== t.id">
              <span>{{ t.emoji }}</span>
              <span>{{ t.name }}</span>
              <span *ngIf="t.isSuggested && routine?.templateId !== t.id"
                class="text-[10px] bg-amber-200 text-amber-800 px-1.5 py-0.5 rounded-full font-bold">Gợi ý</span>
              <span *ngIf="t.isUrgent"
                class="text-[10px] bg-red-200 text-red-800 px-1.5 py-0.5 rounded-full font-bold">Khẩn</span>
            </button>
            <!-- Add custom template button -->
            <button (click)="showCustomForm = !showCustomForm"
              class="flex-shrink-0 flex items-center gap-1 px-4 py-2.5 rounded-xl border border-dashed border-gray-300 text-sm text-gray-500 hover:border-blue-400 hover:text-blue-600 transition-colors">
              <span class="text-lg">+</span> Tạo mẫu
            </button>
          </div>
        </div>

        <!-- Loading state -->
        <div *ngIf="loading" class="text-center py-12">
          <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto"></div>
          <p class="text-gray-500 mt-3 text-sm">Đang tải...</p>
        </div>

        <!-- No routine yet — Start prompt -->
        <div *ngIf="!loading && !routine && suggestedTemplate" class="bg-white rounded-xl shadow-sm border border-gray-200 p-8 text-center mb-6">
          <div class="text-4xl mb-3">{{ suggestedTemplate.emoji }}</div>
          <h3 class="text-lg font-bold text-gray-900 mb-1">{{ suggestedTemplate.name }}</h3>
          <p class="text-sm text-gray-500 mb-1">{{ suggestedTemplate.description }}</p>
          <p class="text-xs text-gray-400 mb-4">~{{ suggestedTemplate.estimatedMinutes }} phút · {{ suggestedTemplate.items.length }} bước</p>
          <button (click)="startRoutine(suggestedTemplate.id)"
            class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2.5 rounded-lg font-medium transition-colors">
            Bắt đầu ngày hôm nay
          </button>
        </div>

        <!-- Main Checklist -->
        <div *ngIf="routine" class="space-y-4 mb-8">
          <!-- Progress bar -->
          <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
            <div class="flex items-center justify-between mb-2">
              <span class="text-sm font-medium text-gray-700">
                {{ routine.completedCount }}/{{ routine.totalCount }} bước hoàn thành
              </span>
              <span class="text-sm text-gray-500">~{{ cachedEstimatedTimeLeft }} phút còn lại</span>
            </div>
            <div class="w-full bg-gray-200 rounded-full h-3">
              <div class="h-3 rounded-full transition-all duration-500"
                [class.bg-emerald-500]="routine.progressPercent >= 50"
                [class.bg-amber-500]="routine.progressPercent > 0 && routine.progressPercent < 50"
                [class.bg-gray-300]="routine.progressPercent === 0"
                [style.width.%]="routine.progressPercent">
              </div>
            </div>
            <div *ngIf="routine.isFullyCompleted" class="mt-3 flex items-center gap-2 text-emerald-600 font-medium text-sm">
              <span>✅</span> Hoàn thành tất cả! Tuyệt vời!
            </div>
          </div>

          <!-- Groups -->
          <div *ngFor="let group of groups" class="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
            <div class="px-4 py-3 border-b border-gray-100 flex items-center justify-between"
              [class.bg-amber-50]="group === 'Sáng'"
              [class.bg-blue-50]="group === 'Trong phiên'"
              [class.bg-violet-50]="group === 'Cuối ngày'">
              <div class="flex items-center gap-2">
                <span class="text-lg">{{ getGroupEmoji(group) }}</span>
                <h3 class="font-bold text-sm"
                  [class.text-amber-800]="group === 'Sáng'"
                  [class.text-blue-800]="group === 'Trong phiên'"
                  [class.text-violet-800]="group === 'Cuối ngày'">
                  {{ group }}
                </h3>
              </div>
              <span class="text-xs text-gray-500">
                {{ cachedGroupCompletedCount[group] || 0 }}/{{ (cachedGroupItems[group] || []).length }}
              </span>
            </div>
            <div class="divide-y divide-gray-50">
              <div *ngFor="let item of cachedGroupItems[group]"
                class="flex items-center gap-3 px-4 py-3 hover:bg-gray-50 transition-colors">
                <!-- Checkbox -->
                <button (click)="toggleItem(item)"
                  class="flex-shrink-0 w-6 h-6 rounded-full border-2 flex items-center justify-center transition-all"
                  [class.bg-emerald-500]="item.isCompleted"
                  [class.border-emerald-500]="item.isCompleted"
                  [class.border-gray-300]="!item.isCompleted"
                  [class.hover:border-emerald-400]="!item.isCompleted">
                  <svg *ngIf="item.isCompleted" class="w-3.5 h-3.5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7"/>
                  </svg>
                </button>
                <!-- Emoji -->
                <span class="text-base flex-shrink-0" *ngIf="item.emoji">{{ item.emoji }}</span>
                <!-- Label + metadata -->
                <div class="flex-1 min-w-0">
                  <span class="text-sm"
                    [class.text-gray-900]="!item.isCompleted"
                    [class.text-gray-400]="item.isCompleted"
                    [class.line-through]="item.isCompleted">
                    {{ item.label }}
                  </span>
                  <span *ngIf="item.isRequired && !item.isCompleted"
                    class="text-red-400 text-xs ml-1">*</span>
                  <div *ngIf="item.isCompleted && item.completedAt" class="text-[10px] text-gray-400 mt-0.5">
                    Hoàn thành lúc {{ formatTime(item.completedAt) }}
                  </div>
                </div>
                <!-- Deep link button -->
                <a *ngIf="item.link" [routerLink]="item.link"
                  class="flex-shrink-0 w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-colors"
                  title="Mở trang liên quan">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/>
                  </svg>
                </a>
              </div>
            </div>
          </div>
        </div>

        <!-- History Section -->
        <div *ngIf="history" class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6">
          <h3 class="text-sm font-bold text-gray-700 mb-3">Lịch sử 30 ngày gần nhất</h3>
          <div class="flex gap-1 flex-wrap">
            <div *ngFor="let day of cachedLast30Days"
              class="w-8 h-8 rounded-md flex items-center justify-center text-[10px] font-medium border transition-colors"
              [class.bg-emerald-100]="cachedDayStatuses[day.key] === 'completed'"
              [class.border-emerald-300]="cachedDayStatuses[day.key] === 'completed'"
              [class.text-emerald-700]="cachedDayStatuses[day.key] === 'completed'"
              [class.bg-amber-100]="cachedDayStatuses[day.key] === 'partial'"
              [class.border-amber-300]="cachedDayStatuses[day.key] === 'partial'"
              [class.text-amber-700]="cachedDayStatuses[day.key] === 'partial'"
              [class.bg-gray-50]="cachedDayStatuses[day.key] === 'none'"
              [class.border-gray-200]="cachedDayStatuses[day.key] === 'none'"
              [class.text-gray-400]="cachedDayStatuses[day.key] === 'none'"
              [title]="cachedDayTooltips[day.key] || ''">
              {{ day.dayOfMonth }}
            </div>
          </div>
          <div class="flex items-center gap-4 mt-3 text-xs text-gray-500">
            <div class="flex items-center gap-1"><div class="w-3 h-3 rounded bg-emerald-200 border border-emerald-300"></div> Hoàn thành</div>
            <div class="flex items-center gap-1"><div class="w-3 h-3 rounded bg-amber-200 border border-amber-300"></div> Một phần</div>
            <div class="flex items-center gap-1"><div class="w-3 h-3 rounded bg-gray-100 border border-gray-200"></div> Chưa làm</div>
          </div>
        </div>

        <!-- Custom Template Form -->
        <div *ngIf="showCustomForm" class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 mb-6">
          <div class="flex items-center justify-between mb-4">
            <h3 class="text-lg font-bold text-gray-900">{{ editingTemplateId ? 'Sửa mẫu' : 'Tạo mẫu mới' }}</h3>
            <button (click)="showCustomForm = false; resetCustomForm()" class="text-gray-400 hover:text-gray-600">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>

          <div class="space-y-4">
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Tên mẫu</label>
                <input [(ngModel)]="customForm.name" type="text" placeholder="VD: Quy trình cuối tuần"
                  class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
              </div>
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Emoji</label>
                  <input [(ngModel)]="customForm.emoji" type="text" placeholder="📋"
                    class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 mb-1">Thời gian (phút)</label>
                  <input [(ngModel)]="customForm.estimatedMinutes" type="number" placeholder="30"
                    class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                </div>
              </div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Mô tả (tuỳ chọn)</label>
              <input [(ngModel)]="customForm.description" type="text" placeholder="Mô tả ngắn về quy trình..."
                class="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
            </div>

            <!-- Items -->
            <div>
              <div class="flex items-center justify-between mb-2">
                <label class="block text-sm font-medium text-gray-700">Các bước</label>
                <button (click)="addCustomItem()"
                  class="text-xs text-blue-600 hover:text-blue-800 font-medium">+ Thêm bước</button>
              </div>
              <div class="space-y-2">
                <div *ngFor="let item of customForm.items; let i = index"
                  class="flex items-center gap-2 bg-gray-50 rounded-lg p-2">
                  <span class="text-xs text-gray-400 w-5 text-center">{{ i + 1 }}</span>
                  <input [(ngModel)]="item.label" placeholder="Tên bước"
                    class="flex-1 border border-gray-300 rounded px-2 py-1.5 text-sm focus:ring-1 focus:ring-blue-500">
                  <select [(ngModel)]="item.group"
                    class="border border-gray-300 rounded px-2 py-1.5 text-sm focus:ring-1 focus:ring-blue-500">
                    <option value="Sáng">Sáng</option>
                    <option value="Trong phiên">Trong phiên</option>
                    <option value="Cuối ngày">Cuối ngày</option>
                  </select>
                  <input [(ngModel)]="item.link" placeholder="/route" title="Deep link (tuỳ chọn)"
                    class="w-24 border border-gray-300 rounded px-2 py-1.5 text-sm focus:ring-1 focus:ring-blue-500">
                  <label class="flex items-center gap-1 text-xs text-gray-500 cursor-pointer">
                    <input type="checkbox" [(ngModel)]="item.isRequired" class="rounded">
                    Bắt buộc
                  </label>
                  <button (click)="removeCustomItem(i)" class="text-red-400 hover:text-red-600">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
                    </svg>
                  </button>
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-2">
              <button (click)="showCustomForm = false; resetCustomForm()"
                class="px-4 py-2 text-sm text-gray-600 hover:text-gray-800">Huỷ</button>
              <button (click)="saveCustomTemplate()"
                [disabled]="!customForm.name || customForm.items.length === 0"
                class="px-4 py-2 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed font-medium">
                {{ editingTemplateId ? 'Lưu thay đổi' : 'Tạo mẫu' }}
              </button>
            </div>
          </div>
        </div>

        <!-- User's Custom Templates Management -->
        <div *ngIf="cachedUserTemplates.length > 0" class="bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-6">
          <h3 class="text-sm font-bold text-gray-700 mb-3">Mẫu của bạn</h3>
          <div class="space-y-2">
            <div *ngFor="let t of cachedUserTemplates" class="flex items-center justify-between bg-gray-50 rounded-lg px-3 py-2">
              <div class="flex items-center gap-2">
                <span>{{ t.emoji }}</span>
                <span class="text-sm font-medium text-gray-700">{{ t.name }}</span>
                <span class="text-xs text-gray-400">~{{ t.estimatedMinutes }} phút</span>
              </div>
              <div class="flex items-center gap-1">
                <button (click)="editTemplate(t)" class="text-xs text-blue-600 hover:text-blue-800 px-2 py-1">Sửa</button>
                <button (click)="deleteCustomTemplate(t.id)" class="text-xs text-red-500 hover:text-red-700 px-2 py-1">Xoá</button>
              </div>
            </div>
          </div>
        </div>

      </div>
    </div>
  `,
  styles: []
})
export class DailyRoutineComponent implements OnInit {
  routine: DailyRoutine | null = null;
  templates: RoutineTemplate[] = [];
  suggestedTemplate: RoutineTemplate | null = null;
  history: RoutineHistory | null = null;
  loading = true;
  groups = ['Sáng', 'Trong phiên', 'Cuối ngày'];

  showCustomForm = false;
  editingTemplateId: string | null = null;
  customForm = this.getEmptyCustomForm();

  // Cached computed values to avoid infinite change detection loops
  cachedLast30Days: { date: Date; key: string; dayOfMonth: number }[] = [];
  cachedGroupItems: Record<string, RoutineItem[]> = {};
  cachedGroupCompletedCount: Record<string, number> = {};
  cachedUserTemplates: RoutineTemplate[] = [];
  cachedStreakMessage: string | null = null;
  cachedEstimatedTimeLeft = 0;
  cachedDayStatuses: Record<string, 'completed' | 'partial' | 'none'> = {};
  cachedDayTooltips: Record<string, string> = {};

  constructor(
    private routineService: DailyRoutineService,
    private notificationService: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadAll();
  }

  private refreshCachedValues(): void {
    this.cachedLast30Days = this.getLast30Days().map(d => ({
      date: d, key: d.toISOString().split('T')[0], dayOfMonth: d.getDate()
    }));
    this.cachedStreakMessage = this.getStreakMessage();
    this.cachedEstimatedTimeLeft = this.getEstimatedTimeLeft();
    this.cachedUserTemplates = this.getUserTemplates();
    for (const group of this.groups) {
      this.cachedGroupItems[group] = this.getGroupItems(group);
      this.cachedGroupCompletedCount[group] = this.cachedGroupItems[group].filter(i => i.isCompleted).length;
    }
    for (const day of this.cachedLast30Days) {
      this.cachedDayStatuses[day.key] = this.getDayStatus(day.date);
      this.cachedDayTooltips[day.key] = this.getDayTooltip(day.date);
    }
  }

  private loadAll(): void {
    this.loading = true;
    forkJoin({
      routine: this.routineService.getToday().pipe(catchError(() => of(null))),
      templates: this.routineService.getTemplates().pipe(catchError(() => of([]))),
      suggested: this.routineService.getSuggestedTemplate().pipe(catchError(() => of(null))),
      history: this.routineService.getHistory(30).pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ routine, templates, suggested, history }) => {
        this.routine = routine;
        this.templates = templates;
        this.history = history;

        // Mark the suggested template
        this.suggestedTemplate = suggested;
        if (suggested) {
          const t = this.templates.find(t => t.id === suggested.id);
          if (t) t.isSuggested = true;
        }
        this.loading = false;
        this.refreshCachedValues();
      },
      error: () => { this.loading = false; }
    });
  }

  startRoutine(templateId: string): void {
    this.loading = true;
    this.routineService.getOrCreateToday(templateId).subscribe({
      next: (r) => {
        this.routine = r;
        this.loading = false;
        this.refreshCachedValues();
        this.notificationService.success('Bắt đầu!', `Đã tạo nhiệm vụ "${r.templateName}"`);
      },
      error: () => {
        this.loading = false;
        this.notificationService.error('Lỗi', 'Không thể tạo nhiệm vụ');
      }
    });
  }

  selectTemplate(t: RoutineTemplate): void {
    if (this.routine?.templateId === t.id) return;
    if (this.routine) {
      // Switch template
      this.routineService.switchTemplate(t.id).subscribe({
        next: (r) => {
          this.routine = r;
          this.refreshCachedValues();
          this.notificationService.success('Đã chuyển', `Chuyển sang "${t.name}"`);
        },
        error: () => this.notificationService.error('Lỗi', 'Không thể chuyển mẫu')
      });
    } else {
      this.startRoutine(t.id);
    }
  }

  toggleItem(item: any): void {
    if (!this.routine) return;
    const newState = !item.isCompleted;
    // Optimistic update
    item.isCompleted = newState;
    this.routineService.completeItem(this.routine.id, item.index, newState).subscribe({
      next: (r) => { this.routine = r; this.refreshCachedValues(); },
      error: () => {
        item.isCompleted = !newState; // Revert
        this.refreshCachedValues();
        this.notificationService.error('Lỗi', 'Không thể cập nhật');
      }
    });
  }

  getGroupItems(group: string): any[] {
    return this.routine?.items.filter(i => i.group === group) ?? [];
  }

  getGroupCompletedCount(group: string): number {
    return this.getGroupItems(group).filter(i => i.isCompleted).length;
  }

  getGroupEmoji(group: string): string {
    switch (group) {
      case 'Sáng': return '🌅';
      case 'Trong phiên': return '📈';
      case 'Cuối ngày': return '🌙';
      default: return '📋';
    }
  }

  getEstimatedTimeLeft(): number {
    if (!this.routine) return 0;
    const template = this.templates.find(t => t.id === this.routine?.templateId);
    const totalMinutes = template?.estimatedMinutes ?? 30;
    const remaining = this.routine.totalCount - this.routine.completedCount;
    return Math.max(0, Math.round(totalMinutes * remaining / this.routine.totalCount));
  }

  getStreakMessage(): string | null {
    if (!this.routine) return null;
    const s = this.routine.currentStreak;
    if (s >= 30) return 'Huyền thoại! 1 tháng không nghỉ!';
    if (s >= 10) return 'Kiên trì! ' + s + ' ngày rồi!';
    if (s >= 5) return 'Tuyệt vời! ' + s + ' ngày liên tiếp!';
    if (s >= 3) return s + ' ngày liên tiếp!';
    return null;
  }

  formatTime(dateString: string): string {
    return new Date(dateString).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  }

  // ─── History ───
  getLast30Days(): Date[] {
    const days: Date[] = [];
    const today = new Date();
    for (let i = 29; i >= 0; i--) {
      const d = new Date(today);
      d.setDate(today.getDate() - i);
      d.setHours(0, 0, 0, 0);
      days.push(d);
    }
    return days;
  }

  getDayStatus(day: Date): 'completed' | 'partial' | 'none' {
    if (!this.history) return 'none';
    const dayStr = day.toISOString().split('T')[0];
    const found = this.history.days.find(d => d.date.split('T')[0] === dayStr);
    if (!found) return 'none';
    if (found.isCompleted) return 'completed';
    if (found.completedCount > 0) return 'partial';
    return 'none';
  }

  getDayTooltip(day: Date): string {
    if (!this.history) return '';
    const dayStr = day.toISOString().split('T')[0];
    const found = this.history.days.find(d => d.date.split('T')[0] === dayStr);
    const dateLabel = day.toLocaleDateString('vi-VN', { weekday: 'short', day: 'numeric', month: 'numeric' });
    if (!found) return dateLabel + ': Chưa làm';
    return `${dateLabel}: ${found.completedCount}/${found.totalCount} (${found.templateName})`;
  }

  // ─── Custom Template ───
  getUserTemplates(): RoutineTemplate[] {
    return this.templates.filter(t => !t.isBuiltIn);
  }

  getEmptyCustomForm() {
    return {
      name: '',
      description: '',
      emoji: '📋',
      estimatedMinutes: 20,
      items: [
        { index: 0, label: '', group: 'Sáng', link: '', isRequired: true, emoji: '' }
      ] as RoutineItemTemplate[]
    };
  }

  resetCustomForm(): void {
    this.customForm = this.getEmptyCustomForm();
    this.editingTemplateId = null;
  }

  addCustomItem(): void {
    this.customForm.items.push({
      index: this.customForm.items.length,
      label: '',
      group: 'Sáng',
      link: '',
      isRequired: false,
      emoji: ''
    });
  }

  removeCustomItem(i: number): void {
    this.customForm.items.splice(i, 1);
    this.customForm.items.forEach((item, idx) => item.index = idx);
  }

  editTemplate(t: RoutineTemplate): void {
    this.editingTemplateId = t.id;
    this.customForm = {
      name: t.name,
      description: t.description || '',
      emoji: t.emoji,
      estimatedMinutes: t.estimatedMinutes,
      items: t.items.map(i => ({ ...i }))
    };
    this.showCustomForm = true;
  }

  saveCustomTemplate(): void {
    if (!this.customForm.name || this.customForm.items.length === 0) return;
    const items = this.customForm.items.map((item, idx) => ({
      ...item,
      index: idx,
      link: item.link || undefined,
      emoji: item.emoji || undefined
    }));

    if (this.editingTemplateId) {
      this.routineService.updateTemplate(this.editingTemplateId, {
        name: this.customForm.name,
        description: this.customForm.description || undefined,
        emoji: this.customForm.emoji,
        estimatedMinutes: this.customForm.estimatedMinutes,
        items
      }).subscribe({
        next: () => {
          this.notificationService.success('Đã lưu', 'Cập nhật mẫu thành công');
          this.showCustomForm = false;
          this.resetCustomForm();
          this.loadAll();
        },
        error: () => this.notificationService.error('Lỗi', 'Không thể cập nhật mẫu')
      });
    } else {
      this.routineService.createTemplate({
        name: this.customForm.name,
        description: this.customForm.description || undefined,
        emoji: this.customForm.emoji,
        estimatedMinutes: this.customForm.estimatedMinutes,
        items
      }).subscribe({
        next: () => {
          this.notificationService.success('Đã tạo', 'Tạo mẫu thành công');
          this.showCustomForm = false;
          this.resetCustomForm();
          this.loadAll();
        },
        error: () => this.notificationService.error('Lỗi', 'Không thể tạo mẫu')
      });
    }
  }

  deleteCustomTemplate(id: string): void {
    this.routineService.deleteTemplate(id).subscribe({
      next: () => {
        this.notificationService.success('Đã xoá', 'Xoá mẫu thành công');
        this.loadAll();
      },
      error: () => this.notificationService.error('Lỗi', 'Không thể xoá mẫu')
    });
  }
}
