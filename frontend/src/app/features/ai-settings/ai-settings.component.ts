import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AiService, AiSettingsDto } from '../../core/services/ai.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-ai-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="max-w-2xl mx-auto px-4 py-6 space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-xl font-bold text-white flex items-center gap-2">
          🤖 Cài đặt AI
        </h1>
        <p class="text-sm text-gray-400 mt-1">Cấu hình trợ lý AI Claude cho tài khoản của bạn</p>
      </div>

      <!-- API Key -->
      <div class="bg-gray-800 rounded-xl p-5 space-y-4">
        <h2 class="font-semibold text-white text-sm">Khóa API Anthropic</h2>

        @if (settings?.hasApiKey && !editingKey) {
          <div class="flex items-center gap-3">
            <div class="flex-1 bg-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 font-mono">
              {{ settings!.maskedApiKey }}
            </div>
            <button (click)="editingKey = true"
                    class="text-blue-400 hover:text-blue-300 text-sm font-medium">
              Thay đổi
            </button>
          </div>
        } @else {
          <div class="space-y-3">
            <div class="relative">
              <input
                [type]="showKey ? 'text' : 'password'"
                [(ngModel)]="apiKeyInput"
                placeholder="sk-ant-api03-..."
                class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2 pr-10
                       placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-blue-500 font-mono"
              />
              <button (click)="showKey = !showKey"
                      class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-white text-xs">
                {{ showKey ? 'Ẩn' : 'Hiện' }}
              </button>
            </div>
            <div class="flex gap-2">
              <button (click)="saveApiKey()"
                      [disabled]="saving || !apiKeyInput.trim()"
                      class="bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 text-white text-sm font-medium
                             rounded-lg px-4 py-2 transition-colors disabled:opacity-50">
                {{ saving ? 'Đang lưu...' : 'Lưu' }}
              </button>
              @if (settings?.hasApiKey) {
                <button (click)="editingKey = false; apiKeyInput = ''"
                        class="text-gray-400 hover:text-white text-sm px-3 py-2">
                  Hủy
                </button>
              }
            </div>
          </div>
        }

        <!-- Test Connection -->
        @if (settings?.hasApiKey) {
          <div class="pt-2 border-t border-gray-700">
            <button (click)="testConnection()"
                    [disabled]="testing"
                    class="text-sm text-emerald-400 hover:text-emerald-300 font-medium disabled:opacity-50">
              {{ testing ? 'Đang kiểm tra...' : '🔌 Kiểm tra kết nối' }}
            </button>
            @if (testResult) {
              <p class="text-xs mt-1" [class]="testResult.success ? 'text-emerald-400' : 'text-red-400'">
                {{ testResult.message }}
              </p>
            }
          </div>
        }
      </div>

      <!-- Model Selection -->
      <div class="bg-gray-800 rounded-xl p-5 space-y-4">
        <h2 class="font-semibold text-white text-sm">Mô hình AI</h2>
        <select [(ngModel)]="selectedModel" (ngModelChange)="saveModel()"
                class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2
                       focus:outline-none focus:ring-1 focus:ring-blue-500">
          <option value="claude-sonnet-4-6-20250514">Claude Sonnet 4.6 — Nhanh, tiết kiệm (mặc định)</option>
          <option value="claude-opus-4-6-20250514">Claude Opus 4.6 — Sâu hơn, chính xác hơn</option>
        </select>
        <div class="text-xs text-gray-500">
          <p>Sonnet: $3/M input, $15/M output tokens</p>
          <p>Opus: $15/M input, $75/M output tokens</p>
        </div>
      </div>

      <!-- Usage Stats -->
      <div class="bg-gray-800 rounded-xl p-5 space-y-4">
        <h2 class="font-semibold text-white text-sm">Thống kê sử dụng</h2>
        <div class="grid grid-cols-3 gap-4">
          <div class="text-center">
            <p class="text-lg font-bold text-white">{{ formatTokens(settings?.totalInputTokens || 0) }}</p>
            <p class="text-xs text-gray-400">Input tokens</p>
          </div>
          <div class="text-center">
            <p class="text-lg font-bold text-white">{{ formatTokens(settings?.totalOutputTokens || 0) }}</p>
            <p class="text-xs text-gray-400">Output tokens</p>
          </div>
          <div class="text-center">
            <p class="text-lg font-bold text-emerald-400">\${{ (settings?.estimatedCostUsd || 0).toFixed(4) }}</p>
            <p class="text-xs text-gray-400">Chi phí ước tính</p>
          </div>
        </div>
      </div>

      <!-- Danger Zone -->
      @if (settings?.hasApiKey) {
        <div class="bg-gray-800 rounded-xl p-5 border border-red-900/50">
          <h2 class="font-semibold text-red-400 text-sm mb-3">Vùng nguy hiểm</h2>
          @if (!confirmDelete) {
            <button (click)="confirmDelete = true"
                    class="text-sm text-red-400 hover:text-red-300 font-medium">
              Xóa API key
            </button>
          } @else {
            <div class="flex items-center gap-3">
              <p class="text-sm text-gray-300">Xác nhận xóa API key?</p>
              <button (click)="deleteApiKey()"
                      class="bg-red-600 hover:bg-red-700 text-white text-sm rounded-lg px-3 py-1.5 font-medium">
                Xóa
              </button>
              <button (click)="confirmDelete = false"
                      class="text-gray-400 hover:text-white text-sm">
                Hủy
              </button>
            </div>
          }
        </div>
      }

      <!-- Help -->
      <div class="text-xs text-gray-500 space-y-1">
        <p>Lấy API key tại: <span class="text-blue-400">console.anthropic.com</span></p>
        <p>API key được mã hóa và lưu an toàn trên server.</p>
      </div>
    </div>
  `
})
export class AiSettingsComponent implements OnInit {
  private aiService = inject(AiService);
  private notify = inject(NotificationService);

  settings: AiSettingsDto | null = null;
  apiKeyInput = '';
  showKey = false;
  editingKey = false;
  saving = false;
  testing = false;
  selectedModel = 'claude-sonnet-4-6-20250514';
  confirmDelete = false;
  testResult: { success: boolean; message: string } | null = null;

  ngOnInit(): void {
    this.loadSettings();
  }

  loadSettings(): void {
    this.aiService.getSettings().subscribe({
      next: (s) => {
        this.settings = s;
        if (s.model) this.selectedModel = s.model;
        this.editingKey = !s.hasApiKey;
      },
      error: () => {
        this.settings = null;
        this.editingKey = true;
      }
    });
  }

  saveApiKey(): void {
    if (!this.apiKeyInput.trim()) return;
    this.saving = true;
    this.aiService.saveSettings({ apiKey: this.apiKeyInput, model: this.selectedModel }).subscribe({
      next: (s) => {
        this.settings = s;
        this.apiKeyInput = '';
        this.editingKey = false;
        this.saving = false;
        this.notify.success('Thành công', 'Đã lưu API key thành công!');
      },
      error: () => {
        this.saving = false;
        this.notify.error('Lỗi', 'Lỗi khi lưu API key.');
      }
    });
  }

  saveModel(): void {
    if (!this.settings?.hasApiKey) return;
    this.aiService.saveSettings({ model: this.selectedModel }).subscribe({
      next: (s) => {
        this.settings = s;
        this.notify.success('Thành công', 'Đã cập nhật mô hình AI.');
      }
    });
  }

  testConnection(): void {
    this.testing = true;
    this.testResult = null;
    this.aiService.testConnection().subscribe({
      next: (r) => {
        this.testResult = { success: true, message: r.message };
        this.testing = false;
      },
      error: (err) => {
        this.testResult = { success: false, message: err?.error?.message || 'Kết nối thất bại.' };
        this.testing = false;
      }
    });
  }

  deleteApiKey(): void {
    this.aiService.deleteSettings().subscribe({
      next: () => {
        this.settings = null;
        this.editingKey = true;
        this.confirmDelete = false;
        this.notify.success('Thành công', 'Đã xóa API key.');
      }
    });
  }

  formatTokens(n: number): string {
    if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
    if (n >= 1_000) return (n / 1_000).toFixed(1) + 'K';
    return n.toString();
  }
}
