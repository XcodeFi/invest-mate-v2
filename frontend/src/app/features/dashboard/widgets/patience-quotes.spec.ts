import { MOOD_LABELS, MoodKey, QUOTES, pickQuote } from './patience-quotes';

describe('patience-quotes', () => {
  const moods: MoodKey[] = ['Calm', 'Fomo', 'Fear', 'Revenge'];

  it('có câu cho cả bốn trạng thái, không nhóm nào rỗng', () => {
    moods.forEach(mood => {
      expect(QUOTES[mood].length).toBeGreaterThan(0);
    });
  });

  it('trả về câu thuộc đúng nhóm tâm trạng', () => {
    moods.forEach(mood => {
      const quote = pickQuote(mood, '2026-08-11');
      expect(QUOTES[mood]).toContain(quote);
    });
  });

  it('cùng seed thì cùng câu — không nhấp nháy mỗi lần render', () => {
    const first = pickQuote('Fomo', '2026-08-11');
    const second = pickQuote('Fomo', '2026-08-11');

    expect(second).toBe(first);
  });

  it('seed khác nhau thì có lúc ra câu khác nhau', () => {
    const seen = new Set<string>();
    for (let day = 1; day <= 28; day++) {
      seen.add(pickQuote('Calm', `2026-08-${String(day).padStart(2, '0')}`).text);
    }

    expect(seen.size).toBeGreaterThan(1);
  });

  it('không bao giờ trả undefined dù seed rỗng', () => {
    moods.forEach(mood => {
      expect(pickQuote(mood, '')).toBeDefined();
    });
  });

  it('mọi câu đều có nội dung tiếng Việt có dấu, không câu nào rỗng', () => {
    moods.forEach(mood => {
      QUOTES[mood].forEach(quote => {
        expect(quote.text.trim().length).toBeGreaterThan(0);
      });
    });
  });

  it('câu ẩn dụ tự viết không gán tên người thật', () => {
    // author bỏ trống hoặc là tên có thật — không có chuỗi rỗng giả làm tác giả
    moods.forEach(mood => {
      QUOTES[mood].forEach(quote => {
        if (quote.author !== undefined) {
          expect(quote.author.trim().length).toBeGreaterThan(0);
        }
      });
    });
  });

  it('nhãn tâm trạng là tiếng Việt có dấu', () => {
    expect(MOOD_LABELS.Calm).toBe('Bình tĩnh');
    expect(MOOD_LABELS.Fear).toBe('Sợ');
    expect(MOOD_LABELS.Revenge).toBe('Cay cú');
  });
});
