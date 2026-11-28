import { convertToParamMap } from '@angular/router';
import { buildTimelineQuery, parseTimelineQuery, RANGE_BY_KEY, rangeKey } from './timeline-url-state';

describe('timeline URL state', () => {
  const parse = (q: Record<string, string>) => parseTimelineQuery(convertToParamMap(q));

  it('range: reads the four labels and nothing else', () => {
    expect(parse({ range: '1T' }).range).toBe(1);
    expect(parse({ range: '3T' }).range).toBe(3);
    expect(parse({ range: '6T' }).range).toBe(6);
    expect(parse({ range: '1N' }).range).toBe(12);
    expect(parse({ range: '2T' }).range).toBeUndefined();
    expect(parse({ range: '12' }).range).toBeUndefined();
    expect(parse({ range: '' }).range).toBeUndefined();
    expect(parse({}).range).toBeUndefined();
  });

  it('range: inherited object keys are not labels (constructor, toString, __proto__)', () => {
    ['constructor', 'toString', '__proto__', 'hasOwnProperty'].forEach(k => expect(parse({ range: k }).range).toBeUndefined());
  });

  it('grp: reads the six indicator group keys and ignores unknown values', () => {
    ['tong-hop', 'xu-huong', 'dong-luong', 'bien-dong', 'dong-tien', 'moc-gia']
      .forEach(g => expect(parse({ grp: g }).group).toBe(g as any));
    expect(parse({ grp: 'khong-co' }).group).toBeUndefined();
    expect(parse({}).group).toBeUndefined();
  });

  it('focus: accepts a real YYYY-MM-DD date only', () => {
    expect(parse({ focus: '2026-11-20' }).focus).toBe('2026-11-20');
    expect(parse({ focus: '2026-02-30' }).focus).toBeUndefined();   // ngày không có thật
    expect(parse({ focus: '20/11/2026' }).focus).toBeUndefined();
    expect(parse({ focus: '2026-11-20T10:00:00' }).focus).toBeUndefined();
    expect(parse({}).focus).toBeUndefined();
  });

  it('does not read symbol or tradeId: those keys stay owned by their existing logic', () => {
    const state = parse({ symbol: 'FPT', tradeId: 't1' }) as Record<string, unknown>;

    expect(Object.keys(state).filter(k => state[k] !== undefined)).toEqual([]);
  });

  it('rangeKey is the inverse of the parse table', () => {
    [1, 3, 6, 12].forEach(m => expect(RANGE_BY_KEY[rangeKey(m)!]).toBe(m));
    expect(rangeKey(2)).toBeNull();
  });

  it('build: writes range always, grp and focus only when set (null removes the key under merge)', () => {
    expect(buildTimelineQuery({ range: 6, group: 'xu-huong', focus: '2026-11-20' }))
      .toEqual({ range: '6T', grp: 'xu-huong', focus: '2026-11-20' });
    expect(buildTimelineQuery({ range: 12, group: null, focus: null }))
      .toEqual({ range: '1N', grp: null, focus: null });
  });

  it('round trip: what build writes, parse reads back', () => {
    const query = buildTimelineQuery({ range: 1, group: 'moc-gia', focus: '2026-01-05' }) as Record<string, string>;

    expect(parse(query)).toEqual({ range: 1, group: 'moc-gia', focus: '2026-01-05' });
  });
});
