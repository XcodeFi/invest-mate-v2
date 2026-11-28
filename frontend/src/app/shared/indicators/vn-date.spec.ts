import { vnDay } from './vn-date';

describe('vnDay: ngày theo giờ Việt Nam (UTC+7) của một mốc thời gian', () => {
  it('a date-only string is returned as is', () => {
    expect(vnDay('2026-09-10')).toBe('2026-09-10');
  });

  it('midnight UTC is the same VN date (07:00 local)', () => {
    expect(vnDay('2026-09-10T00:00:00Z')).toBe('2026-09-10');
  });

  it('17:00 UTC is already the NEXT date in Vietnam (a trade entered at VN midnight)', () => {
    expect(vnDay('2026-09-09T17:00:00Z')).toBe('2026-09-10');
    expect(vnDay('2026-09-09T16:59:59Z')).toBe('2026-09-09');
  });

  it('an explicit +07:00 offset keeps its own date', () => {
    expect(vnDay('2026-09-10T00:00:00+07:00')).toBe('2026-09-10');
  });

  it('a timestamp without an offset is read as UTC, like the backend sends it, not as the browser local time', () => {
    expect(vnDay('2026-09-09T17:00:00')).toBe('2026-09-10');
  });

  it('accepts a Date', () => {
    expect(vnDay(new Date('2026-09-09T18:30:00Z'))).toBe('2026-09-10');
  });

  it('garbage falls back to its first ten characters instead of throwing', () => {
    expect(vnDay('không phải ngày')).toBe('không phải');
    expect(vnDay('')).toBe('');
  });
});
