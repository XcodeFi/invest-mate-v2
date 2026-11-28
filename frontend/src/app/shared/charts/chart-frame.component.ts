import { Component, Input } from '@angular/core';

/**
 * Khung chung của mọi biểu đồ cơ bản: tiêu đề, nút "Xem bảng" (bảng là bản song sinh của biểu đồ: cùng số, để dữ liệu luôn đọc được không cần
 * rê chuột), và câu "Chưa có dữ liệu" khi không có số nào (không vẽ khung rỗng, không vẽ số 0). Biểu đồ nằm trong khung cuộn ngang riêng:
 * trang không bao giờ cuộn ngang ở 400px. Biểu đồ chiếu vào phần mặc định, bảng chiếu vào vùng `[table]`.
 */
@Component({
  selector: 'app-chart-frame',
  standalone: true,
  template: `
    <section class="viz-frame" [attr.data-testid]="testId || 'chart-frame'">
      <div class="viz-frame-head">
        <h3 class="viz-frame-title" data-testid="chart-title">{{ title }}</h3>
        @if (hasData) {
          <button type="button" data-testid="chart-table-toggle" class="viz-frame-toggle"
            [attr.aria-pressed]="showTable" (click)="showTable = !showTable">{{ showTable ? 'Xem biểu đồ' : 'Xem bảng' }}</button>
        }
      </div>
      @if (subtitle) { <p class="viz-frame-sub" data-testid="chart-subtitle">{{ subtitle }}</p> }
      @if (!hasData) {
        <p class="viz-frame-empty" data-testid="chart-empty">{{ emptyText }}</p>
      } @else {
        <div class="viz-frame-body" data-testid="chart-body" [hidden]="showTable"><ng-content></ng-content></div>
        <div class="viz-frame-body" data-testid="chart-table" [hidden]="!showTable"><ng-content select="[table]"></ng-content></div>
      }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .viz-frame { margin-bottom: 1rem; }
    .viz-frame-head { display: flex; align-items: baseline; justify-content: space-between; gap: 0.5rem; margin-bottom: 0.25rem; }
    .viz-frame-title { font-size: 0.75rem; font-weight: 600; color: var(--viz-text); }
    .viz-frame-toggle { font-size: 0.6875rem; color: var(--viz-text-2); text-decoration: underline; }
    .viz-frame-sub { font-size: 0.6875rem; color: var(--viz-text-2); margin-bottom: 0.25rem; }
    .viz-frame-empty { font-size: 0.75rem; font-style: italic; color: var(--viz-text-2); }
    .viz-frame-body { overflow-x: auto; position: relative; }
    .viz-frame-body[hidden] { display: none; }
  `]
})
export class ChartFrameComponent {
  @Input() title = '';
  @Input() subtitle = '';
  /** Có số nào để vẽ không. Không thì chỉ hiện câu `emptyText`. */
  @Input() hasData = true;
  @Input() emptyText = 'Chưa có dữ liệu';
  @Input() testId = '';

  showTable = false;
}
