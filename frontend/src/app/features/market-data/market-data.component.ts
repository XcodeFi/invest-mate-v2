import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MarketDataService, StockPrice, MarketIndex } from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';

@Component({
  selector: 'app-market-data',
  standalone: true,
  imports: [CommonModule, FormsModule, VndCurrencyPipe, UppercaseDirective],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Dữ liệu Thị trường</h1>

      <!-- Market Indexes -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
        <div *ngFor="let idx of marketIndexes" class="bg-white rounded-lg shadow p-4">
          <div class="text-sm text-gray-500 mb-1">{{ idx.indexSymbol }}</div>
          <div class="text-2xl font-bold" [ngClass]="idx.change >= 0 ? 'text-green-600' : 'text-red-600'">
            {{ formatNumber(idx.close) }}
          </div>
          <div class="flex items-center mt-1">
            <span [ngClass]="idx.change >= 0 ? 'text-green-600' : 'text-red-600'" class="text-sm font-medium">
              {{ idx.change >= 0 ? '+' : '' }}{{ formatNumber(idx.change) }}
              ({{ idx.changePercent >= 0 ? '+' : '' }}{{ idx.changePercent.toFixed(2) }}%)
            </span>
          </div>
          <div class="text-xs text-gray-400 mt-1">KL: {{ formatVolume(idx.volume) }}</div>
        </div>
      </div>

      <!-- Stock Lookup -->
      <div class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-4">Tra cứu giá cổ phiếu</h2>
        <div class="flex gap-3 mb-4">
          <input
            type="text"
            [(ngModel)]="searchSymbol" appUppercase
            (keyup.enter)="lookupPrice()"
            placeholder="Nhập mã CP (VD: VNM, FPT, VCB...)"
            class="flex-1 px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent">
          <button
            (click)="lookupPrice()"
            [disabled]="loadingPrice"
            class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
            {{ loadingPrice ? 'Đang tải...' : 'Tra cứu' }}
          </button>
        </div>

        <!-- Current Price Result -->
        <div *ngIf="currentPrice" class="border rounded-lg p-4">
          <div class="flex justify-between items-center mb-2">
            <span class="text-xl font-bold text-gray-800">{{ currentPrice.symbol }}</span>
            <span class="text-sm text-gray-500">{{ currentPrice.date | date:'dd/MM/yyyy' }}</span>
          </div>
          <div class="grid grid-cols-2 md:grid-cols-5 gap-4">
            <div>
              <div class="text-xs text-gray-500">Mở cửa</div>
              <div class="font-semibold">{{ currentPrice.open | vndCurrency }}</div>
            </div>
            <div>
              <div class="text-xs text-gray-500">Cao nhất</div>
              <div class="font-semibold text-green-600">{{ currentPrice.high | vndCurrency }}</div>
            </div>
            <div>
              <div class="text-xs text-gray-500">Thấp nhất</div>
              <div class="font-semibold text-red-600">{{ currentPrice.low | vndCurrency }}</div>
            </div>
            <div>
              <div class="text-xs text-gray-500">Đóng cửa</div>
              <div class="font-bold text-lg">{{ currentPrice.close | vndCurrency }}</div>
            </div>
            <div>
              <div class="text-xs text-gray-500">Khối lượng</div>
              <div class="font-semibold">{{ formatVolume(currentPrice.volume) }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Price History -->
      <div class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-4">Lịch sử giá</h2>
        <div class="flex flex-wrap gap-3 mb-4">
          <input
            type="text"
            [(ngModel)]="historySymbol" appUppercase
            placeholder="Mã CP"
            class="px-4 py-2 border border-gray-300 rounded-lg w-32">
          <input
            type="date"
            [(ngModel)]="historyFrom"
            class="px-4 py-2 border border-gray-300 rounded-lg">
          <input
            type="date"
            [(ngModel)]="historyTo"
            class="px-4 py-2 border border-gray-300 rounded-lg">
          <button
            (click)="loadHistory()"
            [disabled]="loadingHistory"
            class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
            {{ loadingHistory ? 'Đang tải...' : 'Xem lịch sử' }}
          </button>
        </div>

        <!-- History Table -->
        <div *ngIf="priceHistory.length > 0" class="overflow-x-auto">
          <table class="min-w-full table-auto">
            <thead>
              <tr class="bg-gray-50 border-b">
                <th class="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">Ngày</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Mở cửa</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Cao nhất</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Thấp nhất</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Đóng cửa</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">KL</th>
                <th class="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase">Thay đổi</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of priceHistory; let i = index" class="border-b hover:bg-gray-50">
                <td class="px-4 py-3 text-sm">{{ p.date | date:'dd/MM/yyyy' }}</td>
                <td class="px-4 py-3 text-sm text-right">{{ p.open | vndCurrency }}</td>
                <td class="px-4 py-3 text-sm text-right text-green-600">{{ p.high | vndCurrency }}</td>
                <td class="px-4 py-3 text-sm text-right text-red-600">{{ p.low | vndCurrency }}</td>
                <td class="px-4 py-3 text-sm text-right font-semibold">{{ p.close | vndCurrency }}</td>
                <td class="px-4 py-3 text-sm text-right">{{ formatVolume(p.volume) }}</td>
                <td class="px-4 py-3 text-sm text-right font-medium" [ngClass]="getDailyChange(i) >= 0 ? 'text-green-600' : 'text-red-600'">
                  {{ getDailyChange(i) >= 0 ? '+' : '' }}{{ getDailyChange(i).toFixed(2) }}%
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="priceHistory.length === 0 && historyLoaded" class="text-center text-gray-500 py-8">
          Không có dữ liệu lịch sử giá
        </div>
      </div>

      <!-- Batch Prices (Watchlist) -->
      <div class="bg-white rounded-lg shadow p-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-4">Bảng giá nhanh</h2>
        <div class="flex gap-3 mb-4">
          <input
            type="text"
            [(ngModel)]="batchSymbols" appUppercase
            placeholder="Nhập các mã, cách nhau dấu phẩy (VD: VNM,FPT,VCB)"
            class="flex-1 px-4 py-2 border border-gray-300 rounded-lg">
          <button
            (click)="loadBatchPrices()"
            [disabled]="loadingBatch"
            class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
            {{ loadingBatch ? 'Đang tải...' : 'Xem giá' }}
          </button>
        </div>

        <div *ngIf="batchPrices.length > 0" class="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-3">
          <div *ngFor="let bp of batchPrices" class="border rounded-lg p-3 text-center hover:shadow-md transition-shadow">
            <div class="font-bold text-gray-800">{{ bp.symbol }}</div>
            <div class="text-lg font-semibold text-blue-600">{{ bp.close | vndCurrency }}</div>
            <div class="text-xs text-gray-500">KL: {{ formatVolume(bp.volume) }}</div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class MarketDataComponent implements OnInit {
  // Search
  searchSymbol = '';
  currentPrice: StockPrice | null = null;
  loadingPrice = false;

  // History
  historySymbol = '';
  historyFrom = '';
  historyTo = '';
  priceHistory: StockPrice[] = [];
  loadingHistory = false;
  historyLoaded = false;

  // Batch
  batchSymbols = 'VNM,FPT,VCB,HPG,MWG,VIC';
  batchPrices: { symbol: string; close: number; volume: number }[] = [];
  loadingBatch = false;

  // Market Indexes
  marketIndexes: MarketIndex[] = [];

  constructor(
    private marketDataService: MarketDataService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadMarketIndexes();
    this.loadBatchPrices();

    // Default history range: last 30 days
    const today = new Date();
    const thirtyDaysAgo = new Date(today);
    thirtyDaysAgo.setDate(today.getDate() - 30);
    this.historyTo = today.toISOString().split('T')[0];
    this.historyFrom = thirtyDaysAgo.toISOString().split('T')[0];
  }

  loadMarketIndexes(): void {
    const indexes = ['VNINDEX', 'VN30', 'HNX'];
    indexes.forEach(idx => {
      this.marketDataService.getMarketIndex(idx).subscribe({
        next: data => this.marketIndexes.push(data),
        error: () => {} // Silently skip unavailable indexes
      });
    });
  }

  lookupPrice(): void {
    if (!this.searchSymbol.trim()) return;
    this.loadingPrice = true;
    this.marketDataService.getCurrentPrice(this.searchSymbol.trim()).subscribe({
      next: data => {
        this.currentPrice = data;
        this.loadingPrice = false;
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không tìm thấy dữ liệu giá cho mã ' + this.searchSymbol);
        this.loadingPrice = false;
      }
    });
  }

  loadHistory(): void {
    if (!this.historySymbol.trim()) return;
    this.loadingHistory = true;
    this.historyLoaded = false;
    this.marketDataService.getPriceHistory(this.historySymbol.trim(), this.historyFrom, this.historyTo).subscribe({
      next: data => {
        this.priceHistory = data;
        this.loadingHistory = false;
        this.historyLoaded = true;
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi tải lịch sử giá');
        this.loadingHistory = false;
        this.historyLoaded = true;
      }
    });
  }

  loadBatchPrices(): void {
    if (!this.batchSymbols.trim()) return;
    this.loadingBatch = true;
    const symbols = this.batchSymbols.split(',').map(s => s.trim()).filter(s => s);
    this.marketDataService.getBatchPrices(symbols).subscribe({
      next: data => {
        this.batchPrices = data;
        this.loadingBatch = false;
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi tải bảng giá');
        this.loadingBatch = false;
      }
    });
  }

  getDailyChange(index: number): number {
    if (index >= this.priceHistory.length - 1) return 0;
    const current = this.priceHistory[index];
    const previous = this.priceHistory[index + 1];
    if (!previous || previous.close === 0) return 0;
    return ((current.close - previous.close) / previous.close) * 100;
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value);
  }

  formatVolume(volume: number): string {
    if (volume >= 1000000) return (volume / 1000000).toFixed(1) + 'M';
    if (volume >= 1000) return (volume / 1000).toFixed(0) + 'K';
    return volume.toString();
  }
}
