import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { CompanyDossierDto, CompanyDossierService } from '../../core/services/company-dossier.service';
import { vnDay } from '../../shared/indicators/vn-date';

const MAX_RISKS = 3;

/**
 * Thẻ "Luận điểm" cạnh biểu đồ của Dòng thời gian: tóm tắt hồ sơ công ty của mã để người dùng nhìn giá mà vẫn thấy luận điểm.
 * Thu gọn mặc định và tải lười: chỉ gọi `GET /company-dossier/:symbol` ở lần mở đầu tiên (404 là "chưa có hồ sơ", không phải lỗi).
 * "Điều kiện vô hiệu" lấy từ dấu hiệu quan sát được của các rủi ro đánh dấu "phá vỡ luận điểm" (isDealBreaker); không bịa thêm.
 */
@Component({
  selector: 'app-thesis-card',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="bg-white rounded-xl shadow-sm border border-gray-200" data-testid="thesis-card">
      <button type="button" data-testid="thesis-toggle" (click)="toggle()" [attr.aria-expanded]="open"
        class="w-full flex items-center justify-between px-4 py-3 text-left">
        <span class="font-semibold text-gray-800">🧭 Luận điểm</span>
        <span class="text-gray-500 text-sm" aria-hidden="true">{{ open ? '▲ Thu gọn' : '▼ Mở' }}</span>
      </button>

      @if (open) {
        <div class="px-4 pb-4 text-sm space-y-3" data-testid="thesis-body">
          @if (loading) {
            <p class="text-gray-500" data-testid="thesis-loading">Đang tải hồ sơ...</p>
          } @else if (notFound) {
            <div data-testid="thesis-empty">
              <p class="text-gray-700">Chưa có hồ sơ công ty cho {{ symbol }}.</p>
              <a [routerLink]="['/company-dossier', symbol]" data-testid="thesis-create-dossier" class="text-blue-600 underline">Tạo hồ sơ</a>
            </div>
          } @else if (failed) {
            <p class="text-red-600" role="alert" data-testid="thesis-error">Không tải được hồ sơ, thu gọn rồi mở lại để thử lại.</p>
          } @else if (dossier) {
            <div>
              <div class="text-xs uppercase tracking-wide text-gray-500">Mô hình kinh doanh</div>
              <p class="text-gray-800" data-testid="thesis-model">{{ dossier.businessModel }}</p>
            </div>
            <div>
              <div class="text-xs uppercase tracking-wide text-gray-500">Rủi ro hàng đầu</div>
              <ol class="list-decimal pl-5 text-gray-800">
                @for (r of topRisks; track r.rank) {
                  <li data-testid="thesis-risk">{{ r.description }}</li>
                }
              </ol>
            </div>
            <div>
              <div class="text-xs uppercase tracking-wide text-gray-500">Điều kiện vô hiệu luận điểm</div>
              @if (invalidation.length > 0) {
                <ul class="list-disc pl-5 text-gray-800">
                  @for (s of invalidation; track s) {
                    <li data-testid="thesis-invalidation">{{ s }}</li>
                  }
                </ul>
              } @else {
                <p class="text-gray-500" data-testid="thesis-no-invalidation">Hồ sơ chưa ghi rủi ro nào là điều kiện phá vỡ luận điểm.</p>
              }
            </div>
            <p class="text-xs text-gray-600" data-testid="thesis-signed">{{ signedText }}</p>
            <a [routerLink]="['/company-dossier', symbol]" data-testid="thesis-open-dossier" class="text-blue-600 underline">Mở hồ sơ đầy đủ →</a>
          }
        </div>
      }

      <div class="px-4 pb-3">
        <a routerLink="/trade-plan" [queryParams]="{ symbol: symbol }" data-testid="thesis-create-plan"
          class="inline-block px-3 py-1.5 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700">➕ Tạo kế hoạch</a>
      </div>
    </section>
  `
})
export class ThesisCardComponent {
  @Input({ required: true }) symbol = '';

  open = false;
  loading = false;
  failed = false;
  notFound = false;
  dossier: CompanyDossierDto | null = null;
  private requested = false;

  constructor(private service: CompanyDossierService) {}

  toggle(): void {
    this.open = !this.open;
    if (this.open) this.load();
  }

  /** Gọi lần đầu khi mở; sau lỗi (không phải 404) cho gọi lại ở lần mở kế tiếp. */
  private load(): void {
    if (this.requested || !this.symbol) return;
    this.requested = true;
    this.loading = true;
    this.failed = false;
    this.notFound = false;
    this.service.get(this.symbol).subscribe({
      next: d => { this.dossier = d; this.loading = false; },
      error: (e: unknown) => {
        this.loading = false;
        if (e instanceof HttpErrorResponse && e.status === 404) this.notFound = true;
        else { this.failed = true; this.requested = false; }
      }
    });
  }

  get topRisks() {
    return [...(this.dossier?.riskFactors ?? [])].sort((a, b) => a.rank - b.rank).slice(0, MAX_RISKS);
  }

  get invalidation(): string[] {
    return (this.dossier?.riskFactors ?? []).filter(r => r.isDealBreaker && r.observableSignal).map(r => r.observableSignal);
  }

  get signedText(): string {
    const d = this.dossier;
    if (!d?.confirmedAt) return 'Chưa ký';
    const [y, m, day] = vnDay(d.reviewedAt).split('-').map(Number);
    const [ty, tm, td] = vnDay(new Date()).split('-').map(Number);
    const days = Math.max(0, Math.round((Date.UTC(ty, tm - 1, td) - Date.UTC(y, m - 1, day)) / 86_400_000));
    return `Ký ${String(day).padStart(2, '0')}/${String(m).padStart(2, '0')}/${y} (${days} ngày trước)`;
  }
}
