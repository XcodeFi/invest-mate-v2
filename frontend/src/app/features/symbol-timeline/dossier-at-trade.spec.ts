import { dossierBadge } from './dossier-at-trade';

describe('dossierBadge (hồ sơ lúc đó của một giao dịch)', () => {
  it('no dossier (null/undefined) shows no badge', () => {
    expect(dossierBadge(null)).toBeNull();
    expect(dossierBadge(undefined)).toBeNull();
  });

  it('unsigned: says the dossier is not signed', () => {
    const b = dossierBadge({ state: 'unsigned' })!;

    expect(b.text).toBe('Hồ sơ chưa ký');
    expect(b.icon).toBe('⚪');
  });

  it('resigned-after: names the re-sign date (dd/mm, VN day), gives no verdict and explains why in the tooltip', () => {
    const b = dossierBadge({ state: 'resigned-after', resignedAt: '2026-11-19T17:30:00Z' })!;   // 00:30 giờ VN ngày 20/11

    expect(b.text).toBe('Hồ sơ ký lại sau lệnh này (20/11)');
    expect(b.tooltip).toContain('lần ký trước đó không được lưu');
    expect(b.text).not.toContain('cần soát lại');
    expect(b.text).not.toContain('hết hạn');
  });

  it('signed + fresh: "ký N ngày trước lệnh" only', () => {
    const b = dossierBadge({ state: 'signed', daysSigned: 12, freshness: 'fresh' })!;

    expect(b.text).toBe('Hồ sơ ký 12 ngày trước lệnh');
    expect(b.icon).toBe('✅');
  });

  it('signed + needs-review: adds "cần soát lại"', () => {
    const b = dossierBadge({ state: 'signed', daysSigned: 90, freshness: 'needs-review' })!;

    expect(b.text).toBe('Hồ sơ ký 90 ngày trước lệnh · cần soát lại');
    expect(b.icon).toBe('⚠️');
  });

  it('signed + expired: adds "hết hạn"', () => {
    const b = dossierBadge({ state: 'signed', daysSigned: 180, freshness: 'expired' })!;

    expect(b.text).toBe('Hồ sơ ký 180 ngày trước lệnh · hết hạn');
    expect(b.icon).toBe('⛔');
  });

  it('signed on the same day reads "cùng ngày", not "0 ngày trước"', () => {
    expect(dossierBadge({ state: 'signed', daysSigned: 0, freshness: 'fresh' })!.text).toBe('Hồ sơ ký cùng ngày với lệnh');
  });

  it('the 90 and 180 thresholds are NOT in the frontend: the verdict comes from the server freshness only', () => {
    // 400 ngày nhưng server nói fresh (giả định): FE không tự suy ra, chỉ hiển thị điều server nói
    expect(dossierBadge({ state: 'signed', daysSigned: 400, freshness: 'fresh' })!.text).not.toContain('hết hạn');
  });

  it('an unknown state shows no badge instead of guessing', () => {
    expect(dossierBadge({ state: 'tuong-lai' })).toBeNull();
  });
});
