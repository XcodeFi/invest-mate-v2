import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export type MoodState = 'Calm' | 'Fomo' | 'Fear' | 'Revenge';

export interface TodayMoodDto {
  /** null khi hôm nay chưa chấm. */
  mood: MoodState | null;
  /** Đã bấm "Vẫn xem bây giờ" hôm nay chưa. */
  overrode: boolean;
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

  setMood(mood: MoodState): Observable<void> {
    return this.http.post<void>(this.apiUrl, { Mood: mood }, { headers: this.getHeaders() });
  }

  /** Đóng dấu đã bấm qua lớp phủ Hàng đợi quyết định. */
  markOverride(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/override`, {}, { headers: this.getHeaders() });
  }
}
