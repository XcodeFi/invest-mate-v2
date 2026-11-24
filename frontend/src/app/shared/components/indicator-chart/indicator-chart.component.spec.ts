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

  describe('crosshair sync', () => {
    type Anchor = { chart: any };
    const anchors = () => (component as any).anchors as Anchor[];
    const move = (sourceIndex: number, param: any) =>
      (component as any).syncCrosshair(anchors()[sourceIndex].chart, param);

    it('moving the crosshair on one chart places it on the others using each own series value', () => {
      setInputs(layers(['RSI', 'MACD']));
      const [, rsi, macd] = anchors().map(a => a.chart);
      const rsiSpy = spyOn(rsi, 'setCrosshairPosition');
      const macdSpy = spyOn(macd, 'setCrosshairPosition');
      const priceSpy = spyOn(anchors()[0].chart, 'setCrosshairPosition');

      move(0, { time: '2026-01-10', logical: 9, point: { x: 1, y: 1 } });

      expect(priceSpy).not.toHaveBeenCalled();
      expect(rsiSpy).toHaveBeenCalledTimes(1);
      expect(macdSpy).toHaveBeenCalledTimes(1);
      expect(rsiSpy.calls.mostRecent().args[0]).toBe(59);
      expect(rsiSpy.calls.mostRecent().args[1]).toBe('2026-01-10');
    });

    it('clears the crosshair on the others when the pointer leaves', () => {
      setInputs(layers(['RSI', 'MACD']));
      const [price, rsi, macd] = anchors().map(a => a.chart);
      const rsiClear = spyOn(rsi, 'clearCrosshairPosition');
      const macdClear = spyOn(macd, 'clearCrosshairPosition');
      const priceClear = spyOn(price, 'clearCrosshairPosition');

      move(0, { time: undefined, point: undefined });

      expect(rsiClear).toHaveBeenCalledTimes(1);
      expect(macdClear).toHaveBeenCalledTimes(1);
      expect(priceClear).not.toHaveBeenCalled();
    });

    it('clears (instead of placing) on a chart whose own value is empty at that bar', () => {
      const gappy: GroupLayers = {
        overlays: [], priceLines: [], notes: [],
        panes: [{ title: 'Gappy', lines: [line('Gappy', i => (i === 9 ? null : 50 + i))] }]
      };
      setInputs(gappy);
      const gappyChart = anchors()[1].chart;
      const place = spyOn(gappyChart, 'setCrosshairPosition');
      const clear = spyOn(gappyChart, 'clearCrosshairPosition');

      move(0, { time: '2026-01-10', logical: 9, point: { x: 1, y: 1 } });

      expect(place).not.toHaveBeenCalled();
      expect(clear).toHaveBeenCalledTimes(1);
    });

    it('does not re-enter while it is already syncing', () => {
      setInputs(layers(['RSI']));
      const rsi = anchors()[1].chart;
      const spy = spyOn(rsi, 'setCrosshairPosition');
      (component as any).crosshairSyncing = true;

      move(0, { time: '2026-01-10', logical: 9, point: { x: 1, y: 1 } });

      expect(spy).not.toHaveBeenCalled();
    });
  });

  describe('chiều cao', () => {
    const heights = () => Array.from(containers()).map(c => parseInt((c as HTMLElement).style.height, 10));

    it('price chart is 280px; panes share the remaining 280px of the 560px budget', () => {
      setInputs(layers(['RSI']));
      expect(heights()).toEqual([280, 280]);

      fixture.componentRef.setInput('layers', layers(['RSI', 'MACD', 'Stochastic']));
      fixture.detectChanges();
      expect(heights()).toEqual([280, 93, 93, 93]);
    });

    it('each pane is at least 90px even when that exceeds the budget', () => {
      setInputs(layers(['ADX', 'RSI', 'MACD', 'OBV']));

      expect(heights()).toEqual([280, 90, 90, 90, 90]);
    });

    it('price chart is 280px when there are no panes', () => {
      setInputs(layers([]));

      expect(heights()).toEqual([280]);
    });
  });

  describe('giữ khoảng nhìn khi đổi nhóm', () => {
    // Layout của lightweight-charts trong Karma không đủ để zoom thật (setVisibleLogicalRange không đổi khoảng nhìn),
    // nên giả lập ở lớp TimeScaleApi: khoảng nhìn "người dùng đã chọn" trả về từ getVisibleLogicalRange.
    const USER_RANGE = { from: 5, to: 20 };
    const timeScaleProto = () => Object.getPrototypeOf((component as any).charts[0].timeScale());
    const stubRange = () => {
      const proto = timeScaleProto();
      const getSpy = spyOn(proto, 'getVisibleLogicalRange').and.returnValue(USER_RANGE);
      const setSpy = spyOn(proto, 'setVisibleLogicalRange').and.callThrough();
      // giả lập chỉ cần trong lúc dựng lại; sau đó trả về thật (chính lightweight-charts gọi hàm này khi phát sự kiện đổi khoảng nhìn)
      return { setSpy, releaseGet: () => getSpy.and.callThrough() };
    };

    // chart mới đặt lại khoảng nhìn ở khung hình kế tiếp; đợi một khung (đăng ký sau nên chạy sau)
    const nextFrame = (fn: () => void, done: DoneFn) => requestAnimationFrame(() => { fn(); done(); });

    it('keeps visible logical range when layers change with same dates', (done) => {
      setInputs(layers(['RSI', 'MACD']));
      const { setSpy, releaseGet } = stubRange();

      // cùng nội dung ngày nhưng là mảng mới (như khi dựng lại từ cache)
      fixture.componentRef.setInput('dates', [...dates]);
      fixture.componentRef.setInput('layers', layers(['ATR14']));
      fixture.detectChanges();
      releaseGet();

      nextFrame(() => expect(setSpy).toHaveBeenCalledWith(USER_RANGE), done);
    });

    it('resets range when dates change', (done) => {
      setInputs(layers(['RSI']));
      const { setSpy, releaseGet } = stubRange();

      const otherDates = dates.map(d => new Date(Date.parse(d) + 86_400_000 * 100).toISOString());
      fixture.componentRef.setInput('dates', otherDates);
      fixture.componentRef.setInput('layers', layers(['RSI']));
      fixture.detectChanges();
      releaseGet();

      nextFrame(() => expect(setSpy).not.toHaveBeenCalledWith(USER_RANGE), done);
    });

    it('destroying before the restore frame cancels it and never touches a removed chart', (done) => {
      setInputs(layers(['RSI']));
      const { setSpy, releaseGet } = stubRange();
      const cancelSpy = spyOn(window, 'cancelAnimationFrame').and.callThrough();

      fixture.componentRef.setInput('layers', layers(['ATR14']));
      fixture.detectChanges();
      releaseGet();
      fixture.destroy();

      expect(cancelSpy).toHaveBeenCalled();
      nextFrame(() => expect(setSpy).not.toHaveBeenCalledWith(USER_RANGE), done);
    });

    it('rebuilding twice before the frame restores only once, on the latest charts', (done) => {
      setInputs(layers(['RSI']));
      const { setSpy, releaseGet } = stubRange();

      fixture.componentRef.setInput('layers', layers(['ATR14']));
      fixture.detectChanges();
      fixture.componentRef.setInput('layers', layers(['MFI']));
      fixture.detectChanges();
      releaseGet();

      nextFrame(() => {
        const restores = setSpy.calls.allArgs().filter(a => a[0] === USER_RANGE);
        expect(restores.length).toBe(1);
      }, done);
    });
  });
});
