import { previewAdjustment } from './corporate-actions.component';

describe('previewAdjustment', () => {
  it('cổ tức cổ phiếu 30% — tăng số lượng, giảm giá vốn, giữ tổng vốn', () => {
    const r = previewAdjustment(1000, 25_000_000, 'StockDividend', null, 100, 130);
    expect(r.quantityAfter).toBe(1300);
    expect(r.averageCostAfter).toBeCloseTo(19_230.77, 2);
    expect(r.totalCostAfter).toBe(25_000_000);
  });

  it('cổ phiếu lẻ làm tròn xuống', () => {
    const r = previewAdjustment(137, 3_425_000, 'StockDividend', null, 100, 130);
    expect(r.quantityAfter).toBe(178);
  });

  it('cổ tức tiền mặt 5% — giữ nguyên giá vốn, tính tiền theo mệnh giá', () => {
    const r = previewAdjustment(1000, 55_000_000, 'CashDividend', 5, null, null);
    expect(r.quantityAfter).toBe(1000);
    expect(r.averageCostAfter).toBe(55_000);
    expect(r.cashGross).toBe(500_000);
    expect(r.cashNet).toBe(475_000);
  });

  it('chia tách 1:2', () => {
    const r = previewAdjustment(500, 30_000_000, 'StockSplit', null, 1, 2);
    expect(r.quantityAfter).toBe(1000);
    expect(r.averageCostAfter).toBe(30_000);
  });

  it('không có vị thế — không chia cho 0', () => {
    const r = previewAdjustment(0, 0, 'StockDividend', null, 100, 130);
    expect(r.quantityAfter).toBe(0);
    expect(r.averageCostAfter).toBe(0);
  });
});
