import { Injectable, signal } from '@angular/core';
import { isChartRoute } from '../../nav/nav-config';

/** Khoá localStorage lưu lựa chọn thu gọn/mở rộng của người dùng: '1' = thu gọn, '0' = mở rộng; khác thì coi như chưa chọn. */
export const SIDEBAR_STORAGE_KEY = 'imv2.sidebar.collapsed';

/**
 * Trạng thái thu gọn của menu trái. Chưa có lựa chọn nào thì mặc định theo trang (trang nhiều biểu đồ → thanh biểu tượng để biểu đồ rộng nhất);
 * một lựa chọn tường minh của người dùng được lưu lại và luôn thắng mặc định. Truy cập storage bọc try/catch: bị chặn thì vẫn đổi được trong phiên.
 */
@Injectable({ providedIn: 'root' })
export class SidebarService {
  /** null = người dùng chưa chọn. */
  private readonly choice = signal<boolean | null>(SidebarService.read());

  /** Menu có đang thu gọn ở URL này không. */
  collapsed(url: string): boolean {
    return this.choice() ?? isChartRoute(url);
  }

  /** Đảo trạng thái đang thấy và nhớ như lựa chọn của người dùng. */
  toggle(url: string): void {
    const next = !this.collapsed(url);
    this.choice.set(next);
    try { localStorage.setItem(SIDEBAR_STORAGE_KEY, next ? '1' : '0'); } catch { /* bị chặn: chỉ không nhớ được */ }
  }

  private static read(): boolean | null {
    try {
      const raw = localStorage.getItem(SIDEBAR_STORAGE_KEY);
      return raw === '1' ? true : raw === '0' ? false : null;
    } catch { return null; }
  }
}
