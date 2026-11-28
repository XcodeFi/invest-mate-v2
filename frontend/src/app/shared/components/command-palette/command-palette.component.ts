import { Component, ElementRef, HostListener, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { UppercaseDirective } from '../../directives/uppercase.directive';
import { CommandPaletteService } from './command-palette.service';
import { PaletteItem, buildItems } from './command-palette.logic';

/**
 * Bảng lệnh Ctrl+K (Cmd+K trên macOS): gõ để tìm trang (cùng cấu hình với menu) hoặc gõ mã để mở dòng thời gian của mã.
 * Ô nhập dùng appUppercase như mọi ô nhập mã; việc tìm trang không phân biệt hoa thường và dấu.
 */
@Component({
  selector: 'app-command-palette',
  standalone: true,
  imports: [FormsModule, UppercaseDirective],
  template: `
    @if (svc.isOpen()) {
      <div data-testid="palette" class="fixed inset-0 z-[70]">
        <div data-testid="palette-backdrop" class="absolute inset-0 bg-black/40" (click)="svc.close()"></div>
        <div data-testid="palette-box" role="dialog" aria-modal="true" aria-label="Tìm trang hoặc mã cổ phiếu"
             class="relative mx-auto mt-16 sm:mt-24 w-[calc(100%-2rem)] max-w-lg bg-white rounded-xl shadow-xl border border-gray-200 overflow-hidden">
          <input #box data-testid="palette-input" type="text" appUppercase [ngModel]="query()" (ngModelChange)="onQuery($event)"
                 autocomplete="off" spellcheck="false" aria-label="Tìm trang hoặc nhập mã cổ phiếu"
                 placeholder="Tìm trang hoặc nhập mã cổ phiếu (VD: FPT)…"
                 class="w-full px-4 py-3 text-base border-b border-gray-200 bg-transparent text-gray-900 placeholder-gray-500 focus:outline-none" />
          <ul role="listbox" class="max-h-80 overflow-y-auto py-1">
            @for (item of items(); track item.path; let i = $index) {
              <li data-testid="palette-item" role="option" [attr.aria-selected]="i === index()"
                  [attr.data-kind]="item.kind" [attr.data-path]="item.path"
                  (click)="go(item)" (mousemove)="index.set(i)"
                  class="flex items-center gap-3 px-4 py-2 text-sm cursor-pointer"
                  [class.bg-blue-50]="i === index()" [class.text-blue-700]="i === index()" [class.text-gray-700]="i !== index()">
                <span aria-hidden="true">{{ item.icon }}</span>
                <span class="flex-1 truncate">{{ item.label }}</span>
                <span class="text-xs text-gray-600">{{ item.hint }}</span>
              </li>
            }
          </ul>
          @if (items().length === 0) {
            <p data-testid="palette-empty" class="px-4 py-6 text-sm text-center text-gray-500">Không tìm thấy trang nào khớp.</p>
          }
          <p class="hidden sm:block px-4 py-2 text-xs text-gray-500 border-t border-gray-200">↑↓ chọn · Enter mở · Esc đóng</p>
        </div>
      </div>
    }
  `,
})
export class CommandPaletteComponent {
  readonly svc = inject(CommandPaletteService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly query = signal('');
  readonly index = signal(0);
  readonly items = computed<PaletteItem[]>(() => buildItems(this.query()));

  @ViewChild('box') set box(ref: ElementRef<HTMLInputElement> | undefined) {
    // Ô nhập vừa xuất hiện (bảng vừa mở): mỗi lần mở là một lần tìm mới, và đưa con trỏ vào ô.
    if (ref) {
      this.query.set('');
      this.index.set(0);
      setTimeout(() => ref.nativeElement.focus());
    }
  }

  onQuery(value: string): void {
    this.query.set(value ?? '');
    this.index.set(0);
  }

  @HostListener('document:keydown', ['$event'])
  onKey(e: KeyboardEvent): void {
    if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && e.key.toLowerCase() === 'k') {
      if (!this.auth.isAuthenticated()) return;
      e.preventDefault();
      this.svc.toggle();
      return;
    }
    if (!this.svc.isOpen()) return;
    // Bẫy tiêu điểm: hộp thoại chỉ có một ô nhập, Tab/Shift+Tab ở lại đó thay vì rời ra trang phía sau.
    if (e.key === 'Tab') { e.preventDefault(); return; }
    // Đang gõ dở một âm tiết tiếng Việt (Telex/VNI): Enter/mũi tên thuộc về bộ gõ, không phải để chọn mục.
    if (e.isComposing) return;
    const n = this.items().length;
    switch (e.key) {
      case 'Escape': e.preventDefault(); this.svc.close(); break;
      case 'ArrowDown': e.preventDefault(); if (n) this.index.update(i => Math.min(i + 1, n - 1)); break;
      case 'ArrowUp': e.preventDefault(); this.index.update(i => Math.max(i - 1, 0)); break;
      case 'Enter': {
        e.preventDefault();
        const item = this.items()[this.index()];
        if (item) this.go(item);
        break;
      }
    }
  }

  go(item: PaletteItem): void {
    this.svc.close();
    void this.router.navigateByUrl(item.path);
  }
}
