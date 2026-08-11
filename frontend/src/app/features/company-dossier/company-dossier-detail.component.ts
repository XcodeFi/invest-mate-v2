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
import { FundamentalsPanelComponent } from './fundamentals-panel.component';
import { DossierViewComponent } from './dossier-view.component';
import { buildAiPrompt, parseAiPayload } from './dossier-clipboard';
import { CompanyFundamentals } from '../../core/services/market-data.service';

/**
 * Lấy nguyên văn lý do server trả về. Backend đã nói rõ và nói đúng tiếng Việt — ví dụ
 * "Mỗi yếu tố rủi ro phải có dấu hiệu quan sát được" — nhưng trước đây FE ném đi và chỉ hiện
 * "Không thể lưu hồ sơ", nên người dùng không có cách nào biết ô nào đang chặn mình.
 * `detail` là của ProblemDetails (exception middleware); `error` là của các BadRequest tự tay.
 */
export function serverMessage(err: unknown): string {
  const body = (err as { error?: { detail?: string; error?: string } } | null)?.error;
  return body?.detail || body?.error || 'Không thể lưu hồ sơ — thử lại sau.';
}

const PENDING_PLAN_KEY = 'pendingTradePlanDraft';
const MIN_BUSINESS_MODEL_LEN = 30;

/**
 * `touched` chỉ sống ở client — save() map từng field một nên nó không bao giờ lọt xuống API.
 * Đặt cờ trên chính item (thay vì Set theo index) để nó sống sót qua moveUp/moveDown: hai hàm đó
 * tạo object MỚI bằng spread, nên mọi thứ khoá theo identity hay theo index đều đứt sau một lần đổi chỗ.
 */
type RiskFactorRow = RiskFactorDto & { touched?: boolean };

interface DossierEditable {
  businessModel: string;
  moats: { description: string }[];
  riskFactors: RiskFactorRow[];
  notes: string | null;
}

/**
 * MỘT serializer cho cả hai vế của phép so sánh "đã sửa gì chưa". Chỉ lấy field người dùng gõ:
 * `touched` là cờ UI, để nó lọt vào đây thì chỉ cần tab qua một ô rồi tab ra là hồ sơ bị coi như đã
 * sửa, và nút Hủy hỏi lại dù không ai đổi một chữ nào.
 */
function serializeEditable(d: DossierEditable): string {
  return JSON.stringify({
    businessModel: d.businessModel,
    moats: d.moats.map((m) => m.description),
    riskFactors: d.riskFactors.map((r) => [r.rank, r.description, r.observableSignal, r.isDealBreaker, r.suggestedTrigger]),
    notes: d.notes ?? '',
  });
}

@Component({
  selector: 'app-company-dossier-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, FundamentalsPanelComponent, DossierViewComponent],
  template: `
    <div class="container mx-auto px-4 py-6 max-w-6xl">
      <!-- Đường lùi là breadcrumb ở góc trái, không xếp cùng hàng với nhóm nút hành động bên phải. -->
      <a routerLink="/company-dossier" class="inline-block text-sm text-blue-600 hover:underline mb-2">← Danh sách hồ sơ</a>

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
        <div class="flex items-center gap-3">
          @if (!loading) {
            <button (click)="copyForAi()" data-testid="btn-copy" title="Sao chép hồ sơ + số liệu để hỏi một AI khác"
              class="px-3 py-2 border border-gray-300 hover:bg-gray-50 text-gray-700 rounded-lg text-sm whitespace-nowrap">
              {{ copied ? '✓ Đã chép' : 'Sao chép cho AI' }}
            </button>
            <button (click)="openPaste()" data-testid="btn-open-paste"
              class="px-3 py-2 border border-gray-300 hover:bg-gray-50 text-gray-700 rounded-lg text-sm whitespace-nowrap">
              Dán từ AI
            </button>
          }
          @if (!loading && mode === 'view') {
            <button (click)="startEdit()" data-testid="btn-edit"
              class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-medium whitespace-nowrap">
              Sửa
            </button>
          }
        </div>
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

        <!-- Ô viết bên trái, số liệu doanh nghiệp bên phải; xếp dọc trên mobile.
             3:2 chứ không phải 1:1 — chia đôi thì ô nhập rủi ro chỉ còn ~480px, câu tả một dấu hiệu
             quan sát dài hơn thế nên gõ xong không đọc lại được cả câu. -->
        <div class="grid lg:grid-cols-5 gap-6 items-start">
        <div class="lg:col-span-3">

        @if (mode === 'view') {
          <app-dossier-view
            [businessModel]="businessModel"
            [moats]="moats"
            [riskFactors]="riskFactors"
            [notes]="notes"></app-dossier-view>
        } @else {

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
          <div *ngFor="let m of moats; let i = index" class="mb-3">
            <label class="block text-xs font-medium text-gray-600 mb-1">Lợi thế {{ i + 1 }}</label>
            <div class="flex items-center gap-2">
              <input [(ngModel)]="m.description" type="text" placeholder="VD: Mạng lưới phân phối phủ 63 tỉnh thành"
                class="flex-1 px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500">
              <button (click)="removeMoat(i)" class="p-1.5 text-red-400 hover:text-red-600 hover:bg-red-50 rounded">✕</button>
            </div>
          </div>
        </div>

        <!-- Risk Factors -->
        <div class="bg-white rounded-lg shadow p-5 mb-6">
          <div class="flex items-center justify-between mb-3">
            <h2 class="text-sm font-semibold text-gray-700">Yếu tố rủi ro (hạng 1 = nguy hiểm nhất)</h2>
            <button (click)="addRiskFactor()" class="px-3 py-1 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded-lg">+ Thêm yếu tố</button>
          </div>
          <div *ngIf="riskFactors.length === 0" class="text-xs text-gray-400 italic">Chưa có yếu tố rủi ro nào</div>
          <div *ngFor="let r of riskFactors; let i = index" class="border border-gray-200 rounded-lg p-4 mb-3">
            <div class="flex items-start gap-3">
              <div class="flex flex-col items-center gap-0.5 pt-6">
                <button (click)="moveUp(i)" [disabled]="i === 0" class="text-gray-400 hover:text-gray-700 disabled:opacity-30 leading-none">▲</button>
                <span class="px-1.5 py-0.5 rounded text-xs font-bold"
                  [class.bg-red-600]="r.rank === 1" [class.text-white]="r.rank === 1"
                  [class.bg-gray-200]="r.rank !== 1" [class.text-gray-600]="r.rank !== 1">#{{ r.rank }}</span>
                <button (click)="moveDown(i)" [disabled]="i === riskFactors.length - 1" class="text-gray-400 hover:text-gray-700 disabled:opacity-30 leading-none">▼</button>
              </div>
              <div class="flex-1 min-w-0 space-y-3">
                <div>
                  <label class="block text-xs font-medium text-gray-600 mb-1">Mô tả rủi ro</label>
                  <textarea [(ngModel)]="r.description" rows="2"
                    placeholder="VD: Nợ xấu nhóm khách hàng cá nhân tăng nhanh hơn tốc độ trích lập dự phòng"
                    class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500"></textarea>
                </div>
                <div>
                  <label class="block text-xs font-medium text-gray-600 mb-1">
                    Dấu hiệu quan sát được <span class="text-red-500">*</span>
                  </label>
                  <textarea [(ngModel)]="r.observableSignal" rows="2" (blur)="r.touched = true"
                    placeholder="VD: biên lợi nhuận gộp giảm 2 quý liên tiếp"
                    class="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-blue-500"
                    [class.border-red-400]="showSignalError(r)" [class.border-gray-300]="!showSignalError(r)"></textarea>
                  <p *ngIf="showSignalError(r)" class="text-xs text-red-500 mt-1">Bắt buộc phải có dấu hiệu quan sát được</p>
                </div>
                <div class="flex items-center justify-between gap-4 flex-wrap">
                  <div class="flex items-center gap-4 flex-wrap">
                    <select [(ngModel)]="r.suggestedTrigger" class="px-2 py-1 border border-gray-300 rounded-lg text-xs">
                      <option [ngValue]="null">-- Kịch bản vô hiệu hoá --</option>
                      <option *ngFor="let t of triggerOptions" [ngValue]="t.value">{{ t.label }}</option>
                    </select>
                    <label class="flex items-center gap-1.5 text-xs text-gray-600"
                      [title]="dealBreakerDisabled(i) ? 'Đã có yếu tố hủy diệt khác — bỏ tick ở đó trước' : ''">
                      <input type="checkbox" [(ngModel)]="r.isDealBreaker" [disabled]="dealBreakerDisabled(i)">
                      Yếu tố hủy diệt
                    </label>
                  </div>
                  <button (click)="removeRiskFactor(i)"
                    class="px-2 py-1 text-xs text-red-500 hover:text-red-700 hover:bg-red-50 rounded">✕ Xóa yếu tố</button>
                </div>
                <p class="text-xs text-gray-400">Chỉ được đánh dấu MỘT yếu tố hủy diệt — cái mà nếu xảy ra thì luận điểm sụp hoàn toàn.</p>
              </div>
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

        <div class="flex justify-end items-center gap-3 mb-8 flex-wrap">
          @if (missingSignalCount() > 0) {
            <span class="text-xs text-red-500 mr-auto" data-testid="missing-signal-summary">
              Còn {{ missingSignalCount() }} yếu tố thiếu dấu hiệu quan sát
            </span>
          }
          @if (exists) {
            <button (click)="cancelEdit()" [disabled]="saving" data-testid="btn-cancel"
              class="px-4 py-2.5 text-gray-600 hover:text-gray-800 hover:bg-gray-100 rounded-lg text-sm font-medium">
              Hủy
            </button>
          }
          <button (click)="save()" [disabled]="saving"
            class="px-5 py-2.5 bg-gray-700 hover:bg-gray-800 disabled:bg-gray-300 text-white rounded-lg text-sm font-medium">
            {{ saving ? 'Đang lưu...' : 'Lưu' }}
          </button>
        </div>

        }

        </div><!-- /cột trái -->

          <div class="lg:col-span-2">
            <app-fundamentals-panel [symbol]="symbol" (dataLoaded)="fundamentals = $event"></app-fundamentals-panel>
          </div>
        </div><!-- /grid -->

        <!-- Sign — cuối trang, sau nội dung, không cạnh nút Lưu -->
        <div class="border-t pt-6 pb-10 text-center">
          <button (click)="sign()" [disabled]="signing || !canSign()"
            class="px-8 py-3 bg-emerald-600 hover:bg-emerald-700 disabled:bg-gray-300 text-white rounded-lg font-semibold">
            {{ signing ? 'Đang ký...' : signLabel() }}
          </button>
          <p *ngIf="!exists && !signing" class="text-xs text-red-500 mt-2">Cần lưu hồ sơ trước khi ký.</p>
          <p *ngIf="exists && isDirty() && !signing" class="text-xs text-red-500 mt-2" data-testid="dirty-sign-hint">
            Còn thay đổi chưa lưu — bấm Lưu trước, vì chữ ký đóng vào bản đang nằm trên server.
          </p>
          <p class="text-xs text-gray-400 mt-2">Ký xác nhận rằng bạn — con người — đã đọc và chịu trách nhiệm với nội dung hồ sơ này.</p>
        </div>
      </div>

      <!-- Dán từ AI. Overlay z-[60] vì header sticky đang ở z-50. -->
      @if (showPaste) {
        <div class="fixed inset-0 z-[60] bg-black/50 flex items-center justify-center p-4" data-testid="paste-modal">
          <div class="bg-white rounded-lg shadow-xl w-full max-w-2xl p-6">
            <h2 class="text-lg font-semibold text-gray-800 mb-1">Dán nội dung từ AI</h2>
            <p class="text-xs text-gray-500 mb-3">
              Dán nguyên văn câu trả lời. Nội dung sẽ được đổ vào form để bạn đọc lại —
              <strong>không tự lưu và không tự ký</strong>.
            </p>
            <textarea [(ngModel)]="pasteText" rows="10" data-testid="paste-input"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm font-mono focus:ring-2 focus:ring-blue-500"
              placeholder="Dán cả câu trả lời, kể cả phần giải thích — chỉ khối JSON cuối cùng được đọc."></textarea>
            @if (pasteError) {
              <p class="text-sm text-red-600 mt-2" data-testid="paste-error">{{ pasteError }}</p>
            }
            <div class="flex justify-end items-center gap-3 mt-4">
              <button (click)="closePaste()" class="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-lg text-sm font-medium">Hủy</button>
              <button (click)="applyPaste()" [disabled]="!pasteText.trim()" data-testid="btn-apply-paste"
                class="px-5 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 text-white rounded-lg text-sm font-medium">
                Đổ vào form
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class CompanyDossierDetailComponent implements OnInit {
  readonly MIN_BUSINESS_MODEL_LEN = MIN_BUSINESS_MODEL_LEN;

  symbol = '';
  businessModel = '';
  moats: { description: string }[] = [];
  riskFactors: RiskFactorRow[] = [];
  notes: string | null = '';
  reviewedAt: string | null = null;
  confirmedAt: string | null = null;
  agentDraftedAt: string | null = null;
  freshness: DossierFreshness = 'Unconfirmed';

  loading = false;
  saving = false;
  signing = false;
  exists = false;

  /**
   * Hồ sơ đã viết xong thì phần lớn lần mở trang là để ĐỌC LẠI trước khi vào lệnh, nên mặc định là
   * 'view'. Chỉ rơi thẳng vào 'edit' khi thật sự không có gì để đọc (mã chưa có hồ sơ), hoặc khi
   * người dùng bị cổng đá sang đây — lúc đó việc cần làm là viết, không phải đọc.
   */
  mode: 'view' | 'edit' = 'edit';

  /** Đã bấm Lưu lần nào chưa — mốc để bật các lỗi mà người dùng chưa chạm tới ô. */
  saveAttempted = false;

  fundamentals: CompanyFundamentals | null = null;
  copied = false;
  showPaste = false;
  pasteText = '';
  pasteError: string | null = null;

  /** Ảnh chụp nội dung lúc vào 'edit' — để Hủy biết có gì để mất không, và trả lại đúng bản cũ. */
  private snapshot: DossierEditable | null = null;

  // Ngữ cảnh size (nếu được forward qua query params) — chỉ để hiển thị gợi ý, không gọi gate-status.
  private quantity: number | null = null;
  private entryPrice: number | null = null;
  private accountBalance: number | null = null;
  private returnTo: string | null = null;
  private forceEdit = false;

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

    // Cổng đá sang đây (returnTo) hoặc link ?edit=1 = người dùng tới để VIẾT, bỏ qua bản đọc.
    this.forceEdit = qp['edit'] === '1' || this.returnTo === 'trade-plan';

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
        this.mode = this.forceEdit ? 'edit' : 'view';
        this.takeSnapshot();
        this.loading = false;
      },
      error: () => {
        // 404 — mã chưa có hồ sơ: không có gì để đọc, vào thẳng form trống
        this.exists = false;
        this.mode = 'edit';
        this.loading = false;
      },
    });
  }

  // --- Chế độ xem / sửa ---

  startEdit(): void {
    this.takeSnapshot();
    this.mode = 'edit';
  }

  /**
   * Bỏ sửa. Có thay đổi chưa lưu thì hỏi trước — và khi người dùng đồng ý thì phải TRẢ LẠI bản cũ,
   * không chỉ đổi mode: giữ nguyên giá trị đã gõ rồi hiện bản đọc là hiện một thứ chưa hề được lưu.
   */
  cancelEdit(): void {
    if (this.isDirty() && !confirm('Bỏ các thay đổi chưa lưu?')) return;
    this.restoreSnapshot();
    this.mode = 'view';
  }

  isDirty(): boolean {
    if (!this.snapshot) return false;
    return serializeEditable(this.currentContent()) !== serializeEditable(this.snapshot);
  }

  private currentContent(): DossierEditable {
    return {
      businessModel: this.businessModel,
      moats: this.moats,
      riskFactors: this.riskFactors,
      notes: this.notes,
    };
  }

  private takeSnapshot(): void {
    this.snapshot = structuredClone(this.currentContent());
  }

  private restoreSnapshot(): void {
    if (!this.snapshot) return;
    const restored = structuredClone(this.snapshot);
    this.businessModel = restored.businessModel;
    this.moats = restored.moats;
    this.riskFactors = restored.riskFactors;
    this.notes = restored.notes;
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

  /**
   * Đỏ chỉ sau khi người dùng đã rời ô, hoặc sau lần bấm Lưu đầu tiên. Bật đỏ ngay lúc vừa
   * "+ Thêm yếu tố" là báo sai trước khi người ta kịp làm gì — nhìn thấy lần thứ ba là hết được đọc.
   */
  showSignalError(r: RiskFactorRow): boolean {
    return (r.touched === true || this.saveAttempted) && !r.observableSignal.trim();
  }

  /** Đếm để hiện cạnh nút Lưu — biết vì sao chưa ký được mà không phải cuộn cả trang đi tìm. */
  missingSignalCount(): number {
    if (!this.saveAttempted) return 0;
    return this.riskFactors.filter((r) => !r.observableSignal.trim()).length;
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

  // --- Cầu nối với AI ngoài (không nối MCP) ---

  aiPromptText(): string {
    return buildAiPrompt(
      {
        symbol: this.symbol,
        businessModel: this.businessModel,
        moats: this.moats,
        riskFactors: this.riskFactors,
        notes: this.notes,
      },
      this.fundamentals,
    );
  }

  copyForAi(): void {
    const text = this.aiPromptText();
    navigator.clipboard.writeText(text).then(
      () => {
        this.copied = true;
        setTimeout(() => (this.copied = false), 2000);
      },
      () => this.notification.error('Không sao chép được', 'Trình duyệt từ chối quyền ghi clipboard.'),
    );
  }

  openPaste(): void {
    this.pasteText = '';
    this.pasteError = null;
    this.showPaste = true;
  }

  closePaste(): void {
    this.showPaste = false;
  }

  /**
   * Chỉ đổ vào form. KHÔNG lưu, KHÔNG ký — chữ ký là lớp chịu trách nhiệm duy nhất và nó phải do
   * người đọc xong bấm, không phải hệ quả của một cú dán.
   */
  applyPaste(): void {
    const result = parseAiPayload(this.pasteText, this.symbol);
    if (!result.ok) {
      this.pasteError = result.error;
      return;
    }

    this.businessModel = result.value.businessModel;
    this.moats = result.value.moats;
    this.riskFactors = result.value.riskFactors;
    this.notes = result.value.notes;
    this.mode = 'edit';
    this.showPaste = false;

    const tail = result.warnings.length ? ` ${result.warnings.join(' ')}` : '';
    this.notification.success('Đã đổ vào form', `Đọc lại rồi bấm Lưu.${tail}`);
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

  /**
   * Phải tồn tại trên server mới cho ký — backend confirm() trả 404 nếu chưa Lưu lần nào.
   * Và không cho ký khi form còn thay đổi chưa lưu: confirm() đóng dấu vào BẢN ĐANG NẰM TRÊN SERVER,
   * nên ký lúc màn hình đang hiện nội dung khác là ký một thứ mình không đọc. Cửa "Dán từ AI" biến
   * tình huống này từ hiếm thành thường.
   */
  canSign(): boolean {
    return this.exists && !this.isDirty() && this.businessModel.trim().length >= MIN_BUSINESS_MODEL_LEN;
  }

  // --- Save / Sign actions ---

  save(): void {
    if (!this.symbol || this.saving) return;
    this.saveAttempted = true;
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
          this.takeSnapshot();
          this.mode = 'view';
          this.notification.success('Hồ sơ công ty', 'Đã lưu');
        },
        error: (err) => {
          this.saving = false;
          this.notification.error('Không lưu được hồ sơ', serverMessage(err));
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
