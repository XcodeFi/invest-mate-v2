import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  CompanyDossierService,
  RiskFactorDto,
  DossierFreshness,
  INVALIDATION_TRIGGER_LABELS,
  dossierFreshnessLabel,
  dossierFreshnessBadgeClass,
} from '../../core/services/company-dossier.service';
import { NotificationService } from '../../core/services/notification.service';

const PENDING_PLAN_KEY = 'pendingTradePlanDraft';
const MIN_BUSINESS_MODEL_LEN = 30;

@Component({
  selector: 'app-company-dossier-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container mx-auto px-4 py-6 max-w-4xl">
      <div class="flex items-center justify-between mb-6">
        <div>
          <h1 class="text-2xl font-bold text-gray-800">Hồ sơ công ty: {{ symbol }}</h1>
          <div class="flex items-center gap-2 mt-1">
            <span class="text-xs px-2 py-0.5 rounded-full font-medium" [ngClass]="freshnessClass()">
              {{ freshnessLabel() }}
            </span>
            <span *ngIf="reviewedAt" class="text-xs text-gray-400">Soát gần nhất: {{ reviewedAt | date:'short' }}</span>
          </div>
        </div>
        <a routerLink="/company-dossier" class="text-sm text-blue-600 hover:underline whitespace-nowrap">← Danh sách hồ sơ</a>
      </div>

      <div *ngIf="loading" class="text-center text-gray-400 py-10">Đang tải...</div>

      <div *ngIf="!loading">
        <!-- Agent draft warning -->
        <div *ngIf="showAgentDraftWarning()" class="mb-4 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Agent đã cập nhật lúc {{ agentDraftedAt | date:'short' }} — chưa xác nhận
        </div>

        <!-- NeedsReview: nhắc không chặn -->
        <div *ngIf="freshness === 'NeedsReview'" class="mb-4 rounded-lg border border-yellow-300 bg-yellow-50 px-4 py-3 text-sm text-yellow-800">
          Hồ sơ đã 90–179 ngày, chưa bị chặn nhưng nên cập nhật tin mới rồi ký lại.
        </div>

        <!-- Business Model -->
        <div class="bg-white rounded-lg shadow p-5 mb-6">
          <label class="block text-sm font-medium text-gray-700 mb-1">Doanh nghiệp này kiếm tiền bằng gì?</label>
          <p class="text-xs text-gray-400 mb-2">Nêu sản phẩm/dịch vụ và ai trả tiền. "Tiềm năng", "đầu ngành" KHÔNG phải câu trả lời.</p>
          <textarea [(ngModel)]="businessModel" rows="3"
            class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
            placeholder="VD: Bán thép xây dựng cho các nhà thầu và đại lý vật liệu, thu tiền ngay khi giao hàng."></textarea>
          <p class="text-xs mt-1" [class.text-red-500]="businessModelLength() < MIN_BUSINESS_MODEL_LEN" [class.text-gray-400]="businessModelLength() >= MIN_BUSINESS_MODEL_LEN">
            {{ businessModelCounterText() }}
          </p>
        </div>

        <!-- Moats -->
        <div class="bg-white rounded-lg shadow p-5 mb-6">
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-sm font-semibold text-gray-700">Lợi thế cạnh tranh (Moat)</h2>
            <button (click)="addMoat()" class="px-3 py-1 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded-lg">+ Thêm dòng</button>
          </div>
          <div *ngIf="moats.length === 0" class="text-xs text-gray-400 italic">Chưa có moat nào</div>
          <div *ngFor="let m of moats; let i = index" class="flex items-center gap-2 mb-2">
            <input [(ngModel)]="m.description" type="text" placeholder="VD: Mạng lưới phân phối phủ 63 tỉnh thành"
              class="flex-1 px-3 py-2 border border-gray-300 rounded-lg text-sm">
            <button (click)="removeMoat(i)" class="p-1.5 text-red-400 hover:text-red-600 hover:bg-red-50 rounded">✕</button>
          </div>
        </div>

        <!-- Risk Factors -->
        <div class="bg-white rounded-lg shadow p-5 mb-6">
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-sm font-semibold text-gray-700">Yếu tố rủi ro (hạng 1 = nguy hiểm nhất)</h2>
            <button (click)="addRiskFactor()" class="px-3 py-1 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded-lg">+ Thêm yếu tố</button>
          </div>
          <div *ngIf="riskFactors.length === 0" class="text-xs text-gray-400 italic">Chưa có yếu tố rủi ro nào</div>
          <div *ngFor="let r of riskFactors; let i = index" class="border border-gray-200 rounded-lg p-3 mb-3">
            <div class="flex items-start gap-2">
              <div class="flex flex-col items-center pt-1">
                <button (click)="moveUp(i)" [disabled]="i === 0" class="text-gray-400 hover:text-gray-700 disabled:opacity-30 leading-none">▲</button>
                <span class="text-xs font-bold text-gray-500">{{ r.rank }}</span>
                <button (click)="moveDown(i)" [disabled]="i === riskFactors.length - 1" class="text-gray-400 hover:text-gray-700 disabled:opacity-30 leading-none">▼</button>
              </div>
              <div class="flex-1 space-y-2">
                <input [(ngModel)]="r.description" type="text" placeholder="Mô tả rủi ro"
                  class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm">
                <input [(ngModel)]="r.observableSignal" type="text" placeholder="Dấu hiệu quan sát được (bắt buộc) — VD: biên lợi nhuận gộp giảm 2 quý liên tiếp"
                  class="w-full px-3 py-2 border rounded-lg text-sm"
                  [class.border-red-400]="!r.observableSignal.trim()" [class.border-gray-300]="!!r.observableSignal.trim()">
                <p *ngIf="!r.observableSignal.trim()" class="text-xs text-red-500">Bắt buộc phải có dấu hiệu quan sát được</p>
                <div class="flex items-center gap-4 flex-wrap">
                  <select [(ngModel)]="r.suggestedTrigger" class="px-2 py-1 border border-gray-300 rounded-lg text-xs">
                    <option [ngValue]="null">-- Kịch bản vô hiệu hoá --</option>
                    <option *ngFor="let t of triggerOptions" [ngValue]="t.value">{{ t.label }}</option>
                  </select>
                  <label class="flex items-center gap-1.5 text-xs text-gray-600">
                    <input type="checkbox" [(ngModel)]="r.isDealBreaker" [disabled]="dealBreakerDisabled(i)">
                    Yếu tố hủy diệt
                  </label>
                </div>
              </div>
              <button (click)="removeRiskFactor(i)" class="p-1.5 text-red-400 hover:text-red-600 hover:bg-red-50 rounded">✕</button>
            </div>
          </div>
        </div>

        <!-- Notes -->
        <div class="bg-white rounded-lg shadow p-5 mb-6">
          <label class="block text-sm font-medium text-gray-700 mb-1">Ghi chú tự do</label>
          <p class="text-xs text-gray-400 mb-2">Không ảnh hưởng điều kiện chặn.</p>
          <textarea [(ngModel)]="notes" rows="2"
            class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"></textarea>
        </div>

        <div class="flex justify-end mb-8">
          <button (click)="save()" [disabled]="saving"
            class="px-5 py-2.5 bg-gray-700 hover:bg-gray-800 disabled:bg-gray-300 text-white rounded-lg text-sm font-medium">
            {{ saving ? 'Đang lưu...' : 'Lưu' }}
          </button>
        </div>

        <!-- Sign — cuối trang, sau nội dung, không cạnh nút Lưu -->
        <div class="border-t pt-6 pb-10 text-center">
          <button (click)="sign()" [disabled]="signing || !canSign()"
            class="px-8 py-3 bg-emerald-600 hover:bg-emerald-700 disabled:bg-gray-300 text-white rounded-lg font-semibold">
            {{ signing ? 'Đang ký...' : signLabel() }}
          </button>
          <p *ngIf="!exists && !signing" class="text-xs text-red-500 mt-2">Cần lưu hồ sơ trước khi ký.</p>
          <p class="text-xs text-gray-400 mt-2">Ký xác nhận rằng bạn — con người — đã đọc và chịu trách nhiệm với nội dung hồ sơ này.</p>
        </div>
      </div>
    </div>
  `,
})
export class CompanyDossierDetailComponent implements OnInit {
  readonly MIN_BUSINESS_MODEL_LEN = MIN_BUSINESS_MODEL_LEN;

  symbol = '';
  businessModel = '';
  moats: { description: string }[] = [];
  riskFactors: RiskFactorDto[] = [];
  notes: string | null = '';
  reviewedAt: string | null = null;
  confirmedAt: string | null = null;
  agentDraftedAt: string | null = null;
  freshness: DossierFreshness = 'Unconfirmed';

  loading = false;
  saving = false;
  signing = false;
  exists = false;

  // Ngữ cảnh size (nếu được forward qua query params) — chỉ để hiển thị gợi ý, không gọi gate-status.
  private quantity: number | null = null;
  private entryPrice: number | null = null;
  private accountBalance: number | null = null;
  private returnTo: string | null = null;

  readonly triggerOptions = Object.entries(INVALIDATION_TRIGGER_LABELS).map(([value, label]) => ({ value, label }));

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private dossierService: CompanyDossierService,
    private notification: NotificationService,
  ) {}

  ngOnInit(): void {
    const symbolParam = this.route.snapshot.paramMap.get('symbol');
    this.symbol = (symbolParam ?? '').toUpperCase().trim();

    const qp = this.route.snapshot.queryParams;
    this.returnTo = qp['returnTo'] ?? null;
    this.quantity = qp['quantity'] != null ? +qp['quantity'] : null;
    this.entryPrice = qp['entryPrice'] != null ? +qp['entryPrice'] : null;
    this.accountBalance = qp['accountBalance'] != null ? +qp['accountBalance'] : null;

    if (!this.symbol) return;
    this.loading = true;
    this.dossierService.get(this.symbol).subscribe({
      next: (dto) => {
        this.exists = true;
        this.businessModel = dto.businessModel ?? '';
        this.moats = dto.moats ?? [];
        this.riskFactors = dto.riskFactors ?? [];
        this.notes = dto.notes ?? '';
        this.reviewedAt = dto.reviewedAt;
        this.confirmedAt = dto.confirmedAt;
        this.agentDraftedAt = dto.agentDraftedAt;
        this.freshness = dto.freshness;
        this.loading = false;
      },
      error: () => {
        // 404 — mã chưa có hồ sơ, bắt đầu với form trống
        this.exists = false;
        this.loading = false;
      },
    });
  }

  // --- Business model counter ---

  businessModelLength(): number {
    return this.businessModel.trim().length;
  }

  businessModelCounterText(): string {
    const count = this.businessModelLength();
    const sizeHint = this.sizePercentHint();
    if (sizeHint != null) {
      return `${count}/${MIN_BUSINESS_MODEL_LEN} (Size ${sizeHint} tài khoản — bắt buộc ≥ ${MIN_BUSINESS_MODEL_LEN})`;
    }
    return `${count}/${MIN_BUSINESS_MODEL_LEN} (bắt buộc ≥ ${MIN_BUSINESS_MODEL_LEN})`;
  }

  private sizePercentHint(): string | null {
    if (!this.quantity || !this.entryPrice || !this.accountBalance) return null;
    const pct = (this.quantity * this.entryPrice / this.accountBalance) * 100;
    return `${pct.toLocaleString('vi-VN', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
  }

  // --- Moats ---

  addMoat(): void {
    this.moats.push({ description: '' });
  }

  removeMoat(index: number): void {
    this.moats.splice(index, 1);
  }

  // --- Risk factors ---

  addRiskFactor(): void {
    this.riskFactors.push({
      rank: this.riskFactors.length + 1,
      description: '',
      observableSignal: '',
      isDealBreaker: false,
      suggestedTrigger: null,
    });
  }

  removeRiskFactor(index: number): void {
    this.riskFactors.splice(index, 1);
    this.riskFactors = this.riskFactors.map((r, i) => ({ ...r, rank: i + 1 }));
  }

  dealBreakerDisabled(index: number): boolean {
    const otherHasIt = this.riskFactors.some((r, i) => i !== index && r.isDealBreaker);
    return otherHasIt && !this.riskFactors[index].isDealBreaker;
  }

  moveUp(index: number): void {
    if (index === 0) return;
    const items = [...this.riskFactors];
    [items[index - 1], items[index]] = [items[index], items[index - 1]];
    this.riskFactors = items.map((r, i) => ({ ...r, rank: i + 1 }));
  }

  moveDown(index: number): void {
    if (index >= this.riskFactors.length - 1) return;
    this.moveUp(index + 1);
  }

  // --- Freshness / sign ---

  freshnessLabel(): string {
    return dossierFreshnessLabel(this.freshness);
  }

  freshnessClass(): Record<string, boolean> {
    return dossierFreshnessBadgeClass(this.freshness);
  }

  signLabel(): string {
    if (this.freshness === 'Expired') return 'Đã cập nhật tin mới và xác nhận';
    if (this.freshness === 'Unconfirmed') return 'Tôi đã đọc và chịu trách nhiệm';
    return 'Vẫn đúng';
  }

  showAgentDraftWarning(): boolean {
    if (!this.agentDraftedAt) return false;
    if (!this.confirmedAt) return true;
    return new Date(this.agentDraftedAt) > new Date(this.confirmedAt);
  }

  // Phải tồn tại trên server mới cho ký — backend confirm() trả 404 nếu chưa Lưu lần nào.
  canSign(): boolean {
    return this.exists && this.businessModel.trim().length >= MIN_BUSINESS_MODEL_LEN;
  }

  // --- Save / Sign actions ---

  save(): void {
    if (!this.symbol || this.saving) return;
    this.saving = true;
    this.dossierService
      .upsert(this.symbol, {
        BusinessModel: this.businessModel,
        Moats: this.moats.map((m) => ({ Description: m.description })),
        RiskFactors: this.riskFactors.map((r) => ({
          Rank: r.rank,
          Description: r.description,
          ObservableSignal: r.observableSignal,
          IsDealBreaker: r.isDealBreaker,
          SuggestedTrigger: r.suggestedTrigger,
        })),
        Notes: this.notes,
      })
      .subscribe({
        next: () => {
          this.saving = false;
          this.exists = true;
          this.notification.success('Hồ sơ công ty', 'Đã lưu');
        },
        error: () => {
          this.saving = false;
          this.notification.error('Lỗi', 'Không thể lưu hồ sơ');
        },
      });
  }

  sign(): void {
    if (!this.symbol || this.signing || !this.canSign()) return;
    this.signing = true;
    this.dossierService.confirm(this.symbol).subscribe({
      next: () => {
        this.signing = false;
        this.confirmedAt = new Date().toISOString();
        this.freshness = 'Fresh';
        this.notification.success('Hồ sơ công ty', 'Đã ký xác nhận');
        if (this.returnTo === 'trade-plan') {
          this.goBackToTradePlan();
        }
      },
      error: () => {
        this.signing = false;
        this.notification.error('Lỗi', 'Không thể ký xác nhận');
      },
    });
  }

  private goBackToTradePlan(): void {
    const draft = this.consumePendingPlanDraft();
    const queryParams = draft
      ? { symbol: draft['symbol'], entry: draft['entryPrice'], sl: draft['stopLoss'], tp: draft['target'] }
      : {};
    this.router.navigate(['/trade-plan'], { queryParams });
  }

  consumePendingPlanDraft(): Record<string, unknown> | null {
    const raw = sessionStorage.getItem(PENDING_PLAN_KEY);
    if (!raw) return null;
    sessionStorage.removeItem(PENDING_PLAN_KEY);
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }
}
