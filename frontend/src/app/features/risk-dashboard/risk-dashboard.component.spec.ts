import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { RiskDashboardComponent } from './risk-dashboard.component';
import { RiskService, PortfolioRiskSummary, PositionRiskItem, DrawdownResult } from '../../core/services/risk.service';
import { PortfolioService } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';

/**
 * "Sức khỏe rủi ro" trước đây chấm độ phủ SL bằng activeSLCount — đếm từ collection
 * stop_loss_targets. Vị thế có SL trong kế hoạch vẫn bị trừ 20 điểm và dán nhãn
 * "Thiếu cắt lỗ". Chấm theo ngưỡng THẬT của vị thế. Xem ADR-0017.
 */
describe('RiskDashboardComponent — độ phủ stop-loss', () => {
  let component: RiskDashboardComponent;

  const position = (over: Partial<PositionRiskItem> = {}): PositionRiskItem => ({
    symbol: 'HHV', quantity: 300, currentPrice: 10250, marketValue: 3075000,
    positionSizePercent: 100, stopLossPrice: 9000, targetPrice: null,
    riskRewardRatio: null, riskPerShare: 2700, riskAmount: 810000,
    distanceToStopLossPercent: 12.2, distanceToTargetPercent: 0,
    sector: null, beta: null, positionVaR: null,
    stopLossSource: 'Plan', tradePlanId: 'plan-1', ...over
  });

  const summary = (positions: PositionRiskItem[]): PortfolioRiskSummary => ({
    portfolioId: 'p1', totalValue: 3075000, positions,
    maxDrawdown: 0, valueAtRisk95: 0, largestPositionPercent: 100,
    positionCount: positions.length
  });

  const drawdown: DrawdownResult = {
    maxDrawdownPercent: 0, currentDrawdownPercent: 0
  } as DrawdownResult;

  beforeEach(async () => {
    const riskSpy = jasmine.createSpyObj('RiskService', [
      'getPortfolioRiskSummary', 'getDrawdown', 'getCorrelation',
      'getStopLossTargets', 'getRiskProfile'
    ]);
    const portfolioSpy = jasmine.createSpyObj('PortfolioService', ['getAll']);
    const notifSpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'warning', 'info']);
    portfolioSpy.getAll.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [RiskDashboardComponent, RouterTestingModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RiskService, useValue: riskSpy },
        { provide: PortfolioService, useValue: portfolioSpy },
        { provide: NotificationService, useValue: notifSpy }
      ]
    }).compileComponents();

    component = TestBed.createComponent(RiskDashboardComponent).componentInstance;
  });

  function labels(): string[] {
    return component.healthItems.map(i => i.label);
  }

  it('không báo thiếu cắt lỗ khi SL đến từ kế hoạch (activeSLCount = 0)', () => {
    component.overview.activeSLCount = 0;

    component.calculateHealthScore(summary([position()]), drawdown, null);

    expect(labels()).toContain('Có cắt lỗ');
    expect(labels()).not.toContain('Thiếu cắt lỗ');
  });

  it('vẫn báo thiếu cắt lỗ khi không vị thế nào có ngưỡng', () => {
    component.overview.activeSLCount = 0;

    component.calculateHealthScore(
      summary([position({ stopLossPrice: null, stopLossSource: null, tradePlanId: null })]),
      drawdown, null);

    expect(labels()).toContain('Thiếu cắt lỗ');
  });

  it('không báo thiếu cắt lỗ khi danh mục chưa có vị thế nào', () => {
    component.overview.activeSLCount = 0;

    component.calculateHealthScore(summary([]), drawdown, null);

    expect(labels()).not.toContain('Thiếu cắt lỗ');
  });

  it('đủ phủ khi một phần vị thế có ngưỡng', () => {
    component.overview.activeSLCount = 0;

    component.calculateHealthScore(
      summary([position(), position({ symbol: 'T2TEST', stopLossPrice: null, stopLossSource: null })]),
      drawdown, null);

    expect(labels()).toContain('Có cắt lỗ');
  });
});
