import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, OnDestroy, inject, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { marked } from 'marked';
import { AiService, AiStreamChunk, AiChatMessage, AiSettingsDto } from '../../../core/services/ai.service';
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
            <div class="flex items-center gap-2">
              <span class="text-lg">✨</span>
              <h3 class="font-semibold text-white text-sm">{{ title }}</h3>
            </div>
            <div class="flex items-center gap-2">
              <!-- Model Selector -->
              @if (availableModels.length > 1) {
                <select [(ngModel)]="selectedModelId" (ngModelChange)="onModelChange($event)"
                  class="bg-gray-700 text-gray-200 text-xs rounded-md px-2 py-1 border border-gray-600
                         focus:outline-none focus:ring-1 focus:ring-blue-500 max-w-[160px] truncate">
                  @for (m of availableModels; track m.id) {
                    <option [value]="m.id">{{ m.label }}</option>
                  }
                </select>
              } @else if (availableModels.length === 1) {
                <span class="text-xs text-gray-400">{{ availableModels[0].label }}</span>
              }
              @if (tokenUsage.input > 0 || tokenUsage.output > 0) {
                <span class="text-xs text-gray-400" title="Tokens sử dụng">
                  🪙 {{ tokenUsage.input + tokenUsage.output | number }}
                </span>
              }
              <!-- Copy Prompt Button -->
              <button (click)="copyContext()" [disabled]="isCopying"
                class="text-gray-400 hover:text-green-400 transition-colors text-sm leading-none disabled:opacity-50"
                [title]="isCopying ? 'Đang copy...' : 'Copy prompt để dùng với Claude/Gemini client'">
                {{ isCopying ? '...' : '📋' }}
              </button>
              <button (click)="close()" class="text-gray-400 hover:text-white transition-colors text-lg leading-none">&times;</button>
            </div>
          </div>

          <!-- Messages -->
          <div #messagesContainer class="flex-1 overflow-y-auto px-4 py-4 space-y-4">
            @for (msg of messages; track $index) {
              @if (msg.role === 'user') {
                <div class="flex justify-end">
                  <div class="bg-blue-600 text-white rounded-2xl rounded-br-md px-4 py-2 max-w-[85%] text-sm">
                    {{ msg.content }}
                  </div>
                </div>
              } @else {
                <div class="flex justify-start">
                  <div class="bg-gray-800 text-gray-100 rounded-2xl rounded-bl-md px-4 py-3 max-w-[90%] text-sm prose prose-sm prose-invert max-w-none
                              [&_h1]:text-base [&_h2]:text-sm [&_h3]:text-sm [&_h1]:font-bold [&_h2]:font-semibold
                              [&_p]:my-1 [&_ul]:my-1 [&_ol]:my-1 [&_li]:my-0.5
                              [&_code]:bg-gray-700 [&_code]:px-1 [&_code]:rounded [&_code]:text-xs
                              [&_pre]:bg-gray-950 [&_pre]:p-2 [&_pre]:rounded-lg [&_pre]:text-xs
                              [&_strong]:text-blue-300 [&_a]:text-blue-400"
                       [innerHTML]="renderMarkdown(msg.content)">
                  </div>
                </div>
              }
            }

            <!-- Streaming message -->
            @if (isStreaming) {
              <div class="flex justify-start">
                <div class="bg-gray-800 text-gray-100 rounded-2xl rounded-bl-md px-4 py-3 max-w-[90%] text-sm prose prose-sm prose-invert max-w-none
                            [&_h1]:text-base [&_h2]:text-sm [&_h3]:text-sm [&_h1]:font-bold [&_h2]:font-semibold
                            [&_p]:my-1 [&_ul]:my-1 [&_ol]:my-1 [&_li]:my-0.5
                            [&_code]:bg-gray-700 [&_code]:px-1 [&_code]:rounded [&_code]:text-xs
                            [&_pre]:bg-gray-950 [&_pre]:p-2 [&_pre]:rounded-lg [&_pre]:text-xs
                            [&_strong]:text-blue-300 [&_a]:text-blue-400">
                  @if (currentStreamText) {
                    <span [innerHTML]="renderMarkdown(currentStreamText)"></span>
                  }
                  <span class="inline-block w-2 h-4 bg-blue-400 animate-pulse ml-0.5"></span>
                </div>
              </div>
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

            <!-- Empty state -->
            @if (messages.length === 0 && !isStreaming && !errorMessage && useCase === 'chat') {
              <div class="text-center text-gray-500 py-8">
                <span class="text-3xl block mb-2">✨</span>
                <p class="text-sm">Hỏi bất kỳ điều gì về đầu tư, chiến lược, phân tích kỹ thuật...</p>
              </div>
            }
          </div>

          <!-- Input -->
          <div class="px-4 py-3 border-t border-gray-700 bg-gray-800">
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
          </div>
        </div>
      </div>
    }
  `
})
export class AiChatPanelComponent implements OnChanges, OnDestroy {
  @Input() isOpen = false;
  @Input() title = 'Trợ lý AI';
  @Input() useCase = 'chat';
  @Input() contextData: any = {};
  @Output() isOpenChange = new EventEmitter<boolean>();

  @ViewChild('messagesContainer') messagesContainer?: ElementRef;

  messages: AiChatMessage[] = [];
  isStreaming = false;
  currentStreamText = '';
  errorMessage: string | null = null;
  userInput = '';
  tokenUsage = { input: 0, output: 0 };
  isCopying = false;

  // Model selector
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
      this.messages = [];
      this.tokenUsage = { input: 0, output: 0 };
      this.errorMessage = null;
      this.currentStreamText = '';
      this.loadModels();

      // Auto-start for non-chat use cases
      if (this.useCase !== 'chat') {
        this.startStream();
      }
    }
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

    this.userInput = '';
    this.messages.push({ role: 'user', content: text });
    this.errorMessage = null;
    this.startStream(text);
  }

  copyContext(): void {
    if (this.isCopying) return;
    this.isCopying = true;

    const ctx = this.contextData || {};
    const contextPayload: any = { ...ctx };

    // Map useCase-specific fields
    if (this.useCase === 'chat') {
      contextPayload.message = this.userInput || 'Xin chào';
      contextPayload.history = this.messages;
    }

    this.aiService.buildContext(this.useCase, contextPayload).subscribe({
      next: (result) => {
        const prompt = `--- SYSTEM PROMPT ---\n${result.systemPrompt}\n\n--- USER MESSAGE ---\n${result.userMessage}`;
        navigator.clipboard.writeText(prompt).then(() => {
          this.notificationService.success('Đã copy', 'Prompt đã được copy vào clipboard. Paste vào Claude/Gemini client để sử dụng.');
          this.isCopying = false;
        }).catch(() => {
          this.notificationService.error('Lỗi', 'Không thể copy vào clipboard.');
          this.isCopying = false;
        });
      },
      error: (err) => {
        this.notificationService.error('Lỗi', err?.error?.error || 'Không thể tạo prompt.');
        this.isCopying = false;
      }
    });
  }

  renderMarkdown(text: string): SafeHtml {
    const html = marked.parse(text, { async: false }) as string;
    return this.sanitizer.bypassSecurityTrustHtml(html);
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
