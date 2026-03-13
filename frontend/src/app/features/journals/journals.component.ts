import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { JournalService, TradeJournal, CreateJournalRequest, UpdateJournalRequest } from '../../core/services/journal.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-journals',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container mx-auto px-4 py-6">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-2xl font-bold text-gray-800">Nhật ký Giao dịch</h1>
        <button (click)="showCreateForm = !showCreateForm"
          class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition">
          {{ showCreateForm ? 'Đóng' : '+ Tạo nhật ký' }}
        </button>
      </div>

      <!-- Portfolio Filter -->
      <div class="bg-white rounded-lg shadow p-4 mb-6">
        <div class="flex flex-wrap items-center gap-4">
          <label class="text-sm font-medium text-gray-700">Lọc theo danh mục:</label>
          <select [(ngModel)]="selectedPortfolioId" (ngModelChange)="loadJournals()"
            class="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 min-w-[200px]">
            <option value="">Tất cả danh mục</option>
            <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
          </select>
        </div>
      </div>

      <!-- Create Form -->
      <div *ngIf="showCreateForm" class="bg-white rounded-lg shadow p-6 mb-6">
        <h2 class="text-lg font-semibold mb-4">Tạo nhật ký mới</h2>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Trade ID *</label>
            <input [(ngModel)]="newJournal.tradeId" type="text"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="ID giao dịch">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Portfolio ID *</label>
            <select [(ngModel)]="newJournal.portfolioId"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn danh mục --</option>
              <option *ngFor="let p of portfolios" [value]="p.id">{{ p.name }}</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Lý do vào lệnh</label>
            <textarea [(ngModel)]="newJournal.entryReason" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Tại sao quyết định vào lệnh?"></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Bối cảnh thị trường</label>
            <textarea [(ngModel)]="newJournal.marketContext" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Tình hình thị trường lúc vào lệnh?"></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Setup kỹ thuật <sup class="text-violet-400 font-bold">¹</sup></label>
            <textarea [(ngModel)]="newJournal.technicalSetup" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="RSI, MACD, support/resistance..."></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Trạng thái cảm xúc <sup class="text-pink-400 font-bold">²</sup></label>
            <select [(ngModel)]="newJournal.emotionalState"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
              <option value="">-- Chọn --</option>
              <option value="Confident">Tự tin</option>
              <option value="Fearful">Sợ hãi</option>
              <option value="Greedy">Tham lam</option>
              <option value="FOMO">FOMO</option>
              <option value="Calm">Bình tĩnh</option>
              <option value="Anxious">Lo lắng</option>
              <option value="Excited">Hào hứng</option>
            </select>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Mức tự tin (1-10) <sup class="text-amber-400 font-bold">³</sup></label>
            <input [(ngModel)]="newJournal.confidenceLevel" type="number" min="1" max="10"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Đánh giá (0-5)</label>
            <input [(ngModel)]="newJournal.rating" type="number" min="0" max="5"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500">
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Đánh giá sau giao dịch <sup class="text-blue-400 font-bold">⁴</sup></label>
            <textarea [(ngModel)]="newJournal.postTradeReview" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Kết quả giao dịch..."></textarea>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">Bài học rút ra</label>
            <textarea [(ngModel)]="newJournal.lessonsLearned" rows="2"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Bài học gì từ giao dịch này?"></textarea>
          </div>
          <div class="md:col-span-2">
            <label class="block text-sm font-medium text-gray-700 mb-1">Tags</label>
            <input [(ngModel)]="tagsInput" type="text"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="Nhập tags phân cách bằng dấu phẩy (VD: breakout, trending, high-risk)">
          </div>
        </div>
        <!-- Glossary -->
        <div class="mt-4 rounded-lg bg-gray-50 border border-gray-200 px-4 py-3 text-xs text-gray-500 space-y-1">
          <div><sup class="text-violet-400 font-bold">¹</sup> <strong>Setup kỹ thuật (Technical Setup):</strong> Các tín hiệu phân tích kỹ thuật đã nhận diện trước khi vào lệnh: mô hình nến, đường MA, RSI, MACD, vùng hỗ trợ/kháng cự, Bollinger Bands…</div>
          <div><sup class="text-pink-400 font-bold">²</sup> <strong>Trạng thái cảm xúc:</strong> Ghi nhận tâm lý lúc vào lệnh để phân tích sau. <strong>FOMO</strong> (Fear Of Missing Out) = sợ bỏ lỡ cơ hội, vào lệnh khi giá đã chạy xa mà không theo kế hoạch.</div>
          <div><sup class="text-amber-400 font-bold">³</sup> <strong>Mức tự tin (Confidence):</strong> Thang điểm 1–10 đánh giá độ chắc chắn của tín hiệu. Điểm thấp (&lt;5) = tín hiệu yếu, nên giảm size hoặc bỏ qua lệnh.</div>
          <div><sup class="text-blue-400 font-bold">⁴</sup> <strong>Đánh giá sau giao dịch (Post-trade Review):</strong> Ghi lại kết quả thực tế so với kế hoạch: lệnh đúng/sai chiến lược, cảm xúc có ảnh hưởng không, rút kinh nghiệm gì.</div>
        </div>

        <div class="mt-4 flex justify-end gap-2">
          <button (click)="showCreateForm = false"
            class="px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-50 transition">Hủy</button>
          <button (click)="createJournal()"
            [disabled]="!newJournal.tradeId || !newJournal.portfolioId"
            class="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition disabled:opacity-50">
            Tạo nhật ký
          </button>
        </div>
      </div>

      <!-- Journal List -->
      <div class="bg-white rounded-lg shadow">
        <div class="p-4">
          <div *ngIf="loading" class="text-center py-8 text-gray-500">Đang tải...</div>
          <div *ngIf="!loading && journals.length === 0" class="text-center py-8 text-gray-500">
            Chưa có nhật ký giao dịch nào.
          </div>
          <div class="space-y-4">
            <div *ngFor="let j of journals" class="border rounded-lg p-4 hover:shadow-md transition">
              <div class="flex justify-between items-start">
                <div class="flex-1">
                  <div class="flex items-center gap-3 mb-2">
                    <span class="text-sm font-mono bg-gray-100 px-2 py-0.5 rounded">Trade: {{ j.tradeId | slice:0:8 }}...</span>
                    <div class="flex">
                      <span *ngFor="let star of [1,2,3,4,5]"
                        class="text-lg" [class.text-yellow-400]="star <= j.rating" [class.text-gray-300]="star > j.rating">★</span>
                    </div>
                    <span *ngIf="j.confidenceLevel"
                      class="px-2 py-0.5 text-xs rounded-full"
                      [class.bg-green-100]="j.confidenceLevel >= 7" [class.text-green-700]="j.confidenceLevel >= 7"
                      [class.bg-yellow-100]="j.confidenceLevel >= 4 && j.confidenceLevel < 7" [class.text-yellow-700]="j.confidenceLevel >= 4 && j.confidenceLevel < 7"
                      [class.bg-red-100]="j.confidenceLevel < 4" [class.text-red-700]="j.confidenceLevel < 4">
                      Tin cậy: {{ j.confidenceLevel }}/10
                    </span>
                    <span *ngIf="j.emotionalState"
                      class="px-2 py-0.5 text-xs rounded-full bg-blue-100 text-blue-700">
                      {{ getEmotionLabel(j.emotionalState) }}
                    </span>
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
                    <div *ngIf="j.entryReason">
                      <span class="font-medium text-gray-700">Lý do vào lệnh:</span>
                      <p class="text-gray-600">{{ j.entryReason }}</p>
                    </div>
                    <div *ngIf="j.marketContext">
                      <span class="font-medium text-gray-700">Bối cảnh thị trường:</span>
                      <p class="text-gray-600">{{ j.marketContext }}</p>
                    </div>
                    <div *ngIf="j.technicalSetup">
                      <span class="font-medium text-gray-700">Setup kỹ thuật:</span>
                      <p class="text-gray-600">{{ j.technicalSetup }}</p>
                    </div>
                    <div *ngIf="j.postTradeReview">
                      <span class="font-medium text-gray-700">Đánh giá sau GD:</span>
                      <p class="text-gray-600">{{ j.postTradeReview }}</p>
                    </div>
                    <div *ngIf="j.lessonsLearned" class="md:col-span-2">
                      <span class="font-medium text-gray-700">Bài học:</span>
                      <p class="text-gray-600">{{ j.lessonsLearned }}</p>
                    </div>
                  </div>
                  <div *ngIf="j.tags && j.tags.length > 0" class="mt-2 flex flex-wrap gap-1">
                    <span *ngFor="let tag of j.tags"
                      class="px-2 py-0.5 text-xs bg-indigo-100 text-indigo-700 rounded-full">#{{ tag }}</span>
                  </div>
                  <div class="text-xs text-gray-400 mt-2">{{ j.createdAt | date:'dd/MM/yyyy HH:mm' }}</div>
                </div>
                <div class="flex gap-2 ml-4">
                  <button (click)="deleteJournal(j)"
                    class="px-3 py-1 text-sm bg-red-100 text-red-700 rounded hover:bg-red-200 transition">Xóa</button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class JournalsComponent implements OnInit {
  journals: TradeJournal[] = [];
  portfolios: PortfolioSummary[] = [];
  selectedPortfolioId = '';
  loading = false;
  showCreateForm = false;
  tagsInput = '';

  newJournal: CreateJournalRequest = {
    tradeId: '',
    portfolioId: '',
    entryReason: '',
    marketContext: '',
    technicalSetup: '',
    emotionalState: '',
    confidenceLevel: 5,
    postTradeReview: '',
    lessonsLearned: '',
    rating: 3,
    tags: []
  };

  constructor(
    private journalService: JournalService,
    private portfolioService: PortfolioService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadPortfolios();
    this.loadJournals();
  }

  loadPortfolios(): void {
    this.portfolioService.getAll().subscribe({
      next: (data) => this.portfolios = data,
      error: () => this.notification.error('Lỗi', 'Không thể tải danh mục')
    });
  }

  loadJournals(): void {
    this.loading = true;
    this.journalService.getAll(this.selectedPortfolioId || undefined).subscribe({
      next: (data) => {
        this.journals = data;
        this.loading = false;
      },
      error: () => {
        this.notification.error('Lỗi', 'Không thể tải nhật ký');
        this.loading = false;
      }
    });
  }

  createJournal(): void {
    if (!this.newJournal.tradeId || !this.newJournal.portfolioId) return;
    this.newJournal.tags = this.tagsInput ? this.tagsInput.split(',').map(t => t.trim()).filter(t => t) : [];
    this.journalService.create(this.newJournal).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã tạo nhật ký');
        this.showCreateForm = false;
        this.resetForm();
        this.loadJournals();
      },
      error: () => this.notification.error('Lỗi', 'Không thể tạo nhật ký')
    });
  }

  deleteJournal(j: TradeJournal): void {
    if (!confirm('Xóa nhật ký này?')) return;
    this.journalService.delete(j.id).subscribe({
      next: () => {
        this.notification.success('Thành công', 'Đã xóa nhật ký');
        this.loadJournals();
      },
      error: () => this.notification.error('Lỗi', 'Không thể xóa nhật ký')
    });
  }

  getEmotionLabel(emotion: string): string {
    const labels: Record<string, string> = {
      'Confident': '😎 Tự tin', 'Fearful': '😰 Sợ hãi', 'Greedy': '🤑 Tham lam',
      'FOMO': '😱 FOMO', 'Calm': '😌 Bình tĩnh', 'Anxious': '😟 Lo lắng', 'Excited': '🤩 Hào hứng'
    };
    return labels[emotion] || emotion;
  }

  private resetForm(): void {
    this.newJournal = {
      tradeId: '', portfolioId: '', entryReason: '', marketContext: '',
      technicalSetup: '', emotionalState: '', confidenceLevel: 5,
      postTradeReview: '', lessonsLearned: '', rating: 3, tags: []
    };
    this.tagsInput = '';
  }
}
