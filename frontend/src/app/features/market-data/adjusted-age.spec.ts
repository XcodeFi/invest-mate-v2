import { formatAsOf, tradingDaysBehind } from './adjusted-age';

// Ngày dương lịch cố định: 2026-11-23 là thứ Hai.
const d = (iso: string) => new Date(`${iso}T12:00:00`);

describe('tradingDaysBehind: chuỗi đã điều chỉnh lùi sau hôm nay bao nhiêu phiên (bỏ thứ Bảy, Chủ nhật)', () => {
  it('same day → 0', () => expect(tradingDaysBehind('2026-11-24', d('2026-11-24'))).toBe(0));
  it('Monday → Tuesday is 1 session', () => expect(tradingDaysBehind('2026-11-23', d('2026-11-24'))).toBe(1));
  it('Friday → Monday is 1 session (the weekend is skipped)', () => expect(tradingDaysBehind('2026-11-20', d('2026-11-23'))).toBe(1));
  it('Friday → Tuesday is 2 sessions', () => expect(tradingDaysBehind('2026-11-20', d('2026-11-24'))).toBe(2));
  it('Friday → Sunday is 0 (no session happened)', () => expect(tradingDaysBehind('2026-11-20', d('2026-11-22'))).toBe(0));
  it('a week behind is 5 sessions', () => expect(tradingDaysBehind('2026-11-16', d('2026-11-23'))).toBe(5));
  it('an as-of date in the future never counts as behind', () => expect(tradingDaysBehind('2026-12-01', d('2026-11-24'))).toBe(0));
  it('accepts the ISO date-time the API sends', () => expect(tradingDaysBehind('2026-11-20T00:00:00Z', d('2026-11-24'))).toBe(2));
  it('garbage or missing → 0 (no note rather than a wrong note)', () => {
    expect(tradingDaysBehind('không phải ngày', d('2026-11-24'))).toBe(0);
    expect(tradingDaysBehind(null, d('2026-11-24'))).toBe(0);
    expect(tradingDaysBehind(undefined, d('2026-11-24'))).toBe(0);
  });
});

describe('formatAsOf', () => {
  it('shows dd/MM/yyyy', () => expect(formatAsOf('2026-11-20T00:00:00Z')).toBe('20/11/2026'));
  it('garbage → empty', () => expect(formatAsOf('xx')).toBe(''));
});
