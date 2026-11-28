import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { INDICATOR_GUIDES } from '../../indicators/indicator-guides';
import { TaGroup } from '../../indicators/ta-group-layers';

/** Khoá localStorage nhớ khối "Cách đọc" đang mở hay thu gọn ('1' mở, '0' thu gọn); dùng chung mọi trang. */
const GUIDE_OPEN_KEY = 'ta-guide-open';

/**
 * Khối "📖 Cách đọc nhóm …" dưới biểu đồ chỉ báo. Không hiện khi chưa chọn nhóm. Trang cha tính sẵn các dòng
 * "Đọc nhanh hiện tại" (readNow) vì chúng cần chuỗi và phân tích của trang. Đặt NGOÀI khối dính.
 */
@Component({
  selector: 'app-indicator-guide',
  standalone: true,
  imports: [RouterLink],
  host: { class: 'block' },
  template: `
    @if (activeGuide; as guide) {
      <section class="mb-4 rounded-lg border border-blue-100 bg-blue-50/40" data-testid="ta-guide">
        <button type="button" class="w-full flex items-center justify-between gap-2 px-4 py-2 text-left"
          [attr.aria-expanded]="guideOpen" (click)="toggleGuide()" data-testid="ta-guide-toggle">
          <span class="text-sm font-bold text-gray-800">📖 Cách đọc nhóm {{ guide.title }}</span>
          <span class="text-xs text-blue-600">{{ guideOpen ? 'Thu gọn' : 'Mở ra' }}</span>
        </button>
        @if (guideOpen) {
          <div class="px-4 pb-4 text-sm text-gray-700 space-y-3" data-testid="ta-guide-body">
            <p class="text-gray-600">{{ guide.oneLine }}</p>
            @if (readNowLines.length > 0) {
              <div class="rounded-lg bg-white border border-blue-100 px-3 py-2" data-testid="ta-guide-now">
                <div class="text-xs font-semibold text-blue-700 mb-1">Đọc nhanh hiện tại</div>
                <ul class="list-disc pl-5 space-y-0.5">
                  @for (line of readNowLines; track line) { <li>{{ line }}</li> }
                </ul>
              </div>
            }
            @for (item of guide.items; track item.name) {
              <div class="rounded-lg bg-white border border-gray-100 px-3 py-2" data-testid="ta-guide-item">
                <div class="font-semibold text-gray-900">{{ item.name }}</div>
                <p class="mt-0.5">{{ item.what }}</p>
                <p class="mt-1 text-xs text-gray-500"><span class="font-semibold">Nhìn trên biểu đồ:</span> {{ item.onChart }}</p>
                <div class="mt-1 text-xs font-semibold text-gray-600">Cách đọc</div>
                <ul class="list-disc pl-5 space-y-0.5">
                  @for (line of item.howToRead; track line) { <li>{{ line }}</li> }
                </ul>
                <p class="mt-1 text-xs text-amber-700"><span class="font-semibold">Bẫy hay gặp:</span> {{ item.pitfall }}</p>
              </div>
            }
            <p class="text-xs text-gray-500">
              Chỉ báo mô tả quá khứ, không đảm bảo tương lai; dùng kết hợp nhiều chỉ báo, không ra quyết định chỉ từ một chỉ báo.
              <a routerLink="/help" [queryParams]="{ topic: 'phan-tich-thi-truong' }" class="text-blue-600 underline"
                data-testid="ta-guide-help-link">Xem hướng dẫn đầy đủ (Phân tích thị trường)</a>.
            </p>
          </div>
        }
      </section>
    }
  `
})
export class IndicatorGuideComponent {
  @Input() group: TaGroup | null = null;
  @Input() readNowLines: string[] = [];

  guideOpen = IndicatorGuideComponent.readGuideOpen();

  get activeGuide() {
    return this.group ? INDICATOR_GUIDES[this.group] : null;
  }

  toggleGuide(): void {
    this.guideOpen = !this.guideOpen;
    try { localStorage.setItem(GUIDE_OPEN_KEY, this.guideOpen ? '1' : '0'); } catch { /* bị chặn: chỉ không nhớ được */ }
  }

  private static readGuideOpen(): boolean {
    try { return localStorage.getItem(GUIDE_OPEN_KEY) !== '0'; } catch { return true; }
  }
}
