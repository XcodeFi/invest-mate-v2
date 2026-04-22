import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subject, forkJoin, of } from 'rxjs';
import { debounceTime, catchError } from 'rxjs/operators';
import { marked } from 'marked';

interface GuideTopic {
  id: string;
  title: string;
  description: string;
  icon: string;
  color: string;
}

interface TopicContent {
  topic: GuideTopic;
  markdown: string;
  normalized: string;
  html: SafeHtml;
}

interface SearchResult {
  topic: GuideTopic;
  snippet: string;
}

@Component({
  selector: 'app-help',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
          <div class="flex items-center gap-3">
            <span class="text-2xl">📖</span>
            <div>
              <h1 class="text-2xl sm:text-3xl font-bold text-gray-900">Hướng dẫn sử dụng</h1>
              <p class="text-sm text-gray-500 mt-1">Tìm hiểu cách sử dụng Investment Mate</p>
            </div>
          </div>

          <!-- Search bar -->
          <div class="mt-4 relative">
            <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <svg class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
              </svg>
            </div>
            <input type="text"
              [(ngModel)]="searchQuery"
              (ngModelChange)="onSearchChange($event)"
              placeholder="Tìm kiếm hướng dẫn... (hỗ trợ gõ không dấu)"
              class="w-full pl-10 pr-10 py-3 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
            <button *ngIf="searchQuery" (click)="clearSearch()"
              class="absolute inset-y-0 right-0 pr-3 flex items-center text-gray-400 hover:text-gray-600">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>
        </div>
      </div>

      <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-6">

        <!-- Loading -->
        <div *ngIf="loading" class="flex items-center justify-center py-24">
          <div class="animate-spin w-6 h-6 border-3 border-blue-600 border-t-transparent rounded-full"></div>
          <span class="text-gray-500 text-sm ml-3">Đang tải hướng dẫn...</span>
        </div>

        <!-- Error -->
        <div *ngIf="error" class="text-center py-24 text-red-500 text-sm">{{ error }}</div>

        <!-- Index View: Card Grid -->
        <div *ngIf="!loading && !error && !selectedTopic && searchQuery.length < 2">
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            <button *ngFor="let topic of topics"
              (click)="selectTopic(topic)"
              class="bg-white rounded-lg border border-gray-200 p-4 text-left hover:shadow-md hover:border-blue-300 transition-all group">
              <div class="text-2xl mb-2">{{ topic.icon }}</div>
              <div class="font-semibold text-gray-900 group-hover:text-blue-600 text-sm">{{ topic.title }}</div>
              <div class="text-xs text-gray-500 mt-1 leading-relaxed">{{ topic.description }}</div>
            </button>
          </div>
        </div>

        <!-- Search Results -->
        <div *ngIf="!loading && !selectedTopic && searchQuery.length >= 2">
          <div class="text-sm text-gray-500 mb-4">
            <span *ngIf="searchResults.length > 0">{{ searchResults.length }} kết quả cho "{{ searchQuery }}"</span>
            <span *ngIf="searchResults.length === 0">Không tìm thấy kết quả cho "{{ searchQuery }}"</span>
          </div>
          <div class="space-y-3">
            <button *ngFor="let result of searchResults"
              (click)="selectTopic(result.topic)"
              class="w-full bg-white rounded-lg border border-gray-200 p-4 text-left hover:shadow-md hover:border-blue-300 transition-all">
              <div class="flex items-center gap-2 mb-1">
                <span>{{ result.topic.icon }}</span>
                <span class="font-medium text-sm text-blue-600">{{ result.topic.title }}</span>
              </div>
              <div class="text-xs text-gray-600 leading-relaxed" [innerHTML]="result.snippet"></div>
            </button>
          </div>
        </div>

        <!-- Topic Detail View -->
        <div *ngIf="!loading && selectedTopic">
          <button (click)="backToIndex()"
            class="inline-flex items-center gap-1 text-sm text-blue-600 hover:text-blue-800 mb-4">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"/>
            </svg>
            Quay lại danh sách
          </button>

          <div class="bg-white rounded-lg border border-gray-200 p-6 sm:p-8">
            <div class="guide-content" [innerHTML]="selectedTopic.html"></div>
          </div>
        </div>

      </div>
    </div>
  `,
  styles: [`
    :host ::ng-deep .guide-content h1 { font-size: 1.5rem; font-weight: 700; color: #111827; margin-bottom: 1rem; }
    :host ::ng-deep .guide-content h2 { font-size: 1.125rem; font-weight: 600; color: #1f2937; margin-top: 2rem; margin-bottom: 0.75rem; padding-bottom: 0.5rem; border-bottom: 1px solid #e5e7eb; }
    :host ::ng-deep .guide-content h3 { font-size: 0.975rem; font-weight: 600; color: #374151; margin-top: 1.5rem; margin-bottom: 0.5rem; }
    :host ::ng-deep .guide-content p { font-size: 0.875rem; color: #4b5563; margin-bottom: 0.75rem; line-height: 1.7; }
    :host ::ng-deep .guide-content ul { list-style: disc; padding-left: 1.25rem; margin-bottom: 0.75rem; }
    :host ::ng-deep .guide-content ol { list-style: decimal; padding-left: 1.25rem; margin-bottom: 0.75rem; }
    :host ::ng-deep .guide-content li { font-size: 0.875rem; color: #4b5563; margin: 0.25rem 0; line-height: 1.6; }
    :host ::ng-deep .guide-content code { background: #f3f4f6; color: #1d4ed8; padding: 0.125rem 0.375rem; border-radius: 0.25rem; font-size: 0.8rem; }
    :host ::ng-deep .guide-content pre { background: #111827; color: #e5e7eb; padding: 1rem; border-radius: 0.5rem; overflow-x: auto; font-size: 0.8rem; margin-bottom: 1rem; }
    :host ::ng-deep .guide-content pre code { background: transparent; color: inherit; padding: 0; }
    :host ::ng-deep .guide-content table { width: 100%; font-size: 0.8rem; border-collapse: collapse; margin-bottom: 1rem; }
    :host ::ng-deep .guide-content th { background: #f9fafb; text-align: left; padding: 0.5rem 0.75rem; font-weight: 600; color: #374151; border: 1px solid #e5e7eb; }
    :host ::ng-deep .guide-content td { padding: 0.5rem 0.75rem; border: 1px solid #e5e7eb; color: #4b5563; }
    :host ::ng-deep .guide-content blockquote { border-left: 4px solid #93c5fd; padding-left: 1rem; font-style: italic; color: #6b7280; margin-bottom: 1rem; }
    :host ::ng-deep .guide-content a { color: #2563eb; text-decoration: underline; }
    :host ::ng-deep .guide-content a:hover { color: #1d4ed8; }
    :host ::ng-deep .guide-content hr { margin: 1.5rem 0; border-color: #e5e7eb; }
    :host ::ng-deep .guide-content strong { color: #111827; }
    :host ::ng-deep mark { background: #fef08a; padding: 0 2px; border-radius: 2px; }
  `]
})
export class HelpComponent implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private sanitizer = inject(DomSanitizer);

  topics: GuideTopic[] = [
    { id: 'bat-dau-su-dung', title: 'Bắt đầu sử dụng', description: 'Dashboard, tạo danh mục, tổng quan ứng dụng', icon: '🚀', color: 'blue' },
    { id: 'giao-dich', title: 'Giao dịch', description: 'Wizard 5 bước, tạo giao dịch mua/bán, import CSV', icon: '🔄', color: 'green' },
    { id: 'ke-hoach-giao-dich', title: 'Kế hoạch giao dịch', description: 'Entry/SL/TP, checklist, chia lô, scenario', icon: '📑', color: 'purple' },
    { id: 'phan-tich-thi-truong', title: 'Phân tích thị trường', description: 'Tra cứu giá, 10 chỉ báo kỹ thuật, tín hiệu', icon: '📊', color: 'orange' },
    { id: 'quan-ly-rui-ro', title: 'Quản lý rủi ro', description: 'Risk Profile, stop-loss, position sizing, cảnh báo', icon: '🛡️', color: 'red' },
    { id: 'phan-tich-hieu-suat', title: 'Phân tích hiệu suất', description: 'Equity curve, win rate, báo cáo tháng', icon: '📈', color: 'teal' },
    { id: 'cong-cu-ho-tro', title: 'Công cụ hỗ trợ', description: 'Watchlist, nhật ký, daily routine, AI assistant', icon: '🧰', color: 'indigo' },
    { id: 'chien-luoc-giao-dich', title: 'Chiến lược giao dịch', description: '7 chiến lược, kết hợp chỉ báo, 10 nguyên tắc', icon: '🎯', color: 'amber' },
    { id: 'tai-chinh-ca-nhan', title: 'Tài chính cá nhân', description: '5 loại tài khoản, vàng tích trữ, sức khỏe tài chính 6/50/30', icon: '💰', color: 'yellow' },
  ];

  topicContents: TopicContent[] = [];
  selectedTopic: TopicContent | null = null;
  searchQuery = '';
  searchResults: SearchResult[] = [];
  loading = true;
  error: string | null = null;

  private searchSubject = new Subject<string>();
  private destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.searchSubject.pipe(debounceTime(300)).subscribe(query => this.performSearch(query));
    this.loadAllTopics();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadAllTopics(): void {
    const requests = this.topics.map(topic =>
      this.http.get(`/assets/docs/${topic.id}.md`, { responseType: 'text' }).pipe(
        catchError(() => of(''))
      )
    );

    forkJoin(requests).subscribe({
      next: (markdowns) => {
        this.topicContents = markdowns.map((md, i) => ({
          topic: this.topics[i],
          markdown: md,
          normalized: this.removeDiacritics(md.toLowerCase()),
          html: this.sanitizer.bypassSecurityTrustHtml(marked.parse(md) as string)
        }));
        this.loading = false;
      },
      error: () => {
        this.error = 'Không tải được hướng dẫn';
        this.loading = false;
      }
    });
  }

  onSearchChange(query: string): void {
    this.selectedTopic = null;
    this.searchSubject.next(query);
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.searchResults = [];
    this.selectedTopic = null;
  }

  selectTopic(topic: GuideTopic): void {
    const content = this.topicContents.find(tc => tc.topic.id === topic.id);
    if (content) {
      this.selectedTopic = content;
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  backToIndex(): void {
    this.selectedTopic = null;
  }

  private performSearch(query: string): void {
    if (query.length < 2) {
      this.searchResults = [];
      return;
    }

    const normalizedQuery = this.removeDiacritics(query.toLowerCase());
    const results: SearchResult[] = [];

    for (const tc of this.topicContents) {
      let count = 0;
      let searchIdx = 0;

      while (count < 3) {
        const pos = tc.normalized.indexOf(normalizedQuery, searchIdx);
        if (pos === -1) break;

        const start = Math.max(0, pos - 60);
        const end = Math.min(tc.markdown.length, pos + normalizedQuery.length + 60);
        let snippet = tc.markdown.substring(start, end).replace(/[#*|`>\[\]]/g, '').replace(/\n/g, ' ').trim();

        if (start > 0) snippet = '...' + snippet;
        if (end < tc.markdown.length) snippet = snippet + '...';

        const escaped = this.escapeHtml(snippet);
        const highlighted = this.highlightMatch(escaped, query);

        results.push({ topic: tc.topic, snippet: highlighted });
        searchIdx = pos + normalizedQuery.length;
        count++;
      }
    }

    this.searchResults = results.slice(0, 20);
  }

  private removeDiacritics(str: string): string {
    return str
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/g, 'd')
      .replace(/Đ/g, 'D');
  }

  private escapeHtml(str: string): string {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }

  private highlightMatch(html: string, query: string): string {
    const normalizedHtml = this.removeDiacritics(html.toLowerCase());
    const normalizedQuery = this.removeDiacritics(query.toLowerCase());
    const idx = normalizedHtml.indexOf(normalizedQuery);
    if (idx === -1) return html;

    const before = html.substring(0, idx);
    const match = html.substring(idx, idx + query.length);
    const after = html.substring(idx + query.length);
    return `${before}<mark>${match}</mark>${after}`;
  }
}
