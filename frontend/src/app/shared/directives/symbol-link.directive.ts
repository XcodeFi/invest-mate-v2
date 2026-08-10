import { Directive, HostBinding, HostListener, Input, inject } from '@angular/core';
import { Router } from '@angular/router';

/**
 * SymbolLinkDirective — gắn lên phần tử đang hiển thị một mã chứng khoán để bấm
 * được sang dòng thời gian của mã đó.
 *
 * Là attribute directive chứ không phải component: mã đang nằm trong hàng chục
 * template dưới dạng {{ x.symbol }} bên trong <span>/<td>/<div>, nên một
 * component <app-symbol-link> sẽ buộc phải đổi cấu trúc từng chỗ.
 *
 * Dùng:
 *   <span [appSymbolLink]="p.symbol">{{ p.symbol }}</span>
 */
@Directive({ selector: '[appSymbolLink]', standalone: true })
export class SymbolLinkDirective {
  private router = inject(Router);

  @Input('appSymbolLink') symbol: string | null | undefined = '';

  private get normalized(): string {
    return (this.symbol ?? '').trim().toUpperCase();
  }

  // Mã rỗng thì không giả vờ là link: con trỏ, role, tabindex, title đều tắt.
  @HostBinding('class.cursor-pointer') get clickable(): boolean { return !!this.normalized; }
  @HostBinding('class.hover:underline') get underline(): boolean { return !!this.normalized; }
  @HostBinding('attr.role') get role(): string | null { return this.normalized ? 'link' : null; }
  @HostBinding('attr.tabindex') get tabindex(): string | null { return this.normalized ? '0' : null; }
  @HostBinding('attr.title') get title(): string | null {
    return this.normalized ? `Xem dòng thời gian ${this.normalized}` : null;
  }

  @HostListener('click', ['$event'])
  onClick(event: Event): void {
    // Mã rỗng: để click nổi lên cha như thường, đừng cướp sự kiện của hàng bảng.
    if (!this.normalized) return;
    event.stopPropagation();
    this.router.navigate(['/symbol-timeline', this.normalized]);
  }

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    if (!this.normalized) return;
    event.preventDefault();
    event.stopPropagation();
    this.router.navigate(['/symbol-timeline', this.normalized]);
  }
}
