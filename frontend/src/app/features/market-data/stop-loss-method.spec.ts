import { isLowRiskReward, stopLossMethodLabel } from './stop-loss-method';

describe('stop-loss-method', () => {
  it('nhãn tiếng Việt cho từng cách tính cắt lỗ', () => {
    expect(stopLossMethodLabel('support2')).toBe('theo hỗ trợ thứ 2');
    expect(stopLossMethodLabel('atr2x')).toBe('theo 2×ATR');
    expect(stopLossMethodLabel('percent5')).toBe('−5% dưới hỗ trợ');
  });

  it('không có hoặc lạ thì chuỗi rỗng (không bịa nhãn)', () => {
    expect(stopLossMethodLabel(undefined)).toBe('');
    expect(stopLossMethodLabel(null)).toBe('');
    expect(stopLossMethodLabel('khac')).toBe('');
  });

  it('R:R thấp chỉ khi biết và dưới 1', () => {
    expect(isLowRiskReward(0.99)).toBeTrue();
    expect(isLowRiskReward(0)).toBeTrue();
    expect(isLowRiskReward(1)).toBeFalse();
    expect(isLowRiskReward(2.5)).toBeFalse();
    expect(isLowRiskReward(undefined)).toBeFalse();
    expect(isLowRiskReward(null)).toBeFalse();
  });
});
