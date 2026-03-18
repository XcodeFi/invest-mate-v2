import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';

@Component({
  selector: 'app-portfolios',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe],
  template: `
    <div class="min-h-screen bg-gray-50">
      <!-- Header -->
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 py-6">
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Quản lý Danh mục</h1>
              <p class="text-gray-600 mt-1">Quản lý tất cả danh mục đầu tư của bạn</p>
            </div>
            <div class="flex space-x-3">
              <button
                routerLink="/portfolios/create"
                class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200"
              >
                + Tạo Danh mục mới
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Main Content -->
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Search and Filter -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
          <div class="flex flex-col sm:flex-row gap-4">
            <div class="flex-1">
              <input
                type="text"
                placeholder="Tìm kiếm danh mục..."
                class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                [(ngModel)]="searchTerm"
                (input)="onSearch()"
              />
            </div>
            <div class="flex gap-2">
              <select
                class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                [(ngModel)]="sortBy"
                (change)="onSort()"
              >
                <option value="name">Sắp xếp theo tên</option>
                <option value="value">Sắp xếp theo giá trị</option>
                <option value="gainLoss">Sắp xếp theo lãi/lỗ</option>
                <option value="createdAt">Sắp xếp theo ngày tạo</option>
              </select>
              <select
                class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                [(ngModel)]="sortOrder"
                (change)="onSort()"
              >
                <option value="asc">Tăng dần</option>
                <option value="desc">Giảm dần</option>
              </select>
            </div>
          </div>
        </div>

        <!-- Portfolio Grid -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          <div *ngFor="let portfolio of filteredPortfolios" class="bg-white rounded-lg shadow-sm border border-gray-200 hover:shadow-md transition-shadow duration-200">
            <div class="p-6">
              <div class="flex items-start justify-between mb-4">
                <div class="flex-1">
                  <h3 class="text-lg font-semibold text-gray-900 mb-1">{{ portfolio.name }}</h3>
                  <p class="text-sm text-gray-600">Tạo ngày: {{ formatDate(portfolio.createdAt) }}</p>
                </div>
                <div class="flex space-x-2">
                  <button
                    [routerLink]="['/portfolios', portfolio.id]"
                    class="text-blue-600 hover:text-blue-800 p-1"
                    title="Xem chi tiết"
                  >
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"></path>
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"></path>
                    </svg>
                  </button>
                  <button
                    [routerLink]="['/portfolios', portfolio.id, 'edit']"
                    class="text-gray-600 hover:text-gray-800 p-1"
                    title="Chỉnh sửa"
                  >
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"></path>
                    </svg>
                  </button>
                </div>
              </div>

              <div class="space-y-3">
                <div class="flex justify-between items-center">
                  <span class="text-sm text-gray-600">Vốn ban đầu:</span>
                  <span class="font-medium">{{ portfolio.initialCapital | vndCurrency }}</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-sm text-gray-600">Tổng đầu tư:</span>
                  <span class="font-medium">{{ portfolio.totalInvested | vndCurrency }}</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-sm text-gray-600">Tổng bán:</span>
                  <span class="font-medium">{{ portfolio.totalSold | vndCurrency }}</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-sm text-gray-600">Số mã CK:</span>
                  <span class="font-medium">{{ portfolio.uniqueSymbols }}</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-sm text-gray-600">Số giao dịch:</span>
                  <span class="font-medium">{{ portfolio.tradeCount }}</span>
                </div>
              </div>

              <div class="mt-4 pt-4 border-t border-gray-200">
                <div class="flex space-x-2">
                  <button
                    [routerLink]="['/portfolios', portfolio.id, 'trades']"
                    class="flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 px-3 py-2 rounded-lg text-sm font-medium transition-colors duration-200"
                  >
                    Xem giao dịch
                  </button>
                  <button
                    [routerLink]="['/portfolios', portfolio.id, 'analytics']"
                    class="flex-1 bg-blue-100 hover:bg-blue-200 text-blue-700 px-3 py-2 rounded-lg text-sm font-medium transition-colors duration-200"
                  >
                    Phân tích
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Empty State -->
        <div *ngIf="filteredPortfolios.length === 0" class="text-center py-12">
          <svg class="mx-auto h-12 w-12 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"></path>
          </svg>
          <h3 class="mt-2 text-sm font-medium text-gray-900">Chưa có danh mục nào</h3>
          <p class="mt-1 text-sm text-gray-500">Bắt đầu bằng cách tạo danh mục đầu tư đầu tiên của bạn.</p>
          <div class="mt-6">
            <button
              routerLink="/portfolios/create"
              class="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors duration-200"
            >
              Tạo danh mục đầu tiên
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class PortfoliosComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  filteredPortfolios: PortfolioSummary[] = [];
  searchTerm: string = '';
  sortBy: string = 'name';
  sortOrder: string = 'asc';
  isLoading = true;

  constructor(
    private portfolioService: PortfolioService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadPortfolios();
  }

  private loadPortfolios(): void {
    this.isLoading = true;
    this.portfolioService.getAll().subscribe({
      next: (data) => {
        this.portfolios = data;
        this.filteredPortfolios = [...data];
        this.onSort();
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notificationService.error('Lỗi', 'Không thể tải danh sách danh mục');
      }
    });
  }

  onSearch(): void {
    this.filteredPortfolios = this.portfolios.filter(portfolio =>
      portfolio.name.toLowerCase().includes(this.searchTerm.toLowerCase())
    );
    this.onSort();
  }

  onSort(): void {
    this.filteredPortfolios.sort((a, b) => {
      let aValue: any, bValue: any;

      switch (this.sortBy) {
        case 'name':
          aValue = a.name.toLowerCase();
          bValue = b.name.toLowerCase();
          break;
        case 'value':
          aValue = a.totalInvested;
          bValue = b.totalInvested;
          break;
        case 'gainLoss':
          aValue = a.totalSold - a.totalInvested;
          bValue = b.totalSold - b.totalInvested;
          break;
        case 'createdAt':
          aValue = new Date(a.createdAt).getTime();
          bValue = new Date(b.createdAt).getTime();
          break;
        default:
          return 0;
      }

      if (this.sortOrder === 'asc') {
        return aValue > bValue ? 1 : -1;
      } else {
        return aValue < bValue ? 1 : -1;
      }
    });
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('vi-VN');
  }
}