import { CapitalFlowsComponent } from './capital-flows.component';
import { CapitalFlowService } from '../../core/services/capital-flow.service';
import { PortfolioService, PortfolioSummary } from '../../core/services/portfolio.service';
import { PnlService, PortfolioPnL, OverallPnLSummary } from '../../core/services/pnl.service';
import { NotificationService } from '../../core/services/notification.service';
import { ActivatedRoute } from '@angular/router';

describe('CapitalFlowsComponent — hero card getters', () => {
  let component: CapitalFlowsComponent;

  const portfolio = (overrides: Partial<PortfolioSummary> = {}): PortfolioSummary => ({
    id: 'p1',
    name: 'Main',
    initialCapital: 100_000_000,
    netCashFlow: 0,
    currentCapital: 100_000_000,
    createdAt: '2026-01-01',
    tradeCount: 0,
    uniqueSymbols: 0,
    totalInvested: 0,
    totalSold: 0,
    ...overrides
  });

  const pnl = (overrides: Partial<PortfolioPnL> = {}): PortfolioPnL => ({
    portfolioId: 'p1',
    portfolioName: 'Main',
    initialCapital: 0,
    netCashFlow: 0,
    currentCapital: 0,
    totalInvested: 0,
    totalMarketValue: 0,
    totalRealizedPnL: 0,
    totalUnrealizedPnL: 0,
    totalPnL: 0,
    totalPnLPercent: 0,
    positions: [],
    ...overrides
  });

  beforeEach(() => {
    const stub: any = jasmine.createSpyObj('any', ['getAll', 'getFlowHistory', 'getTimeWeightedReturn', 'getPortfolioPnL']);
    const route = { snapshot: { queryParamMap: new Map() } } as unknown as ActivatedRoute;
    component = new CapitalFlowsComponent(
      stub as unknown as CapitalFlowService,
      stub as unknown as PortfolioService,
      stub as unknown as PnlService,
      stub as unknown as NotificationService,
      route
    );
    component.selectedPortfolioId = 'p1';
  });

  describe('normal portfolio (no trades, no pnl)', () => {
    beforeEach(() => {
      component.portfolios = [portfolio()];
      component.portfolioPnL = null;
    });

    it('cashBalance equals currentCapital when no trades', () => {
      expect(component.cashBalance).toBe(100_000_000);
    });

    it('totalAssets equals currentCapital, totalReturn is 0', () => {
      expect(component.totalAssets).toBe(100_000_000);
      expect(component.totalReturn).toBe(0);
      expect(component.totalReturnPercent).toBe(0);
    });

    it('marketBarWidth is 0, cashBarWidth is 100', () => {
      expect(component.marketBarWidth).toBe(0);
      expect(component.cashBarWidth).toBe(100);
    });
  });

  describe('partly invested portfolio with unrealized gain', () => {
    beforeEach(() => {
      component.portfolios = [portfolio({ totalInvested: 60_000_000, totalSold: 0 })];
      component.portfolioPnL = pnl({ totalMarketValue: 70_000_000, totalUnrealizedPnL: 10_000_000 });
    });

    it('cashBalance = currentCapital - invested', () => {
      expect(component.cashBalance).toBe(40_000_000);
    });

    it('totalAssets = cash + market', () => {
      expect(component.totalAssets).toBe(110_000_000);
    });

    it('totalReturn equals unrealized P&L', () => {
      expect(component.totalReturn).toBe(10_000_000);
      expect(component.totalReturnPercent).toBeCloseTo(10, 4);
    });

    it('allocation bar splits 63.6 / 36.4', () => {
      expect(component.marketBarWidth).toBeCloseTo(63.636, 2);
      expect(component.cashBarWidth).toBeCloseTo(36.364, 2);
    });
  });

  describe('overbought portfolio (negative cash balance)', () => {
    beforeEach(() => {
      // Invested 130M from a 100M pool (edge case — borrowed/margin)
      component.portfolios = [portfolio({ totalInvested: 130_000_000, totalSold: 0 })];
      component.portfolioPnL = pnl({ totalMarketValue: 135_000_000 });
    });

    it('cashBalance is negative', () => {
      expect(component.cashBalance).toBe(-30_000_000);
    });

    it('marketBarWidth is clamped to 100', () => {
      // raw marketAllocationPercent = 135/105 * 100 = 128.57%, clamped
      expect(component.marketAllocationPercent).toBeGreaterThan(100);
      expect(component.marketBarWidth).toBe(100);
      expect(component.cashBarWidth).toBe(0);
    });
  });

  describe('zero capital edge case', () => {
    beforeEach(() => {
      component.portfolios = [portfolio({ initialCapital: 0, currentCapital: 0, netCashFlow: 0 })];
      component.portfolioPnL = null;
    });

    it('totalReturnPercent is 0 (guarded division)', () => {
      expect(component.totalReturnPercent).toBe(0);
    });

    it('marketAllocationPercent is 0 when totalAssets is 0', () => {
      expect(component.totalAssets).toBe(0);
      expect(component.marketAllocationPercent).toBe(0);
    });
  });

  describe('loss scenario', () => {
    beforeEach(() => {
      component.portfolios = [portfolio({ totalInvested: 60_000_000 })];
      component.portfolioPnL = pnl({ totalMarketValue: 50_000_000, totalUnrealizedPnL: -10_000_000 });
    });

    it('totalReturn is negative, absTotalReturn is positive magnitude', () => {
      expect(component.totalReturn).toBe(-10_000_000);
      expect(component.absTotalReturn).toBe(10_000_000);
      expect(component.totalReturnPercent).toBeCloseTo(-10, 4);
      expect(component.absTotalReturnPercent).toBeCloseTo(10, 4);
    });
  });

  describe('no selected portfolio', () => {
    beforeEach(() => {
      component.selectedPortfolioId = '';
      component.portfolios = [portfolio()];
    });

    it('all getters return 0', () => {
      expect(component.currentCapital).toBe(0);
      expect(component.cashBalance).toBe(0);
      expect(component.totalAssets).toBe(0);
      expect(component.marketBarWidth).toBe(0);
    });
  });

  describe('overallView — aggregate across all portfolios', () => {
    const summary = (overrides: Partial<OverallPnLSummary> = {}): OverallPnLSummary => ({
      totalPortfolios: 2,
      totalInitialCapital: 150_000_000,
      totalNetCashFlow: 20_000_000,
      totalCurrentCapital: 170_000_000,
      totalInvested: 100_000_000,
      totalMarketValue: 115_000_000,
      totalRealizedPnL: 3_000_000,
      totalUnrealizedPnL: 15_000_000,
      totalPnL: 18_000_000,
      totalPnLPercent: 18,
      portfolios: [],
      ...overrides
    });

    it('returns null when summary not yet loaded', () => {
      component.portfolios = [portfolio()];
      component.overallSummary = null;
      expect(component.overallView).toBeNull();
    });

    it('returns null when portfolios list is empty', () => {
      component.portfolios = [];
      component.overallSummary = summary();
      expect(component.overallView).toBeNull();
    });

    it('aggregates across 2 portfolios correctly', () => {
      component.portfolios = [
        portfolio({ id: 'p1', initialCapital: 100_000_000, netCashFlow: 0, currentCapital: 100_000_000, totalInvested: 60_000_000, totalSold: 0 }),
        portfolio({ id: 'p2', initialCapital: 50_000_000, netCashFlow: 20_000_000, currentCapital: 70_000_000, totalInvested: 40_000_000, totalSold: 0 })
      ];
      component.overallSummary = summary();

      const view = component.overallView!;
      expect(view.currentCapital).toBe(170_000_000);
      expect(view.initialCapital).toBe(150_000_000);
      expect(view.netCashFlow).toBe(20_000_000);
      expect(view.marketValue).toBe(115_000_000);
      expect(view.cashBalance).toBe(70_000_000); // 170 - 100 + 0
      expect(view.totalAssets).toBe(185_000_000); // 70 + 115
      expect(view.totalReturn).toBe(15_000_000); // 185 - 170
      expect(view.totalReturnPercent).toBeCloseTo(8.8235, 3);
    });

    it('sums totalSold across portfolios into cash balance', () => {
      component.portfolios = [
        portfolio({ id: 'p1', currentCapital: 100_000_000, totalInvested: 80_000_000, totalSold: 30_000_000 }),
        portfolio({ id: 'p2', currentCapital: 70_000_000, totalInvested: 20_000_000, totalSold: 10_000_000 })
      ];
      component.overallSummary = summary({ totalInvested: 100_000_000, totalCurrentCapital: 170_000_000 });

      const view = component.overallView!;
      // cashBalance = 170M - 100M + (30+10)M = 110M
      expect(view.cashBalance).toBe(110_000_000);
    });

    it('uses historical gross totalInvested (not OverallPnL open-position cost) — regression for closed-position cash calc', () => {
      // Scenario: user bought 100M, sold ALL for 120M. No open positions.
      // OverallPnLSummary.totalInvested = 0 (no open), totalRealizedPnL = 20M.
      // PortfolioSummary.totalInvested = 100M (gross historical), totalSold = 120M.
      // Correct cashBalance = currentCapital − 100M + 120M = currentCapital + 20M.
      component.portfolios = [
        portfolio({ id: 'p1', currentCapital: 100_000_000, totalInvested: 100_000_000, totalSold: 120_000_000 })
      ];
      component.overallSummary = summary({
        totalCurrentCapital: 100_000_000,
        totalInitialCapital: 100_000_000,
        totalNetCashFlow: 0,
        totalInvested: 0, // PnLService: cost basis of OPEN positions
        totalMarketValue: 0,
        totalRealizedPnL: 20_000_000,
        totalUnrealizedPnL: 0
      });

      const view = component.overallView!;
      expect(view.cashBalance).toBe(120_000_000); // NOT 220M (would be wrong with s.totalInvested)
      expect(view.totalAssets).toBe(120_000_000); // cash + 0 market
      expect(view.totalReturn).toBe(20_000_000); // matches realized P&L
    });

    it('clamps allocation bar when aggregate is overbought', () => {
      component.portfolios = [portfolio({ totalInvested: 200_000_000, totalSold: 0 })];
      component.overallSummary = summary({
        totalCurrentCapital: 100_000_000,
        totalInvested: 200_000_000,
        totalMarketValue: 210_000_000
      });

      const view = component.overallView!;
      expect(view.cashBalance).toBe(-100_000_000);
      expect(view.marketBarWidth).toBe(100);
      expect(view.cashBarWidth).toBe(0);
    });
  });
});
