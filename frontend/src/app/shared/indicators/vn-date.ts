const VN_DAY = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' });
const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;
const HAS_ZONE = /(Z|[+-]\d{2}:?\d{2})$/i;

/**
 * Ngày (YYYY-MM-DD) theo giờ Việt Nam của một mốc thời gian. Giao dịch và nhật ký lưu mốc UTC; một lệnh nhập lúc 00:00 giờ Việt Nam
 * nằm ở 17:00 UTC hôm trước, nên cắt 10 ký tự đầu của chuỗi UTC sẽ lệch về phiên trước. Chuỗi chỉ có ngày giữ nguyên; chuỗi không có
 * múi giờ được đọc như UTC (đúng cách backend gửi), không phải giờ máy của trình duyệt. Sai dạng thì trả 10 ký tự đầu, không ném lỗi.
 */
export function vnDay(value: string | Date): string {
  if (typeof value === 'string' && DATE_ONLY.test(value)) return value;
  const text = typeof value === 'string' ? (HAS_ZONE.test(value) ? value : `${value}Z`) : value;
  const date = new Date(text);
  if (Number.isNaN(date.getTime())) return typeof value === 'string' ? value.substring(0, 10) : '';
  return VN_DAY.format(date);
}
