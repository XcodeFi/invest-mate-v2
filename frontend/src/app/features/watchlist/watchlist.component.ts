import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { WatchlistService, WatchlistSummary, WatchlistDetail, WatchlistItem } from '../../core/services/watchlist.service';
import { MarketDataService, BatchPrice, StockSearchResult, TechnicalAnalysis } from '../../core/services/market-data.service';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { forkJoin, Subject } from 'rxjs';
import { debounceTime, switchMap } from 'rxjs/operators';

interface WatchlistItemView extends WatchlistItem {
  price?: number;
  change?: number;
  changePercent?: number;
  volume?: number;
  loading?: boolean;
  signal?: string;
  signalVi?: string;
}

@Component({
  selector: 'app-watchlist',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, UppercaseDirective, VndCurrencyPipe],
  template: `
    <div class="container mx-auto px-4 py-6 max-w-6xl pb-20 md:pb-6">
      <!-- Header -->
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-6">
        <div>
          <h1 class="text-2xl font-bold text-gray-900">⭐ Watchlist</h1>
          <p class="text-sm text-gray-500 mt-1">Theo dõi cổ phiếu quan tâm & tìm cơ hội giao dịch</p>
        </div>
        <div class="flex gap-2">
          <button (click)="showImportVn30()"
            class="px-3 py-2 text-sm bg-amber-50 text-amber-700 rounded-lg hover:bg-amber-100 transition-colors font-medium">
            🏆 Nhập VN30
          </button>
          <button (click)="showCreateForm = true"
            class="px-3 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium">
            + Tạo danh sách
          </button>
        </div>
      </div>

      <!-- Watchlist tabs -->
      <div *ngIf="watchlists.length > 0" class="flex gap-2 mb-4 overflow-x-auto pb-2 scrollbar-hide">
        <button *ngFor="let wl of watchlists"
          (click)="selectWatchlist(wl)"
          class="flex items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium whitespace-nowrap transition-colors"
          [class.bg-blue-600]="selectedWatchlist?.id === wl.id"
          [class.text-white]="selectedWatchlist?.id === wl.id"
          [class.bg-gray-100]="selectedWatchlist?.id !== wl.id"
          [class.text-gray-700]="selectedWatchlist?.id !== wl.id"
          [class.hover:bg-gray-200]="selectedWatchlist?.id !== wl.id">
          {{ wl.emoji }} {{ wl.name }}
          <span class="text-xs opacity-70">({{ wl.itemCount }})</span>
        </button>
      </div>

      <!-- Empty state -->
      <div *ngIf="watchlists.length === 0 && !loading" class="text-center py-16 bg-white rounded-xl border border-gray-200">
        <div class="text-5xl mb-4">⭐</div>
        <h3 class="text-lg font-semibold text-gray-900 mb-2">Chưa có watchlist nào</h3>
        <p class="text-sm text-gray-500 mb-6">Tạo danh sách theo dõi để không bỏ lỡ cơ hội</p>
        <div class="flex justify-center gap-3">
          <button (click)="showImportVn30()"
            class="px-4 py-2 text-sm bg-amber-50 text-amber-700 rounded-lg hover:bg-amber-100 font-medium">
            🏆 Nhập VN30
          </button>
          <button (click)="showCreateForm = true"
            class="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-medium">
            + Tạo danh sách
          </button>
        </div>
      </div>

      <!-- Loading -->
      <div *ngIf="loading" class="text-center py-12">
        <div class="animate-spin w-8 h-8 border-4 border-blue-600 border-t-transparent rounded-full mx-auto"></div>
        <p class="text-sm text-gray-500 mt-3">Đang tải...</p>
      </div>

      <!-- Watchlist detail -->
      <div *ngIf="detail && !loading">
        <!-- Watchlist header -->
        <div class="bg-white rounded-xl border border-gray-200 p-4 mb-4">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <span class="text-2xl">{{ detail.emoji }}</span>
              <h2 class="text-lg font-bold text-gray-900">{{ detail.name }}</h2>
              <span class="text-xs text-gray-400">({{ detail.items.length }} mã)</span>
            </div>
            <div class="flex gap-2">
              <button (click)="editingWatchlist = true"
                class="p-2 text-gray-400 hover:text-blue-600 transition-colors" title="Sửa danh sách">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z"/></svg>
              </button>
              <button (click)="confirmDeleteWatchlist()"
                class="p-2 text-gray-400 hover:text-red-600 transition-colors" title="Xoá danh sách">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>
              </button>
            </div>
          </div>

          <!-- Add symbol -->
          <div class="mt-3 flex gap-2">
            <div class="relative flex-1">
              <input type="text" [(ngModel)]="newSymbol" appUppercase
                (ngModelChange)="onSymbolSearch($event)"
                (keydown.enter)="addSymbol()"
                placeholder="Nhập mã CK (VD: FPT, VNM...)"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
              <!-- Search results dropdown -->
              <div *ngIf="searchResults.length > 0"
                class="absolute top-full left-0 right-0 mt-1 bg-white rounded-lg shadow-lg border border-gray-200 z-10 max-h-48 overflow-y-auto">
                <button *ngFor="let r of searchResults"
                  (click)="selectSearchResult(r)"
                  class="w-full text-left px-3 py-2 text-sm hover:bg-gray-50 flex items-center gap-2">
                  <span class="font-semibold text-blue-600">{{ r.symbol }}</span>
                  <span class="text-gray-500 truncate">{{ r.companyName }}</span>
                  <span class="text-xs text-gray-400 ml-auto">{{ r.exchange }}</span>
                </button>
              </div>
            </div>
            <button (click)="addSymbol()" [disabled]="!newSymbol.trim()"
              class="px-4 py-2 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 disabled:opacity-50 transition-colors font-medium">
              Thêm
            </button>
          </div>
        </div>

        <!-- Items table -->
        <div *ngIf="itemViews.length > 0" class="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <!-- Desktop table -->
          <div class="hidden md:block overflow-x-auto">
            <table class="w-full">
              <thead class="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th class="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase">Mã</th>
                  <th class="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase">Giá</th>
                  <th class="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase">+/-</th>
                  <th class="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase">KL</th>
                  <th class="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase">Tín hiệu</th>
                  <th class="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase">Mua tại</th>
                  <th class="px-4 py-3 text-right text-xs font-semibold text-gray-500 uppercase">Bán tại</th>
                  <th class="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase">Ghi chú</th>
                  <th class="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase">Hành động</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-100">
                <tr *ngFor="let item of itemViews; let i = index"
                  class="hover:bg-gray-50 transition-colors">
                  <td class="px-4 py-3">
                    <a [routerLink]="'/market-data'" [queryParams]="{ symbol: item.symbol }"
                      class="font-semibold text-blue-600 hover:underline">{{ item.symbol }}</a>
                  </td>
                  <td class="px-4 py-3 text-right font-mono text-sm">
                    <span *ngIf="item.price">{{ item.price | vndCurrency }}</span>
                    <span *ngIf="!item.price" class="text-gray-400">—</span>
                  </td>
                  <td class="px-4 py-3 text-right text-sm font-medium"
                    [class.text-green-600]="(item.changePercent || 0) > 0"
                    [class.text-red-600]="(item.changePercent || 0) < 0"
                    [class.text-gray-500]="(item.changePercent || 0) === 0">
                    <span *ngIf="item.changePercent !== undefined">
                      {{ (item.changePercent || 0) > 0 ? '+' : '' }}{{ (item.changePercent || 0) | number:'1.1-2' }}%
                    </span>
                    <span *ngIf="item.changePercent === undefined" class="text-gray-400">—</span>
                  </td>
                  <td class="px-4 py-3 text-right text-sm text-gray-600 font-mono">
                    <span *ngIf="item.volume">{{ item.volume | number:'1.0-0' }}</span>
                    <span *ngIf="!item.volume" class="text-gray-400">—</span>
                  </td>
                  <td class="px-4 py-3 text-center">
                    <span *ngIf="item.signal" class="text-xs px-2 py-0.5 rounded-full font-semibold whitespace-nowrap"
                      [class.bg-green-100]="item.signal === 'strong_buy' || item.signal === 'buy'"
                      [class.text-green-700]="item.signal === 'strong_buy' || item.signal === 'buy'"
                      [class.bg-red-100]="item.signal === 'strong_sell' || item.signal === 'sell'"
                      [class.text-red-700]="item.signal === 'strong_sell' || item.signal === 'sell'"
                      [class.bg-amber-100]="item.signal === 'hold'"
                      [class.text-amber-700]="item.signal === 'hold'">
                      {{ item.signalVi }}
                    </span>
                    <span *ngIf="!item.signal" class="text-gray-400 text-xs">—</span>
                  </td>
                  <td class="px-4 py-3 text-right">
                    <span *ngIf="item.targetBuyPrice" class="text-sm font-mono text-green-600">
                      {{ item.targetBuyPrice | vndCurrency }}
                    </span>
                    <span *ngIf="!item.targetBuyPrice" class="text-gray-400 text-sm">—</span>
                  </td>
                  <td class="px-4 py-3 text-right">
                    <span *ngIf="item.targetSellPrice" class="text-sm font-mono text-red-600">
                      {{ item.targetSellPrice | vndCurrency }}
                    </span>
                    <span *ngIf="!item.targetSellPrice" class="text-gray-400 text-sm">—</span>
                  </td>
                  <td class="px-4 py-3 text-sm text-gray-600 max-w-[200px] truncate">
                    {{ item.note || '' }}
                  </td>
                  <td class="px-4 py-3 text-center">
                    <div class="flex items-center justify-center gap-1">
                      <a [routerLink]="'/trade-plan'" [queryParams]="{ symbol: item.symbol }"
                        class="p-1.5 text-blue-500 hover:bg-blue-50 rounded transition-colors" title="Tạo kế hoạch">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/></svg>
                      </a>
                      <button (click)="editItem(item)"
                        class="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded transition-colors" title="Sửa ghi chú">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z"/></svg>
                      </button>
                      <button (click)="removeItem(item.symbol)"
                        class="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded transition-colors" title="Xoá">
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
                      </button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Mobile cards -->
          <div class="md:hidden divide-y divide-gray-100">
            <div *ngFor="let item of itemViews" class="p-4">
              <div class="flex items-center justify-between mb-2">
                <a [routerLink]="'/market-data'" [queryParams]="{ symbol: item.symbol }"
                  class="font-semibold text-blue-600 text-lg">{{ item.symbol }}</a>
                <span class="text-sm font-medium"
                  [class.text-green-600]="(item.changePercent || 0) > 0"
                  [class.text-red-600]="(item.changePercent || 0) < 0">
                  <span *ngIf="item.price" class="text-gray-900 mr-2">{{ item.price | vndCurrency }}</span>
                  <span *ngIf="item.changePercent !== undefined">
                    {{ (item.changePercent || 0) > 0 ? '+' : '' }}{{ (item.changePercent || 0) | number:'1.1-2' }}%
                  </span>
                </span>
              </div>
              <div class="flex items-center gap-4 text-xs text-gray-500 mb-2">
                <span *ngIf="item.volume">KL: {{ item.volume | number:'1.0-0' }}</span>
                <span *ngIf="item.signal" class="px-2 py-0.5 rounded-full font-semibold"
                  [class.bg-green-100]="item.signal === 'strong_buy' || item.signal === 'buy'"
                  [class.text-green-700]="item.signal === 'strong_buy' || item.signal === 'buy'"
                  [class.bg-red-100]="item.signal === 'strong_sell' || item.signal === 'sell'"
                  [class.text-red-700]="item.signal === 'strong_sell' || item.signal === 'sell'"
                  [class.bg-amber-100]="item.signal === 'hold'"
                  [class.text-amber-700]="item.signal === 'hold'">
                  {{ item.signalVi }}
                </span>
                <span *ngIf="item.targetBuyPrice" class="text-green-600">Mua: {{ item.targetBuyPrice | vndCurrency }}</span>
                <span *ngIf="item.targetSellPrice" class="text-red-600">Bán: {{ item.targetSellPrice | vndCurrency }}</span>
              </div>
              <div *ngIf="item.note" class="text-xs text-gray-500 mb-2">{{ item.note }}</div>
              <div class="flex gap-2">
                <a [routerLink]="'/trade-plan'" [queryParams]="{ symbol: item.symbol }"
                  class="text-xs text-blue-600 hover:underline">📋 Tạo Plan</a>
                <button (click)="editItem(item)" class="text-xs text-gray-500 hover:text-blue-600">✏️ Sửa</button>
                <button (click)="removeItem(item.symbol)" class="text-xs text-gray-500 hover:text-red-600">🗑️ Xoá</button>
              </div>
            </div>
          </div>
        </div>

        <!-- Empty items -->
        <div *ngIf="itemViews.length === 0 && !loading" class="text-center py-12 bg-white rounded-xl border border-gray-200">
          <div class="text-4xl mb-3">📝</div>
          <p class="text-sm text-gray-500">Chưa có mã nào. Thêm mã ở ô tìm kiếm phía trên.</p>
        </div>
      </div>

      <!-- Create watchlist modal -->
      <div *ngIf="showCreateForm" class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
        <div class="bg-white rounded-xl shadow-xl w-full max-w-md p-6">
          <h3 class="text-lg font-bold text-gray-900 mb-4">{{ editingWatchlist ? 'Sửa danh sách' : 'Tạo danh sách mới' }}</h3>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Tên danh sách</label>
              <input type="text" [(ngModel)]="formName" placeholder="VD: Cổ phiếu theo dõi"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Emoji</label>
              <div class="flex gap-2 flex-wrap">
                <button *ngFor="let e of emojiOptions" (click)="formEmoji = e"
                  class="w-10 h-10 rounded-lg text-xl flex items-center justify-center transition-colors"
                  [class.bg-blue-100]="formEmoji === e"
                  [class.ring-2]="formEmoji === e"
                  [class.ring-blue-500]="formEmoji === e"
                  [class.bg-gray-50]="formEmoji !== e"
                  [class.hover:bg-gray-100]="formEmoji !== e">
                  {{ e }}
                </button>
              </div>
            </div>
          </div>
          <div class="flex justify-end gap-3 mt-6">
            <button (click)="cancelForm()" class="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg">Huỷ</button>
            <button (click)="saveWatchlist()" [disabled]="!formName.trim()"
              class="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50">
              {{ editingWatchlist ? 'Cập nhật' : 'Tạo' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Edit item modal -->
      <div *ngIf="editingItem" class="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
        <div class="bg-white rounded-xl shadow-xl w-full max-w-md p-6">
          <h3 class="text-lg font-bold text-gray-900 mb-4">Sửa {{ editingItem.symbol }}</h3>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Ghi chú</label>
              <input type="text" [(ngModel)]="editNote" placeholder="VD: Chờ breakout 82k"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Giá mua mục tiêu</label>
                <input type="number" [(ngModel)]="editTargetBuy" placeholder="0"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-700 mb-1">Giá bán mục tiêu</label>
                <input type="number" [(ngModel)]="editTargetSell" placeholder="0"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
              </div>
            </div>
          </div>
          <div class="flex justify-end gap-3 mt-6">
            <button (click)="editingItem = null" class="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg">Huỷ</button>
            <button (click)="saveItem()" class="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700">
              Lưu
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .scrollbar-hide::-webkit-scrollbar { display: none; }
    .scrollbar-hide { -ms-overflow-style: none; scrollbar-width: none; }
  `]
})
export class WatchlistComponent implements OnInit {
  watchlists: WatchlistSummary[] = [];
  selectedWatchlist: WatchlistSummary | null = null;
  detail: WatchlistDetail | null = null;
  itemViews: WatchlistItemView[] = [];
  loading = false;
  priceMap: Map<string, BatchPrice> = new Map();

  // Add symbol
  newSymbol = '';
  searchResults: StockSearchResult[] = [];
  private searchSubject = new Subject<string>();

  // Create/edit watchlist form
  showCreateForm = false;
  editingWatchlist = false;
  formName = '';
  formEmoji = '⭐';
  emojiOptions = ['⭐', '🔥', '🏆', '💎', '🚀', '📊', '🎯', '💰', '🔍', '📌'];

  // Edit item
  editingItem: WatchlistItemView | null = null;
  editNote = '';
  editTargetBuy: number | null = null;
  editTargetSell: number | null = null;

  constructor(
    private watchlistService: WatchlistService,
    private marketDataService: MarketDataService
  ) {}

  ngOnInit(): void {
    this.loadWatchlists();

    this.searchSubject.pipe(
      debounceTime(300),
      switchMap(keyword => {
        if (keyword.length < 1) return [[]];
        return this.marketDataService.searchStocks(keyword);
      })
    ).subscribe(results => {
      this.searchResults = results.slice(0, 8);
    });
  }

  loadWatchlists(): void {
    this.loading = true;
    this.watchlistService.getAll().subscribe({
      next: (list) => {
        this.watchlists = list;
        if (list.length > 0) {
          const selected = this.selectedWatchlist
            ? list.find(w => w.id === this.selectedWatchlist!.id) || list[0]
            : list[0];
          this.selectWatchlist(selected);
        } else {
          this.loading = false;
        }
      },
      error: () => { this.loading = false; }
    });
  }

  selectWatchlist(wl: WatchlistSummary): void {
    this.selectedWatchlist = wl;
    this.loadDetail(wl.id);
  }

  loadDetail(id: string): void {
    this.loading = true;
    this.watchlistService.getDetail(id).subscribe({
      next: (detail) => {
        this.detail = detail;
        this.itemViews = detail.items.map(i => ({ ...i }));
        this.loading = false;
        this.loadPrices();
      },
      error: () => { this.loading = false; }
    });
  }

  loadPrices(): void {
    if (this.itemViews.length === 0) return;

    const symbols = this.itemViews.map(i => i.symbol);
    this.marketDataService.getBatchPrices(symbols).subscribe({
      next: (prices) => {
        const map = new Map(prices.map(p => [p.symbol, p]));
        this.priceMap = map;
        this.itemViews = this.itemViews.map(item => {
          const p = map.get(item.symbol);
          if (p) {
            return { ...item, price: p.close, volume: p.volume };
          }
          return item;
        });
        // Load change % via individual price calls for top items
        this.loadChangePercents();
      }
    });
  }

  loadChangePercents(): void {
    // Get detail for each symbol to get changePercent
    const calls = this.itemViews.slice(0, 30).map(item =>
      this.marketDataService.getCurrentPrice(item.symbol)
    );
    if (calls.length === 0) return;

    forkJoin(calls).subscribe({
      next: (prices) => {
        prices.forEach((p, i) => {
          if (i < this.itemViews.length && p) {
            const prev = p.open || p.close;
            this.itemViews[i] = {
              ...this.itemViews[i],
              price: p.close,
              volume: p.volume,
              change: prev ? p.close - prev : 0,
              changePercent: prev ? ((p.close - prev) / prev) * 100 : 0
            };
          }
        });
        this.loadSignals();
      }
    });
  }

  loadSignals(): void {
    // Load technical signals for up to 10 symbols
    const items = this.itemViews.slice(0, 10);
    if (items.length === 0) return;

    const calls = items.map(item =>
      this.marketDataService.getTechnicalAnalysis(item.symbol)
    );
    forkJoin(calls).subscribe({
      next: (analyses) => {
        analyses.forEach((a, i) => {
          if (i < this.itemViews.length && a) {
            this.itemViews[i] = {
              ...this.itemViews[i],
              signal: a.overallSignal,
              signalVi: a.overallSignalVi
            };
          }
        });
      }
    });
  }

  // Symbol search
  onSymbolSearch(keyword: string): void {
    this.searchSubject.next(keyword);
  }

  selectSearchResult(result: StockSearchResult): void {
    this.newSymbol = result.symbol;
    this.searchResults = [];
    this.addSymbol();
  }

  addSymbol(): void {
    if (!this.newSymbol.trim() || !this.detail) return;
    this.searchResults = [];

    this.watchlistService.addItem(this.detail.id, {
      symbol: this.newSymbol.trim()
    }).subscribe({
      next: (updated) => {
        this.detail = updated;
        this.itemViews = updated.items.map(i => ({ ...i }));
        this.newSymbol = '';
        this.refreshSummary();
        this.loadPrices();
      },
      error: (err) => {
        alert(err.error?.detail || err.error?.message || 'Không thể thêm mã');
      }
    });
  }

  removeItem(symbol: string): void {
    if (!this.detail) return;
    if (!confirm('Xoá ' + symbol + ' khỏi watchlist?')) return;

    this.watchlistService.removeItem(this.detail.id, symbol).subscribe({
      next: (updated) => {
        this.detail = updated;
        this.itemViews = updated.items.map(i => {
          const existing = this.itemViews.find(v => v.symbol === i.symbol);
          return existing ? { ...i, price: existing.price, change: existing.change, changePercent: existing.changePercent, volume: existing.volume, signal: existing.signal, signalVi: existing.signalVi } : { ...i };
        });
        this.refreshSummary();
      }
    });
  }

  // Edit item
  editItem(item: WatchlistItemView): void {
    this.editingItem = item;
    this.editNote = item.note || '';
    this.editTargetBuy = item.targetBuyPrice || null;
    this.editTargetSell = item.targetSellPrice || null;
  }

  saveItem(): void {
    if (!this.editingItem || !this.detail) return;

    this.watchlistService.updateItem(this.detail.id, this.editingItem.symbol, {
      note: this.editNote || undefined,
      targetBuyPrice: this.editTargetBuy || undefined,
      targetSellPrice: this.editTargetSell || undefined
    }).subscribe({
      next: (updated) => {
        this.detail = updated;
        this.itemViews = updated.items.map(i => {
          const existing = this.itemViews.find(v => v.symbol === i.symbol);
          return existing ? { ...i, price: existing.price, change: existing.change, changePercent: existing.changePercent, volume: existing.volume, signal: existing.signal, signalVi: existing.signalVi } : { ...i };
        });
        this.editingItem = null;
      }
    });
  }

  // Watchlist CRUD
  showImportVn30(): void {
    this.watchlistService.importVn30(this.selectedWatchlist?.id).subscribe({
      next: () => { this.loadWatchlists(); },
      error: (err) => { alert(err.error?.detail || 'Không thể import VN30'); }
    });
  }

  saveWatchlist(): void {
    if (!this.formName.trim()) return;

    if (this.editingWatchlist && this.detail) {
      this.watchlistService.update(this.detail.id, {
        name: this.formName,
        emoji: this.formEmoji
      }).subscribe({
        next: () => {
          this.cancelForm();
          this.loadWatchlists();
        }
      });
    } else {
      this.watchlistService.create({
        name: this.formName,
        emoji: this.formEmoji
      }).subscribe({
        next: (created) => {
          this.cancelForm();
          this.loadWatchlists();
        }
      });
    }
  }

  confirmDeleteWatchlist(): void {
    if (!this.detail) return;
    if (!confirm('Xoá danh sách "' + this.detail.name + '"?')) return;

    this.watchlistService.delete(this.detail.id).subscribe({
      next: () => {
        this.selectedWatchlist = null;
        this.detail = null;
        this.itemViews = [];
        this.loadWatchlists();
      }
    });
  }

  cancelForm(): void {
    this.showCreateForm = false;
    this.editingWatchlist = false;
    this.formName = '';
    this.formEmoji = '⭐';
  }

  private refreshSummary(): void {
    // Update the summary count locally
    if (this.selectedWatchlist && this.detail) {
      this.selectedWatchlist.itemCount = this.detail.items.length;
    }
  }
}
