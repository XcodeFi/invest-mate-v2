import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TA_GROUPS, TaGroup } from '../../indicators/ta-group-layers';
import { orderByStars, readGroupStars, writeGroupStars } from '../../indicators/ta-group-stars';

/**
 * Thanh chip chọn nhóm chỉ báo, dùng chung cho trang Thị trường và trang Dòng thời gian mã.
 * Mỗi chip có nút ☆/★ riêng (không cướp cú bấm chip): nhóm có sao lên đầu và là nhóm mở sẵn ở lần tải đầu. Chip chỉ phát sự kiện bấm: trang cha quyết định bấm lại nhóm đang chọn thì bỏ chọn hay không.
 */
@Component({
  selector: 'app-indicator-group-bar',
  standalone: true,
  template: `
    <div class="flex flex-wrap gap-2 mb-2" role="group" aria-label="Chọn nhóm chỉ báo để vẽ biểu đồ">
      @for (g of ordered; track g.key) {
        <span class="inline-flex items-center rounded-full border"
          [class.bg-blue-600]="selected === g.key" [class.border-blue-600]="selected === g.key"
          [class.bg-white]="selected !== g.key" [class.border-gray-300]="selected !== g.key">
          <button type="button" class="pl-3 pr-1 py-1 rounded-l-full text-sm font-semibold cursor-pointer select-none"
            [class.text-white]="selected === g.key" [class.text-gray-700]="selected !== g.key" [class.hover:bg-gray-100]="selected !== g.key"
            [attr.aria-pressed]="selected === g.key"
            (click)="groupToggle.emit(g.key)"
            [attr.data-group]="g.key">{{ g.label }}</button>
          <button type="button" class="inline-flex items-center justify-center min-w-[32px] min-h-[32px] rounded-r-full text-sm cursor-pointer select-none"
            [class.text-white]="selected === g.key" [class.text-amber-700]="selected !== g.key"
            [attr.aria-pressed]="isStarred(g.key)"
            [attr.aria-label]="'Sao nhóm ' + g.label"
            [attr.title]="isStarred(g.key) ? 'Bỏ sao' : 'Gắn sao: nhóm hay dùng lên đầu và mở sẵn'"
            (click)="toggleStar(g.key)"
            [attr.data-star]="g.key">{{ isStarred(g.key) ? '★' : '☆' }}</button>
        </span>
      }
    </div>
  `
})
export class IndicatorGroupBarComponent {
  readonly groups = TA_GROUPS;
  /** Nhóm đã gắn sao (nhớ trong localStorage, chỉ là tuỳ chọn giao diện). */
  stars: TaGroup[] = readGroupStars();
  get ordered() { return orderByStars(this.groups, this.stars); }
  isStarred(key: TaGroup): boolean { return this.stars.includes(key); }
  toggleStar(key: TaGroup): void {
    this.stars = this.isStarred(key) ? this.stars.filter(k => k !== key) : this.groups.map(g => g.key).filter(k => k === key || this.stars.includes(k));
    writeGroupStars(this.stars);
  }
  @Input() selected: TaGroup | null = null;
  @Output() groupToggle = new EventEmitter<TaGroup>();
}
