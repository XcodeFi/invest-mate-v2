import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { SymbolTimelineComponent } from './symbol-timeline.component';

/**
 * Mốc hồ sơ công ty trên dòng thời gian.
 *
 * Cạm bẫy đã dò được khi thi hành: component lọc item qua `activeTypes` dựng từ
 * danh sách checkbox. Một `type` không có trong danh sách đó bị loại SẠCH —
 * backend trả đúng mà UI vẫn trống, và không có lỗi nào để lần theo.
 */
describe('SymbolTimelineComponent — mốc hồ sơ', () => {
  let fixture: ComponentFixture<SymbolTimelineComponent>;
  let component: SymbolTimelineComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SymbolTimelineComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(SymbolTimelineComponent);
    component = fixture.componentInstance;
  });

  function withItems(items: any[]) {
    component.timeline = {
      symbol: 'HPG',
      items,
      holdingPeriods: [],
      emotionSummary: null,
      behavioralPatterns: [],
    } as any;
    component.loading = false;
    component.applyFilters();
    fixture.detectChanges();
  }

  const signed = (ts = '2026-08-01T00:00:00Z') =>
    ({ type: 'dossier', timestamp: ts, data: { action: 'signed', version: 2 } });
  const agentDrafted = (ts = '2026-08-05T00:00:00Z') =>
    ({ type: 'dossier', timestamp: ts, data: { action: 'agent-drafted', version: 3 } });

  it('hiện mốc ký hồ sơ', () => {
    withItems([signed()]);

    expect(fixture.nativeElement.textContent).toContain('Ký hồ sơ công ty');
  });

  it('hiện mốc agent sửa kèm lời nhắc phải ký lại', () => {
    withItems([agentDrafted()]);

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Trợ lý AI sửa hồ sơ');
    expect(text).toContain('chờ bạn ký lại');
  });

  it('mốc hồ sơ KHÔNG bị bộ lọc loại bỏ', () => {
    // Đây là cái test đắt nhất trong file: thiếu 'dossier' trong danh sách
    // checkbox thì filteredItems rỗng, empty state hiện lên, và mọi thứ khác
    // trông vẫn "đúng".
    withItems([signed()]);

    expect(component.filteredItems.length).toBe(1);
    expect(fixture.nativeElement.textContent).not.toContain('Chưa có dữ liệu timeline');
  });

  it('bộ lọc có ô cho loại hồ sơ, bỏ tick thì mốc biến mất', () => {
    withItems([signed()]);
    const filter = component.filterOptions.find((f: any) => f.type === 'dossier');

    expect(filter).withContext('phải có ô lọc cho mốc hồ sơ').toBeTruthy();

    filter!.checked = false;
    component.applyFilters();
    fixture.detectChanges();

    expect(component.filteredItems.length).toBe(0);
  });

  it('xuất CSV có dòng cho mốc hồ sơ, không phải dòng rỗng', () => {
    // Nhánh default của exportCsv trả [] — mốc hồ sơ sẽ thành một dòng trống
    // giữa file thay vì bị bỏ qua hẳn, kiểu hỏng khó thấy nhất.
    withItems([signed(), agentDrafted()]);

    const rows = component.filteredItems.map(i => (component as any).csvRow(i));

    expect(rows.every((r: any[]) => r.length > 0)).toBeTrue();
    expect(rows[0][1]).toBe('Hồ sơ công ty');
  });
});
