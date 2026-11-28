import { Component, inject, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, User } from '../../../core/services/auth.service';
import { PnlService } from '../../../core/services/pnl.service';
import { RiskService } from '../../../core/services/risk.service';
import { forkJoin } from 'rxjs';
import { AiChatPanelComponent } from '../ai-chat-panel/ai-chat-panel.component';
import { ThemeToggleComponent } from '../theme-toggle/theme-toggle.component';
import { CommandPaletteService } from '../command-palette/command-palette.service';
import { NAV_GROUPS, NavGroup } from '../../nav/nav-config';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule, AiChatPanelComponent, ThemeToggleComponent],
  template: `
    <header class="bg-white shadow-sm border-b border-gray-200 sticky top-0 z-50">
      <nav class="container mx-auto px-4">
        <div class="flex justify-between items-center h-14">
          <!-- Logo -->
          <a routerLink="/dashboard" class="flex items-center gap-2 shrink-0">
            <span class="text-lg font-bold text-blue-600">💰 Investment Mate</span>
          </a>

          <!-- Menu chính nằm ở thanh trái (app-sidebar, từ 1024px); thanh trên chỉ còn logo, tiện ích và người dùng -->
          <div class="flex-1"></div>

          <!-- Right side: Risk Score + User + Mobile hamburger -->
          <div class="flex items-center gap-3">
            <!-- Chế độ sáng/tối (hiện ở mọi cỡ màn hình, kể cả điện thoại) nằm trong vị trí dành sẵn của thanh trên gọn -->
            <div data-testid="topbar-theme-slot" class="flex items-center"><app-theme-toggle></app-theme-toggle></div>

            <!-- Tìm trang / mã (Ctrl+K); trên điện thoại không có phím tắt nên cần nút -->
            <button *ngIf="authService.isAuthenticated()" data-testid="topbar-search" type="button" (click)="palette.open()"
              class="flex items-center gap-1 px-2.5 py-1 rounded-lg text-sm text-gray-600 hover:bg-gray-100 transition-colors"
              title="Tìm trang hoặc mã cổ phiếu (Ctrl+K)" aria-label="Tìm trang hoặc mã cổ phiếu">
              🔍 <span class="hidden md:inline text-xs text-gray-500">Ctrl+K</span>
            </button>

            <!-- AI Chat Button -->
            <button (click)="showAiPanel = true"
              class="hidden md:flex items-center gap-1 px-2.5 py-1 rounded-lg text-sm font-medium bg-purple-50 text-purple-700 hover:bg-purple-100 transition-colors"
              title="Trợ lý AI">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z"/>
              </svg>
              AI
            </button>

            <!-- Help Button -->
            <a routerLink="/help"
              class="hidden md:flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-medium bg-blue-50 text-blue-700 hover:bg-blue-100 transition-colors"
              title="Hướng dẫn sử dụng">
              ❓ Hướng dẫn
            </a>

            <!-- DEV Changelog Badge (admin only) -->
            <a *ngIf="isAdmin()" routerLink="/changelog"
              class="hidden md:flex items-center gap-1 px-2 py-0.5 rounded text-xs font-mono font-bold bg-gray-800 text-gray-200 hover:bg-gray-700 transition-colors"
              title="Developer Changelog">
              DEV
            </a>

            <!-- Admin link (only visible to admins, hidden during impersonation) -->
            <a *ngIf="isAdmin()" routerLink="/admin"
              class="hidden md:flex items-center gap-1 px-2 py-0.5 rounded text-xs font-mono font-bold bg-red-600 text-white hover:bg-red-700 transition-colors"
              title="Admin: công cụ quản trị">
              ADMIN
            </a>

            <!-- Risk Score Badge -->
            <a *ngIf="riskScore >= 0" routerLink="/risk-dashboard"
              class="hidden sm:flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold cursor-pointer transition-colors"
              [class.bg-green-100]="riskScore >= 70" [class.text-green-700]="riskScore >= 70"
              [class.bg-amber-100]="riskScore >= 40 && riskScore < 70" [class.text-amber-700]="riskScore >= 40 && riskScore < 70"
              [class.bg-red-100]="riskScore < 40" [class.text-red-700]="riskScore < 40"
              [class.animate-pulse]="riskScore < 40"
              title="Sức khỏe rủi ro danh mục">
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z"></path>
              </svg>
              {{ riskScore }}/100
            </a>

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
          <!-- Groups -->
          <div *ngFor="let group of navGroups">
            <button (click)="toggleMobileGroup(group.key)" data-testid="mobile-group"
              class="w-full flex items-center justify-between px-3 py-2.5 text-sm font-medium text-gray-500 uppercase tracking-wider">
              <span class="flex items-center gap-2">
                <span>{{ group.icon }}</span> {{ group.label }}
              </span>
              <svg class="w-4 h-4 transition-transform" [class.rotate-180]="mobileOpenGroup === group.key"
                fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/>
              </svg>
            </button>
            <div *ngIf="mobileOpenGroup === group.key" class="pl-4 space-y-0.5">
              <a *ngFor="let item of group.items" data-testid="mobile-link"
                [routerLink]="item.path" routerLinkActive="bg-blue-50 text-blue-700"
                (click)="mobileMenuOpen = false"
                class="mobile-link">
                <span>{{ item.icon }}</span> {{ item.label }}
              </a>
            </div>
          </div>
        </div>

        <!-- Mobile DEV link (admin only) -->
        <div *ngIf="isAdmin()" class="border-t border-gray-200 px-4 py-2">
          <a routerLink="/changelog" (click)="mobileMenuOpen = false"
            class="flex items-center gap-2 px-3 py-2 rounded-lg text-xs font-mono font-bold text-gray-500 hover:bg-gray-100 transition-colors">
            <span class="bg-gray-800 text-gray-200 px-1.5 py-0.5 rounded text-xs">DEV</span>
            Changelog
          </a>
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

    <app-ai-chat-panel [(isOpen)]="showAiPanel" title="Trợ lý AI" useCase="chat"
      [contextData]="emptyContext">
    </app-ai-chat-panel>
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
  palette = inject(CommandPaletteService);
  private pnlService = inject(PnlService);
  private riskService = inject(RiskService);
  currentUser: User | null = null;
  mobileMenuOpen = false;
  mobileOpenGroup: string | null = null;
  riskScore = -1; // -1 = not loaded yet
  showAiPanel = false;
  readonly emptyContext = {};

  /** Cùng cấu hình với menu trái: không có bản sao các mục menu trong header. */
  readonly navGroups: NavGroup[] = NAV_GROUPS;

  ngOnInit() {
    this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
      if (user) this.loadRiskScore();
    });
  }

  isAdmin(): boolean {
    const token = this.authService.getToken();
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.role === 'Admin' && payload.amr !== 'impersonate';
    } catch {
      return false;
    }
  }

  private loadRiskScore(): void {
    this.pnlService.getSummary().subscribe({
      next: (summary) => {
        if (!summary.portfolios?.length) return;
        const riskRequests = summary.portfolios.map(p =>
          this.riskService.getPortfolioRiskSummary(p.portfolioId)
        );
        forkJoin(riskRequests).subscribe({
          next: (risks) => {
            let score = 100;
            risks.forEach((risk, i) => {
              // Deduct for high drawdown
              if (risk.maxDrawdown > 20) score -= 30;
              else if (risk.maxDrawdown > 10) score -= 15;
              // Deduct for concentration
              if (risk.largestPositionPercent > 50) score -= 25;
              else if (risk.largestPositionPercent > 30) score -= 10;
              // Deduct for positions near stop-loss
              const nearSL = risk.positions.filter(p => p.stopLossPrice != null && p.distanceToStopLossPercent <= 5);
              score -= nearSL.length * 5;
            });
            this.riskScore = Math.max(0, Math.min(100, score));
          },
          error: () => {} // silently fail
        });
      },
      error: () => {}
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

  toggleMobileGroup(key: string): void {
    this.mobileOpenGroup = this.mobileOpenGroup === key ? null : key;
  }

  logout() {
    this.authService.logout().subscribe();
  }
}