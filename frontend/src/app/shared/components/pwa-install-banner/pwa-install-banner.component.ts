import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PwaService } from '../../../core/services/pwa.service';

@Component({
  selector: 'app-pwa-install-banner',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Banner cài đặt ứng dụng -->
    @if (showInstallBanner()) {
      <div class="fixed bottom-16 md:bottom-4 left-4 right-4 md:left-auto md:right-4 md:w-96 z-50
                  bg-white border border-blue-200 rounded-xl shadow-lg p-4
                  animate-slide-up">
        <div class="flex items-start gap-3">
          <img src="assets/icons/icon-72x72.svg" alt="InvestMate icon"
               class="w-12 h-12 rounded-xl flex-shrink-0">
          <div class="flex-1 min-w-0">
            <p class="text-sm font-semibold text-gray-900">Cài đặt Investment Mate</p>
            <p class="text-xs text-gray-500 mt-0.5">
              Truy cập nhanh hơn, dùng được offline khi không có mạng.
            </p>
          </div>
          <button (click)="dismissInstall()"
                  class="text-gray-400 hover:text-gray-600 flex-shrink-0 -mt-1 -mr-1 p-1">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M6 18L18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
        <div class="flex gap-2 mt-3">
          <button (click)="install()"
                  class="flex-1 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium
                         py-2 px-4 rounded-lg transition-colors">
            Cài đặt
          </button>
          <button (click)="dismissInstall()"
                  class="flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-medium
                         py-2 px-4 rounded-lg transition-colors">
            Để sau
          </button>
        </div>
      </div>
    }

    <!-- Banner cập nhật -->
    @if (showUpdateBanner()) {
      <div class="fixed bottom-16 md:bottom-4 left-4 right-4 md:left-auto md:right-4 md:w-96 z-50
                  bg-blue-600 rounded-xl shadow-lg p-4
                  animate-slide-up">
        <div class="flex items-center gap-3">
          <div class="w-8 h-8 bg-white/20 rounded-full flex items-center justify-center flex-shrink-0">
            <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0
                       0a8.003 8.003 0 01-15.357-2m15.357 2H15"/>
            </svg>
          </div>
          <div class="flex-1">
            <p class="text-sm font-semibold text-white">Có phiên bản mới</p>
            <p class="text-xs text-blue-100 mt-0.5">Tải lại để áp dụng cập nhật.</p>
          </div>
          <button (click)="applyUpdate()"
                  class="bg-white text-blue-600 hover:bg-blue-50 text-xs font-semibold
                         py-1.5 px-3 rounded-lg transition-colors flex-shrink-0">
            Cập nhật
          </button>
        </div>
      </div>
    }
  `,
  styles: [`
    @keyframes slide-up {
      from { transform: translateY(100%); opacity: 0; }
      to { transform: translateY(0); opacity: 1; }
    }
    .animate-slide-up {
      animation: slide-up 0.3s ease-out;
    }
  `]
})
export class PwaInstallBannerComponent implements OnInit {
  private pwaService = inject(PwaService);

  showInstallBanner = signal(false);
  showUpdateBanner = signal(false);

  ngOnInit(): void {
    this.pwaService.canInstall$.subscribe(canInstall => {
      if (canInstall && !this.wasInstallDismissed()) {
        this.showInstallBanner.set(true);
      }
    });

    this.pwaService.updateAvailable$.subscribe(available => {
      if (available) {
        this.showInstallBanner.set(false);
        this.showUpdateBanner.set(true);
      }
    });
  }

  async install(): Promise<void> {
    const accepted = await this.pwaService.promptInstall();
    if (!accepted) {
      this.showInstallBanner.set(false);
      this.markInstallDismissed();
    } else {
      this.showInstallBanner.set(false);
    }
  }

  dismissInstall(): void {
    this.showInstallBanner.set(false);
    this.markInstallDismissed();
  }

  async applyUpdate(): Promise<void> {
    await this.pwaService.applyUpdate();
  }

  private wasInstallDismissed(): boolean {
    return localStorage.getItem('pwa-install-dismissed') === 'true';
  }

  private markInstallDismissed(): void {
    localStorage.setItem('pwa-install-dismissed', 'true');
  }
}
