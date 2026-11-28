/** Ngày (yyyy-MM-dd) đầu một chuỗi ISO; không đúng dạng thì null. Không đi qua Date để khỏi lệch múi giờ. */
function dateKey(iso: string | null | undefined): { y: number; m: number; d: number } | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso ?? '');
  return match ? { y: +match[1], m: +match[2], d: +match[3] } : null;
}

/**
 * Chuỗi đã điều chỉnh (tới ngày `asOf`) lùi sau hôm nay bao nhiêu phiên giao dịch, đếm các ngày thứ Hai đến thứ Sáu
 * trong khoảng (asOf, hôm nay]. Không biết ngày lễ nên có thể đếm dư vào dịp nghỉ lễ; thiếu hoặc sai ngày thì 0 (không ghi chú).
 */
export function tradingDaysBehind(asOf: string | null | undefined, now: Date): number {
  const k = dateKey(asOf);
  if (!k) return 0;
  const cursor = new Date(Date.UTC(k.y, k.m - 1, k.d));
  const end = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  let n = 0;
  for (cursor.setUTCDate(cursor.getUTCDate() + 1); cursor.getTime() <= end; cursor.setUTCDate(cursor.getUTCDate() + 1)) {
    const dow = cursor.getUTCDay();
    if (dow !== 0 && dow !== 6) n++;
  }
  return n;
}

/** dd/MM/yyyy từ ngày ISO; sai dạng thì chuỗi rỗng. */
export function formatAsOf(iso: string | null | undefined): string {
  const k = dateKey(iso);
  return k ? `${String(k.d).padStart(2, '0')}/${String(k.m).padStart(2, '0')}/${k.y}` : '';
}
