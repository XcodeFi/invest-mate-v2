import { Component, Input } from '@angular/core';
import { AnnualTrend, QuarterTrend, TrendDirection } from '../../core/services/market-data.service';
import { ChartFrameComponent } from './chart-frame.component';
import { ChartTip, ChartTipComponent, tipPosition, tipPositionFromElement } from './chart-tip.component';
import { formatCompactVn, formatPlainPct, formatSignedPct, quarterShortLabel } from './chart-format';

type Metric = 'revenue' | 'profit';

interface Group {
  key: string;
  label: string;
  current: number | null;
  prev: number | null;
  pct: number | null;
  dir: TrendDirection | null;
  note: string | null;
  incomplete: boolean;
}

interface Bar { kind: 'current' | 'prev'; value: number; x: number; top: number; bottom: number; negative: boolean; path: string }

interface MetricChart {
  metric: Metric;
  title: string;
  baseline: number;
  ticks: { y: number; label: string }[];
  cells: { group: Group; centerX: number; cellX: number; cellW: number; bars: Bar[]; yoyText: string; yoyClass: string }[];
}

interface MarginPoint { key: string; label: string; x: number; y: number; margin: number; prev: number | null; changePp: number | null }

// Hình học cố định (viewBox): rộng 560, bên trái chừa chỗ cho nhãn trục.
const W = 560;
const PAD_L = 56;
const PAD_R = 8;
const LABEL_BAND = 16;
const PLOT_H = 90;
const X_BAND = 16;
const CHART_H = LABEL_BAND + PLOT_H + X_BAND;
const MAX_BAR_W = 14;
const BAR_GAP = 2;
const DEFAULT_QUARTERS = 8;

/**
 * Doanh thu và lợi nhuận theo quý: hai biểu đồ cột nhỏ xếp dọc trên CÙNG trục quý (không trục kép), mỗi quý là một cặp cột "quý này" và "cùng quý
 * năm trước" (bước nhạt hơn của cùng hue), nhãn ▲/▼ %YoY phía trên mỗi cặp. Lợi nhuận âm vẽ dưới đường gốc. Dưới cùng là biểu đồ đường RIÊNG của biên
 * lợi nhuận ròng. Mọi so sánh do server tính (FundamentalsTrendDto): ở đây chỉ vẽ và định dạng, không tính lại; null là vắng mặt/gạch ngang, không
 * bao giờ là 0. Chọn xem theo Quý hoặc Năm (Năm chỉ khi có năm đủ 4 quý; năm chưa đủ ghi "chưa đủ 4 quý").
 */
@Component({
  selector: 'app-quarter-bars',
  standalone: true,
  imports: [ChartFrameComponent, ChartTipComponent],
  template: `
    <app-chart-frame title="Doanh thu và lợi nhuận" [hasData]="hasData" testId="quarter-bars">
      <div class="qb-controls">
        @if (canShowYears) {
          <span class="qb-seg" role="group" aria-label="Kỳ xem">
            <button type="button" data-testid="qb-mode-quarter" [attr.aria-pressed]="mode === 'quarter'" (click)="setMode('quarter')">Quý</button>
            <button type="button" data-testid="qb-mode-year" [attr.aria-pressed]="mode === 'year'" (click)="setMode('year')">Năm</button>
          </span>
        }
        @if (mode === 'quarter' && canShowAll) {
          <button type="button" class="qb-link" data-testid="qb-show-all" (click)="showAll = !showAll">{{ showAll ? 'Chỉ 8 quý gần nhất' : 'Xem tất cả các quý' }}</button>
        }
      </div>

      <div class="qb-legend" data-testid="qb-legend">
        <span><i class="qb-key qb-key-current"></i> {{ mode === 'year' ? 'Năm này' : 'Quý này' }}</span>
        <span><i class="qb-key qb-key-prev"></i> {{ mode === 'year' ? 'Năm trước' : 'Cùng quý năm trước' }}</span>
      </div>

      <div class="qb-wrap" #wrap>
        @for (chart of charts; track chart.metric) {
          <svg [attr.viewBox]="'0 0 ' + width + ' ' + chartHeight" class="qb-svg" role="group" [attr.aria-label]="chart.title">
            <text x="0" y="10" class="qb-title">{{ chart.title }}</text>
            @for (t of chart.ticks; track t.y) {
              <line [attr.x1]="padL" [attr.x2]="width - padR" [attr.y1]="t.y" [attr.y2]="t.y" class="qb-grid"/>
              <text [attr.x]="padL - 4" [attr.y]="t.y + 3" text-anchor="end" class="qb-tick">{{ t.label }}</text>
            }
            <line data-testid="qb-baseline" [attr.data-metric]="chart.metric" [attr.x1]="padL" [attr.x2]="width - padR"
              [attr.y1]="chart.baseline" [attr.y2]="chart.baseline" class="qb-axis"/>
            @for (cell of chart.cells; track cell.group.key) {
              @for (bar of cell.bars; track bar.kind) {
                <path data-testid="qb-bar" [attr.d]="bar.path" [attr.class]="'qb-bar qb-' + bar.kind"
                  [attr.data-metric]="chart.metric" [attr.data-period]="cell.group.key" [attr.data-kind]="bar.kind"
                  [attr.data-negative]="bar.negative" [attr.data-x]="bar.x" [attr.data-top]="bar.top" [attr.data-bottom]="bar.bottom"/>
              }
              <text data-testid="qb-yoy" [attr.data-metric]="chart.metric" [attr.data-period]="cell.group.key"
                [attr.x]="cell.centerX" y="10" text-anchor="middle" [attr.class]="'qb-yoy ' + cell.yoyClass">{{ cell.yoyText }}</text>
              @if (cell.group.incomplete && chart.metric === 'revenue') {
                <text data-testid="qb-incomplete" [attr.data-period]="cell.group.key" [attr.x]="cell.centerX" [attr.y]="chart.baseline - 6"
                  text-anchor="middle" class="qb-incomplete">chưa đủ 4 quý</text>
              }
              <text [attr.x]="cell.centerX" [attr.y]="labelBottom" text-anchor="middle" class="qb-xlabel">{{ cell.group.label }}</text>
              <rect data-testid="qb-hit" class="qb-hit" tabindex="0" role="img" [attr.aria-label]="ariaFor(chart.metric, cell.group)"
                [attr.x]="cell.cellX" y="0" [attr.width]="cell.cellW" [attr.height]="chartHeight"
                (pointermove)="hover(chart.metric, cell.group.key, $event, wrap)" (pointerleave)="tip = null"
                (focus)="focusTip(chart.metric, cell.group.key, $event, wrap)" (blur)="tip = null"/>
            }
          </svg>
        }

        @if (mode === 'quarter' && margins.length > 0) {
          <svg [attr.viewBox]="'0 0 ' + width + ' ' + chartHeight" class="qb-svg" data-testid="qb-margin" role="group" aria-label="Biên lợi nhuận ròng">
            <text x="0" y="10" class="qb-title">Biên lợi nhuận ròng (% doanh thu)</text>
            @for (t of marginTicks; track t.y) {
              <line [attr.x1]="padL" [attr.x2]="width - padR" [attr.y1]="t.y" [attr.y2]="t.y" class="qb-grid"/>
              <text [attr.x]="padL - 4" [attr.y]="t.y + 3" text-anchor="end" class="qb-tick">{{ t.label }}</text>
            }
            @for (run of marginRuns; track $index) {
              @if (run.length > 1) { <polyline data-testid="qb-margin-line" [attr.points]="pointsOf(run)" class="qb-line"/> }
            }
            @for (p of margins; track p.key) {
              <circle data-testid="qb-margin-point" [attr.data-period]="p.key" [attr.cx]="p.x" [attr.cy]="p.y" r="4" class="qb-dot"/>
            }
            @for (g of marginLabels; track g.key) {
              <text [attr.x]="g.x" [attr.y]="labelBottom" text-anchor="middle" class="qb-xlabel">{{ g.label }}</text>
              <rect data-testid="qb-hit" class="qb-hit" tabindex="0" role="img" [attr.aria-label]="ariaFor('margin', undefined, g.key)"
                [attr.x]="g.cellX" y="0" [attr.width]="g.cellW" [attr.height]="chartHeight"
                (pointermove)="hover('margin', g.key, $event, wrap)" (pointerleave)="tip = null"
                (focus)="focusTip('margin', g.key, $event, wrap)" (blur)="tip = null"/>
            }
          </svg>
        }
        <app-chart-tip [tip]="tip"></app-chart-tip>
      </div>

      <p class="qb-note" data-testid="qb-note">{{ note }}</p>

      <table table class="viz-table" data-testid="qb-table-body">
        <thead>
          <tr>
            <th>{{ mode === 'year' ? 'Năm' : 'Kỳ' }}</th>
            <th>Doanh thu</th><th>{{ mode === 'year' ? 'Năm trước' : 'Cùng quý năm trước' }}</th><th>± %</th>
            <th>LN sau thuế</th><th>{{ mode === 'year' ? 'Năm trước' : 'Cùng quý năm trước' }}</th><th>± %</th>
            @if (mode === 'quarter') { <th>Biên ròng</th> }
          </tr>
        </thead>
        <tbody>
          @for (row of tableRows; track row.key) {
            <tr data-testid="qb-row" [attr.data-period]="row.key">
              <td>{{ row.label }}</td>
              <td>{{ row.revenue }}</td><td>{{ row.revenuePrev }}</td><td>{{ row.revenuePct }}</td>
              <td>{{ row.profit }}</td><td>{{ row.profitPrev }}</td><td>{{ row.profitPct }}</td>
              @if (mode === 'quarter') { <td>{{ row.margin }}</td> }
            </tr>
          }
        </tbody>
      </table>
    </app-chart-frame>
  `,
  styles: [`
    :host { display: block; }
    .qb-controls { display: flex; flex-wrap: wrap; align-items: center; gap: 0.75rem; margin-bottom: 0.25rem; font-size: 0.6875rem; }
    .qb-seg { display: inline-flex; border: 1px solid var(--viz-axis); border-radius: 6px; overflow: hidden; }
    .qb-seg button { padding: 1px 8px; color: var(--viz-text-2); }
    .qb-seg button[aria-pressed='true'] { background: var(--viz-hover); color: var(--viz-text); font-weight: 600; }
    .qb-link { color: var(--viz-text-2); text-decoration: underline; }
    .qb-legend { display: flex; flex-wrap: wrap; gap: 0.75rem; font-size: 0.6875rem; color: var(--viz-text-2); margin-bottom: 0.25rem; }
    .qb-key { display: inline-block; width: 10px; height: 10px; border-radius: 2px; margin-right: 3px; vertical-align: -1px; }
    .qb-key-current { background: var(--viz-series-1); }
    .qb-key-prev { background: var(--viz-series-1-prev); }
    .qb-wrap { position: relative; min-width: 560px; }
    .qb-svg { width: 100%; height: auto; display: block; margin-bottom: 0.25rem; overflow: visible; }
    .qb-title { font-size: 11px; fill: var(--viz-text-2); font-weight: 600; }
    .qb-grid { stroke: var(--viz-grid); stroke-width: 1; }
    .qb-axis { stroke: var(--viz-axis); stroke-width: 1; }
    .qb-tick, .qb-xlabel { font-size: 11px; fill: var(--viz-text-2); }
    .qb-bar.qb-current { fill: var(--viz-series-1); }
    .qb-bar.qb-prev { fill: var(--viz-series-1-prev); }
    .qb-yoy { font-size: 11px; font-weight: 600; fill: var(--viz-flat); }
    .qb-yoy.viz-up { fill: var(--viz-up); }
    .qb-yoy.viz-down { fill: var(--viz-down); }
    .qb-yoy.viz-flat { fill: var(--viz-flat); }
    .qb-incomplete { font-size: 11px; fill: var(--viz-text-2); font-style: italic; }
    .qb-line { fill: none; stroke: var(--viz-series-1); stroke-width: 2; stroke-linejoin: round; stroke-linecap: round; }
    .qb-dot { fill: var(--viz-series-1); stroke: var(--viz-surface); stroke-width: 2; }
    .qb-hit { fill: transparent; outline: none; }
    .qb-hit:hover, .qb-hit:focus { fill: var(--viz-hover); }
    .qb-note { font-size: 0.6875rem; color: var(--viz-text-2); margin-top: 0.25rem; }
  `]
})
export class QuarterBarsComponent {
  /** Từ server, MỚI NHẤT TRƯỚC. */
  @Input() quarters: QuarterTrend[] = [];
  @Input() annual: AnnualTrend[] = [];
  /** Ghi chú của server: "Doanh nghiệp báo cáo theo quý; không có số liệu tháng". */
  @Input() note = '';

  mode: 'quarter' | 'year' = 'quarter';
  showAll = false;
  tip: ChartTip | null = null;

  readonly width = W;
  readonly padL = PAD_L;
  readonly padR = PAD_R;
  readonly chartHeight = CHART_H;
  readonly labelBottom = LABEL_BAND + PLOT_H + 12;

  setMode(mode: 'quarter' | 'year'): void {
    this.mode = mode;
    this.tip = null;
  }

  /** Chỉ có chế độ Năm khi ít nhất một năm đủ 4 quý và có số cả năm. */
  get canShowYears(): boolean {
    return this.annual.some(a => a.complete4Quarters && (a.revenue != null || a.netProfit != null));
  }

  get canShowAll(): boolean {
    return this.quarters.length > DEFAULT_QUARTERS;
  }

  // ---------- Mô hình dữ liệu

  /** Các nhóm theo trục thời gian, cũ → mới, cho từng chỉ tiêu. */
  private groups(metric: Metric): Group[] {
    if (this.mode === 'year') {
      return [...this.annual].sort((a, b) => a.year - b.year).map(a => {
        const rev = metric === 'revenue';
        return {
          key: String(a.year), label: String(a.year), incomplete: !a.complete4Quarters,
          current: rev ? a.revenue ?? null : a.netProfit ?? null,
          prev: rev ? a.prevYearRevenue ?? null : a.prevYearNetProfit ?? null,
          pct: (rev ? a.revenueYoyPct : a.netProfitYoyPct) ?? null,
          dir: (rev ? a.revenueDirection : a.netProfitDirection) ?? null,
          note: (rev ? a.revenueNote : a.netProfitNote) ?? null
        };
      });
    }
    const ascending = [...this.quarters].sort((a, b) => a.year * 4 + a.quarter - (b.year * 4 + b.quarter));
    const view = this.showAll ? ascending : ascending.slice(-DEFAULT_QUARTERS);
    return view.map(r => {
      const rev = metric === 'revenue';
      return {
        key: r.period, label: quarterShortLabel(r.period), incomplete: false,
        current: (rev ? r.revenue : r.netProfit) ?? null,
        prev: (rev ? r.revenueSameQuarterLastYear : r.netProfitSameQuarterLastYear) ?? null,
        pct: (rev ? r.revenueYoyPct : r.netProfitYoyPct) ?? null,
        dir: (rev ? r.revenueDirection : r.netProfitDirection) ?? null,
        note: (rev ? r.revenueNote : r.netProfitNote) ?? null
      };
    });
  }

  get hasData(): boolean {
    return (['revenue', 'profit'] as Metric[]).some(m => this.groups(m).some(g => !g.incomplete && (g.current != null || g.prev != null)));
  }

  get charts(): MetricChart[] {
    return (['revenue', 'profit'] as Metric[]).map(m => this.buildChart(m));
  }

  private buildChart(metric: Metric): MetricChart {
    const groups = this.groups(metric);
    const n = Math.max(groups.length, 1);
    const plotW = W - PAD_L - PAD_R;
    const cellW = plotW / n;
    const values = groups.filter(g => !g.incomplete).flatMap(g => [g.current, g.prev]).filter((v): v is number => v != null);
    const dmin = Math.min(0, ...values);
    let dmax = Math.max(0, ...values);
    if (dmax === dmin) dmax = dmin + 1;
    const y = (v: number) => LABEL_BAND + (dmax - v) / (dmax - dmin) * PLOT_H;
    const baseline = y(0);
    const bw = Math.min(MAX_BAR_W, cellW * 0.3);

    const cells = groups.map((group, i) => {
      const cellX = PAD_L + cellW * i;
      const centerX = cellX + cellW / 2;
      const bars: Bar[] = [];
      if (!group.incomplete) {
        if (group.prev != null) bars.push(this.bar('prev', group.prev, centerX - bw - BAR_GAP / 2, bw, y, baseline));
        if (group.current != null) bars.push(this.bar('current', group.current, centerX + BAR_GAP / 2, bw, y, baseline));
      }
      const label = this.yoyLabel(group);
      return { group, centerX, cellX, cellW, bars, yoyText: group.incomplete ? '' : label.text, yoyClass: label.cls };
    });

    const ticks: { y: number; label: string }[] = [];
    if (dmax > 0 && values.length > 0) ticks.push({ y: y(dmax), label: formatCompactVn(dmax) });
    if (dmin < 0) ticks.push({ y: y(dmin), label: formatCompactVn(dmin) });
    return { metric, title: metric === 'revenue' ? 'Doanh thu (tỷ đồng)' : 'Lợi nhuận sau thuế (tỷ đồng)', baseline, ticks, cells };
  }

  private bar(kind: 'current' | 'prev', value: number, x: number, w: number, y: (v: number) => number, baseline: number): Bar {
    const negative = value < 0;
    let top = negative ? baseline : y(value);
    let bottom = negative ? y(value) : baseline;
    if (value !== 0 && bottom - top < 1) { if (negative) bottom = top + 1; else top = bottom - 1; }
    const r = Math.max(0, Math.min(4, w / 2, bottom - top));
    // Đầu dữ liệu bo 4px, đầu chạm đường gốc vuông.
    const path = negative
      ? `M${x},${top} H${x + w} V${bottom - r} Q${x + w},${bottom} ${x + w - r},${bottom} H${x + r} Q${x},${bottom} ${x},${bottom - r} Z`
      : `M${x},${bottom} V${top + r} Q${x},${top} ${x + r},${top} H${x + w - r} Q${x + w},${top} ${x + w},${top + r} V${bottom} Z`;
    return { kind, value, x, top, bottom, negative, path };
  }

  /** Nhãn %YoY phía trên cặp cột. Chỉ có khi server có chiều so sánh; không thì gạch ngang. */
  private yoyLabel(g: Group): { text: string; cls: string } {
    if (g.dir == null) return { text: '—', cls: 'viz-flat' };
    const glyph = g.dir === 'up' ? '▲' : g.dir === 'down' ? '▼' : '▬';
    const cls = g.dir === 'up' ? 'viz-up' : g.dir === 'down' ? 'viz-down' : 'viz-flat';
    // Không có % (lỗ sang lãi...) thì KHÔNG vẽ mũi tên trơ: gạch ngang, chiều và ghi chú nằm trong tooltip.
    return g.pct != null ? { text: `${glyph} ${formatSignedPct(g.pct)}`, cls } : { text: '—', cls: 'viz-flat' };
  }

  // ---------- Biên lợi nhuận ròng (chỉ theo quý)

  private get marginView(): QuarterTrend[] {
    const ascending = [...this.quarters].sort((a, b) => a.year * 4 + a.quarter - (b.year * 4 + b.quarter));
    return this.showAll ? ascending : ascending.slice(-DEFAULT_QUARTERS);
  }

  private get marginDomain(): { min: number; max: number } {
    const vals = this.marginView.map(r => r.netMargin).filter((v): v is number => v != null);
    const min = Math.min(0, ...vals);
    let max = Math.max(0, ...vals);
    if (max === min) max = min + 1;
    return { min, max };
  }

  get marginLabels(): { key: string; label: string; x: number; cellX: number; cellW: number }[] {
    const view = this.marginView;
    const cellW = (W - PAD_L - PAD_R) / Math.max(view.length, 1);
    return view.map((r, i) => ({ key: r.period, label: quarterShortLabel(r.period), x: PAD_L + cellW * (i + 0.5), cellX: PAD_L + cellW * i, cellW }));
  }

  get margins(): MarginPoint[] {
    const { min, max } = this.marginDomain;
    const labels = this.marginLabels;
    return this.marginView.map((r, i) => ({ r, i })).filter(({ r }) => r.netMargin != null).map(({ r, i }) => ({
      key: r.period, label: quarterShortLabel(r.period), x: labels[i].x,
      y: LABEL_BAND + (max - r.netMargin!) / (max - min) * PLOT_H,
      margin: r.netMargin!, prev: r.netMarginSameQuarterLastYear ?? null, changePp: r.netMarginChangePp ?? null
    }));
  }

  /** Các đoạn liên tiếp có số; chỗ thiếu ngắt đường (không nối qua, không về 0). */
  get marginRuns(): MarginPoint[][] {
    const pointByKey = new Map(this.margins.map(p => [p.key, p]));
    const runs: MarginPoint[][] = [];
    let current: MarginPoint[] = [];
    for (const r of this.marginView) {
      const p = pointByKey.get(r.period);
      if (p) current.push(p);
      else if (current.length > 0) { runs.push(current); current = []; }
    }
    if (current.length > 0) runs.push(current);
    return runs;
  }

  get marginTicks(): { y: number; label: string }[] {
    const { min, max } = this.marginDomain;
    const y = (v: number) => LABEL_BAND + (max - v) / (max - min) * PLOT_H;
    const ticks = [{ y: y(max), label: formatPlainPct(max) }, { y: y(0), label: formatPlainPct(0) }];
    if (min < 0) ticks.push({ y: y(min), label: formatPlainPct(min) });
    return ticks;
  }

  pointsOf(run: MarginPoint[]): string {
    return run.map(p => `${p.x},${p.y}`).join(' ');
  }

  // ---------- Tooltip

  hover(metric: Metric | 'margin', key: string, event: MouseEvent, host: HTMLElement): void {
    const pos = tipPosition(event, host);
    this.tip = this.buildTip(metric, key, pos);
  }

  focusTip(metric: Metric | 'margin', key: string, event: Event, host: HTMLElement): void {
    this.tip = this.buildTip(metric, key, tipPositionFromElement(event.target as Element, host));
  }

  ariaFor(metric: Metric | 'margin', group?: Group, key?: string): string {
    const tip = this.buildTip(metric, group?.key ?? key ?? '', { x: 0, y: 0 });
    return tip ? [tip.title, ...tip.rows.map(r => `${r.value} ${r.label}`)].join(', ') : '';
  }

  private buildTip(metric: Metric | 'margin', key: string, pos: { x: number; y: number }): ChartTip | null {
    const cur = { label: this.mode === 'year' ? 'Năm này' : 'Quý này', color: 'var(--viz-series-1)' };
    const prv = { label: this.mode === 'year' ? 'Năm trước' : 'Cùng quý năm trước', color: 'var(--viz-series-1-prev)' };
    if (metric === 'margin') {
      const p = this.margins.find(m => m.key === key);
      if (!p) return null;
      const rows = [{ label: 'Biên lợi nhuận ròng', value: formatPlainPct(p.margin), key: 'var(--viz-series-1)' }];
      if (p.prev != null) rows.push({ label: prv.label, value: formatPlainPct(p.prev), key: 'var(--viz-series-1-prev)' });
      return { ...pos, title: key, rows, note: p.changePp != null ? `Chênh lệch ${p.changePp > 0 ? '+' : p.changePp < 0 ? '−' : ''}${Math.abs(p.changePp).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} điểm %` : undefined };
    }
    const g = this.groups(metric).find(x => x.key === key);
    if (!g) return null;
    const lossy = g.current != null && g.current < 0;
    const rows: ChartTip['rows'] = [];
    if (g.incomplete) return { ...pos, title: key, rows: [], note: 'Chưa đủ 4 quý nên chưa có số cả năm' };
    rows.push({ label: cur.label, value: formatCompactVn(g.current), key: cur.color });
    rows.push({ label: prv.label, value: formatCompactVn(g.prev), key: prv.color });
    if (g.dir != null) rows.push({ label: 'so với cùng kỳ năm trước', value: `${g.dir === 'up' ? '▲' : g.dir === 'down' ? '▼' : '▬'} ${g.pct != null ? formatSignedPct(g.pct) : (g.note ?? '')}`.trim() });
    return { ...pos, title: lossy ? `${key} — Lỗ` : key, rows, note: g.note ?? undefined };
  }

  // ---------- Bảng (bản song sinh của biểu đồ)

  get tableRows(): { key: string; label: string; revenue: string; revenuePrev: string; revenuePct: string; profit: string; profitPrev: string; profitPct: string; margin: string }[] {
    const rev = this.groups('revenue');
    const profit = this.groups('profit');
    const marginBy = new Map(this.marginView.map(r => [r.period, r.netMargin ?? null]));
    const pct = (g: Group) => g.pct != null ? formatSignedPct(g.pct) : (g.note ?? '—');
    return rev.map((g, i) => {
      const p = profit[i];
      return {
        key: g.key, label: g.label,
        revenue: g.incomplete ? '—' : formatCompactVn(g.current), revenuePrev: g.incomplete ? '—' : formatCompactVn(g.prev), revenuePct: g.incomplete ? 'chưa đủ 4 quý' : pct(g),
        profit: p.incomplete ? '—' : formatCompactVn(p.current), profitPrev: p.incomplete ? '—' : formatCompactVn(p.prev), profitPct: p.incomplete ? '—' : pct(p),
        margin: formatPlainPct(marginBy.get(g.key) ?? null)
      };
    });
  }
}
