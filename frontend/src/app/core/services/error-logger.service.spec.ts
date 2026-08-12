import { buildPayload } from './error-logger.service';

describe('buildPayload', () => {
  it('lấy message và stack, cắt stack còn đúng 1000 ký tự', () => {
    const err = new Error('TypeError: đọc thuộc tính của undefined');
    err.stack = 'S'.repeat(5000);

    const p = buildPayload(err, 'Angular ErrorHandler');

    expect(p.message).toBe('TypeError: đọc thuộc tính của undefined');
    expect(p.stack!.length).toBe(1000);
    expect(p.context).toBe('Angular ErrorHandler');
  });

  it('cắt message quá dài còn 500 ký tự', () => {
    const p = buildPayload(new Error('m'.repeat(2000)));

    expect(p.message.length).toBe(500);
  });

  it('KHÔNG kéo theo object đính kèm — chỉ message và stack', () => {
    // Lỗi trong Angular hay mang theo cả object gây lỗi. Ở app này object đó có thể là
    // danh mục hoặc vị thế: JSON.stringify là đẩy thẳng giá trị tài sản ra ngoài.
    const err: any = new Error('Lưu kế hoạch thất bại');
    err.portfolio = { id: 'p1', totalValue: 1_500_000_000, positions: [{ symbol: 'HAH', qty: 5000 }] };

    const serialised = JSON.stringify(buildPayload(err));

    expect(serialised).not.toContain('1500000000');
    expect(serialised).not.toContain('positions');
    expect(serialised).not.toContain('HAH');
  });

  it('bỏ query string, chỉ giữ pathname', () => {
    const p = buildPayload(new Error('x'));

    expect(p.url).toBe(window.location.pathname);
    expect(p.url).not.toContain('?');
  });

  it('lỗi không phải Error vẫn dựng được payload', () => {
    expect(buildPayload('chuỗi trần').message).toBe('chuỗi trần');
    expect(buildPayload(null).message).toBe('Lỗi không rõ');
    expect(buildPayload({ message: '   ' }).message.length).toBeGreaterThan(0);
  });

  it('timestamp là ISO-8601', () => {
    expect(buildPayload(new Error('x')).timestamp).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/);
  });
});
