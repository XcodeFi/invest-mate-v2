import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService, UserOverviewDto } from '../../core/services/admin.service';
import { ImpersonationService } from '../../core/services/impersonation.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-users-overview',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="max-w-6xl mx-auto p-4 md:p-6">
      <h1 class="text-2xl font-bold mb-1">Tổng quan user</h1>
      <p class="text-sm text-gray-600 mb-4">
        Danh sách toàn bộ user + thống kê hoạt động. Bấm "Xem như" để impersonate (read-only).
      </p>

      <div *ngIf="errorMessage()" class="mb-3 p-3 bg-red-50 border border-red-200 text-red-700 text-sm rounded">
        {{ errorMessage() }}
      </div>

      <div class="bg-white border rounded shadow-sm overflow-hidden">
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-gray-50 text-xs uppercase text-gray-600 tracking-wider">
              <tr>
                <th class="px-3 py-2 text-left">User</th>
                <th class="px-3 py-2 text-left">Role</th>
                <th class="px-3 py-2 text-right"># Portfolio</th>
                <th class="px-3 py-2 text-right"># Trade</th>
                <th class="px-3 py-2 text-left">Giao dịch cuối</th>
                <th class="px-3 py-2 text-left">Đăng nhập cuối</th>
                <th class="px-3 py-2 text-left">Impersonate cuối</th>
                <th class="px-3 py-2 text-right"></th>
              </tr>
            </thead>
            <tbody class="divide-y">
              <tr *ngIf="isLoading()">
                <td colspan="8" class="px-3 py-6 text-center text-gray-500">Đang tải…</td>
              </tr>
              <tr *ngIf="!isLoading() && items().length === 0">
                <td colspan="8" class="px-3 py-6 text-center text-gray-500">Không có user nào.</td>
              </tr>
              <tr *ngFor="let u of items()" class="hover:bg-gray-50">
                <td class="px-3 py-2">
                  <div class="font-semibold">{{ u.name || '(chưa có tên)' }}</div>
                  <div class="text-xs text-gray-500 font-mono truncate max-w-xs">{{ u.email }}</div>
                </td>
                <td class="px-3 py-2">
                  <span
                    class="px-2 py-0.5 text-xs rounded"
                    [class.bg-red-100]="u.role === 'Admin'"
                    [class.text-red-700]="u.role === 'Admin'"
                    [class.bg-gray-100]="u.role !== 'Admin'"
                    [class.text-gray-700]="u.role !== 'Admin'">
                    {{ u.role }}
                  </span>
                </td>
                <td class="px-3 py-2 text-right tabular-nums">{{ u.portfolioCount }}</td>
                <td class="px-3 py-2 text-right tabular-nums">{{ u.tradeCount }}</td>
                <td class="px-3 py-2 text-gray-700">{{ formatDate(u.lastTradeAt) }}</td>
                <td class="px-3 py-2 text-gray-700">{{ formatDate(u.lastLoginAt) }}</td>
                <td class="px-3 py-2 text-gray-700">{{ formatDate(u.lastImpersonatedAt) }}</td>
                <td class="px-3 py-2 text-right">
                  <button
                    type="button"
                    (click)="openImpersonate(u)"
                    class="px-2 py-1 text-xs bg-red-600 text-white rounded hover:bg-red-700">
                    Xem như
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="flex items-center justify-between px-3 py-2 border-t bg-gray-50 text-sm">
          <div class="text-gray-600">
            Tổng: <span class="font-semibold">{{ totalCount() }}</span>
            · Trang {{ page() }}/{{ totalPages() || 1 }}
          </div>
          <div class="flex items-center gap-2">
            <label class="text-xs text-gray-500">Mỗi trang:</label>
            <select [ngModel]="pageSize()" (ngModelChange)="setPageSize($event)"
                    class="px-2 py-1 border rounded text-sm">
              <option [ngValue]="20">20</option>
              <option [ngValue]="50">50</option>
              <option [ngValue]="100">100</option>
            </select>
            <button type="button" (click)="prev()" [disabled]="page() <= 1 || isLoading()"
                    class="px-2 py-1 border rounded disabled:opacity-40">‹</button>
            <button type="button" (click)="next()" [disabled]="page() >= totalPages() || isLoading()"
                    class="px-2 py-1 border rounded disabled:opacity-40">›</button>
          </div>
        </div>
      </div>

      <!-- Reason modal -->
      <div *ngIf="selectedUser()" class="fixed inset-0 bg-black/40 flex items-center justify-center z-40 p-4">
        <div class="bg-white rounded-lg shadow-lg max-w-md w-full p-5">
          <h2 class="text-lg font-bold mb-1">Xác nhận impersonate</h2>
          <p class="text-sm text-gray-600 mb-3">
            Bạn sẽ đăng nhập dưới tư cách
            <span class="font-mono">{{ selectedUser()!.email }}</span>.
            Lý do sẽ được ghi log.
          </p>
          <textarea
            [(ngModel)]="reason"
            rows="3"
            placeholder="Ví dụ: Debug bug #123 — trade không hiện trong dashboard"
            class="w-full px-3 py-2 border rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"></textarea>
          <div class="flex gap-2 justify-end mt-4">
            <button type="button" (click)="cancel()" [disabled]="isStarting()"
                    class="px-4 py-2 text-sm rounded border hover:bg-gray-50">Huỷ</button>
            <button type="button" (click)="confirm()" [disabled]="!reason.trim() || isStarting()"
                    class="px-4 py-2 text-sm rounded bg-red-600 text-white hover:bg-red-700 disabled:opacity-50">
              {{ isStarting() ? 'Đang bắt đầu…' : 'Bắt đầu impersonate' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class UsersOverviewComponent implements OnInit {
  items = signal<UserOverviewDto[]>([]);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(20);
  isLoading = signal(false);
  errorMessage = signal('');

  selectedUser = signal<UserOverviewDto | null>(null);
  reason = '';
  isStarting = signal(false);

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));

  constructor(
    private adminService: AdminService,
    private impersonationService: ImpersonationService,
    private notifications: NotificationService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');
    this.adminService.getUsersOverview(this.page(), this.pageSize()).subscribe({
      next: (res) => {
        this.items.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.page.set(res.page);
        this.pageSize.set(res.pageSize);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err?.error?.error || err?.message || 'Lỗi khi tải danh sách user');
      }
    });
  }

  prev(): void {
    if (this.page() > 1) {
      this.page.set(this.page() - 1);
      this.load();
    }
  }

  next(): void {
    if (this.page() < this.totalPages()) {
      this.page.set(this.page() + 1);
      this.load();
    }
  }

  setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
    this.load();
  }

  formatDate(iso: string | null | undefined): string {
    if (!iso) return '—';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '—';
    const now = new Date();
    const diffMs = now.getTime() - d.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
    if (diffDays === 0) return 'Hôm nay';
    if (diffDays === 1) return 'Hôm qua';
    if (diffDays < 7) return `${diffDays} ngày trước`;
    return d.toLocaleDateString('vi-VN');
  }

  openImpersonate(u: UserOverviewDto): void {
    this.selectedUser.set(u);
    this.reason = '';
  }

  cancel(): void {
    this.selectedUser.set(null);
    this.reason = '';
  }

  confirm(): void {
    const u = this.selectedUser();
    const reason = this.reason.trim();
    if (!u || !reason) return;

    this.isStarting.set(true);
    this.impersonationService.startImpersonate({ targetUserId: u.id, reason }).subscribe({
      next: () => {
        this.isStarting.set(false);
        this.notifications.success('Impersonate bắt đầu', `Đang xem như ${u.email}`);
        window.location.href = '/dashboard';
      },
      error: (err) => {
        this.isStarting.set(false);
        const msg = err?.error?.error || err?.message || 'Không thể bắt đầu impersonate';
        this.errorMessage.set(msg);
        this.notifications.error('Impersonate lỗi', msg);
      }
    });
  }
}
