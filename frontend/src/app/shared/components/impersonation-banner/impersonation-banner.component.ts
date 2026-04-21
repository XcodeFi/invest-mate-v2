import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ImpersonationService } from '../../../core/services/impersonation.service';

@Component({
  selector: 'app-impersonation-banner',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="isActive()" class="sticky top-0 z-50 bg-red-600 text-white text-sm shadow">
      <div class="max-w-7xl mx-auto px-4 py-2 flex items-center gap-3 flex-wrap">
        <span class="font-semibold">⚠️ Đang xem với tư cách:</span>
        <span class="font-mono">{{ targetEmail() }}</span>
        <span class="opacity-80 hidden md:inline">— Thao tác POST/PUT/DELETE sẽ bị chặn và ghi log.</span>
        <button
          type="button"
          class="ml-auto bg-white text-red-700 font-semibold px-3 py-1 rounded hover:bg-red-100 disabled:opacity-50"
          [disabled]="isStopping"
          (click)="stop()">
          {{ isStopping ? 'Đang thoát…' : 'Thoát impersonate' }}
        </button>
      </div>
    </div>
  `
})
export class ImpersonationBannerComponent {
  isStopping = false;

  constructor(
    private impersonationService: ImpersonationService,
    private router: Router
  ) {}

  isActive(): boolean {
    return this.impersonationService.isImpersonating();
  }

  targetEmail(): string {
    return this.impersonationService.getTargetInfo()?.email ?? '(không rõ)';
  }

  stop(): void {
    if (this.isStopping) return;
    this.isStopping = true;
    this.impersonationService.stopImpersonate().subscribe({
      next: () => {
        this.isStopping = false;
        this.router.navigate(['/dashboard']).then(() => window.location.reload());
      },
      error: () => {
        this.isStopping = false;
      }
    });
  }
}
