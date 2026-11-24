import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { PlanExpectationComponent } from './plan-expectation.component';
import { ScenarioRow } from './expectation-math';
import { ExpectancyStatsDto, StrategyExpectancyDto, TradePlanService } from '../../../core/services/trade-plan.service';

const stats = (over: Partial<ExpectancyStatsDto> = {}): ExpectancyStatsDto => ({
  strategyId: 's1', strategyName: 'Breakout', n: 19, wins: 8, losses: 9, draws: 2,
  winRate: 0.42, winRateLow95: 0.22, winRateHigh95: 0.64, avgWinR: 2, avgLossR: -1,
  expectancyR: 0.3, profitFactorR: 1.5, ...over,
});

describe('PlanExpectationComponent', () => {
  let fixture: ComponentFixture<PlanExpectationComponent>;
  let component: PlanExpectationComponent;
  let planSpy: jasmine.SpyObj<TradePlanService>;

  const text = (): string => fixture.nativeElement.textContent.replace(/\s+/g, ' ');
  const rowEls = () => fixture.nativeElement.querySelectorAll('[data-testid="scenario-row"]') as NodeListOf<HTMLElement>;
  const probInput = (i: number) => rowEls()[i].querySelector('[data-testid="prob-input"]') as HTMLInputElement;

  function setup(opts: {
    direction?: string; scenarios?: ScenarioRow[]; locked?: boolean; strategyId?: string;
    base?: StrategyExpectancyDto | 'error';
  } = {}) {
    planSpy = jasmine.createSpyObj<TradePlanService>('TradePlanService', ['getExpectancy']);
    const base = opts.base ?? { total: stats({ strategyId: null, strategyName: 'Tất cả' }), strategies: [stats()] };
    planSpy.getExpectancy.and.returnValue(base === 'error' ? throwError(() => new Error('500')) : of(base));
    TestBed.configureTestingModule({
      imports: [PlanExpectationComponent],
      providers: [{ provide: TradePlanService, useValue: planSpy }],
    });
    fixture = TestBed.createComponent(PlanExpectationComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('direction', opts.direction ?? 'Buy');
    fixture.componentRef.setInput('entry', 100);
    fixture.componentRef.setInput('stopLoss', 90);
    fixture.componentRef.setInput('target', 130);
    fixture.componentRef.setInput('quantity', 100);
    fixture.componentRef.setInput('strategyId', opts.strategyId ?? 's1');
    fixture.componentRef.setInput('locked', opts.locked ?? false);
    if (opts.scenarios) fixture.componentRef.setInput('scenarios', opts.scenarios);
    fixture.detectChanges();
  }

  const type = (i: number, value: string) => {
    const el = probInput(i);
    el.value = value;
    el.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  it('shows default three rows with empty probabilities', () => {
    setup();

    expect(rowEls().length).toBe(3);
    const labels = Array.from(rowEls()).map(r => (r.querySelector('input[type="text"]') as HTMLInputElement).value);
    expect(labels).toEqual(['Đạt kế hoạch', 'Cắt lỗ', 'Thoát sớm / hoà']);
    for (let i = 0; i < 3; i++) expect(probInput(i).value).toBe('');
  });

  it('never prefills a probability from the base rate (anti-anchoring)', () => {
    setup();

    for (let i = 0; i < 3; i++) expect(probInput(i).value).toBe('');
    expect(text()).toContain('42%');   // tỉ lệ nền chỉ hiện bên cạnh
  });

  it('ev updates when probabilities sum to 100', () => {
    setup();

    type(0, '35');
    type(1, '50');
    type(2, '15');

    expect(text()).toContain('Tổng xác suất: 100%');
    expect(text()).toMatch(/EV[^+\-]*[+−-]?0[.,]\d+R/);
    expect(component.result.evR).not.toBeNull();
  });

  it('warns "Tổng xác suất phải là 100%" and shows no EV when the sum is wrong', () => {
    setup();

    type(0, '60');
    type(1, '20');

    expect(text()).toContain('Tổng xác suất phải là 100%');
    expect(component.result.evR).toBeNull();
    expect(text()).toContain('Chưa tính được kỳ vọng');
  });

  it('shows the strategy base rate with n and the 95% interval', () => {
    setup();

    expect(text()).toContain('Breakout');
    expect(text()).toContain('42%');
    expect(text()).toContain('19 lệnh');
    expect(text()).toMatch(/22%\s*[–-]\s*64%/);
    expect(text()).toContain('+0.30R');
  });

  it("base rate with n<10 says 'chưa đủ mẫu để tin'", () => {
    setup({ base: { total: stats({ strategyId: null, strategyName: 'Tất cả', n: 5 }), strategies: [stats({ n: 5 })] } });

    expect(text()).toContain('chưa đủ mẫu để tin');
  });

  it('falls back to the total row when the plan has no strategy or it has no closed campaigns', () => {
    setup({ strategyId: '', base: { total: stats({ strategyId: null, strategyName: 'Tất cả', n: 30 }), strategies: [stats()] } });

    expect(text()).toContain('Tất cả');
    expect(text()).toContain('30 lệnh');
  });

  it('shows an empty-state when there are no closed campaigns yet', () => {
    setup({ base: { total: stats({ strategyId: null, strategyName: 'Tất cả', n: 0, winRate: null, expectancyR: null }), strategies: [] } });

    expect(text()).toContain('Chưa có chiến dịch đã đóng');
  });

  it('keeps working when the base rate cannot be loaded', () => {
    setup({ base: 'error' });

    expect(rowEls().length).toBe(3);
    expect(text()).toContain('Không tải được tỉ lệ nền');
  });

  it('sell plan hides the expectation block', () => {
    setup({ direction: 'Sell' });

    expect(fixture.nativeElement.querySelector('[data-testid="plan-expectation"]')).toBeNull();
    expect(planSpy.getExpectancy).not.toHaveBeenCalled();
  });

  it('shows the lock banner and keeps the table editable when the estimate is locked', () => {
    setup({ locked: true });

    expect(text()).toContain('Ước lượng đã khoá lúc khớp lệnh đầu tiên');
    expect(probInput(0).disabled).toBeFalse();
  });

  it('emits the rows on every edit so the parent can save them', () => {
    setup();
    const emitted: ScenarioRow[][] = [];
    component.scenariosChange.subscribe(r => emitted.push(r));

    type(0, '40');

    expect(emitted.length).toBe(1);
    expect(emitted[0][0].probabilityPercent).toBe(40);
  });

  it('adds and removes rows', () => {
    setup();

    (fixture.nativeElement.querySelector('[data-testid="add-row"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(rowEls().length).toBe(4);

    (rowEls()[3].querySelector('[data-testid="remove-row"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(rowEls().length).toBe(3);
  });

  it('cannot remove the last remaining row', () => {
    setup({ scenarios: [{ label: 'Duy nhất', probabilityPercent: null, exitPrice: 120 }] });

    expect((rowEls()[0].querySelector('[data-testid="remove-row"]') as HTMLButtonElement).disabled).toBeTrue();
  });

  it('exit prices of untouched default rows follow the plan prices', () => {
    setup();

    fixture.componentRef.setInput('target', 150);
    fixture.componentRef.setInput('entry', 105);
    fixture.detectChanges();

    expect(component.rows[0].exitPrice).toBe(150);
    expect(component.rows[2].exitPrice).toBe(105);
  });

  it('keeps a user-edited exit price when the plan prices change', () => {
    setup();
    const exit = rowEls()[0].querySelector('[data-testid="exit-input"]') as HTMLInputElement;
    exit.value = '125';
    exit.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    fixture.componentRef.setInput('target', 150);
    fixture.detectChanges();

    expect(component.rows[0].exitPrice).toBe(125);
  });

  it('shows the server-side expectation (fees included) when the plan was saved', () => {
    setup();
    fixture.componentRef.setInput('serverExpectation', { evR: 0.61, impliedWinPercent: 50, evAmount: 610 });
    fixture.detectChanges();

    expect(text()).toContain('Đã trừ phí');
    expect(text()).toContain('+0.61R');
  });
});
