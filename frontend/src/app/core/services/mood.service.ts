import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export type MoodState = 'Calm' | 'Fomo' | 'Fear' | 'Revenge';

export interface TodayMoodDto {
  /** null khi hôm nay chưa chấm. */
  mood: MoodState | null;
  /** Đã bấm "Vẫn xem bây giờ" hôm nay chưa. */
  overrode: boolean;
  /** Lần đổi gần nhất do agent (MCP) ghi. Bình tĩnh do agent ghi thì `mood` là null (không tính là đã tự chấm), trang chủ hỏi lại. */
  fromAgent?: boolean;
}

export interface MoodChangeDto {
  at: string;
  mood: MoodState;
  /** "web" hoặc "mcp". */
  source: string;
}

export interface MoodDayDto {
  /** Ngày VN, YYYY-MM-DD. */
  dateKey: string;
  /** null = có lệnh nhưng chưa chấm tâm trạng. */
  mood: MoodState | null;
  changes: MoodChangeDto[];
  overrode: boolean;
  tradeCount: number;
  /** Đặt lệnh khi đang cảm xúc, do server tính. */
  emotionalTrade: boolean;
}

export interface MoodHistoryDto {
  from: string;
  to: string;
  /** Chỉ các ngày đã chấm hoặc có lệnh. */
  days: MoodDayDto[];
}

/**
 * Tâm trạng tự chấm mỗi ngày (ADR-0013). Lưu ở server theo tài khoản chứ không phải
 * localStorage — mở máy khác vẫn thấy, và xoá cache không mất.
 *
 * Ngày lịch VN do server tính; client không gửi ngày lên.
 */
@Injectable({ providedIn: 'root' })
export class MoodService {
  private readonly apiUrl = `${environment.apiUrl}/mood`;

  constructor(private http: HttpClient, private authService: AuthService) {}

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    });
  }

  getToday(): Observable<TodayMoodDto> {
    return this.http.get<TodayMoodDto>(`${this.apiUrl}/today`, { headers: this.getHeaders() });
  }

  /** Lịch sử theo ngày VN; bỏ trống thì server dùng 90 ngày gần nhất. */
  getHistory(from?: string, to?: string): Observable<MoodHistoryDto> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<MoodHistoryDto>(`${this.apiUrl}/history`, { headers: this.getHeaders(), params });
  }

  setMood(mood: MoodState): Observable<void> {
    return this.http.post<void>(this.apiUrl, { Mood: mood }, { headers: this.getHeaders() });
  }

  /** Đóng dấu đã bấm qua lớp phủ Hàng đợi quyết định. */
  markOverride(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/override`, {}, { headers: this.getHeaders() });
  }
}
