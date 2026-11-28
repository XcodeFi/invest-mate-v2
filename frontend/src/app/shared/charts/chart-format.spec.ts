import { formatCompactVn, formatPlainPct, formatSignedPct, quarterShortLabel } from './chart-format';

describe('chart-format', () => {
  describe('compact_vn_number_format (tiền tính bằng tỷ đồng)', () => {
    it('shows thousands of billions with one decimal', () => {
      expect(formatCompactVn(-0.004)).toBe('0 tỷ');   // làm tròn về 0 thì không mang dấu trừ
      expect(formatCompactVn(0.004)).toBe('0 tỷ');
      expect(formatCompactVn(-0.006)).toBe('−0,01 tỷ');
      expect(formatCompactVn(12345)).toBe('12,3 nghìn tỷ');
      expect(formatCompactVn(10000)).toBe('10 nghìn tỷ');
      expect(formatCompactVn(250000)).toBe('250 nghìn tỷ');
    });

    it('shows hundreds and thousands of billions in full with Vietnamese separators', () => {
      expect(formatCompactVn(1234)).toBe('1.234 tỷ');
      expect(formatCompactVn(9999)).toBe('9.999 tỷ');
      expect(formatCompactVn(100)).toBe('100 tỷ');
    });

    it('keeps one decimal below 100 and two below 1', () => {
      expect(formatCompactVn(45.3)).toBe('45,3 tỷ');
      expect(formatCompactVn(7)).toBe('7 tỷ');
      expect(formatCompactVn(0.45)).toBe('0,45 tỷ');
    });

    it('uses a real minus sign for losses and keeps the unit', () => {
      expect(formatCompactVn(-1234)).toBe('−1.234 tỷ');
      expect(formatCompactVn(-12345)).toBe('−12,3 nghìn tỷ');
    });

    it('null or non-finite is a dash, never 0', () => {
      expect(formatCompactVn(null)).toBe('—');
      expect(formatCompactVn(undefined)).toBe('—');
      expect(formatCompactVn(NaN)).toBe('—');
    });

    it('a true zero is 0 tỷ (it is a value, unlike null)', () => {
      expect(formatCompactVn(0)).toBe('0 tỷ');
    });
  });

  describe('percentages', () => {
    it('signed percent always carries the sign and one decimal', () => {
      expect(formatSignedPct(12.34)).toBe('+12,3%');
      expect(formatSignedPct(-8.1)).toBe('−8,1%');
      expect(formatSignedPct(0.3)).toBe('+0,3%');
      expect(formatSignedPct(0)).toBe('0,0%');
    });

    it('signed percent of null is a dash', () => {
      expect(formatSignedPct(null)).toBe('—');
    });

    it('plain percent has one decimal and no sign', () => {
      expect(formatPlainPct(18.64)).toBe('18,6%');
      expect(formatPlainPct(-25)).toBe('−25,0%');
      expect(formatPlainPct(null)).toBe('—');
    });
  });

  describe('quarter labels', () => {
    it('shortens the year: Q3/2026 → Q3/26', () => {
      expect(quarterShortLabel('Q3/2026')).toBe('Q3/26');
      expect(quarterShortLabel('Q1/2025')).toBe('Q1/25');
    });

    it('leaves anything that is not a quarter label untouched', () => {
      expect(quarterShortLabel('2025')).toBe('2025');
      expect(quarterShortLabel('')).toBe('');
    });
  });
});
