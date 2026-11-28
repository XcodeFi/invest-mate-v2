import { TrapEvent } from '../../core/services/market-data.service';
import { buildTrapMarkers } from './trap-markers';

const trap = (over: Partial<TrapEvent> = {}): TrapEvent => ({
  type: 'failed-breakout', typeVi: 'Phá vỡ thất bại', breakoutDate: '2026-08-03T00:00:00', returnDate: '2026-08-05T00:00:00', confirmedDate: '2026-08-10T00:00:00',
  levelPrice: 100.4, stopPrice: 98.6, riskPct: 1.8, excursionPct: 0.8,
  description: 'Giá đóng cửa vượt đỉnh 20 phiên rồi quay lại.', disclaimer: 'Mốc xác nhận hồi tố sau 5 phiên, không phải dự báo.', ...over,
});

describe('buildTrapMarkers: mốc bẫy xác nhận hồi tố', () => {
  it('returns nothing without events', () => {
    expect(buildTrapMarkers(undefined)).toEqual([]);
    expect(buildTrapMarkers(null)).toEqual([]);
    expect(buildTrapMarkers([])).toEqual([]);
  });

  it('a failed breakout is a red arrow down above the breakout bar, dated by the breakout (retroactive), not by the confirmation', () => {
    const [m] = buildTrapMarkers([trap()]);

    expect(m.time).toBe('2026-08-03');
    expect(m.position).toBe('aboveBar');
    expect(m.shape).toBe('arrowDown');
    expect(m.color).toBe('#dc2626');
    expect(m.text).toBe('Bẫy tăng');
  });

  it('each marker names its theme tone so the chart can keep the label text readable in light and dark', () => {
    expect(buildTrapMarkers([trap()])[0].tone).toBe('trapBull');
    expect(buildTrapMarkers([trap({ type: 'bear-trap', typeVi: 'Bẫy giảm giá' })])[0].tone).toBe('trapBear');
  });

  it('a bear trap is a green arrow up below the bar', () => {
    const [m] = buildTrapMarkers([trap({ type: 'bear-trap', typeVi: 'Bẫy giảm giá' })]);

    expect(m.position).toBe('belowBar');
    expect(m.shape).toBe('arrowUp');
    expect(m.color).toBe('#15803d');
    expect(m.text).toBe('Bẫy giảm');
  });

  it('the tooltip carries the confirmation date, the "Nếu sai" distance with the stop, the excursion and the disclaimer', () => {
    const [m] = buildTrapMarkers([trap()]);
    const lines = m.tooltip!;

    expect(lines[0]).toBe('Phá vỡ thất bại (xác nhận ngày 10/08)');
    expect(lines.some(l => l === 'Nếu sai: −1,8% từ pivot 100,4 tới cắt lỗ quy ước 98,6')).toBeTrue();
    expect(lines.some(l => l.includes('xuyên qua pivot tối đa 0,8%'))).toBeTrue();
    expect(lines[lines.length - 1]).toContain('không phải dự báo');
  });

  it('a bear trap talks about the support, not the pivot', () => {
    const [m] = buildTrapMarkers([trap({ type: 'bear-trap', typeVi: 'Bẫy giảm giá', levelPrice: 99.6, stopPrice: 94.7, riskPct: 4.9, excursionPct: 3.7 })]);

    expect(m.tooltip!.some(l => l === 'Nếu sai: −4,9% từ hỗ trợ 99,6 tới cắt lỗ quy ước 94,7')).toBeTrue();
    expect(m.tooltip!.some(l => l.includes('xuyên qua hỗ trợ tối đa 3,7%'))).toBeTrue();
  });

  it('keeps one marker per event in the order given', () => {
    const ms = buildTrapMarkers([trap(), trap({ breakoutDate: '2026-09-01T00:00:00' })]);

    expect(ms.map(m => m.time)).toEqual(['2026-08-03', '2026-09-01']);
  });

  it('shows no frequency, rate, probability or score wording', () => {
    const text = JSON.stringify(buildTrapMarkers([trap(), trap({ type: 'bear-trap', typeVi: 'Bẫy giảm giá' })])).toLowerCase();

    expect(text).not.toMatch(/xác suất|tần suất|tỉ lệ|tỷ lệ|điểm số|probab|score/);
  });
});
