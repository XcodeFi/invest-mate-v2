import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CompanyFundamentals, FundamentalsTrend, MarketDataService, QuarterTrend, TrendDirection } from '../../core/services/market-data.service';
import { DividendTimelineComponent } from '../../shared/charts/dividend-timeline.component';
import { PeerBarsComponent } from '../../shared/charts/peer-bars.component';
import { PlanBarsComponent } from '../../shared/charts/plan-bars.component';
import { QuarterBarsComponent } from '../../shared/charts/quarter-bars.component';
import { RangeBarComponent } from '../../shared/charts/range-bar.component';
import { ShareBarComponent } from '../../shared/charts/share-bar.component';
import { TrendArrowComponent } from '../../shared/charts/trend-arrow.component';
import { formatCompactVn, formatPlainPct, quarterShortLabel } from '../../shared/charts/chart-format';

interface TrendTile {
  key: string;
  label: string;
  value: string;
  full: string;
  direction: TrendDirection | null;
  pct: number | null;
  note: string | null;
  /** Chú thích đi kèm mũi tên, ví dụ "so với cùng quý năm trước". */
  against: string;
  /** Dòng phụ không có mũi tên (biên lợi nhuận: chênh lệch điểm phần trăm). */
  sub: string;
}

/**
 * Số liệu doanh nghiệp đặt cạnh ô viết hồ sơ: nguyên liệu để trả lời "doanh nghiệp này kiếm tiền
 * bằng gì", KHÔNG phải một phần của điều kiện chặn. Phần nào provider không lấy được thì nói thẳng
 * là không lấy được — render số 0 vào chỗ trống là mời người dùng kết luận sai về doanh nghiệp.
 */
@Component({
  selector: 'app-fundamentals-panel',
  standalone: true,
  imports: [CommonModule, TrendArrowComponent, QuarterBarsComponent, PlanBarsComponent, DividendTimelineComponent, PeerBarsComponent, ShareBarComponent, RangeBarComponent],
  template: `
    <div class="bg-white rounded-lg shadow p-5">
      <div class="flex items-baseline justify-between mb-1">
        <h2 class="text-sm font-semibold text-gray-700">Số liệu doanh nghiệp</h2>
        <span class="text-xs text-gray-400">Nguồn: 24hmoney</span>
      </div>
      <p class="text-xs text-gray-400 mb-4">
        Nguyên liệu để bạn tự viết hồ sơ — không tính vào điều kiện chặn của cổng.
      </p>

      @if (loading) {
        <div class="text-center text-gray-400 py-6 text-sm">Đang tải số liệu...</div>
      } @else if (error) {
        <div class="text-sm text-gray-500">{{ error }}</div>
      } @else {
        <!-- Chỉ số -->
        @if (hasSection('indicators')) {
          <div class="grid grid-cols-3 gap-2 mb-4" data-testid="indicators">
            @for (item of indicatorCards(); track item.label) {
              <div class="border border-gray-200 rounded-lg px-2 py-2" [attr.title]="item.hint" data-testid="indicator-card">
                <div class="text-[10px] uppercase tracking-wide text-gray-400">{{ item.label }}</div>
                <div class="text-base font-semibold text-gray-900 tabular-nums">{{ item.value }}</div>
              </div>
            }
          </div>
          @if (data?.indicators?.auditFirmName) {
            <p class="text-xs text-gray-500 mb-4">
              Kiểm toán: {{ data!.indicators!.auditFirmName }}
              @if (data!.indicators!.auditIsBig4) { <span class="text-emerald-600">(Big4)</span> }
            </p>
          }
        } @else {
          <p class="text-xs text-gray-400 italic mb-4">Chỉ số cơ bản: không lấy được dữ liệu</p>
        }

        <!-- Xu hướng: số lớn có mũi tên so với cùng kỳ (do server tính). Không có kỳ so sánh thì không có mũi tên. -->
        @if (trendTiles().length > 0) {
          <div class="grid grid-cols-2 gap-2 mb-1" data-testid="trend-tiles">
            @for (t of trendTiles(); track t.key) {
              <div class="border border-gray-200 rounded-lg px-2 py-2 min-w-0" data-testid="trend-tile" [attr.data-key]="t.key" [attr.title]="t.full">
                <div class="text-[10px] uppercase tracking-wide text-gray-400">{{ t.label }}</div>
                <div class="text-base font-semibold text-gray-900">{{ t.value }}</div>
                @if (t.direction) {
                  <div class="text-[11px]"><app-trend-arrow [direction]="t.direction" [pct]="t.pct" [note]="t.note" [label]="t.against"></app-trend-arrow></div>
                }
                @if (t.sub) { <div class="text-[11px] text-gray-500" data-testid="trend-sub">{{ t.sub }}</div> }
              </div>
            }
          </div>
          <p class="text-[11px] text-gray-400 mb-4" data-testid="trend-note">{{ data!.trend!.note }}</p>
        }

        @if (range(); as r) {
          <app-range-bar [low]="r.low" [high]="r.high" [price]="r.price" [positionPct]="r.positionPct" [outsideRange]="r.outsideRange"></app-range-bar>
        }

        <!-- Thông tin công ty -->
        <h3 class="text-xs font-semibold text-gray-600 mb-1">Thông tin công ty</h3>
        @if (hasSection('company')) {
          <div class="text-xs text-gray-600 space-y-0.5 mb-4" data-testid="company">
            <!-- Tên có thể null dù khối công ty vẫn có ngành và số cổ phiếu: HPG thật đang như vậy.
                 Thiếu tên thì bỏ hẳn dòng, không để lại một dấu gạch trơ trọi. -->
            @if (data!.company!.companyName) {
              <div>{{ data!.company!.companyName }}</div>
            }
            <div>Sàn: {{ text(data!.company!.exchange) }} · Ngành: {{ text(data!.company!.industry) }}</div>
            <div>
              CP lưu hành: {{ int(data!.company!.outstandingShares) }}
              @if (data!.company!.freeFloatRate != null) { · Free float: {{ pct(data!.company!.freeFloatRate) }} }
            </div>
          </div>

          @if ((data!.company!.majorShareholders?.length ?? 0) > 0) {
            <app-share-bar [holders]="data!.company!.majorShareholders" data-testid="shareholders"></app-share-bar>
          }

          @if ((data!.company!.leaders?.length ?? 0) > 0) {
            <h3 class="text-xs font-semibold text-gray-600 mb-1">Ban lãnh đạo</h3>
            <ul class="text-xs text-gray-600 mb-4 space-y-0.5" data-testid="leaders">
              @for (l of data!.company!.leaders; track $index) {
                <li>{{ l.name }} — {{ text(l.position) }}</li>
              }
            </ul>
          }
        } @else {
          <p class="text-xs text-gray-400 italic mb-4">không lấy được dữ liệu</p>
        }

        <!-- Doanh thu / lợi nhuận theo quý -->
        @if (!hasSection('incomeStatements')) {
          <h3 class="text-xs font-semibold text-gray-600 mb-1">Doanh thu &amp; lợi nhuận theo quý (tỷ VND)</h3>
          <p class="text-xs text-gray-400 italic mb-4">không lấy được dữ liệu</p>
        } @else {
          <button type="button" (click)="toggle('incomeStatements')" [attr.aria-expanded]="isOpen('incomeStatements')"
            class="w-full flex items-center justify-between text-xs font-semibold text-gray-600 mb-1 hover:text-gray-900">
            <span>Doanh thu &amp; lợi nhuận theo quý (tỷ VND)</span>
            <span class="text-gray-400">{{ isOpen('incomeStatements') ? '▾' : '▸' }}</span>
          </button>
        }
        @if (hasSection('incomeStatements') && isOpen('incomeStatements') && (data!.trend?.quarters?.length ?? 0) > 0) {
          <app-quarter-bars [quarters]="data!.trend!.quarters" [annual]="data!.trend!.annual" [note]="data!.trend!.note"></app-quarter-bars>
        }
        @if (hasSection('incomeStatements') && isOpen('incomeStatements') && (data!.trend?.quarters?.length ?? 0) === 0) {
          <table class="w-full text-xs mb-4" data-testid="income-table">
            <thead class="text-gray-400">
              <tr><th class="text-left font-normal">Kỳ</th><th class="text-right font-normal">Doanh thu</th><th class="text-right font-normal">LN sau thuế</th></tr>
            </thead>
            <tbody class="text-gray-700">
              @for (r of data!.incomeStatements; track $index) {
                <tr class="border-t border-gray-100">
                  <td class="py-0.5">{{ text(r.period) }}</td>
                  <td class="text-right">{{ num(r.revenue) }}</td>
                  <td class="text-right">{{ num(r.netProfit) }}</td>
                </tr>
              }
            </tbody>
          </table>
        }

        <!-- Cổ phiếu cùng ngành -->
        @if (!hasSection('peers')) {
          <h3 class="text-xs font-semibold text-gray-600 mb-1">Cổ phiếu cùng ngành</h3>
          <p class="text-xs text-gray-400 italic mb-4">không lấy được dữ liệu</p>
        } @else {
          <button type="button" (click)="toggle('peers')" [attr.aria-expanded]="isOpen('peers')"
            class="w-full flex items-center justify-between text-xs font-semibold text-gray-600 mb-1 hover:text-gray-900">
            <span>Cổ phiếu cùng ngành</span>
            <span class="text-gray-400">{{ isOpen('peers') ? '▾' : '▸' }}</span>
          </button>
        }
        @if (hasSection('peers') && isOpen('peers')) {
          <app-peer-bars [peers]="data!.peers" [symbol]="data!.symbol" [selfPe]="data!.indicators?.pe"></app-peer-bars>
        }

        <!-- Cổ tức -->
        @if (!hasSection('dividendEvents')) {
          <h3 class="text-xs font-semibold text-gray-600 mb-1">Cổ tức</h3>
          <p class="text-xs text-gray-400 italic mb-4">không lấy được dữ liệu</p>
        } @else {
          <button type="button" (click)="toggle('dividendEvents')" [attr.aria-expanded]="isOpen('dividendEvents')"
            class="w-full flex items-center justify-between text-xs font-semibold text-gray-600 mb-1 hover:text-gray-900">
            <span>Cổ tức</span>
            <span class="text-gray-400">{{ isOpen('dividendEvents') ? '▾' : '▸' }}</span>
          </button>
        }
        @if (hasSection('dividendEvents') && isOpen('dividendEvents')) {
          <app-dividend-timeline [events]="data!.dividendEvents" data-testid="dividends"></app-dividend-timeline>
        }

        <!-- Kế hoạch kinh doanh -->
        @if (!hasSection('businessPlan')) {
          <h3 class="text-xs font-semibold text-gray-600 mb-1">Kế hoạch kinh doanh</h3>
          <p class="text-xs text-gray-400 italic">không lấy được dữ liệu</p>
        } @else {
          <button type="button" (click)="toggle('businessPlan')" [attr.aria-expanded]="isOpen('businessPlan')"
            class="w-full flex items-center justify-between text-xs font-semibold text-gray-600 mb-1 hover:text-gray-900">
            <span>Kế hoạch kinh doanh</span>
            <span class="text-gray-400">{{ isOpen('businessPlan') ? '▾' : '▸' }}</span>
          </button>
        }
        @if (hasSection('businessPlan') && isOpen('businessPlan')) {
          <app-plan-bars [targets]="data!.businessPlan!.targets" [year]="data!.businessPlan!.year" [quarter]="data!.businessPlan!.quarter" data-testid="business-plan"></app-plan-bars>
        }
      }
    </div>
  `,
})
export class FundamentalsPanelComponent implements OnInit {
  @Input() symbol = '';

  /** Panel vẫn tự gọi API như cũ; cha chỉ cần bản sao để ghép vào nội dung sao chép cho AI. */
  @Output() dataLoaded = new EventEmitter<CompanyFundamentals | null>();

  data: CompanyFundamentals | null = null;
  loading = false;
  error: string | null = null;

  /**
   * Bốn khối xếp dọc, MỞ SẴN CẢ BỐN: nội dung của chúng giờ là biểu đồ (mỗi biểu đồ có nút "Xem bảng" cho số liệu gốc), thứ người dùng đến đây để xem.
   * Nút gập vẫn có để thu gọn khi cần.
   * Khối KHÔNG lấy được dữ liệu thì không gập: câu "không lấy được dữ liệu" phải luôn nhìn thấy,
   * giấu nó sau một cái mũi tên là mời người đọc tưởng phần đó trống.
   */
  private readonly open: Record<string, boolean> = {
    incomeStatements: true,
    peers: true,
    dividendEvents: true,
    businessPlan: true,
  };

  isOpen(section: string): boolean {
    return this.open[section] === true;
  }

  toggle(section: string): void {
    this.open[section] = !this.open[section];
  }

  constructor(private marketData: MarketDataService) {}

  ngOnInit(): void {
    if (!this.symbol) return;
    this.loading = true;
    this.marketData.getFundamentals(this.symbol).subscribe({
      next: (dto) => {
        this.data = dto;
        this.loading = false;
        this.dataLoaded.emit(dto);
      },
      error: () => {
        this.error = 'Không lấy được số liệu doanh nghiệp cho mã này.';
        this.loading = false;
        this.dataLoaded.emit(null);
      },
    });
  }

  /**
   * Chưa có data thì mọi phần đều là chưa lấy được — nếu trả false, template sẽ render bảng rỗng
   * và người đọc hiểu thành doanh nghiệp không có số liệu.
   */
  isUnavailable(section: string): boolean {
    if (!this.data) return true;
    return (this.data.unavailableSections ?? []).includes(section);
  }

  /**
   * Điều kiện render của mỗi khối: vừa không bị đánh dấu thiếu, vừa THẬT SỰ có payload. Chỉ tin
   * `unavailableSections` là đủ để một lần lệch giữa danh sách và body làm template deref null —
   * và một deref null làm hỏng cả vòng change detection, kéo theo các khối khác biến mất im lặng.
   */
  hasSection(section: string): boolean {
    if (this.isUnavailable(section)) return false;
    switch (section) {
      case 'company': return this.data!.company != null;
      case 'indicators': return this.data!.indicators != null;
      // Còn năm mà không còn chỉ tiêu nào thì backend vẫn coi là "có dữ liệu" — ở đây phải
      // đòi có chỉ tiêu, không thì bung ra một cái bảng rỗng.
      case 'businessPlan': return (this.data!.businessPlan?.targets?.length ?? 0) > 0;
      case 'incomeStatements': return (this.data!.incomeStatements?.length ?? 0) > 0;
      case 'peers': return (this.data!.peers?.length ?? 0) > 0;
      case 'dividendEvents': return (this.data!.dividendEvents?.length ?? 0) > 0;
      default: return false;
    }
  }

  /** Khối so sánh kỳ do server tính; vắng mặt khi backend cũ hoặc không tính được. */
  get trend(): FundamentalsTrend | null {
    return this.data?.trend ?? null;
  }

  /** Vùng 52 tuần: ưu tiên khối của server (có giá hiện tại và vị trí); không có thì chỉ đáy/đỉnh từ chỉ số cơ bản, chưa có giá. */
  range(): { low: number | null; high: number | null; price: number | null; positionPct: number | null; outsideRange: boolean } | null {
    const r = this.trend?.range52W;
    if (r && r.low != null && r.high != null) {
      return { low: r.low, high: r.high, price: r.price ?? null, positionPct: r.positionPct ?? null, outsideRange: r.outsideRange };
    }
    const i = this.data?.indicators;
    if (i && i.min52W != null && i.max52W != null) return { low: i.min52W, high: i.max52W, price: null, positionPct: null, outsideRange: false };
    return null;
  }

  /**
   * Ô số lớn có mũi tên: quý mới nhất và 4 quý gần nhất (TTM), mỗi ô so với kỳ tương ứng năm trước. Chỉ HIỂN THỊ số của server: ô nào thiếu giá trị thì
   * bỏ, thiếu kỳ so sánh thì không có mũi tên.
   */
  trendTiles(): TrendTile[] {
    const t = this.trend;
    if (!t) return [];
    const latest: QuarterTrend | undefined = t.quarters[0];
    const tiles: TrendTile[] = [];
    const add = (tile: Omit<TrendTile, 'value'> & { amount: number | null | undefined; percent?: boolean }) => {
      const { amount, percent, ...rest } = tile;
      // Số thiếu: vẫn hiện ô (dấu gạch + "Chưa có dữ liệu", không mũi tên), không lặng lẽ bỏ.
      if (amount == null) { tiles.push({ ...rest, value: '—', direction: null, pct: null, note: null, sub: 'Chưa có dữ liệu' }); return; }
      tiles.push({ ...rest, value: percent ? formatPlainPct(amount) : formatCompactVn(amount) });
    };
    if (latest) {
      const q = quarterShortLabel(latest.period);
      add({ key: 'revenue-q', label: `Doanh thu ${q}`, amount: latest.revenue, full: this.full(latest.revenue), direction: latest.revenueDirection ?? null,
        pct: latest.revenueYoyPct ?? null, note: latest.revenueNote ?? null, against: 'so với cùng quý năm trước', sub: '' });
      add({ key: 'profit-q', label: `Lợi nhuận ${q}`, amount: latest.netProfit, full: this.full(latest.netProfit), direction: latest.netProfitDirection ?? null,
        pct: latest.netProfitYoyPct ?? null, note: latest.netProfitNote ?? null, against: 'so với cùng quý năm trước', sub: '' });
    }
    if (t.ttm) {
      add({ key: 'revenue-ttm', label: 'Doanh thu 4 quý', amount: t.ttm.revenue, full: this.full(t.ttm.revenue), direction: t.ttm.revenueDirection ?? null,
        pct: t.ttm.revenueYoyPct ?? null, note: t.ttm.revenueNote ?? null, against: 'so với 4 quý trước', sub: '' });
      add({ key: 'profit-ttm', label: 'Lợi nhuận 4 quý', amount: t.ttm.netProfit, full: this.full(t.ttm.netProfit), direction: t.ttm.netProfitDirection ?? null,
        pct: t.ttm.netProfitYoyPct ?? null, note: t.ttm.netProfitNote ?? null, against: 'so với 4 quý trước', sub: '' });
    }
    if (latest) {
      const pp = latest.netMarginChangePp;
      add({ key: 'margin-q', label: `Biên ròng ${quarterShortLabel(latest.period)}`, amount: latest.netMargin ?? null, percent: true, full: 'Lợi nhuận sau thuế / doanh thu', direction: null, pct: null, note: null, against: '',
        sub: pp == null ? '' : `${pp > 0 ? '+' : pp < 0 ? '−' : ''}${Math.abs(pp).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} điểm % so với cùng quý` });
    }
    return tiles;
  }

  /** Giá trị đầy đủ cho tooltip của ô (tỷ đồng, đủ chữ số). */
  private full(v: number | null | undefined): string {
    return v == null ? '' : `${v.toLocaleString('vi-VN', { maximumFractionDigits: 2 })} tỷ đồng`;
  }

  indicatorCards(): { label: string; value: string; hint: string }[] {
    const i = this.data?.indicators;
    if (!i) return [];
    return [
      { label: 'P/E', value: this.num(i.pe), hint: 'P/E: giá cổ phiếu chia cho lợi nhuận mỗi cổ phiếu. Đọc là "mất bao nhiêu năm lợi nhuận hiện tại để hoàn vốn". Càng thấp càng rẻ so với lợi nhuận, nhưng lợi nhuận giảm cũng làm P/E thấp giả.' },
      { label: 'P/B', value: this.num(i.pb), hint: 'P/B: giá chia cho giá trị sổ sách mỗi cổ phiếu. Dưới 1 là giá thấp hơn giá trị sổ sách.' },
      { label: 'ROE', value: this.pct(i.roe), hint: 'ROE: lợi nhuận sau thuế trên vốn chủ sở hữu, đo mỗi đồng vốn của cổ đông đẻ ra bao nhiêu lợi nhuận. Trên 15% thường được coi là tốt.' },
      { label: 'ROA', value: this.pct(i.roa), hint: 'ROA: lợi nhuận sau thuế trên tổng tài sản, đo hiệu quả dùng toàn bộ tài sản (kể cả vốn vay).' },
      { label: 'EPS', value: this.int(i.eps), hint: 'EPS: lợi nhuận sau thuế mỗi cổ phiếu (đồng).' },
      { label: 'Vốn hóa (tỷ)', value: this.int(i.marketCap != null ? i.marketCap / 1_000_000_000 : null), hint: 'Vốn hóa: giá cổ phiếu nhân số cổ phiếu lưu hành, tính bằng tỷ đồng.' },
      { label: 'Beta', value: this.num(i.beta), hint: 'Beta: độ nhạy so với thị trường chung. Trên 1 là biến động mạnh hơn thị trường, dưới 1 là êm hơn.' },
      { label: 'Đáy 52T', value: this.num(i.min52W), hint: 'Giá thấp nhất trong 52 tuần (nghìn đồng).' },
      { label: 'Đỉnh 52T', value: this.num(i.max52W), hint: 'Giá cao nhất trong 52 tuần (nghìn đồng).' },
    ];
  }

  // Null là "không có số", không phải 0 — hiện dấu gạch để không ai đọc thành giá trị.
  num(v: number | null | undefined): string {
    return v == null ? '—' : v.toLocaleString('vi-VN', { maximumFractionDigits: 2 });
  }

  int(v: number | null | undefined): string {
    return v == null ? '—' : v.toLocaleString('vi-VN', { maximumFractionDigits: 0 });
  }

  pct(v: number | null | undefined): string {
    return v == null ? '—' : `${v.toLocaleString('vi-VN', { maximumFractionDigits: 2 })}%`;
  }

  text(v: string | null | undefined): string {
    return v && v.trim() ? v : '—';
  }
}
