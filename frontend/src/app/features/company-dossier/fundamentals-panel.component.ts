import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CompanyFundamentals, MarketDataService } from '../../core/services/market-data.service';

/**
 * Số liệu doanh nghiệp đặt cạnh ô viết hồ sơ: nguyên liệu để trả lời "doanh nghiệp này kiếm tiền
 * bằng gì", KHÔNG phải một phần của điều kiện chặn. Phần nào provider không lấy được thì nói thẳng
 * là không lấy được — render số 0 vào chỗ trống là mời người dùng kết luận sai về doanh nghiệp.
 */
@Component({
  selector: 'app-fundamentals-panel',
  standalone: true,
  imports: [CommonModule],
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
              <div class="border border-gray-200 rounded-lg px-2 py-2">
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
            <h3 class="text-xs font-semibold text-gray-600 mb-1">Cổ đông lớn</h3>
            <ul class="text-xs text-gray-600 mb-4 space-y-0.5" data-testid="shareholders">
              @for (s of data!.company!.majorShareholders; track $index) {
                <li>{{ s.name }} — {{ pct(s.percentage) }}</li>
              }
            </ul>
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
        @if (hasSection('incomeStatements') && isOpen('incomeStatements')) {
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
          <table class="w-full text-xs mb-4" data-testid="peers-table">
            <thead class="text-gray-400">
              <tr><th class="text-left font-normal">Mã</th><th class="text-right font-normal">P/E</th><th class="text-right font-normal">P/B</th><th class="text-right font-normal">± %</th></tr>
            </thead>
            <tbody class="text-gray-700">
              @for (p of data!.peers; track $index) {
                <tr class="border-t border-gray-100">
                  <td class="py-0.5">{{ text(p.symbol) }}</td>
                  <td class="text-right">{{ num(p.pe) }}</td>
                  <td class="text-right">{{ num(p.pb) }}</td>
                  <td class="text-right">{{ num(p.changePercent) }}</td>
                </tr>
              }
            </tbody>
          </table>
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
          <ul class="text-xs text-gray-600 mb-4 space-y-0.5" data-testid="dividends">
            @for (d of data!.dividendEvents; track $index) {
              <li>{{ text(d.exDate) }} — {{ text(d.description) }}</li>
            }
          </ul>
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
          <div class="text-xs text-gray-600" data-testid="business-plan">
            Năm {{ data!.businessPlan!.year }}: doanh thu {{ num(data!.businessPlan!.revenuePlan) }} tỷ ·
            lợi nhuận {{ num(data!.businessPlan!.profitPlan) }} tỷ ·
            cổ tức {{ pct(data!.businessPlan!.dividendPlan) }}
          </div>
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
   * Bốn khối dài xếp dọc làm panel cuộn mãi không hết. Mở sẵn doanh thu — đó là thứ hay đọc nhất khi
   * đang trả lời "doanh nghiệp này kiếm tiền bằng gì"; ba khối kia gập, bung khi cần.
   * Khối KHÔNG lấy được dữ liệu thì không gập: câu "không lấy được dữ liệu" phải luôn nhìn thấy,
   * giấu nó sau một cái mũi tên là mời người đọc tưởng phần đó trống.
   */
  private readonly open: Record<string, boolean> = {
    incomeStatements: true,
    peers: false,
    dividendEvents: false,
    businessPlan: false,
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
      case 'businessPlan': return this.data!.businessPlan != null;
      case 'incomeStatements': return (this.data!.incomeStatements?.length ?? 0) > 0;
      case 'peers': return (this.data!.peers?.length ?? 0) > 0;
      case 'dividendEvents': return (this.data!.dividendEvents?.length ?? 0) > 0;
      default: return false;
    }
  }

  indicatorCards(): { label: string; value: string }[] {
    const i = this.data?.indicators;
    if (!i) return [];
    return [
      { label: 'P/E', value: this.num(i.pe) },
      { label: 'P/B', value: this.num(i.pb) },
      { label: 'ROE', value: this.pct(i.roe) },
      { label: 'ROA', value: this.pct(i.roa) },
      { label: 'EPS', value: this.int(i.eps) },
      { label: 'Vốn hóa (tỷ)', value: this.int(i.marketCap != null ? i.marketCap / 1_000_000_000 : null) },
      { label: 'Beta', value: this.num(i.beta) },
      { label: 'Đáy 52T', value: this.num(i.min52W) },
      { label: 'Đỉnh 52T', value: this.num(i.max52W) },
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
