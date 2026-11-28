import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MarketDataService, ScreenerLookup, ScreenerPassedRow, ScreenerQuery, ScreenerResult } from '../../core/services/market-data.service';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { SymbolLinkDirective } from '../../shared/directives/symbol-link.directive';
import { WatchlistService, WatchlistSummary } from '../../core/services/watchlist.service';
import { saveQuickBoardSymbols } from '../market-data/quick-board-storage';

/** Cột sắp xếp được. Mặc định (sortKey = null) giữ nguyên thứ tự server trả về: bảng chữ cái, không ngầm xếp hạng. */
type SortKey = 'symbol' | 'floor' | 'sector' | 'adtvBn' | 'marketCapBn' | 'roe' | 'pe' | 'rs3m';

const DISCLAIMER = 'Danh sách lọc theo tiêu chí, không phải khuyến nghị mua. Dữ liệu từ nguồn công khai không chính thức, có thể trễ. Cờ dựa trên xếp hạng tương đối, không phải đánh giá rủi ro.';
const MAX_SELECTION = 30;

/**
 * Sàng lọc cổ phiếu toàn thị trường (ADR-0020). Danh sách thoả tiêu chí, KHÔNG phải khuyến nghị: không có điểm số, mặc định xếp theo bảng chữ cái,
 * người dùng tự sắp theo cột nào cũng được. Mọi chuỗi từ server (lý do, ghi chú) chỉ hiển thị dạng văn bản (nội suy Angular), không bao giờ là HTML.
 */
@Component({
  selector: 'app-screener',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, UppercaseDirective, SymbolLinkDirective],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex flex-wrap items-baseline justify-between gap-2 mb-2">
        <h1 class="text-2xl font-bold text-gray-800">Sàng lọc cổ phiếu</h1>
        <a routerLink="/market-data" class="text-sm text-blue-600 underline">← Dữ liệu Thị trường</a>
      </div>
      <p class="text-sm text-gray-600 mb-4" data-testid="screener-intro">
        Quét toàn thị trường theo tiêu chí: loại mã thuộc diện đặc biệt hoặc sắp hủy niêm yết của HOSE, thanh khoản thấp, giá thấp, vốn hoá nhỏ;
        rồi lọc chất lượng (lãi ròng dương, ROE) và xu hướng theo xếp hạng sức mạnh 3 tháng trong toàn bộ mã niêm yết.
        <a routerLink="/help" [queryParams]="{ topic: 'sang-loc-co-phieu' }" class="text-blue-600 underline">Xem hướng dẫn</a>
      </p>

      <p class="text-xs text-amber-800 bg-amber-50 border border-amber-200 rounded px-3 py-2 mb-4" data-testid="screener-disclaimer">{{ disclaimer }}</p>

      <!-- Tiêu chí -->
      <div class="bg-white rounded-lg shadow p-4 mb-4">
        <div class="flex flex-wrap items-center gap-4 mb-3">
          <span class="text-sm font-medium text-gray-700">Sàn:</span>
          <label class="text-sm"><input type="checkbox" data-testid="screener-floor-hose" [(ngModel)]="floorHose" class="mr-1">HOSE</label>
          <label class="text-sm"><input type="checkbox" data-testid="screener-floor-hnx" [(ngModel)]="floorHnx" class="mr-1">HNX</label>
          <label class="text-sm" title="Thanh khoản thấp, nhiều mã đầu cơ và chưa có danh sách cảnh báo chính thức nên mặc định tắt.">
            <input type="checkbox" data-testid="screener-floor-upcom" [(ngModel)]="floorUpcom" class="mr-1">UPCOM (tuỳ chọn)
          </label>
        </div>
        <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3 text-sm">
          <label class="flex flex-col gap-1">GTGD bình quân 10 phiên tối thiểu (tỷ)
            <input type="number" data-testid="screener-min-adtv" [(ngModel)]="minAdtv" class="mt-auto w-full px-2 py-1 border border-gray-300 rounded"></label>
          <label class="flex flex-col gap-1">Giá tối thiểu (đ)
            <input type="number" data-testid="screener-min-price" [(ngModel)]="minPrice" class="mt-auto w-full px-2 py-1 border border-gray-300 rounded"></label>
          <label class="flex flex-col gap-1">Vốn hoá tối thiểu (tỷ)
            <input type="number" data-testid="screener-min-cap" [(ngModel)]="minCap" class="mt-auto w-full px-2 py-1 border border-gray-300 rounded"></label>
          <label class="flex flex-col gap-1">ROE tối thiểu (%)
            <input type="number" data-testid="screener-min-roe" [(ngModel)]="minRoe" class="mt-auto w-full px-2 py-1 border border-gray-300 rounded"></label>
          <label class="flex flex-col gap-1">Trần P/E (để trống = tắt)
            <input type="number" data-testid="screener-max-pe" [(ngModel)]="maxPe" class="mt-auto w-full px-2 py-1 border border-gray-300 rounded"></label>
          <label class="flex flex-col gap-1">Xếp hạng sức mạnh 3 tháng tối thiểu (P)
            <input type="number" data-testid="screener-min-rs3m" [(ngModel)]="minRs3m" class="mt-auto w-full px-2 py-1 border border-gray-300 rounded"></label>
        </div>
        <p class="text-xs text-gray-500 mt-1">P50 là mức trung vị: chỉ giữ mã có xếp hạng sức mạnh 3 tháng từ P{{ rankFloor }} trở lên trong toàn bộ mã niêm yết.</p>
        <div class="flex flex-wrap items-center gap-4 mt-3">
          <label class="text-sm"><input type="checkbox" data-testid="screener-skip-quality" [(ngModel)]="skipQuality" class="mr-1">Bỏ qua tầng chất lượng</label>
          <button type="button" data-testid="screener-run" (click)="run()" [disabled]="loading"
            class="bg-blue-600 hover:bg-blue-700 text-white px-5 py-2 rounded-lg font-medium disabled:opacity-50">{{ loading ? 'Đang lọc...' : 'Lọc' }}</button>
          <span *ngIf="floorError" class="text-sm text-red-600" data-testid="screener-floor-error">{{ floorError }}</span>
        </div>
      </div>

      <!-- Trạng thái dữ liệu -->
      <p *ngIf="error" class="text-sm text-red-600 mb-4" data-testid="screener-error">{{ error }}</p>
      <p *ngIf="result?.status === 'stale'" class="text-sm text-amber-800 bg-amber-50 border border-amber-200 rounded px-3 py-2 mb-4" data-testid="screener-stale">
        Dữ liệu cũ: lần làm mới gần nhất bị lỗi, đang dùng dữ liệu lúc {{ result?.asOf | date:'dd/MM/yyyy HH:mm' }}.
      </p>
      <p *ngIf="result?.status === 'unavailable'" class="text-sm text-red-700 bg-red-50 border border-red-200 rounded px-3 py-2 mb-4" data-testid="screener-unavailable">
        {{ result?.reason }}
      </p>
      <ul *ngIf="result && result.notes.length > 0" class="text-xs text-gray-600 mb-4 list-disc pl-5" data-testid="screener-notes">
        <li *ngFor="let n of result.notes">{{ n }}</li>
      </ul>

      <ng-container *ngIf="result && result.status !== 'unavailable'">
        <p class="text-xs text-gray-500 mb-2" data-testid="screener-asof">
          {{ result.passed.length }} mã đạt trên {{ result.universeCount }} mã đã quét · dữ liệu lúc {{ result.asOf | date:'dd/MM/yyyy HH:mm' }}
        </p>

        <p *ngIf="result.tradeoffCount > 0" class="text-sm text-gray-700 bg-gray-50 rounded px-3 py-2 mb-4" data-testid="screener-tradeoff">
          {{ result.tradeoffCount }} mã bị loại vì định giá hoặc chất lượng nhưng xu hướng tốt và thanh khoản đạt. Có thể bật "Bỏ qua tầng chất lượng" hoặc nới ROE để xem.
        </p>

        <!-- Danh sách đạt -->
        <div class="bg-white rounded-lg shadow p-4 mb-4">
          <div class="flex flex-wrap items-center justify-between gap-2 mb-3">
            <h2 class="text-lg font-semibold text-gray-700">Mã đạt tiêu chí</h2>
            <div class="flex flex-wrap items-center gap-2">
              <span *ngIf="selected.size >= maxSelection" class="text-xs text-amber-700" data-testid="screener-selection-cap">Chọn tối đa {{ maxSelection }} mã</span>
              <select *ngIf="watchlistList.length > 0" data-testid="screener-watchlist-pick" aria-label="Chọn watchlist để thêm vào"
                class="border border-gray-300 rounded-lg px-2 py-2 text-sm bg-white" (change)="targetWatchlistId = $any($event.target).value">
                <option *ngFor="let w of watchlistList" [value]="w.id" [selected]="w.id === targetWatchlistId">{{ w.emoji }} {{ w.name }}</option>
              </select>
              <button type="button" data-testid="screener-watchlist-add" (click)="addToWatchlist()" [disabled]="selected.size === 0 || !targetWatchlistId || adding"
                class="whitespace-nowrap border border-gray-300 bg-white hover:bg-gray-50 text-gray-700 px-4 py-2 rounded-lg text-sm font-medium disabled:opacity-50">
                ⭐ Thêm {{ selected.size }} mã vào watchlist
              </button>
              <button type="button" data-testid="screener-send" (click)="sendToBoard()" [disabled]="selected.size === 0"
                class="bg-green-600 hover:bg-green-700 text-white px-4 py-2 rounded-lg text-sm font-medium disabled:opacity-50">
                Gửi {{ selected.size }} mã sang Bảng giá nhanh
              </button>
            </div>
          </div>

          <p *ngIf="sendError" class="text-sm text-red-600 mb-2" data-testid="screener-send-error">{{ sendError }}</p>
          <p *ngIf="watchlistList.length === 0" class="text-xs text-gray-600 mb-2" data-testid="screener-watchlist-none">
            {{ watchlistsFailed ? 'Không tải được danh sách watchlist.' : 'Chưa có watchlist nào để thêm vào.' }} <a routerLink="/watchlist" class="text-blue-600 underline">Mở trang Watchlist</a>
          </p>
          <p *ngIf="addResult" class="text-sm text-green-700 mb-2" data-testid="screener-watchlist-result" role="status">{{ addResult }}</p>
          <p *ngIf="addError" class="text-sm text-red-600 mb-2" data-testid="screener-watchlist-error" role="alert">{{ addError }}</p>

          <p *ngIf="result.passed.length === 0" class="text-sm text-gray-500" data-testid="screener-empty">Không có mã nào đạt tiêu chí hiện tại.</p>

          <div *ngIf="result.passed.length > 0" class="overflow-x-auto" data-testid="screener-table">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="text-left text-gray-500 border-b">
                  <th class="py-2 pr-2" data-testid="screener-th"><span class="sr-only">Chọn</span></th>
                  <th class="py-2 pr-3" data-testid="screener-th"><button type="button" data-testid="screener-sort-symbol" (click)="sortBy('symbol')">Mã{{ arrow('symbol') }}</button></th>
                  <th class="py-2 pr-3" data-testid="screener-th">Sàn</th>
                  <th class="py-2 pr-3" data-testid="screener-th">Ngành (ICB)</th>
                  <th class="py-2 pr-3 text-right" data-testid="screener-th"><button type="button" data-testid="screener-sort-adtv" (click)="sortBy('adtvBn')">GTGD TB (tỷ){{ arrow('adtvBn') }}</button></th>
                  <th class="py-2 pr-3 text-right" data-testid="screener-th"><button type="button" data-testid="screener-sort-cap" (click)="sortBy('marketCapBn')">Vốn hoá (tỷ){{ arrow('marketCapBn') }}</button></th>
                  <th class="py-2 pr-3 text-right" data-testid="screener-th"><button type="button" data-testid="screener-sort-roe" (click)="sortBy('roe')">ROE %{{ arrow('roe') }}</button></th>
                  <th class="py-2 pr-3 text-right" data-testid="screener-th"><button type="button" data-testid="screener-sort-pe" (click)="sortBy('pe')">P/E{{ arrow('pe') }}</button></th>
                  <th class="py-2 pr-3 text-right" data-testid="screener-th"><button type="button" data-testid="screener-sort-rs3m" (click)="sortBy('rs3m')">Sức mạnh 3T (P){{ arrow('rs3m') }}</button></th>
                  <th class="py-2" data-testid="screener-th">Cờ biến động và ghi chú</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let r of rows; trackBy: trackSymbol" class="border-b align-top">
                  <td class="py-2 pr-2">
                    <input type="checkbox" data-testid="screener-tick" [attr.data-symbol]="r.symbol" [attr.aria-label]="'Chọn ' + r.symbol"
                      [checked]="selected.has(r.symbol)" (change)="toggle(r.symbol, $event)">
                  </td>
                  <td class="py-2 pr-3 font-semibold"><span data-testid="screener-symbol" [appSymbolLink]="r.symbol">{{ r.symbol }}</span><span *ngIf="r.inWatchlist" data-testid="screener-in-watchlist" [attr.data-symbol]="r.symbol" class="ml-1" title="Đã có trong watchlist" aria-label="Đã có trong watchlist">⭐</span></td>
                  <td class="py-2 pr-3">{{ r.floor }}</td>
                  <td class="py-2 pr-3">{{ r.sector ?? '—' }}</td>
                  <td class="py-2 pr-3 text-right font-mono">{{ r.adtvBn != null ? (r.adtvBn | number:'1.0-1') : '—' }}</td>
                  <td class="py-2 pr-3 text-right font-mono">{{ r.marketCapBn | number:'1.0-0' }}</td>
                  <td class="py-2 pr-3 text-right font-mono">{{ r.roe != null ? (r.roe | number:'1.0-1') : '—' }}</td>
                  <td class="py-2 pr-3 text-right font-mono">{{ r.pe != null ? (r.pe | number:'1.0-1') : '—' }}</td>
                  <td class="py-2 pr-3 text-right font-mono">{{ r.rs3m ?? '—' }}</td>
                  <td class="py-2">
                    <span *ngFor="let f of r.flags" data-testid="screener-flag" class="inline-block text-xs bg-orange-50 text-orange-800 border border-orange-200 rounded px-2 py-0.5 mr-1 mb-1">{{ f }}</span>
                    <div *ngFor="let n of r.notes" data-testid="screener-row-note" class="text-xs text-gray-500">{{ n }}</div>
                    <details class="text-xs text-gray-600">
                      <summary class="cursor-pointer">Vì sao đạt</summary>
                      <ul class="list-disc pl-4"><li *ngFor="let reason of r.reasons">{{ reason }}</li></ul>
                    </details>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- Mã HOSE chưa kiểm được trạng thái -->
        <div *ngIf="result.unverifiedHose.length > 0" class="bg-white rounded-lg shadow p-4 mb-4 border-l-4 border-amber-400" data-testid="screener-unverified">
          <h2 class="text-lg font-semibold text-gray-700 mb-1">Chưa kiểm được trạng thái (HOSE)</h2>
          <p class="text-xs text-gray-600 mb-2">Các mã HOSE này qua mọi tầng nhưng chưa đọc được danh sách diện đặc biệt và hủy niêm yết của HOSE, nên chưa được xếp vào danh sách đạt.</p>
          <div class="flex flex-wrap gap-2">
            <span *ngFor="let r of result.unverifiedHose" class="text-sm font-semibold bg-gray-100 rounded px-2 py-0.5">{{ r.symbol }}</span>
          </div>
        </div>

        <!-- Số mã bị loại -->
        <div class="bg-white rounded-lg shadow p-4 mb-4">
          <h2 class="text-lg font-semibold text-gray-700 mb-2">Mã bị loại theo lý do</h2>
          <ul class="text-sm text-gray-700 grid grid-cols-1 md:grid-cols-2 gap-x-6">
            <li *ngFor="let c of excludedCountList" data-testid="screener-excluded-count">{{ c.reason }}: {{ c.count }}</li>
          </ul>
          <details class="mt-3 text-sm text-gray-600" data-testid="screener-sectors">
            <summary class="cursor-pointer">Theo ngành (ICB): để thấy tiêu chí nghiêng về ngành nào</summary>
            <ul class="mt-1">
              <li *ngFor="let s of sectorList">{{ s.sector }}: {{ s.passed }} đạt, {{ s.excluded }} bị loại</li>
            </ul>
          </details>
        </div>
      </ng-container>

      <!-- Vì sao mã X không có? -->
      <div class="bg-white rounded-lg shadow p-4 mb-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-2">Vì sao mã X không có?</h2>
        <div class="flex gap-3">
          <input type="text" data-testid="screener-lookup-input" [(ngModel)]="lookupSymbol" appUppercase placeholder="Nhập một mã, ví dụ FPT"
            class="flex-1 px-3 py-2 border border-gray-300 rounded-lg" (keyup.enter)="lookup()">
          <button type="button" data-testid="screener-lookup-run" (click)="lookup()"
            class="bg-gray-700 hover:bg-gray-800 text-white px-4 py-2 rounded-lg text-sm font-medium">Tra cứu</button>
        </div>
        <p *ngIf="lookupError" class="text-sm text-red-600 mt-2" data-testid="screener-lookup-error">{{ lookupError }}</p>
        <p *ngIf="lookupResult" class="text-sm text-gray-800 mt-2" data-testid="screener-lookup-result">{{ lookupText(lookupResult) }}</p>
      </div>
    </div>
  `
})
export class ScreenerComponent implements OnInit {
  readonly disclaimer = DISCLAIMER;
  readonly maxSelection = MAX_SELECTION;

  floorHose = true;
  floorHnx = true;
  floorUpcom = false;
  minAdtv: number | null = 5;
  minPrice: number | null = 5000;
  minCap: number | null = 1000;
  minRoe: number | null = 12;
  maxPe: number | null = null;
  minRs3m: number | null = 50;
  skipQuality = false;

  loading = false;
  result: ScreenerResult | null = null;
  error = '';
  floorError = '';

  sortKey: SortKey | null = null;
  sortAsc = true;
  selected = new Set<string>();

  /** Tiêu chí của lần lọc gần nhất đã hiện lên bảng: "Vì sao mã X không có?" giải thích theo đúng danh sách đang thấy, không theo ô vừa sửa. */
  private lastQuery: ScreenerQuery | null = null;
  sendError = '';

  /** Thêm vào watchlist (một nơi cho cả hai nguồn: ô chọn của trang và lệnh định kỳ của agent). */
  watchlistList: WatchlistSummary[] = [];
  watchlistsFailed = false;
  targetWatchlistId = '';
  adding = false;
  addResult = '';
  addError = '';

  lookupSymbol = '';
  lookupResult: ScreenerLookup | null = null;
  lookupError = '';

  constructor(private marketData: MarketDataService, private router: Router, private watchlists: WatchlistService) {}

  ngOnInit(): void {
    this.run();
    this.loadWatchlists();
  }

  private loadWatchlists(): void {
    this.watchlists.getAll().subscribe({
      next: lists => {
        this.watchlistList = lists;
        this.targetWatchlistId = (lists.find(w => w.isDefault) ?? lists[0])?.id ?? '';
      },
      error: () => { this.watchlistsFailed = true; }
    });
  }

  private floors(): string[] {
    const f: string[] = [];
    if (this.floorHose) f.push('HOSE');
    if (this.floorHnx) f.push('HNX');
    if (this.floorUpcom) f.push('UPCOM');
    return f;
  }

  /** Số rỗng/NaN (ô nhập bị xoá) thì không gửi, server dùng mặc định; riêng trần P/E rỗng nghĩa là tắt. */
  private num(v: number | null): number | undefined {
    return typeof v === 'number' && Number.isFinite(v) ? v : undefined;
  }

  private buildQuery(): ScreenerQuery {
    return {
      floors: this.floors().join(','),
      includeUpcom: this.floorUpcom,
      minAdtv: this.num(this.minAdtv), minPrice: this.num(this.minPrice), minCap: this.num(this.minCap),
      minRoe: this.num(this.minRoe), maxPe: this.num(this.maxPe), minRs3m: this.num(this.minRs3m),
      skipQualityLayer: this.skipQuality
    };
  }

  run(): void {
    this.floorError = '';
    if (this.floors().length === 0) {
      this.floorError = 'Chọn ít nhất một sàn.';
      return;
    }
    this.loading = true;
    this.error = '';
    const query = this.buildQuery();
    this.marketData.getScreener(query).subscribe({
      next: res => {
        this.result = res;
        this.lastQuery = query;
        this.sendError = '';
        this.loading = false;
        // Bỏ chọn những mã không còn trong danh sách mới.
        const listed = new Set(res.passed.map(r => r.symbol));
        this.selected = new Set([...this.selected].filter(s => listed.has(s)));
      },
      error: () => {
        this.result = null;
        this.loading = false;
        this.error = 'Không tải được danh sách lọc, thử lại sau.';
      }
    });
  }

  // --- Sắp xếp: mặc định giữ thứ tự server (bảng chữ cái), bấm cột để đổi, bấm lại để đảo; ô trống luôn xuống cuối ---

  get rows(): ScreenerPassedRow[] {
    const list = this.result?.passed ?? [];
    const key = this.sortKey;
    if (!key) return list;
    const dir = this.sortAsc ? 1 : -1;
    return [...list].sort((a, b) => {
      const x = a[key] as string | number | null | undefined;
      const y = b[key] as string | number | null | undefined;
      const xe = x === null || x === undefined;
      const ye = y === null || y === undefined;
      if (xe || ye) return xe === ye ? 0 : xe ? 1 : -1;   // ô trống xuống cuối, bất kể chiều
      const cmp = typeof x === 'string' ? x.localeCompare(y as string) : (x as number) - (y as number);
      return cmp * dir;
    });
  }

  sortBy(key: SortKey): void {
    if (this.sortKey === key) this.sortAsc = !this.sortAsc;
    else { this.sortKey = key; this.sortAsc = true; }
  }

  arrow(key: SortKey): string {
    return this.sortKey === key ? (this.sortAsc ? ' ▲' : ' ▼') : '';
  }

  trackSymbol = (_: number, r: ScreenerPassedRow) => r.symbol;

  get excludedCountList(): { reason: string; count: number }[] {
    return Object.entries(this.result?.excludedCounts ?? {})
      .map(([reason, count]) => ({ reason, count }))
      .sort((a, b) => b.count - a.count || a.reason.localeCompare(b.reason));
  }

  get sectorList(): { sector: string; passed: number; excluded: number }[] {
    return Object.entries(this.result?.sectorCounts ?? {}).map(([sector, c]) => ({ sector, passed: c.passed, excluded: c.excluded }));
  }

  // --- Chọn mã rồi gửi sang Bảng giá nhanh (không tự điền gì) ---

  /** Ngưỡng xếp hạng đang hiện trong gợi ý: ô trống thì dùng mặc định P50 (đúng như server). */
  get rankFloor(): number {
    return this.num(this.minRs3m) ?? 50;
  }

  toggle(symbol: string, event: Event): void {
    const box = event.target as HTMLInputElement;
    if (box.checked) {
      if (this.selected.size >= MAX_SELECTION) {
        box.checked = false;   // vượt trần: bỏ dấu chọn vừa đánh
        return;
      }
      this.selected = new Set(this.selected).add(symbol);
    } else {
      const next = new Set(this.selected);
      next.delete(symbol);
      this.selected = next;
    }
  }

  sendToBoard(): void {
    const symbols = [...this.selected].sort((a, b) => a.localeCompare(b));
    if (symbols.length === 0) return;
    if (!saveQuickBoardSymbols(symbols)) {
      // Lưu trữ bị chặn (chế độ riêng tư): danh sách không sang được Bảng giá nhanh, nên nói rõ và để người dùng tự gõ, không chuyển trang.
      this.sendError = `Trình duyệt đang chặn lưu trữ nên không chuyển được danh sách sang Bảng giá nhanh. Hãy gõ các mã này vào ô Bảng giá nhanh: ${symbols.join(',')}`;
      return;
    }
    this.sendError = '';
    this.router.navigate(['/market-data']);
  }

  /** Ghi chú tự điền, ngắn: "Từ sàng lọc 28/11 · ROE ≥ 12 · Sức mạnh 3T ≥ P50". Dùng đúng tiêu chí của lần lọc đang hiện trên bảng. */
  private autoNote(): string {
    const d = new Date();
    const dd = `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
    const qy = this.lastQuery ?? this.buildQuery();
    const parts = [`Từ sàng lọc ${dd}`];
    if (qy.minRoe != null) parts.push(`ROE ≥ ${qy.minRoe}`);
    if (qy.minRs3m != null) parts.push(`Sức mạnh 3T ≥ P${qy.minRs3m}`);
    if (qy.maxPe != null) parts.push(`P/E ≤ ${qy.maxPe}`);
    return parts.join(' · ').slice(0, 200);
  }

  addToWatchlist(): void {
    if (this.adding || this.selected.size === 0 || !this.targetWatchlistId) return;
    const symbols = [...this.selected].sort((a, b) => a.localeCompare(b));
    this.adding = true;
    this.addResult = '';
    this.addError = '';
    this.watchlists.addItemsBulk(this.targetWatchlistId, symbols, this.autoNote()).subscribe({
      next: res => {
        this.adding = false;
        this.addResult = `Đã thêm ${res.added.length}, bỏ qua ${res.skipped.length} (đã có)`;
        const inList = new Set([...res.added, ...res.skipped]);
        for (const r of this.result?.passed ?? []) if (inList.has(r.symbol)) r.inWatchlist = true;
        this.selected = new Set([...this.selected].filter(s => !symbols.includes(s)));
      },
      error: () => {
        this.adding = false;
        this.addError = 'Không thêm được vào watchlist, thử lại sau.';   // giữ nguyên các mã đã chọn để thử lại
      }
    });
  }

  // --- Vì sao mã X không có? ---

  lookup(): void {
    const symbol = (this.lookupSymbol ?? '').trim().toUpperCase();
    if (!symbol) return;
    this.lookupError = '';
    this.marketData.getScreener({ ...(this.lastQuery ?? this.buildQuery()), lookupSymbol: symbol }).subscribe({
      next: res => { this.lookupResult = res.lookup ?? null; },
      error: () => { this.lookupResult = null; this.lookupError = 'Không tra cứu được, thử lại sau.'; }
    });
  }

  lookupText(l: ScreenerLookup): string {
    switch (l.state) {
      case 'passed': return `${l.symbol}: Có trong danh sách đạt tiêu chí.`;
      case 'unverified_hose': return `${l.symbol}: mã HOSE đạt tiêu chí nhưng chưa kiểm được trạng thái (HOSE), nên chưa xếp vào danh sách đạt.`;
      case 'excluded': return `${l.symbol}: Bị loại. ${l.reason ?? ''}`.trim();
      default: return `${l.symbol}: ${l.reason ?? 'Mã không có trong dữ liệu sàng lọc.'}`;
    }
  }
}
