import { ErrorHandler, Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ErrorLoggerService } from '../services/error-logger.service';

/**
 * Bắt mọi lỗi chưa được xử lý của Angular và đẩy về server (server mới chuyển tiếp đi Telegram).
 *
 * `console.error` LUÔN chạy trước, kể cả ở production: nếu việc gửi log hỏng thì vẫn còn dấu vết
 * trong devtools. Chỉ gửi khi chạy production — không thì mỗi lần sửa code lúc đang phát triển
 * lại bắn một tin nhắn.
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly logger = inject(ErrorLoggerService);

  handleError(error: unknown): void {
    console.error(error);

    if (!environment.production) return;

    try {
      this.logger.log(error, 'Angular ErrorHandler');
    } catch {
      // Bộ xử lý lỗi mà tự ném thì Angular không còn chỗ nào để báo nữa.
    }
  }
}
