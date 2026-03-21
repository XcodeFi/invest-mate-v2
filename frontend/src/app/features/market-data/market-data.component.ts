import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  MarketDataService, StockPrice, StockDetail, MarketOverview,
  TopFluctuation, TradingHistorySummary, StockSearchResult, TechnicalAnalysis
} from '../../core/services/market-data.service';
import { NotificationService } from '../../core/services/notification.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { UppercaseDirective } from '../../shared/directives/uppercase.directive';
import { AiChatPanelComponent } from '../../shared/components/ai-chat-panel/ai-chat-panel.component';

@Component({
  selector: 'app-market-data',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, VndCurrencyPipe, UppercaseDirective, AiChatPanelComponent],
  template: `
    <div class="container mx-auto px-4 py-6">
      <h1 class="text-2xl font-bold text-gray-800 mb-6">Dữ liệu Thị trường</h1>

      <!-- Market Indexes (Overview) -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <div *ngFor="let idx of marketOverview"
          class="bg-white rounded-lg shadow p-4 border-l-4 cursor-pointer hover:shadow-md transition-shadow"
          [class.border-green-500]="idx.change >= 0"
          [class.border-red-500]="idx.change < 0"
          (click)="selectIndex(idx.symbol)">
          <div class="flex justify-between items-start mb-1">
            <span class="text-sm font-semibold text-gray-700">{{ idx.symbol }}</span>
            <span class="text-xs px-2 py-0.5 rounded-full"
              [class.bg-green-100]="idx.change >= 0" [class.text-green-700]="idx.change >= 0"
              [class.bg-red-100]="idx.change < 0" [class.text-red-700]="idx.change < 0">
              {{ idx.changePercent >= 0 ? '+' : '' }}{{ idx.changePercent.toFixed(2) }}%
            </span>
          </div>
          <div class="text-2xl font-bold" [class.text-green-600]="idx.change >= 0" [class.text-red-600]="idx.change < 0">
            {{ formatNumber(idx.price) }}
          </div>
          <div class="flex items-center gap-1 mt-1">
            <span class="text-sm font-medium" [class.text-green-600]="idx.change >= 0" [class.text-red-600]="idx.change < 0">
              {{ idx.change >= 0 ? '+' : '' }}{{ formatNumber(idx.change) }}
            </span>
          </div>
          <div class="grid grid-cols-2 gap-x-3 mt-2 text-xs text-gray-500">
            <div>KL: {{ formatVolume(idx.totalVolume) }}</div>
            <div>GT: {{ formatBillion(idx.totalValue) }}</div>
            <div class="text-green-600">NN Mua: {{ formatBillion(idx.foreignBuyValue) }}</div>
            <div class="text-red-600">NN Bán: {{ formatBillion(idx.foreignSellValue) }}</div>
          </div>
        </div>
      </div>
      <div *ngIf="loadingOverview" class="text-center text-gray-400 mb-8">Đang tải chỉ số...</div>

      <!-- Stock Lookup (Detail) -->
      <div class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-4">Tra cứu cổ phiếu</h2>
        <div class="flex gap-3 mb-4">
          <div class="relative flex-1">
            <input type="text" [(ngModel)]="searchSymbol" appUppercase
              (keyup.enter)="lookupStock()"
              (input)="onSearchInput()"
              placeholder="Nhập mã CP (VD: VNM, FPT, VCB...)"
              class="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent">
            <!-- Search suggestions -->
            <div *ngIf="searchResults.length > 0" class="absolute z-10 w-full mt-1 bg-white border rounded-lg shadow-lg max-h-60 overflow-y-auto">
              <div *ngFor="let r of searchResults"
                (click)="selectSearchResult(r)"
                class="px-4 py-2 hover:bg-blue-50 cursor-pointer flex items-center gap-3 border-b last:border-b-0">
                <span class="font-bold text-blue-600 w-16">{{ r.symbol }}</span>
                <span class="text-sm text-gray-600 truncate">{{ r.companyName }}</span>
                <span class="text-xs text-gray-400 ml-auto">{{ r.exchange }}</span>
              </div>
            </div>
          </div>
          <button (click)="lookupStock()" [disabled]="loadingDetail"
            class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
            {{ loadingDetail ? 'Đang tải...' : 'Tra cứu' }}
          </button>
        </div>

        <!-- Stock Detail Result -->
        <div *ngIf="stockDetail" class="border rounded-lg p-5">
          <div class="flex justify-between items-start mb-4">
            <div>
              <div class="flex items-center gap-3">
                <span class="text-2xl font-bold text-gray-800">{{ stockDetail.symbol }}</span>
                <span class="text-sm px-2 py-0.5 rounded bg-gray-100 text-gray-600">{{ stockDetail.exchange }}</span>
              </div>
              <div class="text-sm text-gray-500 mt-1">{{ stockDetail.companyName }}</div>
            </div>
            <div class="text-right">
              <div class="text-3xl font-bold" [class.text-green-600]="stockDetail.change >= 0" [class.text-red-600]="stockDetail.change < 0">
                {{ stockDetail.price | vndCurrency }}
              </div>
              <div class="text-sm font-medium mt-1" [class.text-green-600]="stockDetail.change >= 0" [class.text-red-600]="stockDetail.change < 0">
                {{ stockDetail.change >= 0 ? '+' : '' }}{{ stockDetail.change | vndCurrency }}
                ({{ stockDetail.changePercent >= 0 ? '+' : '' }}{{ stockDetail.changePercent.toFixed(2) }}%)
              </div>
            </div>
          </div>

          <!-- Price Grid -->
          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3 mb-4">
            <div class="bg-gray-50 rounded p-2">
              <div class="text-xs text-gray-500">Tham chiếu</div>
              <div class="font-semibold text-yellow-600">{{ stockDetail.referencePrice | vndCurrency }}</div>
            </div>
            <div class="bg-gray-50 rounded p-2">
              <div class="text-xs text-gray-500">Mở cửa</div>
              <div class="font-semibold">{{ stockDetail.openPrice | vndCurrency }}</div>
            </div>
            <div class="bg-gray-50 rounded p-2">
              <div class="text-xs text-gray-500">Cao nhất</div>
              <div class="font-semibold text-green-600">{{ stockDetail.highPrice | vndCurrency }}</div>
            </div>
            <div class="bg-gray-50 rounded p-2">
              <div class="text-xs text-gray-500">Thấp nhất</div>
              <div class="font-semibold text-red-600">{{ stockDetail.lowPrice | vndCurrency }}</div>
            </div>
            <div class="bg-gray-50 rounded p-2">
              <div class="text-xs text-gray-500">Trần</div>
              <div class="font-semibold text-purple-600">{{ stockDetail.ceilingPrice | vndCurrency }}</div>
            </div>
            <div class="bg-gray-50 rounded p-2">
              <div class="text-xs text-gray-500">Sàn</div>
              <div class="font-semibold text-cyan-600">{{ stockDetail.floorPrice | vndCurrency }}</div>
            </div>
            <div class="bg-gray-50 rounded p-2">
              <div class="text-xs text-gray-500">TB</div>
              <div class="font-semibold">{{ stockDetail.averagePrice | vndCurrency }}</div>
            </div>
            <div class="bg-gray-50 rounded p-2">
              <div class="text-xs text-gray-500">Khối lượng</div>
              <div class="font-semibold">{{ formatVolume(stockDetail.volume) }}</div>
            </div>
          </div>

          <!-- Order Book -->
          <div class="grid grid-cols-2 gap-4 mb-4">
            <div>
              <div class="text-xs font-medium text-green-700 mb-1">Dư mua (Bid)</div>
              <div *ngFor="let bid of stockDetail.bids; let i = index" class="flex justify-between text-sm py-0.5">
                <span class="text-green-600 font-medium">{{ bid.price | vndCurrency }}</span>
                <span class="text-gray-600">{{ formatVolume(bid.volume) }}</span>
              </div>
            </div>
            <div>
              <div class="text-xs font-medium text-red-700 mb-1">Dư bán (Ask)</div>
              <div *ngFor="let ask of stockDetail.asks; let i = index" class="flex justify-between text-sm py-0.5">
                <span class="text-red-600 font-medium">{{ ask.price | vndCurrency }}</span>
                <span class="text-gray-600">{{ formatVolume(ask.volume) }}</span>
              </div>
            </div>
          </div>

          <!-- Foreign Trading -->
          <div class="flex gap-4 text-sm border-t pt-3">
            <div><span class="text-gray-500">NN Mua:</span> <span class="text-green-600 font-medium">{{ formatVolume(stockDetail.foreignBuyVolume) }}</span></div>
            <div><span class="text-gray-500">NN Bán:</span> <span class="text-red-600 font-medium">{{ formatVolume(stockDetail.foreignSellVolume) }}</span></div>
            <div><span class="text-gray-500">Room NN:</span> <span class="font-medium">{{ formatVolume(stockDetail.foreignRoom) }}</span></div>
          </div>

          <!-- Trading Summary -->
          <div *ngIf="tradingSummary" class="border-t pt-3 mt-3">
            <div class="text-xs font-medium text-gray-500 mb-2">Biến động giá</div>
            <div class="flex flex-wrap gap-3">
              <span *ngFor="let item of summaryItems" class="text-sm">
                <span class="text-gray-500">{{ item.label }}:</span>
                <span class="font-medium ml-1" [class.text-green-600]="item.value >= 0" [class.text-red-600]="item.value < 0">
                  {{ item.value >= 0 ? '+' : '' }}{{ item.value.toFixed(2) }}%
                </span>
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Technical Analysis Section -->
      <div *ngIf="analysis" class="border rounded-lg p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <div class="flex items-center gap-2">
            <span class="text-lg">🤖</span>
            <h3 class="font-bold text-gray-900">Phân tích kỹ thuật: {{ analysis.symbol }}</h3>
          </div>
          <span class="text-xs px-3 py-1.5 rounded-full font-bold"
            [class.bg-green-100]="analysis.overallSignal === 'strong_buy' || analysis.overallSignal === 'buy'"
            [class.text-green-700]="analysis.overallSignal === 'strong_buy' || analysis.overallSignal === 'buy'"
            [class.bg-red-100]="analysis.overallSignal === 'strong_sell' || analysis.overallSignal === 'sell'"
            [class.text-red-700]="analysis.overallSignal === 'strong_sell' || analysis.overallSignal === 'sell'"
            [class.bg-amber-100]="analysis.overallSignal === 'hold'"
            [class.text-amber-700]="analysis.overallSignal === 'hold'">
            {{ getSignalEmoji(analysis.overallSignal) }} {{ analysis.overallSignalVi }}
          </span>
        </div>

        <!-- Indicators Grid -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-4">
          <!-- EMA -->
          <div class="bg-gray-50 rounded-lg p-3">
            <div class="text-xs text-gray-500 font-medium mb-1">📊 Xu hướng (EMA20/50)</div>
            <div *ngIf="analysis.ema20 && analysis.ema50" class="text-sm">
              <div>EMA20: <span class="font-mono font-medium">{{ analysis.ema20 | vndCurrency }}</span></div>
              <div>EMA50: <span class="font-mono font-medium">{{ analysis.ema50 | vndCurrency }}</span></div>
              <div class="mt-1 text-xs font-medium"
                [class.text-green-600]="analysis.emaTrend === 'bullish'"
                [class.text-red-600]="analysis.emaTrend === 'bearish'">
                {{ analysis.emaTrend === 'bullish' ? '✅ Xu hướng TĂNG' : '❌ Xu hướng GIẢM' }}
              </div>
            </div>
            <div *ngIf="!analysis.ema20" class="text-xs text-gray-400">Không đủ dữ liệu</div>
          </div>

          <!-- RSI -->
          <div class="bg-gray-50 rounded-lg p-3">
            <div class="text-xs text-gray-500 font-medium mb-1">📈 RSI (14)</div>
            <div *ngIf="analysis.rsi14" class="text-sm">
              <div class="text-2xl font-bold"
                [class.text-green-600]="analysis.rsiSignal === 'oversold'"
                [class.text-red-600]="analysis.rsiSignal === 'overbought'"
                [class.text-gray-700]="analysis.rsiSignal === 'neutral'">
                {{ analysis.rsi14 | number:'1.1-1' }}
              </div>
              <div class="text-xs font-medium mt-1"
                [class.text-green-600]="analysis.rsiSignal === 'oversold'"
                [class.text-red-600]="analysis.rsiSignal === 'overbought'">
                {{ analysis.rsiSignal === 'oversold' ? '🟢 Quá bán' : analysis.rsiSignal === 'overbought' ? '🔴 Quá mua' : '🟡 Trung tính' }}
              </div>
            </div>
          </div>

          <!-- MACD -->
          <div class="bg-gray-50 rounded-lg p-3">
            <div class="text-xs text-gray-500 font-medium mb-1">📉 MACD (12,26,9)</div>
            <div *ngIf="analysis.macdLine !== null && analysis.macdLine !== undefined" class="text-sm">
              <div>MACD: <span class="font-mono font-medium">{{ analysis.macdLine | number:'1.0-0' }}</span></div>
              <div>Signal: <span class="font-mono font-medium">{{ analysis.signalLine | number:'1.0-0' }}</span></div>
              <div class="mt-1 text-xs font-medium"
                [class.text-green-600]="analysis.macdSignal === 'buy'"
                [class.text-red-600]="analysis.macdSignal === 'sell'">
                {{ analysis.macdSignal === 'buy' ? '✅ Tín hiệu MUA' : analysis.macdSignal === 'sell' ? '❌ Tín hiệu BÁN' : '🟡 Trung tính' }}
              </div>
            </div>
          </div>

          <!-- Volume -->
          <div class="bg-gray-50 rounded-lg p-3">
            <div class="text-xs text-gray-500 font-medium mb-1">📊 Khối lượng</div>
            <div *ngIf="analysis.volumeRatio" class="text-sm">
              <div>KL/TB20: <span class="font-bold">{{ analysis.volumeRatio | number:'1.1-1' }}x</span></div>
              <div class="mt-1 text-xs font-medium"
                [class.text-green-600]="analysis.volumeSignal === 'spike' || analysis.volumeSignal === 'high'"
                [class.text-amber-600]="analysis.volumeSignal === 'normal'"
                [class.text-gray-500]="analysis.volumeSignal === 'low'">
                {{ analysis.volumeSignal === 'spike' ? '🔥 Đột biến' : analysis.volumeSignal === 'high' ? '📈 Cao' : analysis.volumeSignal === 'low' ? '📉 Thấp' : '🟡 Bình thường' }}
              </div>
            </div>
          </div>
        </div>

        <!-- Support / Resistance -->
        <div *ngIf="analysis.supportLevels.length > 0 || analysis.resistanceLevels.length > 0"
          class="grid grid-cols-2 gap-3 mb-4">
          <div class="bg-green-50 rounded-lg p-3">
            <div class="text-xs text-green-600 font-medium mb-1">Hỗ trợ</div>
            <div class="flex flex-wrap gap-2">
              <span *ngFor="let s of analysis.supportLevels"
                class="text-sm font-mono font-medium text-green-700 bg-green-100 px-2 py-0.5 rounded">
                {{ s | vndCurrency }}
              </span>
              <span *ngIf="analysis.supportLevels.length === 0" class="text-xs text-gray-400">Chưa xác định</span>
            </div>
          </div>
          <div class="bg-red-50 rounded-lg p-3">
            <div class="text-xs text-red-600 font-medium mb-1">Kháng cự</div>
            <div class="flex flex-wrap gap-2">
              <span *ngFor="let r of analysis.resistanceLevels"
                class="text-sm font-mono font-medium text-red-700 bg-red-100 px-2 py-0.5 rounded">
                {{ r | vndCurrency }}
              </span>
              <span *ngIf="analysis.resistanceLevels.length === 0" class="text-xs text-gray-400">Chưa xác định</span>
            </div>
          </div>
        </div>

        <!-- Trade Suggestion -->
        <div *ngIf="analysis.suggestedEntry" class="bg-blue-50 rounded-lg p-4">
          <div class="text-xs text-blue-600 font-medium mb-2">💡 Gợi ý giao dịch</div>
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm mb-3">
            <div>
              <span class="text-gray-500">Entry:</span>
              <span class="font-mono font-medium ml-1">{{ analysis.suggestedEntry | vndCurrency }}</span>
            </div>
            <div>
              <span class="text-gray-500">Stop-loss:</span>
              <span class="font-mono font-medium text-red-600 ml-1">{{ analysis.suggestedStopLoss | vndCurrency }}</span>
            </div>
            <div>
              <span class="text-gray-500">Mục tiêu:</span>
              <span class="font-mono font-medium text-green-600 ml-1">{{ analysis.suggestedTarget | vndCurrency }}</span>
            </div>
            <div>
              <span class="text-gray-500">R:R:</span>
              <span class="font-bold ml-1" [class.text-green-600]="(analysis.riskRewardRatio || 0) >= 2"
                [class.text-amber-600]="(analysis.riskRewardRatio || 0) < 2">
                1:{{ analysis.riskRewardRatio | number:'1.1-1' }}
              </span>
            </div>
          </div>
          <div class="flex gap-2">
            <a [routerLink]="'/trade-plan'"
              [queryParams]="{ symbol: analysis.symbol, entry: analysis.suggestedEntry, sl: analysis.suggestedStopLoss, tp: analysis.suggestedTarget }"
              class="inline-flex items-center gap-1.5 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 transition-colors">
              📋 Tạo Trade Plan từ gợi ý
            </a>
            <button (click)="openAiEvaluation()"
              class="inline-flex items-center gap-1.5 px-4 py-2 bg-purple-600 text-white text-sm font-medium rounded-lg hover:bg-purple-700 transition-colors">
              ✨ AI Đánh giá
            </button>
          </div>
        </div>
      </div>

      <!-- Analyzing spinner -->
      <div *ngIf="analyzingSignal" class="border rounded-lg p-5 mb-6 text-center">
        <div class="animate-spin w-6 h-6 border-3 border-blue-600 border-t-transparent rounded-full mx-auto"></div>
        <p class="text-sm text-gray-500 mt-2">Đang phân tích kỹ thuật...</p>
      </div>

      <!-- Top Fluctuation -->
      <div class="bg-white rounded-lg shadow p-6 mb-6">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-lg font-semibold text-gray-700">Top biến động</h2>
          <div class="flex gap-1">
            <button *ngFor="let f of floors" (click)="loadTopFluctuation(f.code)"
              class="px-3 py-1 rounded text-xs font-medium transition-colors"
              [class.bg-blue-600]="selectedFloor === f.code" [class.text-white]="selectedFloor === f.code"
              [class.bg-gray-100]="selectedFloor !== f.code" [class.text-gray-600]="selectedFloor !== f.code">
              {{ f.label }}
            </button>
          </div>
        </div>
        <div *ngIf="topFluctuations.length > 0" class="overflow-x-auto hidden md:block">
          <table class="min-w-full table-auto">
            <thead>
              <tr class="bg-gray-50 border-b">
                <th class="px-3 py-2 text-left text-xs font-medium text-gray-500">Mã</th>
                <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Giá</th>
                <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">+/-</th>
                <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">%</th>
                <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">KL</th>
                <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Trần</th>
                <th class="px-3 py-2 text-right text-xs font-medium text-gray-500">Sàn</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let s of topFluctuations" class="border-b hover:bg-gray-50 cursor-pointer" (click)="searchSymbol = s.symbol; lookupStock()">
                <td class="px-3 py-2 text-sm">
                  <span class="font-bold text-gray-800">{{ s.symbol }}</span>
                  <span *ngIf="s.shortName" class="text-xs text-gray-400 ml-1">{{ s.shortName }}</span>
                </td>
                <td class="px-3 py-2 text-sm text-right font-semibold" [class.text-green-600]="s.change >= 0" [class.text-red-600]="s.change < 0">
                  {{ s.price | vndCurrency }}
                </td>
                <td class="px-3 py-2 text-sm text-right" [class.text-green-600]="s.change >= 0" [class.text-red-600]="s.change < 0">
                  {{ s.change >= 0 ? '+' : '' }}{{ s.change | vndCurrency }}
                </td>
                <td class="px-3 py-2 text-sm text-right font-medium" [class.text-green-600]="s.changePercent >= 0" [class.text-red-600]="s.changePercent < 0">
                  {{ s.changePercent >= 0 ? '+' : '' }}{{ s.changePercent.toFixed(2) }}%
                </td>
                <td class="px-3 py-2 text-sm text-right text-gray-600">{{ formatVolume(s.volume) }}</td>
                <td class="px-3 py-2 text-sm text-right text-purple-600">{{ s.ceilingPrice | vndCurrency }}</td>
                <td class="px-3 py-2 text-sm text-right text-cyan-600">{{ s.floorPrice | vndCurrency }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <!-- Mobile cards for Top Fluctuation -->
        <div *ngIf="topFluctuations.length > 0" class="md:hidden divide-y divide-gray-200">
          <div *ngFor="let s of topFluctuations" class="p-4 space-y-2 cursor-pointer active:bg-gray-50" (click)="searchSymbol = s.symbol; lookupStock()">
            <div class="flex items-center justify-between">
              <div>
                <span class="font-bold text-gray-800">{{ s.symbol }}</span>
                <span *ngIf="s.shortName" class="text-xs text-gray-400 ml-1">{{ s.shortName }}</span>
              </div>
              <span class="font-semibold" [class.text-green-600]="s.change >= 0" [class.text-red-600]="s.change < 0">
                {{ s.price | vndCurrency }}
              </span>
            </div>
            <div class="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
              <div><span class="text-gray-500">+/-:</span> <span class="font-medium" [class.text-green-600]="s.change >= 0" [class.text-red-600]="s.change < 0">{{ s.change >= 0 ? '+' : '' }}{{ s.change | vndCurrency }}</span></div>
              <div><span class="text-gray-500">%:</span> <span class="font-medium" [class.text-green-600]="s.changePercent >= 0" [class.text-red-600]="s.changePercent < 0">{{ s.changePercent >= 0 ? '+' : '' }}{{ s.changePercent.toFixed(2) }}%</span></div>
              <div><span class="text-gray-500">KL:</span> <span class="font-medium text-gray-600">{{ formatVolume(s.volume) }}</span></div>
            </div>
          </div>
        </div>
        <div *ngIf="loadingTop" class="text-center text-gray-400 py-4">Đang tải...</div>
      </div>

      <!-- Batch Prices (Watchlist) -->
      <div class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-4">Bảng giá nhanh</h2>
        <div class="flex gap-3 mb-4">
          <input type="text" [(ngModel)]="batchSymbols" appUppercase
            placeholder="Nhập các mã, cách nhau dấu phẩy (VD: VNM,FPT,VCB)"
            class="flex-1 px-4 py-2 border border-gray-300 rounded-lg">
          <button (click)="loadBatchPrices()" [disabled]="loadingBatch"
            class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
            {{ loadingBatch ? 'Đang tải...' : 'Xem giá' }}
          </button>
        </div>
        <div *ngIf="batchPrices.length > 0" class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-3">
          <div *ngFor="let bp of batchPrices"
            class="border rounded-lg p-3 text-center hover:shadow-md transition-shadow cursor-pointer"
            (click)="searchSymbol = bp.symbol; lookupStock()">
            <div class="font-bold text-gray-800">{{ bp.symbol }}</div>
            <div class="text-lg font-semibold text-blue-600">{{ bp.close | vndCurrency }}</div>
            <div class="text-xs text-gray-500">KL: {{ formatVolume(bp.volume) }}</div>
          </div>
        </div>
      </div>

      <!-- Price History -->
      <div class="bg-white rounded-lg shadow p-6">
        <h2 class="text-lg font-semibold text-gray-700 mb-4">Lịch sử giá</h2>
        <div class="flex flex-wrap gap-3 mb-4">
          <input type="text" [(ngModel)]="historySymbol" appUppercase placeholder="Mã CP"
            class="px-4 py-2 border border-gray-300 rounded-lg w-32">
          <input type="date" [(ngModel)]="historyFrom" class="px-4 py-2 border border-gray-300 rounded-lg">
          <input type="date" [(ngModel)]="historyTo" class="px-4 py-2 border border-gray-300 rounded-lg">
          <button (click)="loadHistory()" [disabled]="loadingHistory"
            class="bg-blue-600 hover:bg-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-colors disabled:opacity-50">
            {{ loadingHistory ? 'Đang tải...' : 'Xem lịch sử' }}
          </button>
        </div>
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
    </div>

    <!-- AI Evaluation Panel -->
    <app-ai-chat-panel
      [(isOpen)]="isAiOpen"
      [title]="'AI Đánh giá: ' + searchSymbol"
      [useCase]="'stock-evaluation'"
      [contextData]="{ symbol: searchSymbol }">
    </app-ai-chat-panel>
  `
})
export class MarketDataComponent implements OnInit {
  // Overview
  marketOverview: MarketOverview[] = [];
  loadingOverview = false;

  // Stock Detail
  searchSymbol = '';
  stockDetail: StockDetail | null = null;
  loadingDetail = false;
  tradingSummary: TradingHistorySummary | null = null;
  summaryItems: { label: string; value: number }[] = [];

  // Technical Analysis
  analysis: TechnicalAnalysis | null = null;
  analyzingSignal = false;

  // AI Evaluation
  isAiOpen = false;

  // Search suggestions
  searchResults: StockSearchResult[] = [];
  private searchTimeout: any;

  // Top Fluctuation
  topFluctuations: TopFluctuation[] = [];
  loadingTop = false;
  selectedFloor = '10';
  floors = [
    { code: '10', label: 'HOSE' },
    { code: '02', label: 'HNX' },
    { code: '03', label: 'UPCOM' }
  ];

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

  constructor(
    private marketDataService: MarketDataService,
    private notificationService: NotificationService,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.loadMarketOverview();
    this.loadBatchPrices();
    this.loadTopFluctuation(this.selectedFloor);

    const today = new Date();
    const thirtyDaysAgo = new Date(today);
    thirtyDaysAgo.setDate(today.getDate() - 30);
    this.historyTo = today.toISOString().split('T')[0];
    this.historyFrom = thirtyDaysAgo.toISOString().split('T')[0];

    // Auto-fill symbol from query param (e.g. /market-data?symbol=BVH)
    this.route.queryParams.subscribe(params => {
      const symbol = params['symbol']?.trim();
      if (symbol) {
        this.searchSymbol = symbol.toUpperCase();
        this.lookupStock();
      }
    });
  }

  // --- Market Overview ---

  loadMarketOverview(): void {
    this.loadingOverview = true;
    this.marketDataService.getMarketOverview().subscribe({
      next: data => { this.marketOverview = data; this.loadingOverview = false; },
      error: () => { this.loadingOverview = false; }
    });
  }

  selectIndex(symbol: string): void {
    this.searchSymbol = symbol;
    // Index symbols can't be looked up via stock detail — just show as selected
  }

  // --- AI Evaluation ---

  openAiEvaluation(): void {
    this.isAiOpen = true;
  }

  // --- Stock Lookup ---

  onSearchInput(): void {
    clearTimeout(this.searchTimeout);
    const kw = this.searchSymbol?.trim();
    if (!kw || kw.length < 2) {
      this.searchResults = [];
      return;
    }
    this.searchTimeout = setTimeout(() => {
      this.marketDataService.searchStocks(kw).subscribe({
        next: data => this.searchResults = data,
        error: () => this.searchResults = []
      });
    }, 300);
  }

  selectSearchResult(r: StockSearchResult): void {
    this.searchSymbol = r.symbol;
    this.searchResults = [];
    this.lookupStock();
  }

  lookupStock(): void {
    const sym = this.searchSymbol?.trim();
    if (!sym) return;
    this.searchResults = [];
    this.loadingDetail = true;
    this.stockDetail = null;
    this.tradingSummary = null;
    this.analysis = null;

    this.marketDataService.getStockDetail(sym).subscribe({
      next: data => {
        this.stockDetail = data;
        this.loadingDetail = false;
        this.historySymbol = data.symbol;
        this.loadTradingSummary(data.symbol);
        this.loadTechnicalAnalysis(data.symbol);
      },
      error: () => {
        this.notificationService.error('Lỗi', 'Không tìm thấy dữ liệu cho mã ' + sym);
        this.loadingDetail = false;
      }
    });
  }

  loadTradingSummary(symbol: string): void {
    this.marketDataService.getTradingSummary(symbol).subscribe({
      next: data => {
        this.tradingSummary = data;
        this.summaryItems = [
          { label: '1 ngày', value: data.changeDay },
          { label: '1 tuần', value: data.changeWeek },
          { label: '1 tháng', value: data.changeMonth },
          { label: '3 tháng', value: data.change3Month },
          { label: '6 tháng', value: data.change6Month }
        ];
      },
      error: () => {}
    });
  }

  // --- Technical Analysis ---

  loadTechnicalAnalysis(symbol: string): void {
    this.analyzingSignal = true;
    this.analysis = null;
    this.marketDataService.getTechnicalAnalysis(symbol).subscribe({
      next: data => { this.analysis = data; this.analyzingSignal = false; },
      error: () => { this.analyzingSignal = false; }
    });
  }

  getSignalEmoji(signal: string): string {
    switch (signal) {
      case 'strong_buy': return '🟢🟢';
      case 'buy': return '🟢';
      case 'strong_sell': return '🔴🔴';
      case 'sell': return '🔴';
      default: return '🟡';
    }
  }

  // --- Top Fluctuation ---

  loadTopFluctuation(floor: string): void {
    this.selectedFloor = floor;
    this.loadingTop = true;
    this.marketDataService.getTopFluctuation(floor).subscribe({
      next: data => { this.topFluctuations = data; this.loadingTop = false; },
      error: () => { this.loadingTop = false; }
    });
  }

  // --- History ---

  loadHistory(): void {
    if (!this.historySymbol.trim()) return;
    this.loadingHistory = true;
    this.historyLoaded = false;
    this.marketDataService.getPriceHistory(this.historySymbol.trim(), this.historyFrom, this.historyTo).subscribe({
      next: data => { this.priceHistory = data; this.loadingHistory = false; this.historyLoaded = true; },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi tải lịch sử giá');
        this.loadingHistory = false;
        this.historyLoaded = true;
      }
    });
  }

  // --- Batch ---

  loadBatchPrices(): void {
    if (!this.batchSymbols.trim()) return;
    this.loadingBatch = true;
    const symbols = this.batchSymbols.split(',').map(s => s.trim()).filter(s => s);
    this.marketDataService.getBatchPrices(symbols).subscribe({
      next: data => { this.batchPrices = data; this.loadingBatch = false; },
      error: () => {
        this.notificationService.error('Lỗi', 'Lỗi khi tải bảng giá');
        this.loadingBatch = false;
      }
    });
  }

  // --- Helpers ---

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

  formatBillion(value: number): string {
    if (value >= 1000) return (value / 1000).toFixed(1) + ' nghìn tỷ';
    return value.toFixed(1) + ' tỷ';
  }
}
