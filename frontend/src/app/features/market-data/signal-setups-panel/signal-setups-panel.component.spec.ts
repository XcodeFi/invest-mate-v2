import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SignalSetups } from '../../../core/services/market-data.service';
import { SignalSetupsPanelComponent } from './signal-setups-panel.component';

describe('SignalSetupsPanelComponent', () => {
  let fixture: ComponentFixture<SignalSetupsPanelComponent>;
  const q = (id: string): HTMLElement | null => fixture.nativeElement.querySelector(`[data-testid="${id}"]`);
  const txt = (id: string): string => (q(id)?.textContent ?? '').replace(/\s+/g, ' ').trim();

  function render(inputs: Record<string, unknown>) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [SignalSetupsPanelComponent] });
    fixture = TestBed.createComponent(SignalSetupsPanelComponent);
    Object.entries(inputs).forEach(([k, v]) => fixture.componentRef.setInput(k, v));
    fixture.detectChanges();
  }

  const notes = ['Giá đã điều chỉnh.', 'Thiên lệch người sống sót.', 'HOSE không có lệnh dừng.', 'T+2,5 mô phỏng thận trọng.'];

  function data(overrides: Partial<SignalSetups['setups'][0]> = {}, top: Partial<SignalSetups> = {}): SignalSetups {
    return {
      symbol: 'FPT', adjusted: true, adjustedAsOf: '2026-11-27T00:00:00', disclaimer: 'Kết quả quá khứ, không đảm bảo tương lai', notes,
      setups: [{
        key: 'pullback', name: 'Pullback xu hướng', description: 'mô tả', signals: 11, completed: 7, open: 1, skipped: 2,
        n: 7, minSample: 30, sampleSufficient: false,
        ...overrides
      }],
      ...top
    };
  }

  const active = {
    signalDate: '2026-11-27T00:00:00', entry: 100_000, entryIsEstimate: true, stop: 96_000, target: 108_000, status: 'moi'
  };

  it('title names the symbol', () => {
    render({ symbol: 'FPT', data: data() });

    expect(txt('signal-title')).toContain('Setup Pullback trên FPT');
  });

  it('leads with the active block when a signal is active, ahead of the counts line', () => {
    render({ symbol: 'FPT', data: data({ activePlan: active, activeSince: '2026-11-27T00:00:00' }) });

    const root = q('signal-setups')!;
    const ids = Array.from(root.querySelectorAll('[data-testid]')).map(e => e.getAttribute('data-testid'));
    expect(ids.indexOf('signal-active')).toBeGreaterThan(-1);
    expect(ids.indexOf('signal-active')).toBeLessThan(ids.indexOf('signal-counts'));
  });

  it('active block with an estimated entry says so and shows entry, stop and target', () => {
    render({ symbol: 'FPT', data: data({ activePlan: active }) });

    const t = txt('signal-active');
    expect(t).toContain('ước tính');
    expect(t).toContain('100.000');
    expect(t).toContain('96.000');
    expect(t).toContain('108.000');
    expect(t).not.toContain('ngày thứ');
  });

  it('says under the active block that its levels follow the setup rule, unlike the trade-suggestion card', () => {
    render({ symbol: 'FPT', data: data({ activePlan: active }) });

    expect(txt('signal-active-method')).toBe(
      'Theo luật setup Pullback: vào giá mở cửa phiên sau, cắt lỗ 2×ATR, mục tiêu 2R. Khác với thẻ Gợi ý giao dịch, vốn dựa trên hỗ trợ/kháng cự.');
  });

  it('the method line also shows for an open trade', () => {
    render({ symbol: 'FPT', data: data({ activePlan: { ...active, entryIsEstimate: false, status: 'dang-mo', dayInTrade: 1 } }) });

    expect(q('signal-active-method')).not.toBeNull();
  });

  it('has no method line when nothing is active', () => {
    render({ symbol: 'FPT', data: data({ activePlan: undefined }) });

    expect(q('signal-active-method')).toBeNull();
  });

  it('active block of an open trade shows its status with the day number', () => {
    render({ symbol: 'FPT', data: data({ activePlan: { ...active, entryIsEstimate: false, status: 'dang-mo', dayInTrade: 2 } }) });

    const t = txt('signal-active');
    expect(t).toContain('đang mở, ngày thứ 2');
    expect(t).not.toContain('ước tính');
  });

  it('no active signal shows a plain line, not an empty block', () => {
    render({ symbol: 'FPT', data: data({ activePlan: undefined }) });

    expect(q('signal-active')).toBeNull();
    expect(txt('signal-inactive')).toContain('Chưa có tín hiệu');
  });

  it('n below 30 shows only the counts line and the "not enough sample" sentence, no win rate or EV', () => {
    render({ symbol: 'FPT', data: data() });

    expect(txt('signal-counts')).toBe('11 tín hiệu, 7 lệnh hoàn tất trong 3 năm. Chưa đủ mẫu để thống kê riêng mã này (cần ≥ 30).');
    expect(q('signal-stats')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Tỉ lệ thắng');
    expect(fixture.nativeElement.textContent).not.toContain('EV');
  });

  it('n of 30 or more shows win rate and EV with their intervals instead of the not-enough sentence', () => {
    render({
      symbol: 'FPT',
      data: data({
        completed: 32, n: 32, sampleSufficient: true,
        winRate: 0.5, winRateLow: 0.33, winRateHigh: 0.67, evR: 0.2, evRLow: -0.1, evRHigh: 0.5
      })
    });

    expect(txt('signal-counts')).toBe('11 tín hiệu, 32 lệnh hoàn tất trong 3 năm.');
    const stats = txt('signal-stats');
    expect(stats).toContain('50%');
    expect(stats).toContain('33%');
    expect(stats).toContain('67%');
    expect(stats).toContain('+0.20R');
    expect(stats).toContain('-0.10R');
    expect(stats).toContain('+0.50R');
  });

  it('an EV interval that is not available is shown as n/a, not as zero', () => {
    render({
      symbol: 'FPT',
      data: data({ completed: 30, n: 30, sampleSufficient: true, winRate: 1, winRateLow: 0.88, winRateHigh: 1, evR: 1 })
    });

    expect(txt('signal-stats')).toContain('n/a');
  });

  it('the disclaimer is always present, with data, with an unavailable reason and while loading', () => {
    render({ symbol: 'FPT', data: data() });
    expect(txt('signal-disclaimer')).toBe('Kết quả quá khứ, không đảm bảo tương lai');

    render({ symbol: 'NEW', data: data({}, { reasonUnavailable: 'Chưa đủ lịch sử giá đã điều chỉnh để thống kê', setups: [] }) });
    expect(txt('signal-disclaimer')).toBe('Kết quả quá khứ, không đảm bảo tương lai');

    render({ symbol: 'FPT', data: null, loading: true });
    expect(txt('signal-disclaimer')).toBe('Kết quả quá khứ, không đảm bảo tương lai');

    render({ symbol: 'FPT', data: null, error: 'Không tải được' });
    expect(txt('signal-disclaimer')).toBe('Kết quả quá khứ, không đảm bảo tương lai');
  });

  it('shows the disclosure notes under the table', () => {
    render({ symbol: 'FPT', data: data() });

    const t = txt('signal-notes');
    for (const n of notes) expect(t).toContain(n);
  });

  it('unavailable reason is one line of text and no counts, no table', () => {
    render({ symbol: 'NEW', data: data({}, { reasonUnavailable: 'Chưa đủ lịch sử giá đã điều chỉnh để thống kê', setups: [] }) });

    expect(txt('signal-reason')).toBe('Chưa đủ lịch sử giá đã điều chỉnh để thống kê');
    expect(q('signal-counts')).toBeNull();
    expect(q('signal-active')).toBeNull();
    expect(q('signal-notes')).toBeNull();
  });

  it('loading and error states are one short line each', () => {
    render({ symbol: 'FPT', data: null, loading: true });
    expect(txt('signal-loading')).toContain('Đang');

    render({ symbol: 'FPT', data: null, error: 'Không tải được lịch sử setup' });
    expect(txt('signal-error')).toBe('Không tải được lịch sử setup');
  });

  it('never lists individual trades', () => {
    render({ symbol: 'FPT', data: data({ activePlan: active }) });

    expect(fixture.nativeElement.querySelectorAll('table tbody tr').length).toBeLessThanOrEqual(2);
    expect(fixture.nativeElement.textContent.toLowerCase()).not.toContain('lệnh thắng');
  });

  // ───── tỉ lệ nền VN30 ─────

  const baseRate = {
    label: 'Tỉ lệ nền VN30', k: 28, n: 412, effectiveMonths: 34,
    winRate: 0.4283, winRateLow: 0.37, winRateHigh: 0.49, evR: 0.1205, evRLow: -0.05, evRHigh: 0.29, asOf: '2026-11-27T00:00:00',
    notes: [
      'Khoảng 95% là xấp xỉ: lấy mẫu lại theo khối tháng; các lệnh có thể kéo dài qua nhiều tháng và cùng chịu một bối cảnh thị trường, nên khoảng có thể vẫn hẹp hơn thực tế.',
      'Tham khảo cho setup trên rổ VN30 hiện tại — không phải bằng chứng riêng của mã này; số liệu mô tả quá khứ.'
    ]
  };

  it('a VN30 member shows the base-rate line with k, n, months, win rate and EV with approximate intervals', () => {
    render({ symbol: 'FPT', data: data({ baseRate }) });

    const t = txt('signal-baserate');
    expect(t).toContain('Tỉ lệ nền VN30');
    expect(t).toContain('28 mã');
    expect(t).toContain('412 lệnh');
    expect(t).toContain('34 tháng');
    expect(t).toContain('thắng 43%');
    expect(t).toContain('xấp xỉ');
    expect(t).toContain('37% – 49%');
    expect(t).toContain('+0.12R');
    expect(t).toContain('-0.05R – +0.29R');
  });

  it('the base-rate block carries the basket caveat and the clustering disclosure from the server', () => {
    render({ symbol: 'FPT', data: data({ baseRate }) });

    const notesText = txt('signal-baserate-notes');
    expect(notesText).toContain('không phải bằng chứng riêng của mã này');
    expect(notesText).toContain('kéo dài qua nhiều tháng');
  });

  it('the base-rate line sits below the own counts line of the stock', () => {
    render({ symbol: 'FPT', data: data({ baseRate }) });

    const ids = Array.from(q('signal-setups')!.querySelectorAll('[data-testid]')).map(e => e.getAttribute('data-testid'));
    expect(ids.indexOf('signal-baserate')).toBeGreaterThan(ids.indexOf('signal-counts'));
  });

  it('a non-member shows the reason instead of numbers', () => {
    render({ symbol: 'HHV', data: data({ baseRateReason: 'Chỉ có cho mã thuộc VN30' }) });

    expect(q('signal-baserate')).toBeNull();
    expect(txt('signal-baserate-reason')).toContain('Chỉ có cho mã thuộc VN30');
  });

  it('an insufficient basket shows the reason with the k/30 shortfall', () => {
    render({ symbol: 'FPT', data: data({ baseRateReason: 'Chưa đủ mã VN30 có dữ liệu (7/30, cần từ 15)' }) });

    expect(q('signal-baserate')).toBeNull();
    expect(txt('signal-baserate-reason')).toContain('7/30');
  });

  it('without a base rate or a reason nothing is shown (older backend)', () => {
    render({ symbol: 'FPT', data: data() });

    expect(q('signal-baserate')).toBeNull();
    expect(q('signal-baserate-reason')).toBeNull();
  });
});
