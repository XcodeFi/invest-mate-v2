import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { FundamentalsPanelComponent } from './fundamentals-panel.component';
import { CompanyFundamentals } from '../../core/services/market-data.service';

describe('FundamentalsPanelComponent', () => {
  let fixture: ComponentFixture<FundamentalsPanelComponent>;
  let component: FundamentalsPanelComponent;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FundamentalsPanelComponent, HttpClientTestingModule],
    }).compileComponents();
    fixture = TestBed.createComponent(FundamentalsPanelComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  /** Chạy đúng đường thật: ngOnInit gọi endpoint, trả body, rồi render. */
  const load = (body: CompanyFundamentals) => {
    component.symbol = 'HPG';
    fixture.detectChanges();
    http.expectOne((r) => r.url.endsWith('/market/stock/HPG/fundamentals')).flush(body);
    fixture.detectChanges();
  };

  const withData = (over: Partial<CompanyFundamentals>): CompanyFundamentals => ({
    symbol: 'HPG',
    company: null,
    indicators: null,
    incomeStatements: [],
    peers: [],
    dividendEvents: [],
    businessPlan: null,
    unavailableSections: [],
    ...over,
  });

  it('phần nào không lấy được thì báo, phần có dữ liệu thì không', () => {
    component.data = withData({
      indicators: { pe: 12.3, roe: 18.2 } as any,
      unavailableSections: ['incomeStatements', 'peers'],
    });

    expect(component.isUnavailable('incomeStatements')).toBe(true);
    expect(component.isUnavailable('indicators')).toBe(false);
  });

  it('không có data thì mọi phần đều coi như chưa lấy được', () => {
    // Lúc đang tải hoặc lỗi mạng: trả false sẽ khiến template render bảng rỗng như thể
    // doanh nghiệp không có doanh thu.
    component.data = null;

    expect(component.isUnavailable('incomeStatements')).toBe(true);
    expect(component.isUnavailable('indicators')).toBe(true);
  });

  it('body rỗng mà unavailableSections không nói gì thì vẫn không render khối đó', () => {
    // Danh sách và body lệch nhau: khối vẫn phải ẩn. Nếu chỉ tin danh sách, template deref null và
    // một deref null làm sập cả vòng change detection, kéo các khối khác biến mất im lặng.
    component.data = withData({ company: null, incomeStatements: [], unavailableSections: [] });

    expect(component.hasSection('company')).toBe(false);
    expect(component.hasSection('incomeStatements')).toBe(false);
  });

  it('render chữ "không lấy được" thay vì số 0 cho phần thiếu', () => {
    load(withData({
      indicators: { pe: 12.3 } as any,
      unavailableSections: ['incomeStatements', 'peers', 'dividendEvents', 'businessPlan', 'company'],
    }));

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('không lấy được dữ liệu');
    // Bảng doanh thu không được render khi thiếu — số 0 ở đây đọc thành "doanh thu bằng 0".
    expect(fixture.nativeElement.querySelector('[data-testid="income-table"]')).toBeNull();
  });

  it('render bảng doanh thu khi có dữ liệu', () => {
    load(withData({
      indicators: { pe: 12.3 } as any,
      incomeStatements: [{ period: 'Q1/2026', revenue: 35000, netProfit: 2500, grossProfit: 4000 }],
      unavailableSections: ['peers', 'dividendEvents', 'businessPlan', 'company'],
    }));

    expect(fixture.nativeElement.querySelector('[data-testid="income-table"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Q1/2026');
  });

  it('nói rõ số liệu không tính vào điều kiện chặn', () => {
    load(withData({ indicators: { pe: 1 } as any }));

    expect(fixture.nativeElement.textContent).toContain('không tính vào điều kiện');
  });

  // Provider hiện không trả 4 khối này cho mã nào, nên browser verify không chạm tới được — phần
  // gập/bung chỉ có ở đây. Bỏ nhóm test này là để tính năng không có lớp bảo vệ nào.
  describe('gập / bung khối dài', () => {
    const full = () => withData({
      indicators: { pe: 1 } as any,
      incomeStatements: [{ period: 'Q1/2026', revenue: 100, netProfit: 10, grossProfit: null }],
      peers: [{ symbol: 'HSG', pe: 8, pb: 1, changePercent: 1.2 } as any],
      dividendEvents: [{ exDate: '2026-03-01', description: 'Tiền mặt 10%' } as any],
      businessPlan: { year: 2026, revenuePlan: 1000, profitPlan: 100, dividendPlan: 10 },
      unavailableSections: [],
    });

    it('khối doanh thu mở sẵn — thứ hay đọc nhất khi đang viết hồ sơ', () => {
      load(full());
      expect(component.isOpen('incomeStatements')).toBe(true);
      expect(fixture.nativeElement.querySelector('[data-testid="income-table"]')).not.toBeNull();
    });

    it('ba khối còn lại gập sẵn: có tiêu đề bấm được nhưng chưa render nội dung', () => {
      load(full());
      const el = fixture.nativeElement as HTMLElement;

      expect(el.querySelector('[data-testid="peers-table"]')).toBeNull();
      expect(el.querySelector('[data-testid="dividends"]')).toBeNull();
      expect(el.querySelector('[data-testid="business-plan"]')).toBeNull();
      expect(el.textContent).toContain('Cổ phiếu cùng ngành');
    });

    it('bung ra thì nội dung hiện, gập lại thì mất', () => {
      load(full());

      component.toggle('peers');
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="peers-table"]')).not.toBeNull();

      component.toggle('peers');
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="peers-table"]')).toBeNull();
    });

    it('khối không lấy được dữ liệu KHÔNG gập — câu báo thiếu phải luôn nhìn thấy', () => {
      load(withData({ indicators: { pe: 1 } as any, unavailableSections: ['peers', 'incomeStatements', 'dividendEvents', 'businessPlan'] }));
      const el = fixture.nativeElement as HTMLElement;

      const toggles = Array.from(el.querySelectorAll('button')).filter((b) => b.hasAttribute('aria-expanded'));
      expect(toggles.length).toBe(0);
      expect(el.textContent).toContain('không lấy được dữ liệu');
    });

    it('nút gập khai báo aria-expanded theo đúng trạng thái của chính nó', () => {
      load(full());
      const el = fixture.nativeElement as HTMLElement;
      const byState = Array.from(el.querySelectorAll('button[aria-expanded]')).map((b) => b.getAttribute('aria-expanded'));

      // 1 khối mở (doanh thu) + 3 khối gập.
      expect(byState.filter((v) => v === 'true').length).toBe(1);
      expect(byState.filter((v) => v === 'false').length).toBe(3);
    });
  });

  it('lỗi mạng thì báo không lấy được, không render bảng rỗng', () => {
    component.symbol = 'HPG';
    fixture.detectChanges();
    http.expectOne((r) => r.url.endsWith('/market/stock/HPG/fundamentals'))
      .error(new ProgressEvent('error'), { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(component.data).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Không lấy được số liệu doanh nghiệp');
    expect(fixture.nativeElement.querySelector('[data-testid="income-table"]')).toBeNull();
  });
});
