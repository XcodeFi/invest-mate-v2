import { Component, inject } from '@angular/core';
import { ThemeService } from '../../../core/services/theme.service';

/** Nút đổi chế độ sáng/tối trên header. Hiện biểu tượng của chế độ SẼ chuyển sang (đang tối → ☀️, đang sáng → 🌙) kèm tooltip tiếng Việt. */
@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  template: `
    <button type="button" (click)="theme.toggle()"
      [title]="theme.toggleLabel()" [attr.aria-label]="theme.toggleLabel()"
      data-testid="theme-toggle"
      class="p-2 rounded-lg text-gray-600 hover:bg-gray-100 transition-colors text-base leading-none">
      <span aria-hidden="true" data-testid="theme-toggle-icon">{{ theme.theme() === 'dark' ? '☀️' : '🌙' }}</span>
    </button>
  `
})
export class ThemeToggleComponent {
  readonly theme = inject(ThemeService);
}
