import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, OnDestroy, inject, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { marked } from 'marked';
import { AiService, AiStreamChunk, AiChatMessage, AiSettingsDto, AiContextResult } from '../../../core/services/ai.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-ai-chat-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    @if (isOpen) {
      <div class="fixed inset-0 z-50 flex justify-end">
        <!-- Overlay -->
        <div class="absolute inset-0 bg-black/30" (click)="close()"></div>

        <!-- Panel -->
        <div class="relative w-full max-w-lg bg-gray-900 shadow-2xl flex flex-col h-full border-l border-gray-700">
          <!-- Header -->
          <div class="flex items-center justify-between px-4 py-3 border-b border-gray-700 bg-gray-800">
            <div class="flex items-center gap-2 min-w-0">
              <span class="text-lg">✨</span>
              <h3 class="font-semibold text-white text-sm truncate">{{ title }}</h3>
            </div>
            <div class="flex items-center gap-2 shrink-0">
              @if (useApiStreaming && availableModels.length > 1) {
                <select [(ngModel)]="selectedModelId" (ngModelChange)="onModelChange($event)"
                  class="bg-gray-700 text-gray-200 text-xs rounded-md px-2 py-1 border border-gray-600
                         focus:outline-none focus:ring-1 focus:ring-blue-500 max-w-[140px] truncate">
                  @for (m of availableModels; track m.id) {
                    <option [value]="m.id">{{ m.label }}</option>
                  }
                </select>
              }
              @if (tokenUsage.input > 0 || tokenUsage.output > 0) {
                <span class="text-xs text-gray-400" title="Tokens sử dụng">
                  🪙 {{ tokenUsage.input + tokenUsage.output | number }}
                </span>
              }
              <button (click)="close()" class="text-gray-400 hover:text-white transition-colors text-lg leading-none">&times;</button>
            </div>
          </div>

          <!-- Content area — single scrollable container -->
          <div #messagesContainer class="flex-1 overflow-y-auto">
            <div class="px-4 py-4 space-y-4">

              <!-- Prompt Section (always shown for non-chat) -->
              @if (useCase !== 'chat') {
                <!-- Loading prompt -->
                @if (isLoadingPrompt) {
                  <div class="bg-gray-800/60 rounded-xl p-4 border border-gray-700">
                    <div class="flex items-center gap-2 text-gray-400 text-sm">
                      <div class="w-4 h-4 border-2 border-blue-400 border-t-transparent rounded-full animate-spin"></div>
                      Đang tạo prompt...
                    </div>
                  </div>
                }

                <!-- Prompt display -->
                @if (promptResult && !isLoadingPrompt) {
                  <div class="rounded-xl border border-gray-600 overflow-hidden">
                    <!-- Prompt header bar -->
                    <div class="flex items-center justify-between px-4 py-2.5 bg-gray-800 border-b border-gray-600">
                      <button (click)="promptCollapsed = !promptCollapsed"
                              class="flex items-center gap-2 text-sm font-medium text-gray-200 hover:text-white transition-colors">
                        <span class="text-xs transition-transform" [class.rotate-90]="!promptCollapsed">▶</span>
                        Prompt
                        <span class="text-xs text-gray-500 font-normal">{{ promptCollapsed ? '(thu gọn)' : '' }}</span>
                      </button>
                      <div class="flex items-center gap-1.5">
                        <button (click)="copyPrompt()"
                                [disabled]="isCopying"
                                class="flex items-center gap-1 text-xs px-2.5 py-1 rounded-md transition-colors disabled:opacity-50"
                                [class]="copySuccess
                                  ? 'bg-emerald-600/20 text-emerald-300 border border-emerald-600/40'
                                  : 'bg-gray-700 text-gray-200 hover:bg-gray-600 hover:text-white border border-gray-600'">
                          {{ copySuccess ? '✓ Đã copy' : (isCopying ? '...' : '📋 Copy') }}
                        </button>
                      </div>
                    </div>

                    <!-- Prompt body — NO inner scroll, flows naturally -->
                    @if (!promptCollapsed) {
                      <div class="px-4 py-3 bg-gray-950/40">
                        <!-- System Prompt -->
                        <div class="mb-3">
                          <p class="text-xs font-semibold text-blue-400 uppercase tracking-wide mb-2">System Prompt</p>
                          <div class="ai-prompt-content" [innerHTML]="renderMarkdown(promptResult.systemPrompt)"></div>
                        </div>
                        <!-- Divider -->
                        <hr class="border-gray-700 my-3" />
                        <!-- User Message -->
                        <div>
                          <p class="text-xs font-semibold text-emerald-400 uppercase tracking-wide mb-2">User Message</p>
                          <div class="ai-prompt-content" [innerHTML]="renderMarkdown(promptResult.userMessage)"></div>
                        </div>
                      </div>
                    }
                  </div>
                }

                <!-- Prompt error -->
                @if (promptError) {
                  <div class="bg-red-900/40 border border-red-700 rounded-xl px-4 py-3 text-sm text-red-200">
                    <p class="font-semibold mb-1">Lỗi tạo prompt</p>
                    <p>{{ promptError }}</p>
                  </div>
                }
              }

              <!-- Streaming Chat Messages (only when API mode is on) -->
              @if (useApiStreaming) {
                @for (msg of messages; track $index) {
                  @if (msg.role === 'user') {
                    <div class="flex justify-end">
                      <div class="bg-blue-600 text-white rounded-2xl rounded-br-md px-4 py-2 max-w-[85%] text-sm">
                        {{ msg.content }}
                      </div>
                    </div>
                  } @else {
                    <div class="flex justify-start">
                      <div class="ai-prompt-content bg-gray-800 rounded-2xl rounded-bl-md px-4 py-3 max-w-[90%]"
                           [innerHTML]="renderMarkdown(msg.content)">
                      </div>
                    </div>
                  }
                }

                <!-- Streaming message -->
                @if (isStreaming) {
                  <div class="flex justify-start">
                    <div class="ai-prompt-content bg-gray-800 rounded-2xl rounded-bl-md px-4 py-3 max-w-[90%]">
                      @if (currentStreamText) {
                        <span [innerHTML]="renderMarkdown(currentStreamText)"></span>
                      }
                      <span class="inline-block w-2 h-4 bg-blue-400 animate-pulse ml-0.5"></span>
                    </div>
                  </div>
                }
              }

              <!-- Error -->
              @if (errorMessage) {
                <div class="bg-red-900/40 border border-red-700 rounded-xl px-4 py-3 text-sm text-red-200">
                  <p class="font-semibold mb-1">Lỗi</p>
                  <p>{{ errorMessage }}</p>
                  @if (errorMessage.includes('API key')) {
                    <a routerLink="/ai-settings" (click)="close()" class="text-blue-400 hover:underline text-xs mt-2 inline-block">
                      Cài đặt AI &rarr;
                    </a>
                  }
                </div>
              }

              <!-- Empty state (chat mode only) -->
              @if (messages.length === 0 && !isStreaming && !errorMessage && useCase === 'chat') {
                <div class="text-center text-gray-500 py-8">
                  <span class="text-3xl block mb-2">✨</span>
                  <p class="text-sm">Hỏi bất kỳ điều gì về đầu tư, chiến lược, phân tích kỹ thuật...</p>
                </div>
              }
            </div>
          </div>

          <!-- Input -->
          <div class="px-4 py-3 border-t border-gray-700 bg-gray-800">
            @if (useApiStreaming || useCase === 'chat') {
              <div class="flex gap-2">
                <input
                  [(ngModel)]="userInput"
                  (keydown.enter)="sendFollowUp()"
                  [placeholder]="isStreaming ? 'Đang phân tích...' : 'Nhập câu hỏi...'"
                  [disabled]="isStreaming"
                  class="flex-1 bg-gray-700 text-white text-sm rounded-lg px-3 py-2 placeholder-gray-400
                         focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-50"
                />
                <button
                  (click)="sendFollowUp()"
                  [disabled]="isStreaming || !userInput.trim()"
                  class="bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 text-white text-sm font-medium
                         rounded-lg px-4 py-2 transition-colors disabled:opacity-50">
                  Gửi
                </button>
              </div>
            } @else {
              <p class="text-xs text-gray-500 text-center">
                Bật "Tích hợp API" trong <a routerLink="/ai-settings" (click)="close()" class="text-blue-400 hover:underline">Cài đặt AI</a> để chat trực tiếp.
              </p>
            }
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    :host ::ng-deep .rotate-90 { transform: rotate(90deg); }

    /* Shared prompt/chat content styling — high contrast on dark bg */
    :host ::ng-deep .ai-prompt-content {
      font-size: 0.8125rem;
      line-height: 1.6;
      color: #e2e8f0; /* slate-200 — high contrast */
    }
    :host ::ng-deep .ai-prompt-content h1 { font-size: 1rem; font-weight: 700; color: #f1f5f9; margin: 0.75rem 0 0.25rem; }
    :host ::ng-deep .ai-prompt-content h2 { font-size: 0.875rem; font-weight: 600; color: #f1f5f9; margin: 0.75rem 0 0.25rem; }
    :host ::ng-deep .ai-prompt-content h3 { font-size: 0.8125rem; font-weight: 600; color: #cbd5e1; margin: 0.5rem 0 0.25rem; }
    :host ::ng-deep .ai-prompt-content p { margin: 0.25rem 0; }
    :host ::ng-deep .ai-prompt-content ul,
    :host ::ng-deep .ai-prompt-content ol { margin: 0.25rem 0; padding-left: 1.25rem; }
    :host ::ng-deep .ai-prompt-content li { margin: 0.125rem 0; }
    :host ::ng-deep .ai-prompt-content strong { color: #93c5fd; font-weight: 600; } /* blue-300 */
    :host ::ng-deep .ai-prompt-content a { color: #60a5fa; text-decoration: underline; }
    :host ::ng-deep .ai-prompt-content code {
      background: #1e293b; color: #a5f3fc; padding: 0.125rem 0.375rem;
      border-radius: 0.25rem; font-size: 0.75rem;
    }
    :host ::ng-deep .ai-prompt-content pre {
      background: #0f172a; padding: 0.75rem; border-radius: 0.5rem;
      font-size: 0.75rem; overflow-x: auto;
    }
    :host ::ng-deep .ai-prompt-content pre code { background: transparent; padding: 0; }

    /* Tables */
    :host ::ng-deep .ai-prompt-content table {
      width: 100%; border-collapse: collapse; font-size: 0.75rem; margin: 0.5rem 0;
    }
    :host ::ng-deep .ai-prompt-content th {
      text-align: left; padding: 0.375rem 0.5rem;
      background: #1e293b; color: #e2e8f0; font-weight: 600;
      border-bottom: 1px solid #475569;
    }
    :host ::ng-deep .ai-prompt-content td {
      padding: 0.375rem 0.5rem; color: #cbd5e1;
      border-bottom: 1px solid #334155;
    }
    :host ::ng-deep .ai-prompt-content tr:hover td { background: rgba(51,65,85,0.5); }

    /* HR */
    :host ::ng-deep .ai-prompt-content hr { border-color: #475569; margin: 0.75rem 0; }
  `]
})
export class AiChatPanelComponent implements OnChanges, OnDestroy {
  @Input() isOpen = false;
  @Input() title = 'Trợ lý AI';
  @Input() useCase = 'chat';
  @Input() contextData: any = {};
  @Output() isOpenChange = new EventEmitter<boolean>();

  @ViewChild('messagesContainer') messagesContainer?: ElementRef;

  // Prompt mode
  promptResult: AiContextResult | null = null;
  isLoadingPrompt = false;
  promptError: string | null = null;
  promptCollapsed = false;
  isCopying = false;
  copySuccess = false;

  // Streaming mode
  messages: AiChatMessage[] = [];
  isStreaming = false;
  currentStreamText = '';
  errorMessage: string | null = null;
  userInput = '';
  tokenUsage = { input: 0, output: 0 };

  // Settings
  useApiStreaming = false;
  availableModels: { id: string; label: string; provider: string }[] = [];
  selectedModelId = '';

  private streamSub: Subscription | null = null;
  private sanitizer = inject(DomSanitizer);
  private aiService = inject(AiService);
  private notificationService = inject(NotificationService);

  private static readonly ALL_MODELS: { id: string; label: string; provider: string }[] = [
    { id: 'claude-sonnet-4-6-20250514', label: 'Sonnet 4.6', provider: 'claude' },
    { id: 'claude-opus-4-6-20250514', label: 'Opus 4.6', provider: 'claude' },
    { id: 'gemini-2.0-flash', label: 'Gemini 2.0 Flash', provider: 'gemini' },
    { id: 'gemini-2.5-flash', label: 'Gemini 2.5 Flash', provider: 'gemini' },
    { id: 'gemini-2.5-pro', label: 'Gemini 2.5 Pro', provider: 'gemini' },
  ];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen && !changes['isOpen'].previousValue) {
      this.resetState();
      this.useApiStreaming = localStorage.getItem('ai_use_api_streaming') === 'true';

      if (this.useApiStreaming) {
        this.loadModels();
      }

      // For non-chat use cases, auto-build prompt
      if (this.useCase !== 'chat') {
        this.buildPrompt();

        // If API streaming is on, also auto-start streaming
        if (this.useApiStreaming) {
          this.startStream();
        }
      }
    }
  }

  private resetState(): void {
    this.messages = [];
    this.tokenUsage = { input: 0, output: 0 };
    this.errorMessage = null;
    this.currentStreamText = '';
    this.promptResult = null;
    this.promptError = null;
    this.promptCollapsed = false;
    this.isLoadingPrompt = false;
    this.copySuccess = false;
    this.mdCache.clear();
  }

  private buildPrompt(): void {
    this.isLoadingPrompt = true;
    this.promptError = null;

    const ctx = this.contextData || {};
    this.aiService.buildContext(this.useCase, ctx).subscribe({
      next: (result) => {
        this.promptResult = result;
        this.isLoadingPrompt = false;
        this.scrollToBottom();
        // Auto-copy prompt to clipboard immediately
        this.autoCopyPrompt(result);
      },
      error: (err) => {
        this.promptError = err?.error?.error || err?.message || 'Không thể tạo prompt.';
        this.isLoadingPrompt = false;
      }
    });
  }

  /** Auto-copy right after prompt loads — silent (toast only, no button state change) */
  private autoCopyPrompt(result: AiContextResult): void {
    const prompt = `--- SYSTEM PROMPT ---\n${result.systemPrompt}\n\n--- USER MESSAGE ---\n${result.userMessage}`;
    navigator.clipboard.writeText(prompt).then(() => {
      this.copySuccess = true;
      this.notificationService.success('Đã tự động copy', 'Prompt đã sẵn sàng trong clipboard.');
      setTimeout(() => this.copySuccess = false, 2000);
    }).catch(() => {
      // Silent fail for auto-copy — user can still click Copy manually
    });
  }

  copyPrompt(): void {
    if (this.isCopying || !this.promptResult) return;
    this.isCopying = true;
    this.copySuccess = false;

    const prompt = `--- SYSTEM PROMPT ---\n${this.promptResult.systemPrompt}\n\n--- USER MESSAGE ---\n${this.promptResult.userMessage}`;
    navigator.clipboard.writeText(prompt).then(() => {
      this.isCopying = false;
      this.copySuccess = true;
      this.notificationService.success('Đã copy', 'Prompt đã được copy vào clipboard.');
      setTimeout(() => this.copySuccess = false, 2000);
    }).catch(() => {
      this.isCopying = false;
      this.notificationService.error('Lỗi', 'Không thể copy vào clipboard.');
    });
  }

  private loadModels(): void {
    this.aiService.getSettings().subscribe({
      next: (s) => {
        this.availableModels = AiChatPanelComponent.ALL_MODELS.filter(m =>
          (m.provider === 'claude' && s.hasClaudeApiKey) ||
          (m.provider === 'gemini' && s.hasGeminiApiKey)
        );
        this.selectedModelId = s.model || this.availableModels[0]?.id || '';
      },
      error: () => { this.availableModels = []; }
    });
  }

  onModelChange(modelId: string): void {
    const model = AiChatPanelComponent.ALL_MODELS.find(m => m.id === modelId);
    if (!model) return;
    this.aiService.saveSettings({ provider: model.provider, model: modelId }).subscribe();
  }

  ngOnDestroy(): void {
    this.cancelStream();
  }

  close(): void {
    this.cancelStream();
    this.isOpen = false;
    this.isOpenChange.emit(false);
  }

  sendFollowUp(): void {
    const text = this.userInput.trim();
    if (!text || this.isStreaming) return;

    if (!this.useApiStreaming && this.useCase !== 'chat') return;

    this.userInput = '';
    this.messages.push({ role: 'user', content: text });
    this.errorMessage = null;
    this.startStream(text);
  }

  private mdCache = new Map<string, SafeHtml>();

  renderMarkdown(text: string): SafeHtml {
    let cached = this.mdCache.get(text);
    if (!cached) {
      const html = marked.parse(text, { async: false }) as string;
      cached = this.sanitizer.bypassSecurityTrustHtml(html);
      this.mdCache.set(text, cached);
    }
    return cached;
  }

  private startStream(question?: string): void {
    this.cancelStream();
    this.isStreaming = true;
    this.currentStreamText = '';
    this.errorMessage = null;

    const stream$ = this.getStream(question);
    this.streamSub = stream$.subscribe({
      next: (chunk) => {
        if (chunk.type === 'text' && chunk.text) {
          this.currentStreamText += chunk.text;
          this.scrollToBottom();
        } else if (chunk.type === 'usage') {
          if (chunk.inputTokens) this.tokenUsage.input += chunk.inputTokens;
          if (chunk.outputTokens) this.tokenUsage.output += chunk.outputTokens;
        } else if (chunk.type === 'error') {
          this.errorMessage = chunk.errorMessage || 'Đã xảy ra lỗi.';
          this.isStreaming = false;
        }
      },
      error: (err) => {
        this.errorMessage = err?.message || err?.error?.message || 'Lỗi kết nối.';
        this.isStreaming = false;
      },
      complete: () => {
        if (this.currentStreamText) {
          this.messages.push({ role: 'assistant', content: this.currentStreamText });
          this.currentStreamText = '';
        }
        this.isStreaming = false;
        this.scrollToBottom();
      }
    });
  }

  private getStream(question?: string) {
    const ctx = this.contextData || {};

    switch (this.useCase) {
      case 'journal-review':
        return this.aiService.streamJournalReview(ctx.portfolioId, question);
      case 'portfolio-review':
        return this.aiService.streamPortfolioReview(ctx.portfolioId, question);
      case 'trade-plan-advisor':
        return this.aiService.streamTradePlanAdvisor(ctx.tradePlanId, question);
      case 'monthly-summary':
        return this.aiService.streamMonthlySummary(ctx.portfolioId, ctx.year, ctx.month);
      case 'stock-evaluation':
        return this.aiService.streamStockEvaluation(ctx.symbol, question);
      case 'risk-assessment':
        return this.aiService.streamRiskAssessment(ctx.portfolioId, question);
      case 'position-advisor':
        return this.aiService.streamPositionAdvisor(ctx.portfolioId, question);
      case 'trade-analysis':
        return this.aiService.streamTradeAnalysis(ctx.portfolioId, question);
      case 'watchlist-scanner':
        return this.aiService.streamWatchlistScanner(ctx.watchlistId, question);
      case 'daily-briefing':
        return this.aiService.streamDailyBriefing(question);
      case 'chat':
      default:
        return this.aiService.streamChat(
          question || 'Xin chào! Hãy giới thiệu về bạn.',
          this.messages.filter(m => m.role === 'user' || m.role === 'assistant')
        );
    }
  }

  private cancelStream(): void {
    if (this.streamSub) {
      this.streamSub.unsubscribe();
      this.streamSub = null;
    }
    this.isStreaming = false;
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      const el = this.messagesContainer?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    }, 50);
  }
}
