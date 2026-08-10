import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, EMPTY } from 'rxjs';
import { TradePlanComponent } from './trade-plan.component';
import { StrategyService } from '../../core/services/strategy.service';
import { PortfolioService } from '../../core/services/portfolio.service';
import { RiskService } from '../../core/services/risk.service';
import { MarketDataService } from '../../core/services/market-data.service';
import { TradePlanTemplateService } from '../../core/services/trade-plan-template.service';
import { TradePlanService } from '../../core/services/trade-plan.service';
import { NotificationService } from '../../core/services/notification.service';
import {
  CompanyDossierService,
  SuggestedInvalidationRuleDto,
} from '../../core/services/company-dossier.service';

/**
 * Đề xuất điều kiện "lý do sai" lấy từ hồ sơ công ty. Điểm phải giữ: ĐỀ XUẤT, không tự áp — một
 * plan tự đầy điều kiện mà chưa ai đọc lại chúng thì gate kỷ luật đo được chữ, không đo được ý.
 */
describe('TradePlanComponent — đề xuất điều kiện từ hồ sơ công ty', () => {
  let component: TradePlanComponent;
  let fixture: ComponentFixture<TradePlanComponent>;
  let dossierSpy: jasmine.SpyObj<CompanyDossierService>;

  const rule = (over: Partial<SuggestedInvalidationRuleDto> = {}): SuggestedInvalidationRuleDto => ({
    trigger: 'EarningsMiss',
    detail: 'Giá HRC giảm sâu — dấu hiệu: giảm quá 10% trong một tháng',
    meetsMinLength: true,
    sourceRank: 1,
    ...over,
  });

  beforeEach(async () => {
    const strategySpy = jasmine.createSpyObj('StrategyService', ['getAll']);
    const portfolioSpy = jasmine.createSpyObj('PortfolioService', ['getAll']);
    const riskSpy = jasmine.createSpyObj('RiskService', ['getRiskProfile', 'getPortfolioRiskSummary', 'calculatePositionSize', 'getSizingModels', 'getSectorExposureForPlan']);
    const marketSpy = jasmine.createSpyObj('MarketDataService', ['getPrice', 'getTechnicalAnalysis', 'getCurrentPrice']);
    const templateSpy = jasmine.createSpyObj('TradePlanTemplateService', ['getAll', 'create', 'delete']);
    const planSpy = jasmine.createSpyObj('TradePlanService', [
      'getAll', 'create', 'update', 'updateStatus', 'delete', 'cancel', 'restore',
      'previewReview', 'review', 'getScenarioPresets', 'getScenarioHistory', 'fetchScenarioSuggestion', 'getAdvisory'
    ]);
    const notifSpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'warning', 'info']);
    dossierSpy = jasmine.createSpyObj('CompanyDossierService', ['gateStatus', 'suggestedRules']);

    strategySpy.getAll.and.returnValue(of([]));
    portfolioSpy.getAll.and.returnValue(of([]));
    templateSpy.getAll.and.returnValue(of([]));
    planSpy.getAll.and.returnValue(of([]));
    planSpy.getScenarioPresets.and.returnValue(of([]));
    riskSpy.getPortfolioRiskSummary.and.returnValue(EMPTY);
    riskSpy.getSectorExposureForPlan.and.returnValue(EMPTY);
    marketSpy.getCurrentPrice.and.returnValue(EMPTY);
    marketSpy.getTechnicalAnalysis.and.returnValue(EMPTY);
    dossierSpy.gateStatus.and.returnValue(EMPTY);
    dossierSpy.suggestedRules.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [TradePlanComponent, RouterTestingModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: StrategyService, useValue: strategySpy },
        { provide: PortfolioService, useValue: portfolioSpy },
        { provide: RiskService, useValue: riskSpy },
        { provide: MarketDataService, useValue: marketSpy },
        { provide: TradePlanTemplateService, useValue: templateSpy },
        { provide: TradePlanService, useValue: planSpy },
        { provide: CompanyDossierService, useValue: dossierSpy },
        { provide: NotificationService, useValue: notifSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TradePlanComponent);
    component = fixture.componentInstance;
  });

  it('đề xuất KHÔNG tự vào plan dù stream đã trả về — phải bấm thêm', (done) => {
    // Phải chạy đúng pipeline thật: bản cũ chỉ gán `component.suggestedRules` rồi assert, nên một
    // bug auto-apply đặt trong callback của subscribe vẫn để test này xanh.
    dossierSpy.suggestedRules.and.returnValue(of([rule()]));
    fixture.detectChanges();
    component.plan.symbol = 'HPG';
    component.onSymbolInput();

    setTimeout(() => {
      expect(component.suggestedRules.length).toBe(1);
      expect(component.plan.invalidationCriteria?.length || 0).toBe(0);
      done();
    }, 700);
  });

  it('bấm thêm thì đẩy đúng trigger và detail vào danh sách điều kiện', () => {
    const sg = rule();

    component.addSuggestedRule(sg);

    expect(component.plan.invalidationCriteria!.length).toBe(1);
    expect(component.plan.invalidationCriteria![0].trigger).toBe('EarningsMiss');
    expect(component.plan.invalidationCriteria![0].detail).toBe(sg.detail);
    // checkDate để trống: ngày kiểm chứng là quyết định của người dùng, hồ sơ không biết.
    expect(component.plan.invalidationCriteria![0].checkDate).toBe('');
  });

  it('bấm hai lần cùng một đề xuất không tạo hai điều kiện trùng', () => {
    const sg = rule();

    component.addSuggestedRule(sg);
    component.addSuggestedRule(sg);

    expect(component.plan.invalidationCriteria!.length).toBe(1);
    expect(component.isSuggestionAdded(sg)).toBeTrue();
  });

  it('xoá tay điều kiện thì nút mời thêm lại được', () => {
    // Mirror Set sẽ giữ dấu "đã thêm" mãi và người dùng không thêm lại được cho tới khi đổi mã.
    const sg = rule();
    component.addSuggestedRule(sg);
    component.removeInvalidationRule(0);

    expect(component.isSuggestionAdded(sg)).toBeFalse();
    component.addSuggestedRule(sg);
    expect(component.plan.invalidationCriteria!.length).toBe(1);
  });

  it('plan mở lên đã có điều kiện y hệt thì không mời thêm bản trùng', () => {
    const sg = rule();
    component.plan.invalidationCriteria = [
      { trigger: 'Manual', detail: sg.detail, checkDate: '', isTriggered: false, triggeredAt: null },
    ];

    expect(component.isSuggestionAdded(sg)).toBeTrue();
    component.addSuggestedRule(sg);
    expect(component.plan.invalidationCriteria.length).toBe(1);
  });

  it('đề xuất chưa đủ 20 ký tự vẫn thêm được, không bị chặn ở tầng UI', () => {
    // Gate kỷ luật sẽ bắt lúc Lưu và có sẵn thông báo per-rule. Chặn ở đây thì người dùng mất
    // luôn nội dung gợi ý và phải tự gõ lại từ đầu.
    component.addSuggestedRule(rule({ detail: 'A — dấu hiệu: B', meetsMinLength: false }));

    expect(component.plan.invalidationCriteria!.length).toBe(1);
    expect(component.invalidationDetailError(component.plan.invalidationCriteria![0])).not.toBeNull();
  });

  it('không thêm khi plan đã huỷ (canEditNotes = false)', () => {
    // Luật sẵn có của project: notes/điều kiện sửa được ở MỌI trạng thái trừ Cancelled — không
    // phải chỉ Draft/Ready. Ghi ra đây để lần sau không ai "sửa" test theo trực giác sai.
    component.selectedPlanId = 'plan-1';
    component.selectedPlanStatus = 'Cancelled';

    component.addSuggestedRule(rule());

    expect(component.plan.invalidationCriteria?.length || 0).toBe(0);
  });

  it('nhãn kịch bản dùng chung bảng nhãn với trang hồ sơ', () => {
    expect(component.invalidationTriggerLabel('EarningsMiss')).toBe('KQKD không đạt');
    // Giá trị lạ thì trả nguyên văn, không trả rỗng — rỗng làm mất cả dòng chú thích.
    expect(component.invalidationTriggerLabel('SomethingNew')).toBe('SomethingNew');
  });

  it('đổi mã thì nạp đề xuất của mã mới', (done) => {
    // detectChanges() để ngOnInit chạy: không có nó thì stream chưa được subscribe và test này
    // "pass" mà chẳng kiểm gì.
    fixture.detectChanges();
    const other = rule({ sourceRank: 1, detail: 'Rủi ro của mã khác — dấu hiệu: X' });
    dossierSpy.suggestedRules.and.returnValue(of([other]));
    component.plan.symbol = 'VNM';
    component.onSymbolInput();

    // Stream có debounceTime(500) thật; không có zone.js/testing nên phải chờ bằng setTimeout thật.
    setTimeout(() => {
      expect(component.suggestedRules.length).toBe(1);
      expect(component.isSuggestionAdded(other)).toBeFalse();
      done();
    }, 700);
  });

  it('resetForm bỏ đề xuất của mã cũ, không để lại trên form trống', () => {
    // resetForm đặt symbol='' trực tiếp nên stream không bắn — phải tự dọn.
    component.suggestedRules = [rule()];
    component.resetForm();

    expect(component.suggestedRules.length).toBe(0);
  });
});
