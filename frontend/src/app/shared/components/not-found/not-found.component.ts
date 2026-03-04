import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="min-h-screen bg-gray-50 flex flex-col justify-center py-12 sm:px-6 lg:px-8">
      <div class="mt-8 sm:mx-auto sm:w-full sm:max-w-md">
        <div class="bg-white py-8 px-4 shadow-sm sm:rounded-lg sm:px-10 text-center">
          <div class="mb-6">
            <svg class="mx-auto h-24 w-24 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.172 16.172a4 4 0 015.656 0M9 12h6m-6-4h6m2 5.291A7.962 7.962 0 0112 15c-2.34 0-4.29-.978-5.625-2.508M12 2a9.98 9.98 0 00-7.071 2.929A9.98 9.98 0 002 12a9.98 9.98 0 002.929 7.071A9.98 9.98 0 0012 22a9.98 9.98 0 007.071-2.929A9.98 9.98 0 0022 12a9.98 9.98 0 00-2.929-7.071A9.98 9.98 0 0012 2z"></path>
            </svg>
          </div>
          <h1 class="text-4xl font-bold text-gray-900 mb-2">404</h1>
          <h2 class="text-xl font-semibold text-gray-700 mb-4">Không tìm thấy trang</h2>
          <p class="text-gray-600 mb-8">
            Trang bạn đang tìm kiếm không tồn tại hoặc đã được di chuyển.
          </p>
          <div class="space-y-4">
            <button
              routerLink="/dashboard"
              class="w-full bg-blue-600 hover:bg-blue-700 text-white font-medium py-2 px-4 rounded-lg transition-colors duration-200"
            >
              Về trang chủ
            </button>
            <button
              (click)="goBack()"
              class="w-full bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium py-2 px-4 rounded-lg transition-colors duration-200"
            >
              Quay lại
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: []
})
export class NotFoundComponent {
  goBack(): void {
    window.history.back();
  }
}