import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EstimateVsActualComponent } from './estimate-vs-actual.component';
import { EstimateDto } from '../../../core/services/trade-plan.service';

const estimate = (over: Partial<EstimateDto> = {}): EstimateDto => ({
  scenarios: [], evR: 0.71, impliedWinProbability: 50, feesIncluded: true,
  capturedAt: '2026-11-24T00:00:00Z', locked: true, lockedAt: '2026-11-25T00:00:00Z', ...over,
});

describe('EstimateVsActualComponent', () => {
  let fixture: ComponentFixture<EstimateVsActualComponent>;

  const text = (): string => fixture.nativeElement.textContent.replace(/\s+/g, ' ');

  function render(inputs: Record<string, unknown>) {
    TestBed.configureTestingModule({ imports: [EstimateVsActualComponent] });
    fixture = TestBed.createComponent(EstimateVsActualComponent);
    Object.entries({ direction: 'Buy', ...inputs }).forEach(([k, v]) => fixture.componentRef.setInput(k, v));
    fixture.detectChanges();
  }

  it('shows the estimate next to the actual R', () => {
    render({ estimate: estimate(), realizedR: 1.7 });

    expect(text()).toContain('Ước lượng');
    expect(text()).toContain('EV +0.71R');
    expect(text()).toContain('thắng 50%');
    expect(text()).toContain('Thực tế: +1.70R');
  });

  it('says the estimate is the locked one', () => {
    render({ estimate: estimate(), realizedR: 1.7 });

    expect(text()).toContain('khoá lúc khớp lệnh đầu tiên');
  });

  it('actual R unknown shows n/a, never 0R', () => {
    render({ estimate: estimate(), realizedR: null });

    expect(text()).toContain('Thực tế: n/a');
    expect(text()).not.toContain('0.00R');
  });

  it('notes when the estimate did not include fees', () => {
    render({ estimate: estimate({ feesIncluded: false }), realizedR: 1 });

    expect(text()).toContain('chưa trừ phí');
  });

  it('shows the reason given when the gate was passed with a negative or missing estimate', () => {
    render({ estimate: estimate({ evR: -0.4 }), realizedR: -1, evGateReason: 'Chấp nhận kỳ vọng âm vì lệnh thử nghiệm' });

    expect(text()).toContain('Lý do vượt cổng kỳ vọng');
    expect(text()).toContain('Chấp nhận kỳ vọng âm vì lệnh thử nghiệm');
  });

  it('flags a plan that was entered without passing the expectation gate', () => {
    render({ estimate: null, realizedR: 0.5, evGateSkipped: true });

    expect(text()).toContain('Vào lệnh không qua cổng kỳ vọng');
  });

  it('a plan without any estimate says so', () => {
    render({ estimate: null, realizedR: 0.5 });

    expect(text()).toContain('chưa có ước lượng');
    expect(text()).toContain('Thực tế: +0.50R');
  });

  it('estimate whose EV could not be computed shows n/a for EV', () => {
    render({ estimate: estimate({ evR: null, impliedWinProbability: null }), realizedR: 1 });

    expect(text()).toContain('EV n/a');
  });

  it('renders nothing for sell plans', () => {
    render({ direction: 'Sell', estimate: null, realizedR: null, evGateSkipped: true });

    expect(fixture.nativeElement.querySelector('[data-testid="estimate-vs-actual"]')).toBeNull();
  });
});
