import { MoodHistoryDto } from '../../core/services/mood.service';
import { buildCells, cellLabel, countByMood, emotionalDays, leadingBlanks, MOOD_MARK, UNRATED_MARK } from './mood-journal-model';

const history = (days: MoodHistoryDto['days'], from = '2026-11-23', to = '2026-11-29'): MoodHistoryDto => ({ from, to, days });

describe('mood-journal-model', () => {
  it('builds one cell per VN day from "from" to "to", in order, including days with nothing', () => {
    const cells = buildCells(history([{ dateKey: '2026-11-25', mood: 'Calm', changes: [], overrode: false, tradeCount: 0, emotionalTrade: false }]));

    expect(cells.map(c => c.dateKey)).toEqual(['2026-11-23', '2026-11-24', '2026-11-25', '2026-11-26', '2026-11-27', '2026-11-28', '2026-11-29']);
    expect(cells[2].day?.mood).toBe('Calm');
    expect(cells[0].day).toBeNull();
  });

  it('a day with no check-in is "chưa chấm" (null mood), also when it has trades', () => {
    const cells = buildCells(history([{ dateKey: '2026-11-24', mood: null, changes: [], overrode: false, tradeCount: 2, emotionalTrade: false }]));

    expect(cells[1].mood).toBeNull();
    expect(cellLabel(cells[1])).toContain('chưa chấm');
    expect(cellLabel(cells[1])).toContain('2 lệnh');
  });

  it('weekday: Monday is 0, Sunday is 6, and the blanks before the first cell align the grid', () => {
    const cells = buildCells(history([], '2026-11-25', '2026-11-26'));   // thứ Tư và thứ Năm

    expect(cells.map(c => c.weekday)).toEqual([2, 3]);
    expect(leadingBlanks(cells)).toBe(2);
    expect(leadingBlanks(buildCells(history([], '2026-11-23', '2026-11-23')))).toBe(0);
    expect(leadingBlanks(buildCells(history([], '2026-11-29', '2026-11-29')))).toBe(6);
    expect(leadingBlanks([])).toBe(0);
  });

  it('counts per mood, and the unrated count is every day without a check-in', () => {
    const d = (dateKey: string, mood: any) => ({ dateKey, mood, changes: [], overrode: false, tradeCount: 0, emotionalTrade: false });
    const counts = countByMood(buildCells(history([d('2026-11-23', 'Calm'), d('2026-11-24', 'Calm'), d('2026-11-25', 'Fomo'), d('2026-11-26', 'Fear'), d('2026-11-27', 'Revenge'), d('2026-11-28', null)])));

    expect(counts).toEqual({ Calm: 2, Fomo: 1, Fear: 1, Revenge: 1, unrated: 2 });   // 28/11 có lệnh nhưng chưa chấm + 29/11 trống
  });

  it('every mood has its own symbol, a label and a token, none shared with another or with "chưa chấm" (never colour alone)', () => {
    const marks = Object.values(MOOD_MARK).map(m => m.glyph);

    expect(new Set([...marks, UNRATED_MARK.glyph]).size).toBe(5);
    expect(MOOD_MARK.Calm.glyph).toBe('■');
    expect(MOOD_MARK.Fomo.glyph).toBe('▲');
    expect(MOOD_MARK.Fear.glyph).toBe('▼');
    expect(MOOD_MARK.Revenge.glyph).toBe('✕');
    expect(UNRATED_MARK.glyph).toBe('□');
    expect(new Set(Object.values(MOOD_MARK).map(m => m.token)).size).toBe(4);
  });

  it('the emotional-trade list keeps only days flagged by the server, newest first, and never recomputes the flag', () => {
    const d = (dateKey: string, emotionalTrade: boolean, mood: any = 'Fomo') => ({ dateKey, mood, changes: [], overrode: false, tradeCount: 1, emotionalTrade });

    const list = emotionalDays(history([d('2026-11-24', true), d('2026-11-25', false), d('2026-11-27', true, 'Revenge')]));

    expect(list.map(x => x.dateKey)).toEqual(['2026-11-27', '2026-11-24']);
  });

  it('the cell label lists the date, the mood, the changes with time and source, and the trade count', () => {
    const cells = buildCells(history([{
      dateKey: '2026-11-25', mood: 'Fear', overrode: true, tradeCount: 1, emotionalTrade: true,
      changes: [
        { at: '2026-11-25T02:30:00Z', mood: 'Calm', source: 'web' },
        { at: '2026-11-25T08:05:00Z', mood: 'Fear', source: 'mcp' },
      ]
    }]));

    const label = cellLabel(cells[2]);

    expect(label).toContain('25/11/2026');
    expect(label).toContain('Sợ');
    expect(label).toContain('09:30 Bình tĩnh');   // 02:30Z = 09:30 giờ VN
    expect(label).toContain('15:05 Sợ (từ agent)');
    expect(label).toContain('1 lệnh');
    expect(label).toContain('đã bấm qua lớp phủ');
  });
});
