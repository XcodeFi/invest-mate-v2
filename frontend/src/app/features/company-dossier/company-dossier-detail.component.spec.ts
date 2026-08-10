import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CompanyDossierDetailComponent } from './company-dossier-detail.component';

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
