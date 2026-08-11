import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { CompanyDossierDetailComponent, serverMessage } from './company-dossier-detail.component';

describe('serverMessage', () => {
  it('lấy nguyên văn detail của ProblemDetails từ exception middleware', () => {
    const err = { error: { status: 400, detail: 'Mỗi yếu tố rủi ro phải có dấu hiệu quan sát được' } };

    expect(serverMessage(err)).toBe('Mỗi yếu tố rủi ro phải có dấu hiệu quan sát được');
  });

  it('lấy field error của các BadRequest tự tay trong controller', () => {
    const err = { error: { error: 'Body request không hợp lệ — kiểm tra BusinessModel/Moats/RiskFactors.' } };

    expect(serverMessage(err)).toBe('Body request không hợp lệ — kiểm tra BusinessModel/Moats/RiskFactors.');
  });

  it('chỉ dùng câu chung khi server không nói gì dùng được', () => {
    // Mất mạng, hoặc 500 không body: không có gì để truyền lại thì mới nói câu chung.
    expect(serverMessage({ error: null })).toBe('Không thể lưu hồ sơ — thử lại sau.');
    expect(serverMessage(null)).toBe('Không thể lưu hồ sơ — thử lại sau.');
  });
});

describe('CompanyDossierDetailComponent', () => {
  let fixture: ComponentFixture<CompanyDossierDetailComponent>;
  let component: CompanyDossierDetailComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CompanyDossierDetailComponent, HttpClientTestingModule],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(CompanyDossierDetailComponent);
    component = fixture.componentInstance;
  });

  it('chỉ cho tick một yếu tố hủy diệt', () => {
    component.riskFactors = [
      { rank: 1, description: 'A', observableSignal: 'x', isDealBreaker: true, suggestedTrigger: null },
      { rank: 2, description: 'B', observableSignal: 'y', isDealBreaker: false, suggestedTrigger: null },
    ];
    expect(component.dealBreakerDisabled(1)).toBe(true);
    expect(component.dealBreakerDisabled(0)).toBe(false);
  });

  it('nút ▲ đổi thứ tự và đánh lại rank dense', () => {
    component.riskFactors = [
      { rank: 1, description: 'A', observableSignal: 'x', isDealBreaker: false, suggestedTrigger: null },
      { rank: 2, description: 'B', observableSignal: 'y', isDealBreaker: false, suggestedTrigger: null },
    ];
    component.moveUp(1);
    expect(component.riskFactors.map(r => r.description)).toEqual(['B', 'A']);
    expect(component.riskFactors.map(r => r.rank)).toEqual([1, 2]);
  });

  it('nhãn nút ký đổi theo trạng thái tươi', () => {
    component.freshness = 'Unconfirmed';
    expect(component.signLabel()).toBe('Tôi đã đọc và chịu trách nhiệm');
    component.freshness = 'Expired';
    expect(component.signLabel()).toBe('Đã cập nhật tin mới và xác nhận');
    component.freshness = 'Fresh';
    expect(component.signLabel()).toBe('Vẫn đúng');
  });

  it('cảnh báo khi agent sửa mà chưa ký', () => {
    component.confirmedAt = null;
    component.agentDraftedAt = '2026-08-09T03:00:00Z';
    expect(component.showAgentDraftWarning()).toBe(true);
  });

  it('trả lại đúng entry/SL/TP đã stash sau khi ký', () => {
    const draft = { symbol: 'HPG', entryPrice: 28000, stopLoss: 26000, target: 33000 };
    sessionStorage.setItem('pendingTradePlanDraft', JSON.stringify(draft));

    const restored = component.consumePendingPlanDraft();

    expect(restored).toEqual(draft);
    expect(sessionStorage.getItem('pendingTradePlanDraft')).toBeNull();
  });

  it('không vỡ khi không có draft nào được stash', () => {
    sessionStorage.removeItem('pendingTradePlanDraft');
    expect(component.consumePendingPlanDraft()).toBeNull();
  });

  // F1 — ký không được vượt trước lưu: confirm() backend trả 404 nếu chưa có hồ sơ trên server.
  describe('canSign — bắt buộc đã lưu (F1)', () => {
    it('disabled khi hồ sơ chưa tồn tại trên server, dù đủ ký tự', () => {
      component.exists = false;
      component.businessModel = 'x'.repeat(40);
      expect(component.canSign()).toBe(false);
    });

    it('enable ngay sau khi lưu thành công, không cần tải lại trang', () => {
      const httpMock = TestBed.inject(HttpTestingController);
      component.symbol = 'TCB';
      component.exists = false;
      component.businessModel = 'x'.repeat(40);
      expect(component.canSign()).toBe(false);

      component.save();
      const req = httpMock.expectOne((r) => r.method === 'PUT' && r.url.endsWith('/company-dossiers/TCB'));
      req.flush({ id: '1' });

      expect(component.exists).toBe(true);
      expect(component.canSign()).toBe(true);
      httpMock.verify();
    });

    it('đếm ký tự theo giá trị đã trim — chuỗi toàn khoảng trắng không tính là đủ 30', () => {
      component.businessModel = ' '.repeat(30);
      expect(component.businessModelLength()).toBe(0);
      expect(component.canSign()).toBe(false);
    });
  });
});

// Chế độ mở đầu: hồ sơ đã viết xong thì mở ra để ĐỌC, chỉ vào thẳng form khi không có gì để đọc
// hoặc khi người dùng bị cổng đá sang đây để viết.
describe('CompanyDossierDetailComponent — chế độ view/edit', () => {
  const BUSINESS_MODEL = 'Cho thuê tài chính và cho vay tiêu dùng.';

  /**
   * Factory, không phải hằng dùng chung: component gán `this.riskFactors = dto.riskFactors` nên nó
   * dùng CHUNG tham chiếu mảng với body trả về. Một `const DTO` sẽ bị addRiskFactor() của test trước
   * push thêm phần tử, và test sau nhận một fixture đã phình ra.
   */
  const dto = () => ({
    symbol: 'EVF',
    businessModel: BUSINESS_MODEL,
    moats: [{ description: 'Mạng lưới chi nhánh' }],
    riskFactors: [
      { rank: 1, description: 'Nợ xấu tăng', observableSignal: 'NPL > 3% hai quý', isDealBreaker: false, suggestedTrigger: null },
    ],
    notes: '',
    reviewedAt: '2026-08-01T00:00:00Z',
    confirmedAt: '2026-08-01T00:00:00Z',
    agentDraftedAt: null,
    freshness: 'Fresh',
  });

  /** Spec cũ không dựng ActivatedRoute nên ngOnInit chưa từng chạy — các ca dưới đây cần nó chạy thật. */
  async function createWith(queryParams: Record<string, string> = {}) {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [CompanyDossierDetailComponent, HttpClientTestingModule],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'EVF' }, queryParams } },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(CompanyDossierDetailComponent);
    fixture.detectChanges(); // chạy ngOnInit
    const httpMock = TestBed.inject(HttpTestingController);
    const req = httpMock.expectOne((r) => r.method === 'GET' && r.url.endsWith('/company-dossiers/EVF'));
    return { fixture, component: fixture.componentInstance, req };
  }

  it('hồ sơ đã tồn tại → mở ra ở chế độ đọc', async () => {
    const { component, req } = await createWith();
    req.flush(dto());
    expect(component.mode).toBe('view');
  });

  it('mã chưa có hồ sơ (404) → vào thẳng form, vì không có gì để đọc', async () => {
    const { component, req } = await createWith();
    req.flush('not found', { status: 404, statusText: 'Not Found' });
    expect(component.mode).toBe('edit');
    expect(component.exists).toBe(false);
  });

  it('?edit=1 → vào thẳng form dù hồ sơ đã tồn tại', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());
    expect(component.mode).toBe('edit');
  });

  it('returnTo=trade-plan → vào thẳng form: cổng đá sang đây để VIẾT, không phải để đọc', async () => {
    const { component, req } = await createWith({ returnTo: 'trade-plan' });
    req.flush(dto());
    expect(component.mode).toBe('edit');
  });

  it('lưu thành công thì về chế độ đọc', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());
    const httpMock = TestBed.inject(HttpTestingController);

    component.businessModel = 'x'.repeat(40);
    component.save();
    httpMock.expectOne((r) => r.method === 'PUT' && r.url.endsWith('/company-dossiers/EVF')).flush({ id: '1' });

    expect(component.mode).toBe('view');
  });

  it('Hủy trả lại đúng nội dung cũ, không chỉ đổi mode', async () => {
    const { component, req } = await createWith();
    req.flush(dto());

    component.startEdit();
    component.businessModel = 'gõ dở dang';
    expect(component.isDirty()).toBe(true);

    spyOn(window, 'confirm').and.returnValue(true);
    component.cancelEdit();

    expect(component.mode).toBe('view');
    expect(component.businessModel).toBe(BUSINESS_MODEL);
  });

  // Đỏ ngay lúc vừa "+ Thêm yếu tố" là báo sai trước khi người dùng kịp làm gì.
  it('không báo đỏ khi vừa thêm yếu tố, chưa chạm vào ô', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());

    component.addRiskFactor();
    const added = component.riskFactors[component.riskFactors.length - 1];

    expect(added.observableSignal).toBe('');
    expect(component.showSignalError(added)).toBe(false);
    expect(component.missingSignalCount()).toBe(0);
  });

  it('báo đỏ sau khi người dùng rời ô mà vẫn để trống', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());

    component.addRiskFactor();
    const added = component.riskFactors[component.riskFactors.length - 1];
    added.touched = true;

    expect(component.showSignalError(added)).toBe(true);
  });

  it('bấm Lưu bật lỗi cho cả ô chưa từng chạm, kèm số đếm đúng', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());
    const httpMock = TestBed.inject(HttpTestingController);

    component.addRiskFactor();
    component.addRiskFactor();
    expect(component.missingSignalCount()).toBe(0);

    component.save();
    httpMock.expectOne((r) => r.method === 'PUT').flush({ id: '1' });

    expect(component.missingSignalCount()).toBe(2);
    expect(component.showSignalError(component.riskFactors[1])).toBe(true);
    // Yếu tố có sẵn dấu hiệu thì không bị vạ lây.
    expect(component.showSignalError(component.riskFactors[0])).toBe(false);
  });

  it('cờ touched sống sót qua đổi chỗ — moveUp tạo object mới bằng spread', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());

    component.addRiskFactor();
    component.riskFactors[1].touched = true;
    component.moveUp(1);

    expect(component.riskFactors[0].touched).toBe(true);
  });

  it('touched không lọt xuống payload gửi lên API', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());
    const httpMock = TestBed.inject(HttpTestingController);

    component.riskFactors[0].touched = true;
    component.save();
    const put = httpMock.expectOne((r) => r.method === 'PUT');

    expect(Object.keys(put.request.body.RiskFactors[0])).not.toContain('touched');
    put.flush({ id: '1' });
  });

  it('dán hợp lệ đổ vào form nhưng KHÔNG tự lưu — chữ ký phải do người đọc xong mới bấm', async () => {
    const { component, req } = await createWith();
    req.flush(dto());
    const httpMock = TestBed.inject(HttpTestingController);

    component.openPaste();
    component.pasteText = '```json\n' + JSON.stringify({
      symbol: 'EVF',
      businessModel: 'Bản AI soát lại',
      moats: [{ description: 'Moat mới' }],
      riskFactors: [{ description: 'Rủi ro mới', observableSignal: 'Dấu hiệu mới' }],
      notes: null,
    }) + '\n```';
    component.applyPaste();

    expect(component.businessModel).toBe('Bản AI soát lại');
    expect(component.mode).toBe('edit');
    expect(component.showPaste).toBe(false);
    httpMock.expectNone((r) => r.method === 'PUT');
    httpMock.expectNone((r) => r.method === 'POST');
  });

  it('dán nội dung mã khác bị chặn, form giữ nguyên', async () => {
    const { component, req } = await createWith();
    req.flush(dto());

    component.openPaste();
    component.pasteText = '```json\n{"symbol":"HPG","businessModel":"thép"}\n```';
    component.applyPaste();

    expect(component.pasteError).toContain('HPG');
    expect(component.businessModel).toBe(BUSINESS_MODEL);
    expect(component.showPaste).toBe(true);
  });

  it('nội dung sao chép gồm cả số liệu panel đã phát lên', async () => {
    const { component, req } = await createWith();
    req.flush(dto());

    component.fundamentals = { indicators: { pe: 9.9 }, incomeStatements: [], unavailableSections: [] } as any;

    expect(component.aiPromptText()).toContain('P/E 9.9');
  });

  // Chạm vào ô rồi rời đi mà không gõ gì KHÔNG phải là sửa. `touched` là cờ UI, không phải nội dung.
  it('tab qua ô rồi tab ra không làm hồ sơ thành "đã sửa"', async () => {
    const { component, req } = await createWith();
    req.flush(dto());

    component.startEdit();
    component.riskFactors[0].touched = true;

    expect(component.isDirty()).toBe(false);
  });

  it('không cho ký khi form còn thay đổi chưa lưu — chữ ký đóng vào bản trên server', async () => {
    const { component, req } = await createWith();
    req.flush(dto());
    component.businessModel = 'x'.repeat(40);
    component.startEdit();
    expect(component.canSign()).toBe(true);

    component.businessModel = 'y'.repeat(40);

    expect(component.isDirty()).toBe(true);
    expect(component.canSign()).toBe(false);
  });

  it('dán từ AI xong chưa Lưu thì chưa ký được', async () => {
    const { component, req } = await createWith();
    req.flush(dto());

    component.openPaste();
    component.pasteText = '{"businessModel":"' + 'z'.repeat(40) + '"}';
    component.applyPaste();

    expect(component.canSign()).toBe(false);
  });

  // Mốc "đã bấm Lưu" thuộc về MỘT phiên sửa. Không reset thì phiên sửa thứ hai thừa hưởng nó, và
  // dòng vừa thêm đã đỏ trước khi người dùng kịp gõ — đúng thứ mà chế độ hoãn báo lỗi sinh ra để tránh.
  it('bấm Sửa lần nữa thì mốc "đã bấm Lưu" của phiên trước không còn hiệu lực', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());
    const httpMock = TestBed.inject(HttpTestingController);

    component.save();
    httpMock.expectOne((r) => r.method === 'PUT').flush({ id: '1' });
    expect(component.mode).toBe('view');

    component.startEdit();
    component.addRiskFactor();
    const added = component.riskFactors[component.riskFactors.length - 1];

    expect(component.showSignalError(added)).toBe(false);
    expect(component.missingSignalCount()).toBe(0);
  });

  it('Hủy rồi sửa lại cũng không thừa hưởng mốc đó', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());
    const httpMock = TestBed.inject(HttpTestingController);

    component.save();
    httpMock.expectOne((r) => r.method === 'PUT').flush({ id: '1' });
    component.startEdit();
    component.addRiskFactor();
    // Thêm yếu tố làm form dirty ⇒ cancelEdit() gọi confirm() thật và treo headless Chrome.
    spyOn(window, 'confirm').and.returnValue(true);
    component.cancelEdit();

    component.startEdit();
    component.addRiskFactor();

    expect(component.showSignalError(component.riskFactors[component.riskFactors.length - 1])).toBe(false);
  });

  // Lưu THẤT BẠI thì lỗi phải ở lại — người dùng đang cần nhìn thấy cái gì đang chặn mình.
  it('lưu thất bại thì lỗi vẫn hiện, không bị dọn theo', async () => {
    const { component, req } = await createWith({ edit: '1' });
    req.flush(dto());
    const httpMock = TestBed.inject(HttpTestingController);

    component.addRiskFactor();
    component.save();
    httpMock.expectOne((r) => r.method === 'PUT')
      .flush({ detail: 'lỗi' }, { status: 400, statusText: 'Bad Request' });

    expect(component.mode).toBe('edit');
    expect(component.showSignalError(component.riskFactors[component.riskFactors.length - 1])).toBe(true);
  });

  // Mã chưa có hồ sơ: gõ dở rồi rời đi là mất trắng. isDirty() phải nói đúng sự thật ở cả nhánh này.
  it('hồ sơ mới (404) gõ dở vẫn tính là đã sửa', async () => {
    const { component, req } = await createWith();
    req.flush('not found', { status: 404, statusText: 'Not Found' });

    expect(component.isDirty()).toBe(false);

    component.businessModel = 'đang gõ dở';

    expect(component.isDirty()).toBe(true);
  });

  it('Hủy khi không sửa gì thì không hỏi lại', async () => {
    const { component, req } = await createWith();
    req.flush(dto());
    const confirmSpy = spyOn(window, 'confirm');

    component.startEdit();
    component.cancelEdit();

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(component.mode).toBe('view');
  });
});
