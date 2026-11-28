import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TA_GROUPS, TaGroup } from '../../indicators/ta-group-layers';

/**
 * Thanh chip chọn nhóm chỉ báo, dùng chung cho trang Thị trường và trang Dòng thời gian mã.
 * Chỉ phát sự kiện bấm: trang cha quyết định bấm lại nhóm đang chọn thì bỏ chọn hay không.
 */
@Component({
  selector: 'app-indicator-group-bar',
  standalone: true,
  template: `
    <div class="flex flex-wrap gap-2 mb-2" role="group" aria-label="Chọn nhóm chỉ báo để vẽ biểu đồ">
      @for (g of groups; track g.key) {
        <button type="button" class="px-3 py-1 rounded-full text-sm font-semibold border cursor-pointer select-none"
          [class.bg-blue-600]="selected === g.key" [class.text-white]="selected === g.key"
          [class.border-blue-600]="selected === g.key"
          [class.bg-white]="selected !== g.key" [class.text-gray-700]="selected !== g.key"
          [class.border-gray-300]="selected !== g.key" [class.hover:bg-gray-100]="selected !== g.key"
          [attr.aria-pressed]="selected === g.key"
          (click)="groupToggle.emit(g.key)"
          [attr.data-group]="g.key">{{ g.label }}</button>
      }
    </div>
  `
})
export class IndicatorGroupBarComponent {
  readonly groups = TA_GROUPS;
  @Input() selected: TaGroup | null = null;
  @Output() groupToggle = new EventEmitter<TaGroup>();
}
