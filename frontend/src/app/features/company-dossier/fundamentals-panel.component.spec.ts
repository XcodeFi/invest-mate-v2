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
