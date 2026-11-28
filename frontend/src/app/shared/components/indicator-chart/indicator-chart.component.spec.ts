import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IndicatorChartComponent } from './indicator-chart.component';
import { ThemeService } from '../../../core/services/theme.service';
import { contrastRatio } from '../../theme/contrast';
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
    // Các test cũ kiểm màu ở chế độ sáng (nguyên như trước khi có chế độ tối); chế độ tối có khối test riêng ở cuối file
    TestBed.inject(ThemeService).set('light', false);
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
      panes: [{ title: 'Gappy pane', lines: [gappy, allNullButOne], guides: [{ value: 30, label: 'Thấp' }, { value: 70, label: 'Cao' }] }],
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

  describe('chú giải (legend)', () => {
    const root = () => (fixture.nativeElement as HTMLElement).querySelector('[data-testid="indicator-chart-root"]') as HTMLElement;
    const items = () => Array.from(root().querySelectorAll('[data-testid="legend-item"]')) as HTMLElement[];
    const itemOf = (label: string) => items().find(i => i.textContent!.includes(label))!;
    /** Chuẩn hoá màu CSS (hex → rgb) để so với giá trị trình duyệt trả về. */
    const norm = (c: string) => { const e = document.createElement('i'); e.style.color = c; return e.style.color; };

    it('legend shows a color swatch per line matching series color', () => {
      const green: LineLayer = { label: 'Đường xanh', values: series(i => 10 + i), color: '#16a34a' };
      const red: LineLayer = { label: 'Đường đỏ', values: series(i => 20 + i), color: '#dc2626' };
      setInputs({ overlays: [green], panes: [{ title: 'Tầng', lines: [red] }], priceLines: [], notes: [] });

      const swatch = (label: string) => itemOf(label).querySelector('[data-testid="legend-swatch"]') as HTMLElement;
      expect(swatch('Đường xanh').style.backgroundColor).toBe(norm('#16a34a'));
      expect(swatch('Đường đỏ').style.backgroundColor).toBe(norm('#dc2626'));
    });

    it('value text is tinted in its line colour', () => {
      const orange: LineLayer = { label: 'Đường cam', values: series(i => 10 + i), color: '#f59e0b' };
      setInputs({ overlays: [orange], panes: [{ title: 'Tầng', lines: [line('Đường xanh')] }], priceLines: [], notes: [] });

      const valueEl = (label: string) => itemOf(label).querySelector('[data-testid="legend-value"]') as HTMLElement;
      expect(valueEl('Đường cam').style.color).toBe(norm('#f59e0b'));
      expect(valueEl('Đường xanh').style.color).toBe(norm('#2563eb'));
    });

    it('histogram lines get a square swatch, plain lines a thin bar', () => {
      const hist: LineLayer = { label: 'Cột', values: series(i => i), color: '#94a3b8', kind: 'histogram' };
      const plain: LineLayer = { label: 'Đường', values: series(i => i), color: '#2563eb' };
      setInputs({ overlays: [], panes: [{ title: 'Tầng', lines: [hist, plain] }], priceLines: [], notes: [] });

      const swatch = (label: string) => itemOf(label).querySelector('[data-testid="legend-swatch"]') as HTMLElement;
      expect(swatch('Cột').dataset['shape']).toBe('square');
      expect(swatch('Đường').dataset['shape']).toBe('bar');
    });

    it('legend shows last value when not hovering', () => {
      setInputs(layers(['RSI']));

      const value = itemOf('RSI').querySelector('[data-testid="legend-value"]')!.textContent;
      expect(value).toBe('89');
    });

    it('legend shows the hovered value when the pointer is on a bar', () => {
      setInputs(layers(['RSI']));

      (component as any).updateReadouts({ logical: 9, point: { x: 1, y: 1 } });

      expect(itemOf('RSI').querySelector('[data-testid="legend-value"]')!.textContent).toBe('59');
    });

    describe('OHLC row', () => {
      const ohlc = () => root().querySelector('[data-testid="ohlc-row"]') as HTMLElement;

      it('shows open/high/low/close of the last bar and the change when not hovering', () => {
        setInputs(layers(['RSI']));

        const text = ohlc().textContent!;
        expect(text).toContain('Mở 139');
        expect(text).toContain('Cao 141');
        expect(text).toContain('Thấp 137');
        expect(text).toContain('Đóng 140');
        expect(text).toContain('+1 (+0,72%)');
      });

      it('follows the hover', () => {
        setInputs(layers(['RSI']));

        (component as any).updateReadouts({ logical: 9, point: { x: 1, y: 1 } });

        const text = ohlc().textContent!;
        expect(text).toContain('Mở 109');
        expect(text).toContain('Cao 111');
        expect(text).toContain('Thấp 107');
        expect(text).toContain('Đóng 110');
        expect(text).toContain('+1 (+0,92%)');
      });

      it('colours the change green when up and red when down', () => {
        setInputs(layers([]));
        const change = () => ohlc().querySelector('[data-testid="ohlc-change"]') as HTMLElement;
        expect(change().style.color).toBe(norm('#16a34a'));

        const down: IndicatorChartCandles = { ...candles, close: series(i => 200 - i) as number[] };
        fixture.componentRef.setInput('candles', down);
        fixture.detectChanges();
        expect(change().style.color).toBe(norm('#dc2626'));
      });

      it('first bar has no previous close so no change is shown', () => {
        setInputs(layers([]));

        (component as any).updateReadouts({ logical: 0, point: { x: 1, y: 1 } });

        expect(ohlc().querySelector('[data-testid="ohlc-change"]')!.textContent).toBe('');
      });
    });

    describe('tiêu đề đường ngang trên biểu đồ', () => {
      it('the chart title is the short label when there is one, while the legend below keeps the full label', () => {
        setInputs(layers([]));
        const proto = Object.getPrototypeOf((component as any).charts[0]);
        const original = proto.addCandlestickSeries;
        const titles: string[] = [];
        spyOn(proto, 'addCandlestickSeries').and.callFake(function (this: any, ...args: any[]) {
          const series = original.apply(this, args);
          const create = series.createPriceLine.bind(series);
          series.createPriceLine = (opts: any) => { titles.push(opts.title); return create(opts); };
          return series;
        });

        setInputs({ overlays: [], panes: [], notes: [], priceLines: [
          { label: 'Pivot 108 (còn 2,1% tới pivot)', shortLabel: 'Pivot', price: 108 },
          { label: 'Giá vào', price: 100 },
        ] });

        expect(titles).toEqual(['Pivot', 'Giá vào']);
        const items = Array.from(root().querySelectorAll('[data-testid="price-line-item"]')).map(i => (i.textContent ?? '').trim());
        expect(items[0]).toContain('Pivot 108 (còn 2,1% tới pivot)');
      });

      it('the measured target is drawn green like other take-profit levels and the stop red', () => {
        setInputs({ overlays: [], panes: [], notes: [], priceLines: [
          { label: 'Mục tiêu đo 125 (+15,7% · 1,6R)', price: 125 },
          { label: 'Cắt lỗ 97,5 (−9,7% từ pivot)', price: 97.5 },
          { label: 'Pivot 108', price: 108 },
        ] });

        const swatches = Array.from(root().querySelectorAll('[data-testid="price-line-swatch"]')).map(s => (s as HTMLElement).style.borderTopColor);
        expect(swatches).toEqual(['rgb(21, 128, 61)', 'rgb(220, 38, 38)', 'rgb(37, 99, 235)']);   // giao diện sáng: xanh lá đậm, đỏ, xanh dương
      });

      it('the price-line colours follow the theme (darker green in light, lighter in dark) so label text keeps AA contrast', () => {
        const levels = { overlays: [], panes: [], notes: [], priceLines: [{ label: 'Mục tiêu đo 125', price: 125 }, { label: 'Cắt lỗ 97', price: 97 }] };
        TestBed.inject(ThemeService).set('dark', false);
        setInputs(levels);
        const dark = Array.from(root().querySelectorAll('[data-testid="price-line-swatch"]')).map(s => (s as HTMLElement).style.borderTopColor);
        TestBed.inject(ThemeService).set('light', false);
        setInputs(levels);
        const light = Array.from(root().querySelectorAll('[data-testid="price-line-swatch"]')).map(s => (s as HTMLElement).style.borderTopColor);

        expect(dark).toEqual(['rgb(74, 222, 128)', 'rgb(248, 113, 113)']);
        expect(light).toEqual(['rgb(21, 128, 61)', 'rgb(220, 38, 38)']);
      });
    });

    describe('nhãn giá cuối trên trục', () => {
      it('every indicator line shows its last value as an axis tag', () => {
        setInputs(layers(['RSI']));
        const proto = Object.getPrototypeOf((component as any).charts[0]);
        const lineSpy = spyOn(proto, 'addLineSeries').and.callThrough();

        setInputs({ overlays: [line('EMA')], panes: [{ title: 'RSI', lines: [line('RSI')] }], priceLines: [], notes: [] });

        expect(lineSpy).toHaveBeenCalledTimes(2);
        lineSpy.calls.allArgs().forEach(args => expect((args[0] as any).lastValueVisible).toBeTrue());
      });
    });

    it('a missing value shows a dash, not 0', () => {
      setInputs({ overlays: [], panes: [{ title: 'Tầng', lines: [line('Thưa', i => (i === 0 ? 5 : null))] }], priceLines: [], notes: [] });

      expect(itemOf('Thưa').querySelector('[data-testid="legend-value"]')!.textContent).toBe('—');
    });

    it('price line legend lists label, color and price', () => {
      setInputs({
        overlays: [], panes: [], notes: [],
        priceLines: [{ label: 'Hỗ trợ 1', price: 85200 }, { label: 'Kháng cự 1', price: 99500 }, { label: 'Fib 50%', price: 90000 }],
      });

      const legend = root().querySelector('[data-testid="price-line-legend"]') as HTMLElement;
      const entries = Array.from(legend.querySelectorAll('[data-testid="price-line-item"]')) as HTMLElement[];
      expect(entries.length).toBe(3);
      expect(entries[0].textContent).toContain('Hỗ trợ 1');
      expect(entries[0].textContent).toContain('85.200');
      const swatchColor = (e: HTMLElement) => (e.querySelector('[data-testid="price-line-swatch"]') as HTMLElement).style.borderTopColor;
      expect(swatchColor(entries[0])).toBe(norm('#15803d'));   // hỗ trợ: xanh lá (sắc đậm của giao diện sáng, đạt 4,5:1 trên nền trắng)
      expect(swatchColor(entries[1])).toBe(norm('#dc2626'));   // kháng cự: đỏ
      expect(swatchColor(entries[2])).toBe(norm('#b45309'));   // Fibonacci: cam đậm (đạt 4,5:1 trên nền trắng)
    });

    it('no price line legend when there are no price lines', () => {
      setInputs(layers(['RSI']));

      expect(root().querySelector('[data-testid="price-line-legend"]')).toBeNull();
    });

    it('candle legend present on price pane', () => {
      setInputs(layers(['RSI']));

      const candle = root().querySelector('[data-testid="candle-legend"]')!;
      expect(candle.textContent).toContain('xanh = phiên tăng');
      expect(candle.textContent).toContain('đỏ = phiên giảm');
      expect(root().querySelectorAll('[data-testid="candle-legend"]').length).toBe(1);
    });

    it('guides carry labels (rsi 70 Quá mua / 30 Quá bán)', () => {
      setInputs({
        overlays: [], priceLines: [], notes: [],
        panes: [{ title: 'RSI', lines: [line('RSI14')], guides: [{ value: 70, label: 'Quá mua' }, { value: 30, label: 'Quá bán' }] }],
      });

      const guideText = Array.from(root().querySelectorAll('[data-testid="legend-guide"]')).map(e => e.textContent);
      expect(guideText).toEqual(['70 Quá mua', '30 Quá bán']);
    });

    it('legend uses textContent only (no innerHTML)', () => {
      const evil: LineLayer = { label: '<b id="x">đậm</b>', values: series(i => i), color: '#2563eb' };
      setInputs({ overlays: [evil], panes: [], priceLines: [{ label: '<i>nghiêng</i>', price: 1 }], notes: [] });

      expect(root().textContent).toContain('<b id="x">đậm</b>');
      expect(root().querySelector('b')).toBeNull();
      expect(root().querySelector('i[id], #x')).toBeNull();
      expect(root().textContent).toContain('<i>nghiêng</i>');
    });

    it('the whole chart block stays within the height budget when the legend wraps on a narrow container', () => {
      const guides = [{ value: 70, label: 'Quá mua' }, { value: 30, label: 'Quá bán' }];
      (fixture.nativeElement as HTMLElement).style.display = 'block';
      (fixture.nativeElement as HTMLElement).style.width = '480px';
      setInputs({
        overlays: [line('EMA20 (TB 20 phiên)'), line('EMA50 (TB 50 phiên)')],
        panes: [
          { title: 'RSI (sức mạnh tương đối)', lines: [line('RSI14')], guides },
          { title: 'MACD (động lượng)', lines: [line('Histogram'), line('MACD'), line('Signal (đường tín hiệu)')] },
          { title: 'Stochastic (dao động)', lines: [line('%K (nhanh)'), line('%D (chậm)')], guides },
        ],
        priceLines: [], notes: [],
      });

      expect(root().offsetHeight).toBeLessThanOrEqual(500);
      // và không có mục nào bị cắt mất: mọi mục chú giải vẫn nằm trong DOM
      expect(items().length).toBeGreaterThanOrEqual(2 + 1 + 3 + 2);
    });
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
    /** Chiều cao thật của cả khối (canvas + tiêu đề + chú giải), không phải tổng các hằng số. */
    const total = () => ((fixture.nativeElement as HTMLElement).querySelector('[data-testid="indicator-chart-root"]') as HTMLElement).offsetHeight;

    [0, 1, 2, 3].forEach(n => {
      it(`${n} panes: canvases + titles stay within 500px, price chart is the tallest, panes >= 60px`, () => {
        setInputs(layers(['RSI', 'MACD', 'Stochastic'].slice(0, n)));

        const h = heights();
        expect(h.length).toBe(n + 1);
        expect(total()).toBeLessThanOrEqual(500);
        h.slice(1).forEach(p => {
          expect(p).toBeGreaterThanOrEqual(60);
          expect(p).toBeLessThanOrEqual(h[0]);
        });
      });
    });

    it('a single pane does not balloon: it is capped at 120px and the price chart takes the rest', () => {
      setInputs(layers(['RSI']));

      const h = heights();
      expect(h[1]).toBe(120);
      expect(h[0]).toBeGreaterThan(300);
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

  describe('markers', () => {
    const m = (time: string, text = 'T', tooltip?: string[]) =>
      ({ time, position: 'belowBar' as const, color: '#22c55e', shape: 'arrowUp' as const, text, tooltip });

    const withMarkers = (markers: any[]) => {
      fixture.componentRef.setInput('dates', dates);
      fixture.componentRef.setInput('candles', candles);
      fixture.componentRef.setInput('layers', layers([]));
      fixture.componentRef.setInput('markers', markers);
      fixture.detectChanges();
    };

    it('renders markers on the price series', () => {
      withMarkers([m('2026-01-05'), m('2026-01-10', 'J')]);

      expect(component.markerCount).toBe(2);
    });

    it('a marker on a non-trading day snaps to the previous session and its tooltip lines sit on that session', () => {
      const days = ['2026-11-19T00:00:00Z', '2026-11-20T00:00:00Z', '2026-11-23T00:00:00Z'];
      const c = { open: [1, 2, 3], high: [2, 3, 4], low: [0, 1, 2], close: [1, 2, 3] };
      fixture.componentRef.setInput('dates', days);
      fixture.componentRef.setInput('candles', c);
      fixture.componentRef.setInput('layers', layers([]));
      fixture.componentRef.setInput('markers', [m('2026-11-21', 'T', ['MUA 100 × 63.000đ'])]);
      fixture.detectChanges();

      expect(component.markerCount).toBe(1);
      expect(component.markerLinesAt('2026-11-20')).toEqual(['MUA 100 × 63.000đ']);
      expect(component.markerLinesAt('2026-11-21')).toEqual([]);
    });

    it('a marker before the first session is not drawn', () => {
      withMarkers([m('2025-12-01')]);

      expect(component.markerCount).toBe(0);
    });

    it('changing the markers redraws them without changing the number of charts', () => {
      withMarkers([m('2026-01-05')]);
      expect(component.markerCount).toBe(1);

      fixture.componentRef.setInput('markers', []);
      fixture.detectChanges();

      expect(component.markerCount).toBe(0);
      expect(component.chartCount).toBe(1);
    });

    it('a marker with a tone takes its colour from the theme (text on the canvas must keep >= 4.5:1), a plain marker keeps its own colour', () => {
      const applied: any[][] = [];
      const capture = () => {
        const proto = Object.getPrototypeOf((component as any).charts[0]);
        const original = proto.addCandlestickSeries;
        spyOn(proto, 'addCandlestickSeries').and.callFake(function (this: any, ...args: any[]) {
          const series = original.apply(this, args);
          const set = series.setMarkers.bind(series);
          series.setMarkers = (ms: any[]) => { applied.push(ms); return set(ms); };
          return series;
        });
      };
      const marks = [
        { ...m('2026-01-05', 'Bẫy tăng'), position: 'aboveBar' as const, color: '#dc2626', tone: 'trapBull' },
        { ...m('2026-01-10', 'Bẫy giảm'), color: '#15803d', tone: 'trapBear' },
        { ...m('2026-01-12', 'T'), color: '#22c55e' },
      ];
      withMarkers([]);
      capture();

      TestBed.inject(ThemeService).set('dark', false);
      withMarkers(marks);
      const dark = applied[applied.length - 1].map(x => x.color);
      TestBed.inject(ThemeService).set('light', false);
      withMarkers([...marks]);
      const light = applied[applied.length - 1].map(x => x.color);

      expect(dark).toEqual(['#f87171', '#4ade80', '#22c55e']);
      expect(light).toEqual(['#dc2626', '#15803d', '#22c55e']);
    });

    it('no markers input keeps the chart working as before', () => {
      setInputs(layers(['RSI']));

      expect(component.markerCount).toBe(0);
      expect(component.chartCount).toBe(2);
    });

    it('a histogram pane with per-bar colors renders', () => {
      const colors: (string | null)[] = Array.from({ length: N }, (_, i) => (i % 2 === 0 ? '#22c55e' : '#ef4444'));
      const l: GroupLayers = {
        overlays: [], priceLines: [], notes: [],
        panes: [{ title: 'Cảm xúc', lines: [{ label: 'Tự tin', values: series(i => (i % 3 === 0 ? 5 : null)), kind: 'histogram', colors }] }]
      };

      expect(() => setInputs(l)).not.toThrow();
      expect(component.chartCount).toBe(2);
    });
  });

  describe('chế độ tối', () => {
    const theme = () => TestBed.inject(ThemeService);
    const chartOptions = () => (component as any).charts.map((c: any) => c.options());
    const swatchOf = (label: string): HTMLElement =>
      Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="legend-item"]'))
        .find(el => el.textContent?.includes(label))!.querySelector('[data-testid="legend-swatch"]') as HTMLElement;

    it('indicator_chart_applies_dark_options_and_reapplies_on_toggle', () => {
      theme().set('dark', false);
      setInputs(layers(['RSI', 'MACD']));

      chartOptions().forEach((o: any) => {
        expect(o.layout.background.color).toBe('#111827');
        expect(o.layout.textColor).toBe('#cbd5e1');
        expect(o.grid.horzLines.color).toBe('#1f2937');
        expect(o.rightPriceScale.borderColor).toBe('#334155');
      });

      theme().toggle();   // sang sáng
      fixture.detectChanges();
      expect(component.chartCount).toBe(3);
      chartOptions().forEach((o: any) => {
        expect(o.layout.background.color).toBe('#ffffff');
        expect(o.layout.textColor).toBe('#374151');
      });

      theme().toggle();   // về tối
      fixture.detectChanges();
      chartOptions().forEach((o: any) => expect(o.layout.background.color).toBe('#111827'));
      expect(component.chartCount).toBe(3);   // dựng lại không rò chart cũ
    });

    it('the crosshair follows the theme too', () => {
      theme().set('dark', false);
      setInputs(layers([]));

      const o = chartOptions()[0];
      expect(o.crosshair.vertLine.color).toBe('#64748b');
      expect(o.crosshair.vertLine.labelBackgroundColor).toBe('#334155');
    });

    it('a toggle rebuilds the charts exactly once', () => {
      theme().set('dark', false);
      setInputs(layers(['RSI']));
      const rebuildSpy = spyOn<any>(component, 'rebuild').and.callThrough();

      theme().toggle();
      fixture.detectChanges();

      expect(rebuildSpy).toHaveBeenCalledTimes(1);
    });

    it('lightens a line that is too dark for the dark background, only in dark mode', () => {
      const almostBackground: LineLayer = { label: 'Đường tối', values: series(i => 10 + i), color: '#1f2937' };

      theme().set('dark', false);
      setInputs(layers([], [], [almostBackground]));
      const dark = swatchOf('Đường tối').style.backgroundColor;
      expect(dark).not.toBe('rgb(31, 41, 55)');

      theme().set('light', false);
      fixture.detectChanges();
      expect(swatchOf('Đường tối').style.backgroundColor).toBe('rgb(31, 41, 55)');
    });

    it('keeps the recognisable line colours that already read well on the dark background', () => {
      theme().set('dark', false);
      setInputs(layers([], [], [{ label: 'Đường xanh', values: series(i => 10 + i), color: '#16a34a' }, { label: 'Đường cam', values: series(i => 10 + i), color: '#f59e0b' }]));

      expect(swatchOf('Đường xanh').style.backgroundColor).toBe('rgb(22, 163, 74)');
      expect(swatchOf('Đường cam').style.backgroundColor).toBe('rgb(245, 158, 11)');
      expect(contrastRatio('#16a34a', '#111827')).toBeGreaterThanOrEqual(3);
    });

    it('the price change text uses the lighter up/down colours in dark mode', () => {
      theme().set('dark', false);
      setInputs(layers([]));

      const change = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="ohlc-change"]') as HTMLElement;
      expect(change.style.color).toBe('rgb(74, 222, 128)');   // #4ade80: nến tăng
    });
  });
});
