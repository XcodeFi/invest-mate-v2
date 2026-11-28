import { Injectable, signal } from '@angular/core';

/** Trạng thái mở/đóng của bảng lệnh Ctrl+K; nút ở thanh trên và phím tắt cùng dùng. */
@Injectable({ providedIn: 'root' })
export class CommandPaletteService {
  readonly isOpen = signal(false);
  /** Phần tử đang giữ tiêu điểm lúc mở: đóng thì trả tiêu điểm về đó (hộp thoại có aria-modal nên phải trả lại chỗ cũ). */
  private opener: HTMLElement | null = null;

  open(): void {
    if (!this.isOpen()) this.opener = document.activeElement as HTMLElement | null;
    this.isOpen.set(true);
  }

  close(): void {
    if (!this.isOpen()) return;
    this.isOpen.set(false);
    const back = this.opener;
    this.opener = null;
    if (back && back.isConnected && typeof back.focus === 'function') back.focus();
  }

  toggle(): void { this.isOpen() ? this.close() : this.open(); }
}
