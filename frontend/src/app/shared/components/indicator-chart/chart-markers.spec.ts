import { snapMarkers, toSeriesData } from './chart-markers';
import { IndicatorChartMarker } from './indicator-chart.types';

// 2026-11-20 là thứ Sáu, 2026-11-23 là thứ Hai: ngày 21 và 22 là cuối tuần không có phiên.
const times = ['2026-11-18', '2026-11-19', '2026-11-20', '2026-11-23', '2026-11-24'];

const m = (time: string, text = 'T', extra: Partial<IndicatorChartMarker> = {}): IndicatorChartMarker =>
  ({ time, position: 'belowBar', color: '#22c55e', shape: 'arrowUp', text, ...extra });

describe('snapMarkers: đặt marker lên phiên giao dịch', () => {
  it('marker on a trading day keeps its day', () => {
    const { markers } = snapMarkers([m('2026-11-19')], times);

    expect(markers.map(x => x.time)).toEqual(['2026-11-19']);
  });

  it('marker on a non-trading day (Saturday, Sunday) snaps to the previous session', () => {
    const { markers } = snapMarkers([m('2026-11-21'), m('2026-11-22')], times);

    expect(markers.map(x => x.time)).toEqual(['2026-11-20', '2026-11-20']);
  });

  it('accepts ISO date-times and uses the date part', () => {
    const { markers } = snapMarkers([m('2026-11-21T09:30:00Z')], times);

    expect(markers[0].time).toBe('2026-11-20');
  });

  it('marker before the first session is dropped (no earlier session to hold it)', () => {
    const { markers } = snapMarkers([m('2026-11-10')], times);

    expect(markers).toEqual([]);
  });

  it('marker after the last session snaps to the last session', () => {
    const { markers } = snapMarkers([m('2026-11-30')], times);

    expect(markers.map(x => x.time)).toEqual(['2026-11-24']);
  });

  it('output is sorted by time as the chart requires, and keeps the other fields', () => {
    const { markers } = snapMarkers([m('2026-11-24', 'B'), m('2026-11-18', 'A', { shape: 'circle', color: '#6366f1', position: 'aboveBar' })], times);

    expect(markers.map(x => x.text)).toEqual(['A', 'B']);
    expect(markers[0]).toEqual({ time: '2026-11-18', position: 'aboveBar', color: '#6366f1', shape: 'circle', text: 'A' });
  });

  it('collects tooltip lines per snapped day, in input order, merging markers that land on the same session', () => {
    const { tooltips } = snapMarkers([
      m('2026-11-21', 'T', { tooltip: ['MUA 100 × 63.000đ'] }),
      m('2026-11-20', 'J', { tooltip: ['Nhật ký: vào lệnh'] }),
      m('2026-11-19', 'E'),
    ], times);

    expect(tooltips.get('2026-11-20')).toEqual(['MUA 100 × 63.000đ', 'Nhật ký: vào lệnh']);
    expect(tooltips.has('2026-11-19')).toBeFalse();
  });

  it('no sessions → no markers', () => {
    expect(snapMarkers([m('2026-11-19')], []).markers).toEqual([]);
  });
});

describe('toSeriesData: dữ liệu chuỗi theo trục thời gian chung', () => {
  const t = ['2026-11-18', '2026-11-19', '2026-11-20'];

  it('null becomes a blank point (time only), not zero', () => {
    expect(toSeriesData(t, [0, 1, 2], [1, null, 3])).toEqual([{ time: '2026-11-18', value: 1 }, { time: '2026-11-19' }, { time: '2026-11-20', value: 3 }]);
  });

  it('follows the source index when the axis skips source rows', () => {
    expect(toSeriesData(['a', 'b'], [0, 2], [10, 20, 30])).toEqual([{ time: 'a', value: 10 }, { time: 'b', value: 30 }]);
  });

  it('per-bar colors are attached to the points that have a value and a color', () => {
    const data = toSeriesData(t, [0, 1, 2], [5, null, 7], ['#111', '#222', null]);

    expect(data[0]).toEqual({ time: '2026-11-18', value: 5, color: '#111' });
    expect(data[1]).toEqual({ time: '2026-11-19' });
    expect(data[2]).toEqual({ time: '2026-11-20', value: 7 });
  });
});
