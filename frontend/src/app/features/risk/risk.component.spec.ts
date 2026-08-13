import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of, throwError, EMPTY } from 'rxjs';
import { RiskComponent } from './risk.component';
import { RiskService, PositionRiskItem } from '../../core/services/risk.service';
import { PortfolioService } from '../../core/services/portfolio.service';
import { NotificationService } from '../../core/services/notification.service';
import { TemplateService } from '../../core/services/template.service';
import { TradePlanService } from '../../core/services/trade-plan.service';

/**
 * Dời SL trên trang Quản lý rủi ro phải gửi tới KẾ HOẠCH khi ngưỡng đến từ kế hoạch —
 * ghi vào stop_loss_targets sẽ dựng thêm một nguồn cạnh tranh. Xem ADR-0017.
 */
describe('RiskComponent — dời SL theo nguồn ngưỡng', () => {
  let fixture: ComponentFixture<RiskComponent>;
  let component: RiskComponent;
  let riskSpy: jasmine.SpyObj<RiskService>;
  let planSpy: jasmine.SpyObj<TradePlanService>;

  const position = (over: Partial<PositionRiskItem> = {}): PositionRiskItem => ({
    symbol: 'MWG',
    quantity: 100,
    currentPrice: 74100,
    marketValue: 7410000,
    positionSizePercent: 10,
    stopLossPrice: 64700,
    targetPrice: 85000,
    riskRewardRatio: 1.03,
    riskPerShare: 10000,
    riskAmount: 1000000,
    distanceToStopLossPercent: 12.7,
    distanceToTargetPercent: 14.7,
    sector: null,
    beta: null,
    positionVaR: null,
    stopLossSource: 'Plan',
    tradePlanId: 'plan-1',
    ...over
  });

  beforeEach(async () => {
    // spyObj có danh sách method CỐ ĐỊNH — thiếu một cái là loadRiskData gọi vào undefined.
    riskSpy = jasmine.createSpyObj('RiskService', [
      'getRiskProfile', 'getPortfolioRiskSummary', 'getStopLossTargets',
      'setStopLossTarget', 'getDrawdown', 'getCorrelation'
    ]);
    riskSpy.getRiskProfile.and.returnValue(EMPTY);
    riskSpy.getPortfolioRiskSummary.and.returnValue(EMPTY);
    riskSpy.getStopLossTargets.and.returnValue(EMPTY);
    riskSpy.getDrawdown.and.returnValue(EMPTY);
    riskSpy.getCorrelation.and.returnValue(EMPTY);
    planSpy = jasmine.createSpyObj('TradePlanService', ['updateStopLoss']);
    const portfolioSpy = jasmine.createSpyObj('PortfolioService', ['getAll']);
    const notifSpy = jasmine.createSpyObj('NotificationService', ['success', 'error', 'warning', 'info']);
    const templateSpy = jasmine.createSpyObj('TemplateService', ['getRiskProfileTemplates']);

    portfolioSpy.getAll.and.returnValue(of([]));
    templateSpy.getRiskProfileTemplates.and.returnValue(of([]));
    planSpy.updateStopLoss.and.returnValue(of(undefined as void));

    await TestBed.configureTestingModule({
      imports: [RiskComponent, RouterTestingModule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RiskService, useValue: riskSpy },
        { provide: PortfolioService, useValue: portfolioSpy },
        { provide: NotificationService, useValue: notifSpy },
        { provide: TemplateService, useValue: templateSpy },
        { provide: TradePlanService, useValue: planSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RiskComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  function givenPositions(...positions: PositionRiskItem[]): void {
    // Cả khối phân tích nằm sau *ngIf="selectedPortfolioId" — không set là DOM rỗng.
    component.selectedPortfolioId = 'p1';
    component.riskSummary = {
      portfolioId: 'p1',
      totalValue: 7410000,
      positions,
      maxDrawdown: 0,
      valueAtRisk95: 0,
      largestPositionPercent: 10,
      positionCount: positions.length
    };
    component.activeTab = 'positions';
    fixture.detectChanges();
  }

  it('hiện nút dời SL khi ngưỡng đến từ kế hoạch', () => {
    givenPositions(position());

    expect(fixture.debugElement.query(By.css('[data-test="move-sl-position"]'))).toBeTruthy();
  });

  it('không hiện nút khi ngưỡng đến từ bản ghi stop_loss_targets', () => {
    givenPositions(position({ stopLossSource: 'Target', tradePlanId: null }));

    expect(fixture.debugElement.query(By.css('[data-test="move-sl-position"]'))).toBeNull();
  });

  it('không hiện nút khi vị thế chưa có SL ở đâu cả', () => {
    givenPositions(position({ stopLossSource: null, tradePlanId: null, stopLossPrice: null }));

    expect(fixture.debugElement.query(By.css('[data-test="move-sl-position"]'))).toBeNull();
  });

  it('gửi tới kế hoạch, KHÔNG ghi vào stop_loss_targets', () => {
    givenPositions(position());

    component.openMoveSl(position());
    component.submitMoveSl({ newStopLoss: 71000, reason: 'pyramid xong' });

    expect(planSpy.updateStopLoss).toHaveBeenCalledWith('plan-1', {
      newStopLoss: 71000, reason: 'pyramid xong'
    });
    expect(riskSpy.setStopLossTarget).not.toHaveBeenCalled();
  });

  it('mở modal nạp đúng SL hiện tại', () => {
    component.openMoveSl(position({ stopLossPrice: 64700 }));

    expect(component.slMoveCurrent).toBe(64700);
    expect(component.slMovePosition?.tradePlanId).toBe('plan-1');
  });

  it('lỗi API thì giữ modal mở và báo lỗi', () => {
    const notif = TestBed.inject(NotificationService) as jasmine.SpyObj<NotificationService>;
    planSpy.updateStopLoss.and.returnValue(throwError(() => new Error('boom')));

    component.openMoveSl(position());
    component.submitMoveSl({ newStopLoss: 71000 });

    expect(notif.error).toHaveBeenCalled();
    expect(component.slMovePosition).not.toBeNull();
    expect(component.slMoveSubmitting).toBe(false);
  });

  it('nhãn nguồn ngưỡng hiện "KH" khi SL đến từ kế hoạch', () => {
    givenPositions(position());

    const badge = fixture.debugElement.query(By.css('[data-test="sl-source-badge"]'));
    expect(badge?.nativeElement.textContent).toContain('KH');
  });
});
