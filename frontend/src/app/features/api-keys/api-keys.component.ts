import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiKeysService, ApiKeyDto, CreatedApiKeyDto } from '../../core/services/api-keys.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-api-keys',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="max-w-3xl mx-auto px-4 py-6 space-y-5">
      <!-- Header -->
      <div class="flex items-center justify-between gap-3">
        <div>
          <h1 class="text-xl font-bold text-white flex items-center gap-2">🔑 Khóa API</h1>
          <p class="text-sm text-gray-400 mt-1">
            Khóa truy cập cá nhân cho công cụ ngoài (VD: trợ lý NPU kéo bản tin hằng ngày).
            Token chỉ hiển thị một lần duy nhất khi tạo.
          </p>
        </div>
        <button (click)="openCreate()"
          class="shrink-0 bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors">
          + Tạo khóa
        </button>
      </div>

      <!-- List -->
      <div class="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
        @if (loading) {
          <div class="p-6 text-center text-gray-400 text-sm">Đang tải…</div>
        } @else if (keys.length === 0) {
          <div class="p-8 text-center text-gray-400 text-sm">
            Chưa có khóa API nào. Nhấn “Tạo khóa” để bắt đầu.
          </div>
        } @else {
          <ul class="divide-y divide-gray-700">
            @for (key of keys; track key.id) {
              <li class="p-4 flex items-center justify-between gap-4">
                <div class="min-w-0">
                  <div class="flex items-center gap-2">
                    <span class="font-medium text-white truncate">{{ key.name }}</span>
                    @if (key.revokedAt) {
                      <span class="text-xs px-2 py-0.5 rounded-full bg-gray-700 text-gray-300">Đã thu hồi</span>
                    } @else if (!key.isActive) {
                      <span class="text-xs px-2 py-0.5 rounded-full bg-amber-900/50 text-amber-300">Hết hạn</span>
                    } @else {
                      <span class="text-xs px-2 py-0.5 rounded-full bg-green-900/50 text-green-300">Đang hoạt động</span>
                    }
                  </div>
                  <div class="text-xs text-gray-400 mt-1 font-mono">{{ key.prefix }}…</div>
                  <div class="text-xs text-gray-500 mt-1">
                    Tạo {{ key.createdAt | date:'dd/MM/yyyy' }}
                    · Hết hạn {{ key.expiresAt | date:'dd/MM/yyyy' }}
                    · Dùng lần cuối {{ key.lastUsedAt ? (key.lastUsedAt | date:'dd/MM/yyyy HH:mm') : 'chưa dùng' }}
                  </div>
                </div>
                @if (key.isActive) {
                  @if (revokeConfirmId === key.id) {
                    <div class="flex items-center gap-2 shrink-0">
                      <span class="text-xs text-gray-300">Thu hồi?</span>
                      <button (click)="revokeConfirmId = null"
                        class="text-gray-400 hover:text-white text-sm px-2 py-1">Hủy</button>
                      <button (click)="doRevoke(key)"
                        class="bg-red-600 hover:bg-red-700 text-white text-sm rounded-lg px-3 py-1.5 font-medium">
                        Thu hồi
                      </button>
                    </div>
                  } @else {
                    <button (click)="revokeConfirmId = key.id"
                      class="shrink-0 text-red-400 hover:text-red-300 text-sm font-medium">
                      Thu hồi
                    </button>
                  }
                }
              </li>
            }
          </ul>
        }
      </div>
    </div>

    <!-- Create modal -->
    @if (showCreate) {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
        <div class="bg-gray-800 rounded-xl border border-gray-700 max-w-md w-full">
          <div class="px-6 py-4 border-b border-gray-700">
            <h2 class="text-lg font-semibold text-white">Tạo khóa API</h2>
          </div>
          <div class="px-6 py-4 space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1">Tên khóa</label>
              <input [(ngModel)]="newName" [disabled]="creating" maxlength="100"
                placeholder="VD: Trợ lý NPU"
                class="w-full bg-gray-700 text-white rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1">Hết hạn sau</label>
              <select [(ngModel)]="newExpiresInDays" [disabled]="creating"
                class="w-full bg-gray-700 text-white rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500">
                <option [ngValue]="30">30 ngày</option>
                <option [ngValue]="90">90 ngày (mặc định)</option>
                <option [ngValue]="180">180 ngày</option>
                <option [ngValue]="365">365 ngày</option>
              </select>
            </div>
          </div>
          <div class="px-6 py-4 border-t border-gray-700 flex gap-2 justify-end">
            <button (click)="showCreate = false" [disabled]="creating"
              class="text-gray-400 hover:text-white px-4 py-2 disabled:opacity-50">Hủy</button>
            <button (click)="doCreate()" [disabled]="creating || !newName.trim()"
              class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium disabled:opacity-50">
              {{ creating ? 'Đang tạo…' : 'Tạo' }}
            </button>
          </div>
        </div>
      </div>
    }

    <!-- Plaintext token modal (shown once) -->
    @if (createdKey) {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4">
        <div class="bg-gray-800 rounded-xl border border-gray-700 max-w-md w-full">
          <div class="px-6 py-4 border-b border-gray-700">
            <h2 class="text-lg font-semibold text-white">Khóa “{{ createdKey.name }}” đã được tạo</h2>
          </div>
          <div class="px-6 py-4 space-y-3">
            <div class="rounded-lg bg-amber-900/40 border border-amber-700/50 px-3 py-2 text-xs text-amber-200">
              ⚠️ Sao chép ngay — token chỉ hiển thị một lần duy nhất và không thể lấy lại.
            </div>
            <div class="flex items-center gap-2">
              <code class="flex-1 min-w-0 truncate bg-gray-900 text-green-300 rounded-lg px-3 py-2 text-sm font-mono">
                {{ createdKey.token }}
              </code>
              <button (click)="copyToken()"
                class="shrink-0 bg-gray-700 hover:bg-gray-600 text-white rounded-lg px-3 py-2 text-sm">
                Sao chép
              </button>
            </div>
          </div>
          <div class="px-6 py-4 border-t border-gray-700 flex justify-end">
            <button (click)="dismissCreated()"
              class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium">
              Đã lưu, đóng
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class ApiKeysComponent implements OnInit {
  private service = inject(ApiKeysService);
  private notify = inject(NotificationService);

  keys: ApiKeyDto[] = [];
  loading = true;

  showCreate = false;
  creating = false;
  newName = '';
  newExpiresInDays = 90;
  createdKey: CreatedApiKeyDto | null = null;

  revokeConfirmId: string | null = null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.service.list().subscribe({
      next: keys => { this.keys = keys; this.loading = false; },
      error: () => { this.notify.error('Lỗi', 'Không tải được danh sách khóa API.'); this.loading = false; }
    });
  }

  openCreate(): void {
    this.newName = '';
    this.newExpiresInDays = 90;
    this.showCreate = true;
  }

  doCreate(): void {
    const name = this.newName.trim();
    if (!name) return;
    this.creating = true;
    this.service.create({ name, expiresInDays: this.newExpiresInDays }).subscribe({
      next: created => {
        this.creating = false;
        this.showCreate = false;
        this.createdKey = created;
        this.load();
      },
      error: err => {
        this.creating = false;
        this.notify.error('Lỗi', this.extractError(err) ?? 'Không tạo được khóa API.');
      }
    });
  }

  async copyToken(): Promise<void> {
    if (!this.createdKey) return;
    try {
      await navigator.clipboard.writeText(this.createdKey.token);
      this.notify.success('Đã sao chép', 'Token đã được sao chép vào clipboard.');
    } catch {
      this.notify.warning('Không sao chép được', 'Hãy chọn và sao chép token thủ công.');
    }
  }

  dismissCreated(): void {
    this.createdKey = null;
  }

  doRevoke(key: ApiKeyDto): void {
    this.service.revoke(key.id).subscribe({
      next: () => {
        this.revokeConfirmId = null;
        this.notify.success('Thành công', `Đã thu hồi khóa “${key.name}”.`);
        this.load();
      },
      error: () => this.notify.error('Lỗi', 'Không thu hồi được khóa API.')
    });
  }

  private extractError(err: any): string | null {
    return err?.error?.detail ?? err?.error?.title ?? err?.error?.message ?? null;
  }
}
