import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';

@Component({
  selector: 'app-changelog',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-gray-950 text-gray-100">
      <!-- Header bar -->
      <div class="border-b border-gray-800 bg-gray-900 px-6 py-4 flex items-center gap-3">
        <span class="bg-gray-700 text-gray-200 px-2 py-0.5 rounded text-xs font-mono font-bold">DEV</span>
        <h1 class="text-sm font-mono text-gray-300 font-semibold">CHANGELOG — Investment Mate v2</h1>
        <span class="ml-auto text-xs text-gray-500 font-mono">frontend/src/assets/CHANGELOG.md</span>
      </div>

      <!-- Loading -->
      <div *ngIf="loading" class="flex items-center justify-center py-24 text-gray-500 text-sm font-mono">
        Đang tải changelog...
      </div>

      <!-- Error -->
      <div *ngIf="error" class="flex items-center justify-center py-24 text-red-400 text-sm font-mono">
        Không tải được CHANGELOG.md: {{ error }}
      </div>

      <!-- Markdown content -->
      <div *ngIf="!loading && !error"
        class="max-w-4xl mx-auto px-6 py-10 prose prose-invert prose-sm
               prose-headings:font-mono prose-h1:text-xl prose-h2:text-base
               prose-h2:border-b prose-h2:border-gray-700 prose-h2:pb-2
               prose-code:text-green-400 prose-code:bg-gray-800 prose-code:px-1 prose-code:rounded
               prose-a:text-blue-400 prose-hr:border-gray-700
               prose-li:marker:text-gray-500"
        [innerHTML]="html">
      </div>
    </div>
  `,
  styles: [`
    :host ::ng-deep .prose h2 { margin-top: 2.5rem; }
    :host ::ng-deep .prose h3 { color: #9ca3af; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.05em; }
    :host ::ng-deep .prose ul { padding-left: 1.25rem; }
    :host ::ng-deep .prose li { margin: 0.2rem 0; }
    :host ::ng-deep .prose strong { color: #e5e7eb; }
    :host ::ng-deep .prose del { color: #6b7280; }
  `]
})
export class ChangelogComponent implements OnInit {
  private http = inject(HttpClient);
  private sanitizer = inject(DomSanitizer);

  loading = true;
  error: string | null = null;
  html: SafeHtml = '';

  ngOnInit(): void {
    this.http.get('/assets/CHANGELOG.md', { responseType: 'text' }).subscribe({
      next: (md) => {
        const raw = marked.parse(md) as string;
        this.html = this.sanitizer.bypassSecurityTrustHtml(raw);
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message ?? 'unknown error';
        this.loading = false;
      }
    });
  }
}
