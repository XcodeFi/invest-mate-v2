import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TradeService, BulkTradeItem, BulkCreateResult } from '../../../core/services/trade.service';
import { PortfolioService, PortfolioSummary } from '../../../core/services/portfolio.service';
import { NotificationService } from '../../../core/services/notification.service';
import { VndCurrencyPipe } from '../../../shared/pipes/vnd-currency.pipe';

interface PreviewRow extends BulkTradeItem {
  rowNum: number;
  error?: string;
  totalValue?: number;
}

@Component({
  selector: 'app-trade-import',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, VndCurrencyPipe],
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
              <h1 class="text-3xl font-bold text-gray-900">Import Giao dich</h1>
              <p class="text-gray-600 mt-1">Nhap giao dich tu file CSV</p>
            </div>
          </div>
        </div>
      </div>

      <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <!-- Step 1: Select portfolio + upload -->
        <div class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
          <h2 class="text-lg font-semibold text-gray-800 mb-4">1. Chon danh muc va file CSV</h2>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">Danh muc <span class="text-red-500">*</span></label>
              <select [(ngModel)]="selectedPortfolioId"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
                <option value="">-- Chon danh muc --</option>
                <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
              </select>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 mb-1">File CSV</label>
              <input type="file" accept=".csv,.txt" (change)="onFileSelected($event)"
                class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 file:mr-4 file:py-1 file:px-3 file:rounded file:border-0 file:text-sm file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100" />
            </div>
          </div>

          <!-- CSV Format Guide -->
          <div class="bg-blue-50 border border-blue-200 rounded-lg p-4 text-sm">
            <p class="font-medium text-blue-800 mb-2">Dinh dang CSV:</p>
            <code class="text-blue-700 block bg-blue-100 rounded p-2 text-xs">
              symbol,tradeType,quantity,price,fee,tax,tradeDate<br>
              VNM,BUY,1000,85000,0,0,2024-03-01<br>
              FPT,SELL,500,120000,0,0,2024-03-05
            </code>
            <p class="mt-2 text-blue-600">tradeType: BUY hoac SELL | tradeDate: YYYY-MM-DD (tuy chon) | fee, tax: so (tuy chon, mac dinh 0)</p>
          </div>
        </div>

        <!-- Step 2: Preview -->
        <div *ngIf="previewRows.length > 0" class="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-gray-800">2. Xem truoc ({{ previewRows.length }} dong)</h2>
            <div class="text-sm">
              <span class="text-green-600 font-medium">{{ validCount }} hop le</span>
              <span *ngIf="errorCount > 0" class="text-red-600 font-medium ml-3">{{ errorCount }} loi</span>
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="min-w-full divide-y divide-gray-200 text-sm">
              <thead class="bg-gray-50">
                <tr>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">#</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">Ma CK</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">Loai</th>
                  <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">So luong</th>
                  <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Gia</th>
                  <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Tong</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">Ngay</th>
                  <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">Trang thai</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-gray-200">
                <tr *ngFor="let row of previewRows" [class.bg-red-50]="row.error">
                  <td class="px-3 py-2 text-gray-500">{{ row.rowNum }}</td>
                  <td class="px-3 py-2 font-medium">{{ row.symbol }}</td>
                  <td class="px-3 py-2">
                    <span class="px-2 py-0.5 rounded-full text-xs font-semibold"
                      [class.bg-green-100]="row.tradeType === 'BUY'" [class.text-green-700]="row.tradeType === 'BUY'"
                      [class.bg-red-100]="row.tradeType === 'SELL'" [class.text-red-700]="row.tradeType === 'SELL'">
                      {{ row.tradeType }}
                    </span>
                  </td>
                  <td class="px-3 py-2 text-right">{{ row.quantity | number:'1.0-0' }}</td>
                  <td class="px-3 py-2 text-right">{{ row.price | number:'1.0-0' }}</td>
                  <td class="px-3 py-2 text-right">{{ row.totalValue | vndCurrency }}</td>
                  <td class="px-3 py-2 text-gray-500">{{ row.tradeDate || 'Hom nay' }}</td>
                  <td class="px-3 py-2">
                    <span *ngIf="!row.error" class="text-green-600 text-xs">OK</span>
                    <span *ngIf="row.error" class="text-red-600 text-xs">{{ row.error }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="flex justify-end gap-3 mt-4 pt-4 border-t border-gray-200">
            <button (click)="clearPreview()" class="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 text-sm">
              Huy
            </button>
            <button (click)="importTrades()" [disabled]="importing || validCount === 0 || !selectedPortfolioId"
              class="px-6 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium text-sm disabled:opacity-50">
              {{ importing ? 'Dang import...' : 'Import ' + validCount + ' giao dich' }}
            </button>
          </div>
        </div>

        <!-- Step 3: Result -->
        <div *ngIf="importResult" class="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
          <h2 class="text-lg font-semibold text-gray-800 mb-4">3. Ket qua</h2>
          <div class="grid grid-cols-2 gap-4 mb-4">
            <div class="bg-green-50 rounded-lg p-4 text-center">
              <div class="text-2xl font-bold text-green-600">{{ importResult.successCount }}</div>
              <div class="text-sm text-green-700">Thanh cong</div>
            </div>
            <div class="bg-red-50 rounded-lg p-4 text-center">
              <div class="text-2xl font-bold text-red-600">{{ importResult.failedCount }}</div>
              <div class="text-sm text-red-700">That bai</div>
            </div>
          </div>
          <div *ngIf="importResult.errors.length > 0" class="bg-red-50 border border-red-200 rounded-lg p-3 mb-4">
            <p class="text-sm font-medium text-red-800 mb-1">Chi tiet loi:</p>
            <ul class="list-disc list-inside text-sm text-red-700">
              <li *ngFor="let err of importResult.errors">{{ err }}</li>
            </ul>
          </div>
          <button routerLink="/trades" class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm">
            Ve trang giao dich
          </button>
        </div>
      </div>
    </div>
  `
})
export class TradeImportComponent implements OnInit {
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  previewRows: PreviewRow[] = [];
  importing = false;
  importResult: BulkCreateResult | null = null;

  get validCount(): number { return this.previewRows.filter(r => !r.error).length; }
  get errorCount(): number { return this.previewRows.filter(r => r.error).length; }

  constructor(
    private tradeService: TradeService,
    private portfolioService: PortfolioService,
    private notificationService: NotificationService
  ) {}

  ngOnInit(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => this.portfolios = data,
      error: () => {}
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      const text = reader.result as string;
      this.parseCSV(text);
    };
    reader.readAsText(file);
  }

  parseCSV(text: string): void {
    const lines = text.split(/\r?\n/).filter(l => l.trim());
    this.previewRows = [];

    // Detect header
    const firstLine = lines[0].toLowerCase();
    const hasHeader = firstLine.includes('symbol') || firstLine.includes('tradetype') || firstLine.includes('ma');
    const dataLines = hasHeader ? lines.slice(1) : lines;

    dataLines.forEach((line, i) => {
      const cols = line.split(/[,;\t]/).map(c => c.trim());
      if (cols.length < 4) return;

      const symbol = (cols[0] || '').toUpperCase();
      const tradeType = (cols[1] || '').toUpperCase();
      const quantity = parseFloat(cols[2]) || 0;
      const price = parseFloat(cols[3]) || 0;
      const fee = parseFloat(cols[4]) || 0;
      const tax = parseFloat(cols[5]) || 0;
      const tradeDate = cols[6] || '';

      const row: PreviewRow = {
        rowNum: i + 1 + (hasHeader ? 1 : 0),
        symbol, tradeType, quantity, price, fee, tax, tradeDate,
        totalValue: quantity * price
      };

      // Validate
      if (!symbol) row.error = 'Thieu ma CK';
      else if (tradeType !== 'BUY' && tradeType !== 'SELL') row.error = 'Loai phai la BUY hoac SELL';
      else if (quantity <= 0) row.error = 'So luong phai > 0';
      else if (price <= 0) row.error = 'Gia phai > 0';

      this.previewRows.push(row);
    });
  }

  clearPreview(): void {
    this.previewRows = [];
    this.importResult = null;
  }

  importTrades(): void {
    if (!this.selectedPortfolioId || this.validCount === 0) return;
    this.importing = true;

    const validTrades: BulkTradeItem[] = this.previewRows
      .filter(r => !r.error)
      .map(r => ({
        symbol: r.symbol,
        tradeType: r.tradeType,
        quantity: r.quantity,
        price: r.price,
        fee: r.fee,
        tax: r.tax,
        tradeDate: r.tradeDate || undefined
      }));

    this.tradeService.bulkCreate(this.selectedPortfolioId, validTrades).subscribe({
      next: (result) => {
        this.importResult = result;
        this.importing = false;
        this.previewRows = [];
        if (result.successCount > 0) {
          this.notificationService.success('Import', `Da import ${result.successCount} giao dich`);
        }
      },
      error: (err) => {
        this.importing = false;
        this.notificationService.error('Loi', 'Khong the import giao dich');
      }
    });
  }
}
