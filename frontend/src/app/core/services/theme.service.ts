import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';

export type Theme = 'dark' | 'light';

/** Khoá localStorage. Script chạy trước Angular trong index.html đọc cùng khoá này (có test khoá hai bên khớp nhau). */
export const THEME_STORAGE_KEY = 'imv2.theme';

/** Mặc định là TỐI: thiếu khoá, giá trị lạ hay không đọc được storage đều ra tối. Không lấy theo hệ điều hành. */
export function resolveTheme(raw: string | null | undefined): Theme {
  return raw === 'light' ? 'light' : 'dark';
}

/**
 * Chế độ giao diện. Class `dark` trên &lt;html&gt; do script trong index.html đặt trước khi Angular chạy (không nháy sáng); service này
 * đọc lại cùng nguồn, đổi class khi bấm nút, lưu vào localStorage (chỉ trên trình duyệt này: đủ cho một người dùng) và phát tín hiệu
 * cho biểu đồ. Truy cập storage luôn bọc try/catch: bị chặn thì vẫn đổi được trong phiên, chỉ không nhớ.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly doc = inject(DOCUMENT);

  readonly theme = signal<Theme>('dark');

  /** Nhãn tooltip của nút: nút đưa sang chế độ còn lại. */
  readonly toggleLabel = computed(() => (this.theme() === 'dark' ? 'Chế độ sáng' : 'Chế độ tối'));

  constructor() {
    this.set(resolveTheme(this.read()), false);
  }

  toggle(): void {
    this.set(this.theme() === 'dark' ? 'light' : 'dark');
  }

  set(theme: Theme, persist = true): void {
    this.theme.set(theme);
    this.doc.documentElement.classList.toggle('dark', theme === 'dark');
    if (persist) this.write(theme);
  }

  private read(): string | null {
    try {
      return this.doc.defaultView?.localStorage.getItem(THEME_STORAGE_KEY) ?? null;
    } catch {
      return null;
    }
  }

  private write(theme: Theme): void {
    try {
      this.doc.defaultView?.localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      // storage bị chặn: đổi trong phiên là đủ
    }
  }
}
