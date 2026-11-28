import { Injectable, signal } from '@angular/core';

/** Trạng thái mở/đóng của bảng lệnh Ctrl+K; nút ở thanh trên và phím tắt cùng dùng. */
@Injectable({ providedIn: 'root' })
export class CommandPaletteService {
  readonly isOpen = signal(false);
  open(): void { this.isOpen.set(true); }
  close(): void { this.isOpen.set(false); }
  toggle(): void { this.isOpen.update(v => !v); }
}
