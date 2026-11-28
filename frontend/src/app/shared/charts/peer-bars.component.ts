import { Component, Input } from '@angular/core';
import { PeerRow } from '../../core/services/market-data.service';
import { ChartFrameComponent } from './chart-frame.component';
import { ChartTip, ChartTipComponent, tipPosition, tipPositionFromElement } from './chart-tip.component';
import { formatSignedPct } from './chart-format';

interface PeerBar { symbol: string; pe: number; width: number; self: boolean; peer: PeerRow | null }

/**
 * So sánh cùng ngành: thanh ngang P/E, xếp từ cao xuống thấp. Công ty đang xem được tô màu nhấn (xanh), các mã cùng ngành dùng MỘT màu trung tính
 * (không phải màu danh tính). Mã có P/E âm hoặc thiếu không vẽ: liệt kê "n/a" kèm lý do (lỗ hoặc chưa có số), không vẽ cột 0.
 */
@Component({
  selector: 'app-peer-bars',
  standalone: true,
  imports: [ChartFrameComponent, ChartTipComponent],
  template: `
    <app-chart-frame title="So sánh P/E cùng ngành" [hasData]="bars.length > 0 || naList.length > 0" testId="peer-bars">
      <div class="pe-wrap" #wrap>
        @for (b of bars; track b.symbol) {
          <div class="pe-row" data-testid="pe-row" [attr.data-symbol]="b.symbol" [attr.data-self]="b.self" tabindex="0"
            [attr.aria-label]="ariaOf(b)"
            (pointermove)="hover(b, $event, wrap)" (pointerleave)="tip = null" (focus)="focusTip(b, $event, wrap)" (blur)="tip = null">
            <span class="pe-symbol" [class.pe-self]="b.self">{{ b.symbol }}</span>
            <span class="pe-track"><span class="pe-bar" data-testid="pe-bar" [class.pe-bar-self]="b.self" [style.width.%]="b.width"></span></span>
            <span class="pe-value">{{ b.pe.toLocaleString('vi-VN', { maximumFractionDigits: 1, minimumFractionDigits: 1 }) }}</span>
          </div>
        }
        @if (naList.length > 0) {
          <p class="pe-na-line">
            Không vẽ:
            @for (n of naList; track $index) {
              <span class="pe-na" data-testid="pe-na" [attr.data-symbol]="n.symbol">{{ n.symbol }} (n/a)</span>@if (!$last) {, }
            }
            <span class="pe-na-why"> — P/E âm hoặc chưa có số.</span>
          </p>
        }
        <app-chart-tip [tip]="tip"></app-chart-tip>
      </div>
      <table table class="pe-table">
        <thead><tr><th>Mã</th><th>P/E</th><th>P/B</th><th>± %</th></tr></thead>
        <tbody>
          @for (r of tableRows; track $index) {
            <tr data-testid="pe-table-row"><td>{{ r.symbol }}</td><td>{{ r.pe }}</td><td>{{ r.pb }}</td><td>{{ r.change }}</td></tr>
          }
        </tbody>
      </table>
    </app-chart-frame>
  `,
  styles: [`
    :host { display: block; }
    .pe-wrap { position: relative; min-width: 260px; }
    .pe-row { display: flex; align-items: center; gap: 0.5rem; padding: 2px 0; font-size: 0.6875rem; outline: none; }
    .pe-row:hover, .pe-row:focus { background: var(--viz-hover); }
    .pe-symbol { width: 3.5rem; flex: none; color: var(--viz-text); }
    .pe-self { font-weight: 700; }
    .pe-track { flex: 1; height: 14px; display: flex; align-items: center; }
    .pe-bar { display: block; height: 12px; border-radius: 0 4px 4px 0; background: var(--viz-neutral); }
    .pe-bar-self { background: var(--viz-series-1); }
    .pe-value { width: 3rem; flex: none; text-align: right; color: var(--viz-text); font-variant-numeric: tabular-nums; }
    .pe-na-line { font-size: 0.6875rem; color: var(--viz-text-2); margin-top: 0.25rem; }
    .pe-table { width: 100%; font-size: 0.6875rem; color: var(--viz-text); border-collapse: collapse; }
    .pe-table th { text-align: right; font-weight: 400; color: var(--viz-text-2); padding: 2px 4px; }
    .pe-table td { text-align: right; padding: 2px 4px; border-top: 1px solid var(--viz-grid); }
    .pe-table th:first-child, .pe-table td:first-child { text-align: left; }
  `]
})
export class PeerBarsComponent {
  @Input() peers: PeerRow[] = [];
  /** Mã công ty đang xem và P/E của chính nó (từ chỉ số cơ bản); thiếu P/E thì công ty cũng nằm trong danh sách n/a. */
  @Input() symbol = '';
  @Input() selfPe: number | null | undefined = null;

  tip: ChartTip | null = null;

  private get all(): { symbol: string; pe: number | null; peer: PeerRow | null; self: boolean }[] {
    const rows: { symbol: string; pe: number | null; peer: PeerRow | null; self: boolean }[] = (this.peers ?? []).filter(p => p.symbol && p.symbol.trim() && p.symbol.trim().toUpperCase() !== this.symbol.toUpperCase())
      .map(p => ({ symbol: p.symbol!.trim().toUpperCase(), pe: p.pe ?? null, peer: p, self: false }));
    if (this.symbol) rows.push({ symbol: this.symbol.toUpperCase(), pe: this.selfPe ?? null, peer: null, self: true });
    return rows;
  }

  get bars(): PeerBar[] {
    const drawn = this.all.filter(r => r.pe != null && r.pe > 0).sort((a, b) => b.pe! - a.pe!);
    const max = Math.max(...drawn.map(r => r.pe!), 0);
    return drawn.map(r => ({ symbol: r.symbol, pe: r.pe!, self: r.self, peer: r.peer, width: max > 0 ? (r.pe! / max) * 100 : 0 }));
  }

  get naList(): { symbol: string }[] {
    return this.all.filter(r => !(r.pe != null && r.pe > 0)).map(r => ({ symbol: r.symbol }));
  }

  get tableRows(): { symbol: string; pe: string; pb: string; change: string }[] {
    const n = (v: number | null | undefined) => v == null ? '—' : v.toLocaleString('vi-VN', { maximumFractionDigits: 2 });
    return this.all.map(r => ({
      symbol: r.symbol, pe: n(r.pe), pb: r.peer ? n(r.peer.pb) : '—', change: r.peer ? formatSignedPct(r.peer.changePercent) : '—'
    }));
  }

  ariaOf(b: PeerBar): string {
    return `${b.symbol}: P/E ${b.pe.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}${b.self ? ' (công ty này)' : ''}`;
  }

  private tipOf(b: PeerBar, pos: { x: number; y: number }): ChartTip {
    const rows: ChartTip['rows'] = [{ label: 'P/E', value: b.pe.toLocaleString('vi-VN', { maximumFractionDigits: 1 }), key: b.self ? 'var(--viz-series-1)' : 'var(--viz-neutral)' }];
    if (b.peer?.pb != null) rows.push({ label: 'P/B', value: b.peer.pb.toLocaleString('vi-VN', { maximumFractionDigits: 2 }) });
    if (b.peer?.changePercent != null) rows.push({ label: 'thay đổi giá', value: formatSignedPct(b.peer.changePercent) });
    return { ...pos, title: b.self ? `${b.symbol} (công ty này)` : b.symbol, rows };
  }

  hover(b: PeerBar, event: MouseEvent, host: HTMLElement): void {
    this.tip = this.tipOf(b, tipPosition(event, host));
  }

  focusTip(b: PeerBar, event: Event, host: HTMLElement): void {
    this.tip = this.tipOf(b, tipPositionFromElement(event.target as Element, host));
  }
}
