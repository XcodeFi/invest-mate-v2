/** Khoá localStorage nhớ danh sách của Bảng giá nhanh (trang Thị trường); trang Sàng lọc cổ phiếu cũng ghi vào đây khi gửi mã sang. */
export const QUICK_BOARD_KEY = 'imv2.quickBoard.symbols';

/** Ghi danh sách mã (đã chuẩn hoá) cho Bảng giá nhanh; trình duyệt chặn lưu trữ thì chỉ không nhớ được, không ném lỗi. */
export function saveQuickBoardSymbols(symbols: readonly string[]): void {
  try { localStorage.setItem(QUICK_BOARD_KEY, symbols.join(',')); } catch { /* bị chặn: chỉ không nhớ được */ }
}
