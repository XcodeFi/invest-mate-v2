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

  it('nhãn mốc hồ sơ không rơi vào fallback "Cảnh báo"', () => {
    // Tooltip biểu đồ kết thúc bằng một fallback vô điều kiện gán nhãn "Cảnh báo"
    // cho mọi loại lạ, đọc `item.data.title` — mà mốc hồ sơ có data là
    // { action, version }, không có `title`. Thiếu nhánh riêng thì mốc hồ sơ
    // hiện thành cảnh báo, sai loại hoàn toàn mà không có lỗi nào.
    expect(component.dossierMarkerLabel('signed')).toBe('Ký hồ sơ công ty');
    expect(component.dossierMarkerLabel('agent-drafted')).toContain('Trợ lý AI');
    expect(component.dossierMarkerLabel(undefined)).not.toContain('Cảnh báo');
    expect(component.dossierMarkerLabel(undefined)).toBeTruthy();
  });

  // Đường sang hồ sơ trong feed chỉ hiện khi hồ sơ ĐÃ có mốc ký/agent — mã chưa có hồ sơ thì
  // không có đường nào từ trang này sang đó, đúng lúc cần nhất.
  describe('đường sang hồ sơ ở tiêu đề', () => {
    /** ngOnInit đọc symbol từ route (rỗng trong stub) nên phải gán SAU lần detectChanges đầu. */
    const withSymbol = (symbol: string, items: any[]) => {
      withItems(items);
      component.symbol = symbol;
      fixture.detectChanges();
    };

    it('luôn có, kể cả khi timeline không có mốc hồ sơ nào', () => {
      withSymbol('HHV', []);

      const link = fixture.nativeElement.querySelector('[data-testid="header-dossier-link"]');
      expect(link).withContext('tiêu đề phải có đường sang hồ sơ').not.toBeNull();
      expect(link.getAttribute('href')).toBe('/company-dossier/HHV');
    });

    it('trỏ đúng mã đang mở, không phải mã cứng', () => {
      withSymbol('VNM', [signed()]);

      const link = fixture.nativeElement.querySelector('[data-testid="header-dossier-link"]');
      expect(link.getAttribute('href')).toBe('/company-dossier/VNM');
    });

    it('có nhãn chữ, không chỉ mỗi icon trần', () => {
      withSymbol('HHV', []);

      const link = fixture.nativeElement.querySelector('[data-testid="header-dossier-link"]');
      expect(link.textContent.trim()).toContain('Hồ sơ công ty');
    });
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
