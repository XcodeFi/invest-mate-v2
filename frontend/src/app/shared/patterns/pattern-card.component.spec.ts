import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PatternCandidate } from '../../core/services/market-data.service';
import { PatternCardComponent } from './pattern-card.component';

const cup = (over: Partial<PatternCandidate> = {}): PatternCandidate => ({
  type: 'cup-with-handle', typeVi: 'Cốc tay cầm', state: 'near-pivot', stateVi: 'Sát pivot',
  startDate: '2026-03-02', bottomDate: '2026-05-04', rightLipDate: '2026-08-03',
  cupLengthWeeks: 22, cupDepthPct: 21.5, handleLengthSessions: 8, handleDepthPct: 9.4,
  pivotPrice: 100, distanceToPivotPct: 2.1, volumeVsAvg50: 0.8,
  stopPrice: 90.5, riskPct: 9.5, targetPrice: 121.5, rewardPct: 21.5, rewardRisk: 2.3,
  criteria: [
    { key: 'cup-depth', rule: 'Cốc sâu 12–33% so với đỉnh trái', value: 21.5, unit: '%', passed: true, required: true },
    { key: 'handle-depth', rule: 'Tay cầm sâu 8–15% so với vành phải', value: 9.4, unit: '%', passed: true, required: true },
    { key: 'breakout-volume', rule: 'Khối lượng nến cuối ≥ 1,4× trung bình 50 phiên', value: 0.8, unit: '×', passed: false, required: false },
  ],
  disclaimer: 'Ứng viên theo ngưỡng mặc định có thể chỉnh, không phải tín hiệu mua. Chưa vượt pivot kèm khối lượng thì chưa xác nhận.',
  ...over,
});

describe('PatternCardComponent: Ứng viên mô hình Cốc tay cầm', () => {
  let fixture: ComponentFixture<PatternCardComponent>;
  const el = () => fixture.nativeElement as HTMLElement;
  const q = (id: string) => el().querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const qa = (id: string) => Array.from(el().querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[];
  const text = (id: string) => (q(id)?.textContent ?? '').replace(/\s+/g, ' ').trim();
  const show = (c: PatternCandidate | null, over: Partial<PatternCardComponent> = {}) => {
    fixture = TestBed.createComponent(PatternCardComponent);
    fixture.componentInstance.candidate = c;
    Object.assign(fixture.componentInstance, over);
    fixture.detectChanges();
  };

  beforeEach(() => TestBed.configureTestingModule({ imports: [PatternCardComponent] }));

  it('renders nothing without a candidate', () => {
    show(null);
    expect(q('pattern-card')).toBeNull();
  });

  it('titles the card "Ứng viên mô hình: Cốc tay cầm" and shows the state', () => {
    show(cup());
    expect(el().textContent).toContain('Ứng viên mô hình: Cốc tay cầm');
    expect(text('pattern-state')).toBe('Sát pivot');
    expect(q('pattern-card')!.dataset['state']).toBe('near-pivot');
  });

  it('lists the measurements', () => {
    show(cup());
    expect(text('pattern-cup')).toBe('22,0 tuần, sâu 21,5%');
    expect(text('pattern-handle')).toBe('8 phiên, sâu 9,4%');
    expect(text('pattern-pivot')).toBe('100 (còn 2,1% tới pivot)');
    expect(text('pattern-volume')).toBe('0,80× trung bình 50 phiên');
  });

  it('a pivot already broken reads "đã trên pivot"; a missing volume ratio is a dash, never 0', () => {
    show(cup({ distanceToPivotPct: -1.4, volumeVsAvg50: null }));
    expect(text('pattern-pivot')).toContain('đã trên pivot 1,4%');
    expect(text('pattern-volume')).toBe('—');
  });

  it('shows each criterion with ✓ or ✗, its value, and marks the optional one as reference only', () => {
    show(cup());
    const rows = qa('pattern-criterion');

    expect(rows.map(r => r.dataset['passed'])).toEqual(['true', 'true', 'false']);
    expect(rows[0].textContent).toContain('✓');
    expect(rows[0].textContent).toContain('Cốc sâu 12–33%');
    expect(rows[0].textContent).toContain('21,5%');
    expect(rows[2].textContent).toContain('✗');
    expect(rows[2].textContent).toContain('0,8 ×');
    expect(rows[2].textContent).toContain('tham khảo, không bắt buộc');
    expect(rows[0].textContent).not.toContain('tham khảo');
  });

  it('shows the deterministic downside: "Nếu sai: −X% từ pivot tới điểm cắt lỗ", the stop, the measured target and R', () => {
    show(cup());

    expect(text('pattern-risk')).toBe('Nếu sai: −9,5% từ pivot tới điểm cắt lỗ');
    expect(text('pattern-downside')).toContain('Cắt lỗ 90,5, ngay dưới đáy tay cầm');
    expect(text('pattern-target')).toContain('Mục tiêu đo 121,5 (+21,5% từ pivot)');
    expect(text('pattern-target')).toContain('2,3R');
    expect(text('pattern-downside')).toContain('quy ước hình học, không phải dự báo');
  });

  it('always carries the disclaimer from the server', () => {
    show(cup());
    expect(text('pattern-disclaimer')).toContain('không phải tín hiệu mua');
    expect(text('pattern-disclaimer')).toContain('Chưa vượt pivot kèm khối lượng thì chưa xác nhận');
  });

  it('has no score, confidence or probability wording anywhere', () => {
    show(cup());
    expect((el().textContent ?? '').toLowerCase()).not.toMatch(/điểm số|độ tin cậy|xác suất thành công|confidence|score/);
  });

  it('the draw-on-chart checkbox reflects the input and emits the new value', () => {
    show(cup(), { showOnChart: true });
    const emitted: boolean[] = [];
    fixture.componentInstance.showOnChartChange.subscribe((v: boolean) => emitted.push(v));
    const box = q('pattern-show') as HTMLInputElement;
    expect(box.checked).toBeTrue();

    box.checked = false;
    box.dispatchEvent(new Event('change'));

    expect(emitted).toEqual([false]);
  });

  it('asks the user to pick an indicator group when the chart is not loaded yet', () => {
    show(cup(), { showOnChart: true, chartReady: false });
    expect(q('pattern-chart-hint')).not.toBeNull();

    show(cup(), { showOnChart: true, chartReady: true });
    expect(q('pattern-chart-hint')).toBeNull();
  });

  it('server strings are shown as text, never as HTML', () => {
    show(cup({ stateVi: '<img src=x onerror=alert(1)>', criteria: [{ key: 'x', rule: '<b>bold</b>', value: 1, unit: '%', passed: true, required: true }] }));

    expect(el().querySelector('img')).toBeNull();
    expect(el().querySelector('b')).toBeNull();
    expect(text('pattern-state')).toContain('<img');
  });
});
