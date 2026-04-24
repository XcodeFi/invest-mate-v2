import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, EMPTY } from 'rxjs';

import { AnalyticsComponent } from './analytics.component';
import { PnlService } from '../../core/services/pnl.service';
import { AnalyticsService } from '../../core/services/analytics.service';
import {
  AdvancedAnalyticsService,
  SavingsComparisonDto,
  BankRateSnapshot,
} from '../../core/services/advanced-analytics.service';
import { PortfolioService } from '../../core/services/portfolio.service';
import { CapitalFlowService } from '../../core/services/capital-flow.service';

describe('AnalyticsComponent — vs Savings comparison', () => {
  let component: AnalyticsComponent;
  let fixture: ComponentFixture<AnalyticsComponent>;
  let advSpy: jasmine.SpyObj<AdvancedAnalyticsService>;

  const baseComparison: SavingsComparisonDto = {
    actualValue: 150_000_000,
    hypotheticalValue: 110_000_000,
    opportunityCost: 40_000_000,
    opportunityCostPercent: 36.4,
    usedRate: 0.05,
    rateSource: 'fallback-5',
    savingsAccountsCounted: 0,
    savingsAccountsTotal: 0,
    actualCurve: [],
    flows: [
      { date: '2026-01-01T00:00:00Z', signedAmount: 100_000_000 },
    ],
    cagrActual: 0.18,
    alphaAnnualized: 0.13,
    periodReturnDiff: null,
    asOf: '2027-01-01T00:00:00Z',
    firstFlowDate: '2026-01-01T00:00:00Z',
  };

  beforeEach(async () => {
    const pnlSpy = jasmine.createSpyObj('PnlService', ['getSummary']);
    const analyticsSpy = jasmine.createSpyObj('AnalyticsService', ['getPerformance', 'getRiskSummary']);
    advSpy = jasmine.createSpyObj('AdvancedAnalyticsService', [
      'getPerformance', 'getEquityCurve', 'getMonthlyReturns', 'getSavingsComparison', 'getBankRates',
    ]);
    const portfolioSpy = jasmine.createSpyObj('PortfolioService', ['getAll']);
    const flowSpy = jasmine.createSpyObj('CapitalFlowService', ['getTimeWeightedReturn', 'getFlowHistory']);

    pnlSpy.getSummary.and.returnValue(of({
      totalValue: 0, totalCost: 0, totalPnL: 0, totalPnLPercent: 0, portfolios: [],
    } as any));
    analyticsSpy.getPerformance.and.returnValue(of({} as any));
    analyticsSpy.getRiskSummary.and.returnValue(of({} as any));
    advSpy.getPerformance.and.returnValue(of({} as any));
    advSpy.getEquityCurve.and.returnValue(of({ portfolioId: 'p1', points: [] } as any));
    advSpy.getMonthlyReturns.and.returnValue(of({ portfolioId: 'p1', returns: [], years: [] } as any));
    portfolioSpy.getAll.and.returnValue(of([]));
    flowSpy.getTimeWeightedReturn.and.returnValue(EMPTY);
    flowSpy.getFlowHistory.and.returnValue(EMPTY);

    await TestBed.configureTestingModule({
      imports: [AnalyticsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PnlService, useValue: pnlSpy },
        { provide: AnalyticsService, useValue: analyticsSpy },
        { provide: AdvancedAnalyticsService, useValue: advSpy },
        { provide: PortfolioService, useValue: portfolioSpy },
        { provide: CapitalFlowService, useValue: flowSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AnalyticsComponent);
    component = fixture.componentInstance;
    component.selectedPortfolioId = 'p1';
  });

  it('loadSavingsComparison calls service with portfolioId and no rate for "my-savings" preset', () => {
    advSpy.getSavingsComparison.and.returnValue(of(baseComparison));
    component.loadSavingsComparison();
    expect(advSpy.getSavingsComparison).toHaveBeenCalledWith('p1', undefined);
    expect(component.comparison).toBe(baseComparison);
    expect(component.displayRate).toBe(0.05);
    expect(component.displayHypothetical).toBe(110_000_000);
  });

  it('useRatePreset("market-top") fetches bank rates then applies top 12T rate client-side', () => {
    // Seed comparison first
    component.comparison = { ...baseComparison };
    const snap: BankRateSnapshot = {
      topByTerm: { 12: { termMonths: 12, ratePercent: 7.6, bankName: 'SHB' } } as any,
      sourceTimestamp: null,
      fetchedAt: '2026-04-24T00:00:00Z',
    };
    advSpy.getBankRates.and.returnValue(of(snap));

    component.useRatePreset('market-top');

    expect(advSpy.getBankRates).toHaveBeenCalled();
    // Client-side recompute: rate went from 0.05 → 0.076, so hypothetical value shifts
    expect(component.displayRate).toBeCloseTo(0.076, 3);
  });

  it('client-side recompute: rate change updates displayHypothetical without server call', () => {
    component.comparison = { ...baseComparison };
    component.ratePreset = 'manual';
    advSpy.getSavingsComparison.calls.reset();

    component.manualRatePercent = 10;
    component.onManualRateChange();

    // rate 10% = 0.1 → hypothetical grows vs 0.05
    expect(component.displayRate).toBe(0.1);
    expect(component.displayHypothetical).toBeGreaterThan(baseComparison.hypotheticalValue);
    // No server round-trip for the recompute
    expect(advSpy.getSavingsComparison).not.toHaveBeenCalled();
  });

  it('recomputeWithRate: zero flows → hypothetical = 0', () => {
    component.comparison = { ...baseComparison, flows: [] };
    component.useRatePreset('manual');
    component.manualRatePercent = 6;
    component.onManualRateChange();

    expect(component.displayHypothetical).toBe(0);
  });

  it('manual rate clamped to [0, 30]', () => {
    component.comparison = { ...baseComparison };
    component.ratePreset = 'manual';

    component.manualRatePercent = 50;  // out of range
    component.onManualRateChange();
    expect(component.displayRate).toBe(0.3);  // clamped to 30%

    component.manualRatePercent = -5;
    component.onManualRateChange();
    expect(component.displayRate).toBe(0);  // clamped to 0
  });

  it('onTabChange("vs-savings") auto-loads comparison on first visit', () => {
    advSpy.getSavingsComparison.and.returnValue(of(baseComparison));

    component.onTabChange('vs-savings');
    // setTimeout delay
    return new Promise<void>(resolve => setTimeout(() => {
      expect(advSpy.getSavingsComparison).toHaveBeenCalled();
      resolve();
    }, 10));
  });
});
