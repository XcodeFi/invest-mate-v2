import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, EMPTY, throwError } from 'rxjs';
import { TradePlanComponent } from './trade-plan.component';
import { StrategyService } from '../../core/services/strategy.service';
import { PortfolioService } from '../../core/services/portfolio.service';
import { RiskService } from '../../core/services/risk.service';
import { MarketDataService } from '../../core/services/market-data.service';
import { TradePlanTemplateService } from '../../core/services/trade-plan-template.service';
import { TradePlanService } from '../../core/services/trade-plan.service';
import { NotificationService } from '../../core/services/notification.service';
import { CompanyDossierService } from '../../core/services/company-dossier.service';

describe('TradePlanComponent — Editability Matrix (Strict, Option A)', () => {
  let component: TradePlanComponent;
  let fixture: ComponentFixture<TradePlanComponent>;

  beforeEach(async () => {
    const strategySpy = jasmine.createSpyObj('StrategyService', ['getAll']);
    const portfolioSpy = jasmine.createSpyObj('PortfolioService', ['getAll']);
    const riskSpy = jasmine.createSpyObj('RiskService', ['getRiskProfile', 'getPortfolioRiskSummary', 'calculatePositionSize', 'getSizingModels', 'getSectorExposureForPlan', 'getVolatilitySizingForPlan']);
    const marketSpy = jasmine.createSpyObj('MarketDataService', ['getPrice', 'getTechnicalAnalysis', 'getCurrentPrice']);
    const templateSpy = jasmine.createSpyObj('TradePlanTemplateService', ['getAll', 'create', 'delete']);
    const planSpy = jasmine.createSpyObj('TradePlanService', [
      'getAll', 'create', 'update', 'updateStatus', 'delete', 'cancel', 'restore',
      'previewReview', 'review', 'getScenarioPresets', 'getScenarioHistory', 'fetchScenarioSuggestion', 'getAdvisory'
    ]);
    const notifSpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'warning', 'info']);
    const dossierSpy = jasmine.createSpyObj('CompanyDossierService', ['gateStatus', 'suggestedRules']);

    strategySpy.getAll.and.returnValue(of([]));
    portfolioSpy.getAll.and.returnValue(of([]));
    templateSpy.getAll.and.returnValue(of([]));
    planSpy.getAll.and.returnValue(of([]));
    planSpy.getScenarioPresets.and.returnValue(of([]));
    riskSpy.getPortfolioRiskSummary.and.returnValue(EMPTY);
    riskSpy.getSectorExposureForPlan.and.returnValue(EMPTY);
    riskSpy.getVolatilitySizingForPlan.and.returnValue(EMPTY);
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

  // Helper — set status directly (simulates loaded plan)
  function setStatus(status: string): void {
    component.selectedPlanId = status ? 'plan-1' : null;
    component.selectedPlanStatus = status;
  }

  // ============================================
  // canEditEntryInfo: Draft|Ready only
  // ============================================
  describe('canEditEntryInfo', () => {
    it('is true when creating new plan (no status)', () => {
      setStatus('');
      expect(component.canEditEntryInfo).toBeTrue();
    });

    it('is true for Draft', () => {
      setStatus('Draft');
      expect(component.canEditEntryInfo).toBeTrue();
    });

    it('is true for Ready', () => {
      setStatus('Ready');
      expect(component.canEditEntryInfo).toBeTrue();
    });

    it('is false for InProgress', () => {
      setStatus('InProgress');
      expect(component.canEditEntryInfo).toBeFalse();
    });

    it('is false for Executed', () => {
      setStatus('Executed');
      expect(component.canEditEntryInfo).toBeFalse();
    });

    it('is false for Reviewed', () => {
      setStatus('Reviewed');
      expect(component.canEditEntryInfo).toBeFalse();
    });

    it('is false for Cancelled', () => {
      setStatus('Cancelled');
      expect(component.canEditEntryInfo).toBeFalse();
    });
  });

  // ============================================
  // canEditStopLoss: Draft|Ready full; InProgress tighten-only
  // ============================================
  describe('canEditStopLoss', () => {
    it('is true for Draft', () => {
      setStatus('Draft');
      expect(component.canEditStopLoss).toBeTrue();
    });

    it('is true for Ready', () => {
      setStatus('Ready');
      expect(component.canEditStopLoss).toBeTrue();
    });

    it('is true for InProgress (tighten-only, but input editable)', () => {
      setStatus('InProgress');
      expect(component.canEditStopLoss).toBeTrue();
    });

    it('is false for Executed', () => {
      setStatus('Executed');
      expect(component.canEditStopLoss).toBeFalse();
    });

    it('is false for Reviewed', () => {
      setStatus('Reviewed');
      expect(component.canEditStopLoss).toBeFalse();
    });

    it('is false for Cancelled', () => {
      setStatus('Cancelled');
      expect(component.canEditStopLoss).toBeFalse();
    });
  });

  // ============================================
  // canEditTakeProfit: Draft|Ready only (strict)
  // ============================================
  describe('canEditTakeProfit', () => {
    it('is true for Draft', () => {
      setStatus('Draft');
      expect(component.canEditTakeProfit).toBeTrue();
    });

    it('is true for Ready', () => {
      setStatus('Ready');
      expect(component.canEditTakeProfit).toBeTrue();
    });

    it('is false for InProgress', () => {
      setStatus('InProgress');
      expect(component.canEditTakeProfit).toBeFalse();
    });

    it('is false for Executed / Reviewed / Cancelled', () => {
      (['Executed', 'Reviewed', 'Cancelled']).forEach(s => {
        setStatus(s);
        expect(component.canEditTakeProfit).toBeFalse();
      });
    });
  });

  // ============================================
  // canEditRiskContext: all except Executed|Reviewed|Cancelled
  // ============================================
  describe('canEditRiskContext', () => {
    it('is true for Draft, Ready, InProgress', () => {
      (['Draft', 'Ready', 'InProgress']).forEach(s => {
        setStatus(s);
        expect(component.canEditRiskContext).withContext(s).toBeTrue();
      });
    });

    it('is false for Executed, Reviewed, Cancelled', () => {
      (['Executed', 'Reviewed', 'Cancelled']).forEach(s => {
        setStatus(s);
        expect(component.canEditRiskContext).withContext(s).toBeFalse();
      });
    });
  });

  // ============================================
  // canEditExitTargets: Draft|Ready only
  // ============================================
  describe('canEditExitTargets', () => {
    it('is true for Draft, Ready', () => {
      (['Draft', 'Ready']).forEach(s => {
        setStatus(s);
        expect(component.canEditExitTargets).withContext(s).toBeTrue();
      });
    });

    it('is false for InProgress, Executed, Reviewed, Cancelled', () => {
      (['InProgress', 'Executed', 'Reviewed', 'Cancelled']).forEach(s => {
        setStatus(s);
        expect(component.canEditExitTargets).withContext(s).toBeFalse();
      });
    });
  });

  // ============================================
  // canEditLots: full in Draft/Ready; pending-only in InProgress
  // ============================================
  describe('canEditLots / canEditLot', () => {
    it('canEditLots is true for Draft, Ready', () => {
      (['Draft', 'Ready']).forEach(s => {
        setStatus(s);
        expect(component.canEditLots).withContext(s).toBeTrue();
      });
    });

    it('canEditLots is true for InProgress (pending lots only)', () => {
      setStatus('InProgress');
      expect(component.canEditLots).toBeTrue();
    });

    it('canEditLots is false for Executed, Reviewed, Cancelled', () => {
      (['Executed', 'Reviewed', 'Cancelled']).forEach(s => {
        setStatus(s);
        expect(component.canEditLots).withContext(s).toBeFalse();
      });
    });

    it('canEditLot allows pending lot in InProgress', () => {
      setStatus('InProgress');
      const pending = { lotNumber: 1, plannedPrice: 50000, plannedQuantity: 100, allocationPercent: 50, label: '', status: 'Pending' };
      expect(component.canEditLot(pending)).toBeTrue();
    });

    it('canEditLot blocks executed lot in InProgress', () => {
      setStatus('InProgress');
      const executed = { lotNumber: 1, plannedPrice: 50000, plannedQuantity: 100, allocationPercent: 50, label: '', status: 'Executed' };
      expect(component.canEditLot(executed)).toBeFalse();
    });

    it('canEditLot allows any lot in Draft/Ready', () => {
      setStatus('Draft');
      const anyLot = { lotNumber: 1, plannedPrice: 50000, plannedQuantity: 100, allocationPercent: 50, label: '', status: 'Executed' };
      expect(component.canEditLot(anyLot)).toBeTrue();
    });
  });

  // ============================================
  // canEditChecklist: same as risk context
  // ============================================
  describe('canEditChecklist', () => {
    it('is true for Draft, Ready, InProgress', () => {
      (['Draft', 'Ready', 'InProgress']).forEach(s => {
        setStatus(s);
        expect(component.canEditChecklist).withContext(s).toBeTrue();
      });
    });

    it('is false for Executed, Reviewed, Cancelled', () => {
      (['Executed', 'Reviewed', 'Cancelled']).forEach(s => {
        setStatus(s);
        expect(component.canEditChecklist).withContext(s).toBeFalse();
      });
    });
  });

  // ============================================
  // canEditNotes: all except Cancelled
  // ============================================
  describe('canEditNotes', () => {
    it('is true for all states except Cancelled', () => {
      (['', 'Draft', 'Ready', 'InProgress', 'Executed', 'Reviewed']).forEach(s => {
        setStatus(s);
        expect(component.canEditNotes).withContext(s).toBeTrue();
      });
    });

    it('is false for Cancelled', () => {
      setStatus('Cancelled');
      expect(component.canEditNotes).toBeFalse();
    });
  });

  // ============================================
  // Tighten-SL Gate
  // ============================================
  describe('validateTightenSl', () => {
    beforeEach(() => {
      setStatus('InProgress');
      component.plan.direction = 'Buy';
      component.plan.stopLoss = 48000; // current SL (loaded)
      component.loadedCurrentSl = 48000; // stash what was loaded
    });

    it('accepts tighter SL for Long (newSl > currentSl)', () => {
      const result = component.validateTightenSl(49000);
      expect(result.ok).toBeTrue();
    });

    it('accepts equal SL for Long', () => {
      const result = component.validateTightenSl(48000);
      expect(result.ok).toBeTrue();
    });

    it('rejects looser SL for Long (newSl < currentSl)', () => {
      const result = component.validateTightenSl(47000);
      expect(result.ok).toBeFalse();
      expect(result.reason).toBeTruthy();
    });

    it('accepts tighter SL for Short (newSl < currentSl)', () => {
      component.plan.direction = 'Sell';
      component.loadedCurrentSl = 52000;
      const result = component.validateTightenSl(51000);
      expect(result.ok).toBeTrue();
    });

    it('rejects looser SL for Short (newSl > currentSl)', () => {
      component.plan.direction = 'Sell';
      component.loadedCurrentSl = 52000;
      const result = component.validateTightenSl(53000);
      expect(result.ok).toBeFalse();
    });

    it('always accepts in Draft (no tighten rule)', () => {
      setStatus('Draft');
      component.loadedCurrentSl = 48000;
      const result = component.validateTightenSl(40000); // much looser
      expect(result.ok).toBeTrue();
    });

    it('accepts any SL in InProgress when loadedCurrentSl is null (partial load)', () => {
      setStatus('InProgress');
      component.loadedCurrentSl = null;
      const result = component.validateTightenSl(1);
      expect(result.ok).toBeTrue();
    });
  });

  // ============================================
  // State Banner
  // ============================================
  describe('stateBanner', () => {
    it('returns null for new plan (no status)', () => {
      setStatus('');
      expect(component.stateBanner).toBeNull();
    });

    it('returns Draft banner with neutral tone', () => {
      setStatus('Draft');
      expect(component.stateBanner?.tone).toBe('draft');
      expect(component.stateBanner?.message).toContain('nháp');
    });

    it('returns Ready banner with info tone', () => {
      setStatus('Ready');
      expect(component.stateBanner?.tone).toBe('ready');
    });

    it('returns InProgress banner with warning tone', () => {
      setStatus('InProgress');
      expect(component.stateBanner?.tone).toBe('inprogress');
      expect(component.stateBanner?.message).toContain('tighten');
    });

    it('returns Executed banner', () => {
      setStatus('Executed');
      expect(component.stateBanner?.tone).toBe('executed');
    });

    it('returns Reviewed banner', () => {
      setStatus('Reviewed');
      expect(component.stateBanner?.tone).toBe('reviewed');
    });

    it('returns Cancelled banner', () => {
      setStatus('Cancelled');
      expect(component.stateBanner?.tone).toBe('cancelled');
    });
  });

  // ============================================
  // Invalidation criteria validation (Detail ≥ 20 chars)
  // — repro for production bug: empty detail saved silently as 204 No Content.
  // ============================================
  describe('invalidation criteria validation', () => {
    it('isInvalidationCriteriaValid is true when no rules exist', () => {
      component.plan.invalidationCriteria = [];
      expect(component.isInvalidationCriteriaValid()).toBeTrue();
    });

    it('isInvalidationCriteriaValid is false when a rule has empty detail', () => {
      component.plan.invalidationCriteria = [
        { trigger: 'EarningsMiss', detail: '', checkDate: '', isTriggered: false, triggeredAt: null }
      ];
      expect(component.isInvalidationCriteriaValid()).toBeFalse();
    });

    it('isInvalidationCriteriaValid is false when a rule has detail < 20 chars', () => {
      component.plan.invalidationCriteria = [
        { trigger: 'EarningsMiss', detail: 'EPS giảm', checkDate: '', isTriggered: false, triggeredAt: null }
      ];
      expect(component.isInvalidationCriteriaValid()).toBeFalse();
    });

    it('isInvalidationCriteriaValid is false when a rule has whitespace-only detail of length ≥ 20', () => {
      component.plan.invalidationCriteria = [
        { trigger: 'EarningsMiss', detail: '                         ', checkDate: '', isTriggered: false, triggeredAt: null }
      ];
      expect(component.isInvalidationCriteriaValid()).toBeFalse();
    });

    it('isInvalidationCriteriaValid is true when every rule has detail.trim().length ≥ 20', () => {
      component.plan.invalidationCriteria = [
        { trigger: 'EarningsMiss', detail: 'BCTC Q1/2026 EPS < 20% YoY trong 2 quý liên tiếp', checkDate: '', isTriggered: false, triggeredAt: null },
        { trigger: 'TrendBreak', detail: 'Đóng cửa dưới MA200 với volume > 2× TB20', checkDate: '', isTriggered: false, triggeredAt: null }
      ];
      expect(component.isInvalidationCriteriaValid()).toBeTrue();
    });

    it('isInvalidationCriteriaValid flags only the offending rule when mixed', () => {
      component.plan.invalidationCriteria = [
        { trigger: 'EarningsMiss', detail: 'BCTC Q1/2026 EPS giảm > 20% YoY', checkDate: '', isTriggered: false, triggeredAt: null },
        { trigger: 'TrendBreak', detail: '', checkDate: '', isTriggered: false, triggeredAt: null }
      ];
      expect(component.isInvalidationCriteriaValid()).toBeFalse();
      expect(component.invalidationDetailError(component.plan.invalidationCriteria[0])).toBeNull();
      expect(component.invalidationDetailError(component.plan.invalidationCriteria[1])).toContain('20');
    });
  });

  // ============================================
  // saveDraft — surfaces BE validation errors instead of generic toast
  // ============================================
  describe('saveDraft error toast', () => {
    it('shows specific BE validation message when 400 returned with errors dict', () => {
      const planSpy = TestBed.inject(TradePlanService) as jasmine.SpyObj<TradePlanService>;
      const notifSpy = TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>;

      // Simulate BE 400 ProblemDetails (FluentValidation auto-validation shape).
      planSpy.create.and.returnValue(throwError(() => ({
        status: 400,
        error: {
          errors: {
            'InvalidationCriteria[0].Detail': [
              'Mô tả điều kiện phải có ít nhất 20 ký tự (sau Trim) để có thể chứng minh sai'
            ]
          }
        }
      })));

      component.plan.symbol = 'VIC';
      component.plan.entryPrice = 100_000;
      component.plan.stopLoss = 95_000;
      component.plan.target = 120_000;
      component.plan.invalidationCriteria = [
        { trigger: 'EarningsMiss', detail: '', checkDate: '', isTriggered: false, triggeredAt: null }
      ];
      component.selectedPlanId = null;

      component.saveDraft();

      expect(notifSpy.error).toHaveBeenCalled();
      const errMsg = notifSpy.error.calls.mostRecent().args[1] as string;
      expect(errMsg).toContain('20 ký tự');
    });
  });

  // ============================================
  // dossierGateQueryParams — forward size context sang trang hồ sơ (Step 6 banner link)
  // ============================================
  describe('dossierGateQueryParams', () => {
    it('carries quantity/entryPrice/accountBalance when the form has them', () => {
      component.plan.quantity = 500;
      component.plan.entryPrice = 28000;
      component.accountBalance = 200_000_000;

      expect(component.dossierGateQueryParams()).toEqual({
        quantity: 500,
        entryPrice: 28000,
        accountBalance: 200_000_000,
      });
    });

    it('falls back to optimalShares when quantity is not manually set', () => {
      component.plan.quantity = 0;
      component.optimalShares = 350;
      component.plan.entryPrice = 28000;
      component.accountBalance = 200_000_000;

      expect(component.dossierGateQueryParams()['quantity']).toBe(350);
    });

    it('omits params that are empty instead of sending blanks/zeros', () => {
      component.plan.quantity = 0;
      component.optimalShares = 0;
      component.plan.entryPrice = 0;
      component.accountBalance = 0;

      expect(component.dossierGateQueryParams()).toEqual({});
    });
  });

  // ============================================
  // F3 — DOSSIER_GATE_FAILED banner phải render trong DOM, không chỉ đúng field component
  // ============================================
  describe('DOSSIER_GATE_FAILED banner (rendered DOM)', () => {
    function setupPlan(): void {
      component.plan.symbol = 'HPG';
      component.plan.entryPrice = 100_000;
      component.plan.stopLoss = 95_000;
      component.plan.target = 120_000;
      component.selectedPlanId = null;
    }

    it('hiển thị câu chữ cố định cho reason=missing kèm link sang /company-dossier/HPG', () => {
      const planSpy = TestBed.inject(TradePlanService) as jasmine.SpyObj<TradePlanService>;
      planSpy.create.and.returnValue(throwError(() => ({
        status: 400,
        error: { code: 'DOSSIER_GATE_FAILED', symbol: 'HPG', reason: 'missing', missing: [] }
      })));

      setupPlan();
      component.saveDraft();
      fixture.detectChanges();

      const text = (fixture.nativeElement as HTMLElement).textContent || '';
      expect(text).toContain('Chưa có hồ sơ công ty cho mã này. Viết hồ sơ trước khi lập kế hoạch mua.');

      // Link có thể kèm query params (size context) nên chỉ so khớp phần path, không so đúng toàn bộ href.
      const link = (fixture.nativeElement as HTMLElement).querySelector('a[href^="/company-dossier/HPG"]');
      expect(link).toBeTruthy();
    });

    it('hiển thị từng dòng missing[] cho reason=insufficient', () => {
      const planSpy = TestBed.inject(TradePlanService) as jasmine.SpyObj<TradePlanService>;
      planSpy.create.and.returnValue(throwError(() => ({
        status: 400,
        error: {
          code: 'DOSSIER_GATE_FAILED', symbol: 'HPG', reason: 'insufficient',
          missing: ['Cần ≥ 3 yếu tố rủi ro, đang có 1', 'Cần ≥ 1 moat, đang có 0']
        }
      })));

      setupPlan();
      component.saveDraft();
      fixture.detectChanges();

      const text = (fixture.nativeElement as HTMLElement).textContent || '';
      expect(text).toContain('Cần ≥ 3 yếu tố rủi ro, đang có 1');
      expect(text).toContain('Cần ≥ 1 moat, đang có 0');
    });
  });

  // ============================================
  // Tỷ trọng ngành trên form kiểm-trước — chỉ hiện số, không chặn
  // ============================================
  describe('sectorNotice', () => {
    function setSector(over: Record<string, unknown> = {}): void {
      component.sectorNotice = {
        symbol: 'HPG',
        sector: 'Tài nguyên cơ bản',
        currentPercent: 32,
        projectedPercent: 41,
        limitPercent: 40,
        sameSectorSymbols: ['HSG', 'NKG'],
        ...over
      } as never;
      fixture.detectChanges();
    }

    // Phải đọc text của ĐÚNG khối ngành, không đọc cả trang: trang có "0%" ở nhiều chỗ khác
    // (rủi ro, phân bổ...) nên assert "cả trang không chứa 0%" là không bao giờ đúng được.
    function sectorBlockText(): string {
      const block = (fixture.nativeElement as HTMLElement)
        .querySelector('[data-testid="sector-notice"]');
      return block?.textContent || '';
    }

    // Đọc đúng ô chứa từng con số. Assert "khối không chứa '0%'" là không bao giờ đúng được, vì
    // '0%' là substring của '40%' trong "Hạn mức 40%".
    function testidText(id: string): string {
      const el = (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`);
      return (el?.textContent || '').trim();
    }

    it('hiện ngành, tỷ trọng hiện tại, sau lệnh, hạn mức và mã cùng ngành', () => {
      setSector();

      expect(testidText('sector-current')).toBe('32%');
      expect(testidText('sector-projected')).toBe('41%');
      const text = sectorBlockText();
      expect(text).toContain('Tài nguyên cơ bản');
      expect(text).toContain('Hạn mức 40%');
      expect(text).toContain('HSG');
    });

    it('phần trăm null thì hiện n/a, không hiện 0%', () => {
      setSector({ currentPercent: null, projectedPercent: null });

      // 0% và n/a là hai câu khác nhau: 0% nói "chưa giữ gì ngành này", n/a nói "chưa tính được".
      expect(testidText('sector-current')).toBe('n/a');
      expect(testidText('sector-projected')).toBe('n/a');
    });

    it('chưa tra được ngành thì không hiện khối tỷ trọng', () => {
      setSector({ sector: null });

      expect((fixture.nativeElement as HTMLElement)
        .querySelector('[data-testid="sector-notice"]')).toBeNull();
    });

    it('vượt hạn mức không đổi khả năng lưu nháp', () => {
      component.sectorNotice = null;
      fixture.detectChanges();
      const before = component.canSaveDraft();

      setSector({ projectedPercent: 95 });

      expect(component.sectorOverLimit()).toBeTrue();
      // So với chính giá trị trước đó thay vì kỳ vọng true/false cứng: bất biến cần canh là
      // "cảnh báo ngành không tham gia quyết định lưu", không phụ thuộc trạng thái nền của form.
      expect(component.canSaveDraft()).toBe(before);
    });

    // Dùng done + setTimeout thật vì project không nạp zone.js/testing nên fakeAsync/tick không
    // dùng được. Debounce là 500ms, chờ 700ms cho chắc.
    function fillFormForPreflight(portfolioId: string): void {
      // detectChanges để ngOnInit chạy và subscription debounce tồn tại. Không có dòng này thì
      // "spy chưa được gọi" đúng vì không gì có thể gọi được — một pass rỗng ruột.
      fixture.detectChanges();
      component.plan.symbol = 'HPG';
      component.plan.quantity = 1000;
      component.plan.entryPrice = 60000;
      component.accountBalance = 100_000_000;
      component.plan.portfolioId = portfolioId;
    }

    it('không gọi endpoint tỷ trọng ngành khi chưa chọn danh mục', (done) => {
      const riskSpy = TestBed.inject(RiskService) as jasmine.SpyObj<RiskService>;
      fillFormForPreflight('');

      component.onSymbolInput();

      setTimeout(() => {
        expect(riskSpy.getSectorExposureForPlan).not.toHaveBeenCalled();
        done();
      }, 700);
    });

    it('gọi endpoint với addValue = số lượng × giá vào khi đã chọn danh mục', (done) => {
      const riskSpy = TestBed.inject(RiskService) as jasmine.SpyObj<RiskService>;
      fillFormForPreflight('port-1');

      component.onSymbolInput();

      setTimeout(() => {
        expect(riskSpy.getSectorExposureForPlan).toHaveBeenCalledWith('port-1', 'HPG', 60_000_000);
        done();
      }, 700);
    });

    // addValue luôn dương, và backend cộng thẳng vào giá trị ngành. Với lệnh BÁN thì phép chiếu
    // nói tỷ trọng TĂNG trong khi lệnh đó làm nó GIẢM — sai dấu trên đúng khối cảnh báo rủi ro.
    // Phạm vi tính năng là "sau lệnh MUA dự kiến" (ADR-0012), nên đường bán không gọi.
    it('không gọi endpoint tỷ trọng ngành khi lệnh là BÁN', (done) => {
      const riskSpy = TestBed.inject(RiskService) as jasmine.SpyObj<RiskService>;
      fillFormForPreflight('port-1');
      component.plan.direction = 'Sell';

      component.onSymbolInput();

      setTimeout(() => {
        expect(riskSpy.getSectorExposureForPlan).not.toHaveBeenCalled();
        done();
      }, 700);
    });

    it('đổi sang BÁN thì xoá luôn dòng ngành đang hiện, không để số cũ nằm lại', (done) => {
      // gateStatus mặc định trả EMPTY, mà forkJoin đòi MỌI nguồn phát ít nhất một lần — để EMPTY
      // thì subscribe không bao giờ chạy và test này pass/fail vì lý do khác hẳn điều đang kiểm.
      const dossierSpy = TestBed.inject(CompanyDossierService) as jasmine.SpyObj<CompanyDossierService>;
      dossierSpy.gateStatus.and.returnValue(of({ symbol: 'HPG', passed: true, missing: [] }) as never);
      setSector();
      expect(sectorBlockText()).toContain('Tài nguyên cơ bản');
      fillFormForPreflight('port-1');
      component.plan.direction = 'Sell';

      component.onSymbolInput();

      setTimeout(() => {
        fixture.detectChanges();
        expect(component.sectorNotice).toBeNull();
        expect((fixture.nativeElement as HTMLElement)
          .querySelector('[data-testid="sector-notice"]')).toBeNull();
        done();
      }, 700);
    });
  });

  // ============================================
  // Trần khối lượng theo ngân sách biến động (ADR-0014) — cảnh báo, không chặn
  // ============================================
  describe('volatilityNotice', () => {
    function setVol(over: Record<string, unknown> = {}): void {
      component.volatilityNotice = {
        symbol: 'FPT',
        currentVolatilityPercent: 19.4,
        projectedVolatilityPercent: 20.6,
        budgetVolatilityPercent: 21.1,
        sourceMaxDrawdownPercent: 10,
        correlationWithPortfolio: 0.42,
        marginalRiskContributionPercent: 22,
        capitalWeightPercent: 14,
        maxQuantityWithinBudget: 5600,
        isUnconstrainedByVolatility: false,
        portfolioAlreadyOverBudget: false,
        dataQuality: 'Full',
        missingSymbols: [],
        adjustedSymbols: [],
        fetchFailedSymbols: [],
        observationCount: 64,
        ...over
      } as never;
      fixture.detectChanges();
    }

    function volBlockText(): string {
      const block = (fixture.nativeElement as HTMLElement)
        .querySelector('[data-testid="volatility-notice"]');
      return block?.textContent || '';
    }

    function volEl(id: string): HTMLElement | null {
      return (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`);
    }

    it('Full trong trần: hiện biến động trước/sau, ngân sách, đóng góp rủi ro và trần', () => {
      component.plan.quantity = 1000;
      setVol();

      expect(volBlockText()).toContain('19.4%/năm');
      expect(volEl('volatility-projected')!.textContent).toContain('20.6%/năm');
      expect(volEl('volatility-budget')!.textContent).toContain('21.1%/năm');
      expect(volEl('volatility-mcr')!.textContent).toContain('22% rủi ro');
      expect(volEl('volatility-mcr')!.textContent).toContain('14% vốn');
      expect(volEl('volatility-ceiling')!.textContent).toContain('5600');
      // Trong trần thì không mời áp trần.
      expect(volEl('volatility-apply-ceiling')).toBeNull();
    });

    it('ngân sách luôn nói rõ suy ra từ đâu, không phải hằng số ẩn', () => {
      setVol();

      expect(volBlockText()).toContain('ngưỡng sụt giảm 10%');
      expect(volBlockText()).toContain('1 tháng');
    });

    it('Full vượt trần: hiện nút áp trần và tô đỏ', () => {
      component.plan.quantity = 8000;
      setVol();

      expect(component.volOverCeiling()).toBeTrue();
      const button = volEl('volatility-apply-ceiling');
      expect(button).not.toBeNull();
      expect(button!.textContent).toContain('5600');
      expect(volEl('volatility-notice')!.className).toContain('bg-red-50');
    });

    it('bấm áp trần thì điền đúng số vào ô khối lượng và khoá tính tự động', () => {
      component.plan.quantity = 8000;
      setVol();

      (volEl('volatility-apply-ceiling') as HTMLButtonElement).click();

      expect(component.plan.quantity).toBe(5600);
      // Không đặt cờ này thì vòng tính size tự động ghi đè lại ngay, và nút thành vô nghĩa.
      expect(component.manualQuantity).toBeTrue();
    });

    it('Insufficient: chỉ nói thiếu dữ liệu, KHÔNG hiện con số nào', () => {
      setVol({
        dataQuality: 'Insufficient',
        currentVolatilityPercent: null,
        projectedVolatilityPercent: null,
        correlationWithPortfolio: null,
        marginalRiskContributionPercent: null,
        capitalWeightPercent: null,
        maxQuantityWithinBudget: null,
        missingSymbols: ['NEWCO']
      });

      expect(volEl('volatility-insufficient')!.textContent).toContain('NEWCO');
      // Khối phải TỒN TẠI. Panel rỗng đọc thành "không có vấn đề gì" — ngược với sự thật.
      expect(volEl('volatility-notice')).not.toBeNull();
      expect(volEl('volatility-ceiling')).toBeNull();
      expect(volEl('volatility-mcr')).toBeNull();
      expect(volBlockText()).not.toContain('n/a');
      // Ca này là THIẾU lịch sử thật, không phải nguồn hỏng — không được nói nhầm thành nguồn lỗi.
      expect(volEl('volatility-fetch-failed')).toBeNull();
    });

    it('Insufficient do nguồn lỗi: nói "chưa lấy được", không nói "chưa đủ"', () => {
      setVol({
        dataQuality: 'Insufficient',
        currentVolatilityPercent: null,
        projectedVolatilityPercent: null,
        maxQuantityWithinBudget: null,
        missingSymbols: [],
        fetchFailedSymbols: ['FPT']
      });

      // FPT có thừa lịch sử; nói "chưa đủ lịch sử giá cho FPT" là nói sai sự thật, và người dùng
      // sẽ kết luận nhầm rằng mã này mới hoặc thanh khoản kém.
      const text = volEl('volatility-fetch-failed')!.textContent!;
      expect(text).toContain('FPT');
      expect(text).toContain('Chưa lấy được');
      expect(volEl('volatility-insufficient')).toBeNull();
      expect(volBlockText()).not.toContain('Chưa đủ lịch sử');
    });

    it('Partial: nêu tên mã bị chỉnh và số phiên ước lượng', () => {
      setVol({ dataQuality: 'Partial', missingSymbols: ['GHOST'], adjustedSymbols: ['VHM'] });

      const text = volEl('volatility-partial')!.textContent!;
      expect(text).toContain('GHOST');
      expect(text).toContain('VHM');
      expect(text).toContain('64 phiên');
    });

    it('danh mục đã vượt ngân sách: báo trạng thái, không mời áp trần 0 cổ', () => {
      component.plan.quantity = 8000;
      setVol({ portfolioAlreadyOverBudget: true, maxQuantityWithinBudget: 0 });

      expect(volEl('volatility-over-budget')).not.toBeNull();
      // "Dùng 0 cổ" không phải một hành động.
      expect(volEl('volatility-apply-ceiling')).toBeNull();
      expect(component.canApplyVolCeiling()).toBeFalse();
    });

    it('không bị ràng buộc khác hẳn không tính được', () => {
      setVol({ maxQuantityWithinBudget: null, isUnconstrainedByVolatility: true });

      expect(volEl('volatility-unconstrained')).not.toBeNull();
      expect(volEl('volatility-insufficient')).toBeNull();
      expect(component.volOverCeiling()).toBeFalse();
    });

    it('vượt trần không đổi khả năng lưu nháp — cảnh báo, không chặn', () => {
      component.volatilityNotice = null;
      fixture.detectChanges();
      const before = component.canSaveDraft();

      component.plan.quantity = 99_999;
      setVol();

      expect(component.volOverCeiling()).toBeTrue();
      expect(component.canSaveDraft()).toBe(before);
    });

    it('không gọi endpoint trần khối lượng khi lệnh là BÁN', (done) => {
      const riskSpy = TestBed.inject(RiskService) as jasmine.SpyObj<RiskService>;
      fixture.detectChanges();
      component.plan.symbol = 'HPG';
      component.plan.quantity = 1000;
      component.plan.entryPrice = 60000;
      component.accountBalance = 100_000_000;
      component.plan.portfolioId = 'port-1';
      component.plan.direction = 'Sell';

      component.onSymbolInput();

      setTimeout(() => {
        expect(riskSpy.getVolatilitySizingForPlan).not.toHaveBeenCalled();
        done();
      }, 700);
    });

    it('gọi endpoint với giá vào và số lượng khi đã chọn danh mục', (done) => {
      const riskSpy = TestBed.inject(RiskService) as jasmine.SpyObj<RiskService>;
      fixture.detectChanges();
      component.plan.symbol = 'HPG';
      component.plan.quantity = 1000;
      component.plan.entryPrice = 60000;
      component.accountBalance = 100_000_000;
      component.plan.portfolioId = 'port-1';

      component.onSymbolInput();

      setTimeout(() => {
        expect(riskSpy.getVolatilitySizingForPlan)
          .toHaveBeenCalledWith('port-1', 'HPG', 60000, 1000);
        done();
      }, 700);
    });
  });
});
