import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

/** Đúng những gì backend nhận. Không thêm field nào — mỗi field thêm là một đường rò tiềm năng. */
interface ClientErrorPayload {
  message: string;
  stack?: string;
  url: string;
  userAgent?: string;
  timestamp: string;
  context?: string;
}

const MAX_MESSAGE = 500;
const MAX_STACK = 1000;
const MAX_USER_AGENT = 200;

/**
 * Đẩy lỗi chưa bắt được của trình duyệt về server, để nó lọt vào cùng đường log với lỗi backend.
 *
 * KHÔNG gọi Telegram trực tiếp: bot token sẽ nằm trong bundle JS mà ai cũng đọc được.
 */
@Injectable({ providedIn: 'root' })
export class ErrorLoggerService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly endpoint = `${environment.apiUrl}/client-logs`;

  log(error: unknown, context?: string): void {
    const token = this.auth.getToken();
    // Chưa đăng nhập thì endpoint chặn bằng 401 — gửi cũng vô ích, mà còn tạo thêm một lỗi nữa.
    if (!token) return;

    const headers = new HttpHeaders({
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    });

    // Bắn-và-quên, nuốt lỗi. Nếu chính endpoint log hỏng mà ta lại báo lỗi, mỗi lần hỏng
    // sinh một lỗi mới — vòng lặp vô hạn.
    this.http.post(this.endpoint, buildPayload(error, context), { headers })
      .subscribe({ error: () => { /* cố ý im lặng */ } });
  }
}

/**
 * Chỉ lấy `message` và `stack`. KHÔNG BAO GIỜ `JSON.stringify(error)` — lỗi trong Angular hay
 * mang theo cả object đính kèm, và trong app này object đó có thể là danh mục hoặc vị thế.
 */
export function buildPayload(error: unknown, context?: string): ClientErrorPayload {
  const err = error as { message?: unknown; stack?: unknown } | null;

  const message = typeof err?.message === 'string' && err.message.trim()
    ? err.message
    : String(error ?? 'Lỗi không rõ');

  const stack = typeof err?.stack === 'string' ? err.stack : undefined;

  return {
    message: message.slice(0, MAX_MESSAGE),
    stack: stack?.slice(0, MAX_STACK),
    // Chỉ pathname. Query string có thể mang mã, id danh mục, tham số lọc.
    url: safePathname(),
    userAgent: typeof navigator !== 'undefined' ? navigator.userAgent.slice(0, MAX_USER_AGENT) : undefined,
    timestamp: new Date().toISOString(),
    context: context?.slice(0, 100),
  };
}

function safePathname(): string {
  if (typeof window === 'undefined' || !window.location) return '/';
  return window.location.pathname || '/';
}
