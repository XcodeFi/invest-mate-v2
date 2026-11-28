import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TrendTemplate, TrendTemplateCriterion } from '../../../core/services/market-data.service';
import { TrendTemplateCardComponent } from './trend-template-card.component';

const row = (over: Partial<TrendTemplateCriterion>): TrendTemplateCriterion =>
  ({ key: 'k', rule: 'Giá nằm trên SMA150', value: 7.32, unit: '%', threshold: 0, met: true, ...over });

const TEMPLATE: TrendTemplate = {
  sessions: 400, note: 'Đây là bộ lọc theo xu hướng, không phải tín hiệu mua; mỗi dòng đứng riêng và không có kết luận chung.',
  criteria: [
    row({ key: 'price_above_sma150', rule: 'Giá nằm trên SMA150', value: 7.32, met: true }),
    row({ key: 'sma200_rising', rule: 'SMA200 dốc lên ít nhất 1 tháng (21 phiên)', value: -0.4, met: false }),
    row({ key: 'near_52w_high', rule: 'Giá cách đỉnh 52 tuần không quá 25%', value: 12.5, threshold: 25, met: true }),
    row({ key: 'above_52w_low', rule: 'Giá cao hơn đáy 52 tuần ít nhất 30%', value: null, threshold: 30, met: null, detail: 'Không đủ dữ liệu (52 tuần cần 250 phiên).' }),
    row({ key: 'rs_rating', rule: 'Xếp hạng sức mạnh tương đối (RS) từ 70 trở lên', value: null, unit: null, threshold: null, met: null, detail: 'Ứng dụng chưa có xếp hạng RS theo chuẩn IBD nên không tính tiêu chí này.' }),
  ],
};

describe('TrendTemplateCardComponent', () => {
  let fixture: ComponentFixture<TrendTemplateCardComponent>;
  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll('[data-testid="tt-row"]')) as HTMLElement[];

  function setup(tt: TrendTemplate | null | undefined) {
    TestBed.configureTestingModule({ imports: [TrendTemplateCardComponent] });
    fixture = TestBed.createComponent(TrendTemplateCardComponent);
    fixture.componentInstance.template = tt;
    fixture.detectChanges();
  }
  afterEach(() => fixture?.destroy());

  it('shows nothing when the backend sent no template', () => {
    setup(null);

    expect(el().querySelector('[data-testid="tt-card"]')).toBeNull();
  });

  it('shows nothing when the field is absent (older backend)', () => {
    setup(undefined);

    expect(el().querySelector('[data-testid="tt-card"]')).toBeNull();
  });

  it('lists every criterion with its rule and its distance in percent (Vietnamese decimal comma, explicit sign)', () => {
    setup(TEMPLATE);

    expect(rows().length).toBe(5);
    expect(rows()[0].textContent).toContain('Giá nằm trên SMA150');
    expect(rows()[0].textContent).toContain('+7,32%');
    expect(rows()[1].textContent).toContain('−0,4%');
    expect(rows()[2].textContent).toContain('12,5%');
  });

  it('each row says met, not met or no data in words and a symbol, never by colour alone', () => {
    setup(TEMPLATE);

    expect(rows()[0].getAttribute('data-met')).toBe('true');
    expect(rows()[0].textContent).toContain('✓');
    expect(rows()[0].textContent).toContain('thoả');
    expect(rows()[1].getAttribute('data-met')).toBe('false');
    expect(rows()[1].textContent).toContain('✗');
    expect(rows()[1].textContent).toContain('chưa thoả');
    expect(rows()[3].getAttribute('data-met')).toBe('none');
    expect(rows()[3].textContent).toContain('Không đủ dữ liệu');
  });

  it('a row without data shows the reason and never a zero', () => {
    setup(TEMPLATE);

    expect(rows()[3].querySelector('[data-testid="tt-value"]')).toBeNull();
    expect(rows()[3].textContent).toContain('52 tuần cần 250 phiên');
    expect(rows()[4].textContent).toContain('IBD');
  });

  it('the card has NO overall verdict: no pass/fail badge, no count of passed criteria', () => {
    setup(TEMPLATE);

    const text = el().textContent ?? '';
    expect(text).not.toMatch(/ĐẠT|PASS|\d+\s*\/\s*\d+\s*(tiêu chí|đạt)/i);
    expect(el().querySelector('[data-testid="tt-verdict"]')).toBeNull();
    expect(text).toContain('không phải tín hiệu mua');
  });

  it('credits Mark Minervini as the source of the rules and says the app uses SMA like him', () => {
    setup(TEMPLATE);

    const text = el().textContent ?? '';
    expect(text).toContain('Mark Minervini');
    expect(text).toContain('Trade Like a Stock Market Wizard');
    expect(text).toContain('SMA');
  });

  /** Màu thật người xem thấy: chữ của dòng trạng thái và nền thẻ, đo bằng style tính toán của trình duyệt ở từng giao diện. */
  const measure = (dark: boolean, row: HTMLElement): number => {
    const root = document.documentElement;
    const had = root.classList.contains('dark');
    root.classList.toggle('dark', dark);
    try {
      const parse = (c: string): number[] => (c.match(/[\d.]+/g) ?? []).map(Number).slice(0, 3);
      const lum = ([r, g, b]: number[]) => {
        const f = (v: number) => { const x = v / 255; return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4); };
        return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
      };
      const fg = parse(getComputedStyle(row.querySelector('span')!).color);
      const bg = parse(getComputedStyle(el().querySelector('[data-testid="tt-card"]')!).backgroundColor);
      const [hi, lo] = [lum(fg), lum(bg)].sort((x, y) => y - x);
      return (hi + 0.05) / (lo + 0.05);
    } finally { root.classList.toggle('dark', had); }
  };

  it('the met, not-met and no-data state labels meet AA contrast (4.5:1) in the light and the dark theme', () => {
    setup(TEMPLATE);
    document.body.appendChild(el());

    [0, 1, 3].forEach(i => {
      expect(measure(false, rows()[i])).withContext(`sáng, dòng ${i}`).toBeGreaterThanOrEqual(4.5);
      expect(measure(true, rows()[i])).withContext(`tối, dòng ${i}`).toBeGreaterThanOrEqual(4.5);
    });
  });
});
