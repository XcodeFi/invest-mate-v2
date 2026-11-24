import {
  AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  createChart, IChartApi, ISeriesApi, LineStyle, MouseEventParams, Time
} from 'lightweight-charts';
import { GroupLayers, IndicatorChartCandles, LineLayer } from './indicator-chart.types';

const EMPTY_LAYERS: GroupLayers = { overlays: [], panes: [], priceLines: [], notes: [] };
const EMPTY_CANDLES: IndicatorChartCandles = { open: [], high: [], low: [], close: [] };

const PRICE_HEIGHT = 300;

/** Nhãn trục/crosshair: triệu trở lên (OBV, khối lượng) viết gọn kiểu 2,8M; nghìn trở lên không lẻ thập phân; số nhỏ (RSI, ADX...) 1 chữ số lẻ. */
const formatAxisValue = (v: number): string =>
  Math.abs(v) >= 1_000_000
    ? `${(v / 1_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })}M`
    : v.toLocaleString('vi-VN', { maximumFractionDigits: Math.abs(v) >= 1000 ? 0 : 1 });
const PRICE_FORMAT = { type: 'custom' as const, formatter: formatAxisValue, minMove: 0.01 };
const PANE_HEIGHT = 120;

interface ReadoutItem { label: string; values: (number | null)[]; }
interface Readout { el: HTMLElement; items: ReadoutItem[]; }
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

    // ----- Tầng giá -----
    const priceBlock = this.createBlock(host, 'Giá', PRICE_HEIGHT);
    const priceChart = this.createChart(priceBlock.host, PRICE_HEIGHT, paneCount === 0);
    const candleSeries = priceChart.addCandlestickSeries({
      upColor: '#22c55e', downColor: '#ef4444',
      borderUpColor: '#16a34a', borderDownColor: '#dc2626',
      wickUpColor: '#16a34a', wickDownColor: '#dc2626',
      priceFormat: PRICE_FORMAT
    });
    candleSeries.setData(this.sourceIndex.map((src, k) => ({
      time: times[k] as Time,
      open: this.candles.open[src], high: this.candles.high[src],
      low: this.candles.low[src], close: this.candles.close[src]
    })));

    const priceReadout: ReadoutItem[] = [];
    layers.overlays.forEach(line => {
      const series = priceChart.addLineSeries({
        color: line.color ?? '#2563eb', lineWidth: 2,
        priceLineVisible: false, lastValueVisible: false, crosshairMarkerVisible: false,
        priceFormat: PRICE_FORMAT
      });
      series.setData(this.lineData(times, line.values));
      priceReadout.push({ label: line.label, values: line.values });
    });
    layers.priceLines.forEach(pl => {
      candleSeries.createPriceLine({
        price: pl.price, color: this.priceLineColor(pl.label), lineWidth: 1,
        lineStyle: LineStyle.Dashed, axisLabelVisible: true, title: pl.label
      });
    });
    this.anchors.push({ chart: priceChart, series: candleSeries, values: this.candles.close });
    this.readouts.push({ el: priceBlock.readout, items: [{ label: 'Đóng cửa', values: this.candles.close }, ...priceReadout] });

    // ----- Các tầng dưới -----
    layers.panes.forEach((pane, i) => {
      const block = this.createBlock(host, pane.title, PANE_HEIGHT);
      const chart = this.createChart(block.host, PANE_HEIGHT, i === paneCount - 1);
      const items: ReadoutItem[] = [];
      let firstSeries: ISeriesApi<'Line' | 'Histogram'> | null = null;

      pane.lines.forEach(line => {
        const data = this.lineData(times, line.values);
        const series = line.kind === 'histogram'
          ? chart.addHistogramSeries({ color: line.color ?? '#94a3b8', priceLineVisible: false, lastValueVisible: false, priceFormat: PRICE_FORMAT })
          : chart.addLineSeries({
              color: line.color ?? '#2563eb', lineWidth: 2,
              priceLineVisible: false, lastValueVisible: false, crosshairMarkerVisible: false,
              priceFormat: PRICE_FORMAT
            });
        series.setData(data as any);
        firstSeries ??= series;
        items.push({ label: line.label, values: line.values });
      });

      (pane.guides ?? []).forEach(g => {
        firstSeries?.createPriceLine({
          price: g, color: '#cbd5e1', lineWidth: 1, lineStyle: LineStyle.Dashed, axisLabelVisible: false, title: ''
        });
      });
      if (firstSeries) this.anchors.push({ chart, series: firstSeries, values: pane.lines[0].values });
      this.readouts.push({ el: block.readout, items });
    });

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
    });
    this.observer.observe(host);
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

  private createBlock(host: HTMLElement, title: string, height: number): { host: HTMLElement; readout: HTMLElement } {
    const block = document.createElement('div');
    block.className = 'indicator-chart-pane';
    const header = document.createElement('div');
    header.className = 'flex items-baseline justify-between gap-2 px-1 text-xs text-gray-600';
    const titleEl = document.createElement('span');
    titleEl.className = 'font-semibold';
    titleEl.textContent = title;
    const readout = document.createElement('span');
    readout.className = 'indicator-chart-readout truncate';
    header.append(titleEl, readout);
    const container = document.createElement('div');
    container.className = 'indicator-chart-container w-full';
    container.style.height = `${height}px`;
    block.append(header, container);
    host.appendChild(block);
    return { host: container, readout };
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
    this.readouts.forEach(r => {
      r.el.textContent = r.items
        .map(item => {
          const v = item.values[src];
          return v === null || v === undefined ? null : `${item.label}: ${this.formatValue(v)}`;
        })
        .filter((x): x is string => x !== null)
        .join('  ·  ');
    });
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
    this.anchors = [];
    this.sourceIndex = [];
    this.syncing = false;
    this.crosshairSyncing = false;
    const host = this.root?.nativeElement;
    if (host) host.replaceChildren();
  }
}
