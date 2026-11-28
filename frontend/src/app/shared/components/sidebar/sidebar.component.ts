import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, NavigationEnd } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { NAV_GROUPS, NavGroup } from '../../nav/nav-config';
import { SidebarService } from './sidebar.service';

/**
 * Menu trái cho màn hình máy tính (từ 1024px; dưới đó ẩn hẳn, đã có menu điện thoại và thanh dưới). Đọc từ NAV_GROUPS, không có bản sao.
 * Thu gọn thành thanh biểu tượng 56px (kèm tooltip); mặc định thu gọn trên trang nhiều biểu đồ, lựa chọn đã lưu của người dùng luôn thắng.
 * Nhóm "Cài đặt" thu gọn sẵn, tự mở khi trang hiện tại nằm trong nhóm.
 */
@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NgTemplateOutlet],
  template: `
    @if (auth.isAuthenticated()) {
      <aside data-testid="sidebar" aria-label="Menu chính"
        class="hidden lg:flex flex-col shrink-0 sticky top-14 h-[calc(100vh-3.5rem)] overflow-y-auto overflow-x-hidden bg-white border-r border-gray-200"
        [class.w-14]="collapsed()" [class.w-56]="!collapsed()" [attr.data-collapsed]="collapsed()">
        <nav class="flex-1 py-2">
          @for (group of groups; track group.key; let first = $first) {
            @if (!first) {
              @if (collapsed()) { <hr class="my-1.5 mx-2 border-gray-200"> } @else { <div class="h-1"></div> }
            }
            @if (group.collapsedByDefault) {
              <button type="button" data-testid="sidebar-settings-toggle" (click)="toggleSettings()"
                [attr.aria-expanded]="settingsVisible()" [attr.aria-label]="group.label" [attr.title]="collapsed() ? group.label : null"
                class="w-full flex items-center gap-2 px-3 py-2 text-xs font-semibold text-gray-500 hover:bg-gray-100"
                [class.justify-center]="collapsed()">
                <span aria-hidden="true">{{ group.icon }}</span>
                @if (!collapsed()) {
                  <span class="flex-1 text-left uppercase tracking-wider">{{ group.label }}</span>
                  <span aria-hidden="true">{{ settingsVisible() ? '▾' : '▸' }}</span>
                }
              </button>
              @if (settingsVisible()) { <ng-container *ngTemplateOutlet="items; context: { group: group }"></ng-container> }
            } @else {
              @if (!collapsed()) {
                <div data-testid="sidebar-group-label" class="px-3 pt-2 pb-1 text-[11px] font-semibold tracking-wider text-gray-400">{{ group.label }}</div>
              }
              <ng-container *ngTemplateOutlet="items; context: { group: group }"></ng-container>
            }
          }
        </nav>
        <button type="button" data-testid="sidebar-toggle" (click)="toggle()"
          [attr.aria-label]="collapsed() ? 'Mở rộng menu' : 'Thu gọn menu'" [attr.title]="collapsed() ? 'Mở rộng menu' : null"
          class="sticky bottom-0 bg-white border-t border-gray-200 px-3 py-2 text-sm text-gray-500 hover:bg-gray-100 text-left"
          [class.text-center]="collapsed()">
          {{ collapsed() ? '»' : '« Thu gọn' }}
        </button>
      </aside>
    }

    <ng-template #items let-group="group">
      @for (item of group.items; track item.path) {
        <a data-testid="sidebar-link" [routerLink]="item.path" routerLinkActive="bg-blue-50 text-blue-700 font-medium" ariaCurrentWhenActive="page"
          [attr.aria-label]="item.label" [attr.title]="collapsed() ? item.label : null"
          class="flex items-center gap-2 mx-1.5 my-0.5 px-2.5 py-1.5 rounded-md text-sm text-gray-700 hover:bg-gray-100"
          [class.justify-center]="collapsed()">
          <span aria-hidden="true" class="w-5 text-center">{{ item.icon }}</span>
          @if (!collapsed()) { <span class="truncate">{{ item.label }}</span> }
        </a>
      }
    </ng-template>
  `
})
export class SidebarComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly state = inject(SidebarService);

  readonly groups: NavGroup[] = NAV_GROUPS;

  /** URL hiện tại (cập nhật sau mỗi lần điều hướng xong). */
  private readonly url = toSignal(
    this.router.events.pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd), map(e => e.urlAfterRedirects)),
    { initialValue: this.router.url }
  );

  readonly collapsed = computed(() => this.state.collapsed(this.url()));

  /** Người dùng bấm mở/đóng nhóm Cài đặt; null = theo trang hiện tại (tự mở khi trang nằm trong nhóm). Đổi trang thì về null. */
  private readonly settingsOverride = signal<{ url: string; open: boolean } | null>(null);
  private readonly insideSettings = computed(() => {
    const path = this.url().split(/[?#]/)[0];
    const settings = NAV_GROUPS.find(g => g.collapsedByDefault);
    return !!settings?.items.some(i => path === i.path || path.startsWith(i.path + '/'));
  });
  readonly settingsVisible = computed(() => {
    const o = this.settingsOverride();
    return o && o.url === this.url() ? o.open : this.insideSettings();
  });

  toggle(): void {
    this.state.toggle(this.url());
  }

  toggleSettings(): void {
    this.settingsOverride.set({ url: this.url(), open: !this.settingsVisible() });
  }
}
