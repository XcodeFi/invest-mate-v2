import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PortfolioService } from '../../../core/services/portfolio.service';
import { NotificationService } from '../../../core/services/notification.service';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../../shared/directives/num-mask.directive';

@Component({
  selector: 'app-portfolio-edit',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe, NumMaskDirective],
  template: `
    <div class="min-h-screen bg-gray-50">
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex items-center py-6">
            <button (click)="goBack()" class="mr-4 text-gray-500 hover:text-gray-700">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
              </svg>
            </button>
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Chỉnh sửa Danh mục</h1>
              <p class="text-gray-600 mt-1">Cập nhật thông tin danh mục đầu tư</p>
            </div>
          </div>
        </div>
      </div>

      <!-- Loading -->
      <div *ngIf="isLoading" class="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="text-center py-12">
          <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p class="mt-4 text-gray-600">Đang tải dữ liệu...</p>
        </div>
      </div>

      <div *ngIf="!isLoading" class="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <form (ngSubmit)="onSubmit()" #editForm="ngForm">
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
                  #nameInput="ngModel"
                />
                <p *ngIf="nameInput.invalid && nameInput.touched" class="mt-1 text-sm text-red-600">Tên danh mục là bắt buộc</p>
              </div>

              <div>
                <label for="initialCapital" class="block text-sm font-medium text-gray-700 mb-1">Vốn ban đầu (VND) <span class="text-red-500">*</span></label>
                <input
                  type="text" inputmode="numeric" appNumMask
                  id="initialCapital"
                  name="initialCapital"
                  [(ngModel)]="form.initialCapital"
                  required
                  min="1"
                  class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  #capitalInput="ngModel"
                />
                <p *ngIf="capitalInput.invalid && capitalInput.touched" class="mt-1 text-sm text-red-600">Vốn ban đầu phải lớn hơn 0</p>
                <p *ngIf="form.initialCapital > 0" class="mt-1 text-sm text-gray-500">{{ form.initialCapital | vndCurrency }}</p>
              </div>

              <div class="flex justify-end space-x-3 pt-4 border-t border-gray-200">
                <button type="button" (click)="goBack()" class="px-6 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium">
                  Hủy
                </button>
                <button
                  type="submit"
                  [disabled]="editForm.invalid || isSubmitting"
                  class="px-6 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {{ isSubmitting ? 'Đang lưu...' : 'Lưu thay đổi' }}
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
export class PortfolioEditComponent implements OnInit {
  form = { name: '', initialCapital: 0 };
  isLoading = true;
  isSubmitting = false;
  private portfolioId = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private portfolioService: PortfolioService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.portfolioId = this.route.snapshot.paramMap.get('id') || '';
    this.loadPortfolio();
  }

  private loadPortfolio(): void {
    this.portfolioService.getById(this.portfolioId).subscribe({
      next: (data) => {
        this.form.name = data.name;
        this.form.initialCapital = data.initialCapital;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notificationService.error('Lỗi', 'Không thể tải dữ liệu danh mục');
        this.router.navigate(['/portfolios']);
      }
    });
  }

  onSubmit(): void {
    if (this.isSubmitting) return;
    this.isSubmitting = true;

    this.portfolioService.update(this.portfolioId, this.form).subscribe({
      next: () => {
        this.notificationService.success('Thành công', 'Danh mục đã được cập nhật');
        this.router.navigate(['/portfolios', this.portfolioId]);
      },
      error: (err) => {
        this.isSubmitting = false;
        this.notificationService.error('Lỗi', err.error?.message || 'Không thể cập nhật danh mục');
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/portfolios', this.portfolioId]);
  }

}