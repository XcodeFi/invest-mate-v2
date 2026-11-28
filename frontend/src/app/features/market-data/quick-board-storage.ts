/** Khoá localStorage nhớ danh sách của Bảng giá nhanh (trang Thị trường); trang Sàng lọc cổ phiếu cũng ghi vào đây khi gửi mã sang. */
export const QUICK_BOARD_KEY = 'imv2.quickBoard.symbols';

/**
 * Ghi danh sách mã (đã chuẩn hoá) cho Bảng giá nhanh. Trình duyệt chặn lưu trữ thì không ném lỗi mà trả false, để nơi gọi báo cho người dùng
 * (trang Sàng lọc chuyển danh sách qua đây nên không được để mất lặng lẽ).
 */
export function saveQuickBoardSymbols(symbols: readonly string[]): boolean {
  try { localStorage.setItem(QUICK_BOARD_KEY, symbols.join(',')); return true; } catch { return false; }
}
