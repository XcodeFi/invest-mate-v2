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

describe('TradePlanComponent — Editability Matrix (Strict, Option A)', () => {
  let component: TradePlanComponent;
  let fixture: ComponentFixture<TradePlanComponent>;

  beforeEach(async () => {
    const strategySpy = jasmine.createSpyObj('StrategyService', ['getAll']);
    const portfolioSpy = jasmine.createSpyObj('PortfolioService', ['getAll']);
    const riskSpy = jasmine.createSpyObj('RiskService', ['getRiskProfile', 'getPortfolioRiskSummary', 'calculatePositionSize', 'getSizingModels']);
    const marketSpy = jasmine.createSpyObj('MarketDataService', ['getPrice', 'getTechnicalAnalysis']);
    const templateSpy = jasmine.createSpyObj('TradePlanTemplateService', ['getAll', 'create', 'delete']);
    const planSpy = jasmine.createSpyObj('TradePlanService', [
      'getAll', 'create', 'update', 'updateStatus', 'delete', 'cancel', 'restore',
      'previewReview', 'review', 'getScenarioPresets', 'getScenarioHistory', 'fetchScenarioSuggestion', 'getAdvisory'
    ]);
    const notifSpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'warning', 'info']);

    strategySpy.getAll.and.returnValue(of([]));
    portfolioSpy.getAll.and.returnValue(of([]));
    templateSpy.getAll.and.returnValue(of([]));
    planSpy.getAll.and.returnValue(of([]));
    planSpy.getScenarioPresets.and.returnValue(of([]));
    riskSpy.getPortfolioRiskSummary.and.returnValue(EMPTY);

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
});
