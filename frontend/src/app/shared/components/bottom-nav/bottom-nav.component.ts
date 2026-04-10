import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

interface NavItem {
  path: string;
  label: string;
  icon: string; // SVG path d attribute
  exact?: boolean;
}

@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <nav class="fixed bottom-0 left-0 right-0 z-40 bg-white border-t border-gray-200 md:hidden safe-bottom">
      <div class="flex items-center justify-around h-14">
        <a *ngFor="let item of navItems"
          [routerLink]="item.path"
          routerLinkActive="text-blue-600"
          [routerLinkActiveOptions]="{exact: item.exact || false}"
          class="flex flex-col items-center justify-center flex-1 h-full text-gray-400 hover:text-blue-500 transition-colors"
          [class.text-gray-400]="!isActive(item.path)">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" [attr.d]="item.icon"/>
          </svg>
          <span class="text-[10px] mt-0.5 font-medium">{{ item.label }}</span>
        </a>

        <!-- More button -->
        <button (click)="showMore = !showMore"
          class="flex flex-col items-center justify-center flex-1 h-full transition-colors"
          [class.text-blue-600]="showMore"
          [class.text-gray-400]="!showMore">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4 6h16M4 12h16M4 18h16"/>
          </svg>
          <span class="text-[10px] mt-0.5 font-medium">Thêm</span>
        </button>
      </div>

      <!-- More menu bottom sheet -->
      <div *ngIf="showMore" class="border-t border-gray-100 bg-white max-h-[60vh] overflow-y-auto pb-2">
        <div class="grid grid-cols-4 gap-1 p-3">
          <a *ngFor="let item of moreItems"
            [routerLink]="item.path"
            (click)="showMore = false"
            routerLinkActive="text-blue-600 bg-blue-50"
            class="flex flex-col items-center gap-1 p-2.5 rounded-lg text-gray-600 hover:bg-gray-50 transition-colors">
            <span class="text-lg">{{ item.emoji }}</span>
            <span class="text-[10px] font-medium text-center leading-tight">{{ item.label }}</span>
          </a>
        </div>
      </div>
    </nav>

    <!-- Overlay -->
    <div *ngIf="showMore" (click)="showMore = false"
      class="md:hidden fixed inset-0 bg-black/20 z-30"></div>
  `,
  styles: [`
    :host { display: contents; }
    .safe-bottom { padding-bottom: env(safe-area-inset-bottom, 0px); }
  `]
})
export class BottomNavComponent {
  showMore = false;

  navItems: NavItem[] = [
    {
      path: '/dashboard',
      label: 'Tổng quan',
      icon: 'M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6',
      exact: true
    },
    {
      path: '/trades',
      label: 'Giao dịch',
      icon: 'M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4'
    },
    {
      path: '/trade-plan',
      label: 'Kế hoạch',
      icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4'
    },
    {
      path: '/risk',
      label: 'Rủi ro',
      icon: 'M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z'
    }
  ];

  moreItems = [
    { path: '/daily-routine', label: 'Nhiệm vụ', emoji: '📋' },
    { path: '/ai-settings', label: 'Cài đặt AI', emoji: '🤖' },
    { path: '/portfolios', label: 'Danh mục', emoji: '📁' },
    { path: '/positions', label: 'Vị thế', emoji: '📊' },
    { path: '/capital-flows', label: 'Dòng vốn', emoji: '💸' },
    { path: '/analytics', label: 'Phân tích', emoji: '📈' },
    { path: '/monthly-review', label: 'Báo cáo tháng', emoji: '📅' },
    { path: '/backtesting', label: 'Backtest', emoji: '🧪' },
    { path: '/watchlist', label: 'Watchlist', emoji: '⭐' },
    { path: '/market-data', label: 'Thị trường', emoji: '🌐' },
    { path: '/snapshots', label: 'Snapshot', emoji: '📸' },
    { path: '/trade-wizard', label: 'Wizard GD', emoji: '🧙' },
    { path: '/risk-dashboard', label: 'Risk Board', emoji: '📋' },
    { path: '/strategies', label: 'Chiến lược', emoji: '🎯' },
    { path: '/journals', label: 'Nhật ký', emoji: '📝' },
    { path: '/alerts', label: 'Cảnh báo', emoji: '🔔' },
    { path: '/help', label: 'Hướng dẫn', emoji: '❓' },
    { path: '/changelog', label: 'Changelog', emoji: '🔧' },
  ];

  isActive(path: string): boolean {
    return window.location.pathname.startsWith(path);
  }
}
