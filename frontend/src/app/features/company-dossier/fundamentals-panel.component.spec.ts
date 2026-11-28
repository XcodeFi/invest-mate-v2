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
      businessPlan: { year: 2026, quarter: 2, targets: [{ label: 'Doanh thu', planned: 1000, actual: 600, percentComplete: 60 }] },
      unavailableSections: [],
    });

    it('khối doanh thu mở sẵn — thứ hay đọc nhất khi đang viết hồ sơ', () => {
      load(full());
      expect(component.isOpen('incomeStatements')).toBe(true);
      expect(fixture.nativeElement.querySelector('[data-testid="income-table"]')).not.toBeNull();
    });

    it('cả bốn khối mở sẵn: nội dung của chúng là biểu đồ, thứ người dùng đến đây để xem', () => {
      load(full());
      const el = fixture.nativeElement as HTMLElement;

      expect(el.querySelector('[data-testid="peer-bars"]')).not.toBeNull();
      expect(el.querySelector('[data-testid="dividends"]')).not.toBeNull();
      expect(el.querySelector('[data-testid="business-plan"]')).not.toBeNull();
      expect(el.textContent).toContain('Cổ phiếu cùng ngành');
    });

    it('gập lại thì biểu đồ mất, bung ra thì hiện lại', () => {
      load(full());

      component.toggle('peers');
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="peer-bars"]')).toBeNull();

      component.toggle('peers');
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[data-testid="peer-bars"]')).not.toBeNull();
    });

    it('kế hoạch còn năm nhưng không còn chỉ tiêu nào thì coi như không lấy được', () => {
      // Backend chấm "có dữ liệu" theo bất kỳ field nào khác rỗng, nên {year, quarter, targets: []}
      // vẫn tới được đây. Bung ra thì chỉ là một cái bảng không có dòng nào.
      load(withData({
        indicators: { pe: 1 } as any,
        businessPlan: { year: 2026, quarter: 2, targets: [] },
        unavailableSections: [],
      }));

      expect(component.hasSection('businessPlan')).toBe(false);
      expect(fixture.nativeElement.querySelector('[data-testid="business-plan"]')).toBeNull();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('không lấy được dữ liệu');
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

      // Cả 4 khối mở sẵn.
      expect(byState.filter((v) => v === 'true').length).toBe(4);
      expect(byState.filter((v) => v === 'false').length).toBe(0);

      component.toggle('peers');
      fixture.detectChanges();
      const after = Array.from(el.querySelectorAll('button[aria-expanded]')).map((b) => b.getAttribute('aria-expanded'));
      expect(after.filter((v) => v === 'false').length).toBe(1);
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
  describe('biểu đồ và mũi tên xu hướng (số so sánh do server tính)', () => {
    const quarter = (over: object = {}) => ({
      period: 'Q2/2026', year: 2026, quarter: 2, revenue: 13788.5, revenueSameQuarterLastYear: 16624.7, revenueYoyPct: -17.1, revenueDirection: 'down',
      netProfit: 2570.3, netProfitSameQuarterLastYear: 2740.3, netProfitYoyPct: -6.2, netProfitDirection: 'down', netMargin: 18.6, netMarginSameQuarterLastYear: 16.5, netMarginChangePp: 2.2, ...over,
    });
    const trend = (over: object = {}): any => ({
      quarters: [quarter(), quarter({ period: 'Q1/2026', quarter: 1 })],
      ttm: { period: 'Q2/2026', revenue: 63698, prevRevenue: 66193, revenueYoyPct: -3.8, revenueDirection: 'down', netProfit: 10936, prevNetProfit: 10315, netProfitYoyPct: 6, netProfitDirection: 'up', netMargin: 17.2, prevNetMargin: 15.6 },
      annual: [], range52W: { low: 43.9, high: 68.2, price: 44.6, positionPct: 2.9, outsideRange: false, unit: 'nghìn đồng' }, note: 'Doanh nghiệp báo cáo theo quý; không có số liệu tháng', ...over,
    });
    const withTrend = (t: any, over: Partial<CompanyFundamentals> = {}) =>
      withData({ indicators: { pe: 12, min52W: 43.9, max52W: 68.2 } as any, incomeStatements: [{ period: 'Q2/2026', revenue: 1, netProfit: 1, grossProfit: null }], trend: t, ...over });
    const q = (id: string) => (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
    const qa = (id: string) => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[];
    const tile = (key: string) => qa('trend-tile').find(t => t.dataset['key'] === key);

    it('với số liệu so sánh của server, doanh thu theo quý là biểu đồ cột ghép (bảng cũ chỉ còn là phương án dự phòng)', () => {
      load(withTrend(trend()));

      expect(q('quarter-bars')).not.toBeNull();
      expect(q('income-table')).toBeNull();
    });

    it('không có khối trend (backend cũ) thì vẫn là bảng như trước', () => {
      load(withTrend(null));

      expect(q('quarter-bars')).toBeNull();
      expect(q('income-table')).not.toBeNull();
    });

    it('ô số lớn của quý mới nhất có mũi tên và % so với cùng quý năm trước', () => {
      load(withTrend(trend()));

      const t = tile('revenue-q')!;
      expect(t.textContent).toContain('Doanh thu Q2/26');
      expect(t.textContent).toContain('13,8 nghìn tỷ');
      expect(t.getAttribute('title')).toBe('13.788,5 tỷ đồng');   // giá trị đầy đủ nằm ở tooltip
      expect(t.querySelector('[data-testid="trend-arrow"]')!.textContent!.replace(/\s+/g, ' ')).toContain('▼ −17,1%');
      expect(t.textContent).toContain('so với cùng quý năm trước');
    });

    it('ô TTM so với 4 quý trước; lợi nhuận tăng thì mũi tên lên', () => {
      load(withTrend(trend()));

      const t = tile('profit-ttm')!;
      expect(t.querySelector('[data-testid="trend-arrow"]')!.getAttribute('data-direction')).toBe('up');
      expect(t.textContent).toContain('so với 4 quý trước');
    });

    it('no_arrow_when_no_comparable_period: ô không có kỳ so sánh thì không có mũi tên, vẫn có số', () => {
      load(withTrend(trend({ quarters: [quarter({ revenueSameQuarterLastYear: null, revenueYoyPct: null, revenueDirection: null })], ttm: null })));

      const t = tile('revenue-q')!;
      expect(t.textContent).toContain('13,8 nghìn tỷ');
      expect(t.querySelector('[data-testid="trend-arrow"]')).toBeNull();
      expect(tile('revenue-ttm')).toBeUndefined();
    });

    it('lỗ sang lãi: mũi tên lên kèm ghi chú, không có phần trăm bịa', () => {
      load(withTrend(trend({ quarters: [quarter({ netProfitYoyPct: null, netProfitDirection: 'up', netProfitNote: 'từ lỗ sang lãi' })] })));

      expect(tile('profit-q')!.querySelector('[data-testid="trend-arrow"]')!.textContent!.replace(/\s+/g, ' ')).toContain('▲ từ lỗ sang lãi');
    });

    it('biên lợi nhuận hiện số và chênh lệch điểm %, không có mũi tên', () => {
      load(withTrend(trend()));

      const t = tile('margin-q')!;
      expect(t.textContent).toContain('18,6%');
      expect(t.querySelector('[data-testid="trend-sub"]')!.textContent).toContain('+2,2 điểm % so với cùng quý');
      expect(t.querySelector('[data-testid="trend-arrow"]')).toBeNull();
    });

    it('ghi chú "không có số liệu tháng" của server hiện cạnh ô số', () => {
      load(withTrend(trend()));

      expect(q('trend-note')!.textContent).toContain('không có số liệu tháng');
    });

    it('vùng giá 52 tuần hiện chấm giá hiện tại theo vị trí của server', () => {
      load(withTrend(trend()));

      expect(q('range-bar')).not.toBeNull();
      expect((q('rb-dot') as HTMLElement).style.left).toBe('2.9%');
    });

    it('không có giá hiện tại thì vẫn có vùng đáy–đỉnh từ chỉ số cơ bản, không đặt chấm', () => {
      load(withTrend(null));

      expect(q('range-bar')).not.toBeNull();
      expect(q('rb-dot')).toBeNull();
      expect(q('rb-price')!.textContent).toContain('Chưa có giá hiện tại');
    });

    it('thẻ chỉ số có giải thích tiếng Việt khi rê chuột', () => {
      load(withTrend(null));

      const pe = qa('indicator-card').find(c => c.textContent!.includes('P/E'))!;
      expect(pe.getAttribute('title')).toContain('hoàn vốn');
      expect(qa('indicator-card').every(c => (c.getAttribute('title') ?? '').length > 10)).toBeTrue();
    });

    it('cổ đông là thanh xếp chồng 100% thay cho danh sách', () => {
      load(withTrend(null, { company: { companyName: 'X', majorShareholders: [{ name: 'A', quantity: 1, percentage: 30 }], leaders: [] } as any }));

      expect(q('share-bar')).not.toBeNull();
      expect(qa('sb-seg').map(s => s.dataset['name'])).toEqual(['A', 'Khác']);
    });

    it('phần chưa có dữ liệu vẫn nói "không lấy được dữ liệu" như trước (không vẽ khung rỗng)', () => {
      load(withData({ indicators: { pe: 1 } as any, unavailableSections: ['peers', 'incomeStatements', 'dividendEvents', 'businessPlan'] }));

      expect(q('peer-bars')).toBeNull();
      expect(q('quarter-bars')).toBeNull();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('không lấy được dữ liệu');
    });
  });
});
