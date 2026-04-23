import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

/**
 * Điểm Kỷ luật Thesis — hybrid formula (§D6 plan Vin-discipline).
 * Composite = SL-Integrity 50% + Plan Quality 30% + Review Timeliness 20%.
 */
export interface DisciplineScoreDto {
  /** Composite 0-100. null khi chưa đủ dữ liệu. */
  overall: number | null;
  /** "Kỷ luật Vin" | "Cần cải thiện" | "Trôi dạt" | "Chưa đủ dữ liệu" */
  label: string;
  components: {
    slIntegrity: number | null;
    planQuality: number | null;
    reviewTimeliness: number | null;
  };
  primitives: {
    stopHonorRate: {
      value: number;  // 0..1, -1 khi không có mẫu
      hit: number;
      total: number;
    };
  };
  sampleSize: {
    totalPlans: number;
    closedLossTrades: number;
    daysObserved: number;
  };
  generatedAt: string;
}

export type DisciplinePeriod = 7 | 30 | 90 | 365;

@Injectable({ providedIn: 'root' })
export class DisciplineService {
  private readonly API_URL = `${environment.apiUrl}/me`;

  constructor(private http: HttpClient, private authService: AuthService) {}

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    });
  }

  getScore(days: DisciplinePeriod = 90): Observable<DisciplineScoreDto> {
    return this.http
      .get<DisciplineScoreDto>(`${this.API_URL}/discipline-score?days=${days}`, {
        headers: this.getHeaders(),
      })
      .pipe(catchError((err) => throwError(() => err)));
  }
}
