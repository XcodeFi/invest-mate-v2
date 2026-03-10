import { Component, inject, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, User } from '../../../core/services/auth.service';

interface NavGroup {
  label: string;
  icon: string;
  items: { path: string; label: string; icon: string }[];
}

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <header class="bg-white shadow-sm border-b border-gray-200 sticky top-0 z-50">
      <nav class="container mx-auto px-4">
        <div class="flex justify-between items-center h-14">
          <!-- Logo -->
          <a routerLink="/dashboard" class="flex items-center gap-2 shrink-0">
            <span class="text-lg font-bold text-blue-600">💰 Investment Mate</span>
          </a>

          <!-- Desktop Nav -->
          <div class="hidden lg:flex items-center gap-1 flex-1 justify-center">
            <!-- Dashboard link -->
            <a routerLink="/dashboard" routerLinkActive="nav-active" [routerLinkActiveOptions]="{exact:true}"
              class="nav-item">
              <span class="text-sm">📊</span> Tổng quan
            </a>

            <!-- Grouped Dropdowns -->
            <div *ngFor="let group of navGroups" class="relative"
              (mouseenter)="openDropdown = group.label"
              (mouseleave)="openDropdown = null">
              <button class="nav-item flex items-center gap-1"
                [class.nav-active]="isGroupActive(group)">
                <span class="text-sm">{{ group.icon }}</span>
                {{ group.label }}
                <svg class="w-3 h-3 opacity-50" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
                </svg>
              </button>
              <div *ngIf="openDropdown === group.label"
                class="absolute top-full left-0 mt-0 w-48 bg-white rounded-lg shadow-lg border border-gray-200 py-1 z-50">
                <a *ngFor="let item of group.items"
                  [routerLink]="item.path" routerLinkActive="bg-blue-50 text-blue-700"
                  (click)="openDropdown = null"
                  class="flex items-center gap-2 px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 transition-colors">
                  <span>{{ item.icon }}</span> {{ item.label }}
                </a>
              </div>
            </div>
          </div>

          <!-- Right side: User + Mobile hamburger -->
          <div class="flex items-center gap-3">
            <!-- User Menu (desktop) -->
            <div *ngIf="authService.isAuthenticated()" class="hidden sm:flex items-center gap-2">
              <div class="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center text-blue-600 font-semibold text-sm">
                {{ getInitials() }}
              </div>
              <span class="text-sm text-gray-700 hidden md:inline">{{ currentUser?.name }}</span>
              <button (click)="logout()"
                class="text-sm text-gray-500 hover:text-red-600 transition-colors ml-1" title="Đăng xuất">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/>
                </svg>
              </button>
            </div>
            <div *ngIf="!authService.isAuthenticated()" class="hidden sm:block">
              <a routerLink="/auth/login"
                class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors">
                Đăng nhập
              </a>
            </div>

            <!-- Mobile hamburger -->
            <button (click)="mobileMenuOpen = !mobileMenuOpen"
              class="lg:hidden p-2 rounded-lg text-gray-600 hover:bg-gray-100 transition-colors">
              <svg *ngIf="!mobileMenuOpen" class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16"/>
              </svg>
              <svg *ngIf="mobileMenuOpen" class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
              </svg>
            </button>
          </div>
        </div>
      </nav>

      <!-- Mobile Menu -->
      <div *ngIf="mobileMenuOpen"
        class="lg:hidden border-t border-gray-200 bg-white max-h-[calc(100vh-3.5rem)] overflow-y-auto">
        <div class="px-4 py-3 space-y-1">
          <!-- Dashboard -->
          <a routerLink="/dashboard" routerLinkActive="bg-blue-50 text-blue-700" [routerLinkActiveOptions]="{exact:true}"
            (click)="mobileMenuOpen = false"
            class="mobile-link">
            <span>📊</span> Tổng quan
          </a>

          <!-- Groups -->
          <div *ngFor="let group of navGroups">
            <button (click)="toggleMobileGroup(group.label)"
              class="w-full flex items-center justify-between px-3 py-2.5 text-sm font-medium text-gray-500 uppercase tracking-wider">
              <span class="flex items-center gap-2">
                <span>{{ group.icon }}</span> {{ group.label }}
              </span>
              <svg class="w-4 h-4 transition-transform" [class.rotate-180]="mobileOpenGroup === group.label"
                fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
              </svg>
            </button>
            <div *ngIf="mobileOpenGroup === group.label" class="pl-4 space-y-0.5">
              <a *ngFor="let item of group.items"
                [routerLink]="item.path" routerLinkActive="bg-blue-50 text-blue-700"
                (click)="mobileMenuOpen = false"
                class="mobile-link">
                <span>{{ item.icon }}</span> {{ item.label }}
              </a>
            </div>
          </div>
        </div>

        <!-- Mobile user section -->
        <div class="border-t border-gray-200 px-4 py-3">
          <div *ngIf="authService.isAuthenticated()" class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <div class="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center text-blue-600 font-semibold text-sm">
                {{ getInitials() }}
              </div>
              <span class="text-sm text-gray-700">{{ currentUser?.name }}</span>
            </div>
            <button (click)="logout(); mobileMenuOpen = false"
              class="text-sm text-red-600 hover:text-red-700 font-medium">
              Đăng xuất
            </button>
          </div>
          <div *ngIf="!authService.isAuthenticated()">
            <a routerLink="/auth/login" (click)="mobileMenuOpen = false"
              class="block w-full text-center bg-blue-600 hover:bg-blue-700 text-white px-4 py-2.5 rounded-lg text-sm font-medium transition-colors">
              Đăng nhập
            </a>
          </div>
        </div>
      </div>
    </header>

    <!-- Mobile overlay -->
    <div *ngIf="mobileMenuOpen" (click)="mobileMenuOpen = false"
      class="lg:hidden fixed inset-0 bg-black/20 z-40" style="top: 3.5rem;"></div>
  `,
  styles: [`
    .nav-item {
      @apply px-3 py-2 rounded-md text-sm font-medium text-gray-600
             hover:text-gray-900 hover:bg-gray-100 transition-colors duration-150 cursor-pointer
             whitespace-nowrap;
    }
    .nav-active {
      @apply bg-blue-50 text-blue-700 !important;
    }
    .mobile-link {
      @apply flex items-center gap-2 px-3 py-2.5 rounded-lg text-sm font-medium
             text-gray-700 hover:bg-gray-100 transition-colors;
    }
  `]
})
export class HeaderComponent implements OnInit {
  authService = inject(AuthService);
  currentUser: User | null = null;
  mobileMenuOpen = false;
  openDropdown: string | null = null;
  mobileOpenGroup: string | null = null;

  navGroups: NavGroup[] = [
    {
      label: 'Đầu tư',
      icon: '💼',
      items: [
        { path: '/portfolios', label: 'Danh mục', icon: '📁' },
        { path: '/trades', label: 'Giao dịch', icon: '🔄' },
        { path: '/capital-flows', label: 'Dòng vốn', icon: '💸' },
      ]
    },
    {
      label: 'Phân tích',
      icon: '📈',
      items: [
        { path: '/analytics', label: 'Phân tích', icon: '📊' },
        { path: '/advanced-analytics', label: 'Phân tích nâng cao', icon: '🔬' },
        { path: '/backtesting', label: 'Backtest', icon: '🧪' },
        { path: '/market-data', label: 'Thị trường', icon: '🌐' },
        { path: '/snapshots', label: 'Lịch sử snapshot', icon: '📸' },
      ]
    },
    {
      label: 'Quản lý',
      icon: '⚙️',
      items: [
        { path: '/risk-dashboard', label: 'Risk Dashboard', icon: '📋' },
        { path: '/risk', label: 'Rủi ro chi tiết', icon: '🛡️' },
        { path: '/strategies', label: 'Chiến lược', icon: '🎯' },
        { path: '/trade-plan', label: 'Kế hoạch GD', icon: '📑' },
        { path: '/position-sizing', label: 'Tính vị thế', icon: '🧮' },
        { path: '/journals', label: 'Nhật ký', icon: '📝' },
        { path: '/alerts', label: 'Cảnh báo', icon: '🔔' },
      ]
    }
  ];

  ngOnInit() {
    this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
    });
  }

  @HostListener('window:resize')
  onResize() {
    if (window.innerWidth >= 1024) {
      this.mobileMenuOpen = false;
    }
  }

  getInitials(): string {
    if (!this.currentUser?.name) return '?';
    return this.currentUser.name.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase();
  }

  isGroupActive(group: NavGroup): boolean {
    const currentPath = window.location.pathname;
    return group.items.some(item => currentPath.startsWith(item.path));
  }

  toggleMobileGroup(label: string): void {
    this.mobileOpenGroup = this.mobileOpenGroup === label ? null : label;
  }

  logout() {
    this.authService.logout().subscribe();
  }
}