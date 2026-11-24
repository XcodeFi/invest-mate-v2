import {
  AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  createChart, IChartApi, ISeriesApi, LineStyle, MouseEventParams, Time
} from 'lightweight-charts';
import { GroupLayers, IndicatorChartCandles, LineLayer } from './indicator-chart.types';

const UP_COLOR = '#22c55e';
const DOWN_COLOR = '#ef4444';

const EMPTY_LAYERS: GroupLayers = { overlays: [], panes: [], priceLines: [], notes: [] };
const EMPTY_CANDLES: IndicatorChartCandles = { open: [], high: [], low: [], close: [] };

/**
 * Tổng chiều cao component (mọi canvas + dòng tiêu đề/chú giải của từng tầng + chú giải đường ngang) tối đa. Khối dính trên trang gồm thêm thanh chip
 * và đệm (~60px) nên toàn khối ≤ 560px, vừa một màn hình máy tính 800px cao.
 */
const TOTAL_HEIGHT = 500;
const TITLE_HEIGHT = 20;
const MAX_PRICE_ONLY_HEIGHT = 300;
const MIN_PANE_HEIGHT = 60;
const MIN_PRICE_HEIGHT = 120;
const MAX_PANE_HEIGHT = 120;
const PRICE_SHARE = 0.45;

/** Chia chiều cao canvas: các tầng dưới tối đa 120px mỗi tầng, tầng giá lấy phần còn lại nhưng không dưới 45% vùng vẽ. */
export function computeHeights(paneCount: number): { price: number; pane: number } {
  const canvas = TOTAL_HEIGHT - TITLE_HEIGHT * (paneCount + 1);
  if (paneCount === 0) return { price: Math.min(canvas, MAX_PRICE_ONLY_HEIGHT), pane: 0 };
  const price = Math.max(Math.round(canvas * PRICE_SHARE), canvas - paneCount * MAX_PANE_HEIGHT);
  const pane = Math.max(MIN_PANE_HEIGHT, Math.min(MAX_PANE_HEIGHT, Math.floor((canvas - price) / paneCount)));
  return { price, pane };
}

/** Nhãn trục/crosshair: triệu trở lên (OBV, khối lượng) viết gọn kiểu 2,8M; nghìn trở lên không lẻ thập phân; số nhỏ (RSI, ADX...) 1 chữ số lẻ. */
const formatAxisValue = (v: number): string =>
  Math.abs(v) >= 1_000_000
    ? `${(v / 1_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })}M`
    : v.toLocaleString('vi-VN', { maximumFractionDigits: Math.abs(v) >= 1000 ? 0 : 1 });
/** Giá nến (Mở/Cao/Thấp/Đóng): nghìn trở lên không lẻ, nhỏ hơn giữ tối đa 2 chữ số lẻ (46,85). */
const formatPrice = (v: number): string =>
  v.toLocaleString('vi-VN', { maximumFractionDigits: Math.abs(v) >= 1000 ? 0 : 2 });
const PRICE_FORMAT = { type: 'custom' as const, formatter: formatAxisValue, minMove: 0.01 };

/** Một mục chú giải: values để đọc giá trị theo phiên, valueEl là chỗ ghi giá trị (textContent). */
interface ReadoutItem { values: (number | null)[]; valueEl: HTMLElement; }
/** Hàng Mở/Cao/Thấp/Đóng của tầng giá: phần tử chứa bốn số và phần tử thay đổi (màu theo tăng/giảm). */
interface OhlcRow { numbersEl: HTMLElement; changeEl: HTMLElement; }
interface Readout { items: ReadoutItem[]; }
/** Canvas của một tầng, để co lại khi chú giải xuống dòng làm tổng cao quá ngân sách. */
interface PaneCanvas { chart: IChartApi; container: HTMLElement; min: number; height: number; isPrice: boolean; }
/** Chuỗi "neo" của một chart: dùng giá trị của nó để đặt crosshair khi trỏ chuột ở chart khác. */
interface Anchor { chart: IChartApi; series: ISeriesApi<any>; values: (number | null)[]; }

/**
 * Biểu đồ nến + các tầng chỉ báo xếp dọc. lightweight-charts v4 chưa có pane gốc nên mỗi tầng là một
 * instance chart riêng; trục thời gian được đồng bộ (kéo/zoom một tầng thì các tầng khác theo) và
 * mọi chuỗi đều có đủ điểm thời gian (điểm null là điểm trống, không phải 0) nên chỉ số bar khớp nhau.
 * Crosshair liên kết: trỏ chuột ở tầng nào thì các tầng còn lại đặt crosshair tại cùng phiên (theo giá trị
 * chuỗi chính của tầng đó). Ngoài ra tiêu đề mỗi tầng có dòng đọc giá trị của phiên đang trỏ.
 */
@Component({
  selector: 'app-indicator-chart',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-1">
      <div #root data-testid="indicator-chart-root" class="w-full"></div>
      <ul *ngIf="layers.notes.length > 0" data-testid="indicator-chart-notes"
          class="text-xs text-gray-500 space-y-0.5">
        <li *ngFor="let note of layers.notes">{{ note }}</li>
      </ul>
    </div>
  `
})
export class IndicatorChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  /** Ngày từng phiên (ISO, lấy 10 ký tự đầu YYYY-MM-DD), tăng dần. */
  @Input() dates: string[] = [];
  @Input() candles: IndicatorChartCandles = EMPTY_CANDLES;
  @Input() layers: GroupLayers = EMPTY_LAYERS;

  @ViewChild('root', { static: true }) root!: ElementRef<HTMLDivElement>;

  private charts: IChartApi[] = [];
  private readouts: Readout[] = [];
  private canvases: PaneCanvas[] = [];
  private ohlc: OhlcRow | null = null;
  private observer: ResizeObserver | null = null;
  private viewReady = false;
  private syncing = false;
  private crosshairSyncing = false;
  private anchors: Anchor[] = [];
  /** Chỉ số nguồn (trong dates/values) của từng điểm trên trục thời gian của biểu đồ. */
  private sourceIndex: number[] = [];
  /** Khoá nội dung trục thời gian của lần dựng trước + khoảng nhìn, để giữ zoom khi chỉ đổi lớp chỉ báo. */
  private timesKey = '';
  private savedRange: { from: number; to: number } | null = null;
  private restoreFrame: number | null = null;

  /** Số instance chart đang sống (dùng cho test dọn dẹp). */
  get chartCount(): number {
    return this.charts.length;
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.rebuild();
  }

  ngOnChanges(): void {
    if (this.viewReady) this.rebuild();
  }

  ngOnDestroy(): void {
    this.teardown();
  }

  private rebuild(): void {
    // Lưu khoảng nhìn của chart cuối (chỉ chart này hiện trục thời gian nên có bề rộng trục > 0) trước khi dựng lại
    this.savedRange = this.charts.length > 0 ? this.charts[this.charts.length - 1].timeScale().getVisibleLogicalRange() : null;
    const previousKey = this.timesKey;
    this.teardown();
    const closes = this.candles?.close ?? [];
    if (!this.dates?.length || closes.length === 0) return;

    // Trục thời gian: chỉ nhận ngày tăng dần thật sự (lightweight-charts từ chối trùng/lùi).
    const times: string[] = [];
    this.sourceIndex = [];
    let last = '';
    this.dates.forEach((d, i) => {
      const t = (d ?? '').substring(0, 10);
      if (t > last) {
        times.push(t);
        this.sourceIndex.push(i);
        last = t;
      }
    });
    if (times.length === 0) return;
    this.timesKey = times.join('|');
    const keepRange = this.savedRange !== null && this.timesKey === previousKey;

    const host = this.root.nativeElement;
    const layers = this.layers ?? EMPTY_LAYERS;
    const paneCount = layers.panes.length;
    const { price: priceHeight, pane: paneHeight } = computeHeights(paneCount);

    // ----- Tầng giá -----
    const priceBlock = this.createBlock(host, 'Giá', priceHeight);
    const priceChart = this.createChart(priceBlock.host, priceHeight, paneCount === 0);
    this.canvases.push({ chart: priceChart, container: priceBlock.host, min: MIN_PRICE_HEIGHT, height: priceHeight, isPrice: true });
    const candleSeries = priceChart.addCandlestickSeries({
      upColor: UP_COLOR, downColor: DOWN_COLOR,
      borderUpColor: '#16a34a', borderDownColor: '#dc2626',
      wickUpColor: '#16a34a', wickDownColor: '#dc2626',
      priceFormat: PRICE_FORMAT
    });
    candleSeries.setData(this.sourceIndex.map((src, k) => ({
      time: times[k] as Time,
      open: this.candles.open[src], high: this.candles.high[src],
      low: this.candles.low[src], close: this.candles.close[src]
    })));

    this.ohlc = this.addOhlcRow(priceBlock.legend);
    this.addCandleLegend(priceBlock.legend);
    const priceReadout: ReadoutItem[] = [];
    layers.overlays.forEach(line => {
      const series = priceChart.addLineSeries({
        color: line.color ?? '#2563eb', lineWidth: 2,
        priceLineVisible: false, lastValueVisible: true, crosshairMarkerVisible: false,
        priceFormat: PRICE_FORMAT
      });
      series.setData(this.lineData(times, line.values));
      priceReadout.push(this.addLegendItem(priceBlock.legend, line.label, line.color ?? '#2563eb', 'bar', line.values));
    });
    layers.priceLines.forEach(pl => {
      candleSeries.createPriceLine({
        price: pl.price, color: this.priceLineColor(pl.label), lineWidth: 1,
        lineStyle: LineStyle.Dashed, axisLabelVisible: true, title: pl.label
      });
    });
    this.anchors.push({ chart: priceChart, series: candleSeries, values: this.candles.close });
    this.readouts.push({ items: priceReadout });

    // ----- Các tầng dưới -----
    layers.panes.forEach((pane, i) => {
      const block = this.createBlock(host, pane.title, paneHeight);
      const chart = this.createChart(block.host, paneHeight, i === paneCount - 1);
      this.canvases.push({ chart, container: block.host, min: MIN_PANE_HEIGHT, height: paneHeight, isPrice: false });
      const items: ReadoutItem[] = [];
      let firstSeries: ISeriesApi<'Line' | 'Histogram'> | null = null;

      pane.lines.forEach(line => {
        const data = this.lineData(times, line.values);
        const series = line.kind === 'histogram'
          ? chart.addHistogramSeries({ color: line.color ?? '#94a3b8', priceLineVisible: false, lastValueVisible: true, priceFormat: PRICE_FORMAT })
          : chart.addLineSeries({
              color: line.color ?? '#2563eb', lineWidth: 2,
              priceLineVisible: false, lastValueVisible: true, crosshairMarkerVisible: false,
              priceFormat: PRICE_FORMAT
            });
        series.setData(data as any);
        firstSeries ??= series;
        items.push(this.addLegendItem(block.legend, line.label, line.color ?? (line.kind === 'histogram' ? '#94a3b8' : '#2563eb'),
          line.kind === 'histogram' ? 'square' : 'bar', line.values));
      });

      (pane.guides ?? []).forEach(g => {
        firstSeries?.createPriceLine({
          price: g.value, color: '#94a3b8', lineWidth: 1, lineStyle: LineStyle.Dashed, axisLabelVisible: false, title: g.label
        });
        this.addGuideLegend(block.legend, g.value, g.label);
      });
      if (firstSeries) this.anchors.push({ chart, series: firstSeries, values: pane.lines[0].values });
      this.readouts.push({ items });
    });

    this.addPriceLineLegend(host, layers);

    // ----- Đồng bộ trục thời gian + dòng đọc giá trị -----
    this.charts.forEach(chart => {
      chart.timeScale().fitContent();
      chart.timeScale().subscribeVisibleLogicalRangeChange(range => {
        if (!range || this.syncing) return;
        this.syncing = true;
        try {
          this.charts.forEach(other => { if (other !== chart) other.timeScale().setVisibleLogicalRange(range); });
        } finally {
          this.syncing = false;
        }
      });
      chart.subscribeCrosshairMove(param => {
        this.updateReadouts(param);
        this.syncCrosshair(chart, param);
      });
    });
    // Cùng bộ ngày (cùng mã): giữ khoảng nhìn người dùng đã chọn; đổi ngày thì vẫn vừa khít như cũ.
    // Chart mới tạo bỏ qua setVisibleLogicalRange cho tới khung hình đầu tiên (và còn tự fit lại), nên đặt ở khung kế tiếp.
    if (keepRange) {
      const range = this.savedRange!;
      const axisChart = this.charts[this.charts.length - 1];
      this.restoreFrame = requestAnimationFrame(() => {
        this.restoreFrame = null;
        if (this.charts[this.charts.length - 1] === axisChart) axisChart.timeScale().setVisibleLogicalRange(range);
      });
    }
    this.updateReadouts(null);

    // ----- Co giãn theo khung chứa -----
    this.observer = new ResizeObserver(() => {
      const width = host.clientWidth;
      if (width > 0) this.charts.forEach(c => c.applyOptions({ width }));
      this.fitHeights();   // khung hẹp hơn thì chú giải xuống dòng nhiều hơn
    });
    this.observer.observe(host);
    this.fitHeights();
  }

  /**
   * Chú giải xuống dòng làm cao hơn dự tính: co canvas để tổng (canvas + chú giải) không vượt TOTAL_HEIGHT.
   * Co các tầng dưới trước (tới mức tối thiểu), rồi mới tới tầng giá.
   */
  private fitHeights(): void {
    const host = this.root?.nativeElement;
    if (!host || this.canvases.length === 0) return;
    let over = host.offsetHeight - TOTAL_HEIGHT;
    if (over <= 0) return;
    const order = [...this.canvases.filter(c => !c.isPrice).reverse(), ...this.canvases.filter(c => c.isPrice)];
    for (const c of order) {
      if (over <= 0) break;
      const cut = Math.min(over, c.height - c.min);
      if (cut <= 0) continue;
      c.height -= cut;
      c.container.style.height = `${c.height}px`;
      c.chart.applyOptions({ height: c.height });
      over -= cut;
    }
  }

  /** Đặt/xoá crosshair trên các chart còn lại; cờ chống lặp vì setCrosshairPosition cũng phát sự kiện crosshairMove. */
  private syncCrosshair(source: IChartApi, param: MouseEventParams<Time>): void {
    if (this.crosshairSyncing) return;
    this.crosshairSyncing = true;
    try {
      const others = this.anchors.filter(a => a.chart !== source);
      if (param.time === undefined || !param.point || param.logical === undefined) {
        others.forEach(a => a.chart.clearCrosshairPosition());
        return;
      }
      const k = Math.round(param.logical as number);
      const src = this.sourceIndex[k];
      others.forEach(a => {
        const v = src === undefined ? null : a.values[src];
        if (v === null || v === undefined) a.chart.clearCrosshairPosition();
        else a.chart.setCrosshairPosition(v, param.time as Time, a.series);
      });
    } finally {
      this.crosshairSyncing = false;
    }
  }

  private createBlock(host: HTMLElement, title: string, height: number): { host: HTMLElement; legend: HTMLElement } {
    const block = document.createElement('div');
    block.className = 'indicator-chart-pane';
    const header = document.createElement('div');
    header.className = 'flex flex-wrap items-baseline gap-x-3 gap-y-0.5 px-1 text-xs leading-4 text-gray-600';
    header.style.minHeight = `${TITLE_HEIGHT}px`;
    const titleEl = document.createElement('span');
    titleEl.className = 'font-semibold';
    titleEl.textContent = title;
    const legend = document.createElement('div');
    legend.className = 'indicator-chart-legend flex flex-wrap items-center gap-x-3 gap-y-0.5';
    legend.setAttribute('data-testid', 'legend');
    header.append(titleEl, legend);
    const container = document.createElement('div');
    container.className = 'indicator-chart-container w-full';
    container.style.height = `${height}px`;
    block.append(header, container);
    host.appendChild(block);
    return { host: container, legend };
  }

  /** Mục chú giải của một đường: ô màu đúng màu đường (cột thì ô vuông), tên, và giá trị (cập nhật theo phiên đang trỏ). Chỉ textContent. */
  private addLegendItem(legend: HTMLElement, label: string, color: string, shape: 'bar' | 'square', values: (number | null)[]): ReadoutItem {
    const item = document.createElement('span');
    item.className = 'inline-flex items-center gap-1 whitespace-nowrap';
    item.setAttribute('data-testid', 'legend-item');
    const swatch = document.createElement('span');
    swatch.setAttribute('data-testid', 'legend-swatch');
    swatch.dataset['shape'] = shape;
    swatch.className = 'inline-block shrink-0';
    swatch.style.backgroundColor = color;
    swatch.style.width = shape === 'square' ? '8px' : '14px';
    swatch.style.height = shape === 'square' ? '8px' : '3px';
    swatch.style.borderRadius = shape === 'square' ? '1px' : '2px';
    const name = document.createElement('span');
    name.textContent = label;
    const valueEl = document.createElement('span');
    valueEl.className = 'font-semibold';
    valueEl.style.color = color;
    valueEl.setAttribute('data-testid', 'legend-value');
    item.append(swatch, name, valueEl);
    legend.appendChild(item);
    return { values, valueEl };
  }

  /** Hàng Mở/Cao/Thấp/Đóng + thay đổi so với phiên trước của phiên đang trỏ (hoặc phiên cuối). Chỉ textContent. */
  private addOhlcRow(legend: HTMLElement): OhlcRow {
    const row = document.createElement('span');
    row.className = 'inline-flex flex-wrap items-center gap-x-2 whitespace-nowrap';
    row.setAttribute('data-testid', 'ohlc-row');
    const numbersEl = document.createElement('span');
    numbersEl.className = 'font-semibold text-gray-800';
    const changeEl = document.createElement('span');
    changeEl.className = 'font-semibold';
    changeEl.setAttribute('data-testid', 'ohlc-change');
    row.append(numbersEl, changeEl);
    legend.appendChild(row);
    return { numbersEl, changeEl };
  }

  private updateOhlc(src: number): void {
    if (!this.ohlc) return;
    const { open, high, low, close } = this.candles;
    this.ohlc.numbersEl.textContent =
      `Mở ${formatPrice(open[src])}  Cao ${formatPrice(high[src])}  Thấp ${formatPrice(low[src])}  Đóng ${formatPrice(close[src])}`;
    const prev = src > 0 ? close[src - 1] : undefined;
    if (prev === undefined || prev === null || prev === 0) {
      this.ohlc.changeEl.textContent = '';
      return;
    }
    const diff = close[src] - prev;
    const pct = (diff / prev) * 100;
    const sign = diff >= 0 ? '+' : '−';
    this.ohlc.changeEl.textContent =
      `${sign}${formatPrice(Math.abs(diff))} (${sign}${Math.abs(pct).toLocaleString('vi-VN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%)`;
    this.ohlc.changeEl.style.color = diff >= 0 ? '#16a34a' : '#dc2626';
  }

  /** Chú giải nến: ô xanh/đỏ kèm lời giải thích, không có giá trị. */
  private addCandleLegend(legend: HTMLElement): void {
    const item = document.createElement('span');
    item.className = 'inline-flex items-center gap-1 whitespace-nowrap';
    item.setAttribute('data-testid', 'candle-legend');
    [UP_COLOR, DOWN_COLOR].forEach(color => {
      const box = document.createElement('span');
      box.className = 'inline-block shrink-0';
      box.style.cssText = `width:8px;height:8px;border-radius:1px;background-color:${color}`;
      item.appendChild(box);
    });
    const text = document.createElement('span');
    text.textContent = 'Nến: xanh = phiên tăng, đỏ = phiên giảm';
    item.appendChild(text);
    legend.appendChild(item);
  }

  /** Vạch tham chiếu của tầng (ví dụ "70 Quá mua") nằm cùng dòng chú giải để người xem biết vạch mờ đó là gì. */
  private addGuideLegend(legend: HTMLElement, value: number, label: string): void {
    const item = document.createElement('span');
    item.className = 'inline-flex items-center gap-1 whitespace-nowrap text-gray-500';
    const dash = document.createElement('span');
    dash.className = 'inline-block shrink-0';
    dash.style.cssText = 'width:14px;height:0;border-top:2px dashed #94a3b8';
    const text = document.createElement('span');
    text.setAttribute('data-testid', 'legend-guide');
    text.textContent = `${this.formatValue(value)} ${label}`;
    item.append(dash, text);
    legend.appendChild(item);
  }

  /** Chú giải các đường ngang trên tầng giá (hỗ trợ, kháng cự, cắt lỗ, Fibonacci...): vạch đứt nét đúng màu + nhãn + giá. */
  private addPriceLineLegend(host: HTMLElement, layers: GroupLayers): void {
    if (layers.priceLines.length === 0) return;
    const list = document.createElement('div');
    list.className = 'flex flex-wrap items-center gap-x-3 gap-y-0.5 px-1 pt-1 text-xs text-gray-600';
    list.setAttribute('data-testid', 'price-line-legend');
    layers.priceLines.forEach(pl => {
      const item = document.createElement('span');
      item.className = 'inline-flex items-center gap-1 whitespace-nowrap';
      item.setAttribute('data-testid', 'price-line-item');
      const swatch = document.createElement('span');
      swatch.setAttribute('data-testid', 'price-line-swatch');
      swatch.className = 'inline-block shrink-0';
      swatch.style.cssText = `width:14px;height:0;border-top:2px dashed ${this.priceLineColor(pl.label)}`;
      const text = document.createElement('span');
      text.textContent = `${pl.label} · ${this.formatValue(pl.price)}`;
      item.append(swatch, text);
      list.appendChild(item);
    });
    host.appendChild(list);
  }

  private createChart(container: HTMLElement, height: number, showTime: boolean): IChartApi {
    const chart = createChart(container, {
      layout: { background: { color: '#ffffff' }, textColor: '#374151', fontFamily: 'system-ui, sans-serif' },
      grid: { vertLines: { color: '#f3f4f6' }, horzLines: { color: '#f3f4f6' } },
      crosshair: { mode: 0 },
      // Cùng bề rộng trục giá ở mọi tầng, để vùng vẽ (và các bar) thẳng cột với nhau
      rightPriceScale: { borderColor: '#e5e7eb', minimumWidth: 72 },
      timeScale: { borderColor: '#e5e7eb', timeVisible: false, visible: showTime },
      width: container.clientWidth || this.root.nativeElement.clientWidth || 600,
      height
    });
    this.charts.push(chart);
    return chart;
  }

  /** Điểm null thành điểm trống (có time, không có value) để mọi chuỗi cùng số bar với tầng giá. */
  private lineData(times: string[], values: (number | null)[]): any[] {
    return this.sourceIndex.map((src, k) => {
      const v = values[src];
      return v === null || v === undefined ? { time: times[k] as Time } : { time: times[k] as Time, value: v };
    });
  }

  private updateReadouts(param: MouseEventParams<Time> | null): void {
    let position = this.sourceIndex.length - 1;
    if (param?.logical !== undefined && param.point) {
      const k = Math.round(param.logical as number);
      if (k >= 0 && k < this.sourceIndex.length) position = k;
    }
    const src = this.sourceIndex[position];
    this.updateOhlc(src);
    this.readouts.forEach(r => r.items.forEach(item => {
      const v = item.values[src];
      item.valueEl.textContent = v === null || v === undefined ? '—' : this.formatValue(v);
    }));
  }

  private formatValue(v: number): string {
    return formatAxisValue(v);
  }

  private priceLineColor(label: string): string {
    if (label.startsWith('Cắt lỗ') || label.startsWith('Kháng cự')) return '#dc2626';
    if (label.startsWith('Chốt lời') || label.startsWith('Hỗ trợ')) return '#16a34a';
    if (label.startsWith('Fib')) return '#f59e0b';
    if (label.endsWith('swing')) return '#64748b';
    return '#2563eb';
  }

  private teardown(): void {
    if (this.restoreFrame !== null) cancelAnimationFrame(this.restoreFrame);
    this.restoreFrame = null;
    this.observer?.disconnect();
    this.observer = null;
    this.charts.forEach(c => c.remove());
    this.charts = [];
    this.readouts = [];
    this.canvases = [];
    this.ohlc = null;
    this.anchors = [];
    this.sourceIndex = [];
    this.syncing = false;
    this.crosshairSyncing = false;
    const host = this.root?.nativeElement;
    if (host) host.replaceChildren();
  }
}
