import { ComponentFixture, TestBed } from '@angular/core/testing';
import { QuarterBarsComponent } from './quarter-bars.component';
import { AnnualTrend, QuarterTrend } from '../../core/services/market-data.service';

const NOTE = 'Doanh nghiệp báo cáo theo quý; không có số liệu tháng';

const q = (year: number, quarter: number, over: Partial<QuarterTrend> = {}): QuarterTrend => ({
  period: `Q${quarter}/${year}`, year, quarter, revenue: 100, netProfit: 10, netMargin: 10, ...over,
});

/** Tám quý mới nhất trước, như server trả: bốn quý mới có cùng quý năm trước, bốn quý cũ thì không. */
const eight = (): QuarterTrend[] => [
  q(2026, 2, { revenue: 110, revenueSameQuarterLastYear: 100, revenueYoyPct: 10, revenueDirection: 'up', netProfit: 12, netProfitSameQuarterLastYear: 10, netProfitYoyPct: 20, netProfitDirection: 'up', netMargin: 10.9, netMarginSameQuarterLastYear: 10, netMarginChangePp: 0.9 }),
  q(2026, 1, { revenue: 90, revenueSameQuarterLastYear: 100, revenueYoyPct: -10, revenueDirection: 'down', netProfit: 5, netProfitSameQuarterLastYear: 10, netProfitYoyPct: -50, netProfitDirection: 'down', netMargin: 5.6, netMarginSameQuarterLastYear: 10, netMarginChangePp: -4.4 }),
  q(2025, 4, { revenue: 100, revenueSameQuarterLastYear: 100, revenueYoyPct: 0.3, revenueDirection: 'flat', netProfit: 10, netProfitSameQuarterLastYear: 10, netProfitYoyPct: 0, netProfitDirection: 'flat', netMargin: 10, netMarginSameQuarterLastYear: 10, netMarginChangePp: 0 }),
  q(2025, 3, { revenue: 100, revenueSameQuarterLastYear: 100, revenueYoyPct: 0, revenueDirection: 'flat', netProfit: 10, netProfitSameQuarterLastYear: 10, netProfitYoyPct: 0, netProfitDirection: 'flat', netMargin: 10, netMarginSameQuarterLastYear: 10, netMarginChangePp: 0 }),
  q(2025, 2), q(2025, 1), q(2024, 4), q(2024, 3),
];

describe('QuarterBarsComponent: doanh thu và lợi nhuận theo quý, cùng quý năm trước', () => {
  let fixture: ComponentFixture<QuarterBarsComponent>;
  let comp: QuarterBarsComponent;

  const el = () => fixture.nativeElement as HTMLElement;
  const qa = (id: string) => Array.from(el().querySelectorAll(`[data-testid="${id}"]`)) as HTMLElement[];
  const one = (id: string) => el().querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const bars = (metric: string, kind?: string) => qa('qb-bar').filter(b => b.dataset['metric'] === metric && (!kind || b.dataset['kind'] === kind));
  const yoy = (metric: string, period: string) => qa('qb-yoy').find(t => t.dataset['metric'] === metric && t.dataset['period'] === period)!;
  const show = (quarters: QuarterTrend[], annual: AnnualTrend[] = []) => {
    comp.quarters = quarters;
    comp.annual = annual;
    comp.note = NOTE;
    fixture.detectChanges();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [QuarterBarsComponent] });
    fixture = TestBed.createComponent(QuarterBarsComponent);
    comp = fixture.componentInstance;
  });

  describe('cột ghép: quý này và cùng quý năm trước', () => {
    it('grouped_bars_current_vs_same_quarter_last_year: one pair per quarter, oldest on the left', () => {
      show(eight());

      expect(bars('revenue', 'current').map(b => b.dataset['period'])).toEqual(['Q3/2024', 'Q4/2024', 'Q1/2025', 'Q2/2025', 'Q3/2025', 'Q4/2025', 'Q1/2026', 'Q2/2026']);
      // quý có cùng quý năm trước thì có thêm cột nhạt; quý cũ nhất thì không (không bịa cột 0)
      expect(bars('revenue', 'prev').map(b => b.dataset['period'])).toEqual(['Q3/2025', 'Q4/2025', 'Q1/2026', 'Q2/2026']);
      expect(bars('profit', 'prev').length).toBe(4);
    });

    it('the last-year bar uses the lighter step of the same hue and sits beside the current one', () => {
      show(eight());

      const cur = bars('revenue', 'current').find(b => b.dataset['period'] === 'Q2/2026')!;
      const prev = bars('revenue', 'prev').find(b => b.dataset['period'] === 'Q2/2026')!;
      expect(getComputedStyle(cur).fill).not.toBe(getComputedStyle(prev).fill);
      expect(cur.getAttribute('class')).toContain('qb-current');
      expect(prev.getAttribute('class')).toContain('qb-prev');
      expect(Number(prev.dataset['x'])).toBeLessThan(Number(cur.dataset['x']));
    });

    it('the legend reads "Quý này" / "Cùng quý năm trước"', () => {
      show(eight());

      const text = one('qb-legend')!.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('Quý này');
      expect(text).toContain('Cùng quý năm trước');
    });

    it('bar values come from the server (heights scale with the value, current taller than a smaller prev)', () => {
      show(eight());

      const cur = bars('revenue', 'current').find(b => b.dataset['period'] === 'Q2/2026')!;      // 110
      const prev = bars('revenue', 'prev').find(b => b.dataset['period'] === 'Q2/2026')!;         // 100
      const heightOf = (b: HTMLElement) => Number(b.dataset['bottom']) - Number(b.dataset['top']);
      expect(heightOf(cur)).toBeGreaterThan(heightOf(prev));
      expect(heightOf(cur) / heightOf(prev)).toBeCloseTo(1.1, 1);
    });
  });

  describe('nhãn %YoY phía trên mỗi cặp', () => {
    it('shows ▲ +10,0% for up, ▼ −10,0% for down and the flat arrow for flat', () => {
      show(eight());

      expect(yoy('revenue', 'Q2/2026').textContent).toBe('▲ +10,0%');
      expect(yoy('revenue', 'Q1/2026').textContent).toBe('▼ −10,0%');
      expect(yoy('revenue', 'Q4/2025').textContent).toBe('▬ +0,3%');
      expect(yoy('profit', 'Q2/2026').textContent).toBe('▲ +20,0%');
    });

    it('yoy_shown_only_when_same_quarter_last_year_exists: otherwise a dash and no arrow', () => {
      show(eight());

      expect(yoy('revenue', 'Q3/2024').textContent).toBe('—');
      expect(yoy('profit', 'Q4/2024').textContent).toBe('—');
    });

    it('colour is secondary: up/down/flat classes, but the sign and % are always in the text', () => {
      show(eight());

      expect(yoy('revenue', 'Q2/2026').getAttribute('class')).toContain('viz-up');
      expect(yoy('revenue', 'Q1/2026').getAttribute('class')).toContain('viz-down');
      expect(yoy('revenue', 'Q4/2025').getAttribute('class')).toContain('viz-flat');
    });

    it('a note-only direction (loss to profit): the bar label is a dash, never a bare arrow; direction and note live in the tooltip', () => {
      const list = eight();
      list[0] = { ...list[0], netProfit: 8, netProfitSameQuarterLastYear: -5, netProfitYoyPct: null, netProfitDirection: 'up', netProfitNote: 'từ lỗ sang lãi' };
      show(list);

      expect(yoy('profit', 'Q2/2026').textContent).toBe('—');
      comp.hover('profit', 'Q2/2026', { clientX: 10, clientY: 10 } as MouseEvent, document.createElement('div'));
      fixture.detectChanges();
      expect(one('chart-tip')!.textContent).toContain('▲ từ lỗ sang lãi');
      expect(one('chart-tip')!.textContent).toContain('so với cùng kỳ năm trước');
    });
  });

  describe('trục bắt đầu từ 0', () => {
    it('an all-positive series starts its bars at zero: heights are proportional to the values (no truncated axis)', () => {
      const list = [q(2026, 2, { revenue: 100, netProfit: 50 }), q(2026, 1, { revenue: 50, netProfit: 25 })];
      show(list);

      const height = (period: string) => {
        const b = bars('revenue', 'current').find(x => x.dataset['period'] === period)!;
        return Number(b.dataset['bottom']) - Number(b.dataset['top']);
      };
      expect(height('Q2/2026') / height('Q1/2026')).toBeCloseTo(2, 1);
      expect(height('Q2/2026')).withContext('cột cao nhất không vượt vùng vẽ: trục bắt đầu từ 0, không cắt cụt').toBeLessThanOrEqual(95);
      expect(height('Q2/2026')).toBeGreaterThan(60);
      const baseline = Number(qa('qb-baseline').find(b => b.dataset['metric'] === 'revenue')!.getAttribute('y1'));
      for (const b of bars('revenue')) expect(Number(b.dataset['bottom'])).toBeCloseTo(baseline, 0);
    });
  });

  describe('lợi nhuận âm', () => {
    const withLoss = () => {
      const list = eight();
      list[0] = { ...list[0], netProfit: -20, netMargin: -18.2 };
      return list;
    };

    it('quarter_bars_render_negative_profit_below_baseline', () => {
      show(withLoss());

      const baseline = Number(qa('qb-baseline').find(b => b.dataset['metric'] === 'profit')!.getAttribute('y1'));
      const loss = bars('profit', 'current').find(b => b.dataset['period'] === 'Q2/2026')!;
      const gain = bars('profit', 'current').find(b => b.dataset['period'] === 'Q1/2026')!;

      expect(loss.dataset['negative']).toBe('true');
      expect(Number(loss.dataset['top'])).toBeCloseTo(baseline, 0);          // cột lỗ treo từ đường gốc xuống
      expect(Number(loss.dataset['bottom'])).toBeGreaterThan(baseline + 2);
      expect(gain.dataset['negative']).toBe('false');
      expect(Number(gain.dataset['bottom'])).toBeCloseTo(baseline, 0);       // cột lãi mọc từ đường gốc lên
      expect(Number(gain.dataset['top'])).toBeLessThan(baseline - 2);
    });

    it('the tooltip of a loss quarter says "Lỗ"', () => {
      show(withLoss());

      comp.hover('profit', 'Q2/2026', { clientX: 10, clientY: 10 } as MouseEvent, document.createElement('div'));
      fixture.detectChanges();

      expect(one('chart-tip')!.textContent).toContain('Lỗ');
    });

    it('revenue never gets a below-baseline axis when it has no negatives', () => {
      show(withLoss());

      expect(bars('revenue').every(b => b.dataset['negative'] === 'false')).toBeTrue();
    });
  });

  describe('biên lợi nhuận ròng (biểu đồ đường riêng)', () => {
    it('margin_null_when_revenue_zero_or_missing: a null margin leaves a gap, not a point at 0', () => {
      const list = eight();
      list[2] = { ...list[2], netMargin: null };      // Q4/2025
      show(list);

      const periods = qa('qb-margin-point').map(p => p.dataset['period']);
      expect(periods).not.toContain('Q4/2025');
      expect(periods.length).toBe(7);
      // đường bị ngắt thành hai đoạn quanh chỗ thiếu
      expect(qa('qb-margin-line').length).toBe(2);
    });

    it('is a separate chart with its own title and no second axis on the bars', () => {
      show(eight());

      expect(one('qb-margin')!.textContent).toContain('Biên lợi nhuận ròng');
      expect(qa('qb-axis-right').length).toBe(0);
    });

    it('markers are at least 8px (r ≥ 4)', () => {
      show(eight());

      qa('qb-margin-point').forEach(p => expect(Number(p.getAttribute('r'))).toBeGreaterThanOrEqual(4));
    });
  });

  describe('khoảng thời gian', () => {
    const many = () => Array.from({ length: 12 }, (_, i) => q(2026 - Math.floor(i / 4), 4 - (i % 4)));

    it('shows the last 8 quarters by default with a toggle to all available', () => {
      show(many());

      expect(bars('revenue', 'current').length).toBe(8);
      one('qb-show-all')!.click();
      fixture.detectChanges();
      expect(bars('revenue', 'current').length).toBe(12);
    });

    it('offers no toggle when there are 8 quarters or fewer', () => {
      show(eight());

      expect(one('qb-show-all')).toBeNull();
    });
  });

  describe('xem theo năm', () => {
    const annual = (over: Partial<AnnualTrend> = {}): AnnualTrend[] => [
      { year: 2026, quartersPresent: 2, complete4Quarters: false },
      { year: 2025, quartersPresent: 4, complete4Quarters: true, revenue: 400, prevYearRevenue: 320, revenueYoyPct: 25, revenueDirection: 'up', netProfit: 40, prevYearNetProfit: 80, netProfitYoyPct: -50, netProfitDirection: 'down', ...over },
      { year: 2024, quartersPresent: 2, complete4Quarters: false },
    ];

    it('annual_view_requires_4_quarters: the toggle exists only when some year has all four quarters', () => {
      show(eight(), annual());
      expect(one('qb-mode-year')).not.toBeNull();
      fixture.destroy();

      fixture = TestBed.createComponent(QuarterBarsComponent);
      comp = fixture.componentInstance;
      show(eight(), [{ year: 2025, quartersPresent: 3, complete4Quarters: false }]);
      expect(one('qb-mode-year')).toBeNull();
    });

    it('year view draws only complete years and marks the others "chưa đủ 4 quý"', () => {
      show(eight(), annual());

      one('qb-mode-year')!.click();
      fixture.detectChanges();

      expect(bars('revenue', 'current').map(b => b.dataset['period'])).toEqual(['2025']);
      expect(bars('revenue', 'prev').map(b => b.dataset['period'])).toEqual(['2025']);
      expect(qa('qb-incomplete').map(t => t.dataset['period']).sort()).toEqual(['2024', '2026']);
      expect(qa('qb-incomplete')[0].textContent).toContain('chưa đủ 4 quý');
      expect(yoy('revenue', '2025').textContent).toBe('▲ +25,0%');
      expect(one('qb-legend')!.textContent).toContain('Năm trước');
    });

    it('a complete year without a previous year has no previous bar and a dash', () => {
      show(eight(), annual({ prevYearRevenue: null, revenueYoyPct: null, revenueDirection: null, prevYearNetProfit: null, netProfitYoyPct: null, netProfitDirection: null }));

      one('qb-mode-year')!.click();
      fixture.detectChanges();

      expect(bars('revenue', 'prev').length).toBe(0);
      expect(yoy('revenue', '2025').textContent).toBe('—');
    });

    it('an incomplete year WITH figures never enables the Năm toggle (the check is on complete4Quarters, not on having numbers)', () => {
      show(eight(), [{ year: 2025, quartersPresent: 3, complete4Quarters: false, revenue: 300, netProfit: 30, prevYearRevenue: 200, prevYearNetProfit: 20 }]);

      expect(one('qb-mode-year')).toBeNull();
    });

    it('in year mode an incomplete year with figures gets no bars and "chưa đủ 4 quý" in the table', () => {
      show(eight(), [
        { year: 2026, quartersPresent: 2, complete4Quarters: false, revenue: 999, netProfit: 99, prevYearRevenue: 888, prevYearNetProfit: 88 },
        { year: 2025, quartersPresent: 4, complete4Quarters: true, revenue: 400, prevYearRevenue: 320, revenueYoyPct: 25, revenueDirection: 'up', netProfit: 40, prevYearNetProfit: 80, netProfitYoyPct: -50, netProfitDirection: 'down' },
      ]);
      one('qb-mode-year')!.click();
      fixture.detectChanges();

      expect(bars('revenue').map(b => b.dataset['period'])).not.toContain('2026');
      expect(bars('profit').map(b => b.dataset['period'])).not.toContain('2026');
      const row = qa('qb-row').find(r => r.dataset['period'] === '2026')!;
      expect(row.textContent).toContain('chưa đủ 4 quý');
      expect(row.textContent).not.toContain('999');
    });

    it('year mode brings the quarter view back on request', () => {
      show(eight(), annual());
      one('qb-mode-year')!.click();
      fixture.detectChanges();

      one('qb-mode-quarter')!.click();
      fixture.detectChanges();

      expect(bars('revenue', 'current').length).toBe(8);
    });
  });

  describe('bảng, ghi chú và dữ liệu thiếu', () => {
    it('table_toggle_shows_same_numbers: the table lists the exact values of the bars', () => {
      show(eight());

      const row = qa('qb-row').find(r => r.dataset['period'] === 'Q2/2026')!;
      const text = row.textContent!.replace(/\s+/g, ' ');
      expect(text).toContain('110');
      expect(text).toContain('100');
      expect(text).toContain('+10,0%');
      expect(text).toContain('12');
      expect(text).toContain('+20,0%');
      expect(text).toContain('10,9%');
    });

    it('a null value is a dash in the table, never 0', () => {
      const list = eight();
      list[0] = { ...list[0], revenue: null, revenueSameQuarterLastYear: null, revenueYoyPct: null, revenueDirection: null };
      show(list);

      const text = qa('qb-row').find(r => r.dataset['period'] === 'Q2/2026')!.textContent!;
      expect(text).toContain('—');
      expect(bars('revenue', 'current').map(b => b.dataset['period'])).not.toContain('Q2/2026');
    });

    it('a null net margin is a dash in the table, never 0,0%', () => {
      const list = eight();
      list[0] = { ...list[0], netMargin: null, netMarginSameQuarterLastYear: null, netMarginChangePp: null };
      show(list);

      const cells = Array.from(qa('qb-row').find(r => r.dataset['period'] === 'Q2/2026')!.querySelectorAll('td'));
      const margin = cells[cells.length - 1].textContent!.trim();
      expect(margin).toBe('—');
      expect(margin).not.toContain('0,0');
    });

    it('no_monthly_data_for_financials_note: the server note is shown', () => {
      show(eight());

      expect(one('qb-note')!.textContent).toBe(NOTE);
    });

    it('missing_section_shows_chua_co_du_lieu when there is no quarter or nothing but nulls', () => {
      show([]);
      expect(one('chart-empty')!.textContent).toBe('Chưa có dữ liệu');
      fixture.destroy();

      fixture = TestBed.createComponent(QuarterBarsComponent);
      comp = fixture.componentInstance;
      show([q(2026, 1, { revenue: null, netProfit: null, netMargin: null })]);
      expect(one('chart-empty')).not.toBeNull();
    });

    it('server strings never become markup in the tooltip', () => {
      const list = eight();
      list[0] = { ...list[0], netProfitNote: '<img src=x onerror=alert(1)>' };
      show(list);

      comp.hover('profit', 'Q2/2026', { clientX: 10, clientY: 10 } as MouseEvent, document.createElement('div'));
      fixture.detectChanges();

      expect(el().querySelector('img')).toBeNull();
    });
  });

  describe('theo chế độ giao diện', () => {
    afterEach(() => document.documentElement.classList.remove('dark'));

    it('charts_follow_theme_on_toggle: bar fills are CSS variables, so the colour changes with the dark class without a redraw', () => {
      show(eight());
      const bar = bars('revenue', 'current')[0];

      document.documentElement.classList.remove('dark');
      const light = getComputedStyle(bar).fill;
      document.documentElement.classList.add('dark');
      const dark = getComputedStyle(bar).fill;

      expect(light).toBe('rgb(37, 125, 224)');     // --viz-series-1 sáng #257de0
      expect(dark).toBe('rgb(51, 140, 237)');      // --viz-series-1 tối #338ced
    });
  });
  describe('chữ đủ đọc ở cột hẹp (≥ 11px thực tế)', () => {
    it('không co nhỏ theo cột: ở khung 360px biểu đồ giữ cỡ gốc và cuộn ngang trong khung riêng, mọi chữ SVG ≥ 11px', () => {
      (fixture.nativeElement as HTMLElement).style.display = 'block';
      (fixture.nativeElement as HTMLElement).style.width = '360px';
      show(eight());

      const svg = el().querySelector('svg.qb-svg') as SVGSVGElement;
      const scale = svg.getBoundingClientRect().width / svg.viewBox.baseVal.width;
      const texts = Array.from(svg.querySelectorAll('text')) as SVGTextElement[];
      expect(texts.length).toBeGreaterThan(5);
      for (const t of texts) {
        const px = parseFloat(getComputedStyle(t).fontSize) * scale;
        expect(px).withContext(t.getAttribute('class') ?? 'text').toBeGreaterThanOrEqual(11);
      }
      expect(document.documentElement.scrollWidth).withContext('trang không cuộn ngang').toBeLessThanOrEqual(window.innerWidth);
    });
  });
});
