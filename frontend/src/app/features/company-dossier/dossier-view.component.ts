import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { INVALIDATION_TRIGGER_LABELS, RiskFactorDto } from '../../core/services/company-dossier.service';

/**
 * Bản đọc của hồ sơ công ty. Hồ sơ viết xong thì phần lớn thời gian là để ĐỌC LẠI trước khi vào
 * lệnh, nên mặc định của trang là đây, không phải form. Thứ phải đập vào mắt trong 3 giây là rủi ro
 * hạng 1 và dấu hiệu quan sát của nó — hai thứ đó không được để màu xám mờ.
 */
@Component({
  selector: 'app-dossier-view',
  standalone: true,
  imports: [CommonModule],
  template: `
    <!-- Doanh nghiệp kiếm tiền bằng gì -->
    <div class="bg-white rounded-lg shadow p-5 mb-6">
      <h2 class="text-sm font-semibold text-gray-700 mb-2">Doanh nghiệp này kiếm tiền bằng gì?</h2>
      @if (businessModel.trim()) {
        <p class="text-gray-800 whitespace-pre-line leading-relaxed" data-testid="view-business-model">{{ businessModel }}</p>
      } @else {
        <p class="text-sm text-gray-400 italic">Chưa viết mô hình kinh doanh.</p>
      }
    </div>

    <!-- Moat -->
    <div class="bg-white rounded-lg shadow p-5 mb-6">
      <h2 class="text-sm font-semibold text-gray-700 mb-3">Lợi thế cạnh tranh (Moat)</h2>
      @if (visibleMoats().length) {
        <ul class="flex flex-wrap gap-2" data-testid="view-moats">
          @for (m of visibleMoats(); track $index) {
            <li class="px-3 py-1.5 bg-blue-50 text-blue-800 rounded-full text-sm">{{ m.description }}</li>
          }
        </ul>
      } @else {
        <p class="text-sm text-gray-400 italic">Chưa ghi lợi thế cạnh tranh nào.</p>
      }
    </div>

    <!-- Yếu tố rủi ro -->
    <div class="bg-white rounded-lg shadow p-5 mb-6">
      <h2 class="text-sm font-semibold text-gray-700 mb-3">Yếu tố rủi ro <span class="font-normal text-gray-400">(hạng 1 = nguy hiểm nhất)</span></h2>
      @if (riskFactors.length) {
        <div class="space-y-3" data-testid="view-risks">
          @for (r of riskFactors; track $index) {
            <div class="rounded-lg border p-4"
              [class.border-red-300]="r.isDealBreaker" [class.bg-red-50]="r.isDealBreaker"
              [class.border-gray-200]="!r.isDealBreaker">
              <div class="flex items-start gap-3">
                <span class="shrink-0 mt-0.5 px-2 py-0.5 rounded-md text-xs font-bold"
                  [class.bg-red-600]="r.rank === 1" [class.text-white]="r.rank === 1"
                  [class.bg-gray-200]="r.rank !== 1" [class.text-gray-600]="r.rank !== 1">#{{ r.rank }}</span>
                <div class="flex-1 min-w-0">
                  <p class="text-gray-800" [class.text-base]="r.rank === 1" [class.font-semibold]="r.rank === 1"
                    [class.text-sm]="r.rank !== 1">{{ r.description || '—' }}</p>
                  <p class="text-sm text-gray-700 mt-1">
                    <span class="text-gray-500">Dấu hiệu:</span> {{ r.observableSignal || '—' }}
                  </p>
                  <div class="flex items-center gap-2 mt-2 flex-wrap">
                    @if (r.isDealBreaker) {
                      <span class="px-2 py-0.5 rounded-full text-xs font-medium bg-red-600 text-white"
                        data-testid="deal-breaker-badge">Yếu tố hủy diệt</span>
                    }
                    @if (triggerLabel(r.suggestedTrigger); as label) {
                      <span class="px-2 py-0.5 rounded-full text-xs bg-gray-100 text-gray-600"
                        data-testid="trigger-chip">{{ label }}</span>
                    }
                  </div>
                </div>
              </div>
            </div>
          }
        </div>
      } @else {
        <p class="text-sm text-gray-400 italic">Chưa ghi yếu tố rủi ro nào.</p>
      }
    </div>

    <!-- Ghi chú: rỗng thì bỏ hẳn khối, không để lại tiêu đề trơ trọi -->
    @if (notes && notes.trim()) {
      <div class="bg-white rounded-lg shadow p-5 mb-6" data-testid="view-notes">
        <h2 class="text-sm font-semibold text-gray-700 mb-2">Ghi chú</h2>
        <p class="text-sm text-gray-700 whitespace-pre-line leading-relaxed">{{ notes }}</p>
      </div>
    }
  `,
})
export class DossierViewComponent {
  @Input() businessModel = '';
  @Input() moats: { description: string }[] = [];
  @Input() riskFactors: RiskFactorDto[] = [];
  @Input() notes: string | null = '';

  /** Moat rỗng là dòng người dùng thêm rồi bỏ trống — ở bản đọc nó chỉ là một chip trắng vô nghĩa. */
  visibleMoats(): { description: string }[] {
    return this.moats.filter((m) => m.description?.trim());
  }

  triggerLabel(trigger: string | null): string | null {
    if (!trigger) return null;
    return INVALIDATION_TRIGGER_LABELS[trigger] ?? trigger;
  }
}
