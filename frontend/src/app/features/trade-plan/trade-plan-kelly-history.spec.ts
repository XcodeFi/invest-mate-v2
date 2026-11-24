import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, EMPTY } from 'rxjs';
import { TradePlanComponent } from './trade-plan.component';
import { StrategyService } from '../../core/services/strategy.service';
import { PortfolioService } from '../../core/services/portfolio.service';
import { ConservativeKellyDto, PositionSizingResult, RiskService } from '../../core/services/risk.service';
import { MarketDataService } from '../../core/services/market-data.service';
import { TradePlanTemplateService } from '../../core/services/trade-plan-template.service';
import { TradePlanService } from '../../core/services/trade-plan.service';
import { NotificationService } from '../../core/services/notification.service';
import { CompanyDossierService } from '../../core/services/company-dossier.service';

const kelly = (over: Partial<ConservativeKellyDto> = {}): ConservativeKellyDto => ({
  n: 24, winRateLower95: 0.3883, payoffRatio: 2, fullKelly: 0.0825, halfKellyCapped: 0.0413, reason: null, ...over,
});

const fixedRisk = { model: 'fixed_risk', modelVi: 'Cố định % rủi ro', shares: 2000, positionValue: 200_000_000,
  positionPercent: 20, riskAmount: 10_000_000, withinLimit: true };

const sizing = (k?: ConservativeKellyDto): PositionSizingResult => ({
  recommendedModel: 'fixed_risk',
  models: k?.halfKellyCapped
    ? [fixedRisk, { model: 'kelly', modelVi: 'Kelly Criterion', shares: 400, positionValue: 40_000_000,
        positionPercent: 4.1, riskAmount: 2_000_000, withinLimit: true, note: 'Half-Kelly: 4.1% (Full: 8.3%)' }]
    : [fixedRisk],
  kellyFromHistory: k,
});

describe('TradePlanComponent: Kelly thận trọng theo dữ liệu của người dùng', () => {
  let fixture: ComponentFixture<TradePlanComponent>;
  let component: TradePlanComponent;
  let riskSpy: jasmine.SpyObj<RiskService>;

  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);

  beforeEach(async () => {
    const strategySpy = jasmine.createSpyObj('StrategyService', ['getAll']);
    const portfolioSpy = jasmine.createSpyObj('PortfolioService', ['getAll']);
    riskSpy = jasmine.createSpyObj('RiskService', [
      'getRiskProfile', 'getPortfolioRiskSummary', 'calculatePositionSizing', 'getSectorExposureForPlan', 'getVolatilitySizingForPlan'
    ]);
    const marketSpy = jasmine.createSpyObj('MarketDataService', ['getPrice', 'getTechnicalAnalysis', 'getCurrentPrice']);
    const templateSpy = jasmine.createSpyObj('TradePlanTemplateService', ['getAll', 'create', 'delete']);
    const planSpy = jasmine.createSpyObj('TradePlanService', [
      'getAll', 'create', 'update', 'updateStatus', 'delete', 'cancel', 'restore', 'previewReview', 'review',
      'getScenarioPresets', 'getScenarioHistory', 'fetchScenarioSuggestion', 'getAdvisory', 'updateStopLoss', 'getExpectancy'
    ]);
    const notifSpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'warning', 'info']);
    const dossierSpy = jasmine.createSpyObj('CompanyDossierService', ['gateStatus', 'suggestedRules']);

    strategySpy.getAll.and.returnValue(of([]));
    portfolioSpy.getAll.and.returnValue(of([]));
    templateSpy.getAll.and.returnValue(of([]));
    planSpy.getAll.and.returnValue(of([]));
    planSpy.getExpectancy.and.returnValue(of({ total: { n: 0 }, strategies: [] }));
    planSpy.getScenarioPresets.and.returnValue(of([]));
    riskSpy.getPortfolioRiskSummary.and.returnValue(EMPTY);
    riskSpy.getSectorExposureForPlan.and.returnValue(EMPTY);
    riskSpy.getVolatilitySizingForPlan.and.returnValue(EMPTY);
    riskSpy.calculatePositionSizing.and.returnValue(of(sizing()));
    marketSpy.getCurrentPrice.and.returnValue(EMPTY);
    marketSpy.getTechnicalAnalysis.and.returnValue(EMPTY);
    dossierSpy.gateStatus.and.returnValue(EMPTY);
    dossierSpy.suggestedRules.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [TradePlanComponent, RouterTestingModule],
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: StrategyService, useValue: strategySpy },
        { provide: PortfolioService, useValue: portfolioSpy },
        { provide: RiskService, useValue: riskSpy },
        { provide: MarketDataService, useValue: marketSpy },
        { provide: TradePlanTemplateService, useValue: templateSpy },
        { provide: TradePlanService, useValue: planSpy },
        { provide: CompanyDossierService, useValue: dossierSpy },
        { provide: NotificationService, useValue: notifSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TradePlanComponent);
    component = fixture.componentInstance;
    component.plan.entryPrice = 100_000;
    component.plan.stopLoss = 95_000;
    component.riskPerShare = 5_000;
    component.fetchSizingModels();
    fixture.detectChanges();
  });

  function setHistory(on: boolean, result?: PositionSizingResult) {
    if (result) riskSpy.calculatePositionSizing.and.returnValue(of(result));
    component.useSizingHistory = on;
    component.onSizingHistoryChange();
    fixture.detectChanges();
  }

  it('is off by default: the sizing call has no history option and there is no block', () => {
    expect(riskSpy.calculatePositionSizing).toHaveBeenCalled();
    expect(riskSpy.calculatePositionSizing.calls.mostRecent().args[1]).toBeUndefined();
    expect(q('kelly-history')).toBeNull();
    expect(q('use-history-toggle')).not.toBeNull();
  });

  it('kelly toggle shows n and lower bound', () => {
    setHistory(true, sizing(kelly()));

    expect(riskSpy.calculatePositionSizing.calls.mostRecent().args[1]).toEqual(jasmine.objectContaining({ useHistory: true }));
    const block = q('kelly-history')!.textContent!;
    expect(block).toContain('n = 24');
    expect(block).toContain('39%');                       // cận dưới 38,83%
    expect(block).toContain('cỡ vị thế thận trọng');
    expect(block).toContain('không phải lời khuyên');
    expect(block).toContain('4,1%');                      // half-Kelly
    expect(fixture.nativeElement.textContent).toContain('Kelly Criterion');   // mô hình kelly có trong bảng so sánh
  });

  it('passes the plans strategy so the history is per strategy', () => {
    component.plan.strategyId = 's-42';
    setHistory(true, sizing(kelly()));

    expect(riskSpy.calculatePositionSizing.calls.mostRecent().args[1])
      .toEqual({ useHistory: true, strategyId: 's-42' });
  });

  it('kelly reason when no edge', () => {
    setHistory(true, sizing(kelly({ winRateLower95: 0.2, payoffRatio: 1, fullKelly: 0, halfKellyCapped: 0,
      reason: 'Không có lợi thế theo dữ liệu — Kelly = 0.' })));

    const block = q('kelly-history')!.textContent!;
    expect(block).toContain('Không có lợi thế theo dữ liệu');
    expect(block).toContain('n = 24');
    expect(block).not.toContain('Half-Kelly');
    expect(fixture.nativeElement.textContent).not.toContain('Kelly Criterion');
  });

  it('kelly reason when there is not enough data', () => {
    setHistory(true, sizing(kelly({ n: 7, winRateLower95: null, payoffRatio: null, fullKelly: null, halfKellyCapped: null,
      reason: 'Chưa đủ dữ liệu: cần tối thiểu 20 lệnh thắng/thua, hiện có 7.' })));

    expect(q('kelly-history')!.textContent).toContain('Chưa đủ dữ liệu');
    expect(q('kelly-history')!.textContent).toContain('n = 7');
  });

  it('turning it off hides the block and goes back to the plain request', () => {
    setHistory(true, sizing(kelly()));
    setHistory(false, sizing(kelly()));   // kể cả khi phản hồi vẫn mang khối (cũ), công tắc tắt thì không hiện

    expect(q('kelly-history')).toBeNull();
    expect(riskSpy.calculatePositionSizing.calls.mostRecent().args[1]).toBeUndefined();
  });
});
