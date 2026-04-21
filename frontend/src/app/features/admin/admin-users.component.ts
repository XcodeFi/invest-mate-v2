import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService, AdminUserDto } from '../../core/services/admin.service';
import { ImpersonationService } from '../../core/services/impersonation.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-4 md:p-6">
      <h1 class="text-2xl font-bold mb-1">Admin — Tìm user để impersonate</h1>
      <p class="text-sm text-gray-600 mb-4">
        Chế độ debug: đăng nhập dưới tư cách user khác (read-only).
        Mọi thao tác được ghi log. Mutation bị chặn mặc định.
      </p>

      <div class="flex gap-2 mb-4">
        <input
          type="email"
          [(ngModel)]="query"
          (keyup.enter)="search()"
          placeholder="Nhập email (hỗ trợ tìm một phần, ví dụ: truong)"
          class="flex-1 px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500" />
        <button
          type="button"
          (click)="search()"
          [disabled]="isSearching()"
          class="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50">
          {{ isSearching() ? 'Đang tìm…' : 'Tìm' }}
        </button>
      </div>

      <div *ngIf="errorMessage()" class="mb-3 p-3 bg-red-50 border border-red-200 text-red-700 text-sm rounded">
        {{ errorMessage() }}
      </div>

      <div *ngIf="searched() && results().length === 0 && !isSearching()"
           class="p-4 text-center text-gray-500 bg-gray-50 rounded">
        Không tìm thấy user phù hợp.
      </div>

      <ul class="space-y-2">
        <li *ngFor="let user of results()"
            class="flex items-center justify-between gap-3 p-3 bg-white border rounded hover:shadow-sm">
          <div class="min-w-0">
            <div class="font-semibold truncate">{{ user.name || '(chưa có tên)' }}</div>
            <div class="text-sm text-gray-600 truncate font-mono">{{ user.email }}</div>
            <div class="text-xs text-gray-400">
              Role: {{ user.role }} · Tạo: {{ user.createdAt | date:'yyyy-MM-dd' }}
            </div>
          </div>
          <button
            type="button"
            (click)="openImpersonate(user)"
            class="shrink-0 px-3 py-1.5 bg-red-600 text-white text-sm rounded hover:bg-red-700">
            Xem như user này
          </button>
        </li>
      </ul>

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
            <button
              type="button"
              (click)="cancel()"
              [disabled]="isStarting()"
              class="px-4 py-2 text-sm rounded border hover:bg-gray-50">
              Huỷ
            </button>
            <button
              type="button"
              (click)="confirm()"
              [disabled]="!reason.trim() || isStarting()"
              class="px-4 py-2 text-sm rounded bg-red-600 text-white hover:bg-red-700 disabled:opacity-50">
              {{ isStarting() ? 'Đang bắt đầu…' : 'Bắt đầu impersonate' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class AdminUsersComponent {
  query = '';
  reason = '';
  results = signal<AdminUserDto[]>([]);
  selectedUser = signal<AdminUserDto | null>(null);
  isSearching = signal(false);
  isStarting = signal(false);
  searched = signal(false);
  errorMessage = signal('');

  constructor(
    private adminService: AdminService,
    private impersonationService: ImpersonationService,
    private notifications: NotificationService
  ) {}

  search(): void {
    const q = this.query.trim();
    this.errorMessage.set('');
    if (!q) {
      this.results.set([]);
      this.searched.set(false);
      return;
    }
    this.isSearching.set(true);
    this.adminService.searchUsers(q).subscribe({
      next: (users) => {
        this.results.set(users);
        this.searched.set(true);
        this.isSearching.set(false);
      },
      error: (err) => {
        this.isSearching.set(false);
        this.errorMessage.set(err?.error?.error || err?.message || 'Lỗi khi tìm user');
      }
    });
  }

  openImpersonate(user: AdminUserDto): void {
    this.selectedUser.set(user);
    this.reason = '';
  }

  cancel(): void {
    this.selectedUser.set(null);
    this.reason = '';
  }

  confirm(): void {
    const user = this.selectedUser();
    const reason = this.reason.trim();
    if (!user || !reason) return;

    this.isStarting.set(true);
    this.impersonationService.startImpersonate({ targetUserId: user.id, reason }).subscribe({
      next: () => {
        this.isStarting.set(false);
        this.notifications.success('Impersonate bắt đầu', `Đang xem như ${user.email}`);
        // Navigate to dashboard and reload so the whole app picks up the new token
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
