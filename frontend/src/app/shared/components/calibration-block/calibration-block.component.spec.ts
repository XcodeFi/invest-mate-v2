import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CalibrationBlockComponent } from './calibration-block.component';
import { CalibrationBucketDto, CalibrationResultDto, TradePlanService } from '../../../core/services/trade-plan.service';

const bucket = (over: Partial<CalibrationBucketDto> = {}): CalibrationBucketDto => ({
  label: '55–70%', low: 0.55, high: 0.7, n: 8, avgEstimated: 0.62, actualWinRate: 0.5, sufficient: true, ...over,
});

const empty = (): CalibrationResultDto => ({
  n: 0, brier: null, optimismPoints: null, sufficient: false,
  buckets: [
    bucket({ label: '0–40%', low: 0, high: 0.4, n: 0, avgEstimated: null, actualWinRate: null, sufficient: false }),
    bucket({ label: '40–55%', low: 0.4, high: 0.55, n: 0, avgEstimated: null, actualWinRate: null, sufficient: false }),
    bucket({ n: 0, avgEstimated: null, actualWinRate: null, sufficient: false }),
    bucket({ label: '70–100%', low: 0.7, high: 1, n: 0, avgEstimated: null, actualWinRate: null, sufficient: false }),
  ],
});

describe('CalibrationBlockComponent', () => {
  let fixture: ComponentFixture<CalibrationBlockComponent>;
  let planSpy: jasmine.SpyObj<TradePlanService>;

  const text = (): string => fixture.nativeElement.textContent;
  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);

  function setup(result: CalibrationResultDto | 'error') {
    planSpy = jasmine.createSpyObj<TradePlanService>('TradePlanService', ['getCalibration']);
    planSpy.getCalibration.and.returnValue(result === 'error' ? throwError(() => new Error('500')) : of(result));
    TestBed.configureTestingModule({
      imports: [CalibrationBlockComponent],
      providers: [{ provide: TradePlanService, useValue: planSpy }],
    });
    fixture = TestBed.createComponent(CalibrationBlockComponent);
    fixture.detectChanges();
  }

  it('empty state when no locked estimates', () => {
    setup(empty());

    expect(q('calibration-empty')).not.toBeNull();
    expect(text()).toContain('ước lượng xác suất');
    expect(text()).toContain('đã review');
    expect(q('calibration-row')).toBeNull();
    expect(q('calibration-error')).toBeNull();
  });

  it('faded bucket when n<5', () => {
    const result = empty();
    result.n = 12;
    result.buckets[2] = bucket({ n: 3, sufficient: false });
    result.buckets[3] = bucket({ label: '70–100%', n: 9, sufficient: true });
    setup(result);

    const rows = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="calibration-row"]') as NodeListOf<HTMLElement>);
    const faded = rows.filter(r => r.classList.contains('opacity-50'));
    expect(faded.some(r => r.textContent!.includes('chưa đủ mẫu'))).toBeTrue();
    expect(rows.find(r => r.textContent!.includes('70–100%'))!.classList.contains('opacity-50')).toBeFalse();
  });

  it('brier hidden when n<15', () => {
    const result = empty();
    result.n = 14;
    result.buckets[2] = bucket({ n: 14 });
    setup(result);

    expect(q('calibration-brier')).toBeNull();
    expect(q('calibration-summary')).toBeNull();
    expect(text()).toContain('15');   // nhắc cần tối thiểu 15 mẫu
  });

  it('does not trust a brier value that arrives below the sample threshold', () => {
    const result = empty();
    result.n = 10; result.sufficient = false; result.brier = 0.2; result.optimismPoints = 15;
    result.buckets[2] = bucket({ n: 10 });
    setup(result);

    expect(q('calibration-brier')).toBeNull();
    expect(q('calibration-summary')).toBeNull();
  });

  it('brier and the optimism sentence show when n>=15', () => {
    const result = empty();
    result.n = 20;
    result.sufficient = true;
    result.brier = 0.1867;
    result.optimismPoints = 12.4;
    result.buckets[2] = bucket({ n: 20 });
    setup(result);

    expect(q('calibration-brier')!.textContent).toContain('0,187');
    expect(q('calibration-summary')!.textContent).toContain('lạc quan');
    expect(q('calibration-summary')!.textContent).toContain('12');
  });

  it('says pessimistic for a negative gap', () => {
    const result = empty();
    result.n = 20; result.sufficient = true; result.brier = 0.2; result.optimismPoints = -8;
    result.buckets[2] = bucket({ n: 20 });
    setup(result);

    expect(q('calibration-summary')!.textContent).toContain('bi quan');
  });

  it('says close for a gap under one point', () => {
    const result = empty();
    result.n = 20; result.sufficient = true; result.brier = 0.2; result.optimismPoints = 0.4;
    result.buckets[2] = bucket({ n: 20 });
    setup(result);

    expect(q('calibration-summary')!.textContent).toContain('khá khớp');
  });

  it('shows an error line when the request fails', () => {
    setup('error');

    expect(q('calibration-error')).not.toBeNull();
    expect(q('calibration-row')).toBeNull();
  });
});
