import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IndicatorChartComponent } from './indicator-chart.component';
import { GroupLayers, IndicatorChartCandles, LineLayer } from './indicator-chart.types';

const N = 40;
const dates = Array.from({ length: N }, (_, i) => {
  const d = new Date(Date.UTC(2026, 0, 1 + i));
  return d.toISOString();
});
const series = (f: (i: number) => number | null): (number | null)[] => Array.from({ length: N }, (_, i) => f(i));
const candles: IndicatorChartCandles = {
  open: series(i => 100 + i) as number[],
  high: series(i => 102 + i) as number[],
  low: series(i => 98 + i) as number[],
  close: series(i => 101 + i) as number[],
};
const line = (label: string, f: (i: number) => number | null = i => 50 + i): LineLayer =>
  ({ label, values: series(f), color: '#2563eb' });

const layers = (paneTitles: string[], notes: string[] = [], overlays: LineLayer[] = []): GroupLayers => ({
  overlays,
  panes: paneTitles.map(t => ({ title: t, lines: [line(t)] })),
  priceLines: [],
  notes,
});

describe('IndicatorChartComponent', () => {
  let fixture: ComponentFixture<IndicatorChartComponent>;
  let component: IndicatorChartComponent;

  const containers = () =>
    (fixture.nativeElement as HTMLElement).querySelectorAll('.indicator-chart-container');

  const setInputs = (l: GroupLayers) => {
    fixture.componentRef.setInput('dates', dates);
    fixture.componentRef.setInput('candles', candles);
    fixture.componentRef.setInput('layers', l);
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [IndicatorChartComponent] }).compileComponents();
    fixture = TestBed.createComponent(IndicatorChartComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => fixture.destroy());

  it('renders 1 + panes.length chart containers', () => {
    setInputs(layers(['RSI', 'MACD', 'Stochastic']));

    expect(containers().length).toBe(4);
    expect(component.chartCount).toBe(4);
  });

  it('renders only the price chart when there are no panes', () => {
    setInputs(layers([]));

    expect(containers().length).toBe(1);
  });

  it('renders nothing (and does not throw) when there is no data', () => {
    fixture.componentRef.setInput('dates', []);
    fixture.componentRef.setInput('candles', { open: [], high: [], low: [], close: [] });
    fixture.componentRef.setInput('layers', layers(['RSI']));

    expect(() => fixture.detectChanges()).not.toThrow();
    expect(containers().length).toBe(0);
    expect(component.chartCount).toBe(0);
  });

  it('rebuilds panes when layers change', () => {
    setInputs(layers(['RSI', 'MACD', 'Stochastic']));
    expect(containers().length).toBe(4);

    fixture.componentRef.setInput('layers', layers(['ATR14']));
    fixture.detectChanges();

    expect(containers().length).toBe(2);
    expect(component.chartCount).toBe(2);
  });

  it('shows notes text', () => {
    setInputs(layers([], ['EMA50: Không đủ dữ liệu']));

    const notes = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="indicator-chart-notes"]');
    expect(notes?.textContent).toContain('Không đủ dữ liệu');
    expect(notes?.textContent).toContain('EMA50');
  });

  it('ngOnDestroy removes all chart instances', () => {
    const errorSpy = spyOn(console, 'error');
    setInputs(layers(['RSI', 'MACD']));
    expect(component.chartCount).toBe(3);

    const removeSpies = ((component as any).charts as { remove: () => void }[])
      .map(c => spyOn(c, 'remove').and.callThrough());
    const root = fixture.nativeElement.querySelector('[data-testid="indicator-chart-root"]') as HTMLElement;
    fixture.destroy();

    removeSpies.forEach(spy => expect(spy).toHaveBeenCalledTimes(1));
    expect(component.chartCount).toBe(0);
    expect(root.children.length).toBe(0);
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it('skips null points without throwing', () => {
    const gappy = line('Gappy', i => (i % 3 === 0 ? null : 50 + i));
    const allNullButOne = line('One', i => (i === 10 ? 55 : null));
    const l: GroupLayers = {
      overlays: [gappy],
      panes: [{ title: 'Gappy pane', lines: [gappy, allNullButOne], guides: [30, 70] }],
      priceLines: [{ label: 'Giá vào', price: 110 }],
      notes: [],
    };

    expect(() => setInputs(l)).not.toThrow();
    expect(component.chartCount).toBe(2);
  });

  it('ignores non-increasing dates instead of throwing', () => {
    const dup = [...dates];
    dup[5] = dup[4];
    fixture.componentRef.setInput('dates', dup);
    fixture.componentRef.setInput('candles', candles);
    fixture.componentRef.setInput('layers', layers(['RSI']));

    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.chartCount).toBe(2);
  });

  it('shows the latest values in the readout of each pane', () => {
    setInputs(layers(['RSI']));

    const readouts = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.indicator-chart-readout')
    ).map(e => e.textContent ?? '');
    expect(readouts.length).toBe(2);
    expect(readouts[0]).toContain('Đóng cửa');
    expect(readouts[1]).toContain('RSI: 89');
  });
});
