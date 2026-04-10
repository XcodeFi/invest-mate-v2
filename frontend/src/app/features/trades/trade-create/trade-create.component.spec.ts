import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { TradeCreateComponent } from './trade-create.component';
import { TradeService } from '../../../core/services/trade.service';
import { PortfolioService } from '../../../core/services/portfolio.service';
import { FeeService } from '../../../core/services/fee.service';
import { TradePlanService } from '../../../core/services/trade-plan.service';
import { PnlService, PositionPnL, OverallPnLSummary, PortfolioPnL } from '../../../core/services/pnl.service';
import { NotificationService } from '../../../core/services/notification.service';
import { MarketDataService } from '../../../core/services/market-data.service';
import { RouterTestingModule } from '@angular/router/testing';
import { of, EMPTY } from 'rxjs';
import { TradeType } from '../../../shared/constants/trade-types';

describe('TradeCreateComponent — Bidirectional Auto-suggest', () => {
  let component: TradeCreateComponent;
  let fixture: ComponentFixture<TradeCreateComponent>;
  let pnlService: jasmine.SpyObj<PnlService>;
  let portfolioService: jasmine.SpyObj<PortfolioService>;
  let notificationService: jasmine.SpyObj<NotificationService>;

  const mockPositionVNM: PositionPnL = {
    symbol: 'VNM',
    quantity: 500,
    averageCost: 75000,
    currentPrice: 80000,
    marketValue: 40000000,
    totalCost: 37500000,
    unrealizedPnL: 2500000,
    unrealizedPnLPercent: 6.67,
    realizedPnL: 0,
    totalPnL: 2500000,
    totalPnLPercent: 6.67
  };

  const mockPositionFPT: PositionPnL = {
    symbol: 'FPT',
    quantity: 200,
    averageCost: 120000,
    currentPrice: 130000,
    marketValue: 26000000,
    totalCost: 24000000,
    unrealizedPnL: 2000000,
    unrealizedPnLPercent: 8.33,
    realizedPnL: 0,
    totalPnL: 2000000,
    totalPnLPercent: 8.33
  };

  const mockPositionSold: PositionPnL = {
    symbol: 'VIC',
    quantity: 0,
    averageCost: 50000,
    currentPrice: 48000,
    marketValue: 0,
    totalCost: 0,
    unrealizedPnL: 0,
    unrealizedPnLPercent: 0,
    realizedPnL: 500000,
    totalPnL: 500000,
    totalPnLPercent: 5
  };

  const mockSummary: OverallPnLSummary = {
    totalPortfolios: 2,
    totalInitialCapital: 700000000,
    totalInvested: 100000000,
    totalMarketValue: 110000000,
    totalRealizedPnL: 500000,
    totalUnrealizedPnL: 4500000,
    totalPnL: 5000000,
    totalPnLPercent: 5,
    portfolios: [
      {
        portfolioId: 'port-1',
        portfolioName: 'Portfolio A',
        initialCapital: 500000000,
        totalInvested: 61500000,
        totalMarketValue: 66000000,
        totalRealizedPnL: 500000,
        totalUnrealizedPnL: 4500000,
        totalPnL: 5000000,
        totalPnLPercent: 5,
        positions: [mockPositionVNM, mockPositionFPT, mockPositionSold]
      },
      {
        portfolioId: 'port-2',
        portfolioName: 'Portfolio B',
        initialCapital: 200000000,
        totalInvested: 40000000,
        totalMarketValue: 40000000,
        totalRealizedPnL: 0,
        totalUnrealizedPnL: 0,
        totalPnL: 0,
        totalPnLPercent: 0,
        positions: [
          { ...mockPositionVNM, quantity: 300, marketValue: 24000000, totalCost: 22500000 }
        ]
      }
    ]
  };

  const mockPortfolios = [
    { id: 'port-1', name: 'Portfolio A', initialCapital: 500000000, createdAt: '2026-01-01', tradeCount: 5, uniqueSymbols: 3, totalInvested: 61500000, totalSold: 0 },
    { id: 'port-2', name: 'Portfolio B', initialCapital: 200000000, createdAt: '2026-02-01', tradeCount: 2, uniqueSymbols: 1, totalInvested: 40000000, totalSold: 0 }
  ];

  beforeEach(async () => {
    const tradeSpy = jasmine.createSpyObj('TradeService', ['create']);
    const portfolioSpy = jasmine.createSpyObj('PortfolioService', ['getAll']);
    const feeSpy = jasmine.createSpyObj('FeeService', ['calculateFees']);
    const planSpy = jasmine.createSpyObj('TradePlanService', ['executeLot', 'triggerExitTarget', 'updateStatus']);
    const pnlSpy = jasmine.createSpyObj('PnlService', ['getPortfolioPnL', 'getPositionPnL', 'getSummary']);
    const notifSpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'info', 'warning']);
    const marketSpy = jasmine.createSpyObj('MarketDataService', ['searchStocks']);

    portfolioSpy.getAll.and.returnValue(of(mockPortfolios));
    pnlSpy.getSummary.and.returnValue(of(mockSummary));
    pnlSpy.getPositionPnL.and.returnValue(EMPTY);
    marketSpy.searchStocks.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [TradeCreateComponent, RouterTestingModule],
      providers: [
        { provide: TradeService, useValue: tradeSpy },
        { provide: PortfolioService, useValue: portfolioSpy },
        { provide: FeeService, useValue: feeSpy },
        { provide: TradePlanService, useValue: planSpy },
        { provide: PnlService, useValue: pnlSpy },
        { provide: NotificationService, useValue: notifSpy },
        { provide: MarketDataService, useValue: marketSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TradeCreateComponent);
    component = fixture.componentInstance;
    pnlService = TestBed.inject(PnlService) as jasmine.SpyObj<PnlService>;
    portfolioService = TestBed.inject(PortfolioService) as jasmine.SpyObj<PortfolioService>;
    notificationService = TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>;
  });

  // --- buildPortfolioSymbolMaps ---

  describe('buildPortfolioSymbolMaps', () => {
    it('should build portfolioSymbolsMap from summary data', () => {
      fixture.detectChanges(); // triggers ngOnInit

      expect(component.portfolioSymbolsMap.size).toBe(2);
      expect(component.portfolioSymbolsMap.get('port-1')!.length).toBe(3);
      expect(component.portfolioSymbolsMap.get('port-2')!.length).toBe(1);
    });

    it('should build symbolPortfoliosMap (reverse lookup)', () => {
      fixture.detectChanges();

      expect(component.symbolPortfoliosMap.get('VNM')).toEqual(['port-1', 'port-2']);
      expect(component.symbolPortfoliosMap.get('FPT')).toEqual(['port-1']);
      expect(component.symbolPortfoliosMap.get('VIC')).toEqual(['port-1']);
    });

    it('should handle empty summary gracefully', () => {
      const emptySummary: OverallPnLSummary = {
        ...mockSummary,
        portfolios: []
      };
      pnlService.getSummary.and.returnValue(of(emptySummary));
      fixture.detectChanges();

      expect(component.portfolioSymbolsMap.size).toBe(0);
      expect(component.symbolPortfoliosMap.size).toBe(0);
    });
  });

  // --- Direction 1: Portfolio → Symbol chips ---

  describe('Portfolio → Symbol suggestions', () => {
    beforeEach(() => {
      fixture.detectChanges(); // load summary
    });

    it('should show all positions as chips when BUY and portfolio selected', () => {
      component.form.tradeType = TradeType.BUY;
      component.form.portfolioId = 'port-1';
      component.updatePortfolioPositions();

      expect(component.currentPortfolioPositions.length).toBe(3);
      expect(component.currentPortfolioPositions.map(p => p.symbol)).toEqual(['VNM', 'FPT', 'VIC']);
    });

    it('should show only positions with quantity > 0 when SELL', () => {
      component.form.tradeType = TradeType.SELL;
      component.form.portfolioId = 'port-1';
      component.updatePortfolioPositions();

      expect(component.currentPortfolioPositions.length).toBe(2);
      expect(component.currentPortfolioPositions.map(p => p.symbol)).toEqual(['VNM', 'FPT']);
    });

    it('should return empty when portfolio has no positions', () => {
      component.form.portfolioId = 'nonexistent';
      component.updatePortfolioPositions();

      expect(component.currentPortfolioPositions.length).toBe(0);
    });

    it('should clear positions when no portfolio selected', () => {
      component.form.portfolioId = '';
      component.updatePortfolioPositions();

      expect(component.currentPortfolioPositions.length).toBe(0);
    });
  });

  // --- Direction 2: Symbol → Portfolio auto-suggest ---

  describe('Symbol → Portfolio suggestions', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should auto-select portfolio when symbol exists in exactly one portfolio', () => {
      component.form.portfolioId = '';
      component.form.symbol = 'FPT';
      component.autoSuggestPortfolio();

      expect(component.form.portfolioId).toBe('port-1');
    });

    it('should NOT auto-select when symbol exists in multiple portfolios', () => {
      component.form.portfolioId = '';
      component.form.symbol = 'VNM';
      component.autoSuggestPortfolio();

      expect(component.form.portfolioId).toBe('');
    });

    it('should NOT auto-select for BUY with new symbol (not in any portfolio)', () => {
      component.form.tradeType = TradeType.BUY;
      component.form.portfolioId = '';
      component.form.symbol = 'HPG';
      component.autoSuggestPortfolio();

      expect(component.form.portfolioId).toBe('');
    });

    it('should NOT override already-selected portfolio', () => {
      component.form.portfolioId = 'port-2';
      component.form.symbol = 'FPT';
      component.autoSuggestPortfolio();

      expect(component.form.portfolioId).toBe('port-2');
    });

    it('should return matching portfolio IDs for highlighting', () => {
      component.form.symbol = 'VNM';
      const matching = component.getMatchingPortfolioIds();

      expect(matching).toEqual(['port-1', 'port-2']);
    });

    it('should return empty matching for unknown symbol', () => {
      component.form.symbol = 'HPG';
      const matching = component.getMatchingPortfolioIds();

      expect(matching).toEqual([]);
    });
  });

  // --- SELL mismatch detection ---

  describe('SELL mismatch alert', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should set alert when SELL symbol has no position in selected portfolio', () => {
      component.form.tradeType = TradeType.SELL;
      component.form.portfolioId = 'port-2';
      component.form.symbol = 'FPT'; // FPT only in port-1
      component.checkSellMismatch();

      expect(component.sellMismatchAlert).toContain('FPT');
      expect(component.sellMismatchAlert).toContain('Portfolio B');
    });

    it('should set alert when SELL symbol has zero quantity in portfolio', () => {
      component.form.tradeType = TradeType.SELL;
      component.form.portfolioId = 'port-1';
      component.form.symbol = 'VIC'; // VIC has quantity 0 in port-1
      component.checkSellMismatch();

      expect(component.sellMismatchAlert).toContain('VIC');
    });

    it('should clear alert when SELL symbol matches portfolio with quantity > 0', () => {
      component.form.tradeType = TradeType.SELL;
      component.form.portfolioId = 'port-1';
      component.form.symbol = 'VNM';
      component.checkSellMismatch();

      expect(component.sellMismatchAlert).toBe('');
    });

    it('should clear alert for BUY trades (no mismatch concept)', () => {
      component.form.tradeType = TradeType.BUY;
      component.form.portfolioId = 'port-2';
      component.form.symbol = 'FPT';
      component.checkSellMismatch();

      expect(component.sellMismatchAlert).toBe('');
    });

    it('should clear alert when portfolio or symbol is empty', () => {
      component.form.tradeType = TradeType.SELL;
      component.form.portfolioId = '';
      component.form.symbol = 'VNM';
      component.checkSellMismatch();

      expect(component.sellMismatchAlert).toBe('');
    });

    it('should disable submit button on sell mismatch', () => {
      component.form.tradeType = TradeType.SELL;
      component.form.portfolioId = 'port-2';
      component.form.symbol = 'FPT';
      component.checkSellMismatch();

      expect(component.isSellMismatch).toBeTrue();
    });

    it('should not disable submit on valid sell', () => {
      component.form.tradeType = TradeType.SELL;
      component.form.portfolioId = 'port-1';
      component.form.symbol = 'VNM';
      component.checkSellMismatch();

      expect(component.isSellMismatch).toBeFalse();
    });
  });

  // --- Chip selection ---

  describe('selectPositionChip', () => {
    beforeEach(() => {
      fixture.detectChanges();
    });

    it('should fill symbol and trigger position info load', () => {
      component.form.portfolioId = 'port-1';
      pnlService.getPositionPnL.and.returnValue(of(mockPositionVNM));

      component.selectPositionChip('VNM');

      expect(component.form.symbol).toBe('VNM');
      expect(component.showSymbolDropdown).toBeFalse();
      expect(pnlService.getPositionPnL).toHaveBeenCalledWith('port-1', 'VNM');
    });

    it('should update sell mismatch check after chip selection', () => {
      component.form.tradeType = TradeType.SELL;
      component.form.portfolioId = 'port-1';
      pnlService.getPositionPnL.and.returnValue(of(mockPositionVNM));

      component.selectPositionChip('VNM');

      expect(component.isSellMismatch).toBeFalse();
    });
  });
});
