import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PositionSizingComponent } from './position-sizing.component';
import { PortfolioService } from '../../core/services/portfolio.service';
import { ConservativeKellyDto, PositionSizingResult, RiskService } from '../../core/services/risk.service';

const kelly = (over: Partial<ConservativeKellyDto> = {}): ConservativeKellyDto => ({
  n: 24, winRateLower95: 0.3883, payoffRatio: 2, fullKelly: 0.0825, halfKellyCapped: 0.0413, reason: null, ...over,
});

const sizing = (k: ConservativeKellyDto): PositionSizingResult => ({
  recommendedModel: 'fixed_risk', kellyFromHistory: k,
  models: k.halfKellyCapped ? [{ model: 'kelly', modelVi: 'Kelly Criterion', shares: 400, positionValue: 40_000_000,
    positionPercent: 4.1, riskAmount: 2_000_000, withinLimit: true, note: 'Half-Kelly: 4.1% (Full: 8.3%)' }] : [],
});

describe('PositionSizingComponent: Kelly theo dữ liệu', () => {
  let fixture: ComponentFixture<PositionSizingComponent>;
  let component: PositionSizingComponent;
  let riskSpy: jasmine.SpyObj<RiskService>;

  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);

  function setup(result: PositionSizingResult) {
    riskSpy = jasmine.createSpyObj<RiskService>('RiskService', ['getRiskProfile', 'calculatePositionSizing']);
    riskSpy.calculatePositionSizing.and.returnValue(of(result));
    TestBed.configureTestingModule({
      imports: [PositionSizingComponent],
      providers: [
        { provide: RiskService, useValue: riskSpy },
        { provide: PortfolioService, useValue: { getAll: () => of([]) } },
      ],
    });
    fixture = TestBed.createComponent(PositionSizingComponent);
    component = fixture.componentInstance;
    component.input.entryPrice = 100_000;
    component.input.stopLossPrice = 95_000;
    fixture.detectChanges();
  }

  function setUseHistory(on: boolean) {
    component.useHistory = on;
    component.onUseHistoryChange();
    fixture.detectChanges();
  }

  it('is off by default and does not call the server for history', () => {
    setup(sizing(kelly()));

    expect(q('kelly-history')).toBeNull();
    expect(riskSpy.calculatePositionSizing).not.toHaveBeenCalled();
  });

  it('kelly toggle shows n and lower bound', () => {
    setup(sizing(kelly()));
    setUseHistory(true);

    const call = riskSpy.calculatePositionSizing.calls.mostRecent();
    expect(call.args[1]).toEqual(jasmine.objectContaining({ useHistory: true }));
    const block = q('kelly-history')!.textContent!;
    expect(block).toContain('n = 24');
    expect(block).toContain('39%');         // cận dưới 38,83%
    expect(block).toContain('cỡ vị thế thận trọng');
    expect(block).toContain('không phải lời khuyên');
    expect(block).toContain('4,1%');        // half-Kelly
    expect(q('kelly-shares')!.textContent).toContain('400');
  });

  it('kelly reason when no edge', () => {
    setup(sizing(kelly({ winRateLower95: 0.2, payoffRatio: 1, fullKelly: 0, halfKellyCapped: 0,
      reason: 'Không có lợi thế theo dữ liệu — Kelly = 0.' })));
    setUseHistory(true);

    const block = q('kelly-history')!.textContent!;
    expect(block).toContain('Không có lợi thế theo dữ liệu');
    expect(block).toContain('n = 24');
    expect(block).not.toContain('Half-Kelly');
    expect(q('kelly-shares')).toBeNull();
  });

  it('kelly reason when there is not enough data', () => {
    setup(sizing(kelly({ n: 7, winRateLower95: null, payoffRatio: null, fullKelly: null, halfKellyCapped: null,
      reason: 'Chưa đủ dữ liệu: cần tối thiểu 20 lệnh thắng/thua, hiện có 7.' })));
    setUseHistory(true);

    expect(q('kelly-history')!.textContent).toContain('Chưa đủ dữ liệu');
    expect(q('kelly-history')!.textContent).toContain('n = 7');
  });

  it('turning it off hides the block', () => {
    setup(sizing(kelly()));
    setUseHistory(true);
    setUseHistory(false);

    expect(q('kelly-history')).toBeNull();
  });
});
