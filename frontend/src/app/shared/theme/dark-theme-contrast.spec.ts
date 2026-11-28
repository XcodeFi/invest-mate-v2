import { blendOver, contrastRatio, Rgb } from './contrast';

/**
 * Đọc đúng file src/styles/dark-theme.css (karma phục vụ ở /theme-fixtures, xem angular.json) và kiểm các cặp chữ/nền
 * khai báo ở khối biến `--dm-*` đạt WCAG AA (>= 4,5:1). Đổi một màu trong CSS làm cặp tụt dưới ngưỡng thì test này đỏ.
 */
describe('dark-theme.css', () => {
  const MIN = 4.5;
  let css = '';
  const vars = new Map<string, string>();

  beforeAll(async () => {
    const res = await fetch('/theme-fixtures/dark-theme.css');
    expect(res.ok).withContext('dark-theme.css phải được karma phục vụ ở /theme-fixtures').toBeTrue();
    css = await res.text();
    for (const m of css.matchAll(/--dm-([a-z0-9-]+):\s*([^;]+);/g)) vars.set(m[1], m[2].trim());
  });

  const v = (name: string): string => {
    const value = vars.get(name);
    if (!value) throw new Error(`Thiếu biến --dm-${name} trong dark-theme.css`);
    return value;
  };
  const rgbOf = (hue: string): Rgb => v(`${hue}-rgb`).split(/\s+/).map(Number) as Rgb;

  const SURFACES = ['page', 'surface', 'raised'];
  const NEUTRAL_TEXT = ['text', 'text-2', 'muted', 'subtle'];
  const HUES = ['blue', 'red', 'green', 'amber', 'yellow', 'indigo', 'violet', 'purple', 'emerald', 'teal', 'orange', 'cyan', 'pink'];

  it('declares the surface and text tokens', () => {
    [...SURFACES, 'raised-2', 'border', ...NEUTRAL_TEXT].forEach(n => expect(v(n)).toMatch(/^#[0-9a-f]{6}$/i));
  });

  for (const text of NEUTRAL_TEXT) {
    for (const surface of SURFACES) {
      it(`${text} on ${surface} is at least 4.5:1`, () => {
        expect(contrastRatio(v(text), v(surface))).toBeGreaterThanOrEqual(MIN);
      });
    }
  }

  it('muted and secondary text also read on the stronger raised surface (bg-gray-200 chips)', () => {
    ['text', 'text-2', 'muted'].forEach(t => expect(contrastRatio(v(t), v('raised-2'))).withContext(t).toBeGreaterThanOrEqual(MIN));
  });

  for (const hue of HUES) {
    it(`${hue}: status text reads on the page, the card and the tinted panels (10%, 16%, 24%)`, () => {
      const rgb = rgbOf(hue);
      for (const shade of ['4', '3']) {
        const colour = v(`${hue}-${shade}`);
        for (const surface of ['page', 'surface']) {
          expect(contrastRatio(colour, v(surface))).withContext(`${hue}-${shade} on ${surface}`).toBeGreaterThanOrEqual(MIN);
        }
        for (const alpha of [0.1, 0.16, 0.24]) {
          const tint = blendOver(rgb, alpha, v('surface'));
          expect(contrastRatio(colour, tint)).withContext(`${hue}-${shade} on tint ${alpha}`).toBeGreaterThanOrEqual(MIN);
        }
      }
    });
  }

  // ───── chế độ sáng không đổi ─────

  it('every rule is scoped under html.dark, so light mode is untouched', () => {
    const stripped = css.replace(/\/\*[\s\S]*?\*\//g, '');
    const selectors = stripped
      .split('}')
      .map(block => block.split('{')[0].trim())
      .filter(s => s.length > 0)
      .flatMap(s => s.split(/,\s*\n|,\s*(?=html\.dark)/))
      .map(s => s.trim())
      .filter(s => s.length > 0);

    expect(selectors.length).toBeGreaterThan(100);
    expect(selectors.filter(s => !s.startsWith('html.dark'))).toEqual([]);
  });

  it('remaps the utilities that carry the page: surfaces, text, borders, hover, tints, inputs', () => {
    [
      ':where(.bg-white)', ':where(.bg-gray-50)', ':where(.text-gray-500)', ':where(.text-gray-700)', ':where(.border-gray-300)',
      '.hover\\:bg-gray-50:hover', ':where(.bg-red-100)', ':where(.text-green-600)', ':where(.text-red-600)', '.card', '.nav-item', 'select option'
    ].forEach(s => expect(css).withContext(s).toContain(s));
    expect(css).toContain('color-scheme: dark');
  });

  it('plain-class rules use :where() so hover:/focus: variants still win over them', () => {
    // Luật cho class trơn có độ ưu tiên (0,1,1), thấp hơn .hover\:x:hover (0,2,0): hover vẫn hoạt động ở chế độ tối.
    expect(css).toContain('html.dark :where(.text-blue-600)');
    expect(css).not.toMatch(/html\.dark \.text-blue-600\b/);
  });
});
