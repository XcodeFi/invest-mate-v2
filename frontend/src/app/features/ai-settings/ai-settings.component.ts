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
      <div class="bg-gray-800 rounded-xl px-5 py-4 border border-gray-700">
        <h1 class="text-xl font-bold text-white flex items-center gap-2">
          🤖 Cài đặt AI
        </h1>
        <p class="text-sm text-gray-400 mt-1">Cấu hình trợ lý AI cho tài khoản của bạn</p>
      </div>

      <!-- AI Mode Selection -->
      <div class="bg-gray-800 rounded-xl p-5 space-y-4">
        <h2 class="font-semibold text-white text-sm">Chế độ sử dụng AI</h2>

        <!-- Gen Prompt (always on) -->
        <label class="flex items-start gap-3 cursor-pointer group">
          <input type="checkbox" [checked]="true" disabled
                 class="mt-0.5 w-4 h-4 rounded border-gray-600 bg-gray-700 text-blue-600 opacity-60" />
          <div>
            <span class="text-sm text-white font-medium">Tạo prompt (Copy to Clipboard)</span>
            <p class="text-xs text-gray-400 mt-0.5">
              Tạo prompt có dữ liệu thực tế, hiển thị ngay trong ứng dụng. Copy để dùng với Claude, Gemini hoặc bất kỳ AI client nào.
              <span class="text-emerald-400 font-medium">Miễn phí — không cần API key.</span>
            </p>
          </div>
        </label>

        <!-- Use API Key -->
        <label class="flex items-start gap-3 cursor-pointer group">
          <input type="checkbox" [(ngModel)]="useApiStreaming" (ngModelChange)="onModeChange()"
                 class="mt-0.5 w-4 h-4 rounded border-gray-600 bg-gray-700 text-blue-600
                        focus:ring-blue-500 focus:ring-offset-0 cursor-pointer" />
          <div>
            <span class="text-sm text-white font-medium">Tích hợp API (Streaming trực tiếp)</span>
            <p class="text-xs text-gray-400 mt-0.5">
              Gọi API trực tiếp trên cửa sổ chat, nhận phản hồi streaming real-time.
              <span class="text-amber-400 font-medium">Cần API key — tính phí theo token.</span>
            </p>
          </div>
        </label>
      </div>

      <!-- Provider & API Key (only show when useApiStreaming is on) -->
      @if (useApiStreaming) {
        <!-- Provider Tabs -->
        <div class="bg-gray-800 rounded-xl p-1 flex gap-1">
          <button (click)="selectProvider('claude')"
                  class="flex-1 py-2.5 px-4 rounded-lg text-sm font-medium transition-colors"
                  [class]="selectedProvider === 'claude'
                    ? 'bg-blue-600 text-white'
                    : 'text-gray-400 hover:text-white hover:bg-gray-700'">
            Claude (Anthropic)
          </button>
          <button (click)="selectProvider('gemini')"
                  class="flex-1 py-2.5 px-4 rounded-lg text-sm font-medium transition-colors"
                  [class]="selectedProvider === 'gemini'
                    ? 'bg-blue-600 text-white'
                    : 'text-gray-400 hover:text-white hover:bg-gray-700'">
            Gemini (Google)
          </button>
        </div>

        <!-- API Key -->
        <div class="bg-gray-800 rounded-xl p-5 space-y-4">
          <h2 class="font-semibold text-white text-sm">
            {{ selectedProvider === 'claude' ? 'Khóa API Anthropic' : 'Khóa API Google AI' }}
          </h2>

          @if (hasActiveKey() && !editingKey) {
            <div class="flex items-center gap-3">
              <div class="flex-1 bg-gray-700 rounded-lg px-3 py-2 text-sm text-gray-300 font-mono">
                {{ getActiveMaskedKey() }}
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
                  [placeholder]="selectedProvider === 'claude' ? 'sk-ant-api03-...' : 'AIza...'"
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
                @if (hasActiveKey()) {
                  <button (click)="editingKey = false; apiKeyInput = ''"
                          class="text-gray-400 hover:text-white text-sm px-3 py-2">
                    Hủy
                  </button>
                }
              </div>
            </div>
          }

          <!-- Test Connection -->
          @if (hasActiveKey()) {
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
            @if (selectedProvider === 'claude') {
              <option value="claude-sonnet-4-6-20250514">Claude Sonnet 4.6 — Nhanh, tiết kiệm (mặc định)</option>
              <option value="claude-opus-4-6-20250514">Claude Opus 4.6 — Sâu hơn, chính xác hơn</option>
            } @else {
              <option value="gemini-2.0-flash">Gemini 2.0 Flash — Nhanh nhất, tiết kiệm nhất</option>
              <option value="gemini-2.5-flash">Gemini 2.5 Flash — Thông minh, nhanh</option>
              <option value="gemini-2.5-pro">Gemini 2.5 Pro — Mạnh nhất</option>
            }
          </select>
          <div class="text-xs text-gray-500">
            @if (selectedProvider === 'claude') {
              <p>Sonnet: $3/M input, $15/M output tokens</p>
              <p>Opus: $15/M input, $75/M output tokens</p>
            } @else {
              <p>2.0 Flash: $0.10/M input, $0.40/M output tokens</p>
              <p>2.5 Flash: $0.15/M input, $0.60/M output tokens</p>
              <p>2.5 Pro: $1.25/M input, $10/M output tokens</p>
            }
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
        @if (hasActiveKey()) {
          <div class="bg-gray-800 rounded-xl p-5 border border-red-900/50">
            <h2 class="font-semibold text-red-400 text-sm mb-3">Vùng nguy hiểm</h2>
            @if (!confirmDelete) {
              <button (click)="confirmDelete = true"
                      class="text-sm text-red-400 hover:text-red-300 font-medium">
                Xóa tất cả API key
              </button>
            } @else {
              <div class="flex items-center gap-3">
                <p class="text-sm text-gray-300">Xác nhận xóa tất cả API key?</p>
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
          @if (selectedProvider === 'claude') {
            <p>Lấy API key tại: <span class="text-blue-400">console.anthropic.com</span></p>
          } @else {
            <p>Lấy API key tại: <span class="text-blue-400">aistudio.google.com</span></p>
          }
          <p>API key được mã hóa và lưu an toàn trên server.</p>
        </div>
      }
    </div>
  `
})
export class AiSettingsComponent implements OnInit {
  private aiService = inject(AiService);
  private notify = inject(NotificationService);

  settings: AiSettingsDto | null = null;
  selectedProvider: 'claude' | 'gemini' = 'claude';
  apiKeyInput = '';
  showKey = false;
  editingKey = false;
  saving = false;
  testing = false;
  selectedModel = 'claude-sonnet-4-6-20250514';
  confirmDelete = false;
  testResult: { success: boolean; message: string } | null = null;
  useApiStreaming = false;

  ngOnInit(): void {
    this.useApiStreaming = localStorage.getItem('ai_use_api_streaming') === 'true';
    this.loadSettings();
  }

  onModeChange(): void {
    localStorage.setItem('ai_use_api_streaming', String(this.useApiStreaming));
  }

  loadSettings(): void {
    this.aiService.getSettings().subscribe({
      next: (s) => {
        this.settings = s;
        if (s.provider) this.selectedProvider = s.provider as 'claude' | 'gemini';
        if (s.model) this.selectedModel = s.model;
        this.editingKey = !this.hasActiveKey();
      },
      error: () => {
        this.settings = null;
        this.editingKey = true;
      }
    });
  }

  hasActiveKey(): boolean {
    if (!this.settings) return false;
    return this.selectedProvider === 'claude'
      ? this.settings.hasClaudeApiKey
      : this.settings.hasGeminiApiKey;
  }

  getActiveMaskedKey(): string {
    if (!this.settings) return '';
    return (this.selectedProvider === 'claude'
      ? this.settings.maskedClaudeApiKey
      : this.settings.maskedGeminiApiKey) || '';
  }

  selectProvider(provider: 'claude' | 'gemini'): void {
    if (provider === this.selectedProvider) return;
    this.selectedProvider = provider;
    this.apiKeyInput = '';
    this.editingKey = !this.hasActiveKey();
    this.testResult = null;

    // Switch to default model for this provider if current model doesn't match
    const isClaude = provider === 'claude';
    const modelMatchesProvider = isClaude
      ? this.selectedModel.startsWith('claude-')
      : this.selectedModel.startsWith('gemini-');
    if (!modelMatchesProvider) {
      this.selectedModel = isClaude ? 'claude-sonnet-4-6-20250514' : 'gemini-2.0-flash';
    }

    // Only save to backend if at least one API key already exists
    if (this.settings?.hasClaudeApiKey || this.settings?.hasGeminiApiKey) {
      this.aiService.saveSettings({ provider, model: this.selectedModel }).subscribe({
        next: (s) => { this.settings = s; },
        error: () => { this.notify.error('Lỗi', 'Không thể chuyển nhà cung cấp.'); }
      });
    }
  }

  saveApiKey(): void {
    if (!this.apiKeyInput.trim()) return;
    this.saving = true;
    const data = this.selectedProvider === 'claude'
      ? { claudeApiKey: this.apiKeyInput, provider: this.selectedProvider, model: this.selectedModel }
      : { geminiApiKey: this.apiKeyInput, provider: this.selectedProvider, model: this.selectedModel };
    this.aiService.saveSettings(data).subscribe({
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
    if (!this.hasActiveKey()) return;
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
        this.notify.success('Thành công', 'Đã xóa tất cả API key.');
      }
    });
  }

  formatTokens(n: number): string {
    if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
    if (n >= 1_000) return (n / 1_000).toFixed(1) + 'K';
    return n.toString();
  }
}
