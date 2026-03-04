import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-trade-edit',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50">
      <div class="bg-white shadow-sm border-b border-gray-200">
        <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div class="flex items-center py-6">
            <button routerLink="/trades" class="mr-4 text-gray-500 hover:text-gray-700">
              <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7"></path>
              </svg>
            </button>
            <div>
              <h1 class="text-3xl font-bold text-gray-900">Chỉnh sửa Giao dịch</h1>
              <p class="text-gray-600 mt-1">Chức năng chỉnh sửa giao dịch sẽ được hỗ trợ trong phiên bản tiếp theo</p>
            </div>
          </div>
        </div>
      </div>

      <div class="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 text-center">
          <svg class="mx-auto h-16 w-16 text-yellow-400 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z"></path>
          </svg>
          <h2 class="text-xl font-semibold text-gray-900 mb-2">Tính năng đang phát triển</h2>
          <p class="text-gray-600 mb-6">
            Để đảm bảo tính chính xác của dữ liệu P&L, hiện tại hệ thống chỉ hỗ trợ xóa giao dịch và tạo lại.
            Chức năng chỉnh sửa sẽ được bổ sung trong phiên bản tiếp theo.
          </p>
          <div class="flex justify-center space-x-3">
            <button routerLink="/trades" class="px-6 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-medium">
              Quay lại
            </button>
            <button routerLink="/trades/create" class="px-6 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium">
              Tạo giao dịch mới
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class TradeEditComponent implements OnInit {
  private tradeId = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.tradeId = this.route.snapshot.paramMap.get('id') || '';
  }
}