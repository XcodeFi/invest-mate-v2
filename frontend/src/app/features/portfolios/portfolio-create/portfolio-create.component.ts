import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PortfolioService } from '../../../core/services/portfolio.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-portfolio-create',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="min-h-screen bg-gray-50">
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex items-center py-6">
            <button routerLink="/portfolios" class="mr-4 text-gray-500 hover:text-gray-700">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
              </svg>
            </button>
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Tạo Danh mục Mới</h1>
              <p class="text-gray-600 mt-1">Thiết lập danh mục đầu tư mới</p>
            </div>
          </div>
        </div>
      </div>

      <div class="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <form (ngSubmit)="onSubmit()" #portfolioForm="ngForm">
            <div class="space-y-6">
              <div>
                <label for="name" class="block text-sm font-medium text-gray-700 mb-1">Tên danh mục <span class="text-red-500">*</span></label>
                <input
                  type="text"
                  id="name"
                  name="name"
                  [(ngModel)]="form.name"
                  required
                  maxlength="100"
                  class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  placeholder="VD: Danh mục Chính, Danh mục Ngắn hạn..."
                  #nameInput="ngModel"
                />
                <p *ngIf="nameInput.invalid && nameInput.touched" class="mt-1 text-sm text-red-600">Tên danh mục là bắt buộc</p>
              </div>

              <div>
                <label for="initialCapital" class="block text-sm font-medium text-gray-700 mb-1">Vốn ban đầu (VND) <span class="text-red-500">*</span></label>
                <input
                  type="number"
                  id="initialCapital"
                  name="initialCapital"
                  [(ngModel)]="form.initialCapital"
                  required
                  min="1"
                  max="10000000000"
                  class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  placeholder="VD: 100000000"
                  #capitalInput="ngModel"
                />
                <p *ngIf="capitalInput.invalid && capitalInput.touched" class="mt-1 text-sm text-red-600">Vốn ban đầu phải lớn hơn 0</p>
                <p *ngIf="form.initialCapital > 0" class="mt-1 text-sm text-gray-500">{{ formatCurrency(form.initialCapital) }}</p>
              </div>

              <div class="flex justify-end space-x-3 pt-4 border-t border-gray-200">
                <button type="button" routerLink="/portfolios" class="px-6 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium">
                  Hủy
                </button>
                <button
                  type="submit"
                  [disabled]="portfolioForm.invalid || isSubmitting"
                  class="px-6 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {{ isSubmitting ? 'Đang tạo...' : 'Tạo Danh mục' }}
                </button>
              </div>
            </div>
          </form>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class PortfolioCreateComponent {
  form = { name: '', initialCapital: 0 };
  isSubmitting = false;

  constructor(
    private portfolioService: PortfolioService,
    private notificationService: NotificationService,
    private router: Router
  ) {}

  onSubmit(): void {
    if (this.isSubmitting) return;
    this.isSubmitting = true;

    this.portfolioService.create(this.form).subscribe({
      next: (result) => {
        this.notificationService.success('Thành công', 'Danh mục đã được tạo thành công!');
        this.router.navigate(['/portfolios', result.id]);
      },
      error: (err) => {
        this.isSubmitting = false;
        this.notificationService.error('Lỗi', err.error?.message || 'Không thể tạo danh mục');
      }
    });
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
  }
}