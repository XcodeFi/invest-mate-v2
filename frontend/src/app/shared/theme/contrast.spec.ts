import { blendOver, contrastRatio, ensureContrastOnDark, parseHex, relativeLuminance, toHex } from './contrast';

describe('contrast helper', () => {
  it('black on white is 21:1 and the order does not matter', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 5);
    expect(contrastRatio('#ffffff', '#000000')).toBeCloseTo(21, 5);
  });

  it('identical colours are 1:1', () => {
    expect(contrastRatio('#336699', '#336699')).toBeCloseTo(1, 5);
  });

  it('matches known WCAG values', () => {
    expect(contrastRatio('#777777', '#ffffff')).toBeCloseTo(4.48, 1);   // ngay dưới 4,5: tiêu chuẩn kinh điển
    expect(contrastRatio('#767676', '#ffffff')).toBeGreaterThanOrEqual(4.5);
  });

  it('parses short and long hex and rejects garbage', () => {
    expect(parseHex('#fff')).toEqual([255, 255, 255]);
    expect(parseHex('0b1220')).toEqual([11, 18, 32]);
    expect(() => parseHex('#12')).toThrow();
    expect(toHex([11, 18, 32])).toBe('#0b1220');
  });

  it('luminance: white 1, black 0', () => {
    expect(relativeLuminance([255, 255, 255])).toBeCloseTo(1, 5);
    expect(relativeLuminance([0, 0, 0])).toBe(0);
  });

  it('blendOver puts a translucent colour on its background', () => {
    expect(blendOver([255, 255, 255], 0.5, '#000000')).toBe('#808080');
    expect(blendOver([255, 0, 0], 0, '#112233')).toBe('#112233');
    expect(blendOver([255, 0, 0], 1, '#112233')).toBe('#ff0000');
  });

  it('ensureContrastOnDark lightens a too-dark line and leaves a readable one alone', () => {
    const bg = '#111827';
    const lightened = ensureContrastOnDark('#1f2937', bg);   // gần như cùng màu nền
    expect(lightened).not.toBe('#1f2937');
    expect(contrastRatio(lightened, bg)).toBeGreaterThanOrEqual(3);
    expect(ensureContrastOnDark('#22c55e', bg)).toBe('#22c55e');
    expect(ensureContrastOnDark('#2563eb', bg)).toBe('#2563eb');
  });
});
