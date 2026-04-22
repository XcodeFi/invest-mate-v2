import { Component, OnInit, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  PersonalFinanceService,
  NetWorthSummaryDto,
  FinancialAccountDto,
  FinancialAccountType,
  GoldBrand,
  GoldType,
  GoldPriceDto,
  UpsertFinancialAccountRequest,
  UpsertFinancialProfileRequest,
} from '../../core/services/personal-finance.service';
import { VndCurrencyPipe } from '../../shared/pipes/vnd-currency.pipe';
import { NumMaskDirective } from '../../shared/directives/num-mask.directive';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-personal-finance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, VndCurrencyPipe, NumMaskDirective],
  template: `
    <div class="max-w-5xl mx-auto px-4 py-6 space-y-6">
      <!-- Header -->
      <div class="bg-gray-800 rounded-xl px-5 py-4 border border-gray-700 flex items-center justify-between">
        <div>
          <h1 class="text-xl font-bold text-white flex items-center gap-2">
            💰 Tài chính cá nhân
          </h1>
          <p class="text-sm text-gray-400 mt-1">
            Tổng quan tài sản, vàng, tiết kiệm + nguyên tắc sức khỏe tài chính
          </p>
        </div>
        <span *ngIf="loading" class="text-xs text-gray-400">Đang tải...</span>
      </div>

      <!-- Onboarding: chưa có profile -->
      <div *ngIf="!loading && summary && !summary.hasProfile"
           class="bg-gradient-to-br from-blue-900/40 to-indigo-900/40 rounded-xl border border-blue-700/50 p-6 space-y-4">
        <div class="flex items-center gap-3">
          <span class="text-3xl">🚀</span>
          <div>
            <h2 class="text-lg font-bold text-white">Thiết lập tài chính cá nhân</h2>
            <p class="text-sm text-gray-300">Nhập chi tiêu trung bình/tháng để tính quỹ dự phòng cần thiết.</p>
          </div>
        </div>
        <div class="space-y-2">
          <label class="text-sm text-gray-300">Chi tiêu trung bình/tháng</label>
          <input type="text" inputmode="numeric" appNumMask [(ngModel)]="onboardingMonthlyExpense"
                 placeholder="VD: 20.000.000"
                 class="w-full bg-gray-700 text-white rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500" />
        </div>
        <button (click)="createProfile()" [disabled]="!onboardingMonthlyExpense || saving"
                class="bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 text-white text-sm font-medium rounded-lg px-6 py-2 transition-colors disabled:opacity-50">
          {{ saving ? 'Đang tạo...' : 'Bắt đầu' }}
        </button>
      </div>

      <!-- Has profile: render full UI -->
      <ng-container *ngIf="summary && summary.hasProfile">
        <!-- Net Worth Cards -->
        <div class="grid grid-cols-2 md:grid-cols-5 gap-3">
          <div class="bg-gray-800 rounded-xl p-4 border border-gray-700 col-span-2 md:col-span-1">
            <div class="text-xs text-gray-400 mb-1">Tổng tài sản</div>
            <div class="text-lg font-bold text-white">{{ summary.totalAssets | vndCurrency }}</div>
          </div>
          <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
            <div class="text-xs text-gray-400 mb-1">📈 Chứng khoán</div>
            <div class="text-sm font-bold text-white">{{ summary.securitiesValue | vndCurrency }}</div>
            <div class="text-[10px] text-gray-500 mt-0.5">Auto-sync từ danh mục</div>
          </div>
          <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
            <div class="text-xs text-gray-400 mb-1">🪙 Vàng</div>
            <div class="text-sm font-bold text-white">{{ summary.goldTotal | vndCurrency }}</div>
          </div>
          <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
            <div class="text-xs text-gray-400 mb-1">🏦 Tiết kiệm + 🛡️ Dự phòng</div>
            <div class="text-sm font-bold text-white">{{ (summary.savingsTotal + summary.emergencyTotal) | vndCurrency }}</div>
          </div>
          <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
            <div class="text-xs text-gray-400 mb-1">💵 Nhàn rỗi</div>
            <div class="text-sm font-bold text-white">{{ summary.idleCashTotal | vndCurrency }}</div>
          </div>
        </div>

        <!-- Health Score -->
        <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
          <div class="flex items-center justify-between mb-3">
            <h2 class="font-semibold text-white text-sm">Sức khỏe tài chính</h2>
            <span class="text-2xl font-bold"
                  [class.text-emerald-400]="summary.healthScore >= 80"
                  [class.text-amber-400]="summary.healthScore >= 50 && summary.healthScore < 80"
                  [class.text-red-400]="summary.healthScore < 50">
              {{ summary.healthScore }}/100
            </span>
          </div>
          <div class="w-full bg-gray-700 rounded-full h-3 overflow-hidden">
            <div class="h-3 transition-all duration-500"
                 [class.bg-emerald-500]="summary.healthScore >= 80"
                 [class.bg-amber-500]="summary.healthScore >= 50 && summary.healthScore < 80"
                 [class.bg-red-500]="summary.healthScore < 50"
                 [style.width.%]="summary.healthScore"></div>
          </div>
          <div class="mt-4 space-y-2">
            <div *ngFor="let rule of summary.ruleChecks"
                 class="flex items-center justify-between bg-gray-900/50 rounded-lg px-3 py-2 border"
                 [class.border-emerald-700]="rule.isPassing"
                 [class.border-red-700]="!rule.isPassing">
              <div class="flex items-center gap-2">
                <span *ngIf="rule.isPassing" class="text-emerald-400">✓</span>
                <span *ngIf="!rule.isPassing" class="text-red-400">✗</span>
                <span class="text-sm text-gray-200">{{ rule.description }}</span>
              </div>
              <span class="text-xs text-gray-400">
                {{ rule.currentValue | vndCurrency }} / {{ rule.requiredValue | vndCurrency }}
              </span>
            </div>
          </div>
        </div>

        <!-- Accounts Management -->
        <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
          <div class="flex items-center justify-between mb-4">
            <h2 class="font-semibold text-white text-sm">Tài khoản</h2>
            <button (click)="openNewAccountForm()"
                    class="bg-blue-600 hover:bg-blue-700 text-white text-xs font-medium rounded-lg px-3 py-1.5 transition-colors">
              + Thêm tài khoản
            </button>
          </div>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div *ngFor="let account of summary.accounts"
                 class="bg-gray-900 rounded-lg p-3 border border-gray-700 transition-colors"
                 [class.cursor-pointer]="account.type !== FinancialAccountType.Securities"
                 [class.hover:border-blue-600]="account.type !== FinancialAccountType.Securities"
                 (click)="onCardClick(account)">
              <div class="flex items-start justify-between">
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 mb-1">
                    <span class="text-lg">{{ iconFor(account.type) }}</span>
                    <span class="text-sm font-medium text-white truncate">{{ account.name }}</span>
                  </div>
                  <div class="text-xs text-gray-400 mb-1">{{ typeLabel(account.type) }}</div>
                  <div class="text-base font-bold text-white">{{ account.balance | vndCurrency }}</div>
                  <div *ngIf="account.type === FinancialAccountType.Gold && account.goldQuantity"
                       class="text-[10px] text-gray-500 mt-0.5">
                    {{ account.goldQuantity }} lượng {{ brandLabel(account.goldBrand) }} — {{ goldTypeLabelEnum(account.goldType) }}
                  </div>
                  <div *ngIf="account.type === FinancialAccountType.Savings && account.interestRate"
                       class="text-[10px] text-gray-500 mt-0.5">Lãi suất: {{ account.interestRate }}%/năm</div>
                  <div *ngIf="account.note" class="text-[10px] text-gray-500 mt-0.5">{{ account.note }}</div>
                </div>
                <span *ngIf="account.type !== FinancialAccountType.Securities"
                      class="text-xs text-gray-500 ml-2 whitespace-nowrap">Sửa ›</span>
                <span *ngIf="account.type === FinancialAccountType.Securities"
                      class="text-[10px] text-gray-500 ml-2 whitespace-nowrap">Auto-sync</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Settings -->
        <details class="bg-gray-800 rounded-xl border border-gray-700 group">
          <summary class="p-5 cursor-pointer text-sm font-semibold text-white flex items-center justify-between list-none">
            <span>⚙️ Thiết lập (chi tiêu hàng tháng + ngưỡng nguyên tắc)</span>
            <span class="text-gray-400 group-open:rotate-180 transition-transform">▼</span>
          </summary>
          <div class="p-5 pt-0 space-y-3">
            <div>
              <label class="text-xs text-gray-400 block mb-1">Chi tiêu trung bình/tháng</label>
              <input type="text" inputmode="numeric" appNumMask [(ngModel)]="settingsMonthlyExpense"
                     class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500" />
            </div>
            <div class="grid grid-cols-3 gap-2">
              <div>
                <label class="text-xs text-gray-400 block mb-1">Quỹ dự phòng (tháng)</label>
                <input type="number" min="1" max="24" [(ngModel)]="settingsEmergencyMonths"
                       class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500" />
              </div>
              <div>
                <label class="text-xs text-gray-400 block mb-1">Đầu tư tối đa (%)</label>
                <input type="number" min="0" max="100" step="0.5" [(ngModel)]="settingsMaxInvestment"
                       class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500" />
              </div>
              <div>
                <label class="text-xs text-gray-400 block mb-1">Tiết kiệm tối thiểu (%)</label>
                <input type="number" min="0" max="100" step="0.5" [(ngModel)]="settingsMinSavings"
                       class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500" />
              </div>
            </div>
            <button (click)="saveSettings()" [disabled]="saving"
                    class="bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors disabled:opacity-50">
              {{ saving ? 'Đang lưu...' : 'Lưu thiết lập' }}
            </button>
          </div>
        </details>
      </ng-container>

      <!-- Account Form Modal -->
      <div *ngIf="showAccountForm" class="fixed inset-0 bg-black/70 z-[60] flex items-start justify-center p-4 overflow-y-auto"
           (click)="closeAccountForm()">
        <div class="bg-gray-800 rounded-xl border border-gray-700 p-5 w-full max-w-lg mt-10 space-y-4"
             (click)="$event.stopPropagation()">
          <h3 class="text-lg font-bold text-white">
            {{ formAccountId ? 'Sửa tài khoản' : 'Thêm tài khoản mới' }}
          </h3>

          <div>
            <label class="text-xs text-gray-400 block mb-1">Loại tài khoản</label>
            <select [(ngModel)]="formType" (ngModelChange)="onTypeChange()" [disabled]="!!formAccountId"
                    class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-50">
              <option [ngValue]="FinancialAccountType.Savings">🏦 Tiết kiệm</option>
              <option [ngValue]="FinancialAccountType.Emergency">🛡️ Dự phòng</option>
              <option [ngValue]="FinancialAccountType.IdleCash">💵 Nhàn rỗi</option>
              <option [ngValue]="FinancialAccountType.Gold">🪙 Vàng</option>
            </select>
          </div>

          <div>
            <label class="text-xs text-gray-400 block mb-1">Tên hiển thị</label>
            <input type="text" [(ngModel)]="formName"
                   [placeholder]="formType === FinancialAccountType.Gold ? 'VD: SJC Miếng của tôi' : 'VD: Tiết kiệm VCB'"
                   class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2 focus:outline-none focus:ring-1 focus:ring-blue-500" />
          </div>

          <!-- Gold-specific fields -->
          <ng-container *ngIf="formType === FinancialAccountType.Gold">
            <div class="flex items-center justify-between bg-gray-900 rounded-lg px-3 py-2">
              <span class="text-xs text-gray-300">Tự tính Balance từ giá vàng 24hmoney</span>
              <label class="relative inline-flex items-center cursor-pointer">
                <input type="checkbox" [(ngModel)]="formGoldAutoCalc" (ngModelChange)="onGoldAutoCalcToggle()" class="sr-only peer" />
                <div class="w-9 h-5 bg-gray-700 peer-focus:ring-2 peer-focus:ring-blue-500 rounded-full peer peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-blue-600"></div>
              </label>
            </div>

            <div *ngIf="formGoldAutoCalc" class="space-y-2">
              <div class="grid grid-cols-2 gap-2">
                <div>
                  <label class="text-xs text-gray-400 block mb-1">Thương hiệu</label>
                  <select [(ngModel)]="formGoldBrand" (ngModelChange)="updateGoldPreview()"
                          class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2">
                    <option [ngValue]="GoldBrand.SJC">SJC</option>
                    <option [ngValue]="GoldBrand.DOJI">DOJI</option>
                    <option [ngValue]="GoldBrand.PNJ">PNJ</option>
                    <option [ngValue]="GoldBrand.Other">Khác (BTMC/BTMH/...)</option>
                  </select>
                </div>
                <div>
                  <label class="text-xs text-gray-400 block mb-1">Loại</label>
                  <select [(ngModel)]="formGoldType" (ngModelChange)="updateGoldPreview()"
                          class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2">
                    <option [ngValue]="GoldType.Mieng">Vàng miếng</option>
                    <option [ngValue]="GoldType.Nhan">Vàng nhẫn</option>
                  </select>
                </div>
              </div>
              <div>
                <label class="text-xs text-gray-400 block mb-1">Số lượng (lượng)</label>
                <input type="number" min="0" step="0.01" [(ngModel)]="formGoldQuantity" (ngModelChange)="updateGoldPreview()"
                       placeholder="VD: 2"
                       class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2" />
              </div>
              <div *ngIf="goldPreviewBuyPrice !== null"
                   class="bg-blue-900/30 border border-blue-700/50 rounded-lg px-3 py-2 text-xs space-y-1">
                <div class="flex justify-between text-gray-300">
                  <span>Giá mua vào hiện tại:</span>
                  <span class="font-bold text-white">{{ goldPreviewBuyPrice | vndCurrency }} / lượng</span>
                </div>
                <div class="flex justify-between text-gray-300">
                  <span>Số dư tự tính:</span>
                  <span class="font-bold text-emerald-400">{{ goldPreviewBalance | vndCurrency }}</span>
                </div>
              </div>
              <div *ngIf="goldPreviewError" class="text-xs text-red-400">{{ goldPreviewError }}</div>
            </div>

            <div *ngIf="!formGoldAutoCalc">
              <label class="text-xs text-gray-400 block mb-1">Số dư nhập tay (VND)</label>
              <input type="text" inputmode="numeric" appNumMask [(ngModel)]="formBalance"
                     placeholder="VD: 340.000.000"
                     class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2" />
            </div>
          </ng-container>

          <!-- Balance for non-Gold -->
          <div *ngIf="formType !== FinancialAccountType.Gold && formType !== FinancialAccountType.Securities">
            <label class="text-xs text-gray-400 block mb-1">Số dư (VND)</label>
            <input type="text" inputmode="numeric" appNumMask [(ngModel)]="formBalance"
                   class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2" />
          </div>

          <div *ngIf="formType === FinancialAccountType.Savings">
            <label class="text-xs text-gray-400 block mb-1">Lãi suất (%/năm, optional)</label>
            <input type="number" min="0" max="30" step="0.1" [(ngModel)]="formInterestRate"
                   class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2" />
          </div>

          <div *ngIf="formType === FinancialAccountType.Securities" class="bg-gray-900/50 rounded-lg px-3 py-2 text-xs text-gray-400">
            ℹ️ Giá trị Chứng khoán tự đồng bộ từ danh mục đầu tư — không cần nhập tay.
          </div>

          <div>
            <label class="text-xs text-gray-400 block mb-1">Ghi chú (optional)</label>
            <input type="text" [(ngModel)]="formNote" maxlength="200"
                   class="w-full bg-gray-700 text-white text-sm rounded-lg px-3 py-2" />
          </div>

          <div class="flex gap-2 pt-2">
            <button (click)="closeAccountForm()"
                    class="bg-gray-700 hover:bg-gray-600 text-gray-300 text-sm font-medium rounded-lg px-4 py-2 transition-colors">
              Hủy
            </button>
            <button *ngIf="formCanDelete" (click)="deleteCurrentAccount()" [disabled]="saving"
                    class="bg-red-700 hover:bg-red-600 disabled:bg-gray-600 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors disabled:opacity-50">
              Xóa
            </button>
            <button (click)="submitAccountForm()" [disabled]="saving"
                    class="flex-1 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-600 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors disabled:opacity-50">
              {{ saving ? 'Đang lưu...' : 'Lưu' }}
            </button>
          </div>
          <p *ngIf="formAccountId && !formCanDelete && formType !== FinancialAccountType.Securities"
             class="text-[10px] text-gray-500 text-center">
            Tài khoản có số dư &gt; 0 không thể xóa. Đặt số dư về 0 trước.
          </p>
        </div>
      </div>
    </div>
  `,
})
export class PersonalFinanceComponent implements OnInit {
  private finance = inject(PersonalFinanceService);
  private notify = inject(NotificationService);

  // Expose enum refs to template
  readonly FinancialAccountType = FinancialAccountType;
  readonly GoldBrand = GoldBrand;
  readonly GoldType = GoldType;

  summary: NetWorthSummaryDto | null = null;
  loading = false;
  saving = false;

  // Onboarding
  onboardingMonthlyExpense: number | null = null;

  // Settings (loaded from profile)
  settingsMonthlyExpense: number | null = null;
  settingsEmergencyMonths: number | null = null;
  settingsMaxInvestment: number | null = null;
  settingsMinSavings: number | null = null;

  // Account form state
  showAccountForm = false;
  formAccountId: string | null = null;
  formType: FinancialAccountType = FinancialAccountType.Savings;
  formName = '';
  formBalance: number | null = null;
  formInterestRate: number | null = null;
  formNote = '';
  formGoldAutoCalc = true;
  formGoldBrand: GoldBrand = GoldBrand.SJC;
  formGoldType: GoldType = GoldType.Mieng;
  formGoldQuantity: number | null = null;

  // Gold price cache (fetched once per page open)
  private goldPrices: GoldPriceDto[] = [];
  goldPreviewBuyPrice: number | null = null;
  goldPreviewBalance: number | null = null;
  goldPreviewError: string | null = null;

  // Delete eligibility for current edit target. Securities never deletable; others require Balance = 0.
  formCanDelete = false;
  private editingAccountRef: FinancialAccountDto | null = null;

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.showAccountForm) this.closeAccountForm();
  }

  onCardClick(account: FinancialAccountDto): void {
    if (account.type === FinancialAccountType.Securities) return; // auto-sync, không edit
    this.openEditAccountForm(account);
  }

  ngOnInit(): void {
    this.loadSummary();
  }

  private loadSummary(): void {
    this.loading = true;
    this.finance.getSummary().subscribe({
      next: (s) => {
        this.summary = s;
        if (s.hasProfile) {
          this.settingsMonthlyExpense = s.monthlyExpense;
          // Find rule defaults from ruleChecks — for display only. Pull from profile ideally.
          // Since summary doesn't include rules directly, re-use values from associated rules we know defaults.
          this.loadProfileRules();
        }
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.notify.error('Lỗi', 'Không tải được dữ liệu tài chính cá nhân');
        console.error(err);
      },
    });
  }

  private loadProfileRules(): void {
    // Fetch profile to get rule thresholds (summary DTO doesn't include them for settings form).
    this.finance.getProfile().subscribe({
      next: (p) => {
        if (p) {
          this.settingsEmergencyMonths = p.rules.emergencyFundMonths;
          this.settingsMaxInvestment = p.rules.maxInvestmentPercent;
          this.settingsMinSavings = p.rules.minSavingsPercent;
        }
      },
    });
  }

  createProfile(): void {
    if (!this.onboardingMonthlyExpense || this.onboardingMonthlyExpense <= 0) return;
    this.saving = true;
    const req: UpsertFinancialProfileRequest = { monthlyExpense: this.onboardingMonthlyExpense };
    this.finance.upsertProfile(req).subscribe({
      next: () => {
        this.saving = false;
        this.notify.success('Thành công', 'Đã thiết lập profile tài chính cá nhân');
        this.loadSummary();
      },
      error: (err) => {
        this.saving = false;
        this.notify.error('Lỗi', err?.error?.message ?? 'Không tạo được profile');
      },
    });
  }

  saveSettings(): void {
    this.saving = true;
    const req: UpsertFinancialProfileRequest = {
      monthlyExpense: this.settingsMonthlyExpense ?? undefined,
      emergencyFundMonths: this.settingsEmergencyMonths ?? undefined,
      maxInvestmentPercent: this.settingsMaxInvestment ?? undefined,
      minSavingsPercent: this.settingsMinSavings ?? undefined,
    };
    this.finance.upsertProfile(req).subscribe({
      next: () => {
        this.saving = false;
        this.notify.success('Thành công', 'Đã lưu thiết lập');
        this.loadSummary();
      },
      error: (err) => {
        this.saving = false;
        this.notify.error('Lỗi', err?.error?.message ?? 'Không lưu được thiết lập');
      },
    });
  }

  // ── Account form ──────────────────────────────────────────────────────────

  openNewAccountForm(): void {
    this.formAccountId = null;
    this.editingAccountRef = null;
    this.formCanDelete = false;
    this.formType = FinancialAccountType.Savings;
    this.formName = '';
    this.formBalance = null;
    this.formInterestRate = null;
    this.formNote = '';
    this.formGoldAutoCalc = true;
    this.formGoldBrand = GoldBrand.SJC;
    this.formGoldType = GoldType.Mieng;
    this.formGoldQuantity = null;
    this.goldPrices = []; // Invalidate cache — re-fetch on Gold form open cho fresh price.
    this.resetGoldPreview();
    this.showAccountForm = true;
  }

  openEditAccountForm(account: FinancialAccountDto): void {
    this.formAccountId = account.id;
    this.editingAccountRef = account;
    // Không xóa được nếu: Securities (auto-sync) hoặc Balance > 0
    this.formCanDelete = account.type !== FinancialAccountType.Securities && (account.balance ?? 0) <= 0;
    this.formType = account.type;
    this.formName = account.name;
    this.formBalance = account.balance ?? null;
    this.formInterestRate = account.interestRate ?? null;
    this.formNote = account.note ?? '';
    // Gold fields
    const hasGold = account.goldBrand != null && account.goldType != null && account.goldQuantity != null;
    this.formGoldAutoCalc = hasGold;
    this.formGoldBrand = account.goldBrand ?? GoldBrand.SJC;
    this.formGoldType = account.goldType ?? GoldType.Mieng;
    this.formGoldQuantity = account.goldQuantity ?? null;
    this.goldPrices = []; // Invalidate cache — re-fetch fresh mỗi lần mở form.
    this.resetGoldPreview();
    if (account.type === FinancialAccountType.Gold && hasGold) {
      this.ensureGoldPricesLoaded(() => this.updateGoldPreview());
    }
    this.showAccountForm = true;
  }

  closeAccountForm(): void {
    this.showAccountForm = false;
  }

  onTypeChange(): void {
    if (this.formType === FinancialAccountType.Gold) {
      this.ensureGoldPricesLoaded(() => this.updateGoldPreview());
    } else {
      this.resetGoldPreview();
    }
  }

  onGoldAutoCalcToggle(): void {
    if (this.formGoldAutoCalc) {
      this.ensureGoldPricesLoaded(() => this.updateGoldPreview());
    } else {
      this.resetGoldPreview();
    }
  }

  private ensureGoldPricesLoaded(cb: () => void): void {
    if (this.goldPrices.length > 0) { cb(); return; }
    this.finance.getGoldPrices().subscribe({
      next: (prices) => { this.goldPrices = prices; cb(); },
      error: (err) => {
        this.goldPreviewError = 'Không lấy được giá vàng từ 24hmoney';
        console.error(err);
      },
    });
  }

  updateGoldPreview(): void {
    this.goldPreviewError = null;
    this.goldPreviewBuyPrice = null;
    this.goldPreviewBalance = null;
    const match = this.goldPrices.find(p => p.brand === this.formGoldBrand && p.type === this.formGoldType);
    if (!match) {
      this.goldPreviewError = 'Không có giá cho combo này';
      return;
    }
    this.goldPreviewBuyPrice = match.buyPrice;
    if (this.formGoldQuantity && this.formGoldQuantity > 0) {
      this.goldPreviewBalance = this.formGoldQuantity * match.buyPrice;
    }
  }

  private resetGoldPreview(): void {
    this.goldPreviewBuyPrice = null;
    this.goldPreviewBalance = null;
    this.goldPreviewError = null;
  }

  submitAccountForm(): void {
    if (!this.formName.trim()) {
      this.notify.error('Thiếu thông tin', 'Vui lòng nhập tên tài khoản');
      return;
    }
    this.saving = true;

    const req: UpsertFinancialAccountRequest = {
      accountId: this.formAccountId,
      type: this.formType,
      name: this.formName.trim(),
      note: this.formNote.trim() || null,
    };

    if (this.formType === FinancialAccountType.Gold) {
      if (this.formGoldAutoCalc) {
        req.goldBrand = this.formGoldBrand;
        req.goldType = this.formGoldType;
        req.goldQuantity = this.formGoldQuantity ?? null;
        // Balance computed on backend
      } else {
        req.balance = this.formBalance ?? null;
      }
    } else if (this.formType === FinancialAccountType.Securities) {
      req.balance = 0; // Ignored — live value from PnLService
    } else {
      req.balance = this.formBalance ?? null;
      if (this.formType === FinancialAccountType.Savings) {
        req.interestRate = this.formInterestRate ?? null;
      }
    }

    this.finance.upsertAccount(req).subscribe({
      next: () => {
        this.saving = false;
        this.showAccountForm = false;
        this.notify.success('Thành công', 'Đã lưu tài khoản');
        this.loadSummary();
      },
      error: (err) => {
        this.saving = false;
        this.notify.error('Lỗi', err?.error?.message ?? 'Không lưu được tài khoản');
      },
    });
  }

  deleteCurrentAccount(): void {
    const account = this.editingAccountRef;
    if (!account || !this.formCanDelete) return;
    const label = this.typeLabel(account.type);
    if (!confirm(`Xóa tài khoản "${account.name}" (${label})?`)) return;
    this.saving = true;
    this.finance.removeAccount(account.id).subscribe({
      next: () => {
        this.saving = false;
        this.showAccountForm = false;
        this.notify.success('Thành công', 'Đã xóa tài khoản');
        this.loadSummary();
      },
      error: (err) => {
        this.saving = false;
        this.notify.error('Lỗi', err?.error?.message ?? 'Không xóa được tài khoản');
      },
    });
  }

  // ── Label helpers (delegated to service statics) ──────────────────────────

  typeLabel(type: FinancialAccountType): string { return PersonalFinanceService.accountTypeLabel(type); }
  iconFor(type: FinancialAccountType): string { return PersonalFinanceService.accountTypeIcon(type); }
  brandLabel(brand: GoldBrand | null | undefined): string {
    return brand == null ? '' : PersonalFinanceService.goldBrandLabel(brand);
  }
  goldTypeLabelEnum(type: GoldType | null | undefined): string {
    return type == null ? '' : PersonalFinanceService.goldTypeLabel(type);
  }
}
